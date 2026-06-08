// InteractionPanel.cs
// 交互面板 — 大地图实体交互弹窗（城镇/NPC/敌人）
// 使用 POIPanelBase 统一布局，只填充实体数据和可选动作。
using Godot;
using System.Collections.Generic;
using BladeHex.Strategic;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class InteractionPanel : POIPanelBase
{
    private static readonly Color IllustTown = new(0.08f, 0.10f, 0.18f);
    private static readonly Color IllustVillage = new(0.06f, 0.12f, 0.08f);
    private static readonly Color IllustEnemy = new(0.15f, 0.06f, 0.06f);
    private static readonly Color IllustDefault = new(0.08f, 0.12f, 0.18f, 1.0f);

    // ============================================================================
    // 信号
    // ============================================================================
    [Signal] public delegate void OptionSelectedEventHandler(InteractionOption option);
    [Signal] public delegate void CloseRequestedEventHandler();

    // ============================================================================
    // 字段
    // ============================================================================
    private readonly List<InteractionOption> _currentOptions = new();
    private Node2D? _currentEntity;

    // ============================================================================
    // 统一布局数据
    // ============================================================================
    protected override Color GetIllustrationColor()
    {
        if (_currentEntity is OverworldTown town)
            return town.TownType == "village" ? IllustVillage : IllustTown;
        if (_currentEntity is OverworldEnemy)
            return IllustEnemy;
        return IllustDefault;
    }

    protected override string GetIllustrationText()
    {
        if (_currentEntity is OverworldEnemy enemy)
            return $"[ {enemy.GetDisplayName()} ]";
        if (_currentEntity is OverworldTown town)
            return $"[ {GetTownTypeText(town.TownType)} ]";
        return "[ 遭遇 ]";
    }

    protected override string? GetIllustrationPath()
        => _currentEntity is OverworldTown town ? POIIllustrationResolver.GetTownIllustration(town.TownType) : null;

    protected override string GetPanelTitle() => "";

    protected override string GetInfoText()
    {
        if (_currentEntity == null) return "";

        string name = GetEntityName(_currentEntity);
        string info = GetEntityInfo(_currentEntity);
        return string.IsNullOrEmpty(info) ? name : $"{name} | {info}";
    }

    protected override string GetDescriptionText()
        => _currentEntity != null ? GetEntityDescription(_currentEntity) : "";

    protected override string GetLeaveButtonText() => "离开";

    protected override void PopulateActions(VBoxContainer actionsContainer)
    {
        var grid = new GridContainer();
        grid.Columns = 3;
        grid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        actionsContainer.AddChild(grid);

        foreach (var option in _currentOptions)
        {
            // 普通 leave 交给基类底部“离开”按钮；breakout 等有语义的 Leave 需要显示为动作按钮。
            if (option.CurrentInteractionType == InteractionType.Type.Leave && option.Id == "leave")
                continue;
            grid.AddChild(MakeOptionButton(option));
        }
    }

    // ============================================================================
    // 公开接口
    // ============================================================================

    /// <summary>显示交互面板，展示实体信息和可用选项</summary>
    public void ShowForEntity(Node2D entity, Godot.Collections.Array options)
    {
        _currentEntity = entity;
        _currentOptions.Clear();

        foreach (var variant in options)
        {
            if (variant.AsGodotObject() is InteractionOption option)
                _currentOptions.Add(option);
        }

        ShowPanel();
    }

    /// <summary>隐藏面板并清理当前实体</summary>
    public override void HidePanel()
    {
        base.HidePanel();
        _currentEntity = null;
        _currentOptions.Clear();
    }

    // ============================================================================
    // 关闭处理
    // ============================================================================
    protected override void OnCloseRequested()
    {
    	HidePanel();
    	EmitSignal(SignalName.CloseRequested);
    }

    // ============================================================================
    // 实体信息提取
    // ============================================================================

    private static string GetEntityName(Node2D entity)
    {
        if (entity is OverworldEnemy enemy) return enemy.GetDisplayName();
        if (entity is OverworldTown town) return town.TownName;
        return "未知";
    }

    private static string GetEntityInfo(Node2D entity)
    {
        if (entity is OverworldEnemy enemy && enemy.NpcProfile != null)
            return $"{enemy.NpcProfile.GetNpcTypeNameForType((int)enemy.NpcProfile.npcType)} · {enemy.NpcProfile.GetAttitudeText()}";
        if (entity is OverworldTown town)
            return $"{GetTownTypeText(town.TownType)} · 繁荣度 {town.Prosperity} · 守军 {town.Garrison}";
        return "";
    }

    private static string GetEntityDescription(Node2D entity)
    {
        if (entity is OverworldEnemy enemy) return enemy.GetDescription();
        if (entity is OverworldTown town) return town.GetDescription();
        return "";
    }

    private static string GetTownTypeText(string townType) => townType switch
    {
        "village" => "村庄",
        "port" => "港口",
        "castle" => "城堡",
        "outpost" => "前哨站",
        "tavern" => "旅店",
        "mine" => "矿场",
        "shrine" => "药师所",
        _ => "城镇",
    };

    // ============================================================================
    // 选项管理
    // ============================================================================

    private Button MakeOptionButton(InteractionOption option)
    {
        var btn = CreateActionButton(option.OptionLabel);
        btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        if (!string.IsNullOrEmpty(option.Tooltip))
            btn.TooltipText = option.Tooltip;

        btn.Disabled = !option.Enabled;
        if (!option.Enabled)
            btn.Modulate = new Color(1, 1, 1, 0.5f);

        var captured = option;
        btn.Pressed += () => EmitSignal(SignalName.OptionSelected, captured);

        return btn;
    }
}
