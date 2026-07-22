// ═══════════════════════════════════════════════════════════════
//  Nodin — 首次初始化面板
//  检测首次加载，弹出设置面板让用户配置 Inspector 样式。
//  保存时创建 NodinSettings.asset 并写入 EditorPrefs。
// ═══════════════════════════════════════════════════════════════

using UnityEditor;
using UnityEngine;

namespace Nodin.Editor
{
    /// <summary>首次启动检测器 — 编辑器加载时检查是否已初始化</summary>
    [InitializeOnLoad]
    internal static class NodinInitializer
    {
        private const string InitializedKey = "Nodin.Initialized";

        static NodinInitializer()
        {
            EditorApplication.delayCall += CheckFirstLaunch;
        }

        private static void CheckFirstLaunch()
        {
            if (EditorPrefs.GetBool(InitializedKey, false)) return;
            if (EditorApplication.isCompiling) return;

            var win = EditorWindow.GetWindow<NodinInitWindow>(true, "Nodin 初始化设置", true);
            win.minSize = new Vector2(520, 640);
            win.maxSize = new Vector2(520, 640);
            win.ShowUtility();
        }

        public static void MarkInitialized()
        {
            EditorPrefs.SetBool(InitializedKey, true);
        }

        public static bool IsInitialized => EditorPrefs.GetBool(InitializedKey, false);
    }

    /// <summary>
    /// Nodin 首次初始化设置面板。
    /// 展示样式预览 + 可编辑参数，保存为 ScriptableObject 资产。
    /// </summary>
    internal class NodinInitWindow : EditorWindow
    {
        // ── 临时编辑状态（保存前不落盘）──
        private float _labelWidth = 130f;
        private int _btnSmallH = 20;
        private int _btnMediumH = 28;
        private int _btnLargeH = 36;
        private int _dropdownH = 20;
        private int _fontSize = 12;
        private int _groupHeaderFontSize = 14;
        private Color _textColor = new Color(0.847f, 0.851f, 0.882f, 1f);
        private Color _labelColor = new Color(0.847f, 0.851f, 0.882f, 1f);
        private Color _accentColor = new Color(0.3f, 0.55f, 0.95f, 0.9f);
        private Color _groupHeaderBg = new Color(0.26f, 0.52f, 0.88f, 0.18f);
        private Color _groupSubHeaderBg = new Color(0.22f, 0.22f, 0.24f, 0.6f);
        private Color _arrowColor = new Color(0.6f, 0.65f, 0.75f, 1f);

        // ── 预览状态 ──
        private bool _previewFoldout = true;
        private int _previewDropdownIdx = 0;
        private string _previewTextField = "示例文本";
        private bool _previewToggle = true;

        // ── 滚动位置 ──
        private Vector2 _scroll;

        // ── 预览样式缓存 ──
        private GUIStyle _previewLabelStyle;
        private GUIStyle _previewHeaderStyle;

        private void OnEnable()
        {
            // 如果已有 SO，加载其值作为起始
            var existing = NodinSettings.Get();
            if (existing != null)
            {
                _labelWidth = existing.labelWidth;
                _btnSmallH = existing.buttonHeightSmall;
                _btnMediumH = existing.buttonHeightMedium;
                _btnLargeH = existing.buttonHeightLarge;
                _dropdownH = existing.dropdownHeight;
                _fontSize = existing.fontSize;
                _groupHeaderFontSize = existing.groupHeaderFontSize;
                _textColor = existing.textColor;
                _labelColor = existing.labelColor;
                _accentColor = existing.accentColor;
                _groupHeaderBg = existing.groupHeaderBg;
                _groupSubHeaderBg = existing.groupSubHeaderBg;
                _arrowColor = existing.arrowColor;
            }
        }

        private void OnGUI()
        {
            // ── 背景渐变 ──
            var bgRect = new Rect(0, 0, position.width, position.height);
            Drawing.DrawHorizontalGradient(bgRect, Palette.Bg, Palette.RightBg);

            // ── 整体垂直布局（确保 FlexibleSpace 能将底部按钮推至窗口底部）──
            EditorGUILayout.BeginVertical();

            // ── 标题区 ──
            DrawHeader();

            // ── 滚动内容 ──
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));

            // ── 预览区 ──
            DrawPreview();

            // ── 设置区 ──
            DrawSettings();

            EditorGUILayout.Space(10);
            EditorGUILayout.EndScrollView();

            // ── 底部按钮 ──
            DrawFooter();

