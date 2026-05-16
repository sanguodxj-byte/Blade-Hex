// ItemPlaceholderRenderer.cs
// 程序化占位符渲染 — 当物品/装备槽缺少图标时生成可识别的图形
//
// 设计：
//   - 每个物品类别有专属形状（武器=三角刀刃，护甲=矩形，盾=六边形，饰品=圆形等）
//   - 颜色与稀有度/类型挂钩
//   - 装备槽缺装备时显示槽位类型符号
using Godot;
using BladeHex.Data;

namespace BladeHex.View.UI.Inventory;

/// <summary>
/// 程序化占位符渲染器 — Control 子类，通过 _Draw 绘制。
/// 当 ResourceRegistry 找不到图标时使用。
/// </summary>
[GlobalClass]
public partial class ItemPlaceholderRenderer : Control
{
    public enum PlaceholderShape
    {
        WeaponMelee,    // 三角刀刃
        WeaponRanged,   // 弓箭符号
        WeaponThrowing, // 投掷飞刀
        WeaponCatalyst, // 法杖菱形
        ArmorBody,      // 矩形铠甲
        ArmorHelmet,    // 椭圆头盔
        ArmorGauntlets, // 双手矩形
        ArmorBoots,     // 鞋子轮廓
        Shield,         // 六边形盾
        Ring,           // 圆环
        Amulet,         // 水滴项链
        Cloak,          // 三角斗篷
        Belt,           // 横条腰带
        Bracer,         // 护腕条
        Consumable,     // 药瓶
        Quiver,         // 箭筒
        Generic,        // 方块（fallback）
    }

    /// <summary>占位符形状类型</summary>
    public PlaceholderShape Shape { get; set; } = PlaceholderShape.Generic;

    /// <summary>主色（通常用稀有度色）</summary>
    public Color MainColor { get; set; } = new(0.7f, 0.7f, 0.75f);

    /// <summary>背景色</summary>
    public Color BgColor { get; set; } = new(0.15f, 0.13f, 0.18f, 0.6f);

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        var size = Size;
        var rect = new Rect2(Vector2.Zero, size);
        Vector2 c = size * 0.5f;
        float w = size.X;
        float h = size.Y;
        float r = Mathf.Min(w, h) * 0.4f;

        // 背景
        DrawRect(rect, BgColor, true);
        DrawRect(rect, MainColor * 0.4f, false, 1f);

