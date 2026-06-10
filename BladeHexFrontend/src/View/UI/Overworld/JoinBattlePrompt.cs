using System;
using Godot;
using BladeHex.Strategic;
using BladeHex.Localization;

namespace BladeHex.View.UI.Overworld;

/// <summary>
/// 参战提示 UI 浮窗
/// </summary>
public partial class JoinBattlePrompt : PanelContainer
{
    public event Action<JoinOpportunity, bool>? JoinSelected; // true = 加入攻方, false = 加入守方
    public event Action? LeaveSelected;

    private JoinOpportunity? _currentOpportunity;

    private Label _titleLabel = null!;
    private Label _descLabel = null!;
    private Button _joinAttackerBtn = null!;
    private Button _joinDefenderBtn = null!;
    private Button _leaveBtn = null!;

    public override void _Ready()
    {
        // 1. 设置扁平毛玻璃风格面板样式
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.12f, 0.9f), // 深海蓝毛玻璃半透明
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.22f, 0.35f, 0.6f, 0.6f), // 科技蓝边框
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ShadowColor = new Color(0, 0, 0, 0.35f),
            ShadowSize = 8,
            ContentMarginLeft = 20,
            ContentMarginRight = 20,
            ContentMarginTop = 15,
            ContentMarginBottom = 15
        };
        AddThemeStyleboxOverride("panel", style);

        // 2. 居中并固定在屏幕上方
        CustomMinimumSize = new Vector2(400, 130);
        SetAnchorsPreset(LayoutPreset.CenterTop);
        GrowHorizontal = GrowDirection.Both;
        Position = new Vector2(Position.X - 200, 20); // 浮动偏置

        // 3. 构建布局
        var vbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(vbox);

        _titleLabel = new Label
        {
            Text = L10n.Tr("JOIN_BATTLE_TITLE"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.3f)); // 耀眼金
        _titleLabel.AddThemeFontSizeOverride("font_size", 18);
        vbox.AddChild(_titleLabel);

        _descLabel = new Label
        {
            Text = L10n.Tr("JOIN_BATTLE_DESC_SIEGE", "", "", "", ""),
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _descLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
        _descLabel.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(_descLabel);

        // 按钮栏
        var hbox = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        vbox.AddChild(hbox);

        _joinAttackerBtn = new Button { Text = L10n.Tr("JOIN_BATTLE_ATTACKER") };
        _joinAttackerBtn.AddThemeStyleboxOverride("normal", CreateButtonStyle(new Color(0.6f, 0.2f, 0.2f, 0.8f))); // 战红色
        _joinAttackerBtn.Pressed += () => OnJoinPressed(true);
        hbox.AddChild(_joinAttackerBtn);

        _joinDefenderBtn = new Button { Text = L10n.Tr("JOIN_BATTLE_DEFENDER") };
        _joinDefenderBtn.AddThemeStyleboxOverride("normal", CreateButtonStyle(new Color(0.2f, 0.5f, 0.3f, 0.8f))); // 绿林色
        _joinDefenderBtn.Pressed += () => OnJoinPressed(false);
        hbox.AddChild(_joinDefenderBtn);

        _leaveBtn = new Button { Text = L10n.Tr("JOIN_BATTLE_LEAVE") };
        _leaveBtn.AddThemeStyleboxOverride("normal", CreateButtonStyle(new Color(0.3f, 0.3f, 0.35f, 0.8f)));
        _leaveBtn.Pressed += OnLeavePressed;
        hbox.AddChild(_leaveBtn);

        Visible = false;
    }

    private StyleBoxFlat CreateButtonStyle(Color bg)
    {
        return new StyleBoxFlat
        {
            BgColor = bg,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 6,
            ContentMarginBottom = 6
        };
    }

    /// <summary>
    /// 显示参战提示（支持 NvN 多方战场）
    /// </summary>
    public void ShowPrompt(JoinOpportunity opp)
    {
        _currentOpportunity = opp;
        
        if (opp.Type == WarBattleType.Siege)
        {
            _titleLabel.Text = L10n.Tr("JOIN_BATTLE_TITLE");
            string desc = opp.DefenderPoi != null
                ? L10n.Tr("JOIN_BATTLE_DESC_SIEGE", opp.Attacker.EntityName, opp.Attacker.Faction, opp.DefenderPoi.PoiName, opp.DefenderPoi.OwningFaction)
                : "";
            if (opp.Attackers.Count > 1)
                desc += $"\n{L10n.Tr("JOIN_BATTLE_NVN_ATTACKERS", opp.Attackers.Count, opp.AttackerTotalPower)}";
            if (opp.DefenderPoi != null)
                desc += $"\n{L10n.Tr("JOIN_BATTLE_NVN_DEFENDERS_GARRISON", opp.DefenderPoi.GarrisonCurrent, opp.DefenderPoi.GarrisonMax)}";
            _descLabel.Text = desc;
            _joinAttackerBtn.Text = L10n.Tr("JOIN_BATTLE_ATTACKER");
            _joinDefenderBtn.Visible = true;
        }
        else if (opp.Type == WarBattleType.FieldBattle)
        {
            _titleLabel.Text = L10n.Tr("JOIN_BATTLE_TITLE");
            string desc = opp.DefenderEntity != null
                ? L10n.Tr("JOIN_BATTLE_DESC_FIELD", opp.Attacker.EntityName, opp.Attacker.Faction, opp.DefenderEntity.EntityName, opp.DefenderEntity.Faction)
                : "";
            // NvN 汇总信息
            if (opp.Attackers.Count > 1)
                desc += $"\n{L10n.Tr("JOIN_BATTLE_NVN_ATTACKERS", opp.Attackers.Count, opp.AttackerTotalPower)}";
            if (opp.Defenders.Count > 1)
                desc += $"\n{L10n.Tr("JOIN_BATTLE_NVN_DEFENDERS", opp.Defenders.Count, opp.DefenderTotalPower)}";
            _descLabel.Text = desc;
            _joinAttackerBtn.Text = L10n.Tr("JOIN_BATTLE_ATTACKER");
            _joinDefenderBtn.Visible = true;
        }
        else if (opp.Type == WarBattleType.ArmyJoin)
        {
            _titleLabel.Text = L10n.Tr("JOIN_ARMY_TITLE");
            _descLabel.Text = L10n.Tr("JOIN_ARMY_DESC", opp.Attacker.EntityName, opp.ArmyRef?.TargetPoiName ?? L10n.Tr("COMMON_UNKNOWN"));
            _joinAttackerBtn.Text = L10n.Tr("JOIN_ARMY_BUTTON");
            _joinDefenderBtn.Visible = false;
        }

        Visible = true;
    }

    /// <summary>
    /// 关闭提示
    /// </summary>
    public void HidePrompt()
    {
        Visible = false;
        _currentOpportunity = null;
        _joinDefenderBtn.Visible = true;
        _joinAttackerBtn.Text = L10n.Tr("JOIN_BATTLE_ATTACKER");
    }

    private void OnJoinPressed(bool joinAttacker)
    {
        if (_currentOpportunity != null)
        {
            JoinSelected?.Invoke(_currentOpportunity, joinAttacker);
            HidePrompt();
        }
    }

    private void OnLeavePressed()
    {
        LeaveSelected?.Invoke();
        HidePrompt();
    }
}
