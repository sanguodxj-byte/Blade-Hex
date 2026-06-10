using Godot;
using System;
using System.IO;
using System.Text;
using BladeHex.Diagnostics;

namespace BladeHex.Debug;

[GlobalClass]
public partial class RuntimeLogConsole : Node
{
    public static RuntimeLogConsole? Instance { get; private set; }

    private const int MaxReadBytes = 96 * 1024;
    private static readonly Key ToggleKey = Key.F2;

    private CanvasLayer? _layer;
    private PanelContainer? _panel;
    private RichTextLabel? _logText;
    private Label? _pathLabel;
    private Button? _errorButton;
    private Button? _warnButton;
    private Button? _debugButton;
    private CheckBox? _autoRefresh;
    private DiagnosticReportLevel _currentLevel = DiagnosticReportLevel.Error;
    private bool _visible;
    private float _refreshAccum;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        SetProcess(true);
        SetProcessUnhandledInput(true);
        BuildUi();
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (!_visible || _autoRefresh?.ButtonPressed != true)
            return;

        _refreshAccum += (float)delta;
        if (_refreshAccum >= 1.0f)
        {
            _refreshAccum = 0f;
            Refresh();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            return;

        if (key.Keycode == ToggleKey)
        {
            ToggleVisible();
            GetViewport().SetInputAsHandled();
        }
    }

    public void ToggleVisible()
    {
        SetVisible(!_visible);
    }

    public void SetVisible(bool visible)
    {
        _visible = visible;
        if (_panel != null)
            _panel.Visible = visible;
        if (visible)
            Refresh();
    }

    private void BuildUi()
    {
        _layer = new CanvasLayer { Name = "RuntimeLogConsoleLayer", Layer = 1200 };
        AddChild(_layer);

        _panel = new PanelContainer { Name = "RuntimeLogConsolePanel", Visible = false };
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _panel.OffsetLeft = 120;
        _panel.OffsetTop = 80;
        _panel.OffsetRight = -120;
        _panel.OffsetBottom = -80;
        _layer.AddChild(_panel);

        var bg = new StyleBoxFlat
        {
            BgColor = new Color(0.03f, 0.035f, 0.04f, 0.94f),
            BorderColor = new Color(0.55f, 0.62f, 0.72f, 0.9f),
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8,
            ContentMarginBottom = 10,
        };
        bg.SetCornerRadiusAll(6);
        bg.SetBorderWidthAll(1);
        _panel.AddThemeStyleboxOverride("panel", bg);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(root);

        var toolbar = new HBoxContainer();
        toolbar.AddThemeConstantOverride("separation", 6);
        root.AddChild(toolbar);

        var title = new Label { Text = "Runtime Logs  F2" };
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", new Color(0.9f, 0.95f, 1.0f));
        toolbar.AddChild(title);

        toolbar.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        _errorButton = MakeLevelButton("Error", DiagnosticReportLevel.Error);
        _warnButton = MakeLevelButton("Warn", DiagnosticReportLevel.Warn);
        _debugButton = MakeLevelButton("Debug", DiagnosticReportLevel.Debug);
        toolbar.AddChild(_errorButton);
        toolbar.AddChild(_warnButton);
        toolbar.AddChild(_debugButton);

        _autoRefresh = new CheckBox { Text = "Auto", ButtonPressed = true };
        toolbar.AddChild(_autoRefresh);

        toolbar.AddChild(MakeButton("Refresh", Refresh));
        toolbar.AddChild(MakeButton("Copy", CopyToClipboard));
        toolbar.AddChild(MakeButton("Close", () => SetVisible(false)));

        _pathLabel = new Label();
        _pathLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _pathLabel.AddThemeFontSizeOverride("font_size", 11);
        _pathLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.68f, 0.76f));
        root.AddChild(_pathLabel);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        root.AddChild(scroll);

        _logText = new RichTextLabel
        {
            BbcodeEnabled = true,
            SelectionEnabled = true,
            ContextMenuEnabled = true,
            ScrollActive = true,
            FitContent = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.Arbitrary,
        };
        _logText.AddThemeFontSizeOverride("normal_font_size", 12);
        _logText.AddThemeColorOverride("default_color", new Color(0.86f, 0.9f, 0.92f));
        scroll.AddChild(_logText);

        UpdateLevelButtons();
    }

    private Button MakeLevelButton(string label, DiagnosticReportLevel level)
    {
        var button = new Button { Text = label, ToggleMode = true };
        button.Pressed += () =>
        {
            _currentLevel = level;
            UpdateLevelButtons();
            Refresh();
        };
        return button;
    }

    private static Button MakeButton(string label, Action callback)
    {
        var button = new Button { Text = label };
        button.Pressed += callback;
        return button;
    }

    private void UpdateLevelButtons()
    {
        if (_errorButton != null) _errorButton.ButtonPressed = _currentLevel == DiagnosticReportLevel.Error;
        if (_warnButton != null) _warnButton.ButtonPressed = _currentLevel == DiagnosticReportLevel.Warn;
        if (_debugButton != null) _debugButton.ButtonPressed = _currentLevel == DiagnosticReportLevel.Debug;
    }

    private void Refresh()
    {
        if (_logText == null || _pathLabel == null)
            return;

        string path = DiagnosticLog.GetLogPathForLevel(_currentLevel);
        _pathLabel.Text = $"{_currentLevel}: {path}";

        try
        {
            string content = ReadTail(path, MaxReadBytes);
            _logText.Text = ToBbcode(content, _currentLevel);
            _logText.ScrollToLine(Math.Max(0, _logText.GetLineCount() - 1));
        }
        catch (Exception ex)
        {
            _logText.Text = $"[color=#ff7777]Failed to read log:[/color] {Escape(ex.Message)}";
        }
    }

    private void CopyToClipboard()
    {
        if (_logText == null)
            return;

        DisplayServer.ClipboardSet(_logText.GetParsedText());
    }

    private static string ReadTail(string path, int maxBytes)
    {
        if (!File.Exists(path))
            return "";

        using var stream = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite);
        long start = Math.Max(0, stream.Length - maxBytes);
        stream.Seek(start, SeekOrigin.Begin);

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        string text = reader.ReadToEnd();
        if (start <= 0)
            return text;

        int firstNewline = text.IndexOf('\n');
        return firstNewline >= 0 ? text[(firstNewline + 1)..] : text;
    }

    private static string ToBbcode(string content, DiagnosticReportLevel level)
    {
        if (string.IsNullOrEmpty(content))
            return "[color=#77808c]No log entries.[/color]";

        var sb = new StringBuilder(content.Length + 256);
        string color = level switch
        {
            DiagnosticReportLevel.Error => "#ff7777",
            DiagnosticReportLevel.Warn => "#ffd45f",
            _ => "#d8e0e7",
        };

        foreach (string rawLine in content.Replace("\r\n", "\n").Split('\n'))
        {
            string line = Escape(rawLine);
            if (line.Contains("[EXCEPTION]") || line.Contains("[ERROR]"))
                sb.Append("[color=#ff7777]").Append(line).AppendLine("[/color]");
            else if (line.Contains("[WARN]"))
                sb.Append("[color=#ffd45f]").Append(line).AppendLine("[/color]");
            else
                sb.Append("[color=").Append(color).Append(']').Append(line).AppendLine("[/color]");
        }

        return sb.ToString();
    }

    private static string Escape(string value)
    {
        return value
            .Replace("[", "[lb]")
            .Replace("]", "[rb]");
    }
}
