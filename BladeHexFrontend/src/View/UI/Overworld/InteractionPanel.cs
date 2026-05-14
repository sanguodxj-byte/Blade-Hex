// InteractionPanel.cs
// 交互面板 — 大地图实体交互弹窗（城镇/NPC/敌人）
// 居中 520×560 弹窗，上方插画区 + 标题/描述 + 下方选项列表
using Godot;
using BladeHex.Strategic;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class InteractionPanel : POIPanelBase
{
    // ============================================================================
    // 面板规格
    // ============================================================================
    protected override int PanelWidth => 520;
    protected override int PanelHeight => 560;
    protected override int PanelLayer => 20;

    // 插画区颜色
    private static readonly Color IllustTown = new(0.08f, 0.10f, 0.18f);
    private static readonly Color IllustVillage = new(0.06f, 0.12f, 0.08f);
    private static readonly Color IllustEnemy = new(0.15f, 0.06f, 0.06f);
    private static readonly Color IllustDefault = new(0.08f, 0.12f, 0.18f, 1.0f);

    private const int IllustHeight = 120;
    private const int BtnHeight = 52;
    private const int OptionSpacing = 10;

    // ============================================================================
    // 信号
    // ============================================================================
    [Signal] public delegate void OptionSelectedEventHandler(InteractionOption option);
    [Signal] public delegate void CloseRequestedEventHandler();

    // ============================================================================
    // 字段
    // ============================================================================
    private ColorRect _illustRect = null!;
    private Label _titleLabel = null!;
    private Label _infoLabel = null!;
    private RichTextLabel _descLabel = null!;
    private VBoxContainer _optionsVbox = null!;
    private Node2D? _currentEntity;

    // ============================================================================
    // 公开接口
    // ============================================================================

    /// <summary>显示交互面板，展示实体信息和可用选项</summary>
    public void ShowForEntity(Node2D entity, Godot.Collections.Array options)
    {
        _currentEntity = entity;
        _titleLabel.Text = GetEntityName(entity);
        _infoLabel.Text = GetEntityInfo(entity);
        _descLabel.Text = GetEntityDescription(entity);
        SetIllustrationColor(entity);
        FillOptions(options);
        Root.Visible = true;
    }

    /// <summary>隐藏面板并清理选项</summary>
    public override void HidePanel()
    {
        base.HidePanel();
        _currentEntity = null;
        ClearOptions();
    }

    // ============================================================================
    // 关闭处理
    // ============================================================================
    protected override void OnCloseRequested()
    {
        EmitSignal(SignalName.CloseRequested);
        HidePanel();
    }

    // ============================================================================
    // 内容构建
    // ============================================================================
    protected override void BuildContent(VBoxContainer container)
    {
        container.AddThemeConstantOverride("separation", 0);

        // 插画区
        BuildIllustration(container);

        // 文字内容区
        BuildTextContent(container);

        // 分隔线
        container.AddChild(CreateSeparatorH());

        // 选项区
        BuildOptionsArea(container);
    }

    private void BuildIllustration(VBoxContainer parent)
    {
        var illustPanel = new PanelContainer();
        illustPanel.CustomMinimumSize = new Vector2(0, IllustHeight);
        var style = new StyleBoxFlat { BgColor = IllustDefault };
        style.SetContentMarginAll(0);
        illustPanel.AddThemeStyleboxOverride("panel", style);
        parent.AddChild(illustPanel);

        _illustRect = new ColorRect();
        _illustRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _illustRect.Color = IllustDefault;
        illustPanel.AddChild(_illustRect);
    }

    private void BuildTextContent(VBoxContainer parent)
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", PanelMargin);
        margin.AddThemeConstantOverride("margin_right", PanelMargin);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        parent.AddChild(margin);

        var textVbox = new VBoxContainer();
        textVbox.AddThemeConstantOverride("separation", 6);
        margin.AddChild(textVbox);

        // 标题
        _titleLabel = new Label();
        _titleLabel.AddThemeFontSizeOverride("font_size", FontSizeXl);
        _titleLabel.AddThemeColorOverride("font_color", ThemeTextAccent);
        textVbox.AddChild(_titleLabel);

        // 副标题信息
        _infoLabel = new Label();
        _infoLabel.AddThemeFontSizeOverride("font_size", FontSizeSm);
        _infoLabel.AddThemeColorOverride("font_color", ThemeTextMuted);
        textVbox.AddChild(_infoLabel);

        // 描述（可滚动，占据剩余空间）
        _descLabel = new RichTextLabel();
        _descLabel.BbcodeEnabled = true;
        _descLabel.ScrollActive = true;
        _descLabel.FitContent = false;
        _descLabel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _descLabel.AddThemeFontSizeOverride("normal_font_size", FontSizeMd);
        _descLabel.AddThemeColorOverride("default_color", ThemeTextPrimary);
        textVbox.AddChild(_descLabel);
    }

    private void BuildOptionsArea(VBoxContainer parent)
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", PanelMargin);
        margin.AddThemeConstantOverride("margin_right", PanelMargin);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        margin.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        margin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        parent.AddChild(margin);

        _optionsVbox = new VBoxContainer();
        _optionsVbox.AddThemeConstantOverride("separation", OptionSpacing);
        _optionsVbox.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        _optionsVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        margin.AddChild(_optionsVbox);
    }

    // ============================================================================
    // 实体信息提取
    // ============================================================================

    private void SetIllustrationColor(Node2D entity)
    {
        if (entity is OverworldTown town)
            _illustRect.Color = town.TownType == "village" ? IllustVillage : IllustTown;
        else if (entity is OverworldEnemy)
            _illustRect.Color = IllustEnemy;
        else
            _illustRect.Color = IllustDefault;
    }

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
            return $"{(town.TownType == "village" ? "村庄" : "城镇")} · 繁荣度 {town.Prosperity} · 守军 {town.Garrison}";
        return "";
    }

    private static string GetEntityDescription(Node2D entity)
    {
        if (entity is OverworldEnemy enemy) return enemy.GetDescription();
        if (entity is OverworldTown town) return town.GetDescription();
        return "";
    }

    // ============================================================================
    // 选项管理
    // ============================================================================

    private void FillOptions(Godot.Collections.Array options)
    {
        ClearOptions();
        foreach (var variant in options)
        {
            if (variant.AsGodotObject() is InteractionOption option)
                _optionsVbox.AddChild(MakeOptionButton(option));
        }
    }

    private Button MakeOptionButton(InteractionOption option)
    {
        var btn = CreateButton(option.OptionLabel, new Vector2(0, BtnHeight));
        btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        btn.AddThemeFontSizeOverride("font_size", FontSizeLg);

        if (!string.IsNullOrEmpty(option.Tooltip))
            btn.TooltipText = option.Tooltip;

        // 禁用状态
        btn.Disabled = !option.Enabled;
        if (!option.Enabled) btn.Modulate = new Color(1, 1, 1, 0.5f);

        // 点击事件
        var captured = option;
        btn.Pressed += () => EmitSignal(SignalName.OptionSelected, captured);

        return btn;
    }

    private void ClearOptions()
    {
        foreach (Node child in _optionsVbox.GetChildren())
            child.QueueFree();
    }
}
