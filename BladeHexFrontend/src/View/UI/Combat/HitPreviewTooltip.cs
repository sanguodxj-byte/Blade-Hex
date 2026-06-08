// HitPreviewTooltip.cs
// 命中率与暴击率预览浮窗 - 悬停敌方时显示命中率%、暴击率%、预计伤害范围、优势/劣势原因
// 核心设计原则：不暴露骰子术语(d20)，只显示概率和直观信息，提供极佳的 premium 视觉感受。严禁使用任何 emoji 符号。
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Combat;
using BladeHex.UI;
using BladeHex.UI.Common;
using BladeHex.Map;

namespace BladeHex.UI.Combat;

/// <summary>
/// 命中预览提示 — 悬停敌方时显示命中率、暴击率、伤害范围、优劣势原因
/// </summary>
[GlobalClass]
public partial class HitPreviewTooltip : FloatingPanel
{
    // ============================================================================
    // 子控件
    // ============================================================================
    private RichTextLabel _titleLabel = null!;
    private RichTextLabel _hitLabel = null!;
    private RichTextLabel _critLabel = null!;
    private RichTextLabel _dmgLabel = null!;
    private RichTextLabel _advantageLabel = null!;
    private RichTextLabel _detailsLabel = null!;

    // ============================================================================
    // 颜色常量
    // ============================================================================
    private static readonly Color HIT_COLOR = new(0.25f, 0.85f, 0.35f);
    private static readonly Color MISS_COLOR = new(0.9f, 0.25f, 0.25f);
    private static readonly Color ADVANTAGE_COLOR = new(0.2f, 0.8f, 0.95f);
    private static readonly Color DISADVANTAGE_COLOR = new(0.95f, 0.55f, 0.15f);

    // ============================================================================
    // FloatingPanel 配置
    // ============================================================================

    protected override Color PanelBgColor => new(0.08f, 0.07f, 0.12f, 0.98f); // 战斗预览特殊暗紫灰底色
    protected override Color PanelBorderColor => new(0.72f, 0.58f, 0.36f, 0.9f); // 战斗预览亮金色边框
    protected override bool FollowMouseContinuously => true;

    // ============================================================================
    // 构建内容
    // ============================================================================

    protected override void BuildContent()
    {
        // 目标标题
        _titleLabel = MakeRichText(200);
        Content.AddChild(_titleLabel);

        Content.AddChild(MakeSeparator(0.25f));

        // 命中率与暴击率（横向双栏排版）
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        Content.AddChild(row);

        _hitLabel = MakeRichText(95);
        _critLabel = MakeRichText(105);
        row.AddChild(_hitLabel);
        row.AddChild(_critLabel);

        // 预计伤害
        _dmgLabel = MakeRichText(200);
        Content.AddChild(_dmgLabel);

        Content.AddChild(MakeSeparator(0.25f));

        // 优势/劣势原因
        _advantageLabel = MakeRichText(200);
        Content.AddChild(_advantageLabel);

        // 详细信息（防具、武器、免疫抗性等）
        _detailsLabel = MakeRichText(200);
        Content.AddChild(_detailsLabel);
    }

    // ============================================================================
    // 公共 API
    // ============================================================================

    /// <summary>显示命中率与暴击率预览。</summary>
    public void ShowPreview(Unit attacker, Unit target, HexGrid? grid = null, int coverType = 0, int elevationDiff = 0, bool hasFlanking = false, bool hasSneak = false)
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
                string sideTag2 = target.Data.IsEnemy ? "[color=#ff6666][敌方][/color]" : "[color=#66ff66][友方][/color]";
                _titleLabel.Text = $"[font_size=14][b]{target.Data.UnitName}[/b][/font_size] {sideTag2}\n[color=red][完全隐蔽][/color]";
                _hitLabel.Text = "命中:\n[color=gray][font_size=16][b]--[/b][/font_size][/color]";
                _critLabel.Text = "暴击:\n[color=gray][font_size=16][b]--[/b][/font_size][/color]";
                _dmgLabel.Text = "[color=red]目标处于全掩体阻挡[/color]";
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

        // === 计算优势/劣势状态并精密计算命中率 ===
        bool hasAdvantage = advantages.Count > disadvantages.Count;
        bool hasDisadvantage = disadvantages.Count > advantages.Count;

