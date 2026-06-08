// ItemPopup.cs
// 物品详情悬浮窗 — 右键点击物品时显示
// 完全独立组件，由 DragController 或容器调用
using Godot;
using BladeHex.Data;
using BladeHex.UI.Common;
using BladeHex.Strategic.Economy;

namespace BladeHex.View.UI.Inventory;

/// <summary>
/// 浮动物品详情面板。
/// 使用 TopLevel + ZIndex 100 确保始终在最顶层。
/// </summary>
[GlobalClass]
public partial class ItemPopup : FloatingPanel
{
    private RichTextLabel _text = null!;

    // ============================================================================
    // FloatingPanel 配置
    // ============================================================================

    protected override int PanelShadowSize => 4;
    protected override float MinPanelWidth => 280f;
    protected override bool UseTopLevel => true;
    protected override Vector2 MouseOffset => new(12, -20);
    protected override FloatingPanelDismissMode PanelDismiss => FloatingPanelDismissMode.OnMouseExit;

    // ============================================================================
    // 构建内容
    // ============================================================================

    protected override void BuildContent()
    {
        SetProcessUnhandledInput(true);

        _text = new RichTextLabel();
        _text.BbcodeEnabled = true;
        _text.ScrollActive = false;
        _text.FitContent = true;
        _text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _text.CustomMinimumSize = new Vector2(260, 0);
        _text.MouseFilter = MouseFilterEnum.Ignore;
        Content.AddChild(_text);
    }

    // ============================================================================
    // 公共 API
    // ============================================================================

