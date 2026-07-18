#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// Hub 共享调色板（单一来源）
///
/// 采用更柔和的深色冷灰底色 + 低饱和蓝强调色的现代编辑器主题。
/// 层级通过明度差（而非色相差）建立，减少刺眼感并提升整体协调性。
/// </summary>
public static class Palette
{
    // ── 背景色（Zinc 暖灰系，3 层明度递进）──────────────
    public static readonly Color Bg           = new Color(0.120f, 0.126f, 0.142f, 1f); // #1E2130
    public static readonly Color LeftBg       = new Color(0.110f, 0.116f, 0.132f, 1f); // #1B1D24
    public static readonly Color RightBg      = new Color(0.136f, 0.142f, 0.160f, 1f); // #232942
    public static readonly Color Splitter     = new Color(0.076f, 0.082f, 0.096f, 1f); // #131720
    public static readonly Color ToolbarBg    = new Color(0.102f, 0.108f, 0.123f, 1f); // #1A1E28
    public static readonly Color SearchBg     = new Color(0.104f, 0.111f, 0.128f, 1f); // #1B1E2A
    public static readonly Color ItemBg       = new Color(0.162f, 0.170f, 0.191f, 1f); // #2A3040
    public static readonly Color ItemHover    = new Color(0.192f, 0.201f, 0.225f, 1f); // #313A49
    public static readonly Color ItemSelected = new Color(0.260f, 0.380f, 0.620f, 0.30f); // 柔和选中
    public static readonly Color Selection    = new Color(0.260f, 0.380f, 0.620f, 0.34f);
    public static readonly Color Hover        = new Color(1f, 1f, 1f, 0.04f);
    public static readonly Color CardBg       = new Color(0.170f, 0.177f, 0.198f, 1f); // #2B2E3A
    public static readonly Color GroupBoxBg   = new Color(0.146f, 0.152f, 0.171f, 1f); // #252C3A
    public static readonly Color TagBg        = new Color(0.180f, 0.188f, 0.209f, 1f); // #2E3340
    public static readonly Color StatusBar    = new Color(0.088f, 0.094f, 0.108f, 1f); // #161B20
    public static readonly Color IconBg       = new Color(0.206f, 0.214f, 0.238f, 1f); // #343B4C
    public static readonly Color HelpBoxBg    = new Color(0.141f, 0.147f, 0.166f, 1f); // #242B3D
    public static readonly Color ProgressBg   = new Color(0.110f, 0.116f, 0.132f, 1f); // #1A1E2A
    public static readonly Color KeyCapBg     = new Color(0.158f, 0.166f, 0.185f, 1f); // #2A2E3B

    // ── 文字色（Zinc 系，提高暗色对比度）─────────────────
    public static readonly Color Text       = new Color(0.847f, 0.851f, 0.882f, 1f); // #D9D9E1
    public static readonly Color TextDim    = new Color(0.569f, 0.580f, 0.620f, 1f); // #9199A0
    public static readonly Color TextBright = new Color(0.949f, 0.953f, 0.976f, 1f); // #F2F2F9

    // ── 主题色（柔和蓝-靛色，专业且不刺眼）─────────────────
    public static readonly Color Accent    = new Color(0.443f, 0.618f, 0.918f, 1f); // #6F9FF0
    public static readonly Color AccentDim = new Color(0.443f, 0.618f, 0.918f, 0.45f);
    public static readonly Color Divider   = new Color(1f, 1f, 1f, 0.08f);

    // ── 按钮色 ──────────────────────────────────────────────
    public static readonly Color BtnNormal    = new Color(0.375f, 0.519f, 0.850f, 1f); // #6084D8
    public static readonly Color BtnHover     = new Color(0.463f, 0.631f, 0.923f, 1f); // #769FF0
    public static readonly Color BtnDanger    = new Color(0.710f, 0.255f, 0.278f, 1f); // #B54147
    public static readonly Color BtnDangerHov = new Color(0.812f, 0.337f, 0.357f, 1f); // #CF565B
    public static readonly Color BtnSuccess   = new Color(0.220f, 0.600f, 0.337f, 1f); // #389956
    public static readonly Color BtnSuccessHov= new Color(0.282f, 0.690f, 0.404f, 1f); // #48B067
    public static readonly Color BtnWarn      = new Color(0.741f, 0.541f, 0.165f, 1f); // #BD8A2A
    public static readonly Color BtnWarnHov   = new Color(0.843f, 0.620f, 0.220f, 1f); // #D79E38

    // ── 语义色 ──────────────────────────────────────────────
    public static readonly Color Success  = new Color(0.282f, 0.690f, 0.404f, 1f); // #48B067
    public static readonly Color Warning  = new Color(0.843f, 0.620f, 0.220f, 1f); // #D79E38
    public static readonly Color Error    = new Color(0.812f, 0.337f, 0.357f, 1f); // #CF565B
    public static readonly Color Info     = new Color(0.345f, 0.569f, 0.910f, 1f); // #5891E8

    // ── 拖拽叠加色 ──────────────────────────────────────────
    public static readonly Color DropOverlay = new Color(0.345f, 0.569f, 0.910f, 0.16f);
    public static readonly Color DropBorder  = new Color(0.345f, 0.569f, 0.910f, 0.55f);

    // ── 分类配色（语义化 5 色系，同类共享色相）──────────
    // 蓝 = 创建/构建 / 青 = 数据/管理 / 琥珀 = 配置/工具 / 玫瑰 = 调试/危险 / 石板 = 中性
    public static readonly Color CatDefault = new Color(0.400f, 0.529f, 0.729f, 1f); // #6687BA Slate-Blue
    public static readonly Color CatGreen   = new Color(0.282f, 0.690f, 0.404f, 1f); // #48B067 Emerald
    public static readonly Color CatOrange  = new Color(0.843f, 0.620f, 0.220f, 1f); // #D79E38 Amber
    public static readonly Color CatPurple  = new Color(0.580f, 0.490f, 0.820f, 1f); // #947DD1 Violet
    public static readonly Color CatRed     = new Color(0.808f, 0.376f, 0.443f, 1f); // #CE6071 Rose
    public static readonly Color CatTeal    = new Color(0.282f, 0.690f, 0.690f, 1f); // #48B0B0 Teal
    public static readonly Color CatPink    = new Color(0.780f, 0.435f, 0.608f, 1f); // #C76F9B Pink
    public static readonly Color CatYellow  = new Color(0.804f, 0.706f, 0.290f, 1f); // #CDB44A Yellow

    // ── 纹理创建（共享，供未继承 ToolEditorWindow 的工具使用）──
    /// <summary>创建单色 1x1 纹理（HideAndDontSave），统一替代各工具重复的 MakeTex</summary>
    public static Texture2D MakeTex(int w, int h, Color color)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = color;
        tex.SetPixels(px);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        return tex;
    }
}
#endif
