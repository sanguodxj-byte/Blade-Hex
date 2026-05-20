// EquipmentPlaceholderRenderer.cs
// 程序化装备占位符 — 给战斗端的 sprite 层(头盔/盔甲/手甲/武器/盾)
// 在缺真实美术资源时生成一张可识别的小贴图,直接喂 AnimatedSprite3D。
//
// 风格基准:与 ItemPlaceholderRenderer (UI Control._Draw) 保持一致 —
// 单一主色 + 0.4-0.8 倍乘暗色描边/装饰,几何拼图,无背景框
// (装备是叠在身体 sprite 上的,**不要**画背景框,否则会糊住身体)。
//
// 输出尺寸约定(与 SlotConfigTable.DefaultSize 一致):
//   Helmet  : 56 × 56
//   Body    : 64 × 96    (盔甲)
//   Hands   : 48 × 48
//   Weapon  : 32 × 80    (默认朝右,角色 facing 朝左时由 Sprite3D.FlipH 翻)
//   Shield  : 48 × 48    (放在副手 weapon slot)
using Godot;
using BladeHex.Data;

namespace BladeHex.View.Unit;

/// <summary>程序化装备占位贴图工厂。</summary>
public static class EquipmentPlaceholderRenderer
{
    /// <summary>
    /// 根据装备数据生成对应的占位贴图。返回 null 表示"该装备不需要可见层"
    /// (例如 Belt / Bracer 等小饰品在战斗端不画)。
    /// </summary>
    public static Texture2D? Generate(ItemData item, Color tint)
    {
        return item switch
        {
            WeaponData w when w.IsCatalyst => DrawCatalyst(tint),
            WeaponData w when w.IsThrowing => DrawThrowing(tint),
            WeaponData w when w.IsRanged => DrawBow(tint),
            WeaponData => DrawSword(tint),
            ArmorData a when a.armorType == ArmorData.ArmorType.Shield => DrawShield(tint),
            ArmorData a when IsHelmet(a) => DrawHelmet(tint),
            ArmorData a when a.EquipSlotTarget == ItemData.EquipSlot.Hands => DrawGauntlets(tint),
            ArmorData a when a.EquipSlotTarget == ItemData.EquipSlot.Body => DrawBodyArmor(tint),
            _ => null, // 其它装备(腰带 / 戒指 / 项链等)在战斗端不画 sprite 层
        };
    }

    private static bool IsHelmet(ArmorData a) =>
        a.EquipSlotTarget == ItemData.EquipSlot.Helmet ||
        a.EquipSlotTarget == ItemData.EquipSlot.Head;

    // ============================================================
    // 武器
    // ============================================================

