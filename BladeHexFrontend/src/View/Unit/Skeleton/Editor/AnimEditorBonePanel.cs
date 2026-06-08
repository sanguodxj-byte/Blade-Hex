// AnimEditorBonePanel.cs
// 运行时骨骼动画编辑器 — 骨骼列表 + 属性面板
// 左侧面板：7 个骨骼按钮，选中后显示旋转/位移滑条
using Godot;
using System.Collections.Generic;

namespace BladeHex.View.Unit.Skeleton.Editor;

/// <summary>骨骼选择与属性编辑面板</summary>
public partial class AnimEditorBonePanel : PanelContainer
{
    [Signal] public delegate void BonePoseChangedEventHandler(string boneName, float rotationZ, float positionY, float positionX, float spriteRotation, float scaleX, float scaleY, string easingName);
    [Signal] public delegate void BoneSelectedEventHandler(string boneName);

    private string _selectedBone = "Torso";
    private HSlider _rotSlider = null!;
    private HSlider _posSlider = null!;
    private HSlider _posXSlider = null!;
    private HSlider _spriteRotSlider = null!;
    private HSlider _scaleXSlider = null!;
    private HSlider _scaleYSlider = null!;
    private OptionButton _easingSelect = null!;

    private Label _rotLabel = null!;
    private Label _posLabel = null!;
    private Label _posXLabel = null!;
    private Label _spriteRotLabel = null!;
    private Label _scaleXLabel = null!;
    private Label _scaleYLabel = null!;
    private Label _easingLabel = null!;
    private Label _selectedLabel = null!;

    private VBoxContainer _posRow = null!;
    private VBoxContainer _posXRow = null!;
    private VBoxContainer _spriteRotRow = null!;
    private readonly Dictionary<string, Button> _boneButtons = new();

    private bool _suppressEvents;

    public string SelectedBone => _selectedBone;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(200, 0);
        SizeFlagsVertical = SizeFlags.ExpandFill;

        var style = new StyleBoxFlat { BgColor = new Color(0.06f, 0.06f, 0.08f, 0.9f) };
        style.SetContentMarginAll(8);
        AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        AddChild(vbox);

