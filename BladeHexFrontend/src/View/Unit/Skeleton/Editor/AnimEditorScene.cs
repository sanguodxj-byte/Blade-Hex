// AnimEditorScene.cs
// 运行时骨骼动画编辑器 — 主场景控制器
// 组装预览区、时间轴、骨骼面板，处理交互逻辑
// 挂载到 skeleton_preview.tscn 替代原 SkeletonPreview.cs
using Godot;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;

namespace BladeHex.View.Unit.Skeleton.Editor;

/// <summary>骨骼动画编辑器主控制器</summary>
public partial class AnimEditorScene : Node3D
{
    // ─── 子组件 ───
    private AnimEditorPreview _preview = null!;
    private AnimEditorTimeline _timeline = null!;
    private AnimEditorBonePanel _bonePanel = null!;
    private AnimEditorTexturePanel _texturePanel = null!;

    // ─── 状态 ───
    private AnimClip _clip = null!;
    private int _selectedKeyframeIdx = -1;
    private WeaponAnimCategory _currentWeaponCat = WeaponAnimCategory.Slash;
    private OptionButton _animSelect = null!;
    private OptionButton _bodyTypeSelect = null!;
    private OptionButton _weaponCatSelect = null!;
    private LineEdit _nameInput = null!;
    private SpinBox _durationSpin = null!;
    private CheckBox _loopCheck = null!;
    private Label _statusLabel = null!;

    // ─── 相机 ───
    private Camera3D? _cam;
    private const float MinOrtho = 80f;
    private const float MaxOrtho = 600f;
    private bool _middleDrag;

    // ─── 骨骼拖拽 ───
    private bool _draggingGrip;
    private string _dragBoneName = "";
    private Vector2 _dragStartMouse;
    private float _dragStartRotZ;
    private const float DragSensitivity = 0.5f; // 度/像素
    private const float GizmoHitRadius = 20f; // 屏幕像素半径

    // ─── 部件偏移模式（统一管理武器/护甲/头盔/手甲） ───
    private bool _equipOffsetMode;
    private bool _facingLeft;
    private EquipmentOffsetConfig _currentEquipOffset = null!;
    private ItemData.EquipSlot _currentOffsetSlot = ItemData.EquipSlot.Weapon;
    private CheckBox _equipOffsetModeCheck = null!;
    private OptionButton _equipSlotSelect = null!;
    private CheckBox _hideTexturesCheck = null!;

    // ─── 内置动画模板 ───
    private readonly Dictionary<string, System.Func<AnimClip>> _templates = new()
    {
        ["idle"] = AnimClip.CreateDefaultIdle,
        ["attack_melee"] = AnimClip.CreateDefaultAttackMelee,
    };

    public override void _Ready()
    {
        // 相机（与战斗场景一致的 -45° 俯视）
        _cam = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = 200f,
            RotationDegrees = new Vector3(-45, 0, 0),
            Position = new Vector3(0, 180, 200),
            Current = true,
        };
        AddChild(_cam);

