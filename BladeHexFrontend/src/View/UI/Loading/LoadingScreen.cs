// LoadingScreen.cs
// RPG 风格加载界面 — 带阶段性描述的条状进度条 + Tips 轮播
using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BladeHex.UI.Loading;

[GlobalClass]
public partial class LoadingScreen : CanvasLayer
{
    [Signal] public delegate void LoadingFinishedEventHandler();

    public enum PhaseType
    {
        NewWorld,     // 新建世界
        LoadSave,     // 加载存档
        Combat,        // 战斗加载
        QuickGame,    // 快速游戏
        QuickCombat,  // 快速战斗
    }

    private const float ProgressSmoothSpeed = 2.0f;

    /// <summary>最小加载展示时长（秒）— 新游戏/快速游戏使用</summary>
    private const float MinLoadDurationSec = 6.0f;

    // UI 引用
    private ColorRect _bg = null!;
    private VBoxContainer _contentContainer = null!;
    private Label _phaseTitleLabel = null!;
    private RichTextLabel _phaseDescLabel = null!;
    private ProgressBar _progressBar = null!;
    private Label _progressPercentLabel = null!;
    private TipsDisplay _tipsDisplay = null!;

    // 数据
    private List<LoadingPhase> _phases = new();
    private LoadingPhase? _currentPhase;
    private float _targetProgress = 0.0f;
    private float _displayedProgress = 0.0f;
    private bool _isLoading = false;
    private string _scenePath = "";
    private Tween? _phaseTween;

    /// <summary>加载开始后的累计时间</summary>
    private float _elapsedTime = 0.0f;
    /// <summary>实际资源是否已加载完成</summary>
    private bool _resourceReady = false;
    /// <summary>当前加载类型是否需要最小时长</summary>
    private bool _useMinDuration = false;

    private static LoadingScreen? _instance;
    private bool _isInitialized = false;

    public static LoadingScreen Instance
    {
        get
        {
            if (_instance == null || !IsInstanceValid(_instance))
            {
                _instance = new LoadingScreen { Name = "LoadingScreen" };
                var root = ((SceneTree)Engine.GetMainLoop()).Root;
                root.AddChild(_instance);
                // _Ready 会在 AddChild 后的下一帧调用，但我们需要立即使用
                // 所以手动确保初始化
                _instance._EnsureInitialized();
            }
            return _instance;
        }
    }

