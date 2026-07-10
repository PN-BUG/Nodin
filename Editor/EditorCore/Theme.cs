#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hub 主题配色与图标定义
/// 包含深色主题颜色、分类配色、分类图标（Unity 版本自适应）
/// </summary>
public static class Theme
{
    #region 深色主题配色
    // 统一引用 HubPalette 单一来源，保留 ClrXxx 别名以兼容现有代码
    public static readonly Color ClrBg           = Palette.Bg;
    public static readonly Color ClrLeftBg       = Palette.LeftBg;
    public static readonly Color ClrRightBg      = Palette.RightBg;
    public static readonly Color ClrSplitter     = Palette.Splitter;
    public static readonly Color ClrSelection    = Palette.Selection;
    public static readonly Color ClrHover        = Palette.Hover;
    public static readonly Color ClrText         = Palette.Text;
    public static readonly Color ClrTextDim      = Palette.TextDim;
    public static readonly Color ClrTextBright   = Palette.TextBright;
    public static readonly Color ClrAccent       = Palette.Accent;
    public static readonly Color ClrAccentDim    = Palette.AccentDim;
    public static readonly Color ClrCardBg       = Palette.CardBg;
    public static readonly Color ClrTagBg        = Palette.TagBg;
    public static readonly Color ClrDivider      = Palette.Divider;
    public static readonly Color ClrBtnNormal    = Palette.BtnNormal;
    public static readonly Color ClrBtnHover     = Palette.BtnHover;
    #endregion

    #region 分类配色（已知分类 → 固定颜色）
    public static readonly Dictionary<string, Color> CategoryColors = new Dictionary<string, Color>(StringComparer.Ordinal)
    {
        { "框架初始化", new Color(0.35f, 0.75f, 0.45f, 1f) },
        { "数据管理",   new Color(0.90f, 0.65f, 0.25f, 1f) },
        { "面板管理",   new Color(0.55f, 0.45f, 0.85f, 1f) },
        { "资产工具",   new Color(0.30f, 0.65f, 0.80f, 1f) },
        { "编辑器工具", new Color(0.80f, 0.45f, 0.35f, 1f) },
        { "文件工具",   new Color(0.45f, 0.70f, 0.55f, 1f) },
        { "字体工具",   new Color(0.75f, 0.50f, 0.70f, 1f) },
        { "媒体工具",   new Color(0.85f, 0.55f, 0.40f, 1f) },
        { "UI 工具",   new Color(0.40f, 0.75f, 0.85f, 1f) },
        { "调试工具",   new Color(0.85f, 0.40f, 0.55f, 1f) },
        { "路径工具",   new Color(0.50f, 0.60f, 0.75f, 1f) },
        { "文本工具",   new Color(0.35f, 0.70f, 0.75f, 1f) },
    };
    #endregion

    #region 分类图标（Unity 版本自适应）
    // ── 分类图标（根据 Unity 版本选择）+ 用 Emoji（2022- 用 BMP 安全字符）──
    private static readonly Dictionary<string, string> _categoryIconsEmoji = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "框架初始化", "⚙"   },
        { "数据管理",   "📊" },
        { "面板管理",   "📋" },
        { "资产工具",   "📦" },
        { "编辑器工具", "🔧" },
        { "文件工具",   "📂" },
        { "字体工具",   "🔤" },
        { "媒体工具",   "🎬" },
        { "UI 工具",   "🎨" },
        { "调试工具",   "🐛" },
        { "路径工具",   "🗺" },
        { "文本工具",   "📝" },
    };

    private static readonly Dictionary<string, string> _categoryIconsLegacy = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "框架初始化", "⚙"   },
        { "数据管理",   "▣" },
        { "面板管理",   "▤" },
        { "资产工具",   "◆" },
        { "编辑器工具", "✎" },
        { "文件工具",   "►" },
        { "字体工具",   "A" },
        { "媒体工具",   "▮" },
        { "UI 工具",   "◜" },
        { "调试工具",   "●" },
        { "路径工具",   "◇" },
        { "文本工具",   "T" },
    };

    private static bool _unityVersionChecked;
    private static bool _isUnity6Plus;

    /// <summary>Unity 6+ 的字体回退支持 Emoji，旧版（2022/2023 Mono）不支持补充平面字符</summary>
    public static bool IsUnity6Plus
    {
        get
        {
            if (!_unityVersionChecked)
            {
                var ver = Application.unityVersion;
                _isUnity6Plus = int.TryParse(ver.Split('.')[0], out int major) && major >= 6000;
                _unityVersionChecked = true;
            }
            return _isUnity6Plus;
        }
    }

    /// <summary>当前版本对应的图标字库（延迟计算，避免静态初始化时 Application.unityVersion 不可用）</summary>
    public static Dictionary<string, string> ActiveIconDict
        => IsUnity6Plus ? _categoryIconsEmoji : _categoryIconsLegacy;

    /// <summary>获取分类图标（版本自适应）</summary>
    public static string GetCategoryIcon(string categoryName)
    {
        if (ActiveIconDict.TryGetValue(categoryName, out var icon))
            return icon;
        return IsUnity6Plus ? "🟡" : "★";
    }
    #endregion

    #region 未知分类调色板
    // ── 未知分类调色板（自动循环分配）──
    public static readonly Color[] DefaultPalette = new[]
    {
        new Color(0.55f, 0.75f, 0.45f, 1f),
        new Color(0.45f, 0.65f, 0.80f, 1f),
        new Color(0.80f, 0.55f, 0.50f, 1f),
        new Color(0.65f, 0.55f, 0.75f, 1f),
        new Color(0.50f, 0.70f, 0.60f, 1f),
        new Color(0.75f, 0.65f, 0.45f, 1f),
    };
    #endregion
}
#endif
