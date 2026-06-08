using BladeHex.UI;
using BladeHex.View.AssetSystem;
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class OverworldDayNightClock : Control
{
    private const float ClockSize = 240f;
    private const float WheelSize = 220f;
    private const float PanelItemHeight = 40f;
    private const float PanelCenterOffsetY = 7.5f;

    private static readonly IReadOnlyList<string> SeasonLabels =
    [
        "春季",
        "夏季",
        "秋季",
        "冬季",
    ];

    private static readonly IReadOnlyList<string> WeatherLabels =
    [
        "晴朗",
        "雨天",
        "下雪",
        "多云",
        "起雾",
        "刮风",
    ];

    private TextureRect? _dayNightWheel;
    private TextureRect? _dayNightFrame;
    private PanelContainer _seasonPanel = null!;
    private VBoxContainer _seasonBox = null!;
    private PanelContainer _weatherPanel = null!;
    private VBoxContainer _weatherBox = null!;
    private SimpleFloatingTooltip _clockTooltip = null!;

    private float _targetRotationDegrees;
    private float _targetSeasonY = PanelCenterOffsetY;
    private float _targetWeatherY = PanelCenterOffsetY;
    private string _currentDate = "第1年 第1月 第1日";
    private string _currentTime = "06:00";
    private string _currentSeason = "春季";
    private string _currentWeather = "晴朗";

    public void Initialize()
    {
        CustomMinimumSize = new Vector2(ClockSize, ClockSize);
        Size = new Vector2(ClockSize, ClockSize);
        MouseFilter = MouseFilterEnum.Ignore;

        _dayNightWheel = CreateTextureRect(
            TextureAssetResolver.LoadUiTexture("DayNight_Wheel", "res://BladeHexFrontend/src/assets/ui/DayNight_Wheel.png"),
            WheelSize,
            MouseFilterEnum.Ignore);
        _dayNightWheel.PivotOffset = new Vector2(WheelSize / 2.0f, WheelSize / 2.0f);
        _dayNightWheel.Position = new Vector2((ClockSize - WheelSize) / 2.0f, (ClockSize - WheelSize) / 2.0f);
        AddChild(_dayNightWheel);

        _dayNightFrame = CreateTextureRect(
            TextureAssetResolver.LoadUiTexture("DayNight_Frame", "res://BladeHexFrontend/src/assets/ui/DayNight_Frame.png"),
            ClockSize,
            MouseFilterEnum.Stop);
        _dayNightFrame.Position = Vector2.Zero;
        AddChild(_dayNightFrame);

        _clockTooltip = new SimpleFloatingTooltip { Name = "ClockTooltip" };
        AddChild(_clockTooltip);

        _dayNightFrame.MouseEntered += OnClockHover;
        _dayNightFrame.MouseExited += OnClockUnhover;

        _seasonPanel = CreateScrollPanel(new Vector2(-90, 101f));
        _seasonBox = CreateLabelStack(SeasonLabels);
        _seasonPanel.AddChild(_seasonBox);
        AddChild(_seasonPanel);

        _weatherPanel = CreateScrollPanel(new Vector2(ClockSize + 15, 101f));
        _weatherBox = CreateLabelStack(WeatherLabels);
        _weatherPanel.AddChild(_weatherBox);
        AddChild(_weatherPanel);

        CenterAtTopOfViewport();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        if (_dayNightWheel != null)
        {
            float target = Mathf.DegToRad(_targetRotationDegrees);
            _dayNightWheel.Rotation = Mathf.LerpAngle(_dayNightWheel.Rotation, target, dt * 6.0f);
        }

        if (IsInsideTree())
            CenterAtTopOfViewport();

        if (_seasonBox != null)
            _seasonBox.Position = LerpY(_seasonBox.Position, _targetSeasonY, dt);

        if (_weatherBox != null)
            _weatherBox.Position = LerpY(_weatherBox.Position, _targetWeatherY, dt);
    }

    public void UpdateSeason(string season)
    {
        _currentSeason = string.IsNullOrWhiteSpace(season) ? "春季" : season;
        int index = ResolveSeasonIndex(season);
        _targetSeasonY = PanelCenterOffsetY - index * PanelItemHeight;
    }

    public void UpdateWeather(string weatherText)
    {
        _currentWeather = string.IsNullOrWhiteSpace(weatherText) ? "晴朗" : weatherText;
        int index = ResolveWeatherIndex(weatherText);
        _targetWeatherY = PanelCenterOffsetY - index * PanelItemHeight;
    }

    public void SetTime(string clock)
    {
        _currentTime = string.IsNullOrWhiteSpace(clock) ? "00:00" : clock;
        if (_dayNightWheel == null)
            return;

        var parts = _currentTime.Split(':');
        if (parts.Length == 0 || !float.TryParse(parts[0], out float hour))
            return;

        float minutes = 0f;
        if (parts.Length > 1)
            float.TryParse(parts[1], out minutes);

        float totalHour = hour + minutes / 60.0f;
        _targetRotationDegrees = -(totalHour / 24.0f) * 360.0f;
    }

    public void UpdateDate(string dateText)
    {
        _currentDate = string.IsNullOrWhiteSpace(dateText) ? _currentDate : dateText;
    }

    private static TextureRect CreateTextureRect(Texture2D? texture, float size, MouseFilterEnum mouseFilter)
    {
        return new TextureRect
        {
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            Size = new Vector2(size, size),
            MouseFilter = mouseFilter,
        };
    }

    private static PanelContainer CreateScrollPanel(Vector2 position)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(75, 45),
            Size = new Vector2(75, 45),
            Position = position,
            ClipContents = true,
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        return panel;
    }

    private static VBoxContainer CreateLabelStack(IReadOnlyList<string> labels)
    {
        var box = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
        };
        box.AddThemeConstantOverride("separation", 10);

        foreach (var text in labels)
        {
            var label = new Label
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                CustomMinimumSize = new Vector2(64, 30),
            };
            label.AddThemeFontSizeOverride("font_size", 14);
            label.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.55f));
            box.AddChild(label);
        }

        return box;
    }

    private void CenterAtTopOfViewport()
    {
        float width = GetViewport().GetVisibleRect().Size.X;
        Position = new Vector2(width / 2.0f - ClockSize / 2.0f, -86f);
    }

    private static Vector2 LerpY(Vector2 current, float targetY, float delta)
    {
        current.Y = Mathf.Lerp(current.Y, targetY, delta * 5.0f);
        return current;
    }

    private static int ResolveSeasonIndex(string value)
    {
        string normalized = value?.Trim().ToLowerInvariant() ?? "";
        if (normalized.Contains("summer") || normalized.Contains("\u590f"))
            return 1;
        if (normalized.Contains("autumn") || normalized.Contains("fall") || normalized.Contains("\u79cb"))
            return 2;
        if (normalized.Contains("winter") || normalized.Contains("\u51ac"))
            return 3;
        return 0;
    }

    private static int ResolveWeatherIndex(string value)
    {
        string normalized = value?.Trim().ToLowerInvariant() ?? "";
        if (normalized.Contains("rain") || normalized.Contains("\u96e8"))
            return 1;
        if (normalized.Contains("snow") || normalized.Contains("\u96ea"))
            return 2;
        if (normalized.Contains("cloud") || normalized.Contains("overcast") || normalized.Contains("\u9634") || normalized.Contains("\u4e91"))
            return 3;
        if (normalized.Contains("fog") || normalized.Contains("\u96fe"))
            return 4;
        if (normalized.Contains("wind") || normalized.Contains("storm") || normalized.Contains("\u98ce") || normalized.Contains("\u66b4"))
            return 5;
        return 0;
    }

    private void OnClockHover()
    {
        _clockTooltip.SetText($"Date: {_currentDate}\nTime: {_currentTime}\nSeason: {_currentSeason}\nWeather: {_currentWeather}");
        _clockTooltip.ShowAtMouse();
    }

    private void OnClockUnhover()
    {
        _clockTooltip.HidePanel();
    }
}