            EditorGUILayout.EndVertical();
        }

        // ── 标题区 ────────────────────────────────────────

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(GUILayout.Height(80));

            // 渐变背景
            var headerRect = GUILayoutUtility.GetLastRect();
            Drawing.DrawHorizontalGradient(headerRect, new Color(0.16f, 0.22f, 0.36f, 1f), new Color(0.12f, 0.13f, 0.15f, 1f));

            EditorGUILayout.Space(8);

            // 标题
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Palette.TextBright }
            };
            EditorGUILayout.LabelField("Nodin", titleStyle);

            var subStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Palette.TextDim }
            };
            EditorGUILayout.LabelField("轻量级 Odin Inspector 替代方案 — 初始化样式设置", subStyle);

            EditorGUILayout.Space(4);
            EditorGUILayout.EndVertical();
        }

        // ── 预览区 ────────────────────────────────────────

        private void DrawPreview()
        {
            EditorGUILayout.Space(6);

            // 分节标题
            DrawSectionLabel("样式预览");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.Space(4);

            // 预览：分组标题栏
            DrawPreviewGroupHeader();

            if (_previewFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(2);

                // 预览：Label + TextField
                var prevLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = _labelWidth;

                var prevColor = GUI.color;
                GUI.color = _textColor;

                // 自定义 label 样式（字号）
                var labelStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = _fontSize,
                    normal = { textColor = _labelColor }
                };

                // TextField
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("名称", labelStyle, GUILayout.Width(_labelWidth));
                _previewTextField = EditorGUILayout.TextField(_previewTextField);
                EditorGUILayout.EndHorizontal();

                // Toggle
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("启用", labelStyle, GUILayout.Width(_labelWidth));
                _previewToggle = EditorGUILayout.Toggle(_previewToggle);
                EditorGUILayout.EndHorizontal();

                // Dropdown
                var ddHeight = Mathf.Max(16, _dropdownH);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("类型", labelStyle, GUILayout.Width(_labelWidth));
                var ddOptions = new[] { "选项 A", "选项 B", "选项 C" };
                _previewDropdownIdx = EditorGUILayout.Popup(_previewDropdownIdx, ddOptions, GUILayout.Height(ddHeight));
                EditorGUILayout.EndHorizontal();

                GUI.color = prevColor;
                EditorGUIUtility.labelWidth = prevLabelWidth;

                // 预览按钮
                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("小号按钮", GUILayout.Height(_btnSmallH)))
                    GUI.changed = true;
                if (GUILayout.Button("中号按钮", GUILayout.Height(_btnMediumH)))
                    GUI.changed = true;
                if (GUILayout.Button("大号按钮", GUILayout.Height(_btnLargeH)))
                    GUI.changed = true;
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.EndVertical();
        }

        private void DrawPreviewGroupHeader()
        {
            var rect = EditorGUILayout.GetControlRect(GUILayout.Height(26));

            // 背景
            EditorGUI.DrawRect(rect, _groupHeaderBg);

            // 侧边栏
            var barRect = new Rect(rect.x, rect.y, 3, rect.height);
            EditorGUI.DrawRect(barRect, _accentColor);

            // 箭头
            var arrowRect = new Rect(rect.x + 8, rect.y, 16, rect.height);
            var arrow = _previewFoldout ? "▼" : "▶";
            var prevColor = GUI.color;
            GUI.color = _arrowColor;
            GUI.Label(arrowRect, arrow, EditorStyles.miniLabel);
            GUI.color = prevColor;

            // 标题
            var labelRect = new Rect(rect.x + 26, rect.y, rect.width - 26, rect.height);
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = _groupHeaderFontSize,
                normal = { textColor = _labelColor }
            };
            EditorGUI.LabelField(labelRect, "预览分组", headerStyle);

            // 点击切换
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                _previewFoldout = !_previewFoldout;
                Event.current.Use();
            }
        }

        // ── 设置区 ────────────────────────────────────────

        private void DrawSettings()
        {
            EditorGUILayout.Space(8);

            // ── 标签设置 ──
            DrawSectionLabel("标签设置");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _labelWidth = EditorGUILayout.Slider("默认标签宽度", _labelWidth, 60f, 400f);
            EditorGUILayout.EndVertical();

            // ── 按钮设置 ──
            DrawSectionLabel("按钮设置");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _btnSmallH = EditorGUILayout.IntSlider("小号按钮高度", _btnSmallH, 12, 60);
            _btnMediumH = EditorGUILayout.IntSlider("中号按钮高度", _btnMediumH, 12, 80);
            _btnLargeH = EditorGUILayout.IntSlider("大号按钮高度", _btnLargeH, 12, 100);
            EditorGUILayout.EndVertical();

            // ── 下拉设置 ──
            DrawSectionLabel("下拉设置");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _dropdownH = EditorGUILayout.IntSlider("下拉列表高度", _dropdownH, 12, 60);
            EditorGUILayout.EndVertical();

            // ── 字体设置 ──
            DrawSectionLabel("字体设置");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _fontSize = EditorGUILayout.IntSlider("标签字号", _fontSize, 8, 24);
            _groupHeaderFontSize = EditorGUILayout.IntSlider("分组标题字号", _groupHeaderFontSize, 10, 28);
            EditorGUILayout.EndVertical();

            // ── 颜色设置 ──
            DrawSectionLabel("颜色设置");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _textColor = EditorGUILayout.ColorField("文本颜色", _textColor);
            _labelColor = EditorGUILayout.ColorField("标签颜色", _labelColor);
            _accentColor = EditorGUILayout.ColorField("强调色（侧边栏）", _accentColor);
            _groupHeaderBg = EditorGUILayout.ColorField("顶层分组背景色", _groupHeaderBg);
            _groupSubHeaderBg = EditorGUILayout.ColorField("子分组背景色", _groupSubHeaderBg);
            _arrowColor = EditorGUILayout.ColorField("折叠箭头颜色", _arrowColor);
            EditorGUILayout.EndVertical();
        }

        // ── 底部 ──────────────────────────────────────────

        private void DrawFooter()
        {
            // 固定在窗口底部的按钮栏
            var footerHeight = 50f;
            var footerRect = new Rect(0, position.height - footerHeight, position.width, footerHeight);
            EditorGUI.DrawRect(footerRect, Palette.Splitter);

            // 分隔线
            var lineRect = new Rect(0, footerRect.y, position.width, 1);
            EditorGUI.DrawRect(lineRect, Palette.Divider);

            // 按钮栏（使用 GUILayout 固定在底部）
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginHorizontal(GUILayout.Height(footerHeight - 6));
            GUILayout.Space(16);

            // 默认操作 — 使用默认值并关闭（主按钮，强调色背景）
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = Palette.BtnNormal;
            if (GUILayout.Button("使用默认值并关闭", GUILayout.Height(32)))
            {
                SaveSettings(useDefaults: true);
                Close();
            }
            GUI.backgroundColor = prevBg;

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("保存并关闭", GUILayout.Height(32), GUILayout.Width(160)))
            {
                SaveSettings(useDefaults: false);
                Close();
            }

            GUILayout.Space(16);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>窗口被关闭时（包括点 X 关闭）也标记为已初始化，避免反复弹出</summary>
        private void OnDestroy()
        {
            if (!NodinInitializer.IsInitialized)
            {
                SaveSettings(useDefaults: true);
            }
        }

        // ── 辅助 ──────────────────────────────────────────

        private void DrawSectionLabel(string label)
        {
            EditorGUILayout.Space(4);
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, Palette.Divider);
            EditorGUILayout.Space(2);

            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = Palette.TextDim }
            };
            EditorGUILayout.LabelField(label, style);
        }

        private void SaveSettings(bool useDefaults)
        {
            const string assetPath = "Assets/NodinSettings.asset";

            // 查找或创建 SO
            var settings = AssetDatabase.LoadAssetAtPath<NodinSettings>(assetPath);
            if (settings == null)
            {
                settings = CreateInstance<NodinSettings>();
                AssetDatabase.CreateAsset(settings, assetPath);
            }

            if (useDefaults)
            {
                // 重置为默认值 — 用新实例的默认值覆盖
                var fresh = CreateInstance<NodinSettings>();
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fresh), settings);
                DestroyImmediate(fresh);
            }
            else
            {
                settings.labelWidth = _labelWidth;
                settings.buttonHeightSmall = _btnSmallH;
                settings.buttonHeightMedium = _btnMediumH;
                settings.buttonHeightLarge = _btnLargeH;
                settings.dropdownHeight = _dropdownH;
                settings.fontSize = _fontSize;
                settings.groupHeaderFontSize = _groupHeaderFontSize;
                settings.textColor = _textColor;
                settings.labelColor = _labelColor;
                settings.accentColor = _accentColor;
                settings.groupHeaderBg = _groupHeaderBg;
                settings.groupSubHeaderBg = _groupSubHeaderBg;
                settings.arrowColor = _arrowColor;
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            NodinSettings.SetPath(assetPath);
            NodinInitializer.MarkInitialized();

            Debug.Log("[Nodin] 设置已保存到 " + assetPath);
        }
    }
}
