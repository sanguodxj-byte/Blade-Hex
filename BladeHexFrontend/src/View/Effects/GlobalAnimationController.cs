using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.View.Effects;

public enum GlobalAnimationKind
{
    Opening,
    LegendaryEntrance,
    WorldEvent,
    SceneTransition,
    Custom,
}

public enum GlobalAnimationQueueMode
{
    Enqueue,
    Replace,
    IgnoreIfBusy,
}

public sealed class GlobalAnimationRequest
{
    public string Id { get; init; } = string.Empty;
    public GlobalAnimationKind Kind { get; init; } = GlobalAnimationKind.Custom;
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string ResourcePath { get; init; } = string.Empty;
    public float Duration { get; init; } = 1.2f;
    public Color AccentColor { get; init; } = new(0.85f, 0.72f, 0.46f, 1.0f);
    public Action? FinishedCallback { get; init; }
}

/// <summary>
/// Scene-independent overlay for full-screen presentation beats.
/// It intentionally ships without concrete art assets; callers submit semantic
/// requests now, and future resource-backed animations can plug into ResourcePath.
/// </summary>
[GlobalClass]
public partial class GlobalAnimationController : CanvasLayer
{
    [Signal] public delegate void AnimationStartedEventHandler(string animationId, int kind);
    [Signal] public delegate void AnimationFinishedEventHandler(string animationId, int kind);

    private const int LayerOrder = 180;
    private const float MinDuration = 0.15f;

    private readonly Queue<GlobalAnimationRequest> _queue = new();

    private Control? _root;
    private ColorRect? _scrim;
    private PanelContainer? _panel;
    private Label? _titleLabel;
    private Label? _subtitleLabel;
    private TextureRect? _resourceHost;
    private Tween? _activeTween;
    private GlobalAnimationRequest? _activeRequest;

    public bool IsPlaying => _activeRequest != null;

    public override void _Ready()
    {
        Layer = LayerOrder;
        BuildOverlay();
        HideOverlay();
    }

    public override void _ExitTree()
    {
        _activeTween?.Kill();
        _queue.Clear();
        _activeRequest = null;
    }

    public bool PlayOpening(string animationId = "opening", string title = "", string subtitle = "", float duration = 1.4f, GlobalAnimationQueueMode queueMode = GlobalAnimationQueueMode.Enqueue)
    {
        return Play(new GlobalAnimationRequest
        {
            Id = animationId,
            Kind = GlobalAnimationKind.Opening,
            Title = title,
            Subtitle = subtitle,
            Duration = duration,
        }, queueMode);
    }

    public bool PlayLegendaryEntrance(string legendaryId, string displayName, string subtitle = "", string resourcePath = "", float duration = 1.6f, GlobalAnimationQueueMode queueMode = GlobalAnimationQueueMode.Enqueue)
    {
        string id = string.IsNullOrWhiteSpace(legendaryId)
            ? $"legendary:{displayName}"
            : $"legendary:{legendaryId}";

        return Play(new GlobalAnimationRequest
        {
            Id = id,
            Kind = GlobalAnimationKind.LegendaryEntrance,
            Title = displayName,
            Subtitle = subtitle,
            ResourcePath = resourcePath,
            Duration = duration,
            AccentColor = new Color(0.92f, 0.38f, 0.24f, 1.0f),
        }, queueMode);
    }

    public bool Play(GlobalAnimationRequest request, GlobalAnimationQueueMode queueMode = GlobalAnimationQueueMode.Enqueue)
    {
        if (request == null) return false;

        if (IsPlaying)
        {
            switch (queueMode)
            {
                case GlobalAnimationQueueMode.IgnoreIfBusy:
                    return false;
                case GlobalAnimationQueueMode.Replace:
                    StopActive(emitFinished: false);
                    _queue.Clear();
                    break;
                case GlobalAnimationQueueMode.Enqueue:
                    _queue.Enqueue(request);
                    return true;
            }
        }

        StartRequest(request);
        return true;
    }

    public void ClearQueue()
    {
        _queue.Clear();
    }

    public void SkipCurrent(bool playNext = true)
    {
        StopActive(emitFinished: true);
        if (playNext)
            PlayNextQueued();
    }

