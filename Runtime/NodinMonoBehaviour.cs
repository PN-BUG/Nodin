using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Nodin
{
    /// <summary>
    /// 支持 Nodin 属性自动绘制的 MonoBehaviour 基类。
    /// 继承此类即可在 Inspector 中使用 [LabelText]、[FoldoutGroup]、[DictionaryDrawerSettings] 等 Nodin 属性。
    /// 自动处理 Dictionary 字段的序列化（Unity 原生不支持 Dictionary 序列化）。
    /// </summary>
    public abstract class NodinMonoBehaviour : MonoBehaviour, ISerializationCallbackReceiver
    {
        // ── 序列化存储：将所有 Dictionary 拆成扁平列表持久化 ──
        [SerializeField, HideInInspector] private List<string> _dictFieldNames = new();
        // Key 用 JSON 字符串存储（支持 enum / int / string 等值类型 Key）
        [SerializeField, HideInInspector] private List<string> _dictKeyJsons = new();
        // Value 为 UnityEngine.Object 引用时直接存储引用；否则用 JSON
        [SerializeField, HideInInspector] private List<UnityEngine.Object> _dictValueObjects = new();
        [SerializeField, HideInInspector] private List<string> _dictValueJsons = new();
        // 标记每条记录的 Value 是否为 Object 引用
        [SerializeField, HideInInspector] private List<bool> _dictValueIsObject = new();

        public void OnBeforeSerialize()
        {
            _dictFieldNames.Clear();
            _dictKeyJsons.Clear();
            _dictValueObjects.Clear();
            _dictValueJsons.Clear();
            _dictValueIsObject.Clear();

            var fields = GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                if (field.GetCustomAttribute<HideInInspector>() != null) continue;
                if (!field.FieldType.IsGenericType) continue;
                if (field.FieldType.GetGenericTypeDefinition() != typeof(Dictionary<,>)) continue;

                var dict = field.GetValue(this) as IDictionary;
                if (dict == null || dict.Count == 0) continue;

                bool valueIsObject = typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType.GetGenericArguments()[1]);

                foreach (DictionaryEntry entry in dict)
                {
                    _dictFieldNames.Add(field.Name);
                    _dictKeyJsons.Add(JsonUtility.ToJson(entry.Key));
                    _dictValueIsObject.Add(valueIsObject);
                    if (valueIsObject)
                    {
                        _dictValueObjects.Add(entry.Value as UnityEngine.Object);
                        _dictValueJsons.Add(string.Empty);
                    }
                    else
                    {
                        _dictValueObjects.Add(null);
                        _dictValueJsons.Add(entry.Value != null ? JsonUtility.ToJson(entry.Value) : string.Empty);
                    }
                }
            }
        }

        public void OnAfterDeserialize()
        {
            if (_dictFieldNames.Count == 0) return;

            var fields = GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var fieldMap = new Dictionary<string, FieldInfo>();
            foreach (var f in fields)
                fieldMap[f.Name] = f;

            // 按字段名分组
            var groups = new Dictionary<string, List<int>>();
            for (int i = 0; i < _dictFieldNames.Count; i++)
            {
                var name = _dictFieldNames[i];
                if (!groups.ContainsKey(name))
                    groups[name] = new List<int>();
                groups[name].Add(i);
            }

            foreach (var group in groups)
            {
                if (!fieldMap.TryGetValue(group.Key, out var field)) continue;
                if (!field.FieldType.IsGenericType) continue;
                if (field.FieldType.GetGenericTypeDefinition() != typeof(Dictionary<,>)) continue;

                var keyType = field.FieldType.GetGenericArguments()[0];
                var valType = field.FieldType.GetGenericArguments()[1];
                var dict = Activator.CreateInstance(field.FieldType) as IDictionary;

                foreach (int idx in group.Value)
                {
                    try
                    {
                        var key = JsonUtility.FromJson(_dictKeyJsons[idx], keyType);
                        if (key == null || dict.Contains(key)) continue;

                        if (_dictValueIsObject[idx])
                            dict[key] = _dictValueObjects[idx];
                        else if (!string.IsNullOrEmpty(_dictValueJsons[idx]))
                            dict[key] = JsonUtility.FromJson(_dictValueJsons[idx], valType);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[NodinMonoBehaviour] Dictionary 反序列化失败: {group.Key} → {e.Message}");
                    }
                }

                field.SetValue(this, dict);
            }

            _dictFieldNames.Clear();
            _dictKeyJsons.Clear();
            _dictValueObjects.Clear();
            _dictValueJsons.Clear();
            _dictValueIsObject.Clear();
        }
    }
}
