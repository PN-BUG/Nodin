using UnityEngine;

namespace Nodin
{
    /// <summary>
    /// 支持 Nodin 属性自动绘制的 MonoBehaviour 基类。
    /// 继承此类即可在 Inspector 中使用 [LabelText]、[FoldoutGroup]、[DictionaryDrawerSettings] 等 Nodin 属性。
    /// </summary>
    public abstract class NodinMonoBehaviour : MonoBehaviour { }
}
