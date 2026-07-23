#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Hub GUIStyle 缓存与初始化
/// 所有 GUI 样式集中管理，懒加载初始化
/// </summary>
public static class Styles
{
    #region 样式缓存
    private static GUIStyle _styleCategoryHeader;
    private static GUIStyle _styleToolItem;
    private static GUIStyle _styleToolItemSelected;
    private static GUIStyle _styleRightTitle;
    private static GUIStyle _styleRightSubtitle;
    private static GUIStyle _styleDescription;
    private static GUIStyle _styleCard;
    private static GUIStyle _styleTag;
    private static GUIStyle _styleWelcomeTitle;
    private static GUIStyle _styleWelcomeSub;
    private static GUIStyle _styleStatNum;
    private static GUIStyle _styleStatLabel;
    private static GUIStyle _styleBtnPrimary;
    private static GUIStyle _styleBtnFlat;
    private static GUIStyle _styleSectionHeader;
    private static GUIStyle _styleShortcut;
    private static GUIStyle _styleInvisibleBtn;
    private static GUIStyle _styleLogo;
    private static GUIStyle _styleVersion;
    private static GUIStyle _styleCatCardIcon;
    private static GUIStyle _styleCatCardName;
    private static GUIStyle _styleCatCardCount;
    private static GUIStyle _styleBackButton;
    private static GUIStyle _styleEmptyHint;
    private static GUIStyle _styleKeyCap;
    private static GUIStyle _styleHiddenItemName;
    private static GUIStyle _styleHiddenItemDesc;
    // ── LeftPanel / RightPanel 每帧复用样式（消除 OnGUI 内 GUIStyle 分配）──
    private static GUIStyle _styleSearchPlaceholder;
    private static GUIStyle _styleSortButton;
    private static GUIStyle _styleFoldButton;
    private static GUIStyle _styleMiniLabelBoldCenter;
    private static GUIStyle _styleCategoryTagSearch;

    private static Texture2D _texWhite;
    private static Texture2D _texHover;
    private static Texture2D _texSelected;
    private static Texture2D _texTransparent;
    private static bool _stylesReady;
    #endregion

    #region 纹理访问
    public static Texture2D TexWhite       => _texWhite;
    public static Texture2D TexHover       => _texHover;
    public static Texture2D TexSelected    => _texSelected;
    public static Texture2D TexTransparent => _texTransparent;
    #endregion

    #region 样式访问（懒加载）
    public static GUIStyle CategoryHeader   => EnsureInit() ? _styleCategoryHeader : null;
    public static GUIStyle ToolItem         => EnsureInit() ? _styleToolItem : null;
    public static GUIStyle ToolItemSelected => EnsureInit() ? _styleToolItemSelected : null;
    public static GUIStyle RightTitle       => EnsureInit() ? _styleRightTitle : null;
    public static GUIStyle RightSubtitle    => EnsureInit() ? _styleRightSubtitle : null;
    public static GUIStyle Description      => EnsureInit() ? _styleDescription : null;
    public static GUIStyle Card             => EnsureInit() ? _styleCard : null;
    public static GUIStyle Tag              => EnsureInit() ? _styleTag : null;
    public static GUIStyle WelcomeTitle     => EnsureInit() ? _styleWelcomeTitle : null;
    public static GUIStyle WelcomeSub       => EnsureInit() ? _styleWelcomeSub : null;
    public static GUIStyle StatNum          => EnsureInit() ? _styleStatNum : null;
    public static GUIStyle StatLabel        => EnsureInit() ? _styleStatLabel : null;
    public static GUIStyle BtnPrimary       => EnsureInit() ? _styleBtnPrimary : null;
    public static GUIStyle BtnFlat          => EnsureInit() ? _styleBtnFlat : null;
    public static GUIStyle SectionHeader    => EnsureInit() ? _styleSectionHeader : null;
    public static GUIStyle Shortcut         => EnsureInit() ? _styleShortcut : null;
    public static GUIStyle InvisibleBtn     => EnsureInit() ? _styleInvisibleBtn : null;
    public static GUIStyle Logo             => EnsureInit() ? _styleLogo : null;
    public static GUIStyle Version          => EnsureInit() ? _styleVersion : null;
    public static GUIStyle CatCardIcon      => EnsureInit() ? _styleCatCardIcon : null;
    public static GUIStyle CatCardName      => EnsureInit() ? _styleCatCardName : null;
    public static GUIStyle CatCardCount     => EnsureInit() ? _styleCatCardCount : null;
    public static GUIStyle BackButton       => EnsureInit() ? _styleBackButton : null;
    public static GUIStyle EmptyHint        => EnsureInit() ? _styleEmptyHint : null;
    public static GUIStyle KeyCap           => EnsureInit() ? _styleKeyCap : null;
    public static GUIStyle HiddenItemName   => EnsureInit() ? _styleHiddenItemName : null;
    public static GUIStyle HiddenItemDesc   => EnsureInit() ? _styleHiddenItemDesc : null;

