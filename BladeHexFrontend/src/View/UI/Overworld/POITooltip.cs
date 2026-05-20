// POITooltip.cs
// 大地图 POI 悬浮详情 — 鼠标悬停 POI 时显示名称、类型、繁荣度、驻军等信息
using Godot;
using BladeHex.Strategic;
using BladeHex.UI.Common;

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

    protected override Color PanelBgColor => new(0.06f, 0.06f, 0.08f, 0.95f);
    protected override Color PanelBorderColor => new(0.4f, 0.5f, 0.3f, 0.8f);
    protected override int PanelBorderWidth => 2;
    protected override float MinPanelWidth => 200f;
    protected override Vector2 MouseOffset => new(20, 10);

    // ============================================================================
    // 构建内容
    // ============================================================================

    protected override void BuildContent()
    {
        _nameLabel = MakeTitleLabel("", 18);
        Content.AddChild(_nameLabel);

        _typeLabel = MakeLabel("", 13, new Color(0.7f, 0.7f, 0.6f));
        Content.AddChild(_typeLabel);

        _factionLabel = MakeLabel("", 12, new Color(0.6f, 0.7f, 0.9f));
        Content.AddChild(_factionLabel);

        Content.AddChild(MakeSeparator());

        _prosperityLabel = MakeLabel("", 13, new Color(0.9f, 0.8f, 0.4f));
        Content.AddChild(_prosperityLabel);

        _garrisonLabel = MakeLabel("", 13, new Color(0.6f, 0.75f, 0.9f));
        Content.AddChild(_garrisonLabel);

        _facilityLabel = MakeLabel("", 12, new Color(0.7f, 0.85f, 0.7f));
        Content.AddChild(_facilityLabel);

        Content.AddChild(MakeSeparator());

        _threatLabel = MakeLabel("", 12, new Color(0.9f, 0.5f, 0.4f));
        Content.AddChild(_threatLabel);

        _statusLabel = MakeLabel("", 11, new Color(0.6f, 0.6f, 0.6f));
        Content.AddChild(_statusLabel);
    }

    // ============================================================================
    // 公共 API
    // ============================================================================

    /// <summary>显示 POI 详情</summary>
    public void ShowForPOI(OverworldPOI poi, Vector2 screenPos)
    {
        if (poi == null) return;

        _nameLabel.Text = poi.PoiName;
        _typeLabel.Text = poi.GetTypeName();

        // 势力
        string factionDisplay = poi.OwningFaction switch
        {
            "neutral" => "中立",
            "player" => "己方",
            _ => poi.OwningFaction,
        };
        _factionLabel.Text = $"势力: {factionDisplay}";
        _factionLabel.AddThemeColorOverride("font_color", poi.OwningFaction switch
        {
            "player" => new Color(0.3f, 0.85f, 0.4f),
            "neutral" => new Color(0.7f, 0.7f, 0.7f),
            _ => new Color(0.9f, 0.5f, 0.4f),
        });

        // 繁荣度
        _prosperityLabel.Text = $"繁荣: {poi.Prosperity}";
        _prosperityLabel.AddThemeColorOverride("font_color", poi.Prosperity switch
        {
            >= 70 => new Color(0.3f, 0.9f, 0.4f),
            >= 40 => new Color(0.9f, 0.8f, 0.4f),
            _ => new Color(0.9f, 0.4f, 0.3f),
        });

        // 驻军
        if (poi.GarrisonMax > 0)
        {
            _garrisonLabel.Text = $"驻军: {poi.GarrisonCurrent}/{poi.GarrisonMax}";
            _garrisonLabel.Visible = true;
        }
        else
        {
            _garrisonLabel.Visible = false;
        }

        // 设施
        var facilities = new System.Collections.Generic.List<string>();
        if (poi.HasTavern) facilities.Add("酒馆");
        if (poi.HasShop) facilities.Add("商店");
        if (poi.HasBlacksmith) facilities.Add("铁匠");
        if (poi.HasBarracks) facilities.Add("兵营");
        if (facilities.Count > 0)
        {
            _facilityLabel.Text = $"设施: {string.Join(" / ", facilities)}";
            _facilityLabel.Visible = true;
        }
        else
        {
            _facilityLabel.Visible = false;
        }

        // 威胁等级
        if (poi.ThreatLevel > 0)
        {
            _threatLabel.Text = $"威胁: Lv.{poi.ThreatLevel}";
            _threatLabel.Visible = true;
        }
        else
        {
            _threatLabel.Visible = false;
        }

        // 状态
        var statuses = new System.Collections.Generic.List<string>();
        if (poi.IsUnderSiege) statuses.Add("被围攻!");
        if (poi.NeedsReinforcement()) statuses.Add("需要援助");
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
