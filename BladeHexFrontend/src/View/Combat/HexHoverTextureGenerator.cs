// HexHoverTextureGenerator.cs
// 程序化生成六边形悬浮魔法阵纹理 — 类似法阵几何风格:
// - 六边形外框
// - 6 条从中心到顶点的射线
// - 每对对角顶点的连线(形成内部三角网)
// - 内接正方形(连接对边中点)
// - 6 个顶点发光点
// - 中心微弱发光
// 输出 256×256 带 alpha 的 ImageTexture,暗金色线条,黑色透明背景。
using Godot;
using System;

namespace BladeHex.View.Combat;

public static class HexHoverTextureGenerator
{
    private const int Size = 256;
    private const int Center = Size / 2;
    private const float Radius = Size * 0.45f; // 六边形半径(留边距)

    // 暗金色
    private static readonly Color LineColor = new(0.9f, 0.7f, 0.3f, 1f);
    private static readonly Color GlowColor = new(1.0f, 0.8f, 0.35f, 1f);

    private static ImageTexture? _cached;

    public static Texture2D Get()
    {
        if (_cached != null) return _cached;
        _cached = Generate();
        return _cached;
    }

    private static ImageTexture Generate()
    {
        var img = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);
        // 全透明背景
        img.Fill(new Color(0, 0, 0, 0));

        // 计算 6 个顶点(尖顶六边形:第一个顶点在正上方)
        var verts = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.DegToRad(60f * i - 90f); // -90° 让第一个顶点在正上方
            verts[i] = new Vector2(
                Center + Mathf.Cos(angle) * Radius,
                Center + Mathf.Sin(angle) * Radius);
        }

        // 1. 六边形外框
        for (int i = 0; i < 6; i++)
            DrawLine(img, verts[i], verts[(i + 1) % 6], LineColor, 2.2f);

        // 2. 从中心到每个顶点的射线
        var center = new Vector2(Center, Center);
        for (int i = 0; i < 6; i++)
            DrawLine(img, center, verts[i], LineColor * 0.6f, 1.0f);

        // 3. 对角线(每个顶点连到对面顶点)
        for (int i = 0; i < 3; i++)
            DrawLine(img, verts[i], verts[i + 3], LineColor * 0.45f, 0.8f);

        // 4. 相邻顶点跳一个连线(形成内部三角形)
        for (int i = 0; i < 6; i++)
            DrawLine(img, verts[i], verts[(i + 2) % 6], LineColor * 0.35f, 0.7f);

        // 5. 顶点发光点
        for (int i = 0; i < 6; i++)
            DrawGlow(img, (int)verts[i].X, (int)verts[i].Y, 10, GlowColor);

        // 6. 中心微弱发光
        DrawGlow(img, Center, Center, 14, GlowColor * 0.25f);

        return ImageTexture.CreateFromImage(img);
    }

    private static void DrawLine(Image img, Vector2 a, Vector2 b, Color color, float thickness)
    {
        float dist = a.DistanceTo(b);
        int steps = (int)(dist * 2); // 2 samples per pixel for smoothness
        float halfT = thickness * 0.5f;

        for (int s = 0; s <= steps; s++)
        {
            float t = (float)s / steps;
            float px = Mathf.Lerp(a.X, b.X, t);
            float py = Mathf.Lerp(a.Y, b.Y, t);

            // 画粗线:在垂直方向扩展
            int iThick = (int)Mathf.Ceil(halfT) + 1;
            for (int dy = -iThick; dy <= iThick; dy++)
            {
                for (int dx = -iThick; dx <= iThick; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > halfT + 0.5f) continue;

                    int ix = (int)(px + dx);
                    int iy = (int)(py + dy);
                    if (ix < 0 || iy < 0 || ix >= Size || iy >= Size) continue;

                    // 抗锯齿:距离线中心越远越透明
                    float alpha = Mathf.Clamp(1f - (d - halfT + 0.5f), 0f, 1f);
                    var existing = img.GetPixel(ix, iy);
                    var blended = BlendOver(existing, new Color(color.R, color.G, color.B, color.A * alpha));
                    img.SetPixel(ix, iy, blended);
                }
            }
        }
    }

    private static void DrawGlow(Image img, int cx, int cy, int radius, Color color)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int ix = cx + dx, iy = cy + dy;
                if (ix < 0 || iy < 0 || ix >= Size || iy >= Size) continue;

                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist > radius) continue;

                // 径向衰减(中心亮,边缘暗)
                float falloff = 1f - (dist / radius);
                falloff = falloff * falloff; // 二次衰减更柔和

                var existing = img.GetPixel(ix, iy);
                var glowPixel = new Color(color.R, color.G, color.B, color.A * falloff);
                img.SetPixel(ix, iy, BlendOver(existing, glowPixel));
            }
        }
    }

    private static Color BlendOver(Color bg, Color fg)
    {
        float outA = fg.A + bg.A * (1f - fg.A);
        if (outA <= 0.001f) return new Color(0, 0, 0, 0);
        float outR = (fg.R * fg.A + bg.R * bg.A * (1f - fg.A)) / outA;
        float outG = (fg.G * fg.A + bg.G * bg.A * (1f - fg.A)) / outA;
        float outB = (fg.B * fg.A + bg.B * bg.A * (1f - fg.A)) / outA;
        return new Color(outR, outG, outB, outA);
    }
}