    // ── LeftPanel / RightPanel 复用样式 ──
    public static GUIStyle SearchPlaceholder      => EnsureInit() ? _styleSearchPlaceholder : null;
    public static GUIStyle SortButton             => EnsureInit() ? _styleSortButton : null;
    public static GUIStyle FoldButton             => EnsureInit() ? _styleFoldButton : null;
    public static GUIStyle MiniLabelBoldCenter    => EnsureInit() ? _styleMiniLabelBoldCenter : null;
    public static GUIStyle CategoryTagSearch      => EnsureInit() ? _styleCategoryTagSearch : null;
    #endregion

    #region 样式初始化
    public static bool EnsureInit()
    {
        if (_stylesReady) return true;

        // ── 创建背景纹理 ──────────────────────────────────────────
        _texWhite = Palette.MakeTex(1, 1, Color.white);
        _texHover = Palette.MakeTex(1, 1, Theme.ClrHover);
        _texSelected = Palette.MakeTex(1, 1, Theme.ClrSelection);
        _texTransparent = Palette.MakeTex(1, 1, new Color(0, 0, 0, 0));

        // ── 左侧分类标题 ──
        _styleCategoryHeader = new GUIStyle()
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(18, 8, 0, 0),
            margin = new RectOffset(0, 0, 0, 0),
            normal = { textColor = Theme.ClrTextDim, background = _texTransparent },
            richText = true
        };

        // ── 工具项 ──
        _styleToolItem = new GUIStyle(EditorStyles.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(26, 8, 0, 0),
            margin = new RectOffset(0, 0, 0, 0),
            normal = { textColor = Theme.ClrText, background = _texTransparent },
            hover = { textColor = Theme.ClrTextBright, background = _texHover },
            active = { textColor = Theme.ClrTextBright, background = _texSelected },
            richText = true
        };

        // ── 工具项选中态 ──
        _styleToolItemSelected = new GUIStyle(_styleToolItem)
        {
            fontStyle = FontStyle.Bold,
            normal = { textColor = Theme.ClrTextBright, background = _texSelected },
            hover = { textColor = Color.white, background = _texSelected },
            active = { textColor = Color.white, background = _texSelected }
        };

