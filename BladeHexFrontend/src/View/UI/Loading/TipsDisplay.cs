// TipsDisplay.cs
// Tips提示显示组件 — 自动轮播 + 淡入淡出动画
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.UI.Loading;

[GlobalClass]
public partial class TipsDisplay : Control
{
    [Signal] public delegate void TipChangedEventHandler(Resource tip);

    [Export] public float CycleInterval { get; set; } = 5.0f;
    [Export] public float FadeDuration { get; set; } = 0.6f;
    [Export] public bool ShowIcon { get; set; } = true;
    [Export] public bool AutoStart { get; set; } = true;

    private TipsData? _tipsData;
    private RichTextLabel _tipLabel = null!;
    private Label _iconLabel = null!;
    private HBoxContainer _container = null!;
    private Tween? _tween;
    private Timer _timer = null!;
    private bool _isRunning = false;

    public string FilterCategory { get; set; } = "";

    private static readonly Random _random = new();

    public override void _Ready()
    {
        BuildUI();
        if (AutoStart)
        {
            Start();
        }
    }

    private void BuildUI()
    {
        var theme = UITheme.Instance;
        if (theme == null)
        {
            GD.PushError("TipsDisplay: UITheme.Instance is null");
            return;
        }

        _container = new HBoxContainer();
        _container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _container.AddThemeConstantOverride("separation", theme.SpacingMd);
        _container.Alignment = BoxContainer.AlignmentMode.Center;
        AddChild(_container);

        _iconLabel = new Label();
        _iconLabel.Text = "💡";
        _iconLabel.AddThemeFontSizeOverride("font_size", theme.FontSizeLg);
        _iconLabel.VerticalAlignment = VerticalAlignment.Center;
        _iconLabel.Visible = ShowIcon;
        _container.AddChild(_iconLabel);

        _tipLabel = new RichTextLabel();
        _tipLabel.BbcodeEnabled = true;
        _tipLabel.ScrollActive = false;
        _tipLabel.FitContent = true;
        _tipLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _tipLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _tipLabel.AddThemeFontSizeOverride("normal_font_size", theme.FontSizeSm);
        _tipLabel.AddThemeColorOverride("default_color", theme.TextSecondary);
        _tipLabel.AddThemeStyleboxOverride("normal", new StyleBoxEmpty { ContentMarginLeft = 0, ContentMarginRight = 0, ContentMarginTop = 0, ContentMarginBottom = 0 });
        _container.AddChild(_tipLabel);

        _timer = new Timer();
        _timer.OneShot = false;
        _timer.WaitTime = CycleInterval;
        _timer.Timeout += OnCycleTimer;
        AddChild(_timer);
    }

    public void SetTipsData(TipsData data)
    {
        _tipsData = data;
        _tipsData.ResetRotation();
    }

    public void SetCategoryFilter(string category)
    {
        FilterCategory = category;
        _tipsData?.ResetRotation();
    }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        if (_tipsData == null)
        {
            _tipsData = new TipsData();
        }
        ShowNextTip();
        _timer.Start();
    }

    public void Stop()
    {
        _isRunning = false;
        _timer.Stop();
    }

    private void ShowNextTip()
    {
        if (_tipsData == null) return;

        Tip? tip = null;
        if (!string.IsNullOrEmpty(FilterCategory))
        {
            var filtered = _tipsData.GetTipsByCategory(FilterCategory);
            if (filtered.Count == 0)
            {
                tip = _tipsData.GetNextTip();
            }
            else
            {
                tip = filtered[_random.Next(filtered.Count)];
            }
        }
        else
        {
            tip = _tipsData.GetNextTip();
        }

        if (tip != null)
        {
            AnimateTipChange(tip);
        }
    }

    private void AnimateTipChange(Tip tip)
    {
        if (_tween != null && _tween.IsValid())
        {
            _tween.Kill();
        }
        _tween = CreateTween();
        if (_tween == null) return;
        _tween.SetParallel(false);

        float fadeOutTime = 0.2f;
        float fadeInTime = 0.3f;
        float holdTime = 0.7f;

        _tween.TweenProperty(_container, "modulate:a", 0.0f, fadeOutTime)
              .SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.In);

        _tween.TweenCallback(Callable.From(() =>
        {
            _tipLabel.Text = $"[i]{tip.Text}[/i]";
            EmitSignal(SignalName.TipChanged, tip);
        }));

        _tween.TweenProperty(_container, "modulate:a", 1.0f, fadeInTime)
              .SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.In);

        _tween.TweenInterval(holdTime);
    }

    private void OnCycleTimer() => ShowNextTip();

    public void ShowNext() => ShowNextTip();
    
    public void ShowLastTip()
    {
        if (_tipsData == null) return;
        var tips = _tipsData.GetAllTips();
        if (tips.Count > 0)
        {
            AnimateTipChange(tips[tips.Count - 1]);
        }
    }
}
