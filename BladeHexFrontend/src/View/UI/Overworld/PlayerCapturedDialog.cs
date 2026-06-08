using System;
using Godot;

namespace BladeHex.View.UI.Overworld;

public partial class PlayerCapturedDialog : CanvasLayer
{
    private PanelContainer? _panel;
    private Label? _descLabel;
    private Button? _ransomButton;
    private Button? _waitButton;

    private string _captorName = "未知领主";
    private int _playerGold = 0;
    private Action? _onPayRansom;
    private Action? _onWaitEscape;

    public void Setup(string captorName, int playerGold, Action onPayRansom, Action onWaitEscape)
    {
        _captorName = captorName;
        _playerGold = playerGold;
        _onPayRansom = onPayRansom;
        _onWaitEscape = onWaitEscape;
    }

    public override void _Ready()
    {
        // 全屏背景暗化
        var background = new ColorRect
        {
            Color = new Color(0.05f, 0.05f, 0.05f, 0.85f),
            CustomMinimumSize = new Vector2(1920, 1080),
            MouseFilter = Control.MouseFilterEnum.Stop // 拦截所有大地图点击
        };
        AddChild(background);

        // 主对话框面板 (玻璃态毛玻璃效果)
        _panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(500, 320)
        };
        
        // 样式装饰
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.12f, 0.14f, 0.95f),
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.24f, 0.24f, 0.28f, 1.0f),
            ShadowColor = new Color(0, 0, 0, 0.5f),
            ShadowSize = 20
        };
        _panel.AddThemeStyleboxOverride("panel", style);

        // 居中定位
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center, Control.LayoutPresetMode.Minsize);
        AddChild(_panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_top", 30);
        margin.AddThemeConstantOverride("margin_left", 40);
        margin.AddThemeConstantOverride("margin_right", 40);
        margin.AddThemeConstantOverride("margin_bottom", 30);
        _panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 20);
        margin.AddChild(vbox);

        // 标题
        var title = new Label
        {
            Text = "战 败 与 被 俘",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeColorOverride("font_color", new Color(0.9f, 0.3f, 0.3f));
        title.AddThemeFontSizeOverride("font_size", 24);
        vbox.AddChild(title);

        // 描述文字
        _descLabel = new Label
        {
            Text = $"你被敌方领主 {_captorName} 击败并生擒！\n现在你被关押在其势力城堡中，重获自由需要付出代价。",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _descLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f));
        _descLabel.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(_descLabel);

        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) }); // 隔离

        // 按钮栏
        var buttonHBox = new HBoxContainer();
        buttonHBox.AddThemeConstantOverride("separation", 25);
        buttonHBox.Alignment = BoxContainer.AlignmentMode.Center;
        vbox.AddChild(buttonHBox);

        // 支付赎金按钮
        _ransomButton = new Button
        {
            Text = $"支付赎金 (5000金)\n当前: {_playerGold}金",
            CustomMinimumSize = new Vector2(180, 55)
        };
        _ransomButton.AddThemeStyleboxOverride("normal", CreateButtonStyle(new Color(0.18f, 0.24f, 0.18f, 1.0f)));
        _ransomButton.AddThemeStyleboxOverride("hover", CreateButtonStyle(new Color(0.24f, 0.32f, 0.24f, 1.0f)));
        _ransomButton.AddThemeStyleboxOverride("pressed", CreateButtonStyle(new Color(0.14f, 0.18f, 0.14f, 1.0f)));
        _ransomButton.AddThemeStyleboxOverride("disabled", CreateButtonStyle(new Color(0.18f, 0.18f, 0.18f, 0.5f)));

        if (_playerGold < 5000)
        {
            _ransomButton.Disabled = true;
            _ransomButton.TooltipText = "金币不足以支付高额赎金！";
        }
        _ransomButton.Pressed += OnRansomPressed;
        buttonHBox.AddChild(_ransomButton);

        // 等待逃脱按钮
        _waitButton = new Button
        {
            Text = "等待逃脱\n(需要 7 天时间)",
            CustomMinimumSize = new Vector2(180, 55)
        };
        _waitButton.AddThemeStyleboxOverride("normal", CreateButtonStyle(new Color(0.24f, 0.24f, 0.28f, 1.0f)));
        _waitButton.AddThemeStyleboxOverride("hover", CreateButtonStyle(new Color(0.32f, 0.32f, 0.36f, 1.0f)));
        _waitButton.AddThemeStyleboxOverride("pressed", CreateButtonStyle(new Color(0.18f, 0.18f, 0.20f, 1.0f)));
        _waitButton.Pressed += OnWaitPressed;
        buttonHBox.AddChild(_waitButton);
    }

    private StyleBoxFlat CreateButtonStyle(Color color)
    {
        return new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(1, 1, 1, 0.15f)
        };
    }

    private void OnRansomPressed()
    {
        _onPayRansom?.Invoke();
        QueueFree();
    }

    private void OnWaitPressed()
    {
        _onWaitEscape?.Invoke();
        QueueFree();
    }
}
