// TownPanel.cs
// 城镇面板 — 进入城镇/村庄时显示设施列表，允许玩家选择交互
using Godot;
using BladeHex.Strategic;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class TownPanel : POIPanelBase
{
    // ============================================================================
    // 面板规格
    // ============================================================================

    protected override int PanelWidth => 450;
    protected override int PanelHeight => 450;

    // ============================================================================
    // 信号
    // ============================================================================

    [Signal]
    public delegate void FacilitySelectedEventHandler(int facilityType);

    [Signal]
    public delegate void LeaveTownEventHandler();

    // ============================================================================
    // 字段
    // ============================================================================

    private Label _townNameLabel = null!;
    private Label _townInfoLabel = null!;
    private RichTextLabel _townDescLabel = null!;
    private GridContainer _facilitiesGrid = null!;
    private OverworldTown? _currentTown;

    // ============================================================================
    // 内容构建
    // ============================================================================

    protected override void BuildContent(VBoxContainer container)
    {
        // 城镇名称
        _townNameLabel = CreateTitleLabel("");
        container.AddChild(_townNameLabel);

        // 城镇信息
        _townInfoLabel = CreateMutedLabel("");
        container.AddChild(_townInfoLabel);

        // 描述
        _townDescLabel = CreateRichText(new Vector2(410, 50));
        container.AddChild(_townDescLabel);

        // 分割线
        container.AddChild(CreateSeparatorH());

        // 设施标题
        container.AddChild(CreateBodyLabel("设施:"));

        // 设施按钮网格
        _facilitiesGrid = new GridContainer();
        _facilitiesGrid.Columns = 2;
        _facilitiesGrid.AddThemeConstantOverride("h_separation", SpacingMd);
        _facilitiesGrid.AddThemeConstantOverride("v_separation", SpacingMd);
        container.AddChild(_facilitiesGrid);

        // 分割线
        container.AddChild(CreateSeparatorH());

        // 离开按钮
        var leaveBtn = CreateButton("离开城镇", new Vector2(410, 40));
        leaveBtn.Pressed += () =>
        {
            EmitSignal(SignalName.LeaveTown);
            HidePanel();
        };
        container.AddChild(leaveBtn);
    }

    // ============================================================================
    // 公开接口
    // ============================================================================

    public void ShowTown(OverworldTown town)
    {
        _currentTown = town;
        _townNameLabel.Text = town.TownName;

        string typeText = town.TownType == "village" ? "村庄" : "城镇";
        _townInfoLabel.Text = $"{typeText} · 繁荣: {town.Prosperity} · 守军: {town.Garrison}";
        _townDescLabel.Text = town.GetDescription();

        PopulateFacilities();
        ShowPanel();
    }

    public override void HidePanel()
    {
        base.HidePanel();
        _currentTown = null;
        ClearFacilities();
    }

    protected override void OnCloseRequested()
    {
        EmitSignal(SignalName.LeaveTown);
        HidePanel();
    }

    // ============================================================================
    // 设施管理
    // ============================================================================

    private void PopulateFacilities()
    {
        ClearFacilities();
        if (_currentTown == null)
            return;

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
            if (!facility.IsAvailable)
                continue;

            var btn = CreateButton(facility.FacilityName, new Vector2(195, 50));
            btn.TooltipText = facility.Description;

            int ftype = facility.FacilityTypeInt;
            btn.Pressed += () => EmitSignal(SignalName.FacilitySelected, ftype);
            _facilitiesGrid.AddChild(btn);
        }
    }

    private void ClearFacilities()
    {
        foreach (Node child in _facilitiesGrid.GetChildren())
            child.QueueFree();
    }
}
