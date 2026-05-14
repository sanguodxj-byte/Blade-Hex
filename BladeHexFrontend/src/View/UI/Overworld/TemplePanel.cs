// TemplePanel.cs
// Temple panel - Heal wounds, purchase purifying potions, cleanse curses
using Godot;
using BladeHex.Data;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class TemplePanel : POIPanelBase
{
    [Signal]
    public delegate void TempleFinishedEventHandler();

    private Label _goldLabel = null!;
    private RichTextLabel _resultLabel = null!;
    private EconomyManager _economy = null!;

    // ── Panel specs ───────────────────────────────────────────────
    protected override int PanelWidth => 400;
    protected override int PanelHeight => 360;

    // ── Public API ────────────────────────────────────────────────

    public void ShowTemple(EconomyManager economy)
    {
        _economy = economy;
        _resultLabel.Text = "";
        _goldLabel.Text = $"{(_economy?.Gold ?? 0)}";
        ShowPanel();
    }

    public override void HidePanel()
    {
        base.HidePanel();
    }

    // ── Content ───────────────────────────────────────────────────

    protected override void BuildContent(VBoxContainer container)
    {
        // Title
        container.AddChild(CreateTitleLabel("药师所"));

        // Description
        container.AddChild(CreateBodyLabel("药师的力量可以治愈伤痛，净化邪恶。"));

        // Gold header
        var header = new HBoxContainer();
        var headerTitle = CreateBodyLabel("金币:");
        headerTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(headerTitle);

        _goldLabel = new Label();
        _goldLabel.Text = "0";
        _goldLabel.AddThemeColorOverride("font_color", ThemeTextAccent);
        header.AddChild(_goldLabel);
        container.AddChild(header);

        container.AddChild(CreateSeparatorH());

        // Service buttons
        var btnMinor = CreateButton("轻度治疗 (15金) — 恢复50%HP", new Vector2(360, 40));
        btnMinor.Pressed += () => Heal(15, 0.5f, "轻度治疗");
        container.AddChild(btnMinor);

        var btnMajor = CreateButton("深度治疗 (40金) — 恢复100%HP", new Vector2(360, 40));
        btnMajor.Pressed += () => Heal(40, 1.0f, "深度治疗");
        container.AddChild(btnMajor);

        var btnPurify = CreateButton("净化诅咒 (60金) — 移除所有负面状态", new Vector2(360, 40));
        btnPurify.Pressed += Purify;
        container.AddChild(btnPurify);

        var btnHolyWater = CreateButton("购买净化药水 (25金) — 对亡灵额外奥术伤害", new Vector2(360, 40));
        btnHolyWater.Pressed += BuyHolyWater;
        container.AddChild(btnHolyWater);

        container.AddChild(CreateSeparatorH());

        // Result
        _resultLabel = CreateResultLabel();
        _resultLabel.CustomMinimumSize = new Vector2(360, 40);
        container.AddChild(_resultLabel);

        // Close
        var closeBtn = CreateButton("离开药师所", new Vector2(360, 40));
        closeBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        closeBtn.Pressed += () => { EmitSignal(SignalName.TempleFinished); HidePanel(); };
        container.AddChild(closeBtn);
    }

    protected override void OnCloseRequested()
    {
        EmitSignal(SignalName.TempleFinished);
        HidePanel();
    }

    // ── Internal Logic ────────────────────────────────────────────

    private void Heal(int cost, float ratio, string name)
    {
        if (_economy != null && !_economy.SpendGold(cost))
        {
            _resultLabel.Text = "[color=red]金币不足！[/color]";
            return;
        }

        _resultLabel.Text = $"[color=green]{name}完成！恢复了{ratio * 100:F0}%生命值。[/color]";
        UpdateGold();
    }

    private void Purify()
    {
        if (_economy != null && !_economy.SpendGold(60))
        {
            _resultLabel.Text = "[color=red]金币不足！[/color]";
            return;
        }

        _resultLabel.Text = "[color=green]净化之光笼罩全身，所有诅咒已净化。[/color]";
        UpdateGold();
    }

    private void BuyHolyWater()
    {
        if (_economy != null && !_economy.SpendGold(25))
        {
            _resultLabel.Text = "[color=red]金币不足！[/color]";
            return;
        }

        _resultLabel.Text = "[color=green]获得净化药水×1。对亡灵类敌人造成额外1d6奥术伤害。[/color]";
        UpdateGold();
    }

    private void UpdateGold()
    {
        if (_economy != null)
            _goldLabel.Text = $"{_economy.Gold}";
    }
}