        // 透传 hasFlanking 与 hasSneak 给命中预测核心，实现所有因子的全覆盖
        float hitChanceRaw = CombatResolver.GetHitChancePreview(attacker, target, grid, hasFlanking, hasSneak);
        double hitChance = hitChanceRaw * 100.0;
        hitChance = Mathf.Clamp(hitChance, 5.0, 95.0);

        // === 计算精密暴击率 (依据 DND 优势/劣势 与 技能树加成) ===
        int critThreshold = attacker.Model.GetCritThreshold();
        float bonusCritChance = 0f;
        if (attacker.SkillTree != null)
            bonusCritChance += attacker.SkillTree.GetCriticalRateBonus();

        float d20CritChance = 0f;
        int k = Mathf.Clamp(critThreshold, 1, 20);
        if (hasAdvantage) // 优势
        {
            float failProb = (k - 1) / 20.0f;
            d20CritChance = 1.0f - failProb * failProb;
        }
        else if (hasDisadvantage) // 劣势
        {
            float successProb = (21 - k) / 20.0f;
            d20CritChance = successProb * successProb;
        }
        else // 正常
        {
            d20CritChance = (21 - k) / 20.0f;
        }

        float totalCritChance = d20CritChance + (1.0f - d20CritChance) * bonusCritChance;
        int critChancePercent = Mathf.RoundToInt(totalCritChance * 100.0f);
        critChancePercent = Mathf.Clamp(critChancePercent, 0, 100);

        // 获取暴击伤害倍率
        int critMultiplier = PassiveSkillResolver.GetCritMultiplier(attacker);

        // === 计算预计伤害范围 ===
        int strMod = attacker.GetStatModifier(CombatStats.GetEffectiveStr(attacker.Data));
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

        // === 更新 UI 显示 ===
        string sideTag = target.Data.IsEnemy ? "[color=#ff6666][敌方][/color]" : "[color=#66ff66][友方][/color]";
        string enemyType = target.Data.IsEnemy ? $" • {target.Data.GetEnemyTypeName()}" : "";
        _titleLabel.Text = $"[font_size=14][b]{target.Data.UnitName}[/b][/font_size] {sideTag}\n[color=#8a8a9a]Lv.{target.Data.Level}{enemyType}[/color]";

        Color hitColor = hitChance >= 75 ? HIT_COLOR
            : hitChance >= 50 ? new Color(0.75f, 0.8f, 0.3f)
            : hitChance >= 25 ? new Color(0.9f, 0.65f, 0.2f)
            : MISS_COLOR;

        _hitLabel.Text = $"命中率:\n[color={hitColor.ToHtml()}][font_size=18][b]{Mathf.RoundToInt((float)hitChance)}%[/b][/font_size][/color]";
        _critLabel.Text = $"暴击率:\n[color=gold][font_size=18][b]{critChancePercent}%[/b][/font_size][/color] [color=#eebbee](x{critMultiplier})[/color]";
        _dmgLabel.Text = $"预计伤害: [color=#ffd700][font_size=14][b]{minDmg} - {maxDmg}[/b][/font_size][/color]";

        string advText = "";
        foreach (var a in advantages)
            advText += $"[color={ADVANTAGE_COLOR.ToHtml()}]▲ {a}[/color]\n";
        foreach (var d in disadvantages)
            advText += $"[color={DISADVANTAGE_COLOR.ToHtml()}]▼ {d}[/color]\n";
        _advantageLabel.Text = advText.TrimEnd();

        string weaponName = weapon?.ItemName ?? "徒手";
        int weaponRange = weapon?.RangeCells ?? 1;
        bool isRanged = weapon?.IsRanged ?? false;
        string detailText = $"[color=#8a8a9a]武器: {weaponName} ({(isRanged ? "远程" : "近战")})[/color]\n";
        detailText += $"[color=#8a8a9a]射程: {weaponRange}格[/color]\n";
        detailText += $"[color=#8a8a9a]防御等级: {_GetDefenseRating(targetAc)}[/color]";

        if (target.Data.IsEnemy)
        {
            if (target.Data.Immunities.Length > 0)
                detailText += $"\n[color=#ff7777]免疫: {string.Join(", ", target.Data.Immunities)}[/color]";
            if (target.Data.Resistances.Length > 0)
                detailText += $"\n[color=#ccaa55]抗性: {string.Join(", ", target.Data.Resistances)}[/color]";
        }
        _detailsLabel.Text = detailText;

