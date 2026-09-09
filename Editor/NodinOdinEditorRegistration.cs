// ═══════════════════════════════════════════════════════════════
//  Nodin / Odin 共存注册
//  仅使用 Odin 的公开 Editor Types 配置 API，不修改 Unity 私有 Editor 缓存。
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
    internal static class NodinAttributeUtility
    {
        private static readonly Dictionary<Type, bool> Cache = new();
        private static readonly Assembly NodinRuntimeAssembly = typeof(LabelTextAttribute).Assembly;

        public static bool HasNodinAttributes(Type type)
        {
            if (type == null) return false;
            if (Cache.TryGetValue(type, out bool cached)) return cached;

            const BindingFlags flags = BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic;

            bool result = type.GetMembers(flags).Any(HasNodinAttribute);
            Cache[type] = result;
            return result;
        }

        private static bool HasNodinAttribute(MemberInfo member)
        {
            return CustomAttributeData.GetCustomAttributes(member).Any(attribute =>
                attribute.AttributeType.Assembly == NodinRuntimeAssembly
                && typeof(Attribute).IsAssignableFrom(attribute.AttributeType));
        }
    }

    /// <summary>
    /// Odin 默认会为所有用户类型生成自己的 Editor，所以 Unity 的 fallback
    /// CustomEditor 不会被选中。这里通过 Odin 官方公开的 Editor Types 配置，
    /// 只把实际使用 Nodin 特性的具体类型交给 Nodin 绘制。
    /// </summary>
    [InitializeOnLoad]
    internal static class NodinOdinEditorRegistration
    {
        private const string InspectorConfigTypeName =
            "Sirenix.OdinInspector.Editor.InspectorConfig, Sirenix.OdinInspector.Editor";

        static NodinOdinEditorRegistration()
        {
            if (NodinCompat.HasOdin())
                EditorApplication.delayCall += RegisterNodinTypes;
        }

        [MenuItem("Tools/Nodin/刷新 Odin 编辑器注册")]
        private static void RegisterNodinTypes()
        {
            Type inspectorConfigType = Type.GetType(InspectorConfigTypeName);
            if (inspectorConfigType == null) return;

            try
            {
                object config = inspectorConfigType
                    .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                    ?.GetValue(null);
                object drawingConfig = inspectorConfigType
                    .GetProperty("DrawingConfig", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(config);
                if (config == null || drawingConfig == null) return;

                Type drawingConfigType = drawingConfig.GetType();
                MethodInfo hasEntry = drawingConfigType.GetMethod(
                    "HasEntryForType", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo getEditorType = drawingConfigType.GetMethod(
                    "GetEditorType", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo setEditorType = drawingConfigType.GetMethod(
                    "SetEditorType", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo updateCaches = drawingConfigType.GetMethod(
                    "UpdateCaches", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo updateOdinEditors = inspectorConfigType.GetMethod(
                    "UpdateOdinEditors", BindingFlags.Public | BindingFlags.Instance);

                if (hasEntry == null || getEditorType == null || setEditorType == null
                    || updateOdinEditors == null)
                    return;

                bool changed = false;
                foreach (Type targetType in GetNodinTargetTypes())
                {
                    Type editorBaseType = typeof(ScriptableObject).IsAssignableFrom(targetType)
                        ? typeof(NodinEditor)
                        : typeof(NodinMonoBehaviourFallbackEditor);

                    bool alreadyRegistered = (bool)hasEntry.Invoke(
                        drawingConfig, new object[] { targetType });
                    if (alreadyRegistered)
                    {
                        Type configuredEditor = getEditorType.Invoke(
                            drawingConfig, new object[] { targetType }) as Type;
                        if (configuredEditor == editorBaseType)
                            continue;
                    }

                    setEditorType.Invoke(drawingConfig, new object[] { targetType, editorBaseType });
                    changed = true;
                }

                if (!changed) return;

                updateCaches?.Invoke(drawingConfig, null);
                if (config is UnityEngine.Object configAsset)
                    EditorUtility.SetDirty(configAsset);
                updateOdinEditors.Invoke(config, null);
            }
            catch (TargetInvocationException exception)
            {
                Debug.LogException(exception.InnerException ?? exception);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static IEnumerable<Type> GetNodinTargetTypes()
        {
            IEnumerable<Type> scriptableObjects =
                TypeCache.GetTypesDerivedFrom<ScriptableObject>();
            IEnumerable<Type> monoBehaviours =
                TypeCache.GetTypesDerivedFrom<MonoBehaviour>()
                    .Where(type => !typeof(NodinMonoBehaviour).IsAssignableFrom(type));

            return scriptableObjects
                .Concat(monoBehaviours)
                .Where(type => type != null
                    && !type.IsAbstract
                    && !type.IsGenericTypeDefinition
                    && NodinAttributeUtility.HasNodinAttributes(type));
        }
    }
}
