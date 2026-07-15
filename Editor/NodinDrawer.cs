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

        // ── 字典折叠状态 ──
        private readonly Dictionary<string, bool> _dictFoldouts = new();

        // ── Dictionary 字段列表（用于自动序列化）──
        private readonly FieldInfo[] _dictFields;

        public NodinDrawer(object target)
        {
            _target = target;
            _type = target.GetType();
            // ── 收集字段并缓存所有 Attribute ──
            var fields = _type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(f => f.IsPublic
                    || f.GetCustomAttribute<ShowInInspectorAttribute>() != null
                    || f.GetCustomAttribute<SerializeField>() != null)
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

            DrawUngroupedFields();
            DrawUngroupedButtons();

            foreach (var groupName in _orderedTopGroups)
            {
                DrawTopGroup(groupName);
            }

            // ── Dictionary 保存到序列化备份 ──
            DictSerializationHelper.SaveAll(_target, _dictFields);
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

                // 可序列化容器（如 SerializableDictionary）编辑后需同步内部列表
                if (newValue is ISerializationCallbackReceiver cb)
                    cb.OnBeforeSerialize();

                // Dictionary 字段：立即同步到序列化备份
                if (fm.Field.FieldType.IsGenericType
                    && fm.Field.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    DictSerializationHelper.SaveField(_target, fm.Field);
                }

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

            // Dictionary<TKey, TValue>
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var settingsAttr = fm.Field?.GetCustomAttribute<DictionaryDrawerSettingsAttribute>();
                DrawDictField(label, value, type, hideLabel, settingsAttr);
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

        // ── Dictionary 绘制 ───────────────────────────────

        private void DrawDictField(string label, object value, Type type, bool hideLabel, DictionaryDrawerSettingsAttribute settings)
        {
            if (value == null) return;
            var keyType = type.GetGenericArguments()[0];
            var valType = type.GetGenericArguments()[1];
            var dict = (System.Collections.IDictionary)value;

            string keyLabel = settings?.KeyLabel ?? "Key";
            string valLabel = settings?.ValueLabel ?? "Value";

            // 折叠状态
            string foldKey = $"{label}_{type.FullName}";
            if (!_dictFoldouts.ContainsKey(foldKey))
                _dictFoldouts[foldKey] = true;
            bool expanded = _dictFoldouts[foldKey];

            // ── 标题行（复用 DrawGroupHeader 样式）──
            DrawGroupHeader($"{label}  ({dict.Count})", expanded, isSubGroup: false, out var toggled);
            if (toggled)
            {
                _dictFoldouts[foldKey] = !expanded;
                expanded = !expanded;
            }

            // 右上角添加按钮（覆盖在标题行右侧）
            var lastRect = GUILayoutUtility.GetLastRect();
            var btnRect = new Rect(lastRect.xMax - 28, lastRect.y + 3, 24, 18);
            bool addClicked = GUI.Button(btnRect, "+", EditorStyles.miniButton);

            if (addClicked)
            {
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

            EditorGUILayout.BeginVertical(EditorStyles.textArea);
            EditorGUI.indentLevel++;

            if (dict.Count == 0)
            {
                EditorGUILayout.LabelField("（空字典）", EditorStyles.centeredGreyMiniLabel);
            }

            var keys = new object[dict.Count];
            dict.Keys.CopyTo(keys, 0);

            foreach (var key in keys)
            {
                var entryVal = dict[key];
                EditorGUILayout.BeginHorizontal();

                if (keyType.IsEnum)
                {
                    EditorGUILayout.LabelField(keyLabel, EditorStyles.miniLabel, GUILayout.Width(60));
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.EnumPopup((Enum)key);
                    EditorGUI.EndDisabledGroup();
                }
                else if (keyType == typeof(string))
                {
                    EditorGUILayout.LabelField(keyLabel, EditorStyles.miniLabel, GUILayout.Width(60));
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.TextField((string)key ?? "");
                    EditorGUI.EndDisabledGroup();
                }
                else
                {
                    EditorGUILayout.LabelField($"{keyLabel}: {key}", GUILayout.Width(80));
                }

                // Value 绘制（带标签）
                EditorGUILayout.LabelField(valLabel, EditorStyles.miniLabel, GUILayout.Width(60));
                if (valType == typeof(bool))
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

                if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(16)))
                {
                    dict.Remove(key);
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
                return "O:id:" + obj.GetInstanceID();
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
            // id:InstanceID (回退)
            if (content.StartsWith("id:"))
            {
                var idStr = content.Substring(3);
                return int.TryParse(idStr, out var id) ? EditorUtility.InstanceIDToObject(id) : null;
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
