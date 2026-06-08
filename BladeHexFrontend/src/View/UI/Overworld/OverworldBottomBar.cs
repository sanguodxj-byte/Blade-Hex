using BladeHex.Localization;
using BladeHex.UI;
using BladeHex.View.AssetSystem;
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class OverworldBottomBar : PanelContainer
{
    public event Action<string>? ButtonPressed;

    private static readonly IReadOnlyList<(string IconId, string ActionName)> Buttons =
    [
        ("icon_army", "army"),
        ("icon_kingdom", "kingdom_panel"),
        ("icon_territory", "territory"),
        ("icon_quest", "quests"),
        ("IconEncyclopedia", "encyclopedia_panel"),
        ("IconCamp", "camp"),
    ];

    private static readonly IReadOnlyDictionary<string, string> TooltipKeys = new Dictionary<string, string>
    {
        { "army", "TOOLTIP_PARTY" },
        { "kingdom_panel", "TOOLTIP_KINGDOM" },
        { "territory", "TOOLTIP_TERRITORY" },
        { "quests", "TOOLTIP_QUESTS" },
        { "encyclopedia_panel", "TOOLTIP_ENCYCLOPEDIA" },
        { "camp", "TOOLTIP_CAMP" },
    };

    private HBoxContainer _bottomBar = null!;
    private UIFactory _factory = null!;
    private SimpleFloatingTooltip _buttonTooltip = null!;

    public void Initialize()
    {
        _factory = new UIFactory();

        AddThemeStyleboxOverride("panel", MakePanelStyle());
        SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);
        GrowHorizontal = GrowDirection.End;
        GrowVertical = GrowDirection.Begin;
        MouseFilter = MouseFilterEnum.Pass;

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 0);
        margin.AddThemeConstantOverride("margin_right", 0);
        margin.AddThemeConstantOverride("margin_top", 0);
        margin.AddThemeConstantOverride("margin_bottom", 0);
        AddChild(margin);

        _bottomBar = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Begin,
        };
        _bottomBar.AddThemeConstantOverride("separation", 0);
        margin.AddChild(_bottomBar);

        foreach (var (iconId, actionName) in Buttons)
            CreateBarButton(iconId, actionName);

        CustomMinimumSize = new Vector2(496, 96);
        SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);

        _buttonTooltip = new SimpleFloatingTooltip { Name = "ButtonTooltip" };
        AddChild(_buttonTooltip);
    }

    public void UpdateNewsButtonText(int unreadNewsCount)
    {
    }

    private Button CreateBarButton(string iconId, string actionName)
    {
        var button = _factory.CreateButton("", new Vector2(80, 80));
        button.FocusMode = FocusModeEnum.None;

        var icon = TextureAssetResolver.LoadUiTexture(iconId, $"res://BladeHexFrontend/src/assets/ui/{iconId}.png");
        if (icon != null)
        {
            button.Icon = icon;
            button.ExpandIcon = true;
            button.IconAlignment = HorizontalAlignment.Center;
            button.VerticalIconAlignment = VerticalAlignment.Center;
        }

        var emptyStyle = new StyleBoxEmpty();
        button.AddThemeStyleboxOverride("normal", emptyStyle);
        button.AddThemeStyleboxOverride("hover", emptyStyle);
        button.AddThemeStyleboxOverride("pressed", emptyStyle);
        button.AddThemeStyleboxOverride("disabled", emptyStyle);
        button.AddThemeStyleboxOverride("focus", emptyStyle);

        var material = new ShaderMaterial
        {
            Shader = ShaderAssetResolver.Load("icon_button", "res://BladeHexFrontend/src/assets/shaders/icon_button.gdshader"),
        };
        button.Material = material;

        button.MouseEntered += () => OnButtonHover(button, actionName, true);
        button.MouseExited += () => OnButtonHover(button, actionName, false);
        button.ButtonDown += () => SetPressed(button, true);
        button.ButtonUp += () => SetPressed(button, false);
        button.Pressed += () => ButtonPressed?.Invoke(actionName);
        button.MouseDefaultCursorShape = CursorShape.PointingHand;

        _bottomBar.AddChild(button);
        return button;
    }

    private void OnButtonHover(Button button, string actionName, bool hovered)
    {
        var shaderMaterial = button.Material as ShaderMaterial;
        shaderMaterial?.SetShaderParameter("is_hovered", hovered);

        if (!hovered)
        {
            shaderMaterial?.SetShaderParameter("is_pressed", false);
            _buttonTooltip.HidePanel();
            return;
        }

        if (TooltipKeys.TryGetValue(actionName, out var tooltipKey))
        {
            _buttonTooltip.SetText(L10n.Tr(tooltipKey));
            _buttonTooltip.ShowAtMouse();
        }
    }

    private static void SetPressed(Button button, bool pressed)
    {
        var shaderMaterial = button.Material as ShaderMaterial;
        shaderMaterial?.SetShaderParameter("is_pressed", pressed);
    }

    private static StyleBoxFlat MakePanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.10f, 0.76f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.72f, 0.58f, 0.35f, 0.65f),
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 8f,
            ContentMarginRight = 8f,
            ContentMarginTop = 8f,
            ContentMarginBottom = 8f,
        };
    }
}
