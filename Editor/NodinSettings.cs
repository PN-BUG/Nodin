// ═══════════════════════════════════════════════════════════════
//  Nodin — 全局样式设置 ScriptableObject
//  存储 Inspector 绘制的可配置样式参数（标签宽度、按钮高度、字号、颜色等）。
//  通过 NodinSettings.Get() 静态访问，NodinDrawer 读取替代硬编码常量。
// ═══════════════════════════════════════════════════════════════

using UnityEditor;
using UnityEngine;
using Nodin;

namespace Nodin.Editor
{
    /// <summary>
    /// Nodin 全局样式设置，通过 ScriptableObject 持久化存储。
    /// 资产路径记录在 EditorPrefs("Nodin.SettingsPath") 中。
    /// </summary>
    [CreateAssetMenu(fileName = "NodinSettings", menuName = "Nodin/Settings")]
    public class NodinSettings : ScriptableObject
    {
        // ── 标签设置 ──────────────────────────────────────

        [FoldoutGroup("标签设置", true), LabelText("默认标签宽度")]
        public float labelWidth = 130f;

        [FoldoutGroup("标签设置"), LabelText("最小标签宽度"), MinValue(40f)]
        public float labelWidthMin = 80f;

        [FoldoutGroup("标签设置"), LabelText("最大标签宽度"), MinValue(100f)]
        public float labelWidthMax = 300f;

        [FoldoutGroup("标签设置"), LabelText("标签宽度内边距"), MinValue(0f)]
        public float labelWidthPadding = 28f;

        // ── 按钮设置 ──────────────────────────────────────

        [FoldoutGroup("按钮设置", true), LabelText("小号按钮高度"), MinValue(12), MaxValue(60)]
        public int buttonHeightSmall = 20;

        [FoldoutGroup("按钮设置"), LabelText("中号按钮高度"), MinValue(12), MaxValue(80)]
        public int buttonHeightMedium = 28;

        [FoldoutGroup("按钮设置"), LabelText("大号按钮高度"), MinValue(12), MaxValue(100)]
        public int buttonHeightLarge = 36;

        // ── 下拉设置 ──────────────────────────────────────

        [FoldoutGroup("下拉设置", true), LabelText("下拉列表高度"), MinValue(12), MaxValue(60)]
        public int dropdownHeight = 20;

        // ── 字体设置 ──────────────────────────────────────

        [FoldoutGroup("字体设置", true), LabelText("标签字号"), Range(8, 24)]
        public int fontSize = 12;

        [FoldoutGroup("字体设置"), LabelText("分组标题字号"), Range(10, 28)]
        public int groupHeaderFontSize = 14;

        // ── 颜色设置 ──────────────────────────────────────

        [FoldoutGroup("颜色设置", true), LabelText("文本颜色")]
        public Color textColor = new Color(0.847f, 0.851f, 0.882f, 1f);

        [FoldoutGroup("颜色设置"), LabelText("标签颜色")]
        public Color labelColor = new Color(0.847f, 0.851f, 0.882f, 1f);

        [FoldoutGroup("颜色设置"), LabelText("强调色（侧边栏）")]
        public Color accentColor = new Color(0.3f, 0.55f, 0.95f, 0.9f);

        [FoldoutGroup("颜色设置"), LabelText("顶层分组背景色")]
        public Color groupHeaderBg = new Color(0.26f, 0.52f, 0.88f, 0.18f);

        [FoldoutGroup("颜色设置"), LabelText("子分组背景色")]
        public Color groupSubHeaderBg = new Color(0.22f, 0.22f, 0.24f, 0.6f);

        [FoldoutGroup("颜色设置"), LabelText("折叠箭头颜色")]
        public Color arrowColor = new Color(0.6f, 0.65f, 0.75f, 1f);

        // ── 静态访问 ──────────────────────────────────────

        private const string PrefKey = "Nodin.SettingsPath";
        private static NodinSettings _cached;

        /// <summary>加载当前设置 SO（带缓存）。路径不存在时返回 null，调用方回退到默认值。</summary>
        public static NodinSettings Get()
        {
            if (_cached != null) return _cached;

            var path = EditorPrefs.GetString(PrefKey, "");
            if (!string.IsNullOrEmpty(path))
                _cached = AssetDatabase.LoadAssetAtPath<NodinSettings>(path);

            return _cached;
        }

        /// <summary>设置 SO 资产路径并清空缓存（保存后调用）。</summary>
        public static void SetPath(string path)
        {
            EditorPrefs.SetString(PrefKey, path);
            _cached = null;
        }

        /// <summary>清除缓存（SO 被修改后强制重新加载）。</summary>
        public static void InvalidateCache()
        {
            _cached = null;
        }

        // ── 安全访问器（带默认值回退）──────────────────────

        public static float LabelWidth => Get()?.labelWidth ?? 130f;
        public static float LabelWidthMin => Get()?.labelWidthMin ?? 80f;
        public static float LabelWidthMax => Get()?.labelWidthMax ?? 300f;
        public static float LabelWidthPadding => Get()?.labelWidthPadding ?? 28f;
        public static int ButtonHeightSmall => Get()?.buttonHeightSmall ?? 20;
        public static int ButtonHeightMedium => Get()?.buttonHeightMedium ?? 28;
        public static int ButtonHeightLarge => Get()?.buttonHeightLarge ?? 36;
        public static int DropdownHeight => Get()?.dropdownHeight ?? 20;
        public static int FontSize => Get()?.fontSize ?? 12;
        public static int GroupHeaderFontSize => Get()?.groupHeaderFontSize ?? 14;
        public static Color TextColor => Get()?.textColor ?? new Color(0.847f, 0.851f, 0.882f, 1f);
        public static Color LabelColor => Get()?.labelColor ?? new Color(0.847f, 0.851f, 0.882f, 1f);
        public static Color AccentColor => Get()?.accentColor ?? new Color(0.3f, 0.55f, 0.95f, 0.9f);
        public static Color GroupHeaderBg => Get()?.groupHeaderBg ?? new Color(0.26f, 0.52f, 0.88f, 0.18f);
        public static Color GroupSubHeaderBg => Get()?.groupSubHeaderBg ?? new Color(0.22f, 0.22f, 0.24f, 0.6f);
        public static Color ArrowColor => Get()?.arrowColor ?? new Color(0.6f, 0.65f, 0.75f, 1f);
    }
}
