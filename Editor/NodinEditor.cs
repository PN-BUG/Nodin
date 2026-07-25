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
    /// editorForChildClasses = true 确保覆盖所有 ScriptableObject 派生类（包括被 Odin 抢占的）。
    /// 检测 Nodin 属性后决定是否接管绘制，否则回退到默认绘制。
    /// </summary>
    [CustomEditor(typeof(ScriptableObject), true)]
    [CanEditMultipleObjects]
    [InitializeOnLoad]
    public class NodinEditor : UnityEditor.Editor
    {
        private NodinDrawer _drawer;
        private bool _hasNodinAttributes;

        // ── 按类型缓存检测结果，避免每次 OnEnable 重复反射 ──
        private static readonly Dictionary<System.Type, bool> _attrCache = new();

        // ── Odin 共存：反射缓存 ──
        private static bool _registered;
        private static System.Type _ccaType;
        private static System.Type _monoEditorTypeType;
        private static System.Reflection.FieldInfo _kSEditorsField;
        private static System.Reflection.FieldInfo _sSearchCacheField;
        private static System.Reflection.MethodInfo _rebuildMethod;

        static NodinEditor()
        {
            // Odin 会在 [InitializeOnLoadMethod] 阶段为每个 ScriptableObject 子类型注册 OdinEditor。
            // 我们使用 EditorApplication.delayCall 确保在 Odin 之后注册，覆盖其条目。
            EditorApplication.delayCall += RegisterNodinForNodinAttributedTypes;
        }

        /// <summary>
        /// 扫描所有 ScriptableObject 子类型，为含 Nodin 属性的类型在
        /// CustomEditorAttributes.kSCustomEditors 中注册 NodinEditor，
        /// 覆盖 Odin 动态注册的 OdinEditor 条目。
        /// </summary>
        private static void RegisterNodinForNodinAttributedTypes()
        {
            if (_registered) return;
            _registered = true;

            // 没有 Odin 时，Nodin 的 [CustomEditor] 注册自然胜出，无需扫描覆盖
            if (!NodinCompat.HasOdin()) return;

            try
            {
                _ccaType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.CustomEditorAttributes");
                if (_ccaType == null) return;

                _monoEditorTypeType = _ccaType.GetNestedType("MonoEditorType", BindingFlags.NonPublic | BindingFlags.Public);
                _kSEditorsField = _ccaType.GetField("kSCustomEditors", BindingFlags.Static | BindingFlags.NonPublic);
                _sSearchCacheField = _ccaType.GetField("s_SearchCache", BindingFlags.Static | BindingFlags.NonPublic);
                _rebuildMethod = _ccaType.GetMethod("Rebuild", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                if (_kSEditorsField == null || _monoEditorTypeType == null) return;

                var dict = _kSEditorsField.GetValue(null) as System.Collections.IDictionary;
                if (dict == null) return;

                var nodinEditorType = typeof(NodinEditor);
                int registeredCount = 0;

                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    System.Type[] types;
                    try { types = assembly.GetTypes(); } catch { continue; }

                    foreach (var type in types)
                    {
                        if (!typeof(ScriptableObject).IsAssignableFrom(type)) continue;
                        if (type.IsAbstract) continue;
                        if (!HasNodinAttributes(type)) continue;

                        // 创建 MonoEditorType 条目
                        var entry = System.Activator.CreateInstance(_monoEditorTypeType);
                        _monoEditorTypeType.GetField("m_InspectedType").SetValue(entry, type);
                        _monoEditorTypeType.GetField("m_InspectorType").SetValue(entry, nodinEditorType);
                        _monoEditorTypeType.GetField("m_EditorForChildClasses").SetValue(entry, false);
                        _monoEditorTypeType.GetField("m_IsFallback").SetValue(entry, false);
                        _monoEditorTypeType.GetField("m_RenderPipelineType").SetValue(entry, null);

                        // 替换 kSCustomEditors 中的条目列表
                        var listType = typeof(List<>).MakeGenericType(_monoEditorTypeType);
                        var list = (System.Collections.IList)System.Activator.CreateInstance(listType);
                        list.Add(entry);
                        dict[type] = list;
                        registeredCount++;
                    }
                }

                // 清除搜索缓存
                if (_sSearchCacheField != null)
                {
                    var cache = _sSearchCacheField.GetValue(null) as System.Collections.IList;
                    cache?.Clear();
                }

                // 重建
                _rebuildMethod?.Invoke(null, null);

                if (registeredCount > 0)
                    Debug.Log($"[Nodin] 已为 {registeredCount} 个 ScriptableObject 类型注册 NodinEditor（覆盖 Odin）");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Nodin] 注册 NodinEditor 失败（非致命）: {ex.Message}");
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

            // 检查 [ShowInInspector] 属性
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var p in properties)
            {
                if (p.GetIndexParameters().Length > 0) continue;
                if (p.GetCustomAttribute<ShowInInspectorAttribute>() != null
                    || p.GetCustomAttribute<LabelTextAttribute>() != null
                    || p.GetCustomAttribute<BoxGroupAttribute>() != null
                    || p.GetCustomAttribute<FoldoutGroupAttribute>() != null)
                    return true;
            }

            // 检查是否有 Button 方法
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var m in methods)
            {
                if (m.GetCustomAttribute<ButtonAttribute>() != null)
                    return true;
            }

            return false;
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
        private UnityEditor.Editor _odinDelegate;
        private System.Reflection.FieldInfo _toggleField;
        private bool _toggleFieldChecked;

        private void OnEnable()
        {
            _drawer = new NodinDrawer(target, target);
        }

        private void OnDisable()
        {
            if (_odinDelegate != null)
            {
                DestroyImmediate(_odinDelegate);
                _odinDelegate = null;
            }
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
            if (!NodinCompat.HasOdin())
            {
                DrawDefaultInspector();
                return;
            }

            // 创建并缓存 OdinEditor 实例（按当前 target，单实例）
            if (_odinDelegate == null)
            {
                _odinDelegate = UnityEditor.Editor.CreateEditor(target, NodinCompat.OdinEditorType);
            }
            _odinDelegate?.OnInspectorGUI();
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
            // 如果当前编辑器是 Nodin 自己的，则跳过（避免重复绘制）
            if (editor is NodinMonoBehaviourFallbackEditor || editor is NodinMonoBehaviourEditor) return;

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