        switch (Shape)
        {
            case PlaceholderShape.WeaponMelee:
                DrawWeaponMelee(c, w, h);
                break;
            case PlaceholderShape.WeaponRanged:
                DrawWeaponRanged(c, w, h);
                break;
            case PlaceholderShape.WeaponThrowing:
                DrawWeaponThrowing(c, w, h);
                break;
            case PlaceholderShape.WeaponCatalyst:
                DrawWeaponCatalyst(c, w, h);
                break;
            case PlaceholderShape.ArmorBody:
                DrawArmorBody(c, w, h);
                break;
            case PlaceholderShape.ArmorHelmet:
                DrawHelmet(c, w, h);
                break;
            case PlaceholderShape.ArmorGauntlets:
                DrawGauntlets(c, w, h);
                break;
            case PlaceholderShape.ArmorBoots:
                DrawBoots(c, w, h);
                break;
            case PlaceholderShape.Shield:
                DrawShield(c, r);
                break;
            case PlaceholderShape.Ring:
                DrawRing(c, r);
                break;
            case PlaceholderShape.Amulet:
                DrawAmulet(c, w, h);
                break;
            case PlaceholderShape.Cloak:
                DrawCloak(c, w, h);
                break;
            case PlaceholderShape.Belt:
                DrawBelt(c, w, h);
                break;
            case PlaceholderShape.Bracer:
                DrawBracer(c, w, h);
                break;
            case PlaceholderShape.Consumable:
                DrawConsumable(c, w, h);
                break;
            case PlaceholderShape.Quiver:
                DrawQuiver(c, w, h);
                break;
            default:
                DrawGeneric(c, r);
                break;
        }
    }

    // ============================================================================
    // 形状绘制
    // ============================================================================

    private void DrawWeaponMelee(Vector2 c, float w, float h)
    {
        // 竖向剑：菱形刀身 + 短柄
        var bladeTop = c + new Vector2(0, -h * 0.4f);
        var bladeMid = c + new Vector2(w * 0.1f, 0);
        var bladeBot = c + new Vector2(0, h * 0.2f);
        var bladeLeft = c + new Vector2(-w * 0.1f, 0);
        DrawColoredPolygon(new[] { bladeTop, bladeMid, bladeBot, bladeLeft }, MainColor);
        // 护手
        DrawLine(c + new Vector2(-w * 0.2f, h * 0.2f), c + new Vector2(w * 0.2f, h * 0.2f), MainColor * 0.7f, 2f);
        // 柄
        DrawLine(bladeBot, c + new Vector2(0, h * 0.4f), MainColor * 0.6f, 2f);
    }

    private void DrawWeaponRanged(Vector2 c, float w, float h)
    {
        // 弓：垂直曲线 + 弦
        var top = c + new Vector2(0, -h * 0.4f);
        var bot = c + new Vector2(0, h * 0.4f);
        var bowMid = c + new Vector2(-w * 0.25f, 0);
        // 弓身（用三段折线模拟曲线）
        DrawLine(top, bowMid, MainColor, 2.5f);
        DrawLine(bowMid, bot, MainColor, 2.5f);
        // 弦
        DrawLine(top, bot, MainColor * 0.5f, 1f);
        // 箭
        DrawLine(c + new Vector2(-w * 0.05f, 0), c + new Vector2(w * 0.4f, 0), MainColor * 0.8f, 1.5f);
        // 箭头
        var arrowTip = c + new Vector2(w * 0.4f, 0);
        DrawColoredPolygon(new[] {
            arrowTip,
            arrowTip + new Vector2(-w * 0.08f, -h * 0.05f),
            arrowTip + new Vector2(-w * 0.08f, h * 0.05f),
        }, MainColor);
    }

    private void DrawWeaponThrowing(Vector2 c, float w, float h)
    {
        // 飞刀：小三角
        DrawColoredPolygon(new[] {
            c + new Vector2(0, -h * 0.35f),
            c + new Vector2(w * 0.15f, h * 0.1f),
            c + new Vector2(0, h * 0.05f),
            c + new Vector2(-w * 0.15f, h * 0.1f),
        }, MainColor);
        DrawLine(c + new Vector2(0, h * 0.05f), c + new Vector2(0, h * 0.3f), MainColor * 0.6f, 2f);
    }

    private void DrawWeaponCatalyst(Vector2 c, float w, float h)
    {
        // 法杖：长杆顶端菱形
        DrawLine(c + new Vector2(0, -h * 0.4f), c + new Vector2(0, h * 0.4f), MainColor * 0.7f, 2f);
        // 顶部宝石（菱形）
        var top = c + new Vector2(0, -h * 0.4f);
        DrawColoredPolygon(new[] {
            top + new Vector2(0, -h * 0.08f),
            top + new Vector2(w * 0.1f, 0),
            top + new Vector2(0, h * 0.08f),
            top + new Vector2(-w * 0.1f, 0),
        }, MainColor);
    }

    private void DrawArmorBody(Vector2 c, float w, float h)
    {
        // 盔甲：胸甲剪影
        var topL = c + new Vector2(-w * 0.3f, -h * 0.3f);
        var topR = c + new Vector2(w * 0.3f, -h * 0.3f);
        var midL = c + new Vector2(-w * 0.35f, 0);
        var midR = c + new Vector2(w * 0.35f, 0);
        var botL = c + new Vector2(-w * 0.25f, h * 0.35f);
        var botR = c + new Vector2(w * 0.25f, h * 0.35f);
        DrawColoredPolygon(new[] { topL, topR, midR, botR, botL, midL }, MainColor);
        // 中线
        DrawLine(c + new Vector2(0, -h * 0.3f), c + new Vector2(0, h * 0.35f), MainColor * 0.5f, 1f);
    }

    private void DrawHelmet(Vector2 c, float w, float h)
    {
        // 头盔：上半圆 + 下边
        DrawCircle(c + new Vector2(0, h * 0.05f), Mathf.Min(w, h) * 0.32f, MainColor);
        // 面甲（深色覆盖下半部分）
        DrawRect(new Rect2(c.X - w * 0.32f, c.Y + h * 0.15f, w * 0.64f, h * 0.3f), BgColor, true);
        // T 形面罩
        DrawLine(c + new Vector2(0, -h * 0.1f), c + new Vector2(0, h * 0.1f), MainColor * 0.4f, 2f);
        DrawLine(c + new Vector2(-w * 0.1f, -h * 0.1f), c + new Vector2(w * 0.1f, -h * 0.1f), MainColor * 0.4f, 2f);
    }

    private void DrawGauntlets(Vector2 c, float w, float h)
    {
        // 护手：两个圆角矩形
        float gw = w * 0.25f;
        float gh = h * 0.5f;
        DrawRect(new Rect2(c.X - w * 0.32f, c.Y - gh * 0.5f, gw, gh), MainColor, true);
        DrawRect(new Rect2(c.X + w * 0.07f, c.Y - gh * 0.5f, gw, gh), MainColor, true);
        // 关节线
        DrawLine(c + new Vector2(-w * 0.32f, -gh * 0.1f), c + new Vector2(-w * 0.07f, -gh * 0.1f), MainColor * 0.5f, 1f);
        DrawLine(c + new Vector2(w * 0.07f, -gh * 0.1f), c + new Vector2(w * 0.32f, -gh * 0.1f), MainColor * 0.5f, 1f);
    }

    private void DrawBoots(Vector2 c, float w, float h)
    {
        // 靴子：L 形
        var topL = c + new Vector2(-w * 0.15f, -h * 0.3f);
        var topR = c + new Vector2(w * 0.05f, -h * 0.3f);
        var midR = c + new Vector2(w * 0.05f, h * 0.15f);
        var toeR = c + new Vector2(w * 0.35f, h * 0.15f);
        var toeBot = c + new Vector2(w * 0.35f, h * 0.3f);
        var heelBot = c + new Vector2(-w * 0.15f, h * 0.3f);
        DrawColoredPolygon(new[] { topL, topR, midR, toeR, toeBot, heelBot }, MainColor);
    }

    private void DrawShield(Vector2 c, float r)
    {
        // 圆形盾：圆 + 中央纹章
        DrawCircle(c, r, MainColor);
        DrawCircle(c, r * 0.6f, MainColor * 1.2f);
        // 十字
        DrawLine(c + new Vector2(-r * 0.4f, 0), c + new Vector2(r * 0.4f, 0), MainColor * 0.5f, 2f);
        DrawLine(c + new Vector2(0, -r * 0.4f), c + new Vector2(0, r * 0.4f), MainColor * 0.5f, 2f);
    }

    private void DrawRing(Vector2 c, float r)
    {
        // 戒指：环形（外圆 - 内圆）
        DrawArc(c, r * 0.7f, 0, Mathf.Tau, 32, MainColor, 4f);
        // 宝石
        DrawCircle(c + new Vector2(0, -r * 0.7f), r * 0.18f, MainColor * 1.3f);
    }

    private void DrawAmulet(Vector2 c, float w, float h)
    {
        // 项链：链子（圆弧）+ 水滴吊坠
        DrawArc(c + new Vector2(0, -h * 0.1f), w * 0.3f, Mathf.Pi * 1.2f, Mathf.Pi * 1.8f, 16, MainColor * 0.6f, 1.5f);
        // 水滴
        var pendantTop = c + new Vector2(0, h * 0.0f);
        DrawCircle(pendantTop + new Vector2(0, h * 0.15f), w * 0.12f, MainColor);
        DrawColoredPolygon(new[] {
            pendantTop,
            pendantTop + new Vector2(w * 0.08f, h * 0.15f),
            pendantTop + new Vector2(-w * 0.08f, h * 0.15f),
        }, MainColor);
    }

    private void DrawCloak(Vector2 c, float w, float h)
    {
        // 斗篷：倒三角 + 顶部领口
        DrawColoredPolygon(new[] {
            c + new Vector2(-w * 0.25f, -h * 0.35f),
            c + new Vector2(w * 0.25f, -h * 0.35f),
            c + new Vector2(w * 0.4f, h * 0.35f),
            c + new Vector2(-w * 0.4f, h * 0.35f),
        }, MainColor);
        // 领口
        DrawLine(c + new Vector2(-w * 0.15f, -h * 0.35f), c + new Vector2(w * 0.15f, -h * 0.35f), MainColor * 0.4f, 3f);
    }

    private void DrawBelt(Vector2 c, float w, float h)
    {
        // 腰带：横条 + 中央带扣
        DrawRect(new Rect2(c.X - w * 0.4f, c.Y - h * 0.1f, w * 0.8f, h * 0.2f), MainColor, true);
        // 带扣
        DrawRect(new Rect2(c.X - w * 0.08f, c.Y - h * 0.13f, w * 0.16f, h * 0.26f), MainColor * 1.3f, true);
    }

    private void DrawBracer(Vector2 c, float w, float h)
    {
        // 护腕：两条平行带
        DrawRect(new Rect2(c.X - w * 0.3f, c.Y - h * 0.25f, w * 0.6f, h * 0.18f), MainColor, true);
        DrawRect(new Rect2(c.X - w * 0.3f, c.Y + h * 0.07f, w * 0.6f, h * 0.18f), MainColor, true);
    }

    private void DrawConsumable(Vector2 c, float w, float h)
    {
        // 药瓶：瓶身 + 瓶口
        DrawRect(new Rect2(c.X - w * 0.05f, c.Y - h * 0.4f, w * 0.1f, h * 0.15f), MainColor * 0.6f, true);
        // 瓶身（梯形）
        DrawColoredPolygon(new[] {
            c + new Vector2(-w * 0.1f, -h * 0.25f),
            c + new Vector2(w * 0.1f, -h * 0.25f),
            c + new Vector2(w * 0.22f, h * 0.35f),
            c + new Vector2(-w * 0.22f, h * 0.35f),
        }, MainColor);
        // 液面
        DrawLine(c + new Vector2(-w * 0.18f, h * 0.05f), c + new Vector2(w * 0.18f, h * 0.05f), MainColor * 1.4f, 1.5f);
    }

    private void DrawQuiver(Vector2 c, float w, float h)
    {
        // 箭筒：圆柱 + 顶部箭头
        DrawRect(new Rect2(c.X - w * 0.18f, c.Y - h * 0.1f, w * 0.36f, h * 0.45f), MainColor * 0.7f, true);
        // 箭尾露出
        for (int i = -1; i <= 1; i++)
        {
            float xOffset = i * w * 0.1f;
            DrawLine(c + new Vector2(xOffset, -h * 0.1f), c + new Vector2(xOffset, -h * 0.4f), MainColor, 2f);
            // 羽毛
            DrawColoredPolygon(new[] {
                c + new Vector2(xOffset, -h * 0.4f),
                c + new Vector2(xOffset - w * 0.04f, -h * 0.32f),
                c + new Vector2(xOffset + w * 0.04f, -h * 0.32f),
            }, MainColor * 1.2f);
        }
    }

    private void DrawGeneric(Vector2 c, float r)
    {
        DrawCircle(c, r, MainColor);
        DrawCircle(c, r * 0.5f, BgColor);
    }

    // ============================================================================
    // 分发：根据 ItemData 自动选择形状
    // ============================================================================

    /// <summary>从物品数据推断合适的占位符形状</summary>
    public static PlaceholderShape GetShapeForItem(ItemData item) => item switch
    {
        WeaponData w when w.IsCatalyst => PlaceholderShape.WeaponCatalyst,
        WeaponData w when w.IsThrowing => PlaceholderShape.WeaponThrowing,
        WeaponData w when w.IsRanged => PlaceholderShape.WeaponRanged,
        WeaponData => PlaceholderShape.WeaponMelee,
        ArmorData a when a.armorType == ArmorData.ArmorType.Shield => PlaceholderShape.Shield,
        ArmorData a when a.EquipSlotTarget == ItemData.EquipSlot.Helmet
                     || a.EquipSlotTarget == ItemData.EquipSlot.Head => PlaceholderShape.ArmorHelmet,
        ArmorData a when a.EquipSlotTarget == ItemData.EquipSlot.Hands => PlaceholderShape.ArmorGauntlets,
        ArmorData a when a.EquipSlotTarget == ItemData.EquipSlot.Feet => PlaceholderShape.ArmorBoots,
        ArmorData => PlaceholderShape.ArmorBody,
        AccessoryData acc => acc.accessoryType switch
        {
            AccessoryData.AccessoryType.Ring => PlaceholderShape.Ring,
            AccessoryData.AccessoryType.Amulet => PlaceholderShape.Amulet,
            AccessoryData.AccessoryType.Cloak => PlaceholderShape.Cloak,
            AccessoryData.AccessoryType.Belt => PlaceholderShape.Belt,
            AccessoryData.AccessoryType.Bracer => PlaceholderShape.Bracer,
            _ => PlaceholderShape.Generic,
        },
        ConsumableData => PlaceholderShape.Consumable,
        _ when item.IsQuiver => PlaceholderShape.Quiver,
        _ => PlaceholderShape.Generic,
    };

    /// <summary>从装备槽 ID 推断空槽位的形状（用于装备槽未装备时显示）</summary>
    public static PlaceholderShape GetShapeForSlot(string slotId) => slotId switch
    {
        "primary_main" or "secondary_main" => PlaceholderShape.WeaponMelee,
        "primary_off" or "secondary_off" => PlaceholderShape.Shield,
        "armor" => PlaceholderShape.ArmorBody,
        "helmet" => PlaceholderShape.ArmorHelmet,
        "gauntlets" => PlaceholderShape.ArmorGauntlets,
        "boots" => PlaceholderShape.ArmorBoots,
        "accessory_1" => PlaceholderShape.Ring,
        "accessory_2" => PlaceholderShape.Amulet,
        _ => PlaceholderShape.Generic,
    };
}