        ShowAtMouse();
    }

    /// <summary>显示超出射程预览</summary>
    public void ShowOutOfRange(Unit target, int distance, int maxRange)
    {
        if (target == null || target.Data == null) return;

        string sideTag = target.Data.IsEnemy ? "[color=#ff6666][敌方][/color]" : "[color=#66ff66][友方][/color]";
        string enemyType = target.Data.IsEnemy ? $" • {target.Data.GetEnemyTypeName()}" : "";
        _titleLabel.Text = $"[font_size=14][b]{target.Data.UnitName}[/b][/font_size] {sideTag}\n[color=#ff5555][font_size=11]超出攻击范围[/font_size][/color]";

        _hitLabel.Text = "命中率:\n[color=gray][font_size=18][b]0%[/b][/font_size][/color]";
        _critLabel.Text = "暴击率:\n[color=gray][font_size=18][b]0%[/b][/font_size][/color]";
        _dmgLabel.Text = "预计伤害: [color=gray]--[/color]";
        _advantageLabel.Text = $"[color={DISADVANTAGE_COLOR.ToHtml()}]▼ 距离 {distance} 格 / 射程 {maxRange} 格[/color]";
        _detailsLabel.Text = $"[color=#8a8a9a]防御等级: {_GetDefenseRating(target.GetAc())}[/color]";

        ShowAtMouse();
    }

    /// <summary>显示行动力不足预览</summary>
    public void ShowApDeficient(Unit target, int distance, float requiredAp, float currentAp)
    {
        if (target == null || target.Data == null) return;

        string sideTag = target.Data.IsEnemy ? "[color=#ff6666][敌方][/color]" : "[color=#66ff66][友方][/color]";
        string enemyType = target.Data.IsEnemy ? $" • {target.Data.GetEnemyTypeName()}" : "";
        _titleLabel.Text = $"[font_size=14][b]{target.Data.UnitName}[/b][/font_size] {sideTag}\n[color=#ff5555][font_size=11]行动力不足[/font_size][/color]";

        _hitLabel.Text = "命中率:\n[color=gray][font_size=18][b]--[/b][/font_size][/color]";
        _critLabel.Text = "暴击率:\n[color=gray][font_size=18][b]--[/b][/font_size][/color]";
        _dmgLabel.Text = "预计伤害: [color=gray]--[/color]";
        _advantageLabel.Text = $"[color={DISADVANTAGE_COLOR.ToHtml()}]▼ 需要 {requiredAp:F0} AP / 当前 {currentAp:F0} AP[/color]";
        _detailsLabel.Text = $"[color=#8a8a9a]防御等级: {_GetDefenseRating(target.GetAc())}[/color]";

        ShowAtMouse();
    }

    /// <summary>显示硬性规则阻挡预览。</summary>
    public void ShowBlocked(Unit target, string reason)
    {
        if (target == null || target.Data == null) return;

        string sideTag = target.Data.IsEnemy ? "[color=#ff6666][敌方][/color]" : "[color=#66ff66][友方][/color]";
        string enemyType = target.Data.IsEnemy ? $" • {target.Data.GetEnemyTypeName()}" : "";
        _titleLabel.Text = $"[font_size=14][b]{target.Data.UnitName}[/b][/font_size] {sideTag}\n[color=#ff5555][font_size=11]无法攻击[/font_size][/color]";

        _hitLabel.Text = "命中率:\n[color=gray][font_size=18][b]--[/b][/font_size][/color]";
        _critLabel.Text = "暴击率:\n[color=gray][font_size=18][b]--[/b][/font_size][/color]";
        _dmgLabel.Text = "预计伤害: [color=gray]--[/color]";
        _advantageLabel.Text = $"[color={DISADVANTAGE_COLOR.ToHtml()}]▼ {reason}[/color]";
        _detailsLabel.Text = $"[color=#8a8a9a]防御等级: {_GetDefenseRating(target.GetAc())}[/color]";

        ShowAtMouse();
    }

    /// <summary>隐藏预览</summary>
    public void HidePreview() => HidePanel();

    /// <summary>显示职业技能描述预览</summary>
    public void ShowCareerSkillPreview(string description, Vector2 globalMousePos)
    {
        // 清空其他内容，只显示描述
        _titleLabel.Text = "";
        _hitLabel.Text = "";
        _critLabel.Text = "";
        _dmgLabel.Text = "";
        _advantageLabel.Text = "";
        _detailsLabel.Text = description;

        // 设置位置到鼠标附近
        GlobalPosition = globalMousePos + new Vector2(15, 15);
        Visible = true;
    }

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
