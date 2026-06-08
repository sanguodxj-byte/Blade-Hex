// TownPanel.cs
// 城镇面板 — 进入城镇/村庄时显示设施列表
// 使用统一布局基类：插画 → 信息 → 描述 → 设施列表 → 离开
using Godot;
using BladeHex.Strategic;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class TownPanel : POIPanelBase
{
    // ============================================================================
    // 信号
    // ============================================================================

    [Signal] public delegate void FacilitySelectedEventHandler(int facilityType);
    [Signal] public delegate void LeaveTownEventHandler();

    // ============================================================================
    // 字段
    // ============================================================================

    private OverworldTown? _currentTown;

    // ============================================================================
    // 数据填充
    // ============================================================================

    protected override Color GetIllustrationColor() => new(0.06f, 0.08f, 0.12f, 1.0f);

    protected override string GetIllustrationText()
    {
        if (_currentTown == null) return "[ 城镇 ]";
        string type = _currentTown.TownType switch
        {
            "village" => "村庄",
            "port" => "港口城市",
            "castle" => "城堡",
            "mine" => "矿场",
            _ => "城镇"
        };
        return $"[ {type} ]";
    }

    protected override string? GetIllustrationPath()
        => _currentTown != null ? POIIllustrationResolver.GetTownIllustration(_currentTown.TownType) : null;

    protected override string GetPanelTitle() => "";

    protected override string GetInfoText()
    {
        if (_currentTown == null) return "";
        string typeText = _currentTown.TownType switch
        {
            "village" => "村庄",
            "port" => "港口城市",
            "castle" => "城堡",
            "mine" => "矿场",
            _ => "城镇"
        };
        return $"{_currentTown.TownName} | {typeText} | 繁荣: {_currentTown.Prosperity}";
    }

    protected override string GetDescriptionText()
    {
        if (_currentTown == null) return "";
        // 使用 DescriptionProvider（三因素：类型x繁荣度x种族）
        var poiType = _currentTown.TownType switch
        {
            "village" => Strategic.OverworldPOI.POIType.Village,
            "castle" => Strategic.OverworldPOI.POIType.Castle,
            "mine" => Strategic.OverworldPOI.POIType.Mine,
            _ => Strategic.OverworldPOI.POIType.Town,
        };
        var ctx = Strategic.DescriptionContext.Default;
        ctx.PoiName = _currentTown.TownName;
        ctx.Prosperity = _currentTown.Prosperity;
        ctx.Garrison = _currentTown.Garrison;
        ctx.RaceStyle = !string.IsNullOrEmpty(_currentTown.Faction) ? _currentTown.Faction : "Human";
        return Strategic.DescriptionProvider.GetPoiDescription(poiType, ctx);
    }

    protected override string GetLeaveButtonText() => "离开城镇";

    protected override void PopulateActions(VBoxContainer container)
    {
        if (_currentTown == null) return;

        if (_currentTown.Facilities.Count == 0)
        {
            if (_currentTown.TownType == "village")
                _currentTown.SetupVillageFacilities();
            else
                _currentTown.SetupDefaultFacilities();
        }

        // 浣跨敤 GridContainer 杩涜缃戞牸绱у噾鍖栵紝姣忚涓や釜鎸夐挳
        var grid = new GridContainer();
        grid.Columns = 3;
        grid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        container.AddChild(grid);

        foreach (var facility in _currentTown.Facilities)
        {
            if (!facility.IsAvailable) continue;

            // 绉婚櫎鍚庣画鎻忚堪锛屼粎淇濈暀閰掗/绔炴妧鍦虹瓑鏍稿績鍚嶇О
            string btnText = facility.FacilityName;
            int dashIndex = btnText.IndexOf("-");
            if (dashIndex > 0) btnText = btnText.Substring(0, dashIndex).Trim();
            int spaceIndex = btnText.IndexOf(" ");
            if (spaceIndex > 0) btnText = btnText.Substring(0, spaceIndex).Trim();
            
            var btn = CreateActionButton(btnText);
            btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            int ftype = facility.FacilityTypeInt;
            btn.Pressed += () => EmitSignal(SignalName.FacilitySelected, ftype);
            grid.AddChild(btn);
        }
    }

    // ============================================================================
    // 公开接口
    // ============================================================================

    public void ShowTown(OverworldTown town, bool instantOverlay = false)
    {
        _currentTown = town;
        ShowPanel(instantOverlay);
    }

    public override void HidePanel()
    {
        base.HidePanel();
        _currentTown = null;
    }

    protected override void OnCloseRequested()
    {
    	HidePanel();
    	EmitSignal(SignalName.LeaveTown);
    }
}
