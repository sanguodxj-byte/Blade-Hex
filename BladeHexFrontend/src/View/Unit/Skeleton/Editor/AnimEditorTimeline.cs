// AnimEditorTimeline.cs
// 运行时骨骼动画编辑器 — 时间轴 UI
// 底部时间轴条：播放头、关键帧菱形标记、播放控制按钮、增删帧
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.View.Unit.Skeleton.Editor;

/// <summary>时间轴控件</summary>
public partial class AnimEditorTimeline : Control
{
    [Signal] public delegate void TimeChangedEventHandler(float time);
    [Signal] public delegate void KeyframeSelectedEventHandler(int index);
    [Signal] public delegate void PlayPressedEventHandler();
    [Signal] public delegate void PausePressedEventHandler();
    [Signal] public delegate void StepForwardPressedEventHandler();
    [Signal] public delegate void AddKeyframePressedEventHandler();
    [Signal] public delegate void RemoveKeyframePressedEventHandler();

    private const float TimelineHeight = 60f;
    private const float TrackY = 35f;
    private const float DiamondSize = 8f;
    private const float MarginLeft = 80f;
    private const float MarginRight = 20f;

    private float _duration = 1.0f;
    private float _currentTime;
    private int _selectedKeyframe = -1;
    private List<float> _keyframeTimes = new();
    private bool _draggingPlayhead;

    // 按钮
    private Button _btnPlay = null!;
    private Button _btnPause = null!;
    private Button _btnStep = null!;
    private Button _btnAddKf = null!;
    private Button _btnRemoveKf = null!;

    public float CurrentTime
    {
        get => _currentTime;
        set { _currentTime = Math.Clamp(value, 0, _duration); QueueRedraw(); }
    }

    public int SelectedKeyframe
    {
        get => _selectedKeyframe;
        set { _selectedKeyframe = value; QueueRedraw(); }
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, TimelineHeight + 30);

        // 按钮行
        var hbox = new HBoxContainer();
        hbox.Position = new Vector2(4, 2);
        hbox.AddThemeConstantOverride("separation", 4);
        AddChild(hbox);

        _btnPlay = MakeBtn("▶", "播放");
        _btnPlay.Pressed += () => EmitSignal(SignalName.PlayPressed);
        hbox.AddChild(_btnPlay);

        _btnPause = MakeBtn("⏸", "暂停");
        _btnPause.Pressed += () => EmitSignal(SignalName.PausePressed);
        hbox.AddChild(_btnPause);

        _btnStep = MakeBtn("▷|", "前进一帧");
        _btnStep.Pressed += () => EmitSignal(SignalName.StepForwardPressed);
        hbox.AddChild(_btnStep);

        hbox.AddChild(new VSeparator());

        _btnAddKf = MakeBtn("+帧", "在当前时间添加关键帧");
        _btnAddKf.Pressed += () => EmitSignal(SignalName.AddKeyframePressed);
        hbox.AddChild(_btnAddKf);

