// AnimEditorPreview.cs
// 运行时骨骼动画编辑器 — 3D 预览区（SubViewport 2D 合成版）
// 持有 UpperBodySkeleton 实例（内部为 2D 骨骼），暴露 ApplyPose() 驱动骨骼姿态
// 骨骼操作在 2D 空间进行，通过 SubViewport 输出到 3D billboard
using Godot;
using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.View.Unit.Skeleton.Editor;

/// <summary>
/// 动画编辑器的 3D 预览组件（SubViewport 2D 合成版）。
/// 骨骼节点为 Node2D（在 SubViewport 内），层级由 ZIndex 控制。
/// </summary>
public partial class AnimEditorPreview : Node3D
{
    private UpperBodySkeleton? _skeleton;
    private Node3D? _bodyRoot;
    private Sprite3D? _basePedestal;
    private BoneConfig _config = BoneConfig.Standard;

    // 骨骼关节 gizmo 绘制层（屏幕空间 Control，由 AnimEditorScene 创建并挂载到 CanvasLayer）
    private BoneGizmoOverlay? _gizmoOverlay;

    // 骨骼名 → Node2D 映射（用于 ApplyPose 和拖拽检测）
    private readonly Dictionary<string, Node2D> _boneNodes = new();

    /// <summary>当前是否处于播放模式</summary>
    public bool IsPlaying { get; set; }

    /// <summary>当前播放时间</summary>
    public float PlayTime { get; set; }

    /// <summary>当前编辑的动画片段</summary>
    public AnimClip? CurrentClip { get; set; }

    /// <summary>是否显示骨骼关节 gizmo</summary>
    public bool ShowGizmos
    {
        get => _gizmoOverlay?.Visible ?? true;
        set { if (_gizmoOverlay != null) _gizmoOverlay.Visible = value; }
    }

    public override void _Ready()
    {
        Rebuild(BodyType.Standard);
    }

    public override void _Process(double delta)
    {
        if (!IsPlaying || CurrentClip == null) return;

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

        // 叠加部件偏移配置
        ApplyEquipOffsetOverlay();
    }

    /// <summary>当前部件偏移配置（由外部设置）</summary>
    public EquipmentOffsetConfig? EquipOffsetConfig { get; set; }

    /// <summary>获取内部骨骼实例</summary>
    public UpperBodySkeleton? GetSkeleton() => _skeleton;

    /// <summary>获取骨骼名→Node2D 映射（供拖拽检测用）</summary>
    public IReadOnlyDictionary<string, Node2D>? GetBoneNodes() => _boneNodes.Count > 0 ? _boneNodes : null;

    /// <summary>设置 gizmo 高亮的骨骼名</summary>
    public void SetGizmoSelectedBone(string? boneName)
    {
        if (_gizmoOverlay != null)
            _gizmoOverlay.SelectedBone = boneName;
    }

    /// <summary>
    /// 创建屏幕空间 gizmo overlay 并挂载到指定 UI 容器。
    /// 必须在 Rebuild 之后调用（需要 _boneNodes 已填充）。
    /// </summary>
    /// <param name="uiParent">UI 层容器（CanvasLayer 的子节点）</param>
    /// <param name="camera">3D 相机（用于投影计算）</param>
    public void CreateGizmoOverlay(Control uiParent, Camera3D camera)
    {
        // 如果已有旧的 gizmo，先移除
        if (_gizmoOverlay != null)
        {
            _gizmoOverlay.QueueFree();
            _gizmoOverlay = null;
        }

        _gizmoOverlay = new BoneGizmoOverlay(_boneNodes);
        _gizmoOverlay.Camera = camera;
        _gizmoOverlay.Billboard = _skeleton?.Billboard;
        _gizmoOverlay.Config = _config;
        uiParent.AddChild(_gizmoOverlay);
    }

    /// <summary>更新 gizmo 的 billboard 引用（Rebuild 后需要刷新）</summary>
    public void RefreshGizmoReferences()
    {
        if (_gizmoOverlay == null) return;
        _gizmoOverlay.Billboard = _skeleton?.Billboard;
        _gizmoOverlay.Config = _config;
    }

    /// <summary>重建骨骼（体型变化时调用）</summary>
    public void Rebuild(BodyType bodyType)
    {
        if (_bodyRoot != null)
        {
            _bodyRoot.QueueFree();
            _bodyRoot = null;
        }
        _boneNodes.Clear();

        _config = BoneConfig.FromBodyType(bodyType);

        _bodyRoot = new Node3D { Name = "BodyRoot" };
        AddChild(_bodyRoot);

        // 底座
        _basePedestal = new Sprite3D
        {
            Name = "BasePedestal",
            PixelSize = _config.PixelSize * 0.4f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
            RotationDegrees = new Vector3(-90, 0, 0),
            Position = new Vector3(0, 0.5f, 0),
            AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass,
            SortingOffset = 2.0f,
        };
        var baseTex = GD.Load<Texture2D>("res://assets/generated_ui_icons/unit_base_steel.png");
        if (baseTex != null)
            _basePedestal.Texture = baseTex;
        else
        {
            var ph = new PlaceholderTexture2D { Size = new Vector2(64, 64) };
            _basePedestal.Texture = ph;
            _basePedestal.Modulate = new Color(0.5f, 0.5f, 0.5f);
        }
        _bodyRoot.AddChild(_basePedestal);

        // 骨骼（SubViewport 2D 合成）
        _skeleton = new UpperBodySkeleton();
        _skeleton.Build(_bodyRoot, _config);

        // 建立名称映射（Node2D 骨骼）
        _boneNodes["Torso"] = _skeleton.BoneTorso;
        _boneNodes["Head"] = _skeleton.BoneHead;
        _boneNodes["ArmL"] = _skeleton.BoneArmL;
        _boneNodes["ArmR"] = _skeleton.BoneArmR;
        _boneNodes["ForearmL"] = _skeleton.BoneForearmL;
        _boneNodes["ForearmR"] = _skeleton.BoneForearmR;
        _boneNodes["Weapon"] = _skeleton.BoneWeapon;
        _boneNodes["Shield"] = _skeleton.BoneShield;

        // 不显示纹理占位符 — 只有从纹理面板选择后才显示
        HideAllSprites();
    }

