using BladeHex.Data;
using BladeHex.View.AssetSystem;
using Godot;
using System.Collections.Generic;

namespace BladeHex.View.Unit.Skeleton.Editor;

public partial class AnimEditorPreview : Node3D
{
    private const string BasePedestalPath = "res://assets/generated_ui_icons/unit_base_steel.png";

    private UpperBodySkeleton? _skeleton;
    private Node3D? _bodyRoot;
    private Sprite3D? _basePedestal;
    private BoneConfig _config = BoneConfig.Standard;
    private BoneGizmoOverlay? _gizmoOverlay;
    private readonly Dictionary<string, Node2D> _boneNodes = new();

    public bool IsPlaying { get; set; }
    public float PlayTime { get; set; }
    public AnimClip? CurrentClip { get; set; }
    public EquipmentOffsetConfig? EquipOffsetConfig { get; set; }

    public bool ShowGizmos
    {
        get => _gizmoOverlay?.Visible ?? true;
        set
        {
            if (_gizmoOverlay != null)
                _gizmoOverlay.Visible = value;
        }
    }

    public override void _Ready()
    {
        Rebuild(BodyType.Standard);
    }

    public override void _Process(double delta)
    {
        if (!IsPlaying || CurrentClip == null)
            return;

        PlayTime += (float)delta;
        if (CurrentClip.Loop)
        {
            if (CurrentClip.Duration > 0)
                PlayTime %= CurrentClip.Duration;
        }
        else if (PlayTime >= CurrentClip.Duration)
        {
            PlayTime = CurrentClip.Duration;
            IsPlaying = false;
        }

        var pose = AnimClipInterpolator.Sample(CurrentClip, PlayTime);
        ApplyPose(pose);
        ApplyEquipOffsetOverlay();
    }

    public UpperBodySkeleton? GetSkeleton()
    {
        return _skeleton;
    }

    public IReadOnlyDictionary<string, Node2D>? GetBoneNodes()
    {
        return _boneNodes.Count > 0 ? _boneNodes : null;
    }

    public void SetGizmoSelectedBone(string? boneName)
    {
        if (_gizmoOverlay != null)
            _gizmoOverlay.SelectedBone = boneName;
    }

    public void SetGizmoDisplaceMode(bool active, string? boneName = null)
    {
        if (_gizmoOverlay == null)
            return;

        _gizmoOverlay.DisplaceMode = active;
        _gizmoOverlay.DisplaceBone = boneName;
    }

    public void SetGizmoRotationMode(bool active, string? boneName = null)
    {
        if (_gizmoOverlay == null)
            return;

        _gizmoOverlay.RotationMode = active;
        _gizmoOverlay.RotationBone = boneName;
    }

    public void CreateGizmoOverlay(Control uiParent, Camera3D camera)
    {
        if (_gizmoOverlay != null)
        {
            _gizmoOverlay.QueueFree();
            _gizmoOverlay = null;
        }

        _gizmoOverlay = new BoneGizmoOverlay(_boneNodes)
        {
            Camera = camera,
            Billboard = _skeleton?.Billboard,
            Config = _config,
        };
        uiParent.AddChild(_gizmoOverlay);
    }

    public void RefreshGizmoReferences()
    {
        if (_gizmoOverlay == null)
            return;

        _gizmoOverlay.Billboard = _skeleton?.Billboard;
        _gizmoOverlay.Config = _config;
    }

    public void Rebuild(BodyType bodyType)
    {
        if (_bodyRoot != null)
        {
            RemoveChild(_bodyRoot);
            _bodyRoot.QueueFree();
            _bodyRoot = null;
        }

        _boneNodes.Clear();
        _config = BoneConfig.FromBodyType(bodyType);

        _bodyRoot = new Node3D { Name = "BodyRoot" };
        AddChild(_bodyRoot);

        BuildBasePedestal();

        _skeleton = new UpperBodySkeleton();
        _skeleton.Build(_bodyRoot, _config);

        RegisterBoneNodes();
        HideAllSprites();
    }

    public void ApplyPose(Dictionary<string, BonePose> pose)
    {
        if (_skeleton == null)
            return;

        var defaults = BuildBoneDefaultOffsets();
        foreach (var (boneName, bonePose) in pose)
        {
            if (!_boneNodes.TryGetValue(boneName, out var node))
                continue;

            node.RotationDegrees = bonePose.RotationZ;
            var defaultPosition = defaults.GetValueOrDefault(boneName, Vector2.Zero);
            node.Position = defaultPosition + new Vector2(bonePose.PositionX, -bonePose.PositionY);
            node.Scale = new Vector2(bonePose.ScaleX, bonePose.ScaleY);
        }
    }

