// EntityTooltip.cs
// 大地图实体悬浮详情 — 鼠标悬停地图实体（敌军/商队/冒险者等）时显示信息
using Godot;
using BladeHex.Strategic;
using BladeHex.UI.Common;

namespace BladeHex.View.UI.Overworld;

/// <summary>
/// 地图实体悬浮详情面板 — 悬停大地图移动实体时弹出
/// 显示：名称、类型、势力、战力、AI状态、敌对关系
/// </summary>
[GlobalClass]
public partial class EntityTooltip : FloatingPanel
{
    private Label _nameLabel = null!;
    private Label _typeLabel = null!;
    private Label _factionLabel = null!;
    private Label _powerLabel = null!;
    private Label _sizeLabel = null!;
    private Label _stateLabel = null!;
    private Label _hostileLabel = null!;

    // ============================================================================
    // FloatingPanel 配置
    // ============================================================================

    protected override Color PanelBgColor => new(0.06f, 0.05f, 0.08f, 0.95f);
    protected override Color PanelBorderColor => new(0.5f, 0.35f, 0.3f, 0.8f);
    protected override int PanelBorderWidth => 2;
    protected override float MinPanelWidth => 180f;
    protected override Vector2 MouseOffset => new(20, 10);

    // ============================================================================
    // 构建内容
    // ============================================================================

    protected override void BuildContent()
    {
        _nameLabel = MakeTitleLabel("", 16);
        Content.AddChild(_nameLabel);

        _typeLabel = MakeLabel("", 13, new Color(0.7f, 0.7f, 0.6f));
        Content.AddChild(_typeLabel);

        _factionLabel = MakeLabel("", 12, new Color(0.6f, 0.7f, 0.9f));
        Content.AddChild(_factionLabel);

        Content.AddChild(MakeSeparator());

        _powerLabel = MakeLabel("", 13, new Color(0.9f, 0.75f, 0.4f));
        Content.AddChild(_powerLabel);

        _sizeLabel = MakeLabel("", 12, new Color(0.8f, 0.8f, 0.8f));
        Content.AddChild(_sizeLabel);

        _stateLabel = MakeLabel("", 12, new Color(0.7f, 0.7f, 0.7f));
        Content.AddChild(_stateLabel);

        Content.AddChild(MakeSeparator());

        _hostileLabel = MakeLabel("", 13, new Color(0.9f, 0.4f, 0.3f));
        Content.AddChild(_hostileLabel);
    }

    // ============================================================================
    // 公共 API
    // ============================================================================

    /// <summary>显示实体详情</summary>
    public void ShowForEntity(OverworldEntity entity, Vector2 screenPos)
    {
        if (entity == null) return;

        // 名称（使用实体显示颜色）
        _nameLabel.Text = entity.EntityName;
        _nameLabel.AddThemeColorOverride("font_color", entity.GetDisplayColor());

        // 类型
        _typeLabel.Text = entity.GetTypeName();

        // 势力
        string factionDisplay = entity.Faction switch
        {
            "neutral" => "中立",
            "player" => "己方",
            _ => entity.Faction,
        };
        _factionLabel.Text = $"势力: {factionDisplay}";

        // 战力
        string powerDesc = entity.CombatPower switch
        {
            >= 100 => "极强",
            >= 60 => "强大",
            >= 30 => "中等",
            >= 10 => "较弱",
            _ => "微弱",
        };
        _powerLabel.Text = $"战力: {powerDesc} (Lv.{entity.PartyLevel})";
        _powerLabel.AddThemeColorOverride("font_color", entity.CombatPower switch
        {
            >= 100 => new Color(0.9f, 0.3f, 0.8f),
            >= 60 => new Color(0.9f, 0.4f, 0.3f),
            >= 30 => new Color(0.9f, 0.8f, 0.4f),
            _ => new Color(0.6f, 0.8f, 0.6f),
        });

        // 队伍规模
        _sizeLabel.Text = $"规模: {entity.PartySize}人";

        // AI 状态
        string aiState = entity.GetStateText();
        _stateLabel.Text = $"状态: {aiState}";

        // 敌对关系
        if (entity.IsHostileToPlayer)
        {
            _hostileLabel.Text = "⚔ 敌对";
            _hostileLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.35f, 0.3f));
            _hostileLabel.Visible = true;
        }
        else
        {
            _hostileLabel.Text = "◆ 友好";
            _hostileLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.85f, 0.5f));
            _hostileLabel.Visible = true;
        }

        ShowAt(screenPos);
    }
}
