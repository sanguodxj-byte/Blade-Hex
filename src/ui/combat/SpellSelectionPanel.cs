using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Combat;

namespace BladeHex.UI.Combat;

/// <summary>
/// 法术选择面板 — 显示施法者可用法术、魔力、冷却状态
/// 迁移自 GDScript SpellSelectionPanel.gd
/// </summary>
public partial class SpellSelectionPanel : Control
{
    [Signal] public delegate void SpellSelectedEventHandler(SpellData spell);
    [Signal] public delegate void SpellCancelledEventHandler();

    private ProgressBar _manaBar = null!;
    private Label _manaLabel = null!;
    private GridContainer _spellGrid = null!;
    private readonly List<Button> _spellButtons = new();
    
    private Unit? _caster;
    private SpellManager? _spellManager;

    public override void _Ready()
    {
        SetupUI();
        Visible = false;
    }

    private void SetupUI()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.RightWide);
        OffsetLeft = -280;
        OffsetTop = 60;
        OffsetBottom = -140;

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", UITheme.Instance.MakePanelStyle(new Color(0.08f, 0.08f, 0.12f, 0.95f), new Color(0.4f, 0.35f, 0.6f), 2, 6));
        AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vbox);

        var title = new Label
        {
            Text = "— 选择法术 —",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", new Color(0.7f, 0.6f, 1.0f));
        vbox.AddChild(title);

        var manaHbox = new HBoxContainer();
        vbox.AddChild(manaHbox);

        var manaTitle = new Label { Text = "魔力:", HorizontalAlignment = HorizontalAlignment.Left };
        manaTitle.AddThemeColorOverride("font_color", new Color(0.5f, 0.7f, 1.0f));
        manaHbox.AddChild(manaTitle);

        _manaBar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(150, 16),
            ShowPercentage = false
        };
        UITheme.Instance.ApplyBarTheme(_manaBar, new Color(0.3f, 0.5f, 1.0f), new Color(0.15f, 0.15f, 0.2f));
        manaHbox.AddChild(_manaBar);

        _manaLabel = new Label { Text = "0/0" };
        _manaLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.7f, 1.0f));
        manaHbox.AddChild(_manaLabel);

        _spellGrid = new GridContainer { Columns = 3 };
        _spellGrid.AddThemeConstantOverride("h_separation", 6);
        _spellGrid.AddThemeConstantOverride("v_separation", 6);
        vbox.AddChild(_spellGrid);

        var cancelBtn = new Button { Text = "取消 (Esc)", CustomMinimumSize = new Vector2(100, 32) };
        cancelBtn.Pressed += () => { EmitSignal(SignalName.SpellCancelled); Visible = false; };
        vbox.AddChild(cancelBtn);
    }

    public void Open(Unit caster, SpellManager spellManager)
    {
        _caster = caster;
        _spellManager = spellManager;

        foreach (var btn in _spellButtons) btn.QueueFree();
        _spellButtons.Clear();

        if (caster.Data == null) return;

        int maxMana = spellManager.GetMaxMana(caster);
        _manaBar.MaxValue = maxMana;
        _manaBar.Value = caster.Data.CurrentMana;
        _manaLabel.Text = $"{caster.Data.CurrentMana}/{maxMana}";

        foreach (var spell in caster.Data.KnownSpells)
        {
            var btn = new Button
            {
                CustomMinimumSize = new Vector2(80, 60),
                Text = $"{spell.SpellName}\n({spell.ManaCost})"
            };

            // 冷却检查
            int cooldown = 0;
            if (caster.Data.SpellCooldowns.ContainsKey(spell.SpellId))
            {
                cooldown = (int)caster.Data.SpellCooldowns[spell.SpellId];
            }

            if (cooldown > 0)
            {
                btn.Text += $"\n冷却:{cooldown}";
                btn.Disabled = true;
                btn.Modulate = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            }
            else if (caster.Data.CurrentMana < spell.ManaCost)
            {
                btn.Disabled = true;
                btn.Modulate = new Color(0.4f, 0.4f, 0.5f, 0.6f);
            }
            else
            {
                Color color = GetSchoolColor(spell.spellSchool);
                btn.Modulate = color;
            }

            btn.Pressed += () => OnSpellClicked(spell);
            btn.TooltipText = $"{spell.SpellName} ({spell.GetTierName()} {spell.GetSchoolName()})\n{spell.Description}\n魔力: {spell.ManaCost} | 冷却: {spell.CooldownTurns}回合";

            _spellGrid.AddChild(btn);
            _spellButtons.Add(btn);
        }

        Visible = true;
    }

    private void OnSpellClicked(SpellData spell)
    {
        EmitSignal(SignalName.SpellSelected, spell);
        Visible = false;
    }

    private Color GetSchoolColor(SpellData.SpellSchool school)
    {
        return school switch
        {
            SpellData.SpellSchool.Evocation => new Color(1.0f, 0.4f, 0.2f),
            SpellData.SpellSchool.Abjuration => new Color(0.4f, 0.6f, 1.0f),
            _ => new Color(0.75f, 0.75f, 0.75f)
        };
    }
}
