using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unity 可序列化的字典包装器。
/// 内部用 List 存储键值对以实现序列化，运行时提供 Dictionary 接口。
/// Nodin 的 DrawListField 会自动绘制内部的 _keys/_values 列表。
/// </summary>
[Serializable]
public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
{
    [SerializeField, HideInInspector] private List<TKey> _keys = new();
    [SerializeField, HideInInspector] private List<TValue> _values = new();

    public void OnBeforeSerialize()
    {
        _keys.Clear();
        _values.Clear();
        foreach (var kvp in this)
        {
            _keys.Add(kvp.Key);
            _values.Add(kvp.Value);
        }
    }

    public void OnAfterDeserialize()
    {
        Clear();
        int count = Mathf.Min(_keys.Count, _values.Count);
        for (int i = 0; i < count; i++)
        {
            if (!ContainsKey(_keys[i]))
                this[_keys[i]] = _values[i];
        }
    }
}