    /// <summary>近战剑:32×80,刀身朝上、剑柄朝下。武器锚点在角色腰部,所以剑应"竖立"。</summary>
    private static Texture2D DrawSword(Color tint)
    {
        const int W = 32, H = 80;
        var img = NewImg(W, H);
        var main = tint; var dark = tint * 0.6f;
        int cx = W / 2;

        // 刀身(菱形,从顶端到中部)
        PixelDraw.FillPolygon(img, new[] {
            new Vector2I(cx, 4),                 // 顶端
            new Vector2I(cx + 5, H * 4 / 10),   // 右肩
            new Vector2I(cx, H / 2),             // 底端
            new Vector2I(cx - 5, H * 4 / 10),   // 左肩
        }, main);

        // 护手(横线,在刀身底端)
        PixelDraw.DrawThickLineH(img, cx - 8, cx + 8, H / 2 + 1, dark, 2);

        // 剑柄(竖粗线)
        PixelDraw.DrawThickLineV(img, cx, H / 2 + 3, H * 9 / 10, dark, 3);

        // 柄底圆头
        PixelDraw.FillCircle(img, cx, H * 9 / 10 + 2, 3, main);

        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>弓:32×80,弓身竖向,箭头朝右(默认朝向)。</summary>
    private static Texture2D DrawBow(Color tint)
    {
        const int W = 32, H = 80;
        var img = NewImg(W, H);
        var main = tint; var dark = tint * 0.7f;

        int bowX = W / 2 - 4;
        // 弓身(三段折线模拟曲线)
        PixelDraw.DrawThickLineDiagonal(img, bowX + 4, 6, bowX, H / 2, main, 3);
        PixelDraw.DrawThickLineDiagonal(img, bowX, H / 2, bowX + 4, H - 6, main, 3);
        // 弦
        PixelDraw.DrawLineV(img, bowX + 5, 6, H - 6, main * 0.5f);
        // 箭(横向贯穿)
        PixelDraw.DrawThickLineH(img, bowX + 6, W - 2, H / 2, dark, 1);
        // 箭头(右端三角)
        PixelDraw.FillPolygon(img, new[] {
            new Vector2I(W - 2, H / 2),
            new Vector2I(W - 6, H / 2 - 3),
            new Vector2I(W - 6, H / 2 + 3),
        }, main);

        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>飞刀:32×80,小三角 + 短柄。</summary>
    private static Texture2D DrawThrowing(Color tint)
    {
        const int W = 32, H = 80;
        var img = NewImg(W, H);
        var main = tint; var dark = tint * 0.6f;
        int cx = W / 2;

        // 刀身(小三角朝上)
        PixelDraw.FillPolygon(img, new[] {
            new Vector2I(cx, H * 3 / 10),
            new Vector2I(cx + 6, H / 2),
            new Vector2I(cx, H * 6 / 10),
            new Vector2I(cx - 6, H / 2),
        }, main);
        // 短柄
        PixelDraw.DrawThickLineV(img, cx, H * 6 / 10, H * 8 / 10, dark, 2);

        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>法杖:32×80,长杆 + 顶部菱形宝石。</summary>
    private static Texture2D DrawCatalyst(Color tint)
    {
        const int W = 32, H = 80;
        var img = NewImg(W, H);
        var main = tint; var dark = tint * 0.7f;
        int cx = W / 2;

        // 杖杆(竖向)
        PixelDraw.DrawThickLineV(img, cx, 12, H - 4, dark, 3);
        // 顶部菱形宝石
        PixelDraw.FillDiamond(img, cx, 8, 5, main);
        // 宝石描边
        for (int dy = -5; dy <= 5; dy++)
        {
            int dx = 5 - Mathf.Abs(dy);
            PixelDraw.Set(img, cx + dx, 8 + dy, dark);
            PixelDraw.Set(img, cx - dx, 8 + dy, dark);
        }

        return ImageTexture.CreateFromImage(img);
    }

    // ============================================================
    // 防具
    // ============================================================

    /// <summary>头盔:56×56,上半圆 + 面甲</summary>
    private static Texture2D DrawHelmet(Color tint)
    {
        const int W = 56, H = 56;
        var img = NewImg(W, H);
        var main = tint; var dark = tint * 0.55f;
        int cx = W / 2;

        // 圆顶(头形)
        PixelDraw.FillCircle(img, cx, H / 2, (int)(W * 0.32f), main);
        // 面甲遮住下半部分(暗色矩形)
        PixelDraw.FillRect(img, cx - (int)(W * 0.32f), H / 2 + 3,
            (int)(W * 0.64f), (int)(H * 0.30f), dark);
        // T 形面罩(主色亮线条)
        PixelDraw.DrawThickLineV(img, cx, H / 2 - 4, H / 2 + 8, main, 2);
        PixelDraw.DrawThickLineH(img, cx - 6, cx + 6, H / 2 - 4, main, 2);

        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>盔甲(身体):64×96,胸甲多边形 + 中线</summary>
    private static Texture2D DrawBodyArmor(Color tint)
    {
        const int W = 64, H = 96;
        var img = NewImg(W, H);
        var main = tint; var dark = tint * 0.55f;
        int cx = W / 2;

        int top = (int)(H * 0.20f);
        int bot = (int)(H * 0.85f);
        PixelDraw.FillPolygon(img, new[] {
            new Vector2I(cx - (int)(W * 0.30f), top),     // 左肩
            new Vector2I(cx + (int)(W * 0.30f), top),     // 右肩
            new Vector2I(cx + (int)(W * 0.36f), H / 2),    // 右腰
            new Vector2I(cx + (int)(W * 0.26f), bot),     // 右下
            new Vector2I(cx - (int)(W * 0.26f), bot),     // 左下
            new Vector2I(cx - (int)(W * 0.36f), H / 2),    // 左腰
        }, main);

        // 中线(胸甲分割)
        PixelDraw.DrawLineV(img, cx, top + 4, bot - 4, dark);

        // 肩饰(两个小矩形)
        PixelDraw.FillRect(img, cx - (int)(W * 0.36f), top - 2, 8, 6, dark);
        PixelDraw.FillRect(img, cx + (int)(W * 0.36f) - 6, top - 2, 8, 6, dark);

        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>护手:48×48,两侧矩形(角色双手)</summary>
    private static Texture2D DrawGauntlets(Color tint)
    {
        const int W = 48, H = 48;
        var img = NewImg(W, H);
        var main = tint; var dark = tint * 0.55f;

        int gw = (int)(W * 0.22f);
        int gh = (int)(H * 0.55f);
        int gy = (H - gh) / 2;

        // 左手
        PixelDraw.FillRect(img, 2, gy, gw, gh, main);
        PixelDraw.DrawRectOutline(img, 2, gy, gw, gh, dark);
        // 关节线
        PixelDraw.DrawLineH(img, 2, 2 + gw - 1, gy + gh / 2, dark);

        // 右手
        PixelDraw.FillRect(img, W - 2 - gw, gy, gw, gh, main);
        PixelDraw.DrawRectOutline(img, W - 2 - gw, gy, gw, gh, dark);
        PixelDraw.DrawLineH(img, W - 2 - gw, W - 3, gy + gh / 2, dark);

        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>盾:48×48,圆盾 + 十字纹章</summary>
    private static Texture2D DrawShield(Color tint)
    {
        const int W = 48, H = 48;
        var img = NewImg(W, H);
        var main = tint; var dark = tint * 0.55f;
        int cx = W / 2, cy = H / 2;
        int r = (int)(Mathf.Min(W, H) * 0.40f);

        // 外圆
        PixelDraw.FillCircle(img, cx, cy, r, main);
        // 内圆(亮色)
        PixelDraw.FillCircle(img, cx, cy, (int)(r * 0.6f), main * 1.2f);
        // 十字
        PixelDraw.DrawThickLineH(img, cx - (int)(r * 0.5f), cx + (int)(r * 0.5f), cy, dark, 2);
        PixelDraw.DrawThickLineV(img, cx, cy - (int)(r * 0.5f), cy + (int)(r * 0.5f), dark, 2);

        return ImageTexture.CreateFromImage(img);
    }

    // ============================================================
    // helper
    // ============================================================

    private static Image NewImg(int w, int h)
    {
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        return img;
    }
}
