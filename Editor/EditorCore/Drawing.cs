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

    #region 圆角矩形

    /// <summary>绘制圆角矩形：中心矩形 + 4 角纹理合成</summary>
    public static void DrawRoundedRect(Rect rect, Color color, float radius = 8f)
    {
        if (radius <= 0.5f || rect.width < 2f || rect.height < 2f)
        {
            EditorGUI.DrawRect(rect, color);
            return;
        }

        float maxRadius = Mathf.Min(radius, rect.width * 0.5f, rect.height * 0.5f);
        if (maxRadius < 1.25f)
        {
            EditorGUI.DrawRect(rect, color);
            return;
        }

        int size = Mathf.CeilToInt(maxRadius);

        // 中心矩形（全宽，不含上下角区域）
        float centerH = rect.height - size * 2f;
        if (centerH > 0f)
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + size, rect.width, centerH), color);

        // 左右边缘条（角之间的竖条，仅在宽度足够时需要）
        if (size < rect.width)
        {
            // 已被中心矩形覆盖，无需额外绘制
        }

        // 4 角纹理（圆角区域：圆内有色，圆外透明）
        var tlRect = new Rect(rect.x, rect.y, size, size);
        var trRect = new Rect(rect.xMax - size, rect.y, size, size);
        var blRect = new Rect(rect.x, rect.yMax - size, size, size);
        var brRect = new Rect(rect.xMax - size, rect.yMax - size, size, size);

        var prevColor = GUI.color;
        GUI.color = Color.white;
        GUI.DrawTexture(tlRect, GetCornerTex(size, color, 0), ScaleMode.StretchToFill);
        GUI.DrawTexture(trRect, GetCornerTex(size, color, 1), ScaleMode.StretchToFill);
        GUI.DrawTexture(blRect, GetCornerTex(size, color, 3), ScaleMode.StretchToFill);
        GUI.DrawTexture(brRect, GetCornerTex(size, color, 2), ScaleMode.StretchToFill);
        GUI.color = prevColor;
    }

    // ── 四个方向的圆角纹理缓存 ──
    private const int MaxCornerCacheSize = 64;
    private static readonly System.Collections.Generic.Dictionary<int, Texture2D> _cornerTexCache
        = new System.Collections.Generic.Dictionary<int, Texture2D>();

    private static int CornerKey(int size, Color color, int corner)
    {
        unchecked
        {
            return (size * 31 + corner) * 31
                + ((int)(color.r * 255) << 16)
                + ((int)(color.g * 255) << 8)
                + (int)(color.b * 255);
        }
    }

    private static Texture2D GetCornerTex(int size, Color color, int corner)
    {
        int key = CornerKey(size, color, corner);
        if (_cornerTexCache.TryGetValue(key, out var cached))
            return cached;

        // 缓存上限保护：超限时清空重建
        if (_cornerTexCache.Count >= MaxCornerCacheSize)
        {
            foreach (var oldTex in _cornerTexCache.Values)
            {
                if (oldTex != null) Object.DestroyImmediate(oldTex);
            }
            _cornerTexCache.Clear();
        }

        // 用 2x 超采样生成纹理后缩放，获得抗锯齿边缘。
        // 圆角纹理必须用“单个角的四分之一圆”来生成，不能把圆心放到 2r 位置，否则会把整片角都渲染成满透明/满不透明的块状。
        const int ss = 2;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];

        // 圆心位置取决于角的方向：
        // TL(0): 圆心在右下 → (size, size)
        // TR(1): 圆心在左下 → (0, size)
        // BR(2): 圆心在左上 → (0, 0)
        // BL(3): 圆心在右上 → (size, 0)
        float cx = (corner == 0 || corner == 3) ? size : 0f;
        float cy = (corner == 0 || corner == 1) ? size : 0f;
        float radius = size - 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 对每个目标像素做 ss×ss 超采样，取平均值实现 AA
                float alpha = 0f;
                for (int sy = 0; sy < ss; sy++)
                {
                    for (int sx = 0; sx < ss; sx++)
                    {
                        float px2 = x * ss + sx + 0.5f;
                        float py2 = y * ss + sy + 0.5f;
                        float dx = px2 - cx;
                        float dy = py2 - cy;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        if (dist <= radius)
                            alpha += 1f;
                    }
                }
                alpha /= (ss * ss);

                byte a = (byte)(alpha * 255);
                px[y * size + x] = new Color32(
                    (byte)(color.r * 255),
                    (byte)(color.g * 255),
                    (byte)(color.b * 255),
                    a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        _cornerTexCache[key] = tex;
        return tex;
    }

    private static void DrawCornerTL(Rect rect, int size, Color color)
        => GUI.DrawTexture(rect, GetCornerTex(size, color, 0), ScaleMode.StretchToFill);
    private static void DrawCornerTR(Rect rect, int size, Color color)
        => GUI.DrawTexture(rect, GetCornerTex(size, color, 1), ScaleMode.StretchToFill);
    private static void DrawCornerBR(Rect rect, int size, Color color)
        => GUI.DrawTexture(rect, GetCornerTex(size, color, 2), ScaleMode.StretchToFill);
    private static void DrawCornerBL(Rect rect, int size, Color color)
        => GUI.DrawTexture(rect, GetCornerTex(size, color, 3), ScaleMode.StretchToFill);

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
    private const int MaxGradientCacheSize = 32;
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

        // 缓存上限保护：超限时清空重建
        if (_gradientCache.Count >= MaxGradientCacheSize)
        {
            foreach (var oldTex in _gradientCache.Values)
            {
                if (oldTex != null) Object.DestroyImmediate(oldTex);
            }
            _gradientCache.Clear();
        }

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

        foreach (var tex in _cornerTexCache.Values)
        {
            if (tex != null) Object.DestroyImmediate(tex);
        }
        _cornerTexCache.Clear();
    }

    #endregion
}
#endif