        _btnRemoveKf = MakeBtn("−帧", "删除选中关键帧");
        _btnRemoveKf.Pressed += () => EmitSignal(SignalName.RemoveKeyframePressed);
        hbox.AddChild(_btnRemoveKf);
    }

    /// <summary>更新时间轴数据</summary>
    public void SetData(float duration, List<float> keyframeTimes)
    {
        _duration = Math.Max(0.01f, duration);
        _keyframeTimes = keyframeTimes;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var size = Size;
        float trackWidth = size.X - MarginLeft - MarginRight;
        if (trackWidth <= 0) return;

        // 背景
        DrawRect(new Rect2(0, 24, size.X, TimelineHeight), new Color(0.08f, 0.08f, 0.1f, 0.9f));

        // 轨道线
        float y = 24 + TrackY;
        DrawLine(new Vector2(MarginLeft, y), new Vector2(MarginLeft + trackWidth, y),
            new Color(0.4f, 0.4f, 0.4f), 2);

        // 刻度
        int tickCount = Math.Max(2, (int)(_duration * 10));
        tickCount = Math.Min(tickCount, 20);
        for (int i = 0; i <= tickCount; i++)
        {
            float t = (float)i / tickCount;
            float x = MarginLeft + t * trackWidth;
            float tickH = (i % 5 == 0) ? 8 : 4;
            DrawLine(new Vector2(x, y - tickH), new Vector2(x, y + tickH), new Color(0.3f, 0.3f, 0.3f));

            if (i % 5 == 0)
            {
                string label = $"{t * _duration:F1}s";
                DrawString(ThemeDB.FallbackFont, new Vector2(x - 10, y + 20),
                    label, HorizontalAlignment.Left, -1, 10, new Color(0.5f, 0.5f, 0.5f));
            }
        }

        // 关键帧菱形
        for (int i = 0; i < _keyframeTimes.Count; i++)
        {
            float t = _keyframeTimes[i] / _duration;
            float x = MarginLeft + t * trackWidth;
            var color = (i == _selectedKeyframe) ? new Color(1f, 0.8f, 0.2f) : new Color(0.8f, 0.8f, 0.8f);
            DrawDiamond(new Vector2(x, y), DiamondSize, color);
        }

        // 播放头
        float playX = MarginLeft + (_currentTime / _duration) * trackWidth;
        DrawLine(new Vector2(playX, 24), new Vector2(playX, 24 + TimelineHeight), new Color(1f, 0.3f, 0.3f), 2);
        // 播放头三角
        var tri = new Vector2[] {
            new(playX - 5, 24),
            new(playX + 5, 24),
            new(playX, 30),
        };
        DrawColoredPolygon(tri, new Color(1f, 0.3f, 0.3f));
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            var pos = mb.Position;
            float trackWidth = Size.X - MarginLeft - MarginRight;
            float y = 24 + TrackY;

            // 检查是否点击了关键帧菱形
            for (int i = 0; i < _keyframeTimes.Count; i++)
            {
                float t = _keyframeTimes[i] / _duration;
                float x = MarginLeft + t * trackWidth;
                if (pos.DistanceTo(new Vector2(x, y)) < DiamondSize + 4)
                {
                    _selectedKeyframe = i;
                    _currentTime = _keyframeTimes[i];
                    EmitSignal(SignalName.KeyframeSelected, i);
                    EmitSignal(SignalName.TimeChanged, _currentTime);
                    QueueRedraw();
                    AcceptEvent();
                    return;
                }
            }

            // 点击轨道区域 → 移动播放头
            if (pos.X >= MarginLeft && pos.X <= MarginLeft + trackWidth && pos.Y >= 24 && pos.Y <= 24 + TimelineHeight)
            {
                _draggingPlayhead = true;
                UpdatePlayheadFromMouse(pos.X);
                AcceptEvent();
            }
        }

        if (@event is InputEventMouseButton mbUp && !mbUp.Pressed && mbUp.ButtonIndex == MouseButton.Left)
        {
            _draggingPlayhead = false;
        }

        if (@event is InputEventMouseMotion motion && _draggingPlayhead)
        {
            UpdatePlayheadFromMouse(motion.Position.X);
            AcceptEvent();
        }
    }

    private void UpdatePlayheadFromMouse(float mouseX)
    {
        float trackWidth = Size.X - MarginLeft - MarginRight;
        float t = (mouseX - MarginLeft) / trackWidth;
        t = Math.Clamp(t, 0, 1);
        _currentTime = t * _duration;
        EmitSignal(SignalName.TimeChanged, _currentTime);
        QueueRedraw();
    }

    private void DrawDiamond(Vector2 center, float size, Color color)
    {
        var points = new Vector2[]
        {
            center + new Vector2(0, -size),
            center + new Vector2(size, 0),
            center + new Vector2(0, size),
            center + new Vector2(-size, 0),
        };
        DrawColoredPolygon(points, color);
    }

    private static Button MakeBtn(string text, string tooltip)
    {
        var btn = new Button
        {
            Text = text,
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(36, 22),
        };
        btn.AddThemeFontSizeOverride("font_size", 12);
        return btn;
    }
}
