// ColorUtils.cs
// RGB ↔ HSL 转换工具，用于发色替换等像素级颜色变换。
// 参考公式：new_pixel = HSL(target.H, target.S, original.L)
using Godot;

namespace BladeHex.View.Unit;

/// <summary>RGB ↔ HSL 转换与重着色工具。</summary>
public static class ColorUtils
{
    /// <summary>将 Color (RGBA) 转换为 HSL 分量。</summary>
    /// <param name="h">色相 0~360</param>
    /// <param name="s">饱和度 0~1</param>
    /// <param name="l">明度 0~1</param>
    public static void RgbToHsl(Color c, out float h, out float s, out float l)
    {
        float r = c.R, g = c.G, b = c.B;
        float max = Mathf.Max(r, Mathf.Max(g, b));
        float min = Mathf.Min(r, Mathf.Min(g, b));
        float delta = max - min;

        // 明度
        l = (max + min) * 0.5f;

        if (delta < 1e-6f)
        {
            h = 0f;
            s = 0f;
            return;
        }

        // 饱和度
        s = l <= 0.5f ? delta / (max + min) : delta / (2f - max - min);

        // 色相
        if (Mathf.Abs(max - r) < 1e-6f)
            h = (g - b) / delta + (g < b ? 6f : 0f);
        else if (Mathf.Abs(max - g) < 1e-6f)
            h = (b - r) / delta + 2f;
        else
            h = (r - g) / delta + 4f;

        h *= 60f; // 转 0~360 度
    }

    /// <summary>将 HSL 分量转换回 Color (alpha 保留)。</summary>
    /// <param name="h">色相 0~360</param>
    /// <param name="s">饱和度 0~1</param>
    /// <param name="l">明度 0~1</param>
    /// <param name="alpha">透明度，默认 1.0</param>
    public static Color HslToRgb(float h, float s, float l, float alpha = 1f)
    {
        if (s < 1e-6f)
        {
            float gray = Mathf.Clamp(l, 0f, 1f);
            return new Color(gray, gray, gray, alpha);
        }

        float hue = h / 360f; // 归一化到 0~1
        float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
        float p = 2f * l - q;

        float r = HueToRgb(p, q, hue + 1f / 3f);
        float g = HueToRgb(p, q, hue);
        float b = HueToRgb(p, q, hue - 1f / 3f);

        return new Color(r, g, b, alpha);
    }

    private static float HueToRgb(float p, float q, float t)
    {
        if (t < 0f) t += 1f;
        if (t > 1f) t -= 1f;
        if (t < 1f / 6f) return p + (q - p) * 6f * t;
        if (t < 1f / 2f) return q;
        if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
        return p;
    }

    /// <summary>
    /// 对像素应用"发色替换"变换：
    /// new = HSL(targetHue, targetSat, lightness)
    /// 保留原始像素的透明度，替换为目标的色相和饱和度。
    /// 明度来源由 overrideLightness 控制：
    ///   - < 0   → 使用原像素明度（保留明暗关系）
    ///   - >= 0  → 使用固定明度（银白/纯黑等特殊发色）
    /// </summary>
    /// <param name="original">原始像素颜色</param>
    /// <param name="targetHue">目标色相（0~360）</param>
    /// <param name="targetSat">目标饱和度（0~1）</param>
    /// <param name="overrideLightness">明度覆盖值（-1 = 保留原明度，0~1 = 固定明度）</param>
    /// <returns>重着色后的颜色</returns>
    public static Color RecolorHairPixel(Color original, float targetHue, float targetSat, float overrideLightness = -1f)
    {
        // 透明像素跳过
        if (original.A < 1f / 255f)
            return original;

        RgbToHsl(original, out float h, out float s, out float l);

        // 决定使用的明度
        float finalL = overrideLightness >= 0f ? overrideLightness : l;

        // 核心公式：new_pixel = HSL(target.H, target.S, lightness)
        return HslToRgb(targetHue, targetSat, finalL, original.A);
    }

    /// <summary>
    /// 对整个 Image 应用发色替换。
    /// </summary>
    /// <param name="image">要修改的图像（会被原地修改）</param>
    /// <param name="targetHue">目标色相（0~360）</param>
    /// <param name="targetSat">目标饱和度（0~1）</param>
    /// <param name="overrideLightness">明度覆盖值（-1 = 保留原明度，0~1 = 固定明度）</param>
    public static void RecolorHairImage(Image image, float targetHue, float targetSat, float overrideLightness = -1f)
    {
        int w = image.GetWidth();
        int h = image.GetHeight();

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var original = image.GetPixel(x, y);
                var recolored = RecolorHairPixel(original, targetHue, targetSat, overrideLightness);
                image.SetPixel(x, y, recolored);
            }
        }
    }
}
