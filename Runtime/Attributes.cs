// ═══════════════════════════════════════════════════════════════
//  Nodin — No Odin Inspector
//  轻量级属性定义，提供与 Odin Inspector 兼容的特性集合
//
//  所有特性使用 Nodin 命名空间。
// ═══════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nodin
{
    // ══════════════════════════════════════════════════════════
    //  分组 & 布局
    // ══════════════════════════════════════════════════════════

    /// <summary>将字段/方法归入可折叠分组，支持 '/' 分隔的子分组</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class FoldoutGroupAttribute : Attribute
    {
        public string GroupName { get; }
        public bool Expanded { get; }
        public int Order { get; set; }

        public FoldoutGroupAttribute(string groupName)
        {
            GroupName = groupName;
        }

        public FoldoutGroupAttribute(string groupName, bool expanded = false)
        {
            GroupName = groupName;
            Expanded = expanded;
        }
    }

    /// <summary>将字段归入带标题的盒子分组</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class BoxGroupAttribute : Attribute
    {
        public string GroupName { get; }
        public BoxGroupAttribute(string groupName) { GroupName = groupName; }
    }

    /// <summary>
    /// 将字段归入可切换的分组。第一个 bool 字段作为开关，控制同组其他字段的显示。
    /// 用法：[ToggleGroup("组名")] public bool toggle;
    ///       [ToggleGroup("组名")] public Color color;
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ToggleGroupAttribute : Attribute
    {
        public string GroupName { get; }
        public ToggleGroupAttribute(string groupName) { GroupName = groupName; }
    }

    // ══════════════════════════════════════════════════════════
    //  标签 & 显示
    // ══════════════════════════════════════════════════════════

    /// <summary>自定义 Inspector 中显示的标签文本</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class LabelTextAttribute : Attribute
    {
        public string Text { get; }
        /// <summary>为 true 时根据文字实际像素宽度自动计算标签宽度，否则使用默认固定宽度</summary>
        public bool AutoWidth { get; set; }
        public LabelTextAttribute(string text) { Text = text; }
    }

    /// <summary>隐藏字段标签，仅显示值控件</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class HideLabelAttribute : Attribute { }

    /// <summary>为字段设置鼠标悬停时的 Tooltip 提示文本</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class PropertyTooltipAttribute : Attribute
    {
        public string Tooltip { get; }
        public PropertyTooltipAttribute(string tooltip) { Tooltip = tooltip; }
    }

    /// <summary>在字段上方显示信息提示框</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public class InfoBoxAttribute : Attribute
    {
        public string Message { get; }
        public InfoMessageType Type { get; }
        public string VisibleIfMemberName { get; }

        public InfoBoxAttribute(string message)
        {
            Message = message;
            Type = InfoMessageType.Info;
            VisibleIfMemberName = null;
        }

        public InfoBoxAttribute(string message, object type)
        {
            Message = message;
            Type = (InfoMessageType)type;
            VisibleIfMemberName = null;
        }

        public InfoBoxAttribute(string message, object type, string visibleIfMemberName)
        {
            Message = message;
            Type = (InfoMessageType)type;
            VisibleIfMemberName = visibleIfMemberName;
        }
    }

    /// <summary>信息提示类型</summary>
    public enum InfoMessageType { None, Info, Warning, Error }

    /// <summary>将字符串字段绘制为多行文本区域</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class MultiLinePropertyAttribute : Attribute
    {
        public int Lines { get; }
        public MultiLinePropertyAttribute(int lines) { Lines = lines; }
    }

    // ══════════════════════════════════════════════════════════
    //  条件显示 & 启用
    // ══════════════════════════════════════════════════════════

    /// <summary>当指定成员值等于目标值时显示字段（默认 true）</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class ShowIfAttribute : Attribute
    {
        public string MemberName { get; }
        public object Value { get; }

        public ShowIfAttribute(string memberName)
        {
            MemberName = memberName;
            Value = true;
        }

        public ShowIfAttribute(string memberName, object value)
        {
            MemberName = memberName;
            Value = value;
        }
    }

    /// <summary>当指定成员值等于目标值时隐藏字段（默认 true）</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class HideIfAttribute : Attribute
    {
        public string MemberName { get; }
        public object Value { get; }

        public HideIfAttribute(string memberName)
        {
            MemberName = memberName;
            Value = true;
        }

        public HideIfAttribute(string memberName, object value)
        {
            MemberName = memberName;
            Value = value;
        }
    }

    /// <summary>当指定成员值等于目标值时启用字段（默认 true）</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class EnableIfAttribute : Attribute
    {
        public string MemberName { get; }
        public object Value { get; }

        public EnableIfAttribute(string memberName)
        {
            MemberName = memberName;
            Value = true;
        }

        public EnableIfAttribute(string memberName, object value)
        {
            MemberName = memberName;
            Value = value;
        }
    }

    /// <summary>当指定成员值等于目标值时禁用字段（默认 true）</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class DisableIfAttribute : Attribute
    {
        public string MemberName { get; }
        public object Value { get; }

        public DisableIfAttribute(string memberName)
        {
            MemberName = memberName;
            Value = true;
        }

        public DisableIfAttribute(string memberName, object value)
        {
            MemberName = memberName;
            Value = value;
        }
    }

    /// <summary>将字段标记为只读（不可编辑）</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class ReadOnlyAttribute : Attribute { }

    /// <summary>将枚举字段渲染为切换按钮组</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class EnumToggleButtonsAttribute : Attribute { }

    // ══════════════════════════════════════════════════════════
    //  按钮 & 动作
    // ══════════════════════════════════════════════════════════

    /// <summary>按钮尺寸</summary>
    public enum ButtonSizes { Small, Medium, Large }

    /// <summary>将方法绘制为 Inspector 按钮</summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property)]
    public class ButtonAttribute : Attribute
    {
        public string Name { get; }
        public ButtonSizes Size { get; }

        public ButtonAttribute() { Name = null; Size = ButtonSizes.Medium; }
        public ButtonAttribute(string name) { Name = name; Size = ButtonSizes.Medium; }
        public ButtonAttribute(ButtonSizes size) { Name = null; Size = size; }
        public ButtonAttribute(string name, ButtonSizes size) { Name = name; Size = size; }
    }

    /// <summary>设置按钮或字段的 GUI 颜色</summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property)]
    public class GUIColorAttribute : Attribute
    {
        public Color Color { get; }

        public GUIColorAttribute(float r, float g, float b) { Color = new Color(r, g, b); }
        public GUIColorAttribute(string hex)
        {
            Color = ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
        }
    }

    /// <summary>在 Inspector 中插入自定义 GUI 绘制回调</summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property)]
    public class OnInspectorGUIAttribute : Attribute
    {
        public string MethodName { get; }
        public OnInspectorGUIAttribute() { MethodName = null; }
        public OnInspectorGUIAttribute(string methodName) { MethodName = methodName; }
    }

    /// <summary>强制在 Inspector 中显示非 public 字段</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class ShowInInspectorAttribute : Attribute { }

    // ══════════════════════════════════════════════════════════
    //  字段行为
    // ══════════════════════════════════════════════════════════

    /// <summary>下拉列表选项来源（方法名或字段名）</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class ValueDropdownAttribute : Attribute
    {
        public string MemberName { get; }
        public ValueDropdownAttribute(string memberName) { MemberName = memberName; }
    }

    /// <summary>ValueDropdown 下拉选项条目</summary>
    public struct ValueDropdownItem<T>
    {
        public string Text { get; }
        public T Value { get; }
        public ValueDropdownItem(string text, T value) { Text = text; Value = value; }
        public override string ToString() => Text ?? Value?.ToString() ?? "";
    }

    /// <summary>ValueDropdown 下拉选项列表辅助类型</summary>
    public class ValueDropdownList<T> : List<ValueDropdownItem<T>>
    {
        public void Add(string name, T value) => Add(new ValueDropdownItem<T>(name, value));
    }

    /// <summary>文件夹路径选择器</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class FolderPathAttribute : Attribute
    {
        public bool AbsolutePath { get; set; }
        public bool RequireExistingPath { get; set; }
    }

    /// <summary>限制 Object 引用仅允许 Asset（非场景对象）</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class AssetsOnlyAttribute : Attribute { }

    /// <summary>字段值改变后回调指定方法</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class OnValueChangedAttribute : Attribute
    {
        public string MethodName { get; }
        public OnValueChangedAttribute(string methodName) { MethodName = methodName; }
    }

    /// <summary>List 字段的绘制设置</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class ListDrawerSettingsAttribute : Attribute
    {
        public bool ShowFoldout { get; set; } = true;
        public bool DraggableItems { get; set; } = true;
        public int NumberOfItemsPerPage { get; set; }
        public bool ShowIndexLabels { get; set; }
        public bool HideAddButton { get; set; }
        public bool AlwaysAddDefaultValue { get; set; }
    }

    /// <summary>Dictionary 字段的绘制设置</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class DictionaryDrawerSettingsAttribute : Attribute
    {
        /// <summary>Key 列显示的标签文本</summary>
        public string KeyLabel { get; set; }
        /// <summary>Value 列显示的标签文本</summary>
        public string ValueLabel { get; set; }
        /// <summary>是否显示 Key 列标签（默认 true）</summary>
        public bool IsReadOnly { get; set; }
    }

    // ══════════════════════════════════════════════════════════
    //  数值约束
    // ══════════════════════════════════════════════════════════

    /// <summary>限制数值字段的最小值</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class MinValueAttribute : Attribute
    {
        public double Min { get; }
        public MinValueAttribute(double min) { Min = min; }
    }

    /// <summary>限制数值字段的最大值</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class MaxValueAttribute : Attribute
    {
        public double Max { get; }
        public MaxValueAttribute(double max) { Max = max; }
    }

    /// <summary>同时限制数值字段的最小值和最大值（等价于 MinValue + MaxValue）</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class RangeAttribute : Attribute
    {
        public double Min { get; }
        public double Max { get; }
        public RangeAttribute(double min, double max) { Min = min; Max = max; }
    }

    // ══════════════════════════════════════════════════════════
    //  水平布局
    // ══════════════════════════════════════════════════════════

    /// <summary>将字段归入水平排列分组，同名字段在同一行绘制</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class HorizontalGroupAttribute : Attribute
    {
        public string GroupName { get; }
        public float Width { get; }

        public HorizontalGroupAttribute() { GroupName = ""; Width = 0f; }
        public HorizontalGroupAttribute(string groupName) { GroupName = groupName; Width = 0f; }
        public HorizontalGroupAttribute(string groupName, float width) { GroupName = groupName; Width = width; }
    }

    // ══════════════════════════════════════════════════════════
    //  标题 & 只读显示
    // ══════════════════════════════════════════════════════════

    /// <summary>标题对齐方式</summary>
    public enum TitleAlignment { Left, Center, Right }

    /// <summary>在字段上方绘制粗体标题</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class TitleAttribute : Attribute
    {
        public string TitleText { get; }
        public string Subtitle { get; }
        public TitleAlignment Alignment { get; }
        public bool HorizontalLine { get; set; } = true;
        public bool Bold { get; set; } = true;

        public TitleAttribute(string title)
        {
            TitleText = title;
            Subtitle = null;
            Alignment = TitleAlignment.Left;
        }

        public TitleAttribute(string title, string subtitle)
        {
            TitleText = title;
            Subtitle = subtitle;
            Alignment = TitleAlignment.Left;
        }

        public TitleAttribute(string title, TitleAlignment alignment)
        {
            TitleText = title;
            Subtitle = null;
            Alignment = alignment;
        }

        public TitleAttribute(string title, string subtitle, TitleAlignment alignment)
        {
            TitleText = title;
            Subtitle = subtitle;
            Alignment = alignment;
        }
    }

    /// <summary>将可序列化类的字段直接内联绘制，而非折叠显示</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class InlinePropertyAttribute : Attribute { }

    /// <summary>将字段值以只读字符串形式显示（不可编辑）</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class DisplayAsStringAttribute : Attribute
    {
        public bool Overflow { get; }
        public DisplayAsStringAttribute() { Overflow = false; }
        public DisplayAsStringAttribute(bool overflow) { Overflow = overflow; }
    }

    /// <summary>标记引用字段为必填，为空时在 Inspector 中提示</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class RequiredAttribute : Attribute
    {
        public string Message { get; }
        public RequiredAttribute() { Message = null; }
        public RequiredAttribute(string message) { Message = message; }
    }
}
