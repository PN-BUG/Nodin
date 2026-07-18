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

    #region 分类配色（语义化 5 色系）
    // ── 蓝 = 创建/构建 / 青-绿 = 数据/管理 / 琥珀 = 工具/配置 / 玫瑰 = 调试/危险 / 石板 = 中性 ──
    public static readonly Dictionary<string, Color> CategoryColors = new Dictionary<string, Color>(StringComparer.Ordinal)
    {
        { "框架初始化",   new Color(0.345f, 0.569f, 0.910f, 1f) }, // Blue — 框架核心
        { "数据管理",     new Color(0.282f, 0.690f, 0.690f, 1f) }, // Teal — 数据
        { "面板管理",     new Color(0.400f, 0.529f, 0.729f, 1f) }, // Slate-Blue — 中性面板
        { "资产工具",     new Color(0.282f, 0.690f, 0.404f, 1f) }, // Emerald — 资产
        { "编辑器工具",   new Color(0.580f, 0.490f, 0.820f, 1f) }, // Violet — 编辑器
        { "文件工具",     new Color(0.843f, 0.620f, 0.220f, 1f) }, // Amber — 文件
        { "字体工具",     new Color(0.780f, 0.435f, 0.608f, 1f) }, // Pink — 字体
        { "媒体工具",     new Color(0.804f, 0.706f, 0.290f, 1f) }, // Yellow — 媒体
        { "UI 工具",     new Color(0.345f, 0.569f, 0.910f, 1f) }, // Blue — UI
        { "调试工具",     new Color(0.808f, 0.376f, 0.443f, 1f) }, // Rose — 调试
        { "路径工具",     new Color(0.400f, 0.529f, 0.729f, 1f) }, // Slate-Blue — 路径
        { "文本工具",     new Color(0.282f, 0.690f, 0.690f, 1f) }, // Teal — 文本
        { "包管理工具",   new Color(0.580f, 0.490f, 0.820f, 1f) }, // Violet — 包管理
        { "项目工具",     new Color(0.345f, 0.569f, 0.910f, 1f) }, // Blue — 项目
        { "构建工具",     new Color(0.282f, 0.690f, 0.404f, 1f) }, // Emerald — 构建
        { "数据处理",     new Color(0.282f, 0.690f, 0.690f, 1f) }, // Teal — 数据处理
        { "序列化工具",   new Color(0.804f, 0.706f, 0.290f, 1f) }, // Yellow — 序列化
    };
    #endregion

    #region 分类图标（Unity 版本自适应）
    private static readonly Dictionary<string, string> _categoryIconsEmoji = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "框架初始化",   "⚙"   },
        { "数据管理",     "📊" },
        { "面板管理",     "📋" },
        { "资产工具",     "📦" },
        { "编辑器工具",   "🔧" },
        { "文件工具",     "📂" },
        { "字体工具",     "🔤" },
        { "媒体工具",     "🎬" },
        { "UI 工具",     "🎨" },
        { "调试工具",     "🐛" },
        { "路径工具",     "🗺" },
        { "文本工具",     "📝" },
        { "包管理工具",   "📦" },
        { "项目工具",     "📦" },
        { "构建工具",     "🔨" },
        { "数据处理",     "📊" },
        { "序列化工具",   "🔗" },
    };

    private static readonly Dictionary<string, string> _categoryIconsLegacy = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "框架初始化",   "⚙"   },
        { "数据管理",     "▣" },
        { "面板管理",     "▤" },
        { "资产工具",     "◆" },
        { "编辑器工具",   "✎" },
        { "文件工具",     "►" },
        { "字体工具",     "A" },
        { "媒体工具",     "▮" },
        { "UI 工具",     "◜" },
        { "调试工具",     "●" },
        { "路径工具",     "◇" },
        { "文本工具",     "T" },
        { "包管理工具",   "◆" },
        { "项目工具",     "◆" },
        { "构建工具",     "▣" },
        { "数据处理",     "▣" },
        { "序列化工具",   "◇" },
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
    // ── 未知分类调色板（语义色循环，自动分配）──
    public static readonly Color[] DefaultPalette = new[]
    {
        new Color(0.345f, 0.569f, 0.910f, 1f), // Blue
        new Color(0.282f, 0.690f, 0.404f, 1f), // Emerald
        new Color(0.843f, 0.620f, 0.220f, 1f), // Amber
        new Color(0.580f, 0.490f, 0.820f, 1f), // Violet
        new Color(0.282f, 0.690f, 0.690f, 1f), // Teal
        new Color(0.400f, 0.529f, 0.729f, 1f), // Slate-Blue
    };
    #endregion
}
#endif