    /// <summary>隐藏所有 sprite</summary>
    private void HideAllSprites()
    {
        if (_skeleton == null) return;
        _skeleton.SpriteBody.Visible = false;
        _skeleton.SpriteCostume.Visible = false;
        _skeleton.SpriteHead.Visible = false;
        _skeleton.SpriteHelmet.Visible = false;
        _skeleton.SpriteHands.Visible = false;
        _skeleton.SpriteWeapon.Visible = false;
        _skeleton.SpriteShield.Visible = false;
    }

    /// <summary>应用一组骨骼姿态到 2D 骨骼节点</summary>
    public void ApplyPose(Dictionary<string, BonePose> pose)
    {
        foreach (var (boneName, p) in pose)
        {
            if (!_boneNodes.TryGetValue(boneName, out var node)) continue;

            node.RotationDegrees = p.RotationZ;

            // Weapon: 动画偏移作用于骨骼节点位置，不覆盖 Sprite.Offset（由 EquipmentOffsetConfig 独占）
            if (boneName == "Weapon" && _skeleton != null)
            {
                // 不修改 BoneWeapon.Position — 它由 WeaponMountOffset 固定
                // 动画的 PositionX/Y 不再用于 Weapon（改用 EquipmentOffsetConfig）
            }
            else if (boneName == "Torso")
            {
                node.Position = new Vector2(0, -p.PositionY); // 2D: Y 向上为负
            }
        }
    }

    /// <summary>应用单个骨骼姿态（编辑时实时更新用）</summary>
    public void ApplyBonePose(string boneName, BonePose pose)
    {
        if (!_boneNodes.TryGetValue(boneName, out var node)) return;

        node.RotationDegrees = pose.RotationZ;

        if (boneName == "Weapon" && _skeleton != null)
        {
            // 不修改 BoneWeapon.Position — 由 WeaponMountOffset 固定
        }
        else if (boneName == "Torso")
        {
            node.Position = new Vector2(0, -pose.PositionY);
        }
    }

    /// <summary>重置所有骨骼到零姿态</summary>
    public void ResetPose()
    {
        foreach (var node in _boneNodes.Values)
            node.RotationDegrees = 0;
        if (_boneNodes.TryGetValue("Torso", out var torso))
            torso.Position = Vector2.Zero;
    }

    /// <summary>开始播放</summary>
    public void Play()
    {
        PlayTime = 0;
        IsPlaying = true;
    }

    /// <summary>暂停</summary>
    public void Pause() => IsPlaying = false;

    /// <summary>跳到指定时间并应用姿态（编辑模式）</summary>
    public void SeekTo(float time)
    {
        PlayTime = time;
        if (CurrentClip != null)
        {
            var pose = AnimClipInterpolator.Sample(CurrentClip, time);
            ApplyPose(pose);
        }
    }

    /// <summary>叠加部件偏移配置到对应 sprite</summary>
    private void ApplyEquipOffsetOverlay()
    {
        if (_skeleton == null || EquipOffsetConfig == null) return;
        var sprite = _skeleton.GetSlotSprite(EquipOffsetConfig.Slot);
        if (sprite == null) return;

        sprite.Offset = new Vector2(EquipOffsetConfig.OffsetX, EquipOffsetConfig.OffsetY);

        // 武器：应用旋转
        if (EquipmentOffsetConfig.SupportsRotation(EquipOffsetConfig.Slot))
            sprite.RotationDegrees = EquipOffsetConfig.Rotation;

        // 缩放
        if (!Mathf.IsEqualApprox(EquipOffsetConfig.Scale, 1.0f))
            sprite.Scale = new Vector2(EquipOffsetConfig.Scale, EquipOffsetConfig.Scale);
    }

    /// <summary>
    /// 为 Sprite2D 设置纹理，1:1 像素显示，不缩放。
    /// 素材本身已按正确尺寸制作，直接使用原始分辨率。
    /// </summary>
    public void ApplyTextureWithScale(ItemData.EquipSlot slot, Sprite2D sprite, Texture2D texture)
    {
        sprite.Texture = texture;
        sprite.Visible = true;
        sprite.Scale = Vector2.One;
    }

}
