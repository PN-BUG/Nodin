// ═══════════════════════════════════════════════════════════════
//  Nodin — 反射式自动绘制器
//  读取 [FoldoutGroup]、[ShowIf]、[Button]、[LabelText] 等属性，
//  在 OnGUI 中自动绘制所有 public 字段和标记了 [Button] 的方法。
// ═══════════════════════════════════════════════════════════════

using System;
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
        private readonly Type _type;

        // ── 缓存的字段/方法元数据（构造时一次性读取所有 Attribute）──
        private readonly FieldMeta[] _fieldMetas;
        private readonly MethodMeta[] _methodMetas;

        // ── 缓存的分组排序（构造时计算，避免每帧 LINQ 遍历）──
        private readonly List<string> _orderedTopGroups;
        private readonly Dictionary<string, List<string>> _subGroupMap;

        private readonly Dictionary<string, bool> _foldoutStates = new();

        // ── 复用 GUIContent（避免每帧分配）──
        private static readonly GUIContent _cachedLabel = new GUIContent();

        // ── 复用 GUIStyle（DrawListField 中每个列表项）──
        private static GUIStyle _listItemNumStyle;

        public NodinDrawer(object target)
        {
            _target = target;
            _type = target.GetType();

            // ── 收集字段并缓存所有 Attribute ──
            var fields = _type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(f => f.IsPublic || f.GetCustomAttribute<ShowInInspectorAttribute>() != null)
                .Where(f => f.GetCustomAttribute<HideInInspector>() == null || f.GetCustomAttribute<ShowInInspectorAttribute>() != null)
                .ToArray();

            _fieldMetas = new FieldMeta[fields.Length];
            for (int i = 0; i < fields.Length; i++)
                _fieldMetas[i] = FieldMeta.Build(fields[i]);

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

            foreach (var fm in _fieldMetas)
            {
                var topName = fm.TopGroupName;
                if (topName != null && !_orderedTopGroups.Contains(topName))
                {
                    _orderedTopGroups.Add(topName);
                    groupOrders[topName] = fm.FoldoutGroup?.Order ?? 0;
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
                }
            }

            _orderedTopGroups.Sort((a, b) => groupOrders[a].CompareTo(groupOrders[b]));

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
            DrawUngroupedFields();
            DrawUngroupedButtons();

            foreach (var groupName in _orderedTopGroups)
            {
                DrawTopGroup(groupName);
            }
        }

        // ── 顶层分组 ────────────────────────────────────

        private void DrawTopGroup(string groupName)
        {
            if (!_foldoutStates.ContainsKey(groupName))
            {
                var firstField = Array.Find(_fieldMetas, fm => fm.TopGroupName == groupName);
                var expanded = firstField?.FoldoutGroup?.Expanded ?? false;
                _foldoutStates[groupName] = expanded;
            }

            EditorGUILayout.Space(3);

            DrawGroupHeader(groupName, _foldoutStates[groupName], isSubGroup: false, out var toggled);
            if (toggled) _foldoutStates[groupName] = !_foldoutStates[groupName];

            if (_foldoutStates[groupName])
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

            if (!_foldoutStates.ContainsKey(stateKey))
            {
                var firstField = Array.Find(_fieldMetas, fm => fm.FoldoutGroup?.GroupName == fullGroupName);
                var expanded = firstField?.FoldoutGroup?.Expanded ?? true;
                _foldoutStates[stateKey] = expanded;
            }

            EditorGUILayout.Space(2);
            DrawGroupHeader(shortName, _foldoutStates[stateKey], isSubGroup: true, out var toggled);
            if (toggled) _foldoutStates[stateKey] = !_foldoutStates[stateKey];

            if (_foldoutStates[stateKey])
            {
                EditorGUI.indentLevel++;
                DrawFieldsInGroup(fullGroupName);
                DrawButtonsInGroup(fullGroupName);
                EditorGUI.indentLevel--;
            }
        }

        // ── 分组标题栏 ──────────────────────────────────

        private static void DrawGroupHeader(string title, bool expanded, bool isSubGroup, out bool clicked)
        {
            clicked = false;
            var rect = EditorGUILayout.GetControlRect(GUILayout.Height(isSubGroup ? 22 : 26));
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                clicked = true;
                Event.current.Use();
            }

            var bgRect = rect;
            if (isSubGroup)
                EditorGUI.DrawRect(bgRect, new Color(0.22f, 0.22f, 0.24f, 0.6f));
            else
                EditorGUI.DrawRect(bgRect, new Color(0.26f, 0.52f, 0.88f, 0.18f));

            var barRect = new Rect(rect.x, rect.y, 3, rect.height);
            EditorGUI.DrawRect(barRect, isSubGroup ? new Color(0.4f, 0.4f, 0.45f, 0.8f) : new Color(0.3f, 0.55f, 0.95f, 0.9f));

            var arrowRect = new Rect(rect.x + 8, rect.y, 16, rect.height);
            var arrow = expanded ? "▼" : "▶";
            var oldColor = GUI.color;
            GUI.color = new Color(0.6f, 0.65f, 0.75f, 1f);
            GUI.Label(arrowRect, arrow, EditorStyles.miniLabel);
            GUI.color = oldColor;

            var labelRect = new Rect(rect.x + 26, rect.y, rect.width - 26, rect.height);
            EditorGUI.LabelField(labelRect, title, EditorStyles.boldLabel);
        }

        // ── 字段绘制 ──────────────────────────────────────

        private void DrawUngroupedFields()
        {
            foreach (var fm in _fieldMetas)
            {
                if (fm.FoldoutGroup != null) continue;
                if (!ShouldShow(fm)) continue;
                DrawField(fm);
            }
        }

        private void DrawFieldsInGroup(string groupName)
        {
            foreach (var fm in _fieldMetas)
            {
                if (fm.FoldoutGroup == null) continue;
                if (fm.FoldoutGroup.GroupName != groupName) continue;
                if (!ShouldShow(fm)) continue;
                DrawField(fm);
            }
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
            DrawInfoBox(fm);

            var label = fm.Label;
            var enabled = ShouldEnable(fm);
            var prevEnabled = GUI.enabled;
            GUI.enabled = enabled;

            EditorGUI.BeginChangeCheck();

            var fieldType = fm.Field.FieldType;
            var value = fm.Field.GetValue(_target);
            object newValue = DrawFieldByType(label, value, fieldType, fm);

            if (EditorGUI.EndChangeCheck())
            {
                fm.Field.SetValue(_target, newValue);
                InvokeOnValueChanged(fm);

                // 标记已修改并立即保存
                if (_target is UnityEngine.Object unityObj)
                {
                    EditorUtility.SetDirty(unityObj);
                    AssetDatabase.SaveAssetIfDirty(unityObj);
                }
            }

            GUI.enabled = prevEnabled;
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
                    _cachedLabel.tooltip = "";
                    idx = EditorGUILayout.Popup(hideLabel ? GUIContent.none : _cachedLabel, idx, options);
                    return Convert.ChangeType(options[idx], type);
                }
            }

            // 基本类型
            _cachedLabel.text = label;
            _cachedLabel.tooltip = "";

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
            if (type.IsEnum) return EditorGUILayout.EnumPopup(hideLabel ? GUIContent.none : _cachedLabel, (Enum)value);
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return EditorGUILayout.ObjectField(hideLabel ? GUIContent.none : _cachedLabel, (UnityEngine.Object)value, type, true);

            // List<T>
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                DrawListField(label, value, type, hideLabel);
                return value;
            }

            // 兜底
            EditorGUILayout.LabelField(label, value?.ToString() ?? "null");
            return value;
        }

        private void DrawListField(string label, object value, Type type, bool hideLabel)
        {
            if (value == null) return;
            var listType = type.GetGenericArguments()[0];
            var list = (System.Collections.IList)value;

            EditorGUILayout.BeginHorizontal();
            if (!hideLabel)
                EditorGUILayout.LabelField($"{label}  ({list.Count})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+", GUILayout.Width(24), GUILayout.Height(18)))
            {
                object defaultVal = listType.IsValueType ? Activator.CreateInstance(listType) : null;
                list.Add(defaultVal);
                GUI.changed = true;
                return;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical(EditorStyles.textArea);
            EditorGUI.indentLevel++;

            if (list.Count == 0)
            {
                EditorGUILayout.LabelField("（空列表）", EditorStyles.centeredGreyMiniLabel);
            }

            if (_listItemNumStyle == null)
                _listItemNumStyle = new GUIStyle(EditorStyles.miniLabel) { fixedWidth = 28 };

            for (int i = 0; i < list.Count; i++)
            {
                var itemValue = list[i];
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField($"#{i}", _listItemNumStyle);

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
                else
                    EditorGUILayout.LabelField(itemValue?.ToString() ?? "null");

                if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(16)))
                {
                    list.RemoveAt(i);
                    GUI.changed = true;
                    EditorGUILayout.EndHorizontal();
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        // ── 按钮绘制 ──────────────────────────────────────

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
                ButtonSizes.Small => 20,
                ButtonSizes.Medium => 28,
                ButtonSizes.Large => 36,
                _ => 28,
            };

            var enabled = mm.EnableIf == null || EvaluateCondition(mm.EnableIf.MemberName, mm.EnableIf.Value);
            var prevColor = GUI.backgroundColor;
            if (mm.GUIColor != null) GUI.backgroundColor = mm.GUIColor.Color;

            var prevEnabled = GUI.enabled;
            GUI.enabled = enabled;

            if (GUILayout.Button(label, GUILayout.Height(height)))
            {
                var paramStrs = mm.Method.GetParameters();
                var args = new object[paramStrs.Length];
                for (int i = 0; i < paramStrs.Length; i++)
                    args[i] = paramStrs[i].DefaultValue != DBNull.Value ? paramStrs[i].DefaultValue : (paramStrs[i].ParameterType.IsValueType ? Activator.CreateInstance(paramStrs[i].ParameterType) : null);
                mm.Method.Invoke(_target, args);
            }

            GUI.enabled = prevEnabled;
            GUI.backgroundColor = prevColor;
        }

        // ── 辅助 ──────────────────────────────────────────

        private void InvokeOnValueChanged(FieldMeta fm)
        {
            if (fm.OnValueChanged == null) return;
            var method = _type.GetMethod(fm.OnValueChanged.MethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            method?.Invoke(_target, null);
        }

        // ── 字段元数据缓存 ──────────────────────────────────

        private class FieldMeta
        {
            public FieldInfo Field;
            public string Label;
            public FoldoutGroupAttribute FoldoutGroup;
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

            // 缓存的 dropdown 选项
            private string[] _cachedOptions;
            private bool _optionsResolved;

            public static FieldMeta Build(FieldInfo field)
            {
                var fm = new FieldMeta { Field = field };

                fm.FoldoutGroup = field.GetCustomAttribute<FoldoutGroupAttribute>();
                if (fm.FoldoutGroup != null)
                {
                    var name = fm.FoldoutGroup.GroupName;
                    var slash = name.IndexOf('/');
                    fm.TopGroupName = slash >= 0 ? name.Substring(0, slash) : name;
                }

                fm.LabelText = field.GetCustomAttribute<LabelTextAttribute>();
                fm.HideLabel = field.GetCustomAttribute<HideLabelAttribute>();
                fm.Label = fm.LabelText != null ? fm.LabelText.Text : ObjectNames.NicifyVariableName(field.Name);

                fm.ShowIf = field.GetCustomAttribute<ShowIfAttribute>();
                fm.HideIf = field.GetCustomAttribute<HideIfAttribute>();
                fm.EnableIf = field.GetCustomAttribute<EnableIfAttribute>();
                fm.DisableIf = field.GetCustomAttribute<DisableIfAttribute>();
                fm.ReadOnly = field.GetCustomAttribute<ReadOnlyAttribute>();
                fm.FolderPath = field.GetCustomAttribute<FolderPathAttribute>();
                fm.MultiLine = field.GetCustomAttribute<MultiLinePropertyAttribute>();
                fm.ValueDropdown = field.GetCustomAttribute<ValueDropdownAttribute>();
                fm.InfoBoxes = field.GetCustomAttributes<InfoBoxAttribute>().ToArray();
                fm.OnValueChanged = field.GetCustomAttribute<OnValueChangedAttribute>();

                return fm;
            }

            public string[] GetDropdownOptions(NodinDrawer drawer)
            {
                if (_optionsResolved) return _cachedOptions;
                _optionsResolved = true;
                _cachedOptions = drawer.InvokeValueDropdownMember(ValueDropdown.MemberName);
                return _cachedOptions;
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
}
