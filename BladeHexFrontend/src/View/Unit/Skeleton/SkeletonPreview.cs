// SkeletonPreview.cs
// 编辑器预览场景的根脚本 — 用于在 Godot 编辑器中测试骨骼动画效果
// 挂载到 SkeletonPreview.tscn 的根节点
// 提供 Export 参数控制体型、动画播放、贴图替换等
using Godot;
using BladeHex.View.Unit.Skeleton;
using BladeHex.Data;

namespace BladeHex.View.Unit;

/// <summary>
/// 骨骼动画预览控制器。
/// 在编辑器中运行此场景可实时预览骨骼动画效果。
/// </summary>
[GlobalClass]
public partial class SkeletonPreview : Node3D
{
    // ─── Export 参数（编辑器面板可调） ───

    [ExportGroup("体型")]
    [Export] public BodyType CurrentBodyType { get; set; } = BodyType.Standard;

    [ExportGroup("动画")]
    [Export] public string CurrentAnimation { get; set; } = "idle";
    [Export] public bool AutoPlay { get; set; } = true;

    [ExportGroup("贴图（可选，留空使用占位色块）")]
    [Export] public Texture2D? BodyTexture { get; set; }
    [Export] public Texture2D? CostumeTexture { get; set; }
    [Export] public Texture2D? HeadTexture { get; set; }
    [Export] public Texture2D? HelmetTexture { get; set; }
    [Export] public Texture2D? HandsTexture { get; set; }
    [Export] public Texture2D? WeaponTexture { get; set; }
    [Export] public Texture2D? ShieldTexture { get; set; }

    [ExportGroup("调试")]
    [Export] public bool ShowBoneGizmos { get; set; } = true;
    [Export] public Color PlayerColor { get; set; } = new(0.4f, 0.7f, 1.0f);

    // ─── 内部状态 ───

    private UpperBodySkeleton? _skeleton;
    private Node3D? _bodyRoot;
    private Sprite3D? _basePedestal;
    private BodyType _lastBodyType;
    private string _lastAnimation = "";

    // ─── 相机控制（战场风格） ───
    private Camera3D? _cam;
    private const float MinOrthoSize = 80f;
    private const float MaxOrthoSize = 600f;
    private const float PanSpeed = 400f;
    private bool _middleDragging;
    private Vector2 _dragStart;

    public override void _Ready()
    {
        // 创建正交相机（战场视角）
        _cam = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = 200f,
            RotationDegrees = new Vector3(-45, 0, 0),
            Position = new Vector3(0, 180, 200),
            Current = true,
        };
        AddChild(_cam);