        // 标题
        var title = new Label { Text = "骨骼", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 14);
        title.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.5f));
        vbox.AddChild(title);

        vbox.AddChild(new HSeparator());

        // 骨骼按钮
        foreach (var bone in AnimClip.BoneNames)
        {
            var btn = new Button { Text = BoneDisplayName(bone), ToggleMode = true };
            btn.CustomMinimumSize = new Vector2(0, 28);
            btn.AddThemeFontSizeOverride("font_size", 12);
            btn.Pressed += () => SelectBone(bone);
            vbox.AddChild(btn);
            _boneButtons[bone] = btn;
        }

        vbox.AddChild(new HSeparator());

        // 选中显示
        _selectedLabel = new Label { Text = "选中: Torso" };
        _selectedLabel.AddThemeFontSizeOverride("font_size", 12);
        _selectedLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.9f, 1f));
        vbox.AddChild(_selectedLabel);

        // 旋转滑条
        var rotRow = new VBoxContainer();
        vbox.AddChild(rotRow);
        _rotLabel = new Label { Text = "旋转Z: 0°" };
        _rotLabel.AddThemeFontSizeOverride("font_size", 11);
        rotRow.AddChild(_rotLabel);

        _rotSlider = new HSlider
        {
            MinValue = -360,
            MaxValue = 360,
            Step = 1,
            Value = 0,
            CustomMinimumSize = new Vector2(0, 20),
        };
        _rotSlider.ValueChanged += OnRotChanged;
        rotRow.AddChild(_rotSlider);

        // 位移Y滑条（Torso + Weapon）
        _posRow = new VBoxContainer();
        vbox.AddChild(_posRow);
        _posLabel = new Label { Text = "位移Y: 0" };
        _posLabel.AddThemeFontSizeOverride("font_size", 11);
        _posRow.AddChild(_posLabel);

        _posSlider = new HSlider
        {
            MinValue = -500,
            MaxValue = 500,
            Step = 1,
            Value = 0,
            CustomMinimumSize = new Vector2(0, 20),
        };
        _posSlider.ValueChanged += OnPosChanged;
        _posRow.AddChild(_posSlider);

        // 位移X滑条（Weapon）
        _posXRow = new VBoxContainer();
        vbox.AddChild(_posXRow);
        _posXLabel = new Label { Text = "位移X: 0" };
        _posXLabel.AddThemeFontSizeOverride("font_size", 11);
        _posXRow.AddChild(_posXLabel);

        _posXSlider = new HSlider
        {
            MinValue = -500,
            MaxValue = 500,
            Step = 1,
            Value = 0,
            CustomMinimumSize = new Vector2(0, 20),
        };
        _posXSlider.ValueChanged += OnPosXChanged;
        _posXRow.AddChild(_posXSlider);

        // Sprite旋转滑条（Weapon）
        _spriteRotRow = new VBoxContainer();
        vbox.AddChild(_spriteRotRow);
        _spriteRotLabel = new Label { Text = "图片旋转: 0°" };
        _spriteRotLabel.AddThemeFontSizeOverride("font_size", 11);
        _spriteRotRow.AddChild(_spriteRotLabel);

        _spriteRotSlider = new HSlider
        {
            MinValue = -360,
            MaxValue = 360,
            Step = 5,
            Value = 0,
            CustomMinimumSize = new Vector2(0, 20),
        };
        _spriteRotSlider.ValueChanged += OnSpriteRotChanged;
        _spriteRotRow.AddChild(_spriteRotSlider);

        vbox.AddChild(new HSeparator());

        // 缩放X滑条
        var scaleXRow = new VBoxContainer();
        vbox.AddChild(scaleXRow);
        _scaleXLabel = new Label { Text = "缩放X: 1.00" };
        _scaleXLabel.AddThemeFontSizeOverride("font_size", 11);
        scaleXRow.AddChild(_scaleXLabel);

        _scaleXSlider = new HSlider
        {
            MinValue = 0.1,
            MaxValue = 5.0,
            Step = 0.05,
            Value = 1.0,
            CustomMinimumSize = new Vector2(0, 20),
        };
        _scaleXSlider.ValueChanged += OnScaleXChanged;
        scaleXRow.AddChild(_scaleXSlider);

        // 缩放Y滑条
        var scaleYRow = new VBoxContainer();
        vbox.AddChild(scaleYRow);
        _scaleYLabel = new Label { Text = "缩放Y: 1.00" };
        _scaleYLabel.AddThemeFontSizeOverride("font_size", 11);
        scaleYRow.AddChild(_scaleYLabel);

        _scaleYSlider = new HSlider
        {
            MinValue = 0.1,
            MaxValue = 5.0,
            Step = 0.05,
            Value = 1.0,
            CustomMinimumSize = new Vector2(0, 20),
        };
        _scaleYSlider.ValueChanged += OnScaleYChanged;
        scaleYRow.AddChild(_scaleYSlider);

        // Easing物理缓动
        var easingRow = new VBoxContainer();
        vbox.AddChild(easingRow);
        _easingLabel = new Label { Text = "帧物理缓动:" };
        _easingLabel.AddThemeFontSizeOverride("font_size", 11);
        easingRow.AddChild(_easingLabel);

        _easingSelect = new OptionButton { CustomMinimumSize = new Vector2(0, 24) };
        _easingSelect.AddItem("Linear (匀速)");
        _easingSelect.AddItem("QuadOut (物理减速)");
        _easingSelect.AddItem("BackOut (物理回弹)");
        _easingSelect.AddItem("Bounce (重击余震)");
        _easingSelect.ItemSelected += OnEasingSelected;
        easingRow.AddChild(_easingSelect);

        // 初始选中
        SelectBone("Torso");
    }

    /// <summary>选中骨骼</summary>
    public void SelectBone(string boneName)
    {
        _selectedBone = boneName;
        _selectedLabel.Text = $"选中: {BoneDisplayName(boneName)}";
        bool isWeapon = boneName == "Weapon";

        // 所有骨骼都显示位移滑条
        _posRow.Visible = true;
        _posXRow.Visible = true;
        _spriteRotRow.Visible = isWeapon;

        // 更新标签文字
        if (isWeapon)
        {
            _posLabel.Text = "握持点Y: 0";
            _posXLabel.Text = "握持点X: 0";
        }
        else
        {
            _posLabel.Text = "位移Y: 0";
            _posXLabel.Text = "位移X: 0";
        }

        // 更新按钮状态
        foreach (var (name, btn) in _boneButtons)
            btn.ButtonPressed = name == boneName;

        EmitSignal(SignalName.BoneSelected, boneName);
    }

    /// <summary>设置当前显示的姿态值（切换帧时调用）</summary>
    public void SetDisplayPose(BonePose pose)
    {
        _suppressEvents = true;
        _rotSlider.Value = pose.RotationZ;
        _rotLabel.Text = $"旋转Z: {pose.RotationZ:F0}°";
        _posSlider.Value = pose.PositionY;
        _posLabel.Text = $"位移Y: {pose.PositionY:F0}";
        _posXSlider.Value = pose.PositionX;
        _posXLabel.Text = $"位移X: {pose.PositionX:F0}";
        _spriteRotSlider.Value = pose.SpriteRotation;
        _spriteRotLabel.Text = $"图片旋转: {pose.SpriteRotation:F0}°";

        // 升级高精物理通道参数显示
        _scaleXSlider.Value = pose.ScaleX;
        _scaleXLabel.Text = $"缩放X: {pose.ScaleX:F2}";
        _scaleYSlider.Value = pose.ScaleY;
        _scaleYLabel.Text = $"缩放Y: {pose.ScaleY:F2}";
        _easingSelect.Selected = (int)pose.Easing;

        _suppressEvents = false;
    }

    private void OnRotChanged(double value)
    {
        if (_suppressEvents) return;
        _rotLabel.Text = $"旋转Z: {value:F0}°";
        EmitPoseChanged();
    }

    private void OnPosChanged(double value)
    {
        if (_suppressEvents) return;
        _posLabel.Text = $"位移Y: {value:F0}";
        EmitPoseChanged();
    }

    private void OnPosXChanged(double value)
    {
        if (_suppressEvents) return;
        _posXLabel.Text = $"位移X: {value:F0}";
        EmitPoseChanged();
    }

    private void OnSpriteRotChanged(double value)
    {
        if (_suppressEvents) return;
        _spriteRotLabel.Text = $"图片旋转: {value:F0}°";
        EmitPoseChanged();
    }

    private void OnScaleXChanged(double value)
    {
        if (_suppressEvents) return;
        _scaleXLabel.Text = $"缩放X: {value:F2}";
        EmitPoseChanged();
    }

    private void OnScaleYChanged(double value)
    {
        if (_suppressEvents) return;
        _scaleYLabel.Text = $"缩放Y: {value:F2}";
        EmitPoseChanged();
    }

    private void OnEasingSelected(long index)
    {
        if (_suppressEvents) return;
        EmitPoseChanged();
    }

    private void EmitPoseChanged()
    {
        string easingName = ((EasingType)_easingSelect.Selected).ToString();
        EmitSignal(SignalName.BonePoseChanged, _selectedBone,
            (float)_rotSlider.Value, (float)_posSlider.Value,
            (float)_posXSlider.Value, (float)_spriteRotSlider.Value,
            (float)_scaleXSlider.Value, (float)_scaleYSlider.Value,
            easingName);
    }

    private static string BoneDisplayName(string bone) => bone switch
    {
        "Torso" => "躯干",
        "Head" => "头部",
        "ArmL" => "左上臂",
        "ArmR" => "右上臂",
        "ForearmL" => "左前臂",
        "ForearmR" => "右前臂",
        "Weapon" => "武器",
        _ => bone,
    };
}