    public void ApplyBonePose(string boneName, BonePose pose)
    {
        if (!_boneNodes.TryGetValue(boneName, out var node) || _skeleton == null)
            return;

        var defaults = BuildBoneDefaultOffsets();
        node.RotationDegrees = pose.RotationZ;
        var defaultPosition = defaults.GetValueOrDefault(boneName, Vector2.Zero);
        node.Position = defaultPosition + new Vector2(pose.PositionX, -pose.PositionY);
        node.Scale = new Vector2(pose.ScaleX, pose.ScaleY);
    }

    public void ResetPose()
    {
        foreach (var node in _boneNodes.Values)
            node.RotationDegrees = 0;

        if (_boneNodes.TryGetValue("Torso", out var torso))
            torso.Position = Vector2.Zero;
    }

    public void Play()
    {
        PlayTime = 0;
        IsPlaying = true;
    }

    public void Pause()
    {
        IsPlaying = false;
    }

    public void SeekTo(float time)
    {
        PlayTime = time;
        if (CurrentClip == null)
            return;

        var pose = AnimClipInterpolator.Sample(CurrentClip, time);
        ApplyPose(pose);
        ApplyEquipOffsetOverlay();
    }

    public void ApplyTextureWithScale(ItemData.EquipSlot slot, Sprite2D sprite, Texture2D texture)
    {
        sprite.Texture = texture;
        sprite.Visible = true;
        sprite.Scale = Vector2.One;
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
            _basePedestal.Texture = new PlaceholderTexture2D { Size = new Vector2(64, 64) };
            _basePedestal.Modulate = new Color(0.5f, 0.5f, 0.5f);
        }

        _bodyRoot!.AddChild(_basePedestal);
    }

    private void RegisterBoneNodes()
    {
        if (_skeleton == null)
            return;

        _boneNodes["Torso"] = _skeleton.BoneTorso;
        _boneNodes["Head"] = _skeleton.BoneHead;
        _boneNodes["ArmL"] = _skeleton.BoneArmL;
        _boneNodes["ArmR"] = _skeleton.BoneArmR;
        _boneNodes["ForearmL"] = _skeleton.BoneForearmL;
        _boneNodes["ForearmR"] = _skeleton.BoneForearmR;
        _boneNodes["Weapon"] = _skeleton.BoneWeapon;
        _boneNodes["Shield"] = _skeleton.BoneShield;
    }

    private void HideAllSprites()
    {
        if (_skeleton == null)
            return;

        _skeleton.SpriteBody.Visible = false;
        _skeleton.SpriteCostume.Visible = false;
        _skeleton.SpriteHead.Visible = false;
        _skeleton.SpriteHelmet.Visible = false;
        _skeleton.SpriteHands.Visible = false;
        _skeleton.SpriteWeapon.Visible = false;
        _skeleton.SpriteShield.Visible = false;
    }

    private Dictionary<string, Vector2> BuildBoneDefaultOffsets()
    {
        return new Dictionary<string, Vector2>
        {
            ["Torso"] = Vector2.Zero,
            ["Head"] = new Vector2(0, -_config.HeadOffsetY),
            ["ArmL"] = new Vector2(-_config.ShoulderWidth, -_config.ShoulderY),
            ["ForearmL"] = new Vector2(0, _config.UpperArmLength),
            ["Shield"] = new Vector2(_config.ShieldMountOffset.X, -_config.ShieldMountOffset.Y),
            ["ArmR"] = new Vector2(_config.ShoulderWidth, -_config.ShoulderY),
            ["ForearmR"] = new Vector2(0, _config.UpperArmLength),
            ["Weapon"] = new Vector2(_config.WeaponMountOffset.X, -_config.WeaponMountOffset.Y),
        };
    }

    private void ApplyEquipOffsetOverlay()
    {
        if (_skeleton == null || EquipOffsetConfig == null)
            return;

        var sprite = _skeleton.GetSlotSprite(EquipOffsetConfig.Slot);
        if (sprite == null)
            return;

        sprite.Offset = new Vector2(EquipOffsetConfig.OffsetX, EquipOffsetConfig.OffsetY);
        if (EquipmentOffsetConfig.SupportsRotation(EquipOffsetConfig.Slot))
            sprite.RotationDegrees = EquipOffsetConfig.Rotation;

        if (!Mathf.IsEqualApprox(EquipOffsetConfig.Scale, 1.0f))
            sprite.Scale = new Vector2(EquipOffsetConfig.Scale, EquipOffsetConfig.Scale);
    }
}
