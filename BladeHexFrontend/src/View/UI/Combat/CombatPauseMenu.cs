// CombatPauseMenu.cs
// 战斗中的 ESC 菜单：返回游戏 / 设置 / 回到主菜单 / 退出
// 打开时暂停游戏树（ProcessMode.Always 保证菜单本身仍能处理输入）
using Godot;

namespace BladeHex.UI.Combat;

/// <summary>
/// 战斗场景的 ESC 系统菜单。CombatSceneBase 在按下 ESC 且无其他可取消操作时打开。
/// </summary>
public partial class CombatPauseMenu : CanvasLayer
{
    [Signal] public delegate void ResumeRequestedEventHandler();
    [Signal] public delegate void ReturnToMainMenuRequestedEventHandler();

    private Control _root = null!;

    // ============== 主题色（沿用 OverworldUI 风格，避免外部依赖） ==============
    private static readonly Color BgPrimary = new(0.10f, 0.09f, 0.07f, 0.96f);
    private static readonly Color BgSecondary = new(0.16f, 0.14f, 0.11f, 1.0f);
    private static readonly Color BgCardHover = new(0.22f, 0.18f, 0.14f, 1.0f);
    private static readonly Color BorderDefault = new(0.45f, 0.38f, 0.28f, 1.0f);
    private static readonly Color BorderHighlight = new(0.85f, 0.72f, 0.45f, 1.0f);
    private static readonly Color TextAccent = new(0.95f, 0.85f, 0.55f, 1.0f);
    private static readonly Color TextNegative = new(0.92f, 0.45f, 0.40f, 1.0f);
    private const int FontSizeMd = 16;
    private const int FontSizeXl = 22;
    private const int SpacingLg = 12;
    private const int RadiusMd = 8;
    private const int ButtonHeightLg = 44;

    public override void _Ready()
    {
        // 暂停时仍可工作
        ProcessMode = ProcessModeEnum.Always;
        Layer = 100; // 高于战斗 UI

        _root = new Control { Name = "Root" };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.MouseFilter = Control.MouseFilterEnum.Stop;
        _root.ProcessMode = ProcessModeEnum.Always;
        AddChild(_root);

        BuildMenu();
        Visible = false;
    }

    /// <summary>切换菜单显示。打开时暂停游戏。</summary>
    public void Toggle()
    {
        if (Visible) Close();
        else Open();
    }

    public void Open()
    {
        Visible = true;
        GetTree().Paused = true;
    }

    public void Close()
    {
        Visible = false;
        GetTree().Paused = false;
        EmitSignal(SignalName.ResumeRequested);
    }

    public bool IsOpen => Visible;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.Escape)
        {
            // GameMenuManager 的设置面板由它自己处理 ESC
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    // ===========================================================
    // 构建
    // ===========================================================

    private void BuildMenu()
    {
        // 半透明遮罩
        var overlay = new PanelContainer();
        var overlayBg = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.55f) };
        overlay.AddThemeStyleboxOverride("panel", overlayBg);
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.MouseFilter = Control.MouseFilterEnum.Stop;
        _root.AddChild(overlay);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(center);

        var inner = new PanelContainer();
        var innerBg = MakePanelStyle(BgPrimary, BorderHighlight, 2, RadiusMd, 30);
        inner.AddThemeStyleboxOverride("panel", innerBg);
        center.AddChild(inner);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", SpacingLg);
        vbox.CustomMinimumSize = new Vector2(240, 0);
        inner.AddChild(vbox);

        var title = new Label
        {
            Text = "- 系统菜单 -",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", FontSizeXl);
        title.AddThemeColorOverride("font_color", TextAccent);
        vbox.AddChild(title);

        var resumeBtn = MakeButton("返回游戏");
        resumeBtn.Pressed += Close;
        vbox.AddChild(resumeBtn);

        var settingsBtn = MakeButton("设置");
        settingsBtn.Pressed += OpenSettings;
        vbox.AddChild(settingsBtn);

        var mainMenuBtn = MakeButton("回到主菜单");
        mainMenuBtn.Pressed += () =>
        {
            // 切换场景前必须解除暂停 + 清理所有挂在 /root 下的游离场景（战斗场景/大地图等）
            BladeHex.View.SceneTransition.ChangeSceneTo(GetTree(), "res://src/ui/main_menu/main_menu.tscn");
            EmitSignal(SignalName.ReturnToMainMenuRequested);
        };
        vbox.AddChild(mainMenuBtn);

        var exitBtn = MakeButton("退出游戏");
        exitBtn.AddThemeColorOverride("font_color", TextNegative);
        exitBtn.Pressed += () =>
        {
            GetTree().Paused = false;
            GetTree().Quit();
        };
        vbox.AddChild(exitBtn);
    }

    private void OpenSettings()
    {
        // 统一使用 GameMenuManager 的设置面板
        var gameMenu = BladeHex.Data.Globals.GameMenuOrNull;
        if (gameMenu != null)
        {
            gameMenu.OpenSettings();
        }
    }

    // ===========================================================
    // 工厂
    // ===========================================================

    private Button MakeButton(string text)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(200, ButtonHeightLg),
        };
        btn.AddThemeFontSizeOverride("font_size", FontSizeMd);

        var normal = new StyleBoxFlat { BgColor = BgSecondary };
        normal.SetBorderWidthAll(2);
        normal.BorderColor = BorderDefault;
        normal.SetCornerRadiusAll(4);
        btn.AddThemeStyleboxOverride("normal", normal);

        var hover = new StyleBoxFlat { BgColor = BgCardHover };
        hover.SetBorderWidthAll(2);
        hover.BorderColor = BorderHighlight;
        hover.SetCornerRadiusAll(4);
        btn.AddThemeStyleboxOverride("hover", hover);

        var pressed = new StyleBoxFlat { BgColor = BgCardHover };
        pressed.SetBorderWidthAll(2);
        pressed.BorderColor = BorderHighlight;
        pressed.SetCornerRadiusAll(4);
        btn.AddThemeStyleboxOverride("pressed", pressed);

        return btn;
    }

    private static StyleBoxFlat MakePanelStyle(Color bg, Color border, int borderWidth, int radius, int margin)
    {
        var style = new StyleBoxFlat { BgColor = bg };
        style.SetBorderWidthAll(borderWidth);
        style.BorderColor = border;
        style.SetCornerRadiusAll(radius);
        style.SetContentMarginAll(margin);
        return style;
    }
}
