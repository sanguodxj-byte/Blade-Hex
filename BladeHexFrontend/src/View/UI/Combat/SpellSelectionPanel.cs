// SpellSelectionPanel.cs
// 法术选择面板 — 战斗中选择施放的法术
// 显示已学法术列表，含法力消耗、冷却、射程、描述
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Combat;
using BladeHex.UI;

namespace BladeHex.UI.Combat;

[GlobalClass]
public partial class SpellSelectionPanel : PanelContainer
{
    [Signal] public delegate void SpellChosenEventHandler(SpellData spell);
    [Signal] public delegate void PanelClosedEventHandler();

    private new UITheme Theme => UITheme.Instance!;
    private VBoxContainer _spellList = null!;
    private Label _titleLabel = null!;
    private Label _manaLabel = null!;
    private Unit? _caster;

    public override void _Ready()
    {
        _BuildUI();
        Visible = false;
    }

    private void _BuildUI()
    {
        CustomMinimumSize = new Vector2(320, 400);
        SetAnchorsAndOffsetsPreset(LayoutPreset.CenterRight);
        OffsetLeft = -340;

        var style = new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.07f, 0.95f) };
        style.SetBorderWidthAll(2);
        style.BorderColor = new Color(0.4f, 0.3f, 0.7f, 0.8f);
        style.SetCornerRadiusAll(6);
        style.SetContentMarginAll(12);
        AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        AddChild(vbox);

        // 标题行
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(header);

        _titleLabel = new Label { Text = "法术" };
        _titleLabel.AddThemeFontSizeOverride("font_size", 16);
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.5f, 1f));
        _titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(_titleLabel);

        _manaLabel = new Label { Text = "法力: 0" };
        _manaLabel.AddThemeFontSizeOverride("font_size", 12);
        _manaLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.6f, 1f));
        header.AddChild(_manaLabel);

        var closeBtn = new Button { Text = "✕", CustomMinimumSize = new Vector2(28, 28) };
        closeBtn.Pressed += () => { Visible = false; EmitSignal(SignalName.PanelClosed); };
        header.AddChild(closeBtn);

        vbox.AddChild(new HSeparator());

        // 法术列表（可滚动）
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        vbox.AddChild(scroll);

        _spellList = new VBoxContainer();
        _spellList.AddThemeConstantOverride("separation", 4);
        _spellList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_spellList);
    }

    /// <summary>打开面板，显示施法者的已知法术</summary>
    public void ShowForUnit(Unit caster, SpellManager? spellManager = null)
    {
        _caster = caster;
        Visible = true;
        _Refresh();
    }

    private void _Refresh()
    {
        foreach (Node c in _spellList.GetChildren()) c.QueueFree();

        if (_caster?.Data == null) return;

        int mana = _caster.Data.CurrentMana;
        _manaLabel.Text = $"法力: {mana}";

        var spells = _caster.Data.KnownSpells;
        if (spells == null || spells.Count == 0)
        {
            var empty = new Label { Text = "未学习任何法术" };
            empty.AddThemeFontSizeOverride("font_size", 12);
            empty.AddThemeColorOverride("font_color", new Color(0.5f, 0.48f, 0.45f));
            _spellList.AddChild(empty);
            return;
        }

        foreach (var spell in spells)
        {
            if (spell == null) continue;
            _AddSpellEntry(spell, mana);
        }
    }

    private void _AddSpellEntry(SpellData spell, int currentMana)
    {
        bool canCast = currentMana >= spell.ManaCost && _caster!.CurrentAp >= 4;
        bool onCooldown = false;
        if (_caster!.Data!.SpellCooldowns.ContainsKey(spell.SpellId))
        {
            int cd = _caster.Data.SpellCooldowns[spell.SpellId].AsInt32();
            if (cd > 0) { onCooldown = true; canCast = false; }
        }

        var entry = new PanelContainer();
        var entryStyle = new StyleBoxFlat
        {
            BgColor = canCast ? new Color(0.08f, 0.08f, 0.12f, 0.9f) : new Color(0.06f, 0.06f, 0.08f, 0.7f)
        };
        entryStyle.SetBorderWidthAll(1);
        entryStyle.BorderColor = canCast ? new Color(0.4f, 0.3f, 0.7f, 0.6f) : new Color(0.2f, 0.2f, 0.25f, 0.4f);
        entryStyle.SetCornerRadiusAll(4);
        entryStyle.SetContentMarginAll(8);
        entry.AddThemeStyleboxOverride("panel", entryStyle);
        if (canCast) entry.MouseDefaultCursorShape = CursorShape.PointingHand;
        _spellList.AddChild(entry);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);
        entry.AddChild(vbox);

        // 第一行：名称 + 消耗
        var row1 = new HBoxContainer();
        row1.AddThemeConstantOverride("separation", 6);
        vbox.AddChild(row1);

        var nameColor = canCast ? new Color(0.9f, 0.85f, 1f) : new Color(0.5f, 0.48f, 0.55f);
        var nameLbl = new Label { Text = spell.SpellName };
        nameLbl.AddThemeFontSizeOverride("font_size", 13);
        nameLbl.AddThemeColorOverride("font_color", nameColor);
        nameLbl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row1.AddChild(nameLbl);

        var costLbl = new Label { Text = $"{spell.ManaCost}法力" };
        costLbl.AddThemeFontSizeOverride("font_size", 11);
        costLbl.AddThemeColorOverride("font_color", currentMana >= spell.ManaCost
            ? new Color(0.4f, 0.6f, 1f) : new Color(0.8f, 0.3f, 0.3f));
        row1.AddChild(costLbl);

        // 第二行：描述
        var descLbl = new Label { Text = spell.Description };
        descLbl.AddThemeFontSizeOverride("font_size", 10);
        descLbl.AddThemeColorOverride("font_color", new Color(0.6f, 0.58f, 0.55f));
        descLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        vbox.AddChild(descLbl);

        // 第三行：射程 + 冷却
        var row3 = new HBoxContainer();
        row3.AddThemeConstantOverride("separation", 10);
        vbox.AddChild(row3);

        var rangeLbl = new Label { Text = $"射程 {spell.RangeCells}" };
        rangeLbl.AddThemeFontSizeOverride("font_size", 10);
        rangeLbl.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
        row3.AddChild(rangeLbl);

        if (onCooldown)
        {
            int cd = _caster.Data!.SpellCooldowns[spell.SpellId].AsInt32();
            var cdLbl = new Label { Text = $"冷却 {cd}回合" };
            cdLbl.AddThemeFontSizeOverride("font_size", 10);
            cdLbl.AddThemeColorOverride("font_color", new Color(0.8f, 0.4f, 0.2f));
            row3.AddChild(cdLbl);
        }

        // 点击选择
        if (canCast)
        {
            var captured = spell;
            entry.GuiInput += (ev) =>
            {
                if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                {
                    Visible = false;
                    EmitSignal(SignalName.SpellChosen, captured);
                }
            };
        }
    }
}