        // 灯光
        var light = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-50, -30, 0),
            LightEnergy = 1.2f,
        };
        AddChild(light);

        // 地面
        var ground = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(500, 500) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.35f, 0.3f) },
        };
        AddChild(ground);

        // 3D 预览
        _preview = new AnimEditorPreview();
        AddChild(_preview);

        // 初始动画
        _clip = AnimClip.CreateDefaultIdle();
        _preview.CurrentClip = _clip;

        // 初始部件偏移配置
        _currentEquipOffset = EquipmentOffsetConfig.Get(_currentOffsetSlot);

        // UI 层
        var uiLayer = new CanvasLayer { Layer = 10 };
        AddChild(uiLayer);

        var uiRoot = new Control();
        uiRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        uiRoot.MouseFilter = Control.MouseFilterEnum.Ignore;
        uiLayer.AddChild(uiRoot);

        BuildTopBar(uiRoot);
        BuildBonePanel(uiRoot);
        BuildTexturePanel(uiRoot);
        BuildTimeline(uiRoot);
        BuildStatusBar(uiRoot);

        // 骨骼关节 gizmo（屏幕空间，挂在 UI 层最底层以免遮挡面板）
        _preview.CreateGizmoOverlay(uiRoot, _cam);
        // 将 gizmo 移到最底层（第一个子节点），确保不遮挡 UI 面板
        var gizmoNode = uiRoot.GetChild(uiRoot.GetChildCount() - 1);
        uiRoot.MoveChild(gizmoNode, 0);

        // 初始刷新
        RefreshTimeline();
        SelectKeyframe(0);

        // 加载并应用所有已保存的部件偏移（确保动画预览反映实际游戏效果）
        ApplyAllSavedOffsets();
    }

    /// <summary>加载所有槽位的已保存偏移并应用到预览</summary>
    private void ApplyAllSavedOffsets()
    {
        var skeleton = _preview.GetSkeleton();
        if (skeleton == null) return;

        foreach (var slot in EquipmentOffsetConfig.EditableSlots)
        {
            EquipmentOffsetConfig config;
            if (slot == ItemData.EquipSlot.Weapon)
                config = EquipmentOffsetConfig.GetWeapon(_currentWeaponCat, _clip.Name);
            else
                config = EquipmentOffsetConfig.Get(slot);

            var sprite = skeleton.GetSlotSprite(slot);
            if (sprite == null) continue;

            sprite.Offset = new Vector2(config.OffsetX, config.OffsetY);
            if (EquipmentOffsetConfig.SupportsRotation(slot))
                sprite.RotationDegrees = config.Rotation;
            if (!Mathf.IsEqualApprox(config.Scale, 1.0f))
                sprite.Scale = new Vector2(config.Scale, config.Scale);
            else
                sprite.Scale = Vector2.One;
            if (config.FlipH)
                sprite.Scale = new Vector2(-sprite.Scale.X, sprite.Scale.Y);
        }
    }

    public override void _Process(double delta)
    {
        // 相机 WASD
        if (_cam != null)
        {
            float spd = 400f * (float)delta * (_cam.Size / 200f);
            var v = Vector3.Zero;
            if (Input.IsKeyPressed(Key.W)) v.Z -= 1;
            if (Input.IsKeyPressed(Key.S)) v.Z += 1;
            if (Input.IsKeyPressed(Key.A)) v.X -= 1;
            if (Input.IsKeyPressed(Key.D)) v.X += 1;
            if (v.Length() > 0)
                _cam.Position += v.Normalized() * spd;
        }

        // 播放时同步时间轴
        if (_preview.IsPlaying)
        {
            _timeline.CurrentTime = _preview.PlayTime;
            ApplyAllSavedOffsets();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // ESC 返回主菜单
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        {
            GetTree().ChangeSceneToFile("res://src/ui/main_menu/main_menu.tscn");
            GetViewport().SetInputAsHandled();
            return;
        }

        // F 键：部件偏移模式下切换水平翻转
        if (@event is InputEventKey fKey && fKey.Pressed && !fKey.Echo && fKey.Keycode == Key.F && _equipOffsetMode)
        {
            _currentEquipOffset.FlipH = !_currentEquipOffset.FlipH;
            ApplyEquipOffsetToPreview();
            _statusLabel.Text = $"部件偏移 [{EquipmentOffsetConfig.GetSlotDisplayName(_currentOffsetSlot)}] 翻转: {(_currentEquipOffset.FlipH ? "是" : "否")}";
            GetViewport().SetInputAsHandled();
            return;
        }

        // ─── 左键：骨骼圆点拖拽 ───
        if (@event is InputEventMouseButton lmb && lmb.ButtonIndex == MouseButton.Left)
        {
            if (lmb.Pressed)
            {
                // 部件偏移模式：左键按住任意位置即可拖拽部件偏移
                if (_equipOffsetMode)
                {
                    _draggingGrip = true;
                    _dragBoneName = "EquipOffset";
                    _dragStartMouse = lmb.Position;
                    GetViewport().SetInputAsHandled();
                    return;
                }

                // 正常模式：检测是否点中了某个骨骼 gizmo
                var hit = HitTestGizmo(lmb.Position);
                if (hit != null)
                {
                    _draggingGrip = true;
                    _dragBoneName = hit;
                    _dragStartMouse = lmb.Position;
                    if (_selectedKeyframeIdx >= 0 && _selectedKeyframeIdx < _clip.Keyframes.Count)
                        _dragStartRotZ = _clip.Keyframes[_selectedKeyframeIdx].GetPose(hit).RotationZ;
                    else
                        _dragStartRotZ = 0;
                    _bonePanel.SelectBone(hit);
                    GetViewport().SetInputAsHandled();
                    return;
                }
            }
            else if (_draggingGrip)
            {
                _draggingGrip = false;
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (@event is InputEventMouseMotion dragMotion && _draggingGrip)
        {
            float deltaX = dragMotion.Position.X - _dragStartMouse.X;

            // 部件偏移模式：拖拽修改 offset
            if (_equipOffsetMode && _dragBoneName == "EquipOffset")
            {
                float scale = (_cam?.Size ?? 200f) / GetViewport().GetVisibleRect().Size.Y;
                float pixelScale = scale / (_preview.GetSkeleton()?.Config?.PixelSize ?? 1.5f);
                _currentEquipOffset.OffsetX += dragMotion.Relative.X * pixelScale * 2f;
                _currentEquipOffset.OffsetY += dragMotion.Relative.Y * pixelScale * 2f;
                ApplyEquipOffsetToPreview();
                _bonePanel.SetDisplayPose(new BonePose
                {
                    PositionX = _currentEquipOffset.OffsetX,
                    PositionY = _currentEquipOffset.OffsetY,
                    SpriteRotation = _currentEquipOffset.Rotation,
                });
                GetViewport().SetInputAsHandled();
                return;
            }

            // 正常模式：拖拽修改 rotation_z
            float newRot = Mathf.Clamp(_dragStartRotZ + deltaX * DragSensitivity, -360f, 360f);

            if (_selectedKeyframeIdx >= 0 && _selectedKeyframeIdx < _clip.Keyframes.Count)
            {
                var kf = _clip.Keyframes[_selectedKeyframeIdx];
                var pose = kf.GetPose(_dragBoneName);
                pose.RotationZ = newRot;
                kf.SetPose(_dragBoneName, pose);
                _preview.ApplyBonePose(_dragBoneName, pose);
                _bonePanel.SetDisplayPose(pose);
            }
            GetViewport().SetInputAsHandled();
            return;
        }

        // 滚轮缩放
        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (_equipOffsetMode && (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown))
            {
                // 部件偏移模式：
                //   Shift+滚轮 = 调整旋转（武器专用）
                //   滚轮 = 调整缩放（所有槽位通用）
                bool shiftHeld = mb.ShiftPressed;
                if (shiftHeld && EquipmentOffsetConfig.SupportsRotation(_currentOffsetSlot))
                {
                    // Shift+滚轮：调整旋转
                    float delta = mb.ButtonIndex == MouseButton.WheelUp ? 5f : -5f;
                    _currentEquipOffset.Rotation = Mathf.Clamp(_currentEquipOffset.Rotation + delta, -360f, 360f);
                    _statusLabel.Text = $"部件偏移 [{EquipmentOffsetConfig.GetSlotDisplayName(_currentOffsetSlot)}] 旋转: {_currentEquipOffset.Rotation:F0}°";
                }
                else
                {
                    // 滚轮：调整缩放
                    float delta = mb.ButtonIndex == MouseButton.WheelUp ? 0.05f : -0.05f;
                    _currentEquipOffset.Scale = Mathf.Clamp(_currentEquipOffset.Scale + delta, 0.2f, 3.0f);
                    _statusLabel.Text = $"部件偏移 [{EquipmentOffsetConfig.GetSlotDisplayName(_currentOffsetSlot)}] 缩放: {_currentEquipOffset.Scale:F2}x";
                }
                ApplyEquipOffsetToPreview();
                _bonePanel.SetDisplayPose(new BonePose
                {
                    PositionX = _currentEquipOffset.OffsetX,
                    PositionY = _currentEquipOffset.OffsetY,
                    SpriteRotation = _currentEquipOffset.Rotation,
                });
                GetViewport().SetInputAsHandled();
            }
            else if (mb.ButtonIndex == MouseButton.WheelUp && _cam != null)
            {
                _cam.Size = Mathf.Clamp(_cam.Size * 0.9f, MinOrtho, MaxOrtho);
                GetViewport().SetInputAsHandled();
            }
            else if (mb.ButtonIndex == MouseButton.WheelDown && _cam != null)
            {
                _cam.Size = Mathf.Clamp(_cam.Size * 1.1f, MinOrtho, MaxOrtho);
                GetViewport().SetInputAsHandled();
            }
            else if (mb.ButtonIndex == MouseButton.Middle)
            {
                _middleDrag = true;
                GetViewport().SetInputAsHandled();
            }
        }

        if (@event is InputEventMouseButton mbUp && !mbUp.Pressed && mbUp.ButtonIndex == MouseButton.Middle)
            _middleDrag = false;

        if (@event is InputEventMouseMotion motion && _middleDrag && _cam != null)
        {
            float factor = _cam.Size / GetViewport().GetVisibleRect().Size.Y;
            _cam.Position += new Vector3(-motion.Relative.X * factor, 0, -motion.Relative.Y * factor);
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>检测鼠标位置是否命中某个骨骼 gizmo 圆点</summary>
    private string? HitTestGizmo(Vector2 mousePos)
    {
        if (_cam == null) return null;

        string? closest = null;
        float closestDist = GizmoHitRadius;

        var boneNodes = _preview.GetBoneNodes();
        if (boneNodes == null) return null;

        // 骨骼是 2D 节点（在 SubViewport 内），需要通过 billboard 的 3D 位置来做 hit test
        // 简化方案：用 billboard 的屏幕位置作为基准，加上 2D 骨骼的相对偏移
        var skeleton = _preview.GetSkeleton();
        if (skeleton == null) return null;
        var billboardWorldPos = skeleton.Billboard.GlobalPosition;
        if (_cam.IsPositionBehind(billboardWorldPos)) return null;
        var billboardScreenPos = _cam.UnprojectPosition(billboardWorldPos);

        float worldToScreen = GetViewport().GetVisibleRect().Size.Y / (_cam.Size);

        foreach (var (name, node) in boneNodes)
        {
            // 2D 骨骼的全局位置（相对于 SubViewport 画布中心）
            var bone2DPos = node.GlobalPosition - UpperBodySkeleton.CanvasCenter;
            // 转换为屏幕偏移（像素）
            // 2D Y↓ = 屏幕 Y↓，方向一致，不取反
            var screenOffset = new Vector2(bone2DPos.X, bone2DPos.Y) * skeleton.Config.PixelSize * worldToScreen;
            var screenPos = billboardScreenPos + screenOffset;

            float dist = screenPos.DistanceTo(mousePos);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = name;
            }
        }
        return closest;
    }

    // ═══════════════════════════════════════════
    // UI 构建
    // ═══════════════════════════════════════════

    private void BuildTopBar(Control root)
    {
        var bar = new HBoxContainer();
        bar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        bar.OffsetBottom = 36;
        bar.AddThemeConstantOverride("separation", 10);
        bar.MouseFilter = Control.MouseFilterEnum.Pass;

        var bg = new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.07f, 0.9f) };
        bg.SetContentMarginAll(6);
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", bg);
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        panel.OffsetBottom = 36;
        root.AddChild(panel);
        panel.AddChild(bar);

        // 返回按钮
        var backBtn = new Button { Text = "← 返回" };
        backBtn.Pressed += () => GetTree().ChangeSceneToFile("res://src/ui/main_menu/main_menu.tscn");
        bar.AddChild(backBtn);

        bar.AddChild(new VSeparator());

        // 动画选择
        bar.AddChild(new Label { Text = "动画:" });
        _animSelect = new OptionButton { CustomMinimumSize = new Vector2(140, 0) };
        _animSelect.ItemSelected += OnAnimSelected;
        bar.AddChild(_animSelect);

        // 初始填充动画列表
        RefreshAnimList();

        bar.AddChild(new VSeparator());

        // 体型选择
        bar.AddChild(new Label { Text = "体型:" });
        _bodyTypeSelect = new OptionButton();
        _bodyTypeSelect.AddItem("Standard");
        _bodyTypeSelect.AddItem("Heavy");
        _bodyTypeSelect.AddItem("Slim");
        _bodyTypeSelect.AddItem("Large");
        _bodyTypeSelect.ItemSelected += OnBodyTypeSelected;
        bar.AddChild(_bodyTypeSelect);

        bar.AddChild(new VSeparator());

        // 武器类别选择
        bar.AddChild(new Label { Text = "武器:" });
        _weaponCatSelect = new OptionButton { CustomMinimumSize = new Vector2(120, 0) };
        foreach (var cat in WeaponAnimCategoryUtil.All)
            _weaponCatSelect.AddItem(WeaponAnimCategoryUtil.GetDisplayName(cat));
        _weaponCatSelect.Selected = 0;
        _weaponCatSelect.ItemSelected += OnWeaponCatSelected;
        bar.AddChild(_weaponCatSelect);

        bar.AddChild(new VSeparator());

        // 保存/加载
        var saveBtn = new Button { Text = "保存" };
        saveBtn.Pressed += OnSave;
        bar.AddChild(saveBtn);

        _nameInput = new LineEdit
        {
            PlaceholderText = "文件名",
            CustomMinimumSize = new Vector2(100, 0),
            Text = "idle",
        };
        bar.AddChild(_nameInput);

        var newBtn = new Button { Text = "新建" };
        newBtn.Pressed += OnNewAnim;
        bar.AddChild(newBtn);

        var delBtn = new Button { Text = "删除" };
        delBtn.AddThemeColorOverride("font_color", new Color(0.9f, 0.3f, 0.3f));
        delBtn.Pressed += OnDeleteAnim;
        bar.AddChild(delBtn);

        bar.AddChild(new VSeparator());

        // 时长
        bar.AddChild(new Label { Text = "时长:" });
        _durationSpin = new SpinBox
        {
            MinValue = 0.1, MaxValue = 5.0, Step = 0.1, Value = 1.0,
            CustomMinimumSize = new Vector2(70, 0),
        };
        _durationSpin.ValueChanged += OnDurationChanged;
        bar.AddChild(_durationSpin);

        // 循环
        _loopCheck = new CheckBox { Text = "循环", ButtonPressed = false };
        _loopCheck.Toggled += OnLoopToggled;
        bar.AddChild(_loopCheck);

        // 重置当前帧
        var resetBtn = new Button { Text = "归零" };
        resetBtn.TooltipText = "重置选中骨骼到 0°";
        resetBtn.Pressed += OnResetBone;
        bar.AddChild(resetBtn);

        bar.AddChild(new VSeparator());

        // 部件偏移模式
        _equipOffsetModeCheck = new CheckBox { Text = "部件偏移", ButtonPressed = false };
        _equipOffsetModeCheck.TooltipText = "勾选后拖拽调整偏移，滚轮调整缩放，Shift+滚轮调整旋转(武器)";
        _equipOffsetModeCheck.Toggled += OnEquipOffsetModeToggled;
        bar.AddChild(_equipOffsetModeCheck);

        // 部件槽位选择
        _equipSlotSelect = new OptionButton { CustomMinimumSize = new Vector2(80, 0) };
        foreach (var slot in EquipmentOffsetConfig.EditableSlots)
            _equipSlotSelect.AddItem(EquipmentOffsetConfig.GetSlotDisplayName(slot));
        _equipSlotSelect.Selected = 0;
        _equipSlotSelect.ItemSelected += OnEquipSlotSelected;
        bar.AddChild(_equipSlotSelect);

        bar.AddChild(new VSeparator());

        // 朝向切换
        var facingBtn = new Button { Text = "翻转 ↔" };
        facingBtn.TooltipText = "切换角色朝向（左/右），装备纹理跟随翻转";
        facingBtn.Pressed += OnFlipFacing;
        bar.AddChild(facingBtn);

        // 隐藏/显示纹理
        _hideTexturesCheck = new CheckBox { Text = "隐藏纹理", ButtonPressed = false };
        _hideTexturesCheck.TooltipText = "暂时隐藏所有装备纹理，只显示骨骼";
        _hideTexturesCheck.Toggled += OnHideTexturesToggled;
        bar.AddChild(_hideTexturesCheck);
    }

    private void BuildBonePanel(Control root)
    {
        _bonePanel = new AnimEditorBonePanel();
        _bonePanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.LeftWide);
        _bonePanel.OffsetTop = 40;
        _bonePanel.OffsetBottom = -100;
        _bonePanel.OffsetRight = 200;
        root.AddChild(_bonePanel);

        _bonePanel.BonePoseChanged += OnBonePoseChanged;
        _bonePanel.BoneSelected += OnBoneSelected;
    }

    private void BuildTexturePanel(Control root)
    {
        _texturePanel = new AnimEditorTexturePanel();
        _texturePanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.RightWide);
        _texturePanel.OffsetTop = 40;
        _texturePanel.OffsetBottom = -100;
        _texturePanel.OffsetLeft = -220;
        root.AddChild(_texturePanel);

        _texturePanel.TextureSelected += OnTextureSelected;
    }

    private void BuildTimeline(Control root)
    {
        _timeline = new AnimEditorTimeline();
        _timeline.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        _timeline.OffsetTop = -100;
        _timeline.OffsetLeft = 200;
        _timeline.MouseFilter = Control.MouseFilterEnum.Pass;
        root.AddChild(_timeline);

        _timeline.TimeChanged += OnTimeChanged;
        _timeline.KeyframeSelected += OnKeyframeSelected;
        _timeline.PlayPressed += () => _preview.Play();
        _timeline.PausePressed += () => _preview.Pause();
        _timeline.StepForwardPressed += OnStepForward;
        _timeline.AddKeyframePressed += OnAddKeyframe;
        _timeline.RemoveKeyframePressed += OnRemoveKeyframe;
    }

    private void BuildStatusBar(Control root)
    {
        _statusLabel = new Label();
        _statusLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
        _statusLabel.OffsetLeft = -300;
        _statusLabel.OffsetTop = -20;
        _statusLabel.AddThemeFontSizeOverride("font_size", 11);
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        root.AddChild(_statusLabel);
        UpdateStatus();
    }

    // ═══════════════════════════════════════════
    // 事件处理
    // ═══════════════════════════════════════════

    private void OnAnimSelected(long index)
    {
        string name = _animSelect.GetItemText((int)index);

        // 尝试从文件加载（按当前武器类别）
        var loaded = AnimClipSerializer.Load(name, _currentWeaponCat);
        if (loaded != null)
        {
            _clip = loaded;
        }
        else if (_templates.TryGetValue(name, out var factory))
        {
            _clip = factory();
            _clip.WeaponCategory = _currentWeaponCat;
        }
        else
        {
            _clip = new AnimClip { Name = name, Duration = 1.0f, WeaponCategory = _currentWeaponCat };
        }

        _preview.CurrentClip = _clip;
        _preview.IsPlaying = false;
        RefreshTimeline();
        SelectKeyframe(0);
        UpdateStatus();
        _nameInput.Text = _clip.Name;
        _durationSpin.SetValueNoSignal(_clip.Duration);
        _loopCheck.SetPressedNoSignal(_clip.Loop);
    }

    private void OnWeaponCatSelected(long index)
    {
        _currentWeaponCat = WeaponAnimCategoryUtil.All[(int)index];
        _clip.WeaponCategory = _currentWeaponCat;

        // 刷新动画列表（加载该类别下已保存的动画）
        RefreshAnimList();
        UpdateStatus();
    }

    private void OnBodyTypeSelected(long index)
    {
        _preview.Rebuild((BodyType)(int)index);
        _preview.RefreshGizmoReferences();
        _preview.CurrentClip = _clip;
        if (_selectedKeyframeIdx >= 0 && _selectedKeyframeIdx < _clip.Keyframes.Count)
        {
            var pose = AnimClipInterpolator.Sample(_clip, _clip.Keyframes[_selectedKeyframeIdx].Time);
            _preview.ApplyPose(pose);
        }
    }

    private void OnTimeChanged(float time)
    {
        _preview.Pause();
        _preview.SeekTo(time);
        ApplyAllSavedOffsets();
        RefreshBonePanelFromTime(time);
    }

    private void OnKeyframeSelected(int index)
    {
        SelectKeyframe(index);
    }

    private void OnBonePoseChanged(string boneName, float rotZ, float posY, float posX, float spriteRot)
    {
        // 部件偏移模式：修改部件偏移配置
        if (_equipOffsetMode)
        {
            _currentEquipOffset.OffsetX = posX;
            _currentEquipOffset.OffsetY = posY;
            if (EquipmentOffsetConfig.SupportsRotation(_currentOffsetSlot))
                _currentEquipOffset.Rotation = spriteRot;
            else
                _currentEquipOffset.Scale = Mathf.Clamp(spriteRot, 0.2f, 3.0f);
            ApplyEquipOffsetToPreview();
            return;
        }

        // 正常模式：更新当前关键帧数据
        if (_selectedKeyframeIdx < 0 || _selectedKeyframeIdx >= _clip.Keyframes.Count) return;

        var kf = _clip.Keyframes[_selectedKeyframeIdx];
        var pose = new BonePose { RotationZ = rotZ, PositionY = posY, PositionX = posX, SpriteRotation = spriteRot };
        kf.SetPose(boneName, pose);

        // 实时更新预览
        _preview.ApplyBonePose(boneName, pose);
        ApplyAllSavedOffsets();
    }

    private void OnBoneSelected(string boneName)
    {
        // 刷新滑条显示当前帧该骨骼的值
        if (_selectedKeyframeIdx >= 0 && _selectedKeyframeIdx < _clip.Keyframes.Count)
        {
            var pose = _clip.Keyframes[_selectedKeyframeIdx].GetPose(boneName);
            _bonePanel.SetDisplayPose(pose);
        }

        // 同步 gizmo 高亮
        _preview.SetGizmoSelectedBone(boneName);
    }

    private void OnTextureSelected(string slotName, string texturePath)
    {
        if (_preview == null) return;

        Texture2D? tex = null;
        if (!string.IsNullOrEmpty(texturePath))
            tex = GD.Load<Texture2D>(texturePath);

        // 根据部件名找到对应的 Sprite2D 并应用纹理
        var skeleton = _preview.GetSkeleton();
        if (skeleton == null) return;

        // 映射中文部件名 → EquipSlot + Sprite2D
        var (sprite, slot) = slotName switch
        {
            "身体" => (skeleton.SpriteBody, ItemData.EquipSlot.Body),
            "护甲" => (skeleton.SpriteCostume, ItemData.EquipSlot.Costume),
            "头盔" => (skeleton.SpriteHelmet, ItemData.EquipSlot.Helmet),
            "手甲" => (skeleton.SpriteHands, ItemData.EquipSlot.Hands),
            "武器" => (skeleton.SpriteWeapon, ItemData.EquipSlot.Weapon),
            "盾牌" => ((Sprite2D?)skeleton.SpriteShield, ItemData.EquipSlot.Body), // Shield 无独立 slot，用 Body fallback
            _ => ((Sprite2D?)null, ItemData.EquipSlot.Body),
        };

        if (sprite == null) return;

        if (tex != null)
        {
            // 使用 TextureScaleConfig 计算正确的 Sprite2D.Scale
            _preview.ApplyTextureWithScale(slot, sprite, tex);
        }
        else
        {
            // 清除纹理，恢复默认
            sprite.Texture = null;
            sprite.Scale = Vector2.One;
            sprite.Visible = true;
        }

        // 纹理变更后重新应用所有已保存的偏移
        ApplyAllSavedOffsets();
    }

    private void OnStepForward()
    {
        _preview.Pause();
        float step = 1.0f / 30.0f; // 30fps
        float newTime = Mathf.Min(_preview.PlayTime + step, _clip.Duration);
        _preview.SeekTo(newTime);
        ApplyAllSavedOffsets();
        _timeline.CurrentTime = newTime;
        RefreshBonePanelFromTime(newTime);
    }

    private void OnAddKeyframe()
    {
        float time = _timeline.CurrentTime;
        int idx = _clip.InsertKeyframeAt(time);
        RefreshTimeline();
        SelectKeyframe(idx);
        UpdateStatus();
    }

    private void OnRemoveKeyframe()
    {
        if (_selectedKeyframeIdx < 0) return;
        _clip.RemoveKeyframe(_selectedKeyframeIdx);
        RefreshTimeline();
        _selectedKeyframeIdx = Mathf.Min(_selectedKeyframeIdx, _clip.Keyframes.Count - 1);
        if (_selectedKeyframeIdx >= 0)
            SelectKeyframe(_selectedKeyframeIdx);
        UpdateStatus();
    }

    private void OnSave()
    {
        // 部件偏移模式：保存部件偏移配置
        if (_equipOffsetMode)
        {
            if (_currentOffsetSlot == ItemData.EquipSlot.Weapon)
            {
                EquipmentOffsetConfig.SaveWeapon(_currentEquipOffset, _currentWeaponCat, _clip.Name);
                EquipmentOffsetConfig.ClearCache();
                _statusLabel.Text = $"武器偏移配置已保存: weapon/{_currentWeaponCat.ToString().ToLower()}_{_clip.Name}.json";
            }
            else
            {
                EquipmentOffsetConfig.Save(_currentEquipOffset);
                EquipmentOffsetConfig.ClearCache();
                _statusLabel.Text = $"部件偏移配置已保存: {_currentOffsetSlot.ToString().ToLower()}.json";
            }
            return;
        }

        // 正常模式：保存动画
        string name = _nameInput.Text.Trim();
        if (string.IsNullOrEmpty(name))
            name = _clip.Name;
        _clip.Name = name;

        AnimClipSerializer.Save(_clip);
        _statusLabel.Text = $"已保存: {_currentWeaponCat.ToString().ToLower()}/{name}.json";

        // 确保下拉列表包含此动画
        bool found = false;
        for (int i = 0; i < _animSelect.ItemCount; i++)
        {
            if (_animSelect.GetItemText(i) == _clip.Name) { found = true; break; }
        }
        if (!found)
            _animSelect.AddItem(_clip.Name);
    }

    private void OnNewAnim()
    {
        var savedCount = AnimClipSerializer.ListSaved(_currentWeaponCat).Count;
        _clip = new AnimClip
        {
            Name = $"custom_{savedCount + 1}",
            Duration = 1.0f,
            WeaponCategory = _currentWeaponCat,
        };
        _clip.Keyframes.Add(new AnimKeyframe { Time = 0 });
        _clip.Keyframes.Add(new AnimKeyframe { Time = 1.0f });
        _preview.CurrentClip = _clip;
        _preview.IsPlaying = false;
        RefreshTimeline();
        SelectKeyframe(0);
        UpdateStatus();

        // 添加到下拉
        _animSelect.AddItem(_clip.Name);
        _animSelect.Selected = _animSelect.ItemCount - 1;
    }

    private void OnDeleteAnim()
    {
        string dir = $"user://custom_animations/{_currentWeaponCat.ToString().ToLower()}";
        string path = $"{dir}/{_clip.Name}.json";
        if (FileAccess.FileExists(path))
        {
            DirAccess.Open(dir)?.Remove($"{_clip.Name}.json");
            _statusLabel.Text = $"已删除: {path}";
        }
        else
        {
            _statusLabel.Text = "该动画未保存，无需删除";
        }
        RefreshAnimList();
    }

    private void OnDurationChanged(double value)
    {
        float newDur = (float)value;
        if (newDur > 0 && Mathf.Abs(newDur - _clip.Duration) > 0.001f)
        {
            _clip.SetDuration(newDur);
            RefreshTimeline();
            UpdateStatus();
        }
    }

    private void OnLoopToggled(bool on)
    {
        _clip.Loop = on;
        UpdateStatus();
    }

    private void OnResetBone()
    {
        if (_selectedKeyframeIdx < 0 || _selectedKeyframeIdx >= _clip.Keyframes.Count) return;
        string bone = _bonePanel.SelectedBone;
        var kf = _clip.Keyframes[_selectedKeyframeIdx];
        kf.SetPose(bone, BonePose.Zero);
        _preview.ApplyBonePose(bone, BonePose.Zero);
        ApplyAllSavedOffsets();
        _bonePanel.SetDisplayPose(BonePose.Zero);
    }

    private void OnFlipFacing()
    {
        _facingLeft = !_facingLeft;
        var skeleton = _preview.GetSkeleton();
        if (skeleton == null) return;
        skeleton.SetFacing(_facingLeft);
    }

    private void OnHideTexturesToggled(bool hide)
    {
        var skeleton = _preview.GetSkeleton();
        if (skeleton == null) return;

        foreach (var (_, sprite) in skeleton.SlotSprites)
        {
            if (hide)
            {
                // 记录原始可见性到 meta，然后隐藏
                sprite.SetMeta("_wasVisible", sprite.Visible);
                sprite.Visible = false;
            }
            else
            {
                // 恢复原始可见性
                var was = sprite.GetMeta("_wasVisible", true);
                sprite.Visible = was.AsBool();
            }
        }
    }

    private void OnEquipOffsetModeToggled(bool on)
    {
        _equipOffsetMode = on;
        if (on)
        {
            _currentOffsetSlot = EquipmentOffsetConfig.EditableSlots[_equipSlotSelect.Selected];
            if (_currentOffsetSlot == ItemData.EquipSlot.Weapon)
                _currentEquipOffset = EquipmentOffsetConfig.LoadWeapon(_currentWeaponCat, _clip.Name);
            else
                _currentEquipOffset = EquipmentOffsetConfig.Load(_currentOffsetSlot);
            _bonePanel.SetDisplayPose(new BonePose
            {
                PositionX = _currentEquipOffset.OffsetX,
                PositionY = _currentEquipOffset.OffsetY,
                SpriteRotation = _currentEquipOffset.Rotation,
            });
            ApplyEquipOffsetToPreview();
            var hint = EquipmentOffsetConfig.SupportsRotation(_currentOffsetSlot)
                ? "拖拽偏移，滚轮缩放，Shift+滚轮旋转"
                : "拖拽偏移，滚轮缩放";
            _statusLabel.Text = $"部件偏移 [{EquipmentOffsetConfig.GetSlotDisplayName(_currentOffsetSlot)}]：{hint}，点「保存」保存";
        }
        else
        {
            _statusLabel.Text = "";
            if (_selectedKeyframeIdx >= 0 && _selectedKeyframeIdx < _clip.Keyframes.Count)
                SelectKeyframe(_selectedKeyframeIdx);
        }
    }

    private void OnEquipSlotSelected(long index)
    {
        _currentOffsetSlot = EquipmentOffsetConfig.EditableSlots[(int)index];
        if (_equipOffsetMode)
        {
            if (_currentOffsetSlot == ItemData.EquipSlot.Weapon)
                _currentEquipOffset = EquipmentOffsetConfig.LoadWeapon(_currentWeaponCat, _clip.Name);
            else
                _currentEquipOffset = EquipmentOffsetConfig.Load(_currentOffsetSlot);
            _bonePanel.SetDisplayPose(new BonePose
            {
                PositionX = _currentEquipOffset.OffsetX,
                PositionY = _currentEquipOffset.OffsetY,
                SpriteRotation = _currentEquipOffset.Rotation,
            });
            ApplyEquipOffsetToPreview();
            var hint = EquipmentOffsetConfig.SupportsRotation(_currentOffsetSlot)
                ? "拖拽偏移，滚轮缩放，Shift+滚轮旋转"
                : "拖拽偏移，滚轮缩放";
            _statusLabel.Text = $"部件偏移 [{EquipmentOffsetConfig.GetSlotDisplayName(_currentOffsetSlot)}]：{hint}";
        }
    }

    /// <summary>将当前部件偏移配置应用到预览</summary>
    private void ApplyEquipOffsetToPreview()
    {
        var skeleton = _preview.GetSkeleton();
        if (skeleton == null) return;

        var sprite = skeleton.GetSlotSprite(_currentOffsetSlot);
        if (sprite == null) return;

        sprite.Offset = new Vector2(_currentEquipOffset.OffsetX, _currentEquipOffset.OffsetY);

        // 武器：应用旋转
        if (EquipmentOffsetConfig.SupportsRotation(_currentOffsetSlot))
            sprite.RotationDegrees = _currentEquipOffset.Rotation;

        // 缩放
        if (!Mathf.IsEqualApprox(_currentEquipOffset.Scale, 1.0f))
            sprite.Scale = new Vector2(_currentEquipOffset.Scale, _currentEquipOffset.Scale);
        else
            sprite.Scale = Vector2.One;

        // 水平翻转
        if (_currentEquipOffset.FlipH)
            sprite.Scale = new Vector2(-sprite.Scale.X, sprite.Scale.Y);
    }

    // ═══════════════════════════════════════════
    // 辅助
    // ═══════════════════════════════════════════

    private void SelectKeyframe(int index)
    {
        _selectedKeyframeIdx = index;
        _timeline.SelectedKeyframe = index;

        if (index >= 0 && index < _clip.Keyframes.Count)
        {
            var kf = _clip.Keyframes[index];
            _timeline.CurrentTime = kf.Time;
            _preview.SeekTo(kf.Time);
            ApplyAllSavedOffsets();

            var pose = kf.GetPose(_bonePanel.SelectedBone);
            _bonePanel.SetDisplayPose(pose);
        }
    }

    private void RefreshTimeline()
    {
        var times = _clip.Keyframes.Select(kf => kf.Time).ToList();
        _timeline.SetData(_clip.Duration, times);
    }

    /// <summary>刷新动画下拉列表（切换武器类别时调用）</summary>
    private void RefreshAnimList()
    {
        _animSelect.Clear();
        // 内置模板
        _animSelect.AddItem("idle");
        _animSelect.AddItem("attack_melee");
        _animSelect.AddItem("attack_ranged");
        _animSelect.AddItem("cast");
        _animSelect.AddItem("hit");
        _animSelect.AddItem("die");
        // 该类别下已保存的自定义动画
        foreach (var name in AnimClipSerializer.ListSaved(_currentWeaponCat))
        {
            if (!HasItem(_animSelect, name))
                _animSelect.AddItem(name);
        }
    }

    private void RefreshBonePanelFromTime(float time)
    {
        var allPose = AnimClipInterpolator.Sample(_clip, time);
        if (allPose.TryGetValue(_bonePanel.SelectedBone, out var pose))
            _bonePanel.SetDisplayPose(pose);
    }

    private void UpdateStatus()
    {
        _statusLabel.Text = $"{_clip.Name} | {_clip.Duration:F1}s | {_clip.Keyframes.Count} 帧 | {(_clip.Loop ? "循环" : "单次")}";
    }

    // ─── OptionButton 扩展 ───
    private bool HasItem(OptionButton opt, string text)
    {
        for (int i = 0; i < opt.ItemCount; i++)
            if (opt.GetItemText(i) == text) return true;
        return false;
    }
}
