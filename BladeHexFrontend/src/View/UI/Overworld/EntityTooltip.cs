// EntityTooltip.cs
// 大地图实体悬浮详情 — 鼠标悬停地图实体（敌军/商队/冒险者等）时显示信息
using Godot;
using BladeHex.Strategic;
using BladeHex.UI.Common;
using BladeHex.Localization;

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
    private Label _speedLabel = null!;
    private Label _behaviorLabel = null!;
    private Label _hostileLabel = null!;

    // ============================================================================
    // FloatingPanel 配置
    // ============================================================================

    protected override float MinPanelWidth => 180f;
    protected override int PanelContentMargin => 6;  // 减小内容边距（原 10）

    // ============================================================================
    // 构建内容
    // ============================================================================

    protected override void BuildContent()
    {
        // 减小 VBox 间距，使布局更紧凑
        Content.AddThemeConstantOverride("separation", 2);

        _nameLabel = MakeTitleLabel("", 16);
        Content.AddChild(_nameLabel);

        _typeLabel = MakeStatLabel("");
        Content.AddChild(_typeLabel);

        _factionLabel = MakeStatLabel("");
        Content.AddChild(_factionLabel);

        Content.AddChild(MakeSeparator());

        _powerLabel = MakeBodyLabel("");
        Content.AddChild(_powerLabel);

        _sizeLabel = MakeStatLabel("");
        Content.AddChild(_sizeLabel);

        _stateLabel = MakeStatLabel("");
        Content.AddChild(_stateLabel);

        _speedLabel = MakeLabel("", 12, new Color(0.7f, 0.85f, 0.7f));
        Content.AddChild(_speedLabel);

        _behaviorLabel = MakeLabel("", 12, new Color(0.75f, 0.8f, 0.95f));
        Content.AddChild(_behaviorLabel);

        Content.AddChild(MakeSeparator());

        _hostileLabel = MakeBodyLabel("");
        Content.AddChild(_hostileLabel);
    }

    // ============================================================================
    // 公共 API
    // ============================================================================

    /// <summary>显示实体详情</summary>
    public void ShowForEntity(OverworldEntity entity, Vector2 screenPos,
        System.Collections.Generic.List<NationConfig>? nations = null,
        BladeHex.Strategic.SpeedBreakdown? speedBreakdown = null)
    {
        if (entity == null) return;

        // 名称（使用实体显示颜色）
        _nameLabel.Text = entity.EntityName;
        _nameLabel.AddThemeColorOverride("font_color", entity.GetDisplayColor());

        // 类型
        _typeLabel.Text = entity.GetTypeName();

        // 势力名称翻译（利用 NationConfig 的 DisplayName 避开暴露代码翻译键）
        string factionDisplay = entity.Faction;
        if (nations != null && !string.IsNullOrEmpty(entity.Faction))
        {
            var nation = nations.Find(n => n.Id == entity.Faction);
            if (nation != null)
                factionDisplay = nation.DisplayName;
        }

        factionDisplay = factionDisplay switch
        {
            "neutral" => L10n.Tr("FACTION_NEUTRAL"),
            "player" => L10n.Tr("FACTION_PLAYER"),
            _ => factionDisplay,
        };
        _factionLabel.Text = L10n.Tr("ENTITY_FACTION", factionDisplay);
        _factionLabel.AddThemeColorOverride("font_color", entity.Faction switch
        {
            "player" => new Color(0.3f, 0.85f, 0.4f),
            "neutral" => new Color(0.7f, 0.7f, 0.7f),
            _ => new Color(0.9f, 0.5f, 0.4f),
        });

        // 战力
        string powerDesc = entity.CombatPower switch
        {
            >= 100 => L10n.Tr("POWER_EXTREME"),
            >= 60 => L10n.Tr("POWER_STRONG"),
            >= 30 => L10n.Tr("POWER_MEDIUM"),
            >= 10 => L10n.Tr("POWER_WEAK"),
            _ => L10n.Tr("POWER_TINY"),
        };
        _powerLabel.Text = L10n.Tr("ENTITY_POWER_LEVEL", powerDesc, entity.PartyLevel);
        _powerLabel.AddThemeColorOverride("font_color", entity.CombatPower switch
        {
            >= 100 => new Color(0.9f, 0.3f, 0.8f),
            >= 60 => new Color(0.9f, 0.4f, 0.3f),
            >= 30 => new Color(0.9f, 0.8f, 0.4f),
            _ => new Color(0.6f, 0.8f, 0.6f),
        });

        // 队伍规模（敌军人数显示）
        if (entity.IsHostileToPlayer)
        {
            _sizeLabel.Text = L10n.Tr("ENTITY_ENEMY_SIZE", entity.PartySize);
        }
        else
        {
            _sizeLabel.Text = L10n.Tr("ENTITY_PARTY_SIZE", entity.PartySize);
        }

        // AI 状态
        string aiState = entity.GetStateText();
        _stateLabel.Text = L10n.Tr("ENTITY_STATE", aiState);

        // 移速 — 优先使用 EntitySpeedCalculator 计算的分解值，回退到原始 MoveSpeed
        if (speedBreakdown.HasValue)
        {
            var bd = speedBreakdown.Value;
            _speedLabel.Text = L10n.Tr("ENTITY_SPEED_PX", bd.Final.ToString("F0"));
            _speedLabel.TooltipText = L10n.Tr("TOOLTIP_BASE_SPEED", bd.Base.ToString("F0")) + "\n" +
                L10n.Tr("TOOLTIP_TERRAIN_FACTOR", bd.TerrainName, bd.TerrainFactor.ToString("F2")) + "\n" +
                L10n.Tr("TOOLTIP_ZOC_FACTOR", bd.ZocFactor < 1.0f ? L10n.Tr("STATUS_SLOWED", bd.ZocFactor.ToString("F2")) : L10n.Tr("STATUS_NORMAL_FACTOR")) + "\n" +
                L10n.Tr("TOOLTIP_STATE", bd.StateName) + (bd.ChaseMultiplier > 1.0f ? "\n" + L10n.Tr("TOOLTIP_CHASE_SPEED", bd.ChaseMultiplier.ToString("F2")) : "");
        }
        else
        {
            _speedLabel.Text = L10n.Tr("ENTITY_SPEED", entity.MoveSpeed.ToString("F0"));
            _speedLabel.TooltipText = "";
        }

        // AI 意图/目标
        string behaviorText = entity.LastIntentSummary;
        if (string.IsNullOrEmpty(behaviorText))
        {
            if (entity.CurrentAIState == OverworldEntity.AIState.Chasing && entity.ChaseTarget != null && GodotObject.IsInstanceValid(entity.ChaseTarget))
                behaviorText = L10n.Tr("ENTITY_CHASE_TARGET", entity.ChaseTarget.EntityName);
            else if (entity.CurrentAIState == OverworldEntity.AIState.Fleeing && entity.CurrentTacticalTarget != null && GodotObject.IsInstanceValid(entity.CurrentTacticalTarget))
                behaviorText = L10n.Tr("ENTITY_FLEE_THREAT", entity.CurrentTacticalTarget.EntityName);
        }
        _behaviorLabel.Text = string.IsNullOrEmpty(behaviorText) ? L10n.Tr("ENTITY_INTENT_NORMAL") : L10n.Tr("ENTITY_INTENT", behaviorText);
        _behaviorLabel.Visible = true;

        // 敌对关系
        if (entity.IsHostileToPlayer)
        {
            _hostileLabel.Text = L10n.Tr("ENTITY_HOSTILE");
            _hostileLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.35f, 0.3f));
            _hostileLabel.Visible = true;
        }
        else
        {
            _hostileLabel.Text = L10n.Tr("ENTITY_FRIENDLY");
            _hostileLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.85f, 0.5f));
            _hostileLabel.Visible = true;
        }

        ShowAt(screenPos);
    }
}
