using BladeHex.Data;
using BladeHex.View.AssetSystem;
using BladeHex.View.Unit.Skeleton;
using Godot;

namespace BladeHex.View.Unit;

[GlobalClass]
public partial class SkeletonPreview : Node3D
{
    [ExportGroup("Body")]
    [Export] public BodyType CurrentBodyType { get; set; } = BodyType.Standard;

    [ExportGroup("Animation")]
    [Export] public string CurrentAnimation { get; set; } = "idle";
    [Export] public bool AutoPlay { get; set; } = true;

    [ExportGroup("Textures")]
    [Export] public Texture2D? BodyTexture { get; set; }
    [Export] public Texture2D? CostumeTexture { get; set; }
    [Export] public Texture2D? HeadTexture { get; set; }
    [Export] public Texture2D? HelmetTexture { get; set; }
    [Export] public Texture2D? HandsTexture { get; set; }
    [Export] public Texture2D? WeaponTexture { get; set; }
    [Export] public Texture2D? ShieldTexture { get; set; }

    [ExportGroup("Debug")]
    [Export] public bool ShowBoneGizmos { get; set; } = true;
    [Export] public Color PlayerColor { get; set; } = new(0.4f, 0.7f, 1.0f);

    private const float MinOrthoSize = 80f;
    private const float MaxOrthoSize = 600f;
    private const float PanSpeed = 400f;
    private const string BasePedestalPath = "res://assets/generated_ui_icons/unit_base_steel.png";

    private UpperBodySkeleton? _skeleton;
    private Node3D? _bodyRoot;
    private Sprite3D? _basePedestal;
    private Camera3D? _camera;
    private BodyType _lastBodyType;
    private string _lastAnimation = "";
    private bool _middleDragging;
    private Vector2 _dragStart;

    public override void _Ready()
    {
        _camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = 200f,
            RotationDegrees = new Vector3(-45, 0, 0),
            Position = new Vector3(0, 180, 200),
            Current = true,
        };
        AddChild(_camera);

