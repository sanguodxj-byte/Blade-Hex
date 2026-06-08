// TutorialPromptDialog.cs
// 新游戏开始时的教程开启确认对话框
// "是否开启教程提示？" — 居中弹窗，两个按钮
using Godot;
using System;
using BladeHex.Localization;

namespace BladeHex.UI.Tutorial;

/// <summary>
/// 新游戏教程确认对话框 — 询问玩家是否开启教程。
/// </summary>
[GlobalClass]
public partial class TutorialPromptDialog : CanvasLayer
{
    /// <summary>玩家选择后触发（true=开启教程, false=跳过）</summary>
    public event Action<bool>? Confirmed;

    // 主题
    private static readonly Color OverlayColor = new(0, 0, 0, 0.5f);
    private static readonly Color BgColor = new(0.08f, 0.08f, 0.10f, 0.95f);
    private static readonly Color BorderColor = new(0.5f, 0.42f, 0.25f, 0.8f);
    private static readonly Color TitleColor = new(0.95f, 0.85f, 0.5f);
    private static readonly Color TextColor = new(0.85f, 0.82f, 0.75f);
    private static readonly Color BtnNormalBg = new(0.16f, 0.14f, 0.18f);
    private static readonly Color BtnHoverBg = new(0.26f, 0.22f, 0.30f);
    private static readonly Color BtnBorder = new(0.4f, 0.35f, 0.25f, 0.7f);
    private static readonly Color BtnFont = new(0.92f, 0.88f, 0.78f);
    private static readonly Color BtnFontHover = new(1.0f, 0.9f, 0.6f);

    private const int DialogWidth = 420;
    private const int BtnHeight = 38;

    private Control _root = null!;
    private PanelContainer _dialog = null!;

    public override void _Ready()
    {
        Layer = 160;
        BuildUI();
    }

    private void BuildUI()
    {
        _root = new Control();
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        // 半透明遮罩
        var overlay = new ColorRect();
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.Color = OverlayColor;
        _root.AddChild(overlay);

        // 居中对话框
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(center);

        _dialog = new PanelContainer();
        _dialog.CustomMinimumSize = new Vector2(DialogWidth, 0);
        var style = new StyleBoxFlat { BgColor = BgColor };
        style.SetBorderWidthAll(2);
        style.BorderColor = BorderColor;
        style.SetCornerRadiusAll(10);
        style.SetContentMarginAll(28);
        style.ShadowColor = new Color(0, 0, 0, 0.5f);
        style.ShadowSize = 10;
        _dialog.AddThemeStyleboxOverride("panel", style);
        center.AddChild(_dialog);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 16);
        _dialog.AddChild(vbox);

        // 标题
        var title = new Label();
        title.Text = L10n.Tr("TUTORIAL_PROMPT_TITLE");
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", TitleColor);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        // 说明文字
        var desc = new Label();
        desc.Text = L10n.Tr("TUTORIAL_PROMPT_DESC");
        desc.AddThemeFontSizeOverride("font_size", 15);
        desc.AddThemeColorOverride("font_color", TextColor);
        desc.HorizontalAlignment = HorizontalAlignment.Center;
        desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        vbox.AddChild(desc);

        // 按钮行
        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 16);
        btnRow.Alignment = BoxContainer.AlignmentMode.Center;
        vbox.AddChild(btnRow);

        var yesBtn = MakeButton(L10n.Tr("TUTORIAL_PROMPT_ENABLE"));
        yesBtn.Pressed += () => OnChoice(true);
        btnRow.AddChild(yesBtn);

        var noBtn = MakeButton(L10n.Tr("TUTORIAL_PROMPT_SKIP"));
        noBtn.Pressed += () => OnChoice(false);
        btnRow.AddChild(noBtn);

        // 入场动画
        _dialog.Scale = new Vector2(0.9f, 0.9f);
        _dialog.Modulate = new Color(1, 1, 1, 0);
        _dialog.PivotOffset = new Vector2(DialogWidth / 2f, 100);

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(_dialog, "modulate:a", 1.0f, 0.2f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_dialog, "scale", Vector2.One, 0.25f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    private Button MakeButton(string text)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(130, BtnHeight);
        btn.AddThemeFontSizeOverride("font_size", 16);
        btn.AddThemeColorOverride("font_color", BtnFont);
        btn.AddThemeColorOverride("font_hover_color", BtnFontHover);

        var normal = new StyleBoxFlat { BgColor = BtnNormalBg };
        normal.SetBorderWidthAll(1);
        normal.BorderColor = BtnBorder;
        normal.SetCornerRadiusAll(6);
        normal.SetContentMarginAll(6);
        btn.AddThemeStyleboxOverride("normal", normal);

        var hover = new StyleBoxFlat { BgColor = BtnHoverBg };
        hover.SetBorderWidthAll(1);
        hover.BorderColor = new Color(0.55f, 0.48f, 0.3f, 0.9f);
        hover.SetCornerRadiusAll(6);
        hover.SetContentMarginAll(6);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", hover);

        return btn;
    }

    private void OnChoice(bool enableTutorial)
    {
        Confirmed?.Invoke(enableTutorial);

        // 淡出并销毁
        var tween = CreateTween();
        tween.TweenProperty(_dialog, "modulate:a", 0.0f, 0.12f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(() => QueueFree()));
    }
}
