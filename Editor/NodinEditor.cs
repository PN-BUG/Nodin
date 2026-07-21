// ═══════════════════════════════════════════════════════════════
//  Nodin — Editor 桩类型
//  NodinEditorWindow / NodinEditor / ValueDropdown 辅助类型
// ═══════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Nodin;

namespace Nodin.Editor
{
    /// <summary>
    /// NodinEditorWindow 桩 —— 通过反射自动绘制 Inspector。
    /// 子类无需手写 OnGUI，OnEnable 中自动初始化绘制器。
    /// </summary>
    public class NodinEditorWindow : EditorWindow
    {
        private NodinDrawer _drawer;

        protected virtual void OnEnable()
        {
            _drawer = new NodinDrawer(this);
        }

        protected virtual void OnDisable() { }

        private void OnGUI()
        {
            _drawer?.Draw();
        }
    }

    /// <summary>ValueDropdownItem 桩</summary>
    public struct ValueDropdownItem<T>
    {
        public string Text { get; }
        public T Value { get; }
        public ValueDropdownItem(string text, T value) { Text = text; Value = value; }
    }

    /// <summary>ValueDropdownList 桩</summary>
    public class ValueDropdownList<T> : List<ValueDropdownItem<T>>
    {
        public void Add(string name, T value) => Add(new ValueDropdownItem<T>(name, value));
    }

    /// <summary>
    /// 通用 ScriptableObject 编辑器桩。
    /// 无 Odin 时通过 NodinDrawer 反射自动绘制 Inspector。
    /// </summary>
    [CustomEditor(typeof(ScriptableObject), true)]
    public class NodinEditor : UnityEditor.Editor
    {
        private NodinDrawer _drawer;

        private void OnEnable()
        {
            _drawer = new NodinDrawer(target, target);
        }

        public override void OnInspectorGUI()
        {
            _drawer?.Draw();
        }
    }

    /// <summary>
    /// NodinMonoBehaviour 编辑器桩。
    /// 继承 NodinMonoBehaviour 的类型自动获得 Nodin 属性绘制支持。
    /// </summary>
    [CustomEditor(typeof(NodinMonoBehaviour), true)]
    public class NodinMonoBehaviourEditor : UnityEditor.Editor
    {
        private NodinDrawer _drawer;

        private void OnEnable()
        {
            _drawer = new NodinDrawer(target, target);
        }

        public override void OnInspectorGUI()
        {
            _drawer?.Draw();
        }
    }

    /// <summary>
    /// 通用 MonoBehaviour 编辑器桩。
    /// 对所有 MonoBehaviour 生效（非 fallback），支持多对象编辑。
    /// 当 MonoBehaviour 字段上使用了 Nodin 属性（如 [LabelText]、[FoldoutGroup]）时，
    /// 自动通过 NodinDrawer 绘制；否则回退到默认 Inspector 绘制。
    /// </summary>
    [CustomEditor(typeof(MonoBehaviour), true), CanEditMultipleObjects]
    [InitializeOnLoad]
    public class NodinMonoBehaviourFallbackEditor : UnityEditor.Editor
    {
        private NodinDrawer _drawer;
        private bool _hasNodinAttributes;

        // ── 按类型缓存检测结果，避免每次 OnEnable 重复反射 ──
        private static readonly Dictionary<System.Type, bool> _attrCache = new();

        // ── Odin 共存：在 Inspector 头部绘制完毕后补充 Nodin 按钮 ──
        private static bool _finishedHeaderHooked;
        // 缓存：类型 → 是否含 ButtonAttribute 方法
        private static readonly Dictionary<System.Type, bool> _buttonCache = new();

        static NodinMonoBehaviourFallbackEditor()
        {
            if (!_finishedHeaderHooked)
            {
                _finishedHeaderHooked = true;
                UnityEditor.Editor.finishedDefaultHeaderGUI += OnFinishedHeaderGUI;
            }
        }

        private static void OnFinishedHeaderGUI(UnityEditor.Editor editor)
        {
            if (editor == null || editor.target == null) return;
            // 仅处理 MonoBehaviour
            if (!(editor.target is MonoBehaviour)) return;
            // 如果当前编辑器就是 Nodin 自己的，则跳过（避免重复绘制）
            if (editor.GetType().Namespace == "Nodin.Editor") return;

            var type = editor.target.GetType();
            if (!_buttonCache.TryGetValue(type, out var hasButtons))
            {
                hasButtons = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Any(m => m.GetCustomAttribute<ButtonAttribute>() != null);
                _buttonCache[type] = hasButtons;
            }
            if (!hasButtons) return;

            // 为每个 target 绘制按钮
            foreach (var t in editor.targets)
            {
                if (t == null) continue;
                var drawer = new NodinDrawer(t, t as Object);
                drawer.DrawButtonsOnly();
            }
        }

        private void OnEnable()
        {
            var type = target.GetType();
            if (!_attrCache.TryGetValue(type, out _hasNodinAttributes))
            {
                _hasNodinAttributes = HasNodinAttributes(type);
                _attrCache[type] = _hasNodinAttributes;
            }

            if (_hasNodinAttributes)
                _drawer = new NodinDrawer(target, target);
        }

        public override void OnInspectorGUI()
        {
            if (_hasNodinAttributes && _drawer != null)
                _drawer.Draw();
            else
                DrawDefaultInspector();
        }

        private static bool HasNodinAttributes(System.Type type)
        {
            var fields = type.GetFields(BindingFlags.Public
                | BindingFlags.Instance
                | BindingFlags.NonPublic);

            foreach (var f in fields)
            {
                if (f.GetCustomAttribute<LabelTextAttribute>() != null
                    || f.GetCustomAttribute<FoldoutGroupAttribute>() != null
                    || f.GetCustomAttribute<BoxGroupAttribute>() != null
                    || f.GetCustomAttribute<ToggleGroupAttribute>() != null
                    || f.GetCustomAttribute<ShowIfAttribute>() != null
                    || f.GetCustomAttribute<HideIfAttribute>() != null
                    || f.GetCustomAttribute<ReadOnlyAttribute>() != null
                    || f.GetCustomAttribute<ShowInInspectorAttribute>() != null
                    || f.GetCustomAttribute<InfoBoxAttribute>() != null
                    || f.GetCustomAttribute<ValueDropdownAttribute>() != null
                    || f.GetCustomAttribute<ListDrawerSettingsAttribute>() != null
                    || f.GetCustomAttribute<EnumToggleButtonsAttribute>() != null)
                    return true;
            }
            return false;
        }
    }
}
