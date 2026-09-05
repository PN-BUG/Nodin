// ═══════════════════════════════════════════════════════════════
//  Nodin — 反射式自动绘制器
//  读取 [FoldoutGroup]、[ShowIf]、[Button]、[LabelText] 等属性，
//  在 OnGUI 中自动绘制所有 public 字段和标记了 [Button] 的方法。
// ═══════════════════════════════════════════════════════════════

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Nodin;

namespace Nodin.Editor
{
    /// <summary>
    /// 反射式自动绘制器 —— 通过 Attribute 元数据驱动 Inspector 绘制。
    /// 支持 FoldoutGroup 分组折叠 / ShowIf 条件显示 / EnableIf 条件启用 /
    ///       Button 按钮调用 / LabelText 自定义标签 / InfoBox 信息提示 /
    ///       ReadOnly 只读 / MultiLineProperty 多行文本 / FolderPath 文件夹选择
    /// </summary>
    public class NodinDrawer
    {
        private readonly object _target;
        /// <summary>当前绘制目标对象，供外部缓存判断目标是否已更换。</summary>
        public object Target => _target;
        private readonly Type _type;

        // ── 缓存的字段/方法元数据（构造时一次性读取所有 Attribute）──
        private readonly FieldMeta[] _fieldMetas;
        private readonly MethodMeta[] _methodMetas;
        // Button 方法参数只属于 Inspector 临时输入状态，不写入目标对象。
        private readonly Dictionary<MethodInfo, object[]> _buttonParameterValues = new();

        // ── 缓存的分组排序（构造时计算，避免每帧 LINQ 遍历）──
        private readonly List<string> _orderedTopGroups;
        private readonly Dictionary<string, List<string>> _subGroupMap;

        private readonly Dictionary<string, bool> _foldoutStates = new();

        // ── ToggleGroup 状态 ──
        private readonly Dictionary<string, bool> _toggleGroupStates = new();
        private readonly Dictionary<string, bool> _toggleGroupExpanded = new();

        // ── 复用 GUIContent（避免每帧分配）──
        private static readonly GUIContent _cachedLabel = new GUIContent();
        private static readonly GUIContent _cachedDisplayValue = new GUIContent();
        private static GUIStyle _groupHeaderStyle;
        private static int _groupHeaderStyleFontSize = int.MinValue;
        private static Color _groupHeaderStyleColor;
        private static GUIStyle _listGripStyle;
        private static readonly GUIStyle[] _titleStyles = new GUIStyle[6];

        // ── 标签宽度：默认固定宽度，[LabelText(AutoWidth = true)] 时按文字像素自适应 ──
        // 实际值由 NodinSettings SO 提供，回退到硬编码默认值
        private static readonly Dictionary<string, float> _labelWidthCache = new();

        // ── List 拖拽排序状态 ──
        private static string _dragListKey;
        private static int _dragSrcIndex = -1;
        private static int _dragDstIndex = -1;
        private static float _dragMouseOffsetY; // CS0414: 拖拽预留字段，暂未使用
        private static List<Rect> _dragRowRects = new();
        private const float _dragThreshold = 6f; // 拖拽启动阈值（像素）
        // 延迟启动拖拽：MouseDown 在空白处记录候选，MouseDrag 超过阈值后才真正启动
        private static string _pendingDragListKey;
        private static int _pendingDragSrcIndex = -1;
        private static Vector2 _pendingDragStartPos;

        // ── 字典折叠状态 ──
        private readonly Dictionary<string, bool> _dictFoldouts = new();

        // ── 集合分页状态（按列表/字典字段独立保存）──
        private readonly Dictionary<string, int> _collectionPages = new();

        // ── 内联绘制器缓存（按字段名，避免每帧重建丢失折叠状态）──
        private readonly Dictionary<string, NodinDrawer> _inlineDrawerCache = new();

        // ── Dictionary 字段列表（用于自动序列化）──
        private readonly FieldInfo[] _dictFields;

        // ── Undo 目标（UnityEngine.Object 才能 RecordObject）──
        private readonly UnityEngine.Object _undoTarget;

        // 顶层 Unity 对象的普通序列化字段通过 SerializedProperty 绘制，确保
        // Prefab Override、Undo 和脏标记都走 Unity 的标准序列化管线。
        private readonly SerializedObject _serializedObject;
        private readonly Dictionary<string, SerializedProperty> _serializedPropertyCache = new();
        private readonly HashSet<string> _missingSerializedProperties = new();

        public NodinDrawer(object target, UnityEngine.Object undoTarget = null)
        {
            _target = target;
            _undoTarget = undoTarget ?? (target as UnityEngine.Object);
            _type = target.GetType();
            if (target is UnityEngine.Object unityObject)
                _serializedObject = new SerializedObject(unityObject);
            // ── 收集字段并缓存所有 Attribute ──
            var fields = _type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(f => f.IsPublic
                    || f.GetCustomAttribute<ShowInInspectorAttribute>() != null
                    || f.GetCustomAttribute<SerializeField>() != null)
                .Where(f => f.GetCustomAttribute<HideInInspector>() == null || f.GetCustomAttribute<ShowInInspectorAttribute>() != null)
                .ToArray();

            // ── 收集标记了 [ShowInInspector] 的属性 ──
            var showProps = _type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(p => p.GetCustomAttribute<ShowInInspectorAttribute>() != null && p.GetIndexParameters().Length == 0)
                .ToArray();

            _fieldMetas = new FieldMeta[fields.Length + showProps.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                var meta = FieldMeta.Build(fields[i]);
                meta.BindCallbacks(_type);
                _fieldMetas[i] = meta;
            }
            for (int i = 0; i < showProps.Length; i++)
            {
                var meta = FieldMeta.BuildFromProperty(showProps[i]);
                meta.BindCallbacks(_type);
                _fieldMetas[fields.Length + i] = meta;
            }

            // ── 收集方法并缓存所有 Attribute ──
            var methods = _type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.GetCustomAttribute<ButtonAttribute>() != null)
                .ToArray();

            _methodMetas = new MethodMeta[methods.Length];
            for (int i = 0; i < methods.Length; i++)
                _methodMetas[i] = MethodMeta.Build(methods[i]);

            // ── 预计算分组排序 ──
            _orderedTopGroups = new List<string>();
            var groupOrders = new Dictionary<string, int>();
            // 记录每个分组首次出现的插入顺序，用于稳定排序的 tiebreaker
            var groupInsertIndex = new Dictionary<string, int>();
            int insertCounter = 0;

            foreach (var fm in _fieldMetas)
            {
                var topName = fm.TopGroupName;
                if (topName != null && !_orderedTopGroups.Contains(topName))
                {
                    _orderedTopGroups.Add(topName);
                    groupOrders[topName] = fm.FoldoutGroup?.Order ?? 0;
                    groupInsertIndex[topName] = insertCounter++;
                }
            }

            foreach (var mm in _methodMetas)
            {
                if (mm.FoldoutGroup == null) continue;
                var topName = mm.TopGroupName;
                if (!_orderedTopGroups.Contains(topName))
                {
                    _orderedTopGroups.Add(topName);
                    groupOrders[topName] = mm.FoldoutGroup.Order;
                    groupInsertIndex[topName] = insertCounter++;
                }
            }

            // 使用插入顺序作为 tiebreaker，保证 Order 相同时保持代码声明顺序
            _orderedTopGroups.Sort((a, b) =>
            {
                int cmp = groupOrders[a].CompareTo(groupOrders[b]);
                return cmp != 0 ? cmp : groupInsertIndex[a].CompareTo(groupInsertIndex[b]);
            });

            // ── 收集 Dictionary 字段用于自动序列化 ──
            _dictFields = DictSerializationHelper.CollectDictFields(_type);

            // ── 预计算子分组映射 ──
            _subGroupMap = new Dictionary<string, List<string>>();
            foreach (var fm in _fieldMetas)
            {
                if (fm.FoldoutGroup == null) continue;
                var groupName = fm.FoldoutGroup.GroupName;
                var slash = groupName.IndexOf('/');
                if (slash < 0) continue;
                var topName = groupName.Substring(0, slash);
                if (!_subGroupMap.TryGetValue(topName, out var subs))
                {
                    subs = new List<string>();
                    _subGroupMap[topName] = subs;
                }
                if (!subs.Contains(groupName))
                    subs.Add(groupName);
            }
            foreach (var mm in _methodMetas)
            {
                if (mm.FoldoutGroup == null) continue;
                var groupName = mm.FoldoutGroup.GroupName;
                var slash = groupName.IndexOf('/');
                if (slash < 0) continue;
                var topName = groupName.Substring(0, slash);
                if (!_subGroupMap.TryGetValue(topName, out var subs))
                {
                    subs = new List<string>();
                    _subGroupMap[topName] = subs;
                }
                if (!subs.Contains(groupName))
                    subs.Add(groupName);
            }
        }

        public void Draw()
        {
            _serializedObject?.UpdateIfRequiredOrScript();

            // ── Dictionary 恢复序列化备份 ──
            DictSerializationHelper.RestoreAll(_target, _dictFields);

            // ── Script 字段（双击可打开脚本） ──
            if (_target is MonoBehaviour mb)
            {
                var monoScript = MonoScript.FromMonoBehaviour(mb);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField("Script", monoScript, typeof(MonoScript), false);
                EditorGUI.EndDisabledGroup();
            }
            else if (_target is ScriptableObject so)
            {
                var monoScript = MonoScript.FromScriptableObject(so);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField("Script", monoScript, typeof(MonoScript), false);
                EditorGUI.EndDisabledGroup();
            }
            // 普通 [Serializable] 数据对象不显示 Script 引用。
            // 它不是可独立挂载的 Unity 对象，嵌套列表中重复显示只会占用大量纵向空间。

            DrawUngroupedFields();

            foreach (var groupName in _orderedTopGroups)
            {
                if (groupName.StartsWith("__ToggleGroup_"))
                {
                    // ToggleGroup 按代码出现顺序内联绘制
                    var toggleName = groupName.Substring("__ToggleGroup_".Length);
                    DrawToggleGroup(toggleName);
                }
                else
                {
                    DrawTopGroup(groupName);
                }
            }

            // 无分组按钮绘制在所有分组之后，避免按钮出现在数据上方
            DrawUngroupedButtons();

            // ── Dictionary 保存到序列化备份 ──
            DictSerializationHelper.SaveAll(_target, _dictFields);
        }

        // ── 顶层分组 ────────────────────────────────────