    /// <summary>确保 UI 已构建（可能在 _Ready 之前被调用）</summary>
    private void _EnsureInitialized()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        Layer = 100;
        _BuildUI();
        Visible = false;
    }

    public static void LoadScene(string scenePath, PhaseType phaseType = PhaseType.NewWorld)
    {
        Instance._StartLoading(scenePath, phaseType);
    }

    public override void _Ready()
    {
        _EnsureInitialized();
        _instance = this;
    }

    private void _BuildUI()
    {
        var theme = UITheme.Instance;

        _bg = new ColorRect();
        _bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _bg.Color = theme?.BgPrimary ?? new Color(0.02f, 0.02f, 0.04f);
        AddChild(_bg);

        if (theme == null)
        {
            // 最小化 fallback — 无主题时仍能显示基本加载界面
            _contentContainer = new VBoxContainer();
            AddChild(_contentContainer);
            _phaseTitleLabel = new Label { Text = "加载中..." };
            _contentContainer.AddChild(_phaseTitleLabel);
            _phaseDescLabel = new RichTextLabel();
            _contentContainer.AddChild(_phaseDescLabel);
            _progressBar = new ProgressBar { CustomMinimumSize = new Vector2(400, 16) };
            _contentContainer.AddChild(_progressBar);
            _progressPercentLabel = new Label { Text = "0%" };
            _contentContainer.AddChild(_progressPercentLabel);
            _tipsDisplay = new TipsDisplay();
            AddChild(_tipsDisplay);
            return;
        }

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        _contentContainer = new VBoxContainer();
        _contentContainer.CustomMinimumSize = new Vector2(600, 0);
        _contentContainer.AddThemeConstantOverride("separation", 0);
        center.AddChild(_contentContainer);

        var topSpacer = new Control { CustomMinimumSize = new Vector2(0, 80) };
        _contentContainer.AddChild(topSpacer);

        _phaseTitleLabel = new Label();
        _phaseTitleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _phaseTitleLabel.AddThemeFontSizeOverride("font_size", theme.FontSizeXxl);
        _phaseTitleLabel.AddThemeColorOverride("font_color", theme.TextAccent);
        _contentContainer.AddChild(_phaseTitleLabel);

        var titleGap = new Control { CustomMinimumSize = new Vector2(0, theme.SpacingLg) };
        _contentContainer.AddChild(titleGap);

        _phaseDescLabel = new RichTextLabel();
        _phaseDescLabel.BbcodeEnabled = true;
        _phaseDescLabel.ScrollActive = false;
        _phaseDescLabel.FitContent = true;
        _phaseDescLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _phaseDescLabel.AddThemeFontSizeOverride("normal_font_size", theme.FontSizeMd);
        _phaseDescLabel.AddThemeColorOverride("default_color", theme.TextSecondary);
        _phaseDescLabel.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        _contentContainer.AddChild(_phaseDescLabel);

        _contentContainer.AddChild(new Control { CustomMinimumSize = new Vector2(0, 40) });
        _contentContainer.AddChild(_CreateDecorLine(theme));
        _contentContainer.AddChild(new Control { CustomMinimumSize = new Vector2(0, theme.SpacingLg) });

        var progressVbox = new VBoxContainer();
        progressVbox.AddThemeConstantOverride("separation", theme.SpacingSm);
        _contentContainer.AddChild(progressVbox);

        _progressBar = new ProgressBar();
        _progressBar.CustomMinimumSize = new Vector2(600, 20);
        _progressBar.MinValue = 0.0;
        _progressBar.MaxValue = 100.0;
        _progressBar.Value = 0.0;
        _progressBar.ShowPercentage = false;
        _ApplyProgressBarStyle(theme);
        progressVbox.AddChild(_progressBar);

        _progressPercentLabel = new Label();
        _progressPercentLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _progressPercentLabel.AddThemeFontSizeOverride("font_size", theme.FontSizeSm);
        _progressPercentLabel.AddThemeColorOverride("font_color", theme.TextMuted);
        _progressPercentLabel.Text = "0%";
        progressVbox.AddChild(_progressPercentLabel);

        _contentContainer.AddChild(new Control { CustomMinimumSize = new Vector2(0, theme.SpacingLg) });
        _contentContainer.AddChild(_CreateDecorLine(theme));

        var bottomPusher = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _contentContainer.AddChild(bottomPusher);

        _tipsDisplay = new TipsDisplay();
        _tipsDisplay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        _tipsDisplay.CustomMinimumSize = new Vector2(0, 40);
        _tipsDisplay.OffsetTop = -60;
        _tipsDisplay.OffsetBottom = -20;
        _tipsDisplay.OffsetLeft = 80;
        _tipsDisplay.OffsetRight = -80;
        AddChild(_tipsDisplay);
    }

    private HSeparator _CreateDecorLine(UITheme theme)
    {
        var sep = new HSeparator();
        var style = new StyleBoxFlat();
        style.BgColor = new Color(theme.BorderHighlight.R, theme.BorderHighlight.G, theme.BorderHighlight.B, 0.3f);
        style.SetContentMarginAll(1);
        sep.AddThemeStyleboxOverride("separator", style);
        return sep;
    }

    private void _ApplyProgressBarStyle(UITheme theme)
    {
        var fill = new StyleBoxFlat();
        fill.BgColor = theme.TextAccent;
        fill.SetCornerRadiusAll((int)theme.RadiusSm);
        fill.ShadowColor = new Color(theme.TextAccent.R, theme.TextAccent.G, theme.TextAccent.B, 0.3f);
        fill.ShadowSize = 4;
        _progressBar.AddThemeStyleboxOverride("fill", fill);

        var bg = new StyleBoxFlat();
        bg.BgColor = new Color(0.08f, 0.08f, 0.10f, 0.9f);
        bg.SetBorderWidthAll(1);
        bg.BorderColor = theme.BorderDefault;
        bg.SetCornerRadiusAll((int)theme.RadiusSm);
        _progressBar.AddThemeStyleboxOverride("background", bg);
    }

    private void _StartLoading(string scenePath, PhaseType phaseType)
    {
        _scenePath = scenePath;
        _targetProgress = 0.0f;
        _displayedProgress = 0.0f;
        _elapsedTime = 0.0f;
        _resourceReady = false;
        _isLoading = true;

        // 新游戏和快速游戏使用最小时长，其他类型不限制
        _useMinDuration = phaseType == PhaseType.NewWorld || phaseType == PhaseType.QuickGame;

        var phaseData = new LoadingPhaseData();
        _phases = phaseType switch
        {
            PhaseType.NewWorld => phaseData.GetNewWorldPhases(),
            PhaseType.LoadSave => phaseData.GetLoadSavePhases(),
            PhaseType.Combat => phaseData.GetCombatPhases(),
            PhaseType.QuickGame => phaseData.GetQuickGamePhases(),
            PhaseType.QuickCombat => phaseData.GetQuickCombatPhases(),
            _ => phaseData.GetNewWorldPhases()
        };

        _currentPhase = null;
        Visible = true;
        _tipsDisplay.Start();

        _contentContainer.Modulate = new Color(1, 1, 1, 0);
        var tween = CreateTween();
        tween.TweenProperty(_contentContainer, "modulate:a", 1.0f, 0.3f);

        // 立即开始加载资源（不等淡入动画完成）
        _BeginActualLoad();
    }

    private async void _BeginActualLoad()
    {
        var err = ResourceLoader.LoadThreadedRequest(_scenePath);
        if (err != Error.Ok)
        {
            GD.PushError($"LoadingScreen: 加载失败: {_scenePath}");
            _resourceReady = true; // 标记完成以便退出
            _FinishLoad();
            return;
        }

        // 轮询加载状态，完成后标记 _resourceReady
        while (true)
        {
            var progress = new Godot.Collections.Array();
            var status = ResourceLoader.LoadThreadedGetStatus(_scenePath, progress);

            if (status == ResourceLoader.ThreadLoadStatus.InProgress)
            {
                // 实际加载进度（0~1），用于确保进度条不会超过实际进度
                float realProgress = (float)progress[0];
                // 如果不使用最小时长，直接用实际进度
                if (!_useMinDuration)
                    _targetProgress = realProgress;
            }
            else if (status == ResourceLoader.ThreadLoadStatus.Loaded)
            {
                _resourceReady = true;
                if (!_useMinDuration)
                {
                    _FinishLoad();
                }
                // 使用最小时长时，由 _Process 在时间到达后调用 _FinishLoad
                break;
            }
            else
            {
                GD.PushError($"LoadingScreen: 加载失败: {status}");
                _resourceReady = true;
                _FinishLoad();
                break;
            }
            await Task.Delay(50);
        }
    }

    private void _FinishLoad()
    {
        _isLoading = false;
        _targetProgress = 1.0f;
        _displayedProgress = 1.0f;
        _progressBar.Value = 100.0;
        _progressPercentLabel.Text = "100%";
        _tipsDisplay.ShowLastTip();
        
        _DoSceneTransition();
    }

    private void _DoSceneTransition()
    {
        var resource = (PackedScene)ResourceLoader.LoadThreadedGet(_scenePath);

        // 切换前清理 /root 下的游离节点（手动 AddChild 上去的旧战斗场景等）
        BladeHex.View.SceneTransition.CleanupOrphanNodes(GetTree());

        // 不立即淡出 — 新场景的 _Ready() 可能耗时
        // 切换场景后 LoadingScreen 保持可见（Layer=100 覆盖一切）
        GetTree().ChangeSceneToPacked(resource);

        // 新场景初始化完成后会调用 LoadingScreen.NotifySceneReady() 触发淡出
        // 如果 3 秒内没收到通知，自动淡出（兜底）
        _sceneReadyTimeout = 3.0f;
        _waitingForSceneReady = true;
    }

    private bool _waitingForSceneReady = false;
    private float _sceneReadyTimeout = 0f;

    /// <summary>
    /// 由新场景在初始化完成后调用，通知 LoadingScreen 可以淡出。
    /// </summary>
    public static void NotifySceneReady()
    {
        if (_instance != null && _instance._waitingForSceneReady)
        {
            _instance._waitingForSceneReady = false;
            _instance._FadeOut();
        }
    }

    private void _FadeOut()
    {
        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(_contentContainer, "modulate:a", 0.0f, 0.5f);
        tween.TweenProperty(_bg, "color:a", 0.0f, 0.5f);
        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(() =>
        {
            Visible = false;
            _tipsDisplay.Stop();
            EmitSignal(SignalName.LoadingFinished);
        }));
    }

    public override void _Process(double delta)
    {
        if (!_isLoading && !Visible) return;

        // 等待新场景初始化完成的超时检查
        if (_waitingForSceneReady)
        {
            _sceneReadyTimeout -= (float)delta;
            if (_sceneReadyTimeout <= 0f)
            {
                _waitingForSceneReady = false;
                _FadeOut();
            }
            return;
        }

        _elapsedTime += (float)delta;

        // 模拟进度：在最小时长内从 0→1 平滑推进
        if (_useMinDuration && _isLoading)
        {
            // 基于时间的模拟进度（使用缓动曲线让前期快后期慢，更自然）
            float timeRatio = Mathf.Clamp(_elapsedTime / MinLoadDurationSec, 0.0f, 1.0f);
            // 使用 ease-out 曲线：前 80% 时间走到 95%，最后 20% 时间走完剩余 5%
            float simulatedProgress = timeRatio < 0.8f
                ? (timeRatio / 0.8f) * 0.95f
                : 0.95f + ((timeRatio - 0.8f) / 0.2f) * 0.05f;

            _targetProgress = simulatedProgress;

            // 时间到达且资源已就绪 → 完成加载
            if (_elapsedTime >= MinLoadDurationSec && _resourceReady)
            {
                _FinishLoad();
                return;
            }
        }

        _displayedProgress = Mathf.Lerp(_displayedProgress, _targetProgress, (float)(ProgressSmoothSpeed * delta));

        float percent = _displayedProgress * 100.0f;
        _progressBar.Value = percent;
        _progressPercentLabel.Text = $"{(int)Mathf.Round(percent)}%";

        _UpdatePhaseText(_displayedProgress);
    }

    private void _UpdatePhaseText(float progress)
    {
        var phase = LoadingPhaseData.GetPhaseAtProgress(_phases, progress);
        if (phase == null) return;

        if (_currentPhase == null || _currentPhase.Title != phase.Title)
        {
            _currentPhase = phase;
            _AnimatePhaseChange(phase);
        }
    }

    private void _AnimatePhaseChange(LoadingPhase phase)
    {
        if (_phaseTween != null && _phaseTween.IsValid()) _phaseTween.Kill();

        _phaseTween = CreateTween();
        float fadeOut = 0.2f;
        float fadeIn = 0.3f;
        float hold = 0.7f;

        _phaseTween.SetParallel(true);
        _phaseTween.TweenProperty(_phaseTitleLabel, "modulate:a", 0.0f, fadeOut);
        _phaseTween.TweenProperty(_phaseDescLabel, "modulate:a", 0.0f, fadeOut);
        _phaseTween.SetParallel(false);

        _phaseTween.TweenCallback(Callable.From(() =>
        {
            _phaseTitleLabel.Text = phase.Title;
            _phaseDescLabel.Text = $"[center][i]{phase.Description}[/i][/center]";
        }));

        _phaseTween.SetParallel(true);
        _phaseTween.TweenProperty(_phaseTitleLabel, "modulate:a", 1.0f, fadeIn);
        _phaseTween.TweenProperty(_phaseDescLabel, "modulate:a", 1.0f, fadeIn);
        _phaseTween.SetParallel(false);

        _phaseTween.TweenInterval(hold);
    }
}
