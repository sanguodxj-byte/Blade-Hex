using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Combat;
using BladeHex.Map;

namespace BladeHex.UI.Combat;

/// <summary>
/// 命中率预览浮窗 - 悬停敌方时显示命中率%、预计伤害范围、优势/劣势原因
/// 迁移自 GDScript HitPreviewTooltip.gd
/// </summary>
public partial class HitPreviewTooltip : PanelContainer
{
    private Label _hitLabel = null!;
    private Label _dmgLabel = null!;
    private RichTextLabel _advantageLabel = null!;
    private RichTextLabel _detailsLabel = null!;

    private readonly Color _hitColor = new(0.3f, 0.85f, 0.3f);
    private readonly Color _missColor = new(0.85f, 0.3f, 0.3f);
    private readonly Color _advantageColor = new(0.3f, 0.85f, 0.9f);
    private readonly Color _disadvantageColor = new(0.9f, 0.5f, 0.2f);

    public override void _Ready()
    {
        SetupTooltip();
        Visible = false;
        ZIndex = 100;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    private void SetupTooltip()
    {
        AddThemeStyleboxOverride("panel", UITheme.Instance.MakePanelStyle(new Color(0.06f, 0.05f, 0.09f, 0.95f), new Color(0.5f, 0.4f, 0.2f, 0.8f), 2, 4));

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 3);
        AddChild(vbox);

        _hitLabel = new Label();
        _hitLabel.AddThemeFontSizeOverride("font_size", 15);
        vbox.AddChild(_hitLabel);

        _dmgLabel = new Label();
        _dmgLabel.AddThemeFontSizeOverride("font_size", 13);
        _dmgLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.75f, 0.5f));
        vbox.AddChild(_dmgLabel);

        vbox.AddChild(new HSeparator());

        _advantageLabel = new RichTextLabel { BbcodeEnabled = true, FitContent = true, CustomMinimumSize = new Vector2(180, 0), ScrollActive = false };
        vbox.AddChild(_advantageLabel);

        _detailsLabel = new RichTextLabel { BbcodeEnabled = true, FitContent = true, CustomMinimumSize = new Vector2(180, 0), ScrollActive = false };
        vbox.AddChild(_detailsLabel);
    }

    public void ShowPreview(Unit attacker, Unit target, HexGrid? grid = null, int coverType = 0, int elevationDiff = 0)
    {
        if (!GodotObject.IsInstanceValid(attacker) || !GodotObject.IsInstanceValid(target) || attacker.Data == null || target.Data == null) return;

        Visible = true;
        var weapon = attacker.GetMainHand() as WeaponData;

        // 计算命中率
        float hitChance = CombatResolver.GetHitChancePreview(attacker, target, grid) * 100.0f;
        hitChance = Math.Clamp(hitChance, 5.0f, 95.0f);

        // 收集优势劣势
        var advantages = new List<string>();
        var disadvantages = new List<string>();

        if (elevationDiff > 0) advantages.Add("占据高地");
        else if (elevationDiff < 0) disadvantages.Add("仰攻不利");

        if (coverType == 1 && weapon != null && weapon.IsRanged) disadvantages.Add("半掩体阻挡");
        if (coverType == 2 && weapon != null && weapon.IsRanged)
        {
            _hitLabel.Text = "不可攻击";
            _hitLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            _dmgLabel.Text = "目标完全隐蔽";
            _advantageLabel.Text = "";
            _detailsLabel.Text = "[color=gray]全掩体单位不可被远程攻击[/color]";
            return;
        }

        // 更新 UI
        _hitLabel.Text = $"命中率: {(int)Math.Round(hitChance)}%";
        _hitLabel.AddThemeColorOverride("font_color", hitChance >= 70 ? _hitColor : (hitChance >= 40 ? new Color(0.8f, 0.8f, 0.3f) : _missColor));

        var dmgPreview = CombatResolver.GetDamagePreview(attacker);
        _dmgLabel.Text = $"预计伤害: {dmgPreview["min"]} - {dmgPreview["max"]}";

        string advText = "";
        foreach (var a in advantages) advText += $"[color=#{_advantageColor.ToHtml(false)}]▲ {a}[/color]\n";
        foreach (var d in disadvantages) advText += $"[color=#{_disadvantageColor.ToHtml(false)}]▼ {d}[/color]\n";
        _advantageLabel.Text = advText.Trim();

        string detailText = $"[color=gray]武器: {(weapon?.ItemName ?? "徒手")} ({(weapon?.IsRanged == true ? "远程" : "近战")})[/color]\n";
        detailText += $"[color=gray]射程: {(weapon?.RangeCells ?? 1)}格[/color]\n";
        detailText += $"[color=gray]防御等级: {GetDefenseRating(target.GetAc())}[/color]";
        _detailsLabel.Text = detailText;
    }

    private string GetDefenseRating(int ac) => ac switch
    {
        <= 8 => "极弱",
        <= 12 => "普通",
        <= 16 => "精良",
        _ => "坚固"
    };

    public void FollowMouse(Vector2 globalPos)
    {
        Position = globalPos + new Vector2(15, 15);
        // 边界修正逻辑可在此处添加
    }

    public void HidePreview() => Visible = false;
}
