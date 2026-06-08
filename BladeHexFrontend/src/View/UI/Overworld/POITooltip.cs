// POITooltip.cs
// 大地图 POI 悬浮详情 — 鼠标悬停 POI 时显示名称、类型、繁荣度、驻军等信息
using Godot;
using BladeHex.Strategic;
using BladeHex.UI.Common;
using BladeHex.Localization;

namespace BladeHex.View.UI.Overworld;

/// <summary>
/// POI 悬浮详情面板 — 悬停大地图 POI 标记时弹出
/// 显示：名称、类型、势力、繁荣度、驻军、设施、威胁等级
/// </summary>
[GlobalClass]
public partial class POITooltip : FloatingPanel
{
    private Label _nameLabel = null!;
    private Label _typeLabel = null!;
    private Label _factionLabel = null!;
    private Label _prosperityLabel = null!;
    private Label _garrisonLabel = null!;
    private Label _facilityLabel = null!;
    private Label _threatLabel = null!;
    private Label _statusLabel = null!;

    // ============================================================================
    // FloatingPanel 配置
    // ============================================================================

    // POITooltip 使用基类默认颜色（从 UITheme 读取），无特殊 override

    // ============================================================================
    // 构建内容
    // ============================================================================

    protected override void BuildContent()
    {
        _nameLabel = MakeTitleLabel("", 18);
        Content.AddChild(_nameLabel);

        _typeLabel = MakeStatLabel("");
        Content.AddChild(_typeLabel);

        _factionLabel = MakeStatLabel("");
        Content.AddChild(_factionLabel);

        Content.AddChild(MakeSeparator());

        _prosperityLabel = MakeBodyLabel("");
        Content.AddChild(_prosperityLabel);

        _garrisonLabel = MakeBodyLabel("");
        Content.AddChild(_garrisonLabel);

        _facilityLabel = MakeStatLabel("");
        Content.AddChild(_facilityLabel);

        Content.AddChild(MakeSeparator());

        _threatLabel = MakeStatLabel("");
        Content.AddChild(_threatLabel);

        _statusLabel = MakeMutedLabel("");
        Content.AddChild(_statusLabel);
    }

    // ============================================================================
    // 公共 API
    // ============================================================================

    /// <summary>显示 POI 详情</summary>
    public void ShowForPOI(OverworldPOI poi, Vector2 screenPos, System.Collections.Generic.List<NationConfig>? nations = null)
    {
        if (poi == null) return;

        _nameLabel.Text = poi.PoiName;
        _typeLabel.Text = poi.GetTypeName();

        // 势力名称翻译（利用 NationConfig 的 DisplayName 避开暴露代码翻译键）
        string factionDisplay = poi.OwningFaction;
        if (nations != null && !string.IsNullOrEmpty(poi.OwningFaction))
        {
            var nation = nations.Find(n => n.Id == poi.OwningFaction);
            if (nation != null)
                factionDisplay = nation.DisplayName;
        }

        factionDisplay = factionDisplay switch
        {
            "neutral" => L10n.Tr("FACTION_NEUTRAL"),
            "player" => L10n.Tr("FACTION_PLAYER"),
            _ => factionDisplay,
        };
        _factionLabel.Text = L10n.Tr("POI_FACTION", factionDisplay);
        _factionLabel.AddThemeColorOverride("font_color", poi.OwningFaction switch
        {
            "player" => new Color(0.3f, 0.85f, 0.4f),
            "neutral" => new Color(0.7f, 0.7f, 0.7f),
            _ => new Color(0.9f, 0.5f, 0.4f),
        });

        // 繁荣度
        _prosperityLabel.Text = L10n.Tr("POI_PROSPERITY", poi.Prosperity);
        _prosperityLabel.AddThemeColorOverride("font_color", poi.Prosperity switch
        {
            >= 70 => new Color(0.3f, 0.9f, 0.4f),
            >= 40 => new Color(0.9f, 0.8f, 0.4f),
            _ => new Color(0.9f, 0.4f, 0.3f),
        });

        // 驻军
        if (poi.GarrisonMax > 0)
        {
            _garrisonLabel.Text = L10n.Tr("POI_GARRISON", poi.GarrisonCurrent, poi.GarrisonMax);
            _garrisonLabel.Visible = true;
        }
        else
        {
            _garrisonLabel.Visible = false;
        }

        // 设施
        var facilities = new System.Collections.Generic.List<string>();
        if (poi.HasTavern) facilities.Add(L10n.Tr("FACILITY_TAVERN"));
        if (poi.HasShop) facilities.Add(L10n.Tr("FACILITY_SHOP"));
        if (poi.HasBlacksmith) facilities.Add(L10n.Tr("FACILITY_BLACKSMITH"));
        if (poi.HasBarracks) facilities.Add(L10n.Tr("FACILITY_BARRACKS"));
        if (facilities.Count > 0)
        {
            _facilityLabel.Text = L10n.Tr("POI_FACILITIES", string.Join(" / ", facilities));
            _facilityLabel.Visible = true;
        }
        else
        {
            _facilityLabel.Visible = false;
        }

        // 威胁等级
        if (poi.ThreatLevel > 0)
        {
            _threatLabel.Text = L10n.Tr("POI_THREAT_LEVEL", poi.ThreatLevel);
            _threatLabel.Visible = true;
        }
        else
        {
            _threatLabel.Visible = false;
        }

        // 状态
        var statuses = new System.Collections.Generic.List<string>();
        if (poi.IsUnderSiege) statuses.Add(L10n.Tr("POI_STATUS_UNDER_SIEGE"));
        if (poi.NeedsReinforcement()) statuses.Add(L10n.Tr("POI_STATUS_NEEDS_REINFORCEMENT"));
        if (statuses.Count > 0)
        {
            _statusLabel.Text = string.Join(" | ", statuses);
            _statusLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.4f, 0.3f));
            _statusLabel.Visible = true;
        }
        else
        {
            _statusLabel.Visible = false;
        }

        ShowAt(screenPos);
    }
}
