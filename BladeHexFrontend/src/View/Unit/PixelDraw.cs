// PixelDraw.cs
// 像素级几何绘制原语 — 用于把 Image 当 canvas 程序化生成纹理。
// 共享给:
//   - UnitPlaceholderRenderer(人体剪影)
//   - EquipmentPlaceholderRenderer(装备剪影)
//
// 风格基准:与 ItemPlaceholderRenderer (Control._Draw) 同效果但走 Image 像素路径,
// 这样生成的 Texture2D 可直接喂给 AnimatedSprite3D / Sprite2D。
using Godot;

namespace BladeHex.View.Unit;

/// <summary>把 Image 当 canvas 用的几何绘制原语。所有方法对越界像素静默忽略。</summary>
public static class PixelDraw
{
    public static void Set(Image img, int x, int y, Color c)
    {
        if (x < 0 || y < 0 || x >= img.GetWidth() || y >= img.GetHeight()) return;
        if (c.A >= 0.999f) { img.SetPixel(x, y, c); return; }
        var bg = img.GetPixel(x, y);
        var outA = c.A + bg.A * (1f - c.A);
        if (outA <= 0.001f) return;
        var outR = (c.R * c.A + bg.R * bg.A * (1f - c.A)) / outA;
        var outG = (c.G * c.A + bg.G * bg.A * (1f - c.A)) / outA;
        var outB = (c.B * c.A + bg.B * bg.A * (1f - c.A)) / outA;
        img.SetPixel(x, y, new Color(outR, outG, outB, outA));
    }

    public static void FillRect(Image img, int x, int y, int w, int h, Color c)
    {
        for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++)
                Set(img, i, j, c);
    }

    public static void DrawRectOutline(Image img, int x, int y, int w, int h, Color c)
    {
        for (int i = x; i < x + w; i++) { Set(img, i, y, c); Set(img, i, y + h - 1, c); }
        for (int j = y; j < y + h; j++) { Set(img, x, j, c); Set(img, x + w - 1, j, c); }
    }

    public static void FillCircle(Image img, int cx, int cy, int r, Color c)
    {
        int r2 = r * r;
        for (int j = cy - r; j <= cy + r; j++)
            for (int i = cx - r; i <= cx + r; i++)
            {
                int dx = i - cx, dy = j - cy;
                if (dx * dx + dy * dy <= r2) Set(img, i, j, c);
            }
    }

    public static void DrawCircleOutline(Image img, int cx, int cy, int r, Color c)
    {
        int x = r, y = 0, err = 0;
        while (x >= y)
        {
            Set(img, cx + x, cy + y, c); Set(img, cx + y, cy + x, c);
            Set(img, cx - y, cy + x, c); Set(img, cx - x, cy + y, c);
            Set(img, cx - x, cy - y, c); Set(img, cx - y, cy - x, c);
            Set(img, cx + y, cy - x, c); Set(img, cx + x, cy - y, c);
            y++;
            if (err <= 0) { err += 2 * y + 1; }
            else { x--; err += 2 * (y - x) + 1; }
        }
    }

    public static void FillTrapezoid(Image img,
        int x1, int y1, int x2, int y2,
        int x3, int y3, int x4, int y4, Color c)
    {
        // (x1,y1)左上 (x2,y2)右上 (x3,y3)右下 (x4,y4)左下;水平上下边
        int yTop = Mathf.Min(y1, y2);
        int yBot = Mathf.Max(y3, y4);
        for (int y = yTop; y <= yBot; y++)
        {
            float t = yBot == yTop ? 0f : (float)(y - yTop) / (yBot - yTop);
            int xL = (int)Mathf.Lerp(x1, x4, t);
            int xR = (int)Mathf.Lerp(x2, x3, t);
            for (int x = xL; x <= xR; x++) Set(img, x, y, c);
        }
    }

    /// <summary>填充任意凸多边形(扫描线算法)。点必须按顺时针或逆时针顺序。</summary>
    public static void FillPolygon(Image img, Vector2I[] pts, Color c)
    {
        if (pts.Length < 3) return;
        int yMin = pts[0].Y, yMax = pts[0].Y;
        for (int i = 1; i < pts.Length; i++)
        {
            if (pts[i].Y < yMin) yMin = pts[i].Y;
            if (pts[i].Y > yMax) yMax = pts[i].Y;
        }

        for (int y = yMin; y <= yMax; y++)
        {
            // 收集所有与扫描线相交的 x
            var xs = new System.Collections.Generic.List<int>();
            for (int i = 0; i < pts.Length; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Length];
                if (a.Y == b.Y) continue;
                int yLo = Mathf.Min(a.Y, b.Y);
                int yHi = Mathf.Max(a.Y, b.Y);
                if (y < yLo || y >= yHi) continue;
                float t = (float)(y - a.Y) / (b.Y - a.Y);
                xs.Add((int)Mathf.Lerp(a.X, b.X, t));
            }
            xs.Sort();
            for (int i = 0; i + 1 < xs.Count; i += 2)
                for (int x = xs[i]; x <= xs[i + 1]; x++)
                    Set(img, x, y, c);
        }
    }

    public static void DrawLineV(Image img, int x, int y1, int y2, Color c)
    {
        if (y1 > y2) (y1, y2) = (y2, y1);
        for (int j = y1; j <= y2; j++) Set(img, x, j, c);
    }

    public static void DrawLineH(Image img, int x1, int x2, int y, Color c)
    {
        if (x1 > x2) (x1, x2) = (x2, x1);
        for (int i = x1; i <= x2; i++) Set(img, i, y, c);
    }

    public static void DrawThickLineV(Image img, int x, int y1, int y2, Color c, int thickness)
    {
        if (y1 > y2) (y1, y2) = (y2, y1);
        int half = thickness / 2;
        for (int t = -half; t <= thickness - half - 1; t++)
            for (int j = y1; j <= y2; j++)
                Set(img, x + t, j, c);
    }

    public static void DrawThickLineH(Image img, int x1, int x2, int y, Color c, int thickness)
    {
        if (x1 > x2) (x1, x2) = (x2, x1);
        int half = thickness / 2;
        for (int t = -half; t <= thickness - half - 1; t++)
            for (int i = x1; i <= x2; i++)
                Set(img, i, y + t, c);
    }

    public static void DrawThickLineDiagonal(Image img,
        int x1, int y1, int x2, int y2, Color c, int thickness)
    {
        int dx = Mathf.Abs(x2 - x1);
        int dy = -Mathf.Abs(y2 - y1);
        int sx = x1 < x2 ? 1 : -1;
        int sy = y1 < y2 ? 1 : -1;
        int err = dx + dy;
        int x = x1, y = y1;
        int half = thickness / 2;
        while (true)
        {
            for (int t = -half; t <= thickness - half - 1; t++)
            {
                Set(img, x, y + t, c);
                Set(img, x + t, y, c);
            }
            if (x == x2 && y == y2) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x += sx; }
            if (e2 <= dx) { err += dx; y += sy; }
        }
    }

    public static void FillDiamond(Image img, int cx, int cy, int r, Color c)
    {
        for (int j = -r; j <= r; j++)
            for (int i = -r; i <= r; i++)
                if (Mathf.Abs(i) + Mathf.Abs(j) <= r)
                    Set(img, cx + i, cy + j, c);
    }
}