        // ── 右侧标题（加大）──
        _styleRightTitle = new GUIStyle()
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Theme.ClrTextBright },
            richText = true,
            padding = new RectOffset(0, 0, 0, 0)
        };

        _styleRightSubtitle = new GUIStyle()
        {
            fontSize = 12,
            normal = { textColor = Theme.ClrTextDim },
            richText = true
        };

        // ── 描述文本 ──
        _styleDescription = new GUIStyle()
        {
            fontSize = 13,
            wordWrap = true,
            normal = { textColor = Theme.ClrText },
            richText = true,
            padding = new RectOffset(2, 2, 2, 2)
        };

        // ── 卡片 ──
        _styleCard = new GUIStyle()
        {
            padding = new RectOffset(14, 14, 12, 12)
        };

        // ── 标签 ──
        _styleTag = new GUIStyle()
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Theme.ClrText },
            padding = new RectOffset(8, 8, 3, 3)
        };

        // ── 欢迎页标题（加大、加间距）──
        _styleWelcomeTitle = new GUIStyle()
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Theme.ClrTextBright },
            richText = true
        };

        _styleWelcomeSub = new GUIStyle()
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Theme.ClrTextDim },
            richText = true
        };

        _styleStatNum = new GUIStyle()
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Theme.ClrAccent }
        };

        _styleStatLabel = new GUIStyle()
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Theme.ClrTextDim }
        };

        // ── 按钮 ──
        _styleBtnPrimary = new GUIStyle()
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
            hover = { textColor = Color.white },
            active = { textColor = new Color(0.82f, 0.82f, 0.82f) },
            padding = new RectOffset(16, 16, 8, 8)
        };

        _styleBtnFlat = new GUIStyle()
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Theme.ClrText },
            hover = { textColor = Theme.ClrTextBright },
            padding = new RectOffset(10, 10, 6, 6)
        };

        // ── 分节标题（加粗、提高对比度）──
        _styleSectionHeader = new GUIStyle()
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Theme.ClrTextDim },
            padding = new RectOffset(0, 0, 4, 4)
        };

        // ── 快捷键标签 ──
        _styleShortcut = new GUIStyle()
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Theme.ClrTextDim },
            padding = new RectOffset(5, 5, 2, 2)
        };

        // ── 透明按钮 ──
        _styleInvisibleBtn = new GUIStyle()
        {
            normal = { background = _texTransparent },
            hover = { background = _texTransparent },
            active = { background = _texTransparent },
            focused = { background = _texTransparent }
        };

        // ── Logo ──
        _styleLogo = new GUIStyle()
        {
            fontSize = 16,
            richText = true,
            normal = { textColor = Theme.ClrTextBright },
            padding = new RectOffset(0, 0, 2, 2)
        };

        // ── 版本信息 ──
        _styleVersion = new GUIStyle()
        {
            richText = true,
            normal = { textColor = Theme.ClrTextDim }
        };

        // ── 分类卡片图标 ──
        _styleCatCardIcon = new GUIStyle()
        {
            fontSize = 14,
            normal = { textColor = Theme.ClrAccent }
        };

        _styleCatCardName = new GUIStyle()
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Theme.ClrText },
            clipping = TextClipping.Clip
        };

        _styleCatCardCount = new GUIStyle()
        {
            fontSize = 9,
            normal = { textColor = Theme.ClrTextDim }
        };

        // ── 返回按钮（提高对比度）──
        _styleBackButton = new GUIStyle()
        {
            fontSize = 11,
            normal = { textColor = Theme.ClrTextDim },
            hover = { textColor = Theme.ClrTextBright },
            padding = new RectOffset(0, 0, 2, 2)
        };

        _styleEmptyHint = new GUIStyle()
        {
            fontSize = 11,
            normal = { textColor = Theme.ClrTextDim },
            padding = new RectOffset(8, 0, 4, 4)
        };

        // ── 快捷键键帽 ──
        _styleKeyCap = new GUIStyle()
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Theme.ClrTextBright }
        };

        _styleHiddenItemName = new GUIStyle()
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Theme.ClrTextBright },
            clipping = TextClipping.Clip
        };

        _styleHiddenItemDesc = new GUIStyle()
        {
            fontSize = 9,
            normal = { textColor = Theme.ClrTextDim },
            clipping = TextClipping.Clip
        };

        // ── LeftPanel / RightPanel 复用样式初始化 ──
        _styleSearchPlaceholder = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = Theme.ClrTextDim },
            alignment = TextAnchor.MiddleLeft
        };

        _styleSortButton = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 10
        };

        _styleFoldButton = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            fontStyle = FontStyle.Bold
        };

        _styleMiniLabelBoldCenter = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 12
        };

        _styleCategoryTagSearch = new GUIStyle(EditorStyles.miniLabel)
        {
            fontStyle = FontStyle.Italic,
            fontSize = 9,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(4, 4, 0, 0)
        };

        _stylesReady = true;
        return true;
    }
    #endregion

    #region 资源清理

    [UnityEditor.InitializeOnLoadMethod]
    private static void RegisterCleanup()
    {
        AssemblyReloadEvents.beforeAssemblyReload += CleanupTextures;
    }

    private static void CleanupTextures()
    {
        if (_texWhite != null)       { Object.DestroyImmediate(_texWhite);       _texWhite = null; }
        if (_texHover != null)       { Object.DestroyImmediate(_texHover);       _texHover = null; }
        if (_texSelected != null)    { Object.DestroyImmediate(_texSelected);    _texSelected = null; }
        if (_texTransparent != null) { Object.DestroyImmediate(_texTransparent); _texTransparent = null; }
        _stylesReady = false;
    }

    #endregion
}
#endif
