// HitPreviewTooltip.cs
// 命中率预览浮窗 - 悬停敌方时显示命中率%、预计伤害范围、优势/劣势原因
// 核心设计原则：不暴露骰子术语(d20)，只显示概率和直观信息
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Combat;
using BladeHex.UI;
using BladeHex.UI.Common;

namespace BladeHex.UI.Combat;

/// <summary>
/// 命中预览提示 — 悬停敌方时显示命中率、伤害范围、优劣势原因
/// </summary>
[GlobalClass]
public partial class HitPreviewTooltip : FloatingPanel
{
    // ============================================================================
    // 子控件
    // ============================================================================
    private Label _hitLabel = null!;
    private Label _dmgLabel = null!;
    private RichTextLabel _advantageLabel = null!;
    private RichTextLabel _detailsLabel = null!;

    // ============================================================================
    // 颜色常量
    // ============================================================================
    private static readonly Color HIT_COLOR = new(0.3f, 0.85f, 0.3f);
    private static readonly Color MISS_COLOR = new(0.85f, 0.3f, 0.3f);
    private static readonly Color ADVANTAGE_COLOR = new(0.3f, 0.85f, 0.9f);
    private static readonly Color DISADVANTAGE_COLOR = new(0.9f, 0.5f, 0.2f);

    // ============================================================================
    // FloatingPanel 配置
    // ============================================================================

    protected override Color PanelBgColor => new(0.06f, 0.05f, 0.09f, 0.95f);
    protected override Color PanelBorderColor => new(0.5f, 0.4f, 0.2f, 0.8f);
    protected override int PanelBorderWidth => 2;
    protected override int PanelCornerRadius => 4;
    protected override int PanelContentMargin => 8;
    protected override Vector2 MouseOffset => new(15, 15);
    protected override bool FollowMouseContinuously => true;

    // ============================================================================
    // 构建内容
    // ============================================================================

