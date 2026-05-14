// HitPreviewTooltip.cs
// 命中率预览浮窗 - 悬停敌方时显示命中率%、预计伤害范围、优势/劣势原因
// 核心设计原则：不暴露骰子术语(d20)，只显示概率和直观信息
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Combat;
using BladeHex.UI;

namespace BladeHex.UI.Combat;

/// <summary>
/// 命中预览提示 — 悬停敌方时显示命中率、伤害范围、优劣势原因
/// </summary>
[GlobalClass]
public partial class HitPreviewTooltip : PanelContainer
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
    private static readonly Color BG_COLOR = new(0.06f, 0.05f, 0.09f, 0.95f);
    private static readonly Color BORDER_COLOR = new(0.5f, 0.4f, 0.2f, 0.8f);
    private static readonly Color HIT_COLOR = new(0.3f, 0.85f, 0.3f);
    private static readonly Color MISS_COLOR = new(0.85f, 0.3f, 0.3f);
    private static readonly Color ADVANTAGE_COLOR = new(0.3f, 0.85f, 0.9f);
    private static readonly Color DISADVANTAGE_COLOR = new(0.9f, 0.5f, 0.2f);
    private static readonly Color NEUTRAL_COLOR = new(0.7f, 0.7f, 0.7f);

    // ============================================================================
    // _Ready
    // ============================================================================
    public override void _Ready()
    {
        _SetupTooltip();
        Visible = false;
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        // 持续跟随鼠标位置
        var mousePos = GetViewport().GetMousePosition();
        Position = mousePos + new Vector2(15, 15);

        // 边界修正
        var viewportSize = GetViewport().GetVisibleRect().Size;
        if (Position.X + Size.X > viewportSize.X)
            Position = new Vector2(mousePos.X - Size.X - 10, Position.Y);
        if (Position.Y + Size.Y > viewportSize.Y)
            Position = new Vector2(Position.X, mousePos.Y - Size.Y - 10);
    }

    // ============================================================================
    // 初始化 UI 结构
    // ============================================================================
    private void _SetupTooltip()
    {
        // 面板样式
        var style = new StyleBoxFlat();
        style.BgColor = BG_COLOR;
        style.SetBorderWidthAll(2);
        style.BorderColor = BORDER_COLOR;
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(8);
        AddThemeStyleboxOverride("panel", style);

        // 确保始终在最上层
        ZIndex = 100;

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 3);
        AddChild(vbox);

        // 命中率
        _hitLabel = new Label();
        _hitLabel.AddThemeFontSizeOverride("font_size", 15);
        vbox.AddChild(_hitLabel);

        // 预计伤害
        _dmgLabel = new Label();
        _dmgLabel.AddThemeFontSizeOverride("font_size", 13);
        _dmgLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.75f, 0.5f));
        vbox.AddChild(_dmgLabel);

        // 分隔线
        var sep = new HSeparator();
        sep.AddThemeStyleboxOverride("separator", _MakeLineStyle(new Color(0.4f, 0.35f, 0.2f, 0.5f)));
        vbox.AddChild(sep);

        // 优势/劣势原因
        _advantageLabel = new RichTextLabel();
        _advantageLabel.BbcodeEnabled = true;
        _advantageLabel.CustomMinimumSize = new Vector2(180, 0);
        _advantageLabel.FitContent = true;
        _advantageLabel.ScrollActive = false;
        vbox.AddChild(_advantageLabel);

        // 详细信息（掩体、高程、武器等）
        _detailsLabel = new RichTextLabel();
        _detailsLabel.BbcodeEnabled = true;
        _detailsLabel.CustomMinimumSize = new Vector2(180, 0);
        _detailsLabel.FitContent = true;
        _detailsLabel.ScrollActive = false;
        vbox.AddChild(_detailsLabel);

        // 鼠标穿透（避免阻挡其他交互）
        MouseFilter = MouseFilterEnum.Ignore;
    }

    private static StyleBoxFlat _MakeLineStyle(Color color)
    {
        var s = new StyleBoxFlat();
        s.BgColor = color;
        s.SetContentMarginAll(1);
        return s;
    }

    // ============================================================================
    // 显示预览信息
    // ============================================================================
    /// <summary>
    /// 显示命中预览。
    /// </summary>
    /// <param name="attacker">攻击方单位</param>
    /// <param name="target">防御方单位</param>
    /// <param name="coverType">掩体等级 (0=无, 1=半掩体, 2=全掩体)</param>
    /// <param name="elevationDiff">高程差 (正=攻击者在高处)</param>
    /// <param name="hasFlanking">是否包夹</param>
    /// <param name="hasSneak">是否伏击</param>
    public void ShowPreview(Unit attacker, Unit target, int coverType = 0, int elevationDiff = 0, bool hasFlanking = false, bool hasSneak = false)
    {
        if (attacker == null || target == null || attacker.Data == null || target.Data == null)
            return;

        Visible = true;

        var weapon = attacker.GetMainHand() as WeaponData;
        int targetAc = target.GetAc();

        // === 收集优势/劣势因素 ===
        var advantages = new List<string>();
        var disadvantages = new List<string>();

        // 高程优势
        if (elevationDiff > 0)
        {
            advantages.Add("占据高地");
        }
        else if (elevationDiff < 0)
        {
            disadvantages.Add("仰攻不利");
        }

        // 包夹优势
        if (hasFlanking)
            advantages.Add("包夹攻击");

        // 伏击优势
        if (hasSneak)
            advantages.Add("伏击!");

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
                // 全掩体不可被远程攻击
                _hitLabel.Text = "不可攻击";
                _hitLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
                _dmgLabel.Text = "目标完全隐蔽";
                _advantageLabel.Text = "";
                _detailsLabel.Text = "[color=gray]全掩体单位不可被远程攻击[/color]";
                return;
            }
        }

        // 目标低HP状态（轻伤/重伤惩罚）
        if (target.CurrentHp > 0)
        {
            float hpRatio = (float)target.CurrentHp / Mathf.Max(target.GetMaxHp(), 1);
            if (hpRatio < 0.25f)
                advantages.Add("目标重伤");
            else if (hpRatio < 0.5f)
                advantages.Add("目标轻伤");
        }

        // 士气影响（攻击方士气低 → 失误率增加）
        if (attacker.Data.IsEnemy && attacker.Data.Morale <= -40)
            disadvantages.Add("士气崩溃");

        // === 计算命中率 ===
        // 使用 CombatResolver 的预览公式（考虑攻击加值、AC、高地等）
        float hitChanceRaw = CombatResolver.GetHitChancePreview(attacker, target);
        double hitChance = hitChanceRaw * 100.0;
        hitChance = Mathf.Clamp(hitChance, 5.0, 95.0);

        // 优势/劣势修正（约 ±15%）
        if (advantages.Count > disadvantages.Count)
            hitChance = Mathf.Min(hitChance + 15.0, 95.0);
        else if (disadvantages.Count > advantages.Count)
            hitChance = Mathf.Max(hitChance - 15.0, 5.0);

        // === 计算预计伤害范围 ===
        int strMod = attacker.GetStatModifier(attacker.Data.Str);
        int levelExtra = attacker.Data != null ? RPGRuleEngine.GetDamageDiceCount(attacker.Data.Level) - 1 : 0;

        int minDmg;
        int maxDmg;
        if (weapon != null)
        {
            minDmg = weapon.DamageDiceCount + strMod;
            maxDmg = weapon.DamageDiceCount * weapon.DamageDiceSides + strMod;
            // 等级Nd20: 最少每骰1点，最多每骰20点
            minDmg += levelExtra * 1;
            maxDmg += levelExtra * 20;
            minDmg = Mathf.Max(1, minDmg);
            maxDmg = Mathf.Max(minDmg, maxDmg);
        }
        else
        {
            // 徒手：1d20 + 等级Nd20 + STR
            minDmg = 1 + levelExtra * 1 + strMod;
            maxDmg = 20 + levelExtra * 20 + strMod;
            minDmg = Mathf.Max(1, minDmg);
            maxDmg = Mathf.Max(minDmg, maxDmg);
        }

        // 包夹伤害加成
        if (hasFlanking)
            maxDmg = (int)(maxDmg * 1.25f);

        // === 更新UI显示 ===

        // 命中率颜色
        Color hitColor;
        if (hitChance >= 75)
            hitColor = HIT_COLOR;
        else if (hitChance >= 50)
            hitColor = new Color(0.7f, 0.8f, 0.3f);
        else if (hitChance >= 25)
            hitColor = new Color(0.85f, 0.65f, 0.2f);
        else
            hitColor = MISS_COLOR;

        _hitLabel.Text = $"命中率: {Mathf.RoundToInt((float)hitChance)}%";
        _hitLabel.AddThemeColorOverride("font_color", hitColor);

        _dmgLabel.Text = $"预计伤害: {minDmg} - {maxDmg}";

        // 优势/劣势文本
        string advText = "";
        foreach (var a in advantages)
            advText += $"[color={ADVANTAGE_COLOR.ToHtml()}]\u25B2 {a}[/color]\n";
        foreach (var d in disadvantages)
            advText += $"[color={DISADVANTAGE_COLOR.ToHtml()}]\u25BC {d}[/color]\n";
        _advantageLabel.Text = advText.TrimEnd();

        // 详细信息
        string detailText = "";
        string weaponName = weapon?.ItemName ?? "徒手";
        int weaponRange = weapon?.RangeCells ?? 1;
        bool isRanged = weapon?.IsRanged ?? false;

        detailText += $"[color=gray]武器: {weaponName} ({(isRanged ? "远程" : "近战")})[/color]\n";
        detailText += $"[color=gray]射程: {weaponRange}格[/color]\n";
        detailText += $"[color=gray]防御等级: {_GetDefenseRating(targetAc)}[/color]";

        // 敌方特殊属性提醒
        if (target.Data.IsEnemy)
        {
            if (target.Data.Immunities.Length > 0)
                detailText += $"\n[color=#ff6666]免疫: {string.Join(", ", target.Data.Immunities)}[/color]";
            if (target.Data.Resistances.Length > 0)
                detailText += $"\n[color=#ccaa44]抗性: {string.Join(", ", target.Data.Resistances)}[/color]";
        }

        _detailsLabel.Text = detailText;
    }

    // ============================================================================
    // 显示超出射程信息
    // ============================================================================
    /// <summary>
    /// 显示超出射程预览 — 命中率 0%，提示距离信息。
    /// </summary>
    public void ShowOutOfRange(Unit target, int distance, int maxRange)
    {
        if (target == null || target.Data == null)
            return;

        Visible = true;

        _hitLabel.Text = "命中率: 0%";
        _hitLabel.AddThemeColorOverride("font_color", MISS_COLOR);

        _dmgLabel.Text = "超出攻击范围";

        _advantageLabel.Text = $"[color={DISADVANTAGE_COLOR.ToHtml()}]\u25BC 距离 {distance} 格 / 射程 {maxRange} 格[/color]";

        string detailText = $"[color=gray]目标: {target.Data.UnitName}[/color]\n";
        detailText += $"[color=gray]防御等级: {_GetDefenseRating(target.GetAc())}[/color]";
        _detailsLabel.Text = detailText;
    }

    // ============================================================================
    // 隐藏预览
    // ============================================================================
    public void HidePreview()
    {
        Visible = false;
    }

    // ============================================================================
    // 跟随鼠标位置
    // ============================================================================
    public async void FollowMouse(Vector2 globalPos)
    {
        Position = globalPos + new Vector2(15, 15);

        // 边界修正（防止超出屏幕）
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var viewportSize = GetViewport().GetVisibleRect().Size;
        if (Position.X + Size.X > viewportSize.X)
            Position = new Vector2(globalPos.X - Size.X - 10, Position.Y);
        if (Position.Y + Size.Y > viewportSize.Y)
            Position = new Vector2(Position.X, globalPos.Y - Size.Y - 10);
    }

    // ============================================================================
    // 防御等级描述 — 将内部AC值转换为玩家感知的防御等级
    // ============================================================================
    private static string _GetDefenseRating(int ac)
    {
        if (ac <= 8) return "极弱";
        if (ac <= 10) return "较弱";
        if (ac <= 12) return "普通";
        if (ac <= 14) return "坚固";
        if (ac <= 16) return "精良";
        if (ac <= 18) return "极其坚固";
        return "铜墙铁壁";
    }
}
