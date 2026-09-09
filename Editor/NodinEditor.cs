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
    /// 安装 Odin 时作为 fallback，具体的 Nodin 类型由 NodinOdinEditorRegistration
    /// 通过 Odin 的公开 Editor Types 配置注册；未安装 Odin 时仍可直接工作。
    /// </summary>
    [CustomEditor(typeof(ScriptableObject), true, isFallback = true)]
    [CanEditMultipleObjects]
    public class NodinEditor : UnityEditor.Editor
    {
        private NodinDrawer _drawer;
        private bool _hasNodinAttributes;

        private void OnEnable()
        {
            if (target == null) return;

            _hasNodinAttributes = NodinAttributeUtility.HasNodinAttributes(target.GetType());
            if (_hasNodinAttributes)
                _drawer = new NodinDrawer(target, target, serializedObject);
        }

        public override void OnInspectorGUI()
        {
            if (_hasNodinAttributes && _drawer != null)
            {
                // Nodin 接管 Inspector 后，Unity 默认的 MonoBehaviour 启用开关不会自动绘制。
                // 直接操作组件，避免损坏/缺失组件导致 SerializedObject 创建失败。
                if (target is MonoBehaviour behaviour)
                {
                    EditorGUI.BeginChangeCheck();
                    bool enabled = EditorGUILayout.Toggle("启用", behaviour.enabled);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(behaviour, "切换组件启用状态");
                        behaviour.enabled = enabled;
                        EditorUtility.SetDirty(behaviour);
                    }
                    EditorGUILayout.Space(2);
                }
                _drawer.Draw();
            }
            else
                DrawDefaultInspector();
        }
    }

    /// <summary>Nodin 兼容性辅助（Odin 共存检测等）</summary>
    internal static class NodinCompat
    {
        private static bool _odinChecked;
        private static bool _hasOdin;
        private static System.Type _odinEditorType;

        /// <summary>检测项目中是否存在 Odin Inspector（仅反射一次）</summary>
        public static bool HasOdin()
        {
            if (!_odinChecked)
            {
                _odinChecked = true;
                _odinEditorType = System.Type.GetType("Sirenix.OdinInspector.Editor.OdinEditor, Sirenix.OdinInspector.Editor");
                _hasOdin = _odinEditorType != null;
            }
            return _hasOdin;
        }

        /// <summary>判断编辑器是否为 OdinEditor 或其子类</summary>
        public static bool IsOdinEditor(UnityEditor.Editor editor)
        {
            if (!HasOdin() || editor == null) return false;
            return _odinEditorType.IsAssignableFrom(editor.GetType());
        }

        /// <summary>OdinEditor 类型（未安装 Odin 时为 null）</summary>
        public static System.Type OdinEditorType => _odinEditorType;
    }

    /// <summary>
    /// NodinMonoBehaviour 编辑器桩。
    /// 继承 NodinMonoBehaviour 的类型自动获得 Nodin 属性绘制支持。
    /// 如果目标对象有名为 useNodinDrawing 的 bool 字段且值为 false，
    /// 则按实例委托给 Odin Editor 绘制（不影响其他实例）。
    /// </summary>
    [CustomEditor(typeof(NodinMonoBehaviour), true)]
    public class NodinMonoBehaviourEditor : UnityEditor.Editor
    {
        private NodinDrawer _drawer;
        private System.Reflection.FieldInfo _toggleField;
        private bool _toggleFieldChecked;

        private void OnEnable()
        {
            _drawer = new NodinDrawer(target, target, serializedObject);
        }

        public override void OnInspectorGUI()
        {
            // 按类型缓存查找 useNodinDrawing 字段
            if (!_toggleFieldChecked)
            {
                _toggleFieldChecked = true;
                _toggleField = target.GetType().GetField("useNodinDrawing",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            }

            // 有 toggle 字段且值为 false → 委托给 Odin（按实例，不影响全局）
            if (_toggleField != null
                && _toggleField.GetValue(target) is bool useNodin
                && !useNodin)
            {
                DrawWithOdinOrNative();
                return;
            }

            _drawer?.Draw();
        }

        private void DrawWithOdinOrNative()
        {
            // 不在一个 Editor 内创建并手动驱动另一个 Editor。Unity 2022.3/2023.2
            // 的 UI Toolkit Inspector 会因此产生失效的数据绑定请求。
            DrawDefaultInspector();
        }
    }

    /// <summary>
    /// 通用 MonoBehaviour 编辑器桩。
    /// 这是 fallback，因此不会抢占 Odin 或项目已有的具体 CustomEditor。
    /// 带 Nodin 特性的具体类型会由 NodinOdinEditorRegistration 精确注册。
    /// </summary>
    [CustomEditor(typeof(MonoBehaviour), true, isFallback = true), CanEditMultipleObjects]
    public class NodinMonoBehaviourFallbackEditor : UnityEditor.Editor
    {
        private NodinDrawer _drawer;
        private bool _hasNodinAttributes;

        private void OnEnable()
        {
            if (target == null) return;

            _hasNodinAttributes = NodinAttributeUtility.HasNodinAttributes(target.GetType());
            if (_hasNodinAttributes)
                _drawer = new NodinDrawer(target, target, serializedObject);
        }

        public override void OnInspectorGUI()
        {
            if (_hasNodinAttributes && _drawer != null)
            {
                _drawer.Draw();
            }
            else
                DrawDefaultInspector();
        }
    }

    /// <summary>Nodin 菜单入口 — 重新打开初始化设置面板</summary>
    internal static class NodinMenu
    {
        [MenuItem("Tools/Nodin/初始化设置")]
        private static void OpenInitWindow()
        {
            var win = EditorWindow.GetWindow<NodinInitWindow>(true, "Nodin 初始化设置", true);
            win.minSize = new Vector2(520, 640);
            win.maxSize = new Vector2(520, 640);
            win.ShowUtility();
        }

        [MenuItem("Tools/Nodin/选中设置资产")]
        private static void SelectSettingsAsset()
        {
            var path = EditorPrefs.GetString("Nodin.SettingsPath", "Assets/NodinSettings.asset");
            var asset = AssetDatabase.LoadAssetAtPath<NodinSettings>(path);
            if (asset != null)
            {
                Selection.activeObject = asset;
            }
            else
            {
                EditorUtility.DisplayDialog("Nodin", $"未找到设置资产：{path}\n请先运行「初始化设置」。", "确定");
            }
        }
    }
}