    protected override void BuildContent()
    {
        // 命中率
        _hitLabel = new Label();
        _hitLabel.AddThemeFontSizeOverride("font_size", 15);
        Content.AddChild(_hitLabel);

        // 预计伤害
        _dmgLabel = new Label();
        _dmgLabel.AddThemeFontSizeOverride("font_size", 13);
        _dmgLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.75f, 0.5f));
        Content.AddChild(_dmgLabel);

        Content.AddChild(MakeSeparator());

        // 优势/劣势原因
        _advantageLabel = MakeRichText(180);
        Content.AddChild(_advantageLabel);

        // 详细信息（掩体、高程、武器等）
        _detailsLabel = MakeRichText(180);
        Content.AddChild(_detailsLabel);
    }

    // ============================================================================
    // 公共 API
    // ============================================================================

    /// <summary>显示命中预览。</summary>
    public void ShowPreview(Unit attacker, Unit target, int coverType = 0, int elevationDiff = 0, bool hasFlanking = false, bool hasSneak = false)
    {
        if (attacker == null || target == null || attacker.Data == null || target.Data == null)
            return;

        var weapon = attacker.GetMainHand() as WeaponData;
        int targetAc = target.GetAc();

        // === 收集优势/劣势因素 ===
        var advantages = new List<string>();
        var disadvantages = new List<string>();

        if (elevationDiff > 0) advantages.Add("占据高地");
        else if (elevationDiff < 0) disadvantages.Add("仰攻不利");

        if (hasFlanking) advantages.Add("包夹攻击");
        if (hasSneak) advantages.Add("伏击!");

        // 掩体劣势（仅远程）
        if (coverType > 0 && weapon != null && weapon.IsRanged)
        {
            if (coverType == 1)
            {
                disadvantages.Add("半掩体阻挡");
                targetAc += 2;
            }
            else if (coverType == 2)
            {
                disadvantages.Add("全掩体阻挡");
                _hitLabel.Text = "不可攻击";
                _hitLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
                _dmgLabel.Text = "目标完全隐蔽";
                _advantageLabel.Text = "";
                _detailsLabel.Text = "[color=gray]全掩体单位不可被远程攻击[/color]";
                ShowAtMouse();
                return;
            }
        }

        // 目标低HP状态
        if (target.CurrentHp > 0)
        {
            float hpRatio = (float)target.CurrentHp / Mathf.Max(target.GetMaxHp(), 1);
            if (hpRatio < 0.25f) advantages.Add("目标重伤");
            else if (hpRatio < 0.5f) advantages.Add("目标轻伤");
        }

        if (attacker.Data.IsEnemy && attacker.Data.Morale <= -40)
            disadvantages.Add("士气崩溃");

        // === 计算命中率 ===
        float hitChanceRaw = CombatResolver.GetHitChancePreview(attacker, target);
        double hitChance = hitChanceRaw * 100.0;
        hitChance = Mathf.Clamp(hitChance, 5.0, 95.0);

        if (advantages.Count > disadvantages.Count)
            hitChance = Mathf.Min(hitChance + 15.0, 95.0);
        else if (disadvantages.Count > advantages.Count)
            hitChance = Mathf.Max(hitChance - 15.0, 5.0);

        // === 计算预计伤害范围 ===
        int strMod = attacker.GetStatModifier(attacker.Data.Str);
        int levelExtra = attacker.Data != null ? RPGRuleEngine.GetDamageDiceCount(attacker.Data.Level) - 1 : 0;

        int minDmg, maxDmg;
        if (weapon != null)
        {
            minDmg = weapon.DamageDiceCount + strMod + levelExtra;
            maxDmg = weapon.DamageDiceCount * weapon.DamageDiceSides + strMod + levelExtra * 20;
        }
        else
        {
            minDmg = 1 + levelExtra + strMod;
            maxDmg = 20 + levelExtra * 20 + strMod;
        }
        minDmg = Mathf.Max(1, minDmg);
        maxDmg = Mathf.Max(minDmg, maxDmg);

        if (hasFlanking) maxDmg = (int)(maxDmg * 1.25f);

        // === 更新UI显示 ===
        Color hitColor = hitChance >= 75 ? HIT_COLOR
            : hitChance >= 50 ? new Color(0.7f, 0.8f, 0.3f)
            : hitChance >= 25 ? new Color(0.85f, 0.65f, 0.2f)
            : MISS_COLOR;

        _hitLabel.Text = $"命中率: {Mathf.RoundToInt((float)hitChance)}%";
        _hitLabel.AddThemeColorOverride("font_color", hitColor);
        _dmgLabel.Text = $"预计伤害: {minDmg} - {maxDmg}";

        string advText = "";
        foreach (var a in advantages)
            advText += $"[color={ADVANTAGE_COLOR.ToHtml()}]\u25B2 {a}[/color]\n";
        foreach (var d in disadvantages)
            advText += $"[color={DISADVANTAGE_COLOR.ToHtml()}]\u25BC {d}[/color]\n";
        _advantageLabel.Text = advText.TrimEnd();

        string weaponName = weapon?.ItemName ?? "徒手";
        int weaponRange = weapon?.RangeCells ?? 1;
        bool isRanged = weapon?.IsRanged ?? false;
        string detailText = $"[color=gray]武器: {weaponName} ({(isRanged ? "远程" : "近战")})[/color]\n";
        detailText += $"[color=gray]射程: {weaponRange}格[/color]\n";
        detailText += $"[color=gray]防御等级: {_GetDefenseRating(targetAc)}[/color]";

        if (target.Data.IsEnemy)
        {
            if (target.Data.Immunities.Length > 0)
                detailText += $"\n[color=#ff6666]免疫: {string.Join(", ", target.Data.Immunities)}[/color]";
            if (target.Data.Resistances.Length > 0)
                detailText += $"\n[color=#ccaa44]抗性: {string.Join(", ", target.Data.Resistances)}[/color]";
        }
        _detailsLabel.Text = detailText;

        ShowAtMouse();
    }

    /// <summary>显示超出射程预览</summary>
    public void ShowOutOfRange(Unit target, int distance, int maxRange)
    {
        if (target == null || target.Data == null) return;

        _hitLabel.Text = "命中率: 0%";
        _hitLabel.AddThemeColorOverride("font_color", MISS_COLOR);
        _dmgLabel.Text = "超出攻击范围";
        _advantageLabel.Text = $"[color={DISADVANTAGE_COLOR.ToHtml()}]\u25BC 距离 {distance} 格 / 射程 {maxRange} 格[/color]";
        _detailsLabel.Text = $"[color=gray]目标: {target.Data.UnitName}[/color]\n[color=gray]防御等级: {_GetDefenseRating(target.GetAc())}[/color]";

        ShowAtMouse();
    }

    /// <summary>隐藏预览</summary>
    public void HidePreview() => HidePanel();

    // ============================================================================
    // 内部方法
    // ============================================================================

    private static string _GetDefenseRating(int ac) => ac switch
    {
        <= 8 => "极弱",
        <= 10 => "较弱",
        <= 12 => "普通",
        <= 14 => "坚固",
        <= 16 => "精良",
        <= 18 => "极其坚固",
        _ => "铜墙铁壁",
    };
}
