#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Hub 绘图工具方法
/// 纹理创建、箭头绘制、渐变矩形、资源清理
/// </summary>
public static class Drawing
{
    #region 纹理创建

    /// <summary>创建单色纹理（委托 HubPalette.MakeTex）</summary>
    public static Texture2D MakeTex(int w, int h, Color color)
    {
        return Palette.MakeTex(w, h, color);
    }

    #endregion

    #region 折叠箭头

    private static Texture2D _texArrowExpanded;
    private static Texture2D _texArrowCollapsed;

    /// <summary>绘制折叠箭头（预烘焙纹理，FilterMode.Point 锐利渲染）</summary>
    public static void DrawFoldoutArrow(Rect rect, bool expanded)
    {
        var tex = expanded ? ArrowExpanded : ArrowCollapsed;
        if (tex == null) return;

        const float size = 8f;
        float x = rect.x + (rect.width - size) * 0.5f;
        float y = rect.y + (rect.height - size) * 0.5f;
        GUI.DrawTexture(new Rect(x, y, size, size), tex, ScaleMode.StretchToFill);
    }

    public static Texture2D ArrowExpanded
        => _texArrowExpanded ?? (_texArrowExpanded = MakeArrowTex(true));

    public static Texture2D ArrowCollapsed
        => _texArrowCollapsed ?? (_texArrowCollapsed = MakeArrowTex(false));

    /// <summary>预烘焙箭头纹理（16x16 高分辨率，FilterMode.Point 保持锐利）</summary>
    public static Texture2D MakeArrowTex(bool expanded)
    {
        const int SIZE = 16;
        var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
        var px = new Color[SIZE * SIZE];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        Color c = Theme.ClrTextDim;
        if (expanded)
        {
            // ▼ 向下三角：每行宽度递增，水平居中
            for (int row = 0; row < SIZE; row++)
            {
                int lineW = System.Math.Min(SIZE, (row + 1) * SIZE / (SIZE - 1));
                int startX = (SIZE - lineW) / 2;
                for (int col = 0; col < lineW; col++)
                    px[row * SIZE + startX + col] = c;
            }
        }
        else
        {
            // ▶ 向右三角：左侧底边最宽，右侧尖端最窄
            for (int col = 0; col < SIZE; col++)
            {
                int lineH = System.Math.Min(SIZE, (SIZE - col) * SIZE / (SIZE - 1));
                int startY = (SIZE - lineH) / 2;
                for (int dy = 0; dy < lineH; dy++)
                    px[(startY + dy) * SIZE + col] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        tex.hideFlags = HideFlags.HideAndDontSave;
        return tex;
    }

    #endregion

    #region 渐变绘制

    public static void DrawGradientRect(Rect rect, Color left, Color right)
    {
        DrawHorizontalGradient(rect, left, right);
    }

    // ── 渐变纹理缓存（按颜色对缓存，避免逐像素 DrawRect 循环）──
    private static readonly System.Collections.Generic.Dictionary<int, Texture2D> _gradientCache
        = new System.Collections.Generic.Dictionary<int, Texture2D>();
    private const int GradientTexWidth = 64;

    private static int ColorPairKey(Color a, Color b)
    {
        // 将 Color（32bit rgba）打包为 int，组合两色为 key
        unchecked
        {
            int ha = ((int)(a.r * 255) << 24) | ((int)(a.g * 255) << 16) | ((int)(a.b * 255) << 8) | (int)(a.a * 255);
            int hb = ((int)(b.r * 255) << 24) | ((int)(b.g * 255) << 16) | ((int)(b.b * 255) << 8) | (int)(b.a * 255);
            return (ha * 397) ^ hb;
        }
    }

    /// <summary>绘制水平渐变矩形（带纹理缓存，替代逐像素 DrawRect 循环）</summary>
    public static void DrawHorizontalGradient(Rect rect, Color left, Color right)
    {
        var tex = GetGradientTexture(left, right);
        if (tex == null)
        {
            // 回退：单色填充
            EditorGUI.DrawRect(rect, left);
            return;
        }
        // 保存并恢复 GUI.color，避免污染外部着色状态
        var prevColor = GUI.color;
        GUI.color = Color.white;
        GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill);
        GUI.color = prevColor;
    }

    /// <summary>获取或生成水平渐变纹理（按颜色对缓存）</summary>
    public static Texture2D GetGradientTexture(Color left, Color right)
    {
        int key = ColorPairKey(left, right);
        if (_gradientCache.TryGetValue(key, out var cached))
            return cached;

        var tex = new Texture2D(GradientTexWidth, 1, TextureFormat.RGBA32, false);
        var px = new Color32[GradientTexWidth];
        for (int i = 0; i < GradientTexWidth; i++)
        {
            float t = (float)i / (GradientTexWidth - 1);
            px[i] = Color.Lerp(left, right, t);
        }
        tex.SetPixels32(px);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        _gradientCache[key] = tex;
        return tex;
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
        if (_texArrowExpanded != null)  { Object.DestroyImmediate(_texArrowExpanded);  _texArrowExpanded = null; }
        if (_texArrowCollapsed != null) { Object.DestroyImmediate(_texArrowCollapsed); _texArrowCollapsed = null; }

        foreach (var tex in _gradientCache.Values)
        {
            if (tex != null) Object.DestroyImmediate(tex);
        }
        _gradientCache.Clear();
    }

    #endregion
}
#endif