    private void StartRequest(GlobalAnimationRequest request)
    {
        EnsureOverlay();
        _activeRequest = request;

        LoadResourceVisual(request.ResourcePath);
        ApplyText(request);
        ShowOverlay();

        EmitSignal(SignalName.AnimationStarted, request.Id, (int)request.Kind);

        float duration = Mathf.Max(request.Duration, MinDuration);
        float holdDuration = Mathf.Max(0.0f, duration - 0.55f);

        _root!.Modulate = new Color(1, 1, 1, 0);
        _panel!.Scale = new Vector2(0.96f, 0.96f);
        _scrim!.Color = new Color(0, 0, 0, 0);

        _activeTween?.Kill();
        _activeTween = CreateTween();
        _activeTween.TweenProperty(_scrim, "color", new Color(0, 0, 0, 0.72f), 0.16f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        _activeTween.Parallel().TweenProperty(_root, "modulate:a", 1.0f, 0.18f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        _activeTween.Parallel().TweenProperty(_panel, "scale", Vector2.One, 0.22f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        _activeTween.TweenInterval(holdDuration);
        _activeTween.TweenProperty(_root, "modulate:a", 0.0f, 0.20f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        _activeTween.Parallel().TweenProperty(_scrim, "color:a", 0.0f, 0.20f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        _activeTween.TweenCallback(Callable.From(FinishActive));
    }

    private void FinishActive()
    {
        var finished = _activeRequest;
        HideOverlay();
        _activeTween = null;
        _activeRequest = null;

        if (finished != null)
        {
            EmitSignal(SignalName.AnimationFinished, finished.Id, (int)finished.Kind);
            finished.FinishedCallback?.Invoke();
        }

        PlayNextQueued();
    }

    private void StopActive(bool emitFinished)
    {
        var stopped = _activeRequest;
        _activeTween?.Kill();
        _activeTween = null;
        _activeRequest = null;
        HideOverlay();

        if (emitFinished && stopped != null)
        {
            EmitSignal(SignalName.AnimationFinished, stopped.Id, (int)stopped.Kind);
            stopped.FinishedCallback?.Invoke();
        }
    }

    private void PlayNextQueued()
    {
        if (_activeRequest != null || _queue.Count == 0) return;
        StartRequest(_queue.Dequeue());
    }

    private void BuildOverlay()
    {
        _root = new Control
        {
            Name = "GlobalAnimationRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        _scrim = new ColorRect
        {
            Name = "Scrim",
            Color = new Color(0, 0, 0, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _scrim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_scrim);

        _panel = new PanelContainer
        {
            Name = "Content",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            PivotOffset = new Vector2(320, 120),
        };
        _panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _panel.CustomMinimumSize = new Vector2(640, 220);
        _root.AddChild(_panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 32);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_right", 32);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        _panel.AddChild(margin);

        var stack = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        margin.AddChild(stack);

        _resourceHost = new TextureRect
        {
            Name = "ResourceHost",
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            CustomMinimumSize = new Vector2(180, 90),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        stack.AddChild(_resourceHost);

        _titleLabel = new Label
        {
            Name = "Title",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 34);
        stack.AddChild(_titleLabel);

        _subtitleLabel = new Label
        {
            Name = "Subtitle",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _subtitleLabel.AddThemeFontSizeOverride("font_size", 18);
        stack.AddChild(_subtitleLabel);
    }

    private void EnsureOverlay()
    {
        if (_root == null || !GodotObject.IsInstanceValid(_root))
            BuildOverlay();
    }

    private void ApplyText(GlobalAnimationRequest request)
    {
        if (_titleLabel == null || _subtitleLabel == null || _panel == null) return;

        _titleLabel.Text = string.IsNullOrWhiteSpace(request.Title)
            ? DefaultTitle(request.Kind)
            : request.Title;
        _subtitleLabel.Text = request.Subtitle;
        _subtitleLabel.Visible = !string.IsNullOrWhiteSpace(request.Subtitle);

        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.055f, 0.052f, 0.88f),
            BorderColor = request.AccentColor,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
        };
        _panel.AddThemeStyleboxOverride("panel", panelStyle);
    }

    private void LoadResourceVisual(string resourcePath)
    {
        if (_resourceHost == null) return;

        _resourceHost.Texture = null;
        _resourceHost.Visible = false;

        if (string.IsNullOrWhiteSpace(resourcePath) || !ResourceLoader.Exists(resourcePath))
            return;

        var texture = GD.Load<Texture2D>(resourcePath);
        if (texture == null) return;

        _resourceHost.Texture = texture;
        _resourceHost.Visible = true;
    }

    private void ShowOverlay()
    {
        if (_root == null) return;
        Visible = true;
        _root.Visible = true;
    }

    private void HideOverlay()
    {
        if (_root != null)
            _root.Visible = false;
        Visible = false;
    }

    private static string DefaultTitle(GlobalAnimationKind kind)
    {
        return kind switch
        {
            GlobalAnimationKind.Opening => "Chapter Begins",
            GlobalAnimationKind.LegendaryEntrance => "Legendary Presence",
            GlobalAnimationKind.WorldEvent => "World Event",
            GlobalAnimationKind.SceneTransition => "Transition",
            _ => "Event",
        };
    }
}
