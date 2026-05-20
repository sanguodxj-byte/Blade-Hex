// UnitPlaceholderRenderer.cs
// 程序化单位占位符 — 只画**人体**(头/身/腿/臂),不画装备。
// 装备由 EquipmentPlaceholderRenderer 生成单独贴图,在 sprite 层叠加。
// 尺寸:80×120,与旧 PlaceholderTexture2D 保持一致。
using Godot;
using BladeHex.Data;

namespace BladeHex.View.Unit;

public static class UnitPlaceholderRenderer
{
    public const int TexW = 80;
    public const int TexH = 120;

    public static Texture2D Generate(UnitData data, Color tint)
    {
        var img = Image.CreateEmpty(TexW, TexH, false, Image.Format.Rgba8);
        // 完全透明背景
        for (int y = 0; y < TexH; y++)
            for (int x = 0; x < TexW; x++)
                img.SetPixel(x, y, new Color(0, 0, 0, 0));

        var main = tint;
        var dark = tint * 0.6f;

        int cx = TexW / 2;

        // 头(实心圆)
        const int headR = 12;
        const int headCY = 18;
        FillCircle(img, cx, headCY, headR, main);

        // 颈
        FillRect(img, cx - 3, headCY + headR, 6, 4, dark);

        // 躯干(梯形,从颈到骨盆)
        int torsoTop = headCY + headR + 4;     // 34
        int torsoBot = 84;
        int torsoTopHalf = 18;
        int torsoBotHalf = 14;
        for (int y = torsoTop; y <= torsoBot; y++)
        {
            float t = (float)(y - torsoTop) / Mathf.Max(1, torsoBot - torsoTop);
            int half = (int)Mathf.Lerp(torsoTopHalf, torsoBotHalf, t);
            for (int x = cx - half; x <= cx + half; x++)
                Set(img, x, y, main);
        }

        // 双臂(从肩到中腰的斜粗线)
        for (int side = 0; side < 2; side++)
        {
            int dir = side == 0 ? -1 : 1;
            int sx = cx + dir * torsoTopHalf;
            int sy = torsoTop + 2;
            int ex = sx + dir * 4;
            int ey = (torsoTop + torsoBot) / 2 + 8;
            DrawThickLine(img, sx, sy, ex, ey, dark, 3);
        }

        // 双腿(竖粗线)
        int legTop = torsoBot;
        int legBot = 112;
        DrawThickLine(img, cx - 5, legTop, cx - 5, legBot, dark, 5);
        DrawThickLine(img, cx + 5, legTop, cx + 5, legBot, dark, 5);

        return ImageTexture.CreateFromImage(img);
    }

    private static void Set(Image img, int x, int y, Color c)
    {
        if (x < 0 || y < 0 || x >= img.GetWidth() || y >= img.GetHeight()) return;
        img.SetPixel(x, y, c);
    }

    private static void FillCircle(Image img, int cx, int cy, int r, Color c)
    {
        int r2 = r * r;
        for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                int dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy <= r2) Set(img, x, y, c);
            }
    }

    private static void FillRect(Image img, int x, int y, int w, int h, Color c)
    {
        for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++)
                Set(img, i, j, c);
    }

    private static void DrawThickLine(Image img, int x1, int y1, int x2, int y2, Color c, int thick)
    {
        int dx = Mathf.Abs(x2 - x1);
        int dy = -Mathf.Abs(y2 - y1);
        int sx = x1 < x2 ? 1 : -1;
        int sy = y1 < y2 ? 1 : -1;
        int err = dx + dy;
        int x = x1, y = y1;
        int half = thick / 2;
        while (true)
        {
            for (int t1 = -half; t1 <= thick - half - 1; t1++)
                for (int t2 = -half; t2 <= thick - half - 1; t2++)
                    Set(img, x + t1, y + t2, c);
            if (x == x2 && y == y2) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x += sx; }
            if (e2 <= dx) { err += dx; y += sy; }
        }
    }
}