        private void DrawTopGroup(string groupName)
        {
            // 同名 ToggleGroup 已包含子分组，跳过避免重复
            if (_toggleGroupStates.ContainsKey(groupName)) return;

            var firstField = Array.Find(_fieldMetas, fm => fm.TopGroupName == groupName);
            bool groupExpanded = GetFoldoutState(groupName, firstField?.FoldoutGroup?.Expanded ?? false);

            EditorGUILayout.Space(3);

            DrawGroupHeader(groupName, groupExpanded, isSubGroup: false, out var toggled);
            if (toggled) groupExpanded = !groupExpanded;
            SetFoldoutState(groupName, groupExpanded);

            if (groupExpanded)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.Space(2);

                DrawFieldsInGroup(groupName);

                if (_subGroupMap.TryGetValue(groupName, out var subGroups))
                {
                    foreach (var sub in subGroups)
                    {
                        DrawSubGroup(sub, groupName);
                    }
                }

                DrawButtonsInGroup(groupName);

                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();
            }
        }

        // ── 子分组 ──────────────────────────────────────

        private void DrawSubGroup(string fullGroupName, string parentGroup)
        {
            var shortName = fullGroupName.Substring(parentGroup.Length + 1);
            var stateKey = fullGroupName;

            var firstField = Array.Find(_fieldMetas, fm => fm.FoldoutGroup?.GroupName == fullGroupName);
            bool expanded = GetFoldoutState(stateKey, firstField?.FoldoutGroup?.Expanded ?? true);

            EditorGUILayout.Space(2);
            DrawGroupHeader(shortName, expanded, isSubGroup: true, out var toggled);
            if (toggled) expanded = !expanded;
            SetFoldoutState(stateKey, expanded);

            if (expanded)
            {
                EditorGUI.indentLevel++;
                DrawFieldsInGroup(fullGroupName);
                DrawButtonsInGroup(fullGroupName);
                EditorGUI.indentLevel--;
            }
        }

        // ── ToggleGroup 绘制 ──────────────────────────────

        /// <summary>收集并绘制所有 ToggleGroup</summary>
        private void DrawAllToggleGroups()
        {
            // 收集所有 ToggleGroup 名称（按首次出现顺序）
            var groupNames = new List<string>();
            foreach (var fm in _fieldMetas)
            {
                if (fm.ToggleGroup == null) continue;
                var name = fm.ToggleGroup.GroupName;
                if (!groupNames.Contains(name))
                    groupNames.Add(name);
            }

            foreach (var name in groupNames)
            {
                DrawToggleGroup(name);
            }
        }

        /// <summary>绘制单个 ToggleGroup：bool 字段作为标题开关，其余字段在开关打开时显示</summary>
        private void DrawToggleGroup(string groupName)
        {
            // 找到 bool 字段（toggle 开关）
            var toggleField = Array.Find(_fieldMetas, fm =>
                fm.ToggleGroup?.GroupName == groupName && fm.FieldType == typeof(bool));

            // 初始化状态
            if (!_toggleGroupStates.ContainsKey(groupName) && toggleField != null)
                _toggleGroupStates[groupName] = (bool)toggleField.GetValue(_target);
            if (!_toggleGroupExpanded.ContainsKey(groupName))
                _toggleGroupExpanded[groupName] = GetFoldoutState($"__toggle_{groupName}", true);

            EditorGUILayout.Space(3);

            // 绘制带 toggle 的标题栏
            var rect = EditorGUILayout.GetControlRect(GUILayout.Height(26));
            var bgRect = rect;
            EditorGUI.DrawRect(bgRect, NodinSettings.GroupHeaderBg);

            var barRect = new Rect(rect.x, rect.y, 3, rect.height);
            EditorGUI.DrawRect(barRect, NodinSettings.AccentColor);

            // 折叠箭头（点击切换展开）
            var arrowRect = new Rect(rect.x + 8, rect.y, 16, rect.height);
            var expanded = _toggleGroupExpanded[groupName];
            Drawing.DrawFoldoutArrow(arrowRect, expanded, NodinSettings.ArrowColor);

            if (Event.current.type == EventType.MouseDown && arrowRect.Contains(Event.current.mousePosition))
            {
                _toggleGroupExpanded[groupName] = !_toggleGroupExpanded[groupName];
                SetFoldoutState($"__toggle_{groupName}", _toggleGroupExpanded[groupName]);
                Event.current.Use();
            }

            // Toggle 开关
            var toggleValue = _toggleGroupStates.ContainsKey(groupName) && _toggleGroupStates[groupName];
            var toggleRect = new Rect(rect.x + 26, rect.y, 20, rect.height);
            var newToggle = EditorGUI.Toggle(toggleRect, toggleValue);

            if (newToggle != toggleValue)
            {
                _toggleGroupStates[groupName] = newToggle;
                if (toggleField != null)
                {
                    RecordUndo($"Nodin: 切换 {groupName}");
                    toggleField.Field.SetValue(_target, newToggle);
                    if (_target is UnityEngine.Object unityObj)
                    {
                        MarkDirty(unityObj);
                    }
                }
            }

            // 标题文字（点击文字也能切换 toggle）
            var labelRect = new Rect(rect.x + 48, rect.y, rect.width - 48, rect.height);
            EditorGUI.LabelField(labelRect, groupName, GetGroupHeaderStyle());

            if (Event.current.type == EventType.MouseDown && labelRect.Contains(Event.current.mousePosition))
            {
                _toggleGroupStates[groupName] = !_toggleGroupStates[groupName];
                if (toggleField != null)
                {
                    RecordUndo($"Nodin: 切换 {groupName}");
                    toggleField.Field.SetValue(_target, _toggleGroupStates[groupName]);
                    if (_target is UnityEngine.Object unityObj2)
                    {
                        MarkDirty(unityObj2);
                    }
                }
                Event.current.Use();
            }

            // 绘制组内字段（bool 开关打开 且 折叠展开时）
            if (_toggleGroupStates[groupName] && _toggleGroupExpanded[groupName])
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.Space(2);

                foreach (var fm in _fieldMetas)
                {
                    if (fm.ToggleGroup?.GroupName != groupName) continue;
                    // 仅跳过作为分组开关的第一个 bool 字段；组内其他 bool 是正常配置项，必须绘制。
                    if (fm == toggleField) continue;
                    if (!ShouldShow(fm)) continue;
                    DrawField(fm);
                }

                // ── 同名 FoldoutGroup 的子分组（如 贴图导入规则/公共参数）──
                if (_subGroupMap.TryGetValue(groupName, out var subGroups))
                {
                    foreach (var sub in subGroups)
                    {
                        DrawSubGroup(sub, groupName);
                    }
                }

                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();
            }
        }

        // ── 分组标题栏 ──────────────────────────────────

        private static Rect DrawGroupHeader(string title, bool expanded, bool isSubGroup, out bool clicked, float rightReservedWidth = 0f)
        {
            clicked = false;
            var rect = EditorGUILayout.GetControlRect(GUILayout.Height(isSubGroup ? 22 : 26));

            // 绘制背景
            if (isSubGroup)
                EditorGUI.DrawRect(rect, NodinSettings.GroupSubHeaderBg);
            else
                EditorGUI.DrawRect(rect, NodinSettings.GroupHeaderBg);

            // 左侧彩色条
            var barRect = new Rect(rect.x, rect.y, 3, rect.height);
            EditorGUI.DrawRect(barRect, isSubGroup ? new Color(0.4f, 0.4f, 0.45f, 0.8f) : NodinSettings.AccentColor);

            // 箭头
            Drawing.DrawFoldoutArrow(
                new Rect(rect.x + 8, rect.y, 16, rect.height),
                expanded,
                NodinSettings.ArrowColor);

            // 标题文字
            var labelRect = new Rect(rect.x + 26, rect.y, rect.width - 26 - (rightReservedWidth > 0 ? rightReservedWidth : 0), rect.height);
            EditorGUI.LabelField(labelRect, title, GetGroupHeaderStyle());

            // 点击检测（整个标题区域，排除右侧按钮区域）
            var clickRect = rightReservedWidth > 0
                ? new Rect(rect.x, rect.y, rect.width - rightReservedWidth, rect.height)
                : rect;

            // 使用 Button 处理点击
            var prevColor = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.001f); // 几乎透明，但仍可接收点击
            if (GUI.Button(clickRect, GUIContent.none))
            {
                clicked = true;
            }
            GUI.color = prevColor;

            return rect;
        }

        // ── 字段绘制 ──────────────────────────────────────

        private void DrawUngroupedFields()
        {
            var drawn = new HashSet<int>();
            for (int i = 0; i < _fieldMetas.Length; i++)
            {
                if (drawn.Contains(i)) continue;
                var fm = _fieldMetas[i];
                if (fm.FoldoutGroup != null) continue;
                if (fm.ToggleGroup != null) continue; // ToggleGroup 由 DrawAllToggleGroups 绘制
                if (fm.HorizontalGroup != null)
                {
                    DrawHorizontalGroupFields(i, fm.HorizontalGroup.GroupName, null, drawn);
                    continue;
                }
                if (!ShouldShow(fm)) continue;
                DrawField(fm);
                drawn.Add(i);
            }
        }

        private void DrawFieldsInGroup(string groupName)
        {
            var drawn = new HashSet<int>();
            for (int i = 0; i < _fieldMetas.Length; i++)
            {
                if (drawn.Contains(i)) continue;
                var fm = _fieldMetas[i];
                if (fm.FoldoutGroup == null) continue;
                if (fm.FoldoutGroup.GroupName != groupName) continue;
                // ── ToggleGroup 联动：当父分组名匹配某个 ToggleGroup 且其开关关闭时，跳过绘制 ──
                if (fm.ToggleGroup == null)
                {
                    var topName = fm.TopGroupName;
                    if (topName != null && _toggleGroupStates.TryGetValue(topName, out var toggled) && !toggled)
                        continue;
                }
                if (fm.HorizontalGroup != null)
                {
                    DrawHorizontalGroupFields(i, fm.HorizontalGroup.GroupName, groupName, drawn);
                    continue;
                }
                if (!ShouldShow(fm)) continue;
                DrawField(fm);
                drawn.Add(i);
            }
        }

        /// <summary>收集并水平绘制同一 HorizontalGroup 的字段</summary>
        private void DrawHorizontalGroupFields(int startIdx, string hGroupName, string foldoutGroupName, HashSet<int> drawn)
        {
            var groupFields = new List<(FieldMeta fm, float width)>();
            for (int j = startIdx; j < _fieldMetas.Length; j++)
            {
                var fm = _fieldMetas[j];
                if (fm.HorizontalGroup == null) continue;
                if (fm.HorizontalGroup.GroupName != hGroupName) continue;
                if (foldoutGroupName != null)
                {
                    if (fm.FoldoutGroup == null || fm.FoldoutGroup.GroupName != foldoutGroupName) continue;
                }
                else
                {
                    if (fm.FoldoutGroup != null) continue;
                }
                groupFields.Add((fm, fm.HorizontalGroup.Width));
                drawn.Add(j);
            }

            EditorGUILayout.BeginHorizontal();
            for (int k = 0; k < groupFields.Count; k++)
            {
                var (fm, width) = groupFields[k];
                if (!ShouldShow(fm)) continue;

                if (width > 0f && width <= 1f)
                    EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                else if (width > 1f)
                    EditorGUILayout.BeginVertical(GUILayout.Width(width));
                else
                    EditorGUILayout.BeginVertical();

                DrawField(fm);

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
        }

        private bool ShouldShow(FieldMeta fm)
        {
            if (fm.ShowIf != null && !EvaluateCondition(fm.ShowIf.MemberName, fm.ShowIf.Value))
                return false;

            if (fm.HideIf != null && EvaluateCondition(fm.HideIf.MemberName, fm.HideIf.Value))
                return false;

            return true;
        }

        private bool ShouldEnable(FieldMeta fm)
        {
            if (fm.EnableIf != null && !EvaluateCondition(fm.EnableIf.MemberName, fm.EnableIf.Value))
                return false;

            if (fm.DisableIf != null && EvaluateCondition(fm.DisableIf.MemberName, fm.DisableIf.Value))
                return false;

            if (fm.ReadOnly != null) return false;

            return true;
        }

        private bool EvaluateCondition(string memberName, object expectedValue)
        {
            if (string.IsNullOrEmpty(memberName)) return true;

            // 表达式条件：支持 [ShowIf("@mode == MyEnum.Value")] 这类更直观的写法。
            // 先保留原有的成员名模式，确保现有 [ShowIf(nameof(member), value)] 不受影响。
            if (TryEvaluateComparisonExpression(memberName, out bool expressionResult))
                return expressionResult.Equals(expectedValue);

            var field = _type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                var actual = field.GetValue(_target);
                return actual?.Equals(expectedValue) ?? expectedValue == null;
            }

            var prop = _type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null)
            {
                var actual = prop.GetValue(_target);
                return actual?.Equals(expectedValue) ?? expectedValue == null;
            }

            var method = _type.GetMethod(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null && method.ReturnType == typeof(bool) && method.GetParameters().Length == 0)
            {
                return (bool)method.Invoke(_target, null);
            }

            return true;
        }

        private void DrawField(FieldMeta fm)
        {
            DrawTitle(fm);
            DrawInfoBox(fm);

            var label = fm.Label;
            var enabled = ShouldEnable(fm);
            var prevEnabled = GUI.enabled;
            // 子 Drawer 必须继承父级的禁用状态；否则 [ReadOnly] 容器内的
            // [Serializable] 对象会重新启用自己的字段。
            GUI.enabled = prevEnabled && enabled;

            // DisplayAsString — 以只读字符串显示
            if (fm.DisplayAsString != null)
            {
                var displayValue = fm.GetValue(_target);
                var strValue = displayValue?.ToString() ?? "null";
                _cachedLabel.text = label;
                _cachedLabel.tooltip = fm.PropertyTooltip?.Tooltip ?? "";
                _cachedDisplayValue.text = strValue;
                _cachedDisplayValue.tooltip = null;
                EditorGUILayout.LabelField(_cachedLabel, _cachedDisplayValue);
                DrawRequiredWarning(fm, displayValue);
                GUI.enabled = prevEnabled;
                return;
            }

            // ── 标签宽度：默认使用固定宽度；[LabelText(AutoWidth = true)] 时按文字像素自适应 ──
            float prevLabelWidth = EditorGUIUtility.labelWidth;
            if (fm.HideLabel == null)
            {
                bool autoW = fm.LabelText != null && fm.LabelText.AutoWidth;
                EditorGUIUtility.labelWidth = autoW ? CalcLabelWidth(label) : NodinSettings.LabelWidth;
            }

            if (TryDrawSerializedField(fm, label))
            {
                DrawRequiredWarning(fm, fm.GetValue(_target));
                EditorGUIUtility.labelWidth = prevLabelWidth;
                GUI.enabled = prevEnabled;
                return;
            }

            EditorGUI.BeginChangeCheck();

            var fieldType = fm.FieldType;
            var value = fm.GetValue(_target);
            object newValue = DrawFieldByType(label, value, fieldType, fm);

            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo($"Nodin: 修改 {fm.FieldName}");

                // MinValue 约束
                if (fm.MinValue != null)
                    newValue = ClampMin(newValue, fm.MinValue.Min);

                // MaxValue 约束
                if (fm.MaxValue != null)
                    newValue = ClampMax(newValue, fm.MaxValue.Max);

                // // Range 约束（同时限制最小值和最大值）
                // if (fm.Range != null)
                // {
                //     newValue = ClampMin(newValue, fm.Range.Min);
                //     newValue = ClampMax(newValue, fm.Range.Max);
                // }

                fm.SetValue(_target, newValue);
                InvokeOnValueChanged(fm);

                // 可序列化容器（如 SerializableDictionary）编辑后需同步内部列表
                if (newValue is ISerializationCallbackReceiver cb)
                    cb.OnBeforeSerialize();

                // Dictionary 字段：立即同步到序列化备份
                if (fm.HasSerializableField && fm.FieldType.IsGenericType
                    && fm.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    DictSerializationHelper.SaveField(_target, fm.Field);
                }

                // 标记已修改并立即保存
                if (_target is UnityEngine.Object unityObj)
                {
                    MarkDirty(unityObj);
                }
            }

            DrawRequiredWarning(fm, value);

            EditorGUIUtility.labelWidth = prevLabelWidth;
            GUI.enabled = prevEnabled;
        }

        private bool GetFoldoutState(string foldoutKey, bool defaultValue)
        {
            if (_foldoutStates.TryGetValue(foldoutKey, out bool value))
            {
                return value;
            }

            value = EditorPrefs.GetBool(GetFoldoutPrefsKey(foldoutKey), defaultValue);
            _foldoutStates[foldoutKey] = value;
            return value;
        }

        private void SetFoldoutState(string foldoutKey, bool value)
        {
            _foldoutStates[foldoutKey] = value;
            EditorPrefs.SetBool(GetFoldoutPrefsKey(foldoutKey), value);
        }

        private string GetFoldoutPrefsKey(string foldoutKey)
        {
            string targetKey = _undoTarget != null
                ? GlobalObjectId.GetGlobalObjectIdSlow(_undoTarget).ToString()
                : _type.AssemblyQualifiedName;
            return $"Nodin.Foldout.{targetKey}.{foldoutKey}";
        }

        private bool TryDrawSerializedField(FieldMeta fm, string label)
        {
            if (!CanDrawWithSerializedProperty(fm))
                return false;

            var property = GetSerializedProperty(fm);
            if (property == null)
                return false;

            _cachedLabel.text = label;
            _cachedLabel.tooltip = fm.PropertyTooltip?.Tooltip ?? string.Empty;
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(property, fm.HideLabel != null ? GUIContent.none : _cachedLabel, false);
            if (EditorGUI.EndChangeCheck())
            {
                _serializedObject.ApplyModifiedProperties();
                InvokeOnValueChanged(fm);
                _serializedObject.UpdateIfRequiredOrScript();
            }

            return true;
        }

        private bool TryEvaluateComparisonExpression(string expression, out bool result)
        {
            result = false;
            if (!expression.StartsWith("@", StringComparison.Ordinal)) return false;

            string body = expression.Substring(1).Trim();
            int flagOperatorIndex = body.IndexOf('&');
            if (flagOperatorIndex >= 0)
            {
                string flagMemberName = body.Substring(0, flagOperatorIndex).Trim();
                string flagText = body.Substring(flagOperatorIndex + 1).Trim();
                object flagActual = GetConditionMemberValue(flagMemberName);
                if (!string.IsNullOrEmpty(flagMemberName) && !string.IsNullOrEmpty(flagText) && flagActual != null && flagActual.GetType().IsEnum)
                {
                    object flag = ParseExpressionValue(flagText, flagActual.GetType());
                    if (flag.GetType() == flagActual.GetType())
                    {
                        result = (Convert.ToUInt64(flagActual) & Convert.ToUInt64(flag)) != 0;
                        return true;
                    }
                }
                return false;
            }

            int operatorIndex = body.IndexOf("==", StringComparison.Ordinal);
            bool isNotEqual = false;
            if (operatorIndex < 0)
            {
                operatorIndex = body.IndexOf("!=", StringComparison.Ordinal);
                isNotEqual = operatorIndex >= 0;
            }
            if (operatorIndex < 0) return false;

            string memberName = body.Substring(0, operatorIndex).Trim();
            string expectedText = body.Substring(operatorIndex + 2).Trim();
            if (string.IsNullOrEmpty(memberName) || string.IsNullOrEmpty(expectedText)) return false;

            object actual = GetConditionMemberValue(memberName);
            if (actual == null) return false;

            object expected = ParseExpressionValue(expectedText, actual.GetType());
            bool equals = actual.Equals(expected);
            result = isNotEqual ? !equals : equals;
            return true;
        }

        private object GetConditionMemberValue(string memberName)
        {
            var field = _type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) return field.GetValue(_target);

            var property = _type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null) return property.GetValue(_target);

            return null;
        }

        private static object ParseExpressionValue(string text, Type valueType)
        {
            text = text.Trim().Trim('"', '\'');
            if (valueType.IsEnum)
            {
                int dotIndex = text.LastIndexOf('.');
                string enumName = dotIndex >= 0 ? text.Substring(dotIndex + 1) : text;
                return Enum.TryParse(valueType, enumName, out object enumValue) ? enumValue : text;
            }

            if (valueType == typeof(bool) && bool.TryParse(text, out bool boolValue)) return boolValue;
            if (valueType == typeof(int) && int.TryParse(text, out int intValue)) return intValue;
            if (valueType == typeof(float) && float.TryParse(text, out float floatValue)) return floatValue;
            return text;
        }

        private SerializedProperty GetSerializedProperty(FieldMeta fm)
        {
            string propertyPath = fm.FieldName;
            if (_serializedPropertyCache.TryGetValue(propertyPath, out var property))
                return property;
            if (_missingSerializedProperties.Contains(propertyPath))
                return null;

            property = _serializedObject.FindProperty(propertyPath);
            if (property == null)
            {
                _missingSerializedProperties.Add(propertyPath);
                return null;
            }

            _serializedPropertyCache[propertyPath] = property;
            return property;
        }

        private bool CanDrawWithSerializedProperty(FieldMeta fm)
        {
            if (_serializedObject == null || !fm.HasSerializableField || fm.Property != null)
                return false;

            // 保留 Nodin 的专用 Drawer 和复杂容器反射路径，避免改变现有 Inspector 体验。
            if (fm.FolderPath != null || fm.MultiLine != null || fm.ValueDropdown != null
                || fm.MinValue != null || fm.MaxValue != null || fm.Range != null
                || fm.DisplayAsString != null)
                return false;

            var type = fm.FieldType;
            if (type.IsArray || type.IsGenericType
                || type.GetCustomAttribute<InlinePropertyAttribute>() != null
                || (!type.IsValueType && type.GetCustomAttribute<SerializableAttribute>() != null))
                return false;

            return IsSimpleUnitySerializedType(type);
        }

        private static bool IsSimpleUnitySerializedType(Type type)
        {
            return type.IsEnum
                || type.IsPrimitive
                || type == typeof(string)
                || type == typeof(Vector2)
                || type == typeof(Vector3)
                || type == typeof(Vector4)
                || type == typeof(Vector2Int)
                || type == typeof(Vector3Int)
                || type == typeof(Color)
                || type == typeof(Rect)
                || type == typeof(RectInt)
                || type == typeof(Bounds)
                || type == typeof(BoundsInt)
                || type == typeof(Quaternion)
                || type == typeof(LayerMask)
                || type == typeof(AnimationCurve)
                || typeof(UnityEngine.Object).IsAssignableFrom(type);
        }

        /// <summary>
        /// 根据标签文字实际像素宽度计算最优标签宽度。
        /// 带缓存，避免每帧重复 CalcSize。
        /// </summary>
        private float CalcLabelWidth(string label)
        {
            if (_labelWidthCache.TryGetValue(label, out float cached))
                return cached;

            _cachedLabel.text = label;
            float textWidth = EditorStyles.label.CalcSize(_cachedLabel).x;
            float width = Mathf.Clamp(textWidth + NodinSettings.LabelWidthPadding + EditorGUI.indentLevel * 14f, NodinSettings.LabelWidthMin, NodinSettings.LabelWidthMax);
            _labelWidthCache[label] = width;
            return width;
        }

        /// <summary>绘制 Title 标题</summary>
        private static void DrawTitle(FieldMeta fm)
        {
            if (fm.Title == null) return;
            var title = fm.Title.TitleText;
            var subtitle = fm.Title.Subtitle;

            EditorGUILayout.Space(2);

            var style = GetTitleStyle(fm.Title.Bold, fm.Title.Alignment);

            if (!string.IsNullOrEmpty(subtitle))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(title, style);
                GUILayout.Label(subtitle, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField(title, style);
            }

            if (fm.Title.HorizontalLine)
            {
                var rect = EditorGUILayout.GetControlRect(false, 1);
                EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
                EditorGUILayout.Space(1);
            }
        }

        private static GUIStyle GetGroupHeaderStyle()
        {
            int fontSize = NodinSettings.GroupHeaderFontSize;
            Color textColor = NodinSettings.LabelColor;
            if (_groupHeaderStyle == null
                || _groupHeaderStyleFontSize != fontSize
                || _groupHeaderStyleColor != textColor)
            {
                _groupHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = fontSize,
                    normal = { textColor = textColor }
                };
                _groupHeaderStyleFontSize = fontSize;
                _groupHeaderStyleColor = textColor;
            }

            return _groupHeaderStyle;
        }

        private static GUIStyle GetTitleStyle(bool bold, TitleAlignment alignment)
        {
            int index = (bold ? 3 : 0) + (int)alignment;
            var style = _titleStyles[index];
            if (style != null)
                return style;

            TextAnchor textAlignment = alignment switch
            {
                TitleAlignment.Center => TextAnchor.MiddleCenter,
                TitleAlignment.Right => TextAnchor.UpperRight,
                _ => TextAnchor.UpperLeft,
            };
            style = new GUIStyle(bold ? EditorStyles.boldLabel : EditorStyles.label)
            {
                alignment = textAlignment
            };
            _titleStyles[index] = style;
            return style;
        }

        private static GUIStyle GetListGripStyle()
        {
            return _listGripStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                padding = new RectOffset(0, 0, 0, 0)
            };
        }

        /// <summary>Required 校验：值为空时显示警告</summary>
        private static void DrawRequiredWarning(FieldMeta fm, object value)
        {
            if (fm.Required == null) return;

            bool isNull = value == null
                || (value is UnityEngine.Object uo && uo == null)
                || (value is string s && string.IsNullOrEmpty(s))
                || (value is System.Collections.ICollection c && c.Count == 0);

            if (isNull)
            {
                var msg = fm.Required.Message ?? $"「{fm.Label}」未赋值";
                EditorGUILayout.HelpBox(msg, MessageType.Warning);
            }
        }

        /// <summary>将数值限制到最小值</summary>
        private static object ClampMin(object value, double min)
        {
            switch (value)
            {
                case int i: return (int)Math.Max(i, min);
                case long l: return (long)Math.Max(l, min);
                case float f: return (float)Math.Max(f, min);
                case double d: return Math.Max(d, min);
                default: return value;
            }
        }

        /// <summary>将数值限制到最大值</summary>
        private static object ClampMax(object value, double max)
        {
            switch (value)
            {
                case int i: return (int)Math.Min(i, max);
                case long l: return (long)Math.Min(l, max);
                case float f: return (float)Math.Min(f, max);
                case double d: return Math.Min(d, max);
                default: return value;
            }
        }

        // ── MonoScript 查找缓存（按类型，避免每帧重复搜索）──
        private static readonly Dictionary<Type, MonoScript> _monoScriptCache = new();

        /// <summary>
        /// 根据类型查找对应的 MonoScript 资源。
        /// 先按类名快速缩小范围搜索，未命中再全量兜底。结果缓存，每种类型只搜索一次。
        /// </summary>
        private static MonoScript FindMonoScriptForType(Type type)
        {
            if (type == null) return null;

            if (_monoScriptCache.TryGetValue(type, out var cached))
                return cached;

            MonoScript result = null;

            // 快速路径：按类名搜索，缩小范围
            var guids = AssetDatabase.FindAssets($"{type.Name} t:Script");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".cs")) continue;
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null) continue;
                if (script.GetClass() == type) { result = script; break; }
                if (script.text.Contains($"class {type.Name}")) { result = script; break; }
            }

            // 兜底：全量扫描（处理文件名与类名完全不同的情况）
            if (result == null)
            {
                var classDecl = $"class {type.Name}";
                var allGuids = AssetDatabase.FindAssets("t:Script");
                foreach (var guid in allGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith(".cs")) continue;
                    var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                    if (script == null) continue;
                    if (script.GetClass() == type) { result = script; break; }
                    if (script.text.Contains(classDecl)) { result = script; break; }
                }
            }

            _monoScriptCache[type] = result;
            return result;
        }

        private void DrawInfoBox(FieldMeta fm)
        {
            if (fm.InfoBoxes == null) return;
            foreach (var info in fm.InfoBoxes)
            {
                if (!string.IsNullOrEmpty(info.VisibleIfMemberName) && !EvaluateCondition(info.VisibleIfMemberName, true))
                    continue;

                var msgType = info.Type switch
                {
                    InfoMessageType.Info => MessageType.Info,
                    InfoMessageType.Warning => MessageType.Warning,
                    InfoMessageType.Error => MessageType.Error,
                    _ => MessageType.None,
                };
                EditorGUILayout.HelpBox(info.Message, msgType);
            }
        }

        private object DrawFieldByType(string label, object value, Type type, FieldMeta fm)
        {
            var hideLabel = fm.HideLabel != null;

            // FolderPath
            if (fm.FolderPath != null && type == typeof(string))
            {
                var path = (string)value;
                EditorGUILayout.BeginHorizontal();
                if (!hideLabel) EditorGUILayout.PrefixLabel(label);
                path = EditorGUILayout.TextField(path ?? "");
                if (GUILayout.Button("📁", GUILayout.Width(28)))
                {
                    var selected = EditorUtility.OpenFolderPanel("选择文件夹", path ?? "", "");
                    if (!string.IsNullOrEmpty(selected))
                    {
                        if (!fm.FolderPath.AbsolutePath && selected.StartsWith(Application.dataPath))
                            path = "Assets" + selected.Substring(Application.dataPath.Length);
                        else
                            path = selected;
                    }
                }
                EditorGUILayout.EndHorizontal();
                return path;
            }

            // MultiLineProperty
            if (fm.MultiLine != null && type == typeof(string))
            {
                var lines = Mathf.Max(1, fm.MultiLine.Lines);
                if (hideLabel)
                    return EditorGUILayout.TextArea((string)value ?? "", GUILayout.MinHeight(lines * 18));
                return EditorGUILayout.TextField(label, (string)value ?? "");
            }

            // ValueDropdown
            if (fm.ValueDropdown != null)
            {
                var options = fm.GetDropdownOptions(this);
                if (options != null && options.Length > 0)
                {
                    var currentStr = value?.ToString() ?? "";
                    var idx = Array.FindIndex(options, o => o == currentStr);
                    if (idx < 0) idx = 0;
                    _cachedLabel.text = label;
                    _cachedLabel.tooltip = fm.PropertyTooltip?.Tooltip ?? "";
                    idx = EditorGUILayout.Popup(hideLabel ? GUIContent.none : _cachedLabel, idx, options);
                    // 枚举类型使用 Enum.Parse 转换，避免 Convert.ChangeType 无法从 string 转枚举的问题。
                    if (type.IsEnum)
                        return Enum.Parse(type, options[idx]);
                    return Convert.ChangeType(options[idx], type);
                }
            }

            // 基本类型
            _cachedLabel.text = label;
            _cachedLabel.tooltip = fm.PropertyTooltip?.Tooltip ?? "";

            if (type == typeof(bool)) return EditorGUILayout.Toggle(hideLabel ? GUIContent.none : _cachedLabel, (bool)value);
            if (type == typeof(int)) return EditorGUILayout.IntField(hideLabel ? GUIContent.none : _cachedLabel, (int)value);
            if (type == typeof(long)) return EditorGUILayout.LongField(hideLabel ? GUIContent.none : _cachedLabel, (long)value);
            if (type == typeof(float)) return EditorGUILayout.FloatField(hideLabel ? GUIContent.none : _cachedLabel, (float)value);
            if (type == typeof(double)) return EditorGUILayout.DoubleField(hideLabel ? GUIContent.none : _cachedLabel, (double)value);
            if (type == typeof(string)) return EditorGUILayout.TextField(hideLabel ? GUIContent.none : _cachedLabel, (string)value ?? "");
            if (type == typeof(Vector2)) return EditorGUILayout.Vector2Field(hideLabel ? "" : label, (Vector2)value);
            if (type == typeof(Vector3)) return EditorGUILayout.Vector3Field(hideLabel ? "" : label, (Vector3)value);
            if (type == typeof(Vector4)) return EditorGUILayout.Vector4Field(hideLabel ? "" : label, (Vector4)value);
            if (type == typeof(Color)) return EditorGUILayout.ColorField(hideLabel ? GUIContent.none : _cachedLabel, (Color)value);
            if (type == typeof(Rect)) return EditorGUILayout.RectField(hideLabel ? GUIContent.none : _cachedLabel, (Rect)value);
            if (type.IsEnum)
            {
                var enumValue = (Enum)value;
                return type.IsDefined(typeof(FlagsAttribute), false)
                    ? EditorGUILayout.EnumFlagsField(hideLabel ? GUIContent.none : _cachedLabel, enumValue)
                    : EditorGUILayout.EnumPopup(hideLabel ? GUIContent.none : _cachedLabel, enumValue);
            }
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return EditorGUILayout.ObjectField(hideLabel ? GUIContent.none : _cachedLabel, (UnityEngine.Object)value, type, true);

            // List<T> / T[]
            if ((type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) || type.IsArray)
            {
                var listSettings = fm.GetAttr<ListDrawerSettingsAttribute>();
                return DrawListField(label, value, type, hideLabel, listSettings);
            }

            // Dictionary<TKey, TValue>
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var settingsAttr = fm.GetAttr<DictionaryDrawerSettingsAttribute>();
                DrawDictField(label, value, type, hideLabel, settingsAttr);
                return value;
            }

            // 可序列化类/结构体 — [InlineProperty] 或 [Serializable] 内联绘制
            if (value != null && (type.GetCustomAttribute<InlinePropertyAttribute>() != null
                || type.GetCustomAttribute<SerializableAttribute>() != null))
            {
                // 折叠头部
                string foldKey = $"__inline_{fm.FieldName}_{type.FullName}";
                bool inlineExpanded = GetFoldoutState(foldKey, true);
                // ReadOnly 只禁止修改数据，不应阻止嵌套对象折叠。
                // 列表标题已有相同处理；内联 Serializable 字段也必须临时恢复 GUI 交互。
                bool previousGuiEnabled = GUI.enabled;
                GUI.enabled = true;
                DrawGroupHeader(label, inlineExpanded, isSubGroup: false, out var inlineToggled);
                GUI.enabled = previousGuiEnabled;
                if (inlineToggled) inlineExpanded = !inlineExpanded;
                SetFoldoutState(foldKey, inlineExpanded);

                if (inlineExpanded)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.Space(2);
                    // 缓存内联绘制器，避免每帧重建丢失折叠等临时状态
                    var fieldKey = fm.FieldName ?? label;
                    if (!_inlineDrawerCache.TryGetValue(fieldKey, out var inlineDrawer)
                        || inlineDrawer.Target != value)
                    {
                        inlineDrawer = new NodinDrawer(value, _undoTarget);
                        _inlineDrawerCache[fieldKey] = inlineDrawer;
                    }
                    inlineDrawer.Draw();
                    EditorGUILayout.Space(2);
                    EditorGUILayout.EndVertical();
                }
                return value;
            }

            // 兜底
            EditorGUILayout.LabelField(label, value?.ToString() ?? "null");
            return value;
        }

        private object DrawListField(string label, object value, Type type, bool hideLabel, ListDrawerSettingsAttribute settings = null)
        {
            bool canDrag = settings?.DraggableItems != false;
            bool canAdd = settings?.HideAddButton != true;
            bool alwaysAddDefault = settings?.AlwaysAddDefaultValue == true;
            int pageSize = settings?.NumberOfItemsPerPage > 0
                ? settings.NumberOfItemsPerPage
                : NodinSettings.CollectionItemsPerPage;
            bool isArray = type.IsArray;
            var listType = isArray ? type.GetElementType() : type.GetGenericArguments()[0];

            // 未初始化的数组/List 也必须绘制空集合和拖放区域，否则用户没有入口添加对象。
            if (value == null)
            {
                value = isArray
                    ? Array.CreateInstance(listType, 0)
                    : Activator.CreateInstance(type);
            }

            bool inlineItems = listType.GetCustomAttribute<InlinePropertyAttribute>() != null
                || listType.GetCustomAttribute<SerializableAttribute>() != null;
            var list = isArray
                ? new List<object>(((Array)value).Cast<object>())
                : (System.Collections.IList)value;

            // ── 折叠头部（复用分组标题样式）──
            string foldKey = $"__list_{label}_{type.FullName}";
            bool expanded = GetFoldoutState(foldKey, list.Count == 0);

            // ReadOnly 应只禁止数据编辑，折叠状态仍应可操作。
            // DrawField 会在 ReadOnly 字段外层禁用 GUI，因此这里临时恢复交互。
            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = true;
            var headerRect = DrawGroupHeader($"{label}  ({list.Count})", expanded, isSubGroup: false, out var toggled, rightReservedWidth: 32);
            GUI.enabled = previousGuiEnabled;
            if (toggled)
            {
                expanded = !expanded;
            }
            SetFoldoutState(foldKey, expanded);

            // 右上角 + 按钮（覆盖在标题行右侧）
            if (canAdd)
            {
                var btnRect = new Rect(headerRect.xMax - 28, headerRect.y + 1, 22, 18);
                if (GUI.Button(btnRect, "+", EditorStyles.miniButton))
                {
                    RecordUndo("Nodin: 添加列表元素");
                    // UnityEngine.Object（例如 AudioClip）不能通过 Activator 构造；
                    // 新增时保留 null 槽位，供 Inspector 指定或拖入资源。
                    bool canCreateDefault = listType.IsValueType
                        || (alwaysAddDefault
                            && !typeof(UnityEngine.Object).IsAssignableFrom(listType)
                            && listType.GetConstructor(Type.EmptyTypes) != null);
                    object defaultVal = canCreateDefault ? Activator.CreateInstance(listType) : null;
                    list.Add(defaultVal);
                    GUI.changed = true;
                }
            }

            if (!expanded) return value;

            DrawCollectionPagination(foldKey, list.Count, pageSize, out int pageStart, out int pageEnd);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.Space(2);
            EditorGUI.indentLevel++;

            if (list.Count == 0)
            {
                EditorGUILayout.LabelField("（空列表）", EditorStyles.centeredGreyMiniLabel);
            }

            // 所有对象列表（无论是否已有元素）均提供统一的追加拖放区域。
            if (typeof(UnityEngine.Object).IsAssignableFrom(listType))
            {
                Rect dropRect = GUILayoutUtility.GetRect(0f, 32f, GUILayout.ExpandWidth(true));
                GUI.Box(dropRect, "拖入对象以追加（支持多选）", EditorStyles.helpBox);

                Event dragEvent = Event.current;
                if (dropRect.Contains(dragEvent.mousePosition))
                {
                    if (dragEvent.type == EventType.DragUpdated)
                    {
                        DragAndDrop.visualMode = HasCompatibleDraggedObject(listType)
                            ? DragAndDropVisualMode.Copy
                            : DragAndDropVisualMode.Rejected;
                        dragEvent.Use();
                    }
                    else if (dragEvent.type == EventType.DragPerform)
                    {
                        int addedCount = 0;
                        foreach (UnityEngine.Object item in DragAndDrop.objectReferences)
                        {
                            if (TryGetDraggedListItem(item, listType, out UnityEngine.Object listItem))
                            {
                                list.Add(listItem);
                                addedCount++;
                            }
                        }

                        if (addedCount > 0)
                        {
                            DragAndDrop.AcceptDrag();
                            RecordUndo("Nodin: 拖入集合元素");
                            GUI.changed = true;
                        }

                        dragEvent.Use();
                    }
                }
            }

            // ── 拖拽事件处理 ──
            var e = Event.current;
            var evtType = e.type;
            bool isDraggingThisList = _dragListKey == foldKey && _dragSrcIndex >= 0;
            bool isPendingThisList = _pendingDragListKey == foldKey && _pendingDragSrcIndex >= 0;

            // 只在拖拽中的列表绘制时清理/收集行 Rect（防止其它列表覆盖）
            if (evtType == EventType.Repaint && isDraggingThisList)
                _dragRowRects.Clear();

            // ── 拖拽过程中更新目标位置 ──
            if (evtType == EventType.MouseDrag && isDraggingThisList)
            {
                _dragDstIndex = _dragSrcIndex;
                var mousePos = e.mousePosition;
                for (int r = 0; r < _dragRowRects.Count; r++)
                {
                    var rr = _dragRowRects[r];
                    if (mousePos.y < rr.yMin) { _dragDstIndex = pageStart + r; break; }
                    if (mousePos.y >= rr.yMin && mousePos.y < rr.yMax)
                    {
                        _dragDstIndex = mousePos.y < rr.yMin + rr.height * 0.5f ? pageStart + r : pageStart + r + 1;
                        break;
                    }
                    if (r == _dragRowRects.Count - 1 && mousePos.y >= rr.yMax)
                    {
                        _dragDstIndex = pageEnd;
                        break;
                    }
                }
                GUI.changed = true;
                e.Use();
            }
            // ── 延迟启动：鼠标移动超过阈值才真正启动拖拽 ──
            else if (evtType == EventType.MouseDrag && isPendingThisList)
            {
                if (Vector2.Distance(e.mousePosition, _pendingDragStartPos) > _dragThreshold)
                {
                    RecordUndo("Nodin: 拖拽排序列表");
                    _dragListKey = _pendingDragListKey;
                    _dragSrcIndex = _pendingDragSrcIndex;
                    _dragDstIndex = _pendingDragSrcIndex;
                    isDraggingThisList = true;
                    _dragMouseOffsetY = 0;
                    _dragRowRects.Clear(); // 清除过时 Rect，等待下次 Repaint 用当前列表的 Rect 填充
                    GUI.changed = true;
                    e.Use();
                }
            }

            bool skipRemaining = false;
            System.Action pendingReorder = null; // 延迟执行排序，避免循环中修改列表导致索引错乱

            for (int i = pageStart; i < pageEnd; i++)
            {
                if (skipRemaining) break;

                // ── 拖拽放下指示线（绘制在目标行上方）──
                if (isDraggingThisList && _dragDstIndex == i && _dragSrcIndex != i)
                {
                    var indRect = GUILayoutUtility.GetRect(0, 2, GUILayout.ExpandWidth(true));
                    var prevColor = GUI.color;
                    GUI.color = new Color(0.2f, 0.6f, 1f, 0.9f);
                    GUI.DrawTexture(indRect, EditorGUIUtility.whiteTexture);
                    GUI.color = prevColor;
                    GUILayout.Space(-2);
                }

                var itemValue = list[i];
                // 每个元素独立容器，便于区分复杂对象及其嵌套列表。
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                // ── 拖拽把手（≡ 视觉提示，不是按钮）──
                if (canDrag)
                {
                    GUILayout.Label("≡", GetListGripStyle(), GUILayout.Width(16), GUILayout.Height(16));
                }

                // ── 字段值绘制 ──
                // 拖拽中的行：半透明 + 禁用交互
                bool isDraggedRow = isDraggingThisList && _dragSrcIndex == i;
                if (isDraggedRow)
                {
                    var c = GUI.color;
                    GUI.color = new Color(c.r, c.g, c.b, 0.45f);
                    GUI.enabled = false;
                }

                bool valueChanged = false;
                EditorGUI.BeginChangeCheck();

                // 可序列化复杂元素内容通常很长（例如单条对话数据），为每个元素提供独立折叠。
                // 默认收起，基础类型和 Unity Object 列表仍保持原有紧凑绘制。
                bool itemExpanded = true;
                if (inlineItems && itemValue != null)
                {
                    string itemFoldKey = $"__list_item_{foldKey}_{i}";
                    itemExpanded = GetFoldoutState(itemFoldKey, false);

                    bool previousItemGuiEnabled = GUI.enabled;
                    GUI.enabled = true;
                    itemExpanded = EditorGUILayout.Foldout(
                        itemExpanded,
                        GetListItemFoldoutTitle(itemValue, i),
                        true,
                        EditorStyles.foldout);
                    GUI.enabled = previousItemGuiEnabled;
                    SetFoldoutState(itemFoldKey, itemExpanded);
                }

                if (listType == typeof(bool))
                    list[i] = EditorGUILayout.Toggle((bool)itemValue);
                else if (listType == typeof(int))
                    list[i] = EditorGUILayout.IntField((int)itemValue);
                else if (listType == typeof(float))
                    list[i] = EditorGUILayout.FloatField((float)itemValue);
                else if (listType == typeof(string))
                    list[i] = EditorGUILayout.TextField((string)itemValue ?? "");
                else if (listType == typeof(Vector3))
                    list[i] = EditorGUILayout.Vector3Field("", (Vector3)itemValue);
                else if (listType == typeof(Vector2))
                    list[i] = EditorGUILayout.Vector2Field("", (Vector2)itemValue);
                else if (listType == typeof(Color))
                    list[i] = EditorGUILayout.ColorField((Color)itemValue, GUILayout.Width(60));
                else if (listType.IsEnum)
                    list[i] = EditorGUILayout.EnumPopup((Enum)itemValue);
                else if (typeof(UnityEngine.Object).IsAssignableFrom(listType))
                    list[i] = EditorGUILayout.ObjectField((UnityEngine.Object)itemValue, listType, true);
                else if (inlineItems && itemValue != null)
                {
                    // 复杂元素的正文改在标题行结束后全宽绘制；这里仅保留折叠标题。
                }
                else
                    EditorGUILayout.LabelField(itemValue?.ToString() ?? "null");

                valueChanged = EditorGUI.EndChangeCheck();
                if (valueChanged) RecordUndo("Nodin: 修改列表元素");

                if (isDraggedRow)
                {
                    GUI.enabled = true;
                    var c = GUI.color;
                    GUI.color = new Color(c.r, c.g, c.b, 1f);
                }

                // ▲ 上移
                using (new EditorGUI.DisabledScope(i == 0))
                {
                    if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(20), GUILayout.Height(16)))
                    {
                        RecordUndo("Nodin: 上移列表元素");
                        (list[i], list[i - 1]) = (list[i - 1], list[i]);
                        GUI.changed = true;
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        skipRemaining = true;
                    }
                }
                if (skipRemaining) continue;
                // ▼ 下移
                using (new EditorGUI.DisabledScope(i == list.Count - 1))
                {
                    if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(20), GUILayout.Height(16)))
                    {
                        RecordUndo("Nodin: 下移列表元素");
                        (list[i], list[i + 1]) = (list[i + 1], list[i]);
                        GUI.changed = true;
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        skipRemaining = true;
                    }
                }
                if (skipRemaining) continue;

                if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(20), GUILayout.Height(16)))
                {
                    RecordUndo("Nodin: 删除列表元素");
                    list.RemoveAt(i);
                    GUI.changed = true;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    skipRemaining = true;
                }
                if (skipRemaining) continue;

                EditorGUILayout.EndHorizontal();

                if (inlineItems && itemValue != null && itemExpanded)
                {
                    EditorGUILayout.Space(2);
                    // 保留元素 Drawer，避免每次 Repaint 都重置内部 List/Foldout 状态。
                    var drawerKey = $"__list_{foldKey}_{i}";
                    if (!_inlineDrawerCache.TryGetValue(drawerKey, out var inlineDrawer)
                        || inlineDrawer.Target != itemValue)
                    {
                        inlineDrawer = new NodinDrawer(itemValue, _undoTarget);
                        _inlineDrawerCache[drawerKey] = inlineDrawer;
                    }
                    inlineDrawer.Draw();
                }

                EditorGUILayout.EndVertical();

                // 记录整行 Rect（仅拖拽中的列表，防止其它列表覆盖）
                if (evtType == EventType.Repaint && isDraggingThisList)
                {
                    var lastRowRect = GUILayoutUtility.GetLastRect();
                    _dragRowRects.Add(lastRowRect);
                }

                // ── 空白区域拖拽检测：MouseDown 没被字段/按钮消费 → 候选拖拽 ──
                // 用 e.type 而非 evtType，因为按钮消费事件后 e.type 会变成 Used
                if (canDrag && e.type == EventType.MouseDown && e.button == 0 && !isDraggingThisList && !isPendingThisList)
                {
                    var lastRow = GUILayoutUtility.GetLastRect();
                    if (lastRow.Contains(e.mousePosition))
                    {
                        _pendingDragListKey = foldKey;
                        _pendingDragSrcIndex = i;
                        _pendingDragStartPos = e.mousePosition;
                        isPendingThisList = true;
                    }
                }

                // ── 拖拽结束 ──
                if (evtType == EventType.MouseUp && (isDraggingThisList || isPendingThisList) && i == pageEnd - 1)
                {
                    if (isDraggingThisList && _dragDstIndex != _dragSrcIndex && _dragDstIndex >= 0)
                    {
                        int src = _dragSrcIndex, dst = _dragDstIndex;
                        pendingReorder = () =>
                        {
                            var obj = list[src];
                            list.RemoveAt(src);
                            int insertAt = dst > src ? dst - 1 : dst;
                            insertAt = Mathf.Clamp(insertAt, 0, list.Count);
                            list.Insert(insertAt, obj);
                        };
                    }
                    _dragListKey = null;
                    _dragSrcIndex = -1;
                    _dragDstIndex = -1;
                    _pendingDragListKey = null;
                    _pendingDragSrcIndex = -1;
                    isDraggingThisList = false;
                    isPendingThisList = false;
                    e.Use();
                }
            }

            // ── 拖拽结束：延迟执行排序（避免循环中修改列表导致索引错乱）──
            if (pendingReorder != null)
            {
                pendingReorder();
                GUI.changed = true;
            }

            // ── 拖拽中：在列表末尾绘制指示线（拖到最后位置时）──
            if (isDraggingThisList && _dragRowRects.Count > 0 && _dragDstIndex >= list.Count)
            {
                var lastRow = _dragRowRects[_dragRowRects.Count - 1];
                var indRect = new Rect(lastRow.x, lastRow.yMax, lastRow.width, 2);
                var prevColor = GUI.color;
                GUI.color = new Color(0.2f, 0.6f, 1f, 0.9f);
                GUI.DrawTexture(indRect, EditorGUIUtility.whiteTexture);
                GUI.color = prevColor;
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);
            EditorGUILayout.EndVertical();
            if (!isArray) return value;

            var array = Array.CreateInstance(listType, list.Count);
            list.CopyTo(array, 0);
            return array;
        }

        private static bool HasCompatibleDraggedObject(Type listType)
        {
            foreach (UnityEngine.Object item in DragAndDrop.objectReferences)
            {
                if (TryGetDraggedListItem(item, listType, out _))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 支持从 Project 拖入组件，也支持从 Hierarchy 拖入其所在 GameObject。
        /// </summary>
        private static bool TryGetDraggedListItem(UnityEngine.Object draggedObject, Type listType, out UnityEngine.Object listItem)
        {
            listItem = null;
            if (draggedObject == null)
            {
                return false;
            }

            if (listType.IsInstanceOfType(draggedObject))
            {
                listItem = draggedObject;
                return true;
            }

            GameObject gameObject = draggedObject as GameObject;
            if (gameObject == null && draggedObject is Component component)
            {
                gameObject = component.gameObject;
            }

            Component matchingComponent = gameObject != null ? gameObject.GetComponent(listType) : null;
            if (matchingComponent == null && gameObject != null)
            {
                matchingComponent = gameObject.GetComponentInChildren(listType, true);
            }
            if (matchingComponent == null)
            {
                return false;
            }

            listItem = matchingComponent;
            return true;
        }

        private static string GetListItemFoldoutTitle(object itemValue, int index)
        {
            string prefix = $"元素 {index}";
            if (itemValue == null)
                return prefix;

            Type itemType = itemValue.GetType();
            string[] preferredNames = { "nodeName", "picName", "displayName", "name", "id", "Id" };
            for (int i = 0; i < preferredNames.Length; i++)
            {
                FieldInfo field = itemType.GetField(
                    preferredNames[i],
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object value = field?.GetValue(itemValue);
                if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
                    return prefix + " · " + value;

                PropertyInfo property = itemType.GetProperty(
                    preferredNames[i],
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.GetIndexParameters().Length == 0)
                    value = property.GetValue(itemValue);
                if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
                    return prefix + " · " + value;
            }

            return prefix;
        }

        // ── Dictionary 绘制 ───────────────────────────────

        private void DrawDictField(string label, object value, Type type, bool hideLabel, DictionaryDrawerSettingsAttribute settings)
        {
            if (value == null) return;
            var keyType = type.GetGenericArguments()[0];
            var valType = type.GetGenericArguments()[1];
            var dict = (System.Collections.IDictionary)value;
            bool inlineValues = valType.GetCustomAttribute<InlinePropertyAttribute>() != null
                || valType.GetCustomAttribute<SerializableAttribute>() != null;
            int pageSize = settings?.NumberOfItemsPerPage > 0
                ? settings.NumberOfItemsPerPage
                : NodinSettings.CollectionItemsPerPage;

            string keyLabel = settings?.KeyLabel ?? "Key";
            string valLabel = settings?.ValueLabel ?? "Value";

            // 折叠状态
            string foldKey = $"{label}_{type.FullName}";
            bool expanded = GetFoldoutState($"__dict_{foldKey}", true);

            // ── 标题行（复用 DrawGroupHeader 样式，右侧预留 + 按钮区域）──
            // 与 List 一致：只读字典仍允许展开/折叠查看内容。
            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = true;
            var headerRect = DrawGroupHeader($"{label}  ({dict.Count})", expanded, isSubGroup: false, out var toggled, rightReservedWidth: 32);
            GUI.enabled = previousGuiEnabled;
            if (toggled)
            {
                expanded = !expanded;
            }
            SetFoldoutState($"__dict_{foldKey}", expanded);

            // 右上角添加按钮（覆盖在标题行右侧）
            var btnRect = new Rect(headerRect.xMax - 28, headerRect.y + 3, 24, 18);
            bool addClicked = GUI.Button(btnRect, "+", EditorStyles.miniButton);

            if (addClicked)
            {
                RecordUndo("Nodin: 添加字典条目");
                if (keyType.IsEnum)
                {
                    var usedKeys = new HashSet<object>();
                    foreach (var k in dict.Keys) usedKeys.Add(k);
                    var allValues = Enum.GetValues(keyType);
                    object firstFree = null;
                    foreach (var ev in allValues)
                    {
                        if (!usedKeys.Contains(ev)) { firstFree = ev; break; }
                    }
                    if (firstFree != null)
                    {
                        object defaultVal = valType.IsValueType ? Activator.CreateInstance(valType) : null;
                        dict[firstFree] = defaultVal;
                        GUI.changed = true;
                    }
                }
                else
                {
                    object defaultKey = keyType.IsValueType ? Activator.CreateInstance(keyType) : null;
                    if (defaultKey != null && !dict.Contains(defaultKey))
                    {
                        object defaultVal = valType.IsValueType ? Activator.CreateInstance(valType) : null;
                        dict[defaultKey] = defaultVal;
                        GUI.changed = true;
                    }
                }
            }

            // 折叠时不显示内容
            if (!expanded) return;

            DrawCollectionPagination(foldKey, dict.Count, pageSize, out int pageStart, out int pageEnd);

            EditorGUILayout.BeginVertical(EditorStyles.textArea);
            EditorGUI.indentLevel++;

            if (dict.Count == 0)
            {
                EditorGUILayout.LabelField("（空字典）", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                // ── 列标题行 ──
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(keyLabel, EditorStyles.boldLabel, GUILayout.Width(80));
                EditorGUILayout.LabelField(valLabel, EditorStyles.boldLabel);
                GUILayout.Space(22); // 为删除按钮预留空间
                EditorGUILayout.EndHorizontal();
            }

            var keys = new object[dict.Count];
            dict.Keys.CopyTo(keys, 0);

            for (int keyIndex = pageStart; keyIndex < pageEnd; keyIndex++)
            {
                var key = keys[keyIndex];
                var entryVal = dict[key];
                bool drawInlineValue = inlineValues && entryVal != null;
                string entryFoldKey = $"{foldKey}:{key}";
                if (drawInlineValue && !_dictFoldouts.ContainsKey(entryFoldKey))
                    _dictFoldouts[entryFoldKey] = GetFoldoutState($"__dict_entry_{entryFoldKey}", false);

                EditorGUILayout.BeginHorizontal();

                if (keyType.IsEnum)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.EnumPopup((Enum)key, GUILayout.Width(80));
                    EditorGUI.EndDisabledGroup();
                }
                else if (keyType == typeof(string))
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.TextField((string)key ?? "", GUILayout.Width(80));
                    EditorGUI.EndDisabledGroup();
                }
                else
                {
                    EditorGUILayout.LabelField($"{key}", GUILayout.Width(80));
                }
                if (drawInlineValue)
                {
                    _dictFoldouts[entryFoldKey] = EditorGUILayout.Foldout(_dictFoldouts[entryFoldKey], valType.Name, true);
                    SetFoldoutState($"__dict_entry_{entryFoldKey}", _dictFoldouts[entryFoldKey]);
                }
                else if (valType == typeof(bool))
                    dict[key] = EditorGUILayout.Toggle((bool)(entryVal ?? false));
                else if (valType == typeof(int))
                    dict[key] = EditorGUILayout.IntField((int)(entryVal ?? 0));
                else if (valType == typeof(float))
                    dict[key] = EditorGUILayout.FloatField((float)(entryVal ?? 0f));
                else if (valType == typeof(string))
                    dict[key] = EditorGUILayout.TextField((string)entryVal ?? "");
                else if (valType == typeof(Vector3))
                    dict[key] = EditorGUILayout.Vector3Field("", (Vector3)(entryVal ?? Vector3.zero));
                else if (valType == typeof(Vector2))
                    dict[key] = EditorGUILayout.Vector2Field("", (Vector2)(entryVal ?? Vector2.zero));
                else if (valType == typeof(Color))
                    dict[key] = EditorGUILayout.ColorField((Color)(entryVal ?? Color.white), GUILayout.Width(60));
                else if (typeof(UnityEngine.Object).IsAssignableFrom(valType))
                    dict[key] = EditorGUILayout.ObjectField((UnityEngine.Object)entryVal, valType, true);
                else
                    EditorGUILayout.LabelField(entryVal?.ToString() ?? "null");

                bool removeClicked = GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(16));
                EditorGUILayout.EndHorizontal();

                if (removeClicked)
                {
                    RecordUndo("Nodin: 删除字典条目");
                    dict.Remove(key);
                    GUI.changed = true;
                    break;
                }

                if (drawInlineValue && _dictFoldouts[entryFoldKey])
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    var drawerKey = $"__dict_{entryFoldKey}";
                    if (!_inlineDrawerCache.TryGetValue(drawerKey, out var inlineDrawer)
                        || inlineDrawer.Target != entryVal)
                    {
                        inlineDrawer = new NodinDrawer(entryVal, _undoTarget);
                        _inlineDrawerCache[drawerKey] = inlineDrawer;
                    }
                    inlineDrawer.Draw();
                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        /// <summary>
        /// 绘制集合分页栏。分页导航不修改集合内容，因此 ReadOnly 集合也允许操作。
        /// </summary>
        private void DrawCollectionPagination(string collectionKey, int itemCount, int pageSize, out int startIndex, out int endIndex)
        {
            pageSize = Mathf.Max(1, pageSize);
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(itemCount / (float)pageSize));
            if (!_collectionPages.TryGetValue(collectionKey, out int page))
                page = 0;
            page = Mathf.Clamp(page, 0, pageCount - 1);
            _collectionPages[collectionKey] = page;

            startIndex = page * pageSize;
            endIndex = Mathf.Min(startIndex + pageSize, itemCount);
            if (pageCount <= 1) return;

            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = true;
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(page == 0))
            {
                if (GUILayout.Button("◀", EditorStyles.miniButtonLeft, GUILayout.Width(26)))
                {
                    _collectionPages[collectionKey] = page - 1;
                    GUI.changed = true;
                }
            }
            GUILayout.Label($"{startIndex + 1}-{endIndex} / {itemCount}  （第 {page + 1}/{pageCount} 页）", EditorStyles.centeredGreyMiniLabel);
            using (new EditorGUI.DisabledScope(page >= pageCount - 1))
            {
                if (GUILayout.Button("▶", EditorStyles.miniButtonRight, GUILayout.Width(26)))
                {
                    _collectionPages[collectionKey] = page + 1;
                    GUI.changed = true;
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUI.enabled = previousGuiEnabled;
        }

        // ── 按钮绘制 ──────────────────────────────────────

        /// <summary>
        /// 仅绘制所有 [Button] 方法，供外部编辑器（如 Odin）共存时补充绘制。
        /// 不绘制字段，避免与外部编辑器重复。
        /// </summary>
        public void DrawButtonsOnly()
        {
            if (_methodMetas == null || _methodMetas.Length == 0) return;
            EditorGUILayout.Space(4);
            foreach (var mm in _methodMetas)
            {
                if (!ShouldShowMethod(mm)) continue;
                DrawButton(mm);
            }
        }

        private void DrawUngroupedButtons()
        {
            foreach (var mm in _methodMetas)
            {
                if (mm.FoldoutGroup != null) continue;
                if (!ShouldShowMethod(mm)) continue;
                DrawButton(mm);
            }
        }

        private void DrawButtonsInGroup(string groupName)
        {
            foreach (var mm in _methodMetas)
            {
                if (mm.FoldoutGroup == null) continue;
                if (mm.FoldoutGroup.GroupName != groupName) continue;
                if (!ShouldShowMethod(mm)) continue;
                DrawButton(mm);
            }
        }

        private bool ShouldShowMethod(MethodMeta mm)
        {
            if (mm.ShowIf != null && !EvaluateCondition(mm.ShowIf.MemberName, mm.ShowIf.Value))
                return false;
            return true;
        }

        private void DrawButton(MethodMeta mm)
        {
            var label = mm.Button.Name ?? mm.LabelText?.Text ?? ObjectNames.NicifyVariableName(mm.Method.Name);
            var height = mm.Button.Size switch
            {
                ButtonSizes.Small => NodinSettings.ButtonHeightSmall,
                ButtonSizes.Medium => NodinSettings.ButtonHeightMedium,
                ButtonSizes.Large => NodinSettings.ButtonHeightLarge,
                _ => NodinSettings.ButtonHeightMedium,
            };

            var enabled = mm.EnableIf == null || EvaluateCondition(mm.EnableIf.MemberName, mm.EnableIf.Value);
            var prevColor = GUI.backgroundColor;
            if (mm.GUIColor != null) GUI.backgroundColor = mm.GUIColor.Color;

            var prevEnabled = GUI.enabled;
            GUI.enabled = prevEnabled && enabled;

            var parameters = mm.Method.GetParameters();
            if (!_buttonParameterValues.TryGetValue(mm.Method, out var args))
            {
                args = new object[parameters.Length];
                for (int i = 0; i < parameters.Length; i++) args[i] = GetDefaultButtonParameterValue(parameters[i]);
                _buttonParameterValues.Add(mm.Method, args);
            }
            if (parameters.Length > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                for (int i = 0; i < parameters.Length; i++)
                    args[i] = DrawButtonParameter(parameters[i], args[i]);
            }
            if (GUILayout.Button(label, GUILayout.Height(height)))
            {
                mm.Method.Invoke(_target, args);
            }
            if (parameters.Length > 0)
                EditorGUILayout.EndVertical();

            GUI.enabled = prevEnabled;
            GUI.backgroundColor = prevColor;
        }

        private static object GetDefaultButtonParameterValue(ParameterInfo parameter)
        {
            return parameter.HasDefaultValue && parameter.DefaultValue != DBNull.Value ? parameter.DefaultValue
                : parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null;
        }

        private static object DrawButtonParameter(ParameterInfo parameter, object value)
        {
            var type = parameter.ParameterType;
            string label = ObjectNames.NicifyVariableName(parameter.Name);
            if (type == typeof(bool)) return EditorGUILayout.Toggle(label, value is bool b && b);
            if (type == typeof(int)) return EditorGUILayout.IntField(label, value is int i ? i : 0);
            if (type == typeof(float)) return EditorGUILayout.FloatField(label, value is float f ? f : 0f);
            if (type == typeof(double)) return EditorGUILayout.DoubleField(label, value is double d ? d : 0d);
            if (type == typeof(string)) return EditorGUILayout.TextField(label, value as string ?? string.Empty);
            if (type.IsEnum) return EditorGUILayout.EnumPopup(label, value as Enum ?? (Enum)Enum.GetValues(type).GetValue(0));
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return EditorGUILayout.ObjectField(label, value as UnityEngine.Object, type, true);
            EditorGUILayout.LabelField(label, $"不支持的参数类型：{type.Name}");
            return value;
        }

        // ── 辅助 ──────────────────────────────────────────

        // ── Undo 辅助 ─────────────────────────────────────

        private void RecordUndo(string name)
        {
            if (_undoTarget != null)
                Undo.RecordObject(_undoTarget, name);
        }

        private static void MarkDirty(UnityEngine.Object target)
        {
            EditorUtility.SetDirty(target);
        }

        private void InvokeOnValueChanged(FieldMeta fm)
        {
            if (fm.OnValueChanged == null) return;
            fm.OnValueChangedMethod?.Invoke(_target, null);

            // OnValueChanged 后清除所有 ValueDropdown 选项缓存，确保嵌套 [Serializable] 类中
            // 依赖字段的下拉列表能正确刷新。
            foreach (var meta in _fieldMetas)
            {
                if (meta.ValueDropdown != null)
                    meta.InvalidateDropdownCache();
            }
        }

        // ── 字段元数据缓存 ──────────────────────────────────

        private class FieldMeta
        {
            public FieldInfo Field;
            public PropertyInfo Property; // [ShowInInspector] 属性
            public string Label;
            public FoldoutGroupAttribute FoldoutGroup;
            public ToggleGroupAttribute ToggleGroup;
            public string TopGroupName;
            public LabelTextAttribute LabelText;
            public HideLabelAttribute HideLabel;
            public ShowIfAttribute ShowIf;
            public HideIfAttribute HideIf;
            public EnableIfAttribute EnableIf;
            public DisableIfAttribute DisableIf;
            public ReadOnlyAttribute ReadOnly;
            public FolderPathAttribute FolderPath;
            public MultiLinePropertyAttribute MultiLine;
            public ValueDropdownAttribute ValueDropdown;
            public InfoBoxAttribute[] InfoBoxes;
            public OnValueChangedAttribute OnValueChanged;
            public MethodInfo OnValueChangedMethod;
            public MinValueAttribute MinValue;
            public MaxValueAttribute MaxValue;
            public RangeAttribute Range;
            public PropertyTooltipAttribute PropertyTooltip;
            public HorizontalGroupAttribute HorizontalGroup;
            public TitleAttribute Title;
            public RequiredAttribute Required;
            public DisplayAsStringAttribute DisplayAsString;

            // ── 统一访问器（兼容 FieldInfo 和 PropertyInfo）──
            public Type FieldType => Property != null ? Property.PropertyType : Field.FieldType;
            public string FieldName => Property != null ? Property.Name : Field.Name;
            public object GetValue(object target) => Property != null ? Property.GetValue(target) : Field.GetValue(target);
            public void SetValue(object target, object value) { if (Property != null && Property.CanWrite) Property.SetValue(target, value); else Field?.SetValue(target, value); }
            public T GetAttr<T>() where T : Attribute => (Property?.GetCustomAttribute<T>() ?? Field?.GetCustomAttribute<T>());
            public bool HasSerializableField => Field != null; // 只有真正的字段才能被 Unity 序列化

            // 缓存的 dropdown 选项
            private string[] _cachedOptions;
            private bool _optionsResolved;

            private void BuildFromMember(MemberInfo member)
            {
                FoldoutGroup = member.GetCustomAttribute<FoldoutGroupAttribute>();
                if (FoldoutGroup != null)
                {
                    var name = FoldoutGroup.GroupName;
                    var slash = name.IndexOf('/');
                    TopGroupName = slash >= 0 ? name.Substring(0, slash) : name;
                }

                // ── BoxGroup 后备：当没有 FoldoutGroup 时，用 BoxGroup 做分组 ──
                if (TopGroupName == null)
                {
                    var boxGroup = member.GetCustomAttribute<BoxGroupAttribute>();
                    if (boxGroup != null)
                    {
                        var name = boxGroup.GroupName;
                        var slash = name.IndexOf('/');
                        TopGroupName = slash >= 0 ? name.Substring(0, slash) : name;
                        FoldoutGroup = new FoldoutGroupAttribute(boxGroup.GroupName);
                    }
                }

                ToggleGroup = member.GetCustomAttribute<ToggleGroupAttribute>();
                if (ToggleGroup != null && TopGroupName == null)
                {
                    TopGroupName = "__ToggleGroup_" + ToggleGroup.GroupName;
                }

                LabelText = member.GetCustomAttribute<LabelTextAttribute>();
                HideLabel = member.GetCustomAttribute<HideLabelAttribute>();
                Label = LabelText != null ? LabelText.Text : ObjectNames.NicifyVariableName(member.Name);

                ShowIf = member.GetCustomAttribute<ShowIfAttribute>();
                HideIf = member.GetCustomAttribute<HideIfAttribute>();
                EnableIf = member.GetCustomAttribute<EnableIfAttribute>();
                DisableIf = member.GetCustomAttribute<DisableIfAttribute>();
                ReadOnly = member.GetCustomAttribute<ReadOnlyAttribute>();
                FolderPath = member.GetCustomAttribute<FolderPathAttribute>();
                MultiLine = member.GetCustomAttribute<MultiLinePropertyAttribute>();
                ValueDropdown = member.GetCustomAttribute<ValueDropdownAttribute>();
                InfoBoxes = member.GetCustomAttributes<InfoBoxAttribute>().ToArray();
                OnValueChanged = member.GetCustomAttribute<OnValueChangedAttribute>();
                MinValue = member.GetCustomAttribute<MinValueAttribute>();
                MaxValue = member.GetCustomAttribute<MaxValueAttribute>();
                Range = member.GetCustomAttribute<RangeAttribute>();
                PropertyTooltip = member.GetCustomAttribute<PropertyTooltipAttribute>();
                HorizontalGroup = member.GetCustomAttribute<HorizontalGroupAttribute>();
                Title = member.GetCustomAttribute<TitleAttribute>();
                Required = member.GetCustomAttribute<RequiredAttribute>();
                DisplayAsString = member.GetCustomAttribute<DisplayAsStringAttribute>();
            }

            public void BindCallbacks(Type targetType)
            {
                if (OnValueChanged != null)
                {
                    OnValueChangedMethod = targetType.GetMethod(
                        OnValueChanged.MethodName,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
            }

            public static FieldMeta Build(FieldInfo field)
            {
                var fm = new FieldMeta { Field = field };
                fm.BuildFromMember(field);
                return fm;
            }

            public static FieldMeta BuildFromProperty(PropertyInfo property)
            {
                var fm = new FieldMeta { Property = property };
                fm.BuildFromMember(property);
                return fm;
            }

            public string[] GetDropdownOptions(NodinDrawer drawer)
            {
                // 不缓存，每次绘制时重新调用方法获取最新选项。
                // 这确保嵌套 [Serializable] 类中 ValueDropdown 依赖其他字段时能正确刷新。
                _cachedOptions = drawer.InvokeValueDropdownMember(ValueDropdown.MemberName);
                return _cachedOptions;
            }

            public void InvalidateDropdownCache()
            {
                _cachedOptions = null;
            }
        }

        // ── 方法元数据缓存 ──────────────────────────────────

        private class MethodMeta
        {
            public MethodInfo Method;
            public ButtonAttribute Button;
            public FoldoutGroupAttribute FoldoutGroup;
            public string TopGroupName;
            public LabelTextAttribute LabelText;
            public GUIColorAttribute GUIColor;
            public EnableIfAttribute EnableIf;
            public ShowIfAttribute ShowIf;

            public static MethodMeta Build(MethodInfo method)
            {
                var mm = new MethodMeta { Method = method };

                mm.Button = method.GetCustomAttribute<ButtonAttribute>();
                mm.FoldoutGroup = method.GetCustomAttribute<FoldoutGroupAttribute>();
                if (mm.FoldoutGroup != null)
                {
                    var name = mm.FoldoutGroup.GroupName;
                    var slash = name.IndexOf('/');
                    mm.TopGroupName = slash >= 0 ? name.Substring(0, slash) : name;
                }
                // ── BoxGroup 后备：当没有 FoldoutGroup 时，用 BoxGroup 做分组 ──
                if (mm.TopGroupName == null)
                {
                    var boxGroup = method.GetCustomAttribute<BoxGroupAttribute>();
                    if (boxGroup != null)
                    {
                        var name = boxGroup.GroupName;
                        var slash = name.IndexOf('/');
                        mm.TopGroupName = slash >= 0 ? name.Substring(0, slash) : name;
                        mm.FoldoutGroup = new FoldoutGroupAttribute(boxGroup.GroupName);
                    }
                }
                mm.LabelText = method.GetCustomAttribute<LabelTextAttribute>();
                mm.GUIColor = method.GetCustomAttribute<GUIColorAttribute>();
                mm.EnableIf = method.GetCustomAttribute<EnableIfAttribute>();
                mm.ShowIf = method.GetCustomAttribute<ShowIfAttribute>();

                return mm;
            }
        }

        // ── ValueDropdown 辅助 ──────────────────────────────

        private string[] InvokeValueDropdownMember(string memberName)
        {
            if (string.IsNullOrEmpty(memberName)) return null;

            var method = _type.GetMethod(memberName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null)
            {
                var result = method.Invoke(method.IsStatic ? null : _target, null);
                return ConvertToStringArray(result);
            }

            var field = _type.GetField(memberName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                var result = field.GetValue(field.IsStatic ? null : _target);
                return ConvertToStringArray(result);
            }

            return null;
        }

        private static string[] ConvertToStringArray(object result)
        {
            if (result == null) return null;
            if (result is string[] arr) return arr;
            if (result is System.Collections.IEnumerable enumerable)
            {
                var list = new List<string>();
                foreach (var item in enumerable)
                    list.Add(item?.ToString() ?? "");
                return list.ToArray();
            }
            return new[] { result.ToString() };
        }
    }

    // ══════════════════════════════════════════════════════════
    //  Dictionary 自动序列化辅助（透明持久化，用户无感）
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 自动为 Dictionary 字段提供持久化（通过 EditorPrefs）。
    /// Draw 前恢复、Draw 后保存，用户无感。
    /// </summary>
    internal static class DictSerializationHelper
    {
        private const string PREFIX = "NodinDict_";

        public static void RestoreAll(object target, FieldInfo[] dictFields)
        {
            if (dictFields == null || target == null) return;
            foreach (var field in dictFields)
                RestoreOne(target, field);
        }

        public static void SaveAll(object target, FieldInfo[] dictFields)
        {
            if (dictFields == null || target == null) return;
            foreach (var field in dictFields)
                SaveOne(target, field);
        }

        public static FieldInfo[] CollectDictFields(Type type)
        {
            return type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(f => f.FieldType.IsGenericType
                    && f.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                .ToArray();
        }

        public static void SaveField(object target, FieldInfo field)
        {
            if (!field.FieldType.IsGenericType) return;
            if (field.FieldType.GetGenericTypeDefinition() != typeof(Dictionary<,>)) return;
            SaveOne(target, field);
        }

        private static string GetKey(object target, FieldInfo field)
        {
            // 使用稳定标识符：场景路径 + GameObject 层级路径 + 组件类型 + 字段名
            // GetInstanceID 在场景切换后会变，不可靠
            string stableId;
            if (target is MonoBehaviour mb && mb != null)
            {
                string scenePath = mb.gameObject.scene.path ?? "";
                string goPath = GetGameObjectPath(mb.gameObject);
                string typeName = mb.GetType().FullName;
                stableId = $"{scenePath}|{goPath}|{typeName}";
            }
            else if (target is ScriptableObject so && so != null)
            {
                stableId = $"SO|{so.GetType().FullName}|{so.name}";
            }
            else
            {
                stableId = target.GetType().FullName + "|" + target.GetHashCode();
            }
            return $"{PREFIX}{stableId}_{field.Name}";
        }

        private static string GetGameObjectPath(GameObject go)
        {
            string path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private static void SaveOne(object target, FieldInfo field)
        {
            var dict = field.GetValue(target) as IDictionary;
            if (dict == null || dict.Count == 0) return;

            var args = field.FieldType.GetGenericArguments();
            var keyType = args[0];
            var valType = args[1];
            var key = GetKey(target, field);

            // 用换行符分隔条目（路径中不会出现换行符）
            var entries = new List<string>();
            foreach (DictionaryEntry entry in dict)
            {
                var kStr = SerializeValue(entry.Key, keyType);
                var vStr = SerializeValue(entry.Value, valType);
                entries.Add(kStr + "||" + vStr);
            }

            EditorPrefs.SetString(key, string.Join("\n", entries));
        }

        private static void RestoreOne(object target, FieldInfo field)
        {
            var dict = field.GetValue(target) as IDictionary;
            if (dict == null || dict.Count > 0) return;

            var key = GetKey(target, field);
            var data = EditorPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(data)) return;

            var args = field.FieldType.GetGenericArguments();
            var keyType = args[0];
            var valType = args[1];
            var lines = data.Split('\n');

            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                var parts = line.Split(new[] { "||" }, 2, StringSplitOptions.None);
                if (parts.Length < 2) continue;
                var k = DeserializeValue(parts[0], keyType);
                var v = DeserializeValue(parts[1], valType);
                if (k != null && !dict.Contains(k))
                    dict[k] = v;
            }
        }

        private static string SerializeValue(object value, Type type)
        {
            if (value == null) return "";
            if (type.IsEnum) return "E:" + Convert.ToInt32(value);
            if (type == typeof(int)) return "I:" + value;
            if (type == typeof(long)) return "L:" + value;
            if (type == typeof(float)) return "F:" + value;
            if (type == typeof(double)) return "D:" + value;
            if (type == typeof(bool)) return "B:" + value;
            if (type == typeof(string)) return "S:" + value;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                var obj = value as UnityEngine.Object;
                if (obj == null) return "O:";
                // Transform/Component: 用场景路径+层级路径稳定标识
                if (obj is Component comp)
                    return "O:scene:" + GetGameObjectPath(comp.gameObject) + "@" + comp.gameObject.scene.path;
                // GameObject: 用场景路径+层级路径
                if (obj is GameObject go)
                    return "O:scene:" + GetGameObjectPath(go) + "@" + go.scene.path;
                // Asset: 用资源路径
                var assetPath = UnityEditor.AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(assetPath))
                    return "O:asset:" + assetPath;
#pragma warning disable CS0618 // InstanceID 仍可用于序列化回退
                return "O:id:" + obj.GetInstanceID();
#pragma warning restore CS0618
            }
            return "X:" + value;
        }

        private static object DeserializeValue(string str, Type type)
        {
            if (string.IsNullOrEmpty(str)) return type.IsValueType ? Activator.CreateInstance(type) : null;

            // 解析类型前缀
            var colonIdx = str.IndexOf(':');
            if (colonIdx < 0) return ParseBasic(str, type);
            var prefix = str.Substring(0, colonIdx);
            var content = str.Substring(colonIdx + 1);

            switch (prefix)
            {
                case "I": return int.TryParse(content, out var i) ? i : 0;
                case "F": return float.TryParse(content, out var f) ? f : 0f;
                case "L": return long.TryParse(content, out var l) ? l : 0L;
                case "D": return double.TryParse(content, out var d) ? d : 0.0;
                case "B": return bool.TryParse(content, out var b) && b;
                case "S": return content;
                case "E": return int.TryParse(content, out var ei) ? Enum.ToObject(type, ei) : Activator.CreateInstance(type);
                case "O": return DeserializeObject(content, type);
                default: return ParseBasic(str, type);
            }
        }

        private static object ParseBasic(string str, Type type)
        {
            if (type == typeof(int)) return int.TryParse(str, out var i) ? i : 0;
            if (type == typeof(float)) return float.TryParse(str, out var f) ? f : 0f;
            if (type.IsEnum) return int.TryParse(str, out var ei) ? Enum.ToObject(type, ei) : Activator.CreateInstance(type);
            return null;
        }

        private static object DeserializeObject(string content, Type type)
        {
            // scene:GameObjPath@ScenePath
            if (content.StartsWith("scene:"))
            {
                var parts = content.Substring(6).Split('@');
                var goPath = parts[0];
                var scenePath = parts.Length > 1 ? parts[1] : "";
                var go = FindGameObjectByPath(goPath, scenePath);
                if (go == null) return null;
                if (typeof(Transform).IsAssignableFrom(type)) return go.transform;
                if (type == typeof(GameObject)) return go;
                return go.GetComponent(type);
            }
            // asset:AssetPath
            if (content.StartsWith("asset:"))
            {
                var assetPath = content.Substring(6);
                return UnityEditor.AssetDatabase.LoadAssetAtPath(assetPath, type);
            }
            // id:InstanceID (legacy 回退)
            if (content.StartsWith("id:"))
            {
                var idStr = content.Substring(3);
#pragma warning disable CS0618 // InstanceIDToObject 仍可用于反序列化回退
                return int.TryParse(idStr, out var id) ? EditorUtility.InstanceIDToObject(id) : null;
#pragma warning restore CS0618
            }
            return null;
        }

        private static GameObject FindGameObjectByPath(string goPath, string scenePath)
        {
            // 优先在指定场景中查找
            if (!string.IsNullOrEmpty(scenePath))
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByPath(scenePath);
                if (scene.IsValid())
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        if (root.name + "/" == goPath.Substring(0, root.name.Length + 1) || root.name == goPath)
                        {
                            var target = root.transform.Find(goPath.Substring(root.name.Length).TrimStart('/'));
                            if (target != null) return target.gameObject;
                            if (root.name == goPath) return root;
                        }
                    }
                }
            }
            // 回退：在所有场景中查找
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name == goPath) return root;
                    var target = root.transform.Find(goPath.Substring(root.name.Length).TrimStart('/'));
                    if (target != null) return target.gameObject;
                }
            }
            return null;
        }
    }
}