        RebuildSkeleton();
    }

    public override void _Process(double delta)
    {
        // 热更新：体型变化时重建
        if (CurrentBodyType != _lastBodyType)
        {
            RebuildSkeleton();
        }

        // 动画切换
        if (CurrentAnimation != _lastAnimation && _skeleton != null)
        {
            _skeleton.PlayAnimation(CurrentAnimation);
            _lastAnimation = CurrentAnimation;
        }

        // WASD 相机平移
        if (_cam != null)
        {
            float spd = PanSpeed * (float)delta * (_cam.Size / 200f);
            var v = Vector3.Zero;
            if (Input.IsKeyPressed(Key.W)) v.Z -= 1;
            if (Input.IsKeyPressed(Key.S)) v.Z += 1;
            if (Input.IsKeyPressed(Key.A)) v.X -= 1;
            if (Input.IsKeyPressed(Key.D)) v.X += 1;
            if (v.Length() > 0)
                _cam.Position += v.Normalized() * spd;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // ESC → 返回主菜单
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        {
            GetTree().ChangeSceneToFile("res://src/ui/main_menu/main_menu.tscn");
            GetViewport().SetInputAsHandled();
            return;
        }

        // 滚轮缩放
        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp && _cam != null)
            {
                _cam.Size = Mathf.Clamp(_cam.Size * 0.9f, MinOrthoSize, MaxOrthoSize);
                GetViewport().SetInputAsHandled();
            }
            else if (mb.ButtonIndex == MouseButton.WheelDown && _cam != null)
            {
                _cam.Size = Mathf.Clamp(_cam.Size * 1.1f, MinOrthoSize, MaxOrthoSize);
                GetViewport().SetInputAsHandled();
            }
            else if (mb.ButtonIndex == MouseButton.Middle)
            {
                _middleDragging = true;
                _dragStart = mb.Position;
                GetViewport().SetInputAsHandled();
            }
        }

        // 中键释放
        if (@event is InputEventMouseButton mbUp && !mbUp.Pressed && mbUp.ButtonIndex == MouseButton.Middle)
        {
            _middleDragging = false;
        }

        // 中键拖拽平移
        if (@event is InputEventMouseMotion motion && _middleDragging && _cam != null)
        {
            float factor = _cam.Size / GetViewport().GetVisibleRect().Size.Y;
            var delta = motion.Relative;
            _cam.Position += new Vector3(-delta.X * factor, 0, -delta.Y * factor);
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>重建整个骨骼（体型变化时调用）</summary>
    private void RebuildSkeleton()
    {
        // 清除旧节点
        if (_bodyRoot != null)
        {
            _bodyRoot.QueueFree();
            _bodyRoot = null;
        }

        _lastBodyType = CurrentBodyType;
        var config = BoneConfig.FromBodyType(CurrentBodyType);

        // 创建 BodyRoot
        _bodyRoot = new Node3D { Name = "BodyRoot" };
        AddChild(_bodyRoot);

        // 底座
        BuildBasePedestal(config);

        // 构建骨骼
        _skeleton = new UpperBodySkeleton();
        _skeleton.Build(_bodyRoot, config);

        // 应用贴图或占位色块
        ApplyTextures(config);

        // 应用已保存的部件偏移配置
        ApplyEquipmentOffsets();

        // 骨骼关节可视化
        if (ShowBoneGizmos)
            BuildBoneGizmos();

        // 播放动画
        if (AutoPlay)
        {
            _skeleton.PlayAnimation(CurrentAnimation);
            _lastAnimation = CurrentAnimation;
        }
    }

    /// <summary>构建底座</summary>
    private void BuildBasePedestal(BoneConfig config)
    {
        _basePedestal = new Sprite3D
        {
            Name = "BasePedestal",
            PixelSize = config.PixelSize * 0.4f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
            RotationDegrees = new Vector3(-90, 0, 0),
            Position = new Vector3(0, 0.5f, 0),
            AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass,
            SortingOffset = 2.0f, // 正值 = 排序靠后 = 画在最底层
        };

        // 尝试加载底座贴图，失败则用占位
        var baseTex = GD.Load<Texture2D>("res://assets/generated_ui_icons/unit_base_steel.png");
        if (baseTex != null)
        {
            _basePedestal.Texture = baseTex;
        }
        else
        {
            var placeholder = new PlaceholderTexture2D();
            placeholder.Size = new Vector2(64, 64);
            _basePedestal.Texture = placeholder;
            _basePedestal.Modulate = new Color(0.5f, 0.5f, 0.5f);
        }

        _bodyRoot!.AddChild(_basePedestal);
    }

    /// <summary>应用贴图或生成占位色块</summary>
    private void ApplyTextures(BoneConfig config)
    {
        if (_skeleton == null) return;

        // 2D 合成版：直接设置 Sprite2D 的 Texture，不需要 PixelSize

        // Body
        ApplyOrPlaceholder(_skeleton.SpriteBody, BodyTexture,
            new Vector2(config.TorsoWidth, config.TorsoHeight), PlayerColor);

        // Costume
        if (CostumeTexture != null)
        {
            _skeleton.SpriteCostume.Texture = CostumeTexture;
            _skeleton.SpriteCostume.Visible = true;
        }

        // Head
        ApplyOrPlaceholder(_skeleton.SpriteHead, HeadTexture,
            new Vector2(48, 48), PlayerColor * 1.2f);

        // Helmet
        if (HelmetTexture != null)
        {
            _skeleton.SpriteHelmet.Texture = HelmetTexture;
            _skeleton.SpriteHelmet.Visible = true;
        }

        // Hands
        if (HandsTexture != null)
        {
            _skeleton.SpriteHands.Texture = HandsTexture;
            _skeleton.SpriteHands.Visible = true;
        }
        else
        {
            ApplyOrPlaceholder(_skeleton.SpriteHands, null,
                new Vector2(48, 48), PlayerColor * 0.8f);
        }

        // Weapon
        if (WeaponTexture != null)
        {
            _skeleton.SpriteWeapon.Texture = WeaponTexture;
            _skeleton.SpriteWeapon.Visible = true;
        }
        else
        {
            ApplyOrPlaceholder(_skeleton.SpriteWeapon, null,
                new Vector2(32, 80), new Color(0.6f, 0.6f, 0.6f));
        }

        // Shield
        if (ShieldTexture != null)
        {
            _skeleton.SpriteShield.Texture = ShieldTexture;
            _skeleton.SpriteShield.Visible = true;
        }
    }

    /// <summary>设置贴图或生成纯色占位</summary>
    private static void ApplyOrPlaceholder(Sprite2D sprite, Texture2D? texture, Vector2 size, Color color)
    {
        if (texture != null)
        {
            sprite.Texture = texture;
        }
        else
        {
            // 生成纯色占位图
            var img = Image.CreateEmpty((int)size.X, (int)size.Y, false, Image.Format.Rgba8);
            img.Fill(color);
            sprite.Texture = ImageTexture.CreateFromImage(img);
        }
        sprite.Visible = true;
    }

    /// <summary>骨骼 gizmo 可视化（2D 骨骼在 SubViewport 内，不再支持 3D gizmo）</summary>
    private void BuildBoneGizmos()
    {
        // 新方案中骨骼是 Node2D（在 SubViewport 内），3D gizmo 不适用
        // 骨骼可视化改为在编辑器的 AnimEditorScene 中通过 2D overlay 实现
    }

    // ─── 朝向控制 ───

    /// <summary>应用已保存的部件偏移配置</summary>
    private void ApplyEquipmentOffsets()
    {
        if (_skeleton == null) return;
        foreach (var slot in BladeHex.View.Unit.Skeleton.Editor.EquipmentOffsetConfig.EditableSlots)
        {
            var config = BladeHex.View.Unit.Skeleton.Editor.EquipmentOffsetConfig.Get(slot);
            if (config.OffsetX == 0 && config.OffsetY == 0
                && Mathf.IsEqualApprox(config.Scale, 1.0f)
                && Mathf.IsEqualApprox(config.Rotation, 0f))
                continue;

            var sprite = _skeleton.GetSlotSprite(slot);
            if (sprite == null || !sprite.Visible) continue;

            sprite.Offset = new Vector2(config.OffsetX, config.OffsetY);
            if (BladeHex.View.Unit.Skeleton.Editor.EquipmentOffsetConfig.SupportsRotation(slot))
                sprite.RotationDegrees = config.Rotation;
            if (!Mathf.IsEqualApprox(config.Scale, 1.0f))
                sprite.Scale = new Vector2(config.Scale, config.Scale);
        }
    }

    // ─── 朝向控制 ───

    /// <summary>设置朝向（供 UI 调用）</summary>
    public void SetFacingLeft(bool facingLeft)
    {
        _skeleton?.SetFacing(facingLeft);
    }

    // ─── 动画事件接收（AnimationPlayer method track 回调） ───

    /// <summary>动画事件回调入口</summary>
    public void OnAnimEvent(string eventName)
    {
        GD.Print($"[SkeletonPreview] AnimEvent: {eventName}");
    }
}