        RebuildSkeleton();
    }

    public override void _Process(double delta)
    {
        if (CurrentBodyType != _lastBodyType)
            RebuildSkeleton();

        if (CurrentAnimation != _lastAnimation && _skeleton != null)
        {
            _skeleton.PlayAnimation(CurrentAnimation);
            _lastAnimation = CurrentAnimation;
        }

        UpdateCameraPan((float)delta);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        {
            GetTree().ChangeSceneToFile("res://BladeHexFrontend/src/ui/main_menu/main_menu.tscn");
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventMouseButton mouseButton)
        {
            HandleMouseButton(mouseButton);
            return;
        }

        if (@event is InputEventMouseMotion motion && _middleDragging && _camera != null)
        {
            float factor = _camera.Size / GetViewport().GetVisibleRect().Size.Y;
            var delta = motion.Relative;
            _camera.Position += new Vector3(-delta.X * factor, 0, -delta.Y * factor);
            GetViewport().SetInputAsHandled();
        }
    }

    public void SetFacingLeft(bool facingLeft)
    {
        _skeleton?.SetFacing(facingLeft);
    }

    public void OnAnimEvent(string eventName)
    {
        GD.Print($"[SkeletonPreview] AnimEvent: {eventName}");
    }

    private void HandleMouseButton(InputEventMouseButton mouseButton)
    {
        if (!mouseButton.Pressed)
        {
            if (mouseButton.ButtonIndex == MouseButton.Middle)
                _middleDragging = false;
            return;
        }

        if (mouseButton.ButtonIndex == MouseButton.WheelUp && _camera != null)
        {
            _camera.Size = Mathf.Clamp(_camera.Size * 0.9f, MinOrthoSize, MaxOrthoSize);
            GetViewport().SetInputAsHandled();
        }
        else if (mouseButton.ButtonIndex == MouseButton.WheelDown && _camera != null)
        {
            _camera.Size = Mathf.Clamp(_camera.Size * 1.1f, MinOrthoSize, MaxOrthoSize);
            GetViewport().SetInputAsHandled();
        }
        else if (mouseButton.ButtonIndex == MouseButton.Middle)
        {
            _middleDragging = true;
            _dragStart = mouseButton.Position;
            GetViewport().SetInputAsHandled();
        }
    }

    private void UpdateCameraPan(float delta)
    {
        if (_camera == null)
            return;

        float speed = PanSpeed * delta * (_camera.Size / 200f);
        var direction = Vector3.Zero;
        if (Input.IsKeyPressed(Key.W))
            direction.Z -= 1;
        if (Input.IsKeyPressed(Key.S))
            direction.Z += 1;
        if (Input.IsKeyPressed(Key.A))
            direction.X -= 1;
        if (Input.IsKeyPressed(Key.D))
            direction.X += 1;

        if (direction.Length() > 0)
            _camera.Position += direction.Normalized() * speed;
    }

    private void RebuildSkeleton()
    {
        if (_bodyRoot != null)
        {
            RemoveChild(_bodyRoot);
            _bodyRoot.QueueFree();
            _bodyRoot = null;
        }

        _lastBodyType = CurrentBodyType;
        var config = BoneConfig.FromBodyType(CurrentBodyType);

        _bodyRoot = new Node3D { Name = "BodyRoot" };
        AddChild(_bodyRoot);

        BuildBasePedestal();

        _skeleton = new UpperBodySkeleton();
        _skeleton.Build(_bodyRoot, config);

        ApplyTextures(config);
        ApplyEquipmentOffsets();

        if (ShowBoneGizmos)
            BuildBoneGizmos();

        if (AutoPlay)
        {
            _skeleton.PlayAnimation(CurrentAnimation);
            _lastAnimation = CurrentAnimation;
        }
    }

    private void BuildBasePedestal()
    {
        _basePedestal = new Sprite3D
        {
            Name = "BasePedestal",
            PixelSize = 0.5f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
            RotationDegrees = new Vector3(-90, 0, 0),
            Position = new Vector3(0, 2.0f, 0),
            AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass,
            SortingOffset = 1.0f,
        };

        var baseTexture = TextureAssetResolver.LoadIcon("unit_base_steel", BasePedestalPath);
        if (baseTexture != null)
        {
            _basePedestal.Texture = baseTexture;
        }
        else
        {
            var placeholder = new PlaceholderTexture2D { Size = new Vector2(64, 64) };
            _basePedestal.Texture = placeholder;
            _basePedestal.Modulate = new Color(0.5f, 0.5f, 0.5f);
        }

        _bodyRoot!.AddChild(_basePedestal);
    }

    private void ApplyTextures(BoneConfig config)
    {
        if (_skeleton == null)
            return;

        ApplyOrPlaceholder(_skeleton.SpriteBody, BodyTexture, new Vector2(config.TorsoWidth, config.TorsoHeight), PlayerColor);

        if (CostumeTexture != null)
        {
            _skeleton.SpriteCostume.Texture = CostumeTexture;
            _skeleton.SpriteCostume.Visible = true;
        }

        ApplyOrPlaceholder(_skeleton.SpriteHead, HeadTexture, new Vector2(48, 48), PlayerColor * 1.2f);

        if (HelmetTexture != null)
        {
            _skeleton.SpriteHelmet.Texture = HelmetTexture;
            _skeleton.SpriteHelmet.Visible = true;
        }

        if (HandsTexture != null)
        {
            _skeleton.SpriteHands.Texture = HandsTexture;
            _skeleton.SpriteHands.Visible = true;
            _skeleton.SpriteHandsL.Texture = HandsTexture;
            _skeleton.SpriteHandsL.Visible = true;
        }
        else
        {
            ApplyOrPlaceholder(_skeleton.SpriteHands, null, new Vector2(48, 48), PlayerColor * 0.8f);
            ApplyOrPlaceholder(_skeleton.SpriteHandsL, null, new Vector2(48, 48), PlayerColor * 0.8f);
        }

        if (WeaponTexture != null)
        {
            _skeleton.SpriteWeapon.Texture = WeaponTexture;
            _skeleton.SpriteWeapon.Visible = true;
        }
        else
        {
            ApplyOrPlaceholder(_skeleton.SpriteWeapon, null, new Vector2(32, 80), new Color(0.6f, 0.6f, 0.6f));
        }

        if (ShieldTexture != null)
        {
            _skeleton.SpriteShield.Texture = ShieldTexture;
            _skeleton.SpriteShield.Visible = true;
        }
    }

    private static void ApplyOrPlaceholder(Sprite2D sprite, Texture2D? texture, Vector2 size, Color color)
    {
        if (texture != null)
        {
            sprite.Texture = texture;
        }
        else
        {
            const int canvasSize = 256;
            var image = Image.CreateEmpty(canvasSize, canvasSize, false, Image.Format.Rgba8);
            int rectWidth = (int)size.X;
            int rectHeight = (int)size.Y;
            int rectX = (canvasSize - rectWidth) / 2;
            int rectY = (canvasSize - rectHeight) / 2;
            image.FillRect(new Rect2I(rectX, rectY, rectWidth, rectHeight), color);
            sprite.Texture = ImageTexture.CreateFromImage(image);
        }

        sprite.Visible = true;
    }

    private static void BuildBoneGizmos()
    {
        // The current skeleton preview uses 2D sprites inside a SubViewport.
        // Bone gizmos are handled by the animation editor overlay.
    }

    private void ApplyEquipmentOffsets()
    {
        if (_skeleton == null)
            return;

        foreach (var slot in BladeHex.View.Unit.Skeleton.Editor.EquipmentOffsetConfig.EditableSlots)
        {
            var config = BladeHex.View.Unit.Skeleton.Editor.EquipmentOffsetConfig.Get(slot);
            if (config.OffsetX == 0
                && config.OffsetY == 0
                && Mathf.IsEqualApprox(config.Scale, 1.0f)
                && Mathf.IsEqualApprox(config.Rotation, 0f))
            {
                continue;
            }

            var sprite = _skeleton.GetSlotSprite(slot);
            if (sprite == null || !sprite.Visible)
                continue;

            sprite.Offset = new Vector2(config.OffsetX, config.OffsetY);
            if (BladeHex.View.Unit.Skeleton.Editor.EquipmentOffsetConfig.SupportsRotation(slot))
                sprite.RotationDegrees = config.Rotation;

            sprite.Scale = !Mathf.IsEqualApprox(config.Scale, 1.0f)
                ? new Vector2(config.Scale, config.Scale)
                : Vector2.One;

            if (slot == ItemData.EquipSlot.Hands && _skeleton.SpriteHandsL != null)
            {
                _skeleton.SpriteHandsL.Offset = sprite.Offset;
                _skeleton.SpriteHandsL.Scale = sprite.Scale;
            }
        }

        if (_skeleton.SpriteBody != null)
            _skeleton.SpriteBody.Scale = Vector2.One;
        if (_skeleton.SpriteHead != null)
            _skeleton.SpriteHead.Scale = Vector2.One;
    }
}
