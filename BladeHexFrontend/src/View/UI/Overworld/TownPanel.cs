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
            "port" => "港口",
            "castle" => "城堡",
            "outpost" => "前哨站",
            "tavern" => "旅店",
            "mine" => "矿场",
            "shrine" => "药师所",
            _ => "城镇"
        };
        return $"[ {type} ]";
    }

    protected override string GetPanelTitle() => "";

    protected override string GetInfoText()
    {
        if (_currentTown == null) return "";
        string typeText = _currentTown.TownType switch
        {
            "village" => "村庄",
            "port" => "港口",
            "castle" => "城堡",
            "outpost" => "前哨站",
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
            "port" => Strategic.OverworldPOI.POIType.Port,
            "castle" => Strategic.OverworldPOI.POIType.Castle,
            "outpost" => Strategic.OverworldPOI.POIType.Outpost,
            "tavern" => Strategic.OverworldPOI.POIType.Tavern,
            "mine" => Strategic.OverworldPOI.POIType.Mine,
            "shrine" => Strategic.OverworldPOI.POIType.Shrine,
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

        // 确保设施已初始化
        if (_currentTown.Facilities.Count == 0)
        {
            if (_currentTown.TownType == "village")
                _currentTown.SetupVillageFacilities();
            else
                _currentTown.SetupDefaultFacilities();
        }

        foreach (var facility in _currentTown.Facilities)
        {
            if (!facility.IsAvailable) continue;

            string desc = facility.Description;
            string btnText = string.IsNullOrEmpty(desc)
                ? facility.FacilityName
                : $"{facility.FacilityName} -- {desc}";
            var btn = CreateActionButton(btnText);
            int ftype = facility.FacilityTypeInt;
            btn.Pressed += () => EmitSignal(SignalName.FacilitySelected, ftype);
            container.AddChild(btn);
        }
    }

    // ============================================================================
    // 公开接口
    // ============================================================================

    public void ShowTown(OverworldTown town)
    {
        _currentTown = town;
        ShowPanel();
    }

    public override void HidePanel()
    {
        base.HidePanel();
        _currentTown = null;
    }

    protected override void OnCloseRequested()
    {
        EmitSignal(SignalName.LeaveTown);
        HidePanel();
    }
}