    /// <summary>在指定屏幕位置显示物品详情</summary>
    public void ShowFor(ItemData item, Vector2 mousePos)
    {
        _text.Text = BuildText(item);
        ShowAt(mousePos);
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        if (!Visible) return;
        if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            HidePanel();
            GetViewport().SetInputAsHandled();
        }
    }

    public new void Hide() => HidePanel();

    // ============================================================================
    // 文本构建
    // ============================================================================

    private static string GetDamageTypeName(WeaponData.DamageType dt) => dt switch
    {
        WeaponData.DamageType.Slash => "砍伤",
        WeaponData.DamageType.Pierce => "刺伤",
        WeaponData.DamageType.Crush => "钝伤",
        WeaponData.DamageType.Magic => "魔法",
        WeaponData.DamageType.Fire => "火焰",
        WeaponData.DamageType.Frost => "冰霜",
        WeaponData.DamageType.Lightning => "闪电",
        _ => "物理",
    };

    private static string BuildWeaponTraitsLine(WeaponData wpn)
    {
        var traits = new System.Collections.Generic.List<string>();
        if (wpn.IsTwoHanded) traits.Add("[color=#e0c860]双手[/color][color=#aaa]：需要双手持握[/color]");
        if (wpn.IsFinesse) traits.Add("[color=#e0c860]灵巧[/color][color=#aaa]：可用敏捷替代力量[/color]");
        if (wpn.IsRanged) traits.Add("[color=#e0c860]远程[/color][color=#aaa]：远距离攻击[/color]");
        if (wpn.IsThrowing) traits.Add($"[color=#e0c860]投掷[/color][color=#aaa]：射程{wpn.ThrowRange}格[/color]");
        if (wpn.NeedsReload) traits.Add($"[color=#e0c860]装填[/color][color=#aaa]：消耗{wpn.ReloadCost}AP[/color]");
        if (wpn.IsBlunt) traits.Add("[color=#e0c860]钝击[/color][color=#aaa]：对亡灵全额伤害[/color]");
        if (wpn.IsArmorPiercing) traits.Add("[color=#e0c860]破甲[/color][color=#aaa]：目标AC-2[/color]");
        if (wpn.IsReach) traits.Add("[color=#e0c860]长柄[/color][color=#aaa]：近战+1格范围[/color]");
        if (wpn.IsAntiCavalry) traits.Add("[color=#e0c860]反骑[/color][color=#aaa]：对冲锋目标伤害×2[/color]");
        if (wpn.IsSweep) traits.Add("[color=#e0c860]横扫[/color][color=#aaa]：可攻击多个相邻敌人[/color]");
        if (wpn.IsCatalyst) traits.Add("[color=#e0c860]触媒[/color][color=#aaa]：可施放法术[/color]");
        if (wpn.IsDualWieldable) traits.Add("[color=#e0c860]双持[/color][color=#aaa]：可副手装备[/color]");
        if (wpn.IsLongbow) traits.Add("[color=#e0c860]长弓[/color][color=#aaa]：高AP消耗远程[/color]");
        if (wpn.IsCrossbow) traits.Add("[color=#e0c860]弩[/color][color=#aaa]：高穿透需装填[/color]");
        if (traits.Count == 0) return "";
        return "[color=#c8b070]特性[/color]\n" + string.Join("\n", traits);
    }

    private static string BuildText(ItemData item)
    {
        var rc = item.GetRarityColor().ToHtml(false);
        string text = $"[color=#{rc}][b]{item.GetFullName()}[/b][/color]\n";
        text += $"[color=#999]{item.GetRarityName()}[/color]";

        if (item is WeaponData wpn)
        {
            int totalMin = wpn.DamageDiceCount + wpn.BonusDamageDiceCount + wpn.BonusDamage;
            int totalMax = wpn.DamageDiceCount * wpn.DamageDiceSides
                         + wpn.BonusDamageDiceCount * wpn.BonusDamageDiceSides
                         + wpn.BonusDamage;
            string dmgText = $"伤害: {totalMin}-{totalMax}";
            if (wpn.BonusAttack != 0)
                dmgText += $" 命中{wpn.BonusAttack:+#;-#;+0}";
            text += $"\n\n[color=#ddd]{dmgText}[/color]";
            text += $"\n[color=#aaa]类型: {GetDamageTypeName(wpn.WeaponDamageType)}[/color]";

            var stats = new System.Collections.Generic.List<string>();
            stats.Add($"AP消耗: {wpn.ApCost}");
            if (wpn.RangeCells > 1) stats.Add($"射程: {wpn.RangeCells}格");
            if (wpn.StrRequired > 0) stats.Add($"需要力量: {wpn.StrRequired}");
            if (wpn.NeedsAmmo && wpn.MaxAmmo > 0) stats.Add($"弹药: {wpn.CurrentAmmo}/{wpn.MaxAmmo}");
            text += $"\n[color=#aaa]{string.Join("  ", stats)}[/color]";

            string traitsLine = BuildWeaponTraitsLine(wpn);
            if (!string.IsNullOrEmpty(traitsLine))
                text += $"\n\n{traitsLine}";
        }
        else if (item is ArmorData arm)
        {
            text += $"\n\n[color=#aaa]{arm.GetArmorTypeName()}[/color]";

            if (arm.armorType == ArmorData.ArmorType.Shield)
            {
                text += $"\n[color=#ddd]AC{arm.GetTotalAcBonus():+#;-#;+0}[/color]";
                if (arm.RangedDamageMultiplier < 1.0f)
                    text += $"\n[color=#ddd]远程减伤: {(1.0f - arm.RangedDamageMultiplier) * 100:F0}%[/color]";
            }
            else
            {
                text += $"\n[color=#ddd]闪避: {10 + arm.AcBonus + arm.BonusAc}+敏捷[/color]";
                if (arm.MaxDexBonus < 99)
                    text += $"\n[color=#ddd]敏捷上限: {arm.MaxDexBonus}[/color]";
            }

            text += $"\n[color=#ddd]装甲耐久: {arm.CurrentArmorPoints}/{arm.MaxArmorPoints} | 穿透阈值: {arm.DrThreshold}[/color]";

            var penalties = new System.Collections.Generic.List<string>();
            if (arm.MovementPenalty != 0) penalties.Add($"速度{-arm.MovementPenalty:+#;-#;+0}");
            if (arm.ApPenalty != 0) penalties.Add($"AP{-arm.ApPenalty:+#;-#;+0}");
            if (arm.StrRequired > 0) penalties.Add($"需要力量: {arm.StrRequired}");
            if (arm.StealthDisadvantage) penalties.Add("隐匿不利");
            if (penalties.Count > 0)
                text += $"\n[color=#c87070]{string.Join("  ", penalties)}[/color]";

            string statBonus = arm.GetStatBonusText();
            if (!string.IsNullOrEmpty(statBonus))
                text += $"\n[color=#70c870]{statBonus}[/color]";
        }

        if (!string.IsNullOrEmpty(item.Description))
            text += $"\n\n[color=#555]─────────────────[/color]\n[color=#aaa]{item.Description}[/color]";

        text += $"\n\n[color=#555]─────────────────[/color]";
        text += $"\n[color=#666]占用: {item.InvWidth}×{item.InvHeight} 格  估值: {TradePricingService.GetBasePrice(item)} 金[/color]";

        string affixes = item.GetAffixDescriptions();
        if (!string.IsNullOrEmpty(affixes))
            text += $"\n\n[color=#b07de8]{affixes}[/color]";

        return text;
    }
}
