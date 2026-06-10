// UpperBodySkeleton.cs
// 上半身骨骼系统 — SubViewport 2D 合成方案
// 所有部件在 SubViewport 内用 Sprite2D + ZIndex 渲染，输出到单个 Sprite3D billboard
// 彻底解决 3D 深度排序导致的层级闪烁问题
using Godot;
using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.View.Unit.Skeleton;

/// <summary>
/// 上半身骨骼构建器（SubViewport 2D 合成版）。
/// <para>在 SubViewport 内构建 2D 骨骼树，各部件用 ZIndex 控制层级。</para>
/// <para>SubViewport 的纹理输出到一个 billboard Sprite3D 放在 3D 场景中。</para>
/// </summary>
public sealed class UpperBodySkeleton
{
    // ═══════════════════════════════════════════
    // 常量
    // ═══════════════════════════════════════════

    /// <summary>SubViewport 画布尺寸（像素）</summary>
    public const int ViewportWidth = 512;
    public const int ViewportHeight = 640;

    /// <summary>画布中心（角色脚底在底部中央）</summary>
    public static readonly Vector2 CanvasCenter = new(ViewportWidth / 2f, ViewportHeight * 0.85f);

    // ═══════════════════════════════════════════
    // 3D 侧节点（放在 3D 场景中）
    // ═══════════════════════════════════════════

    /// <summary>3D 根节点（挂在 BodyRoot 下）</summary>
    public Node3D Root { get; private set; } = null!;

    /// <summary>输出 billboard（单个 Sprite3D，纹理来自 SubViewport）</summary>
    public Sprite3D Billboard { get; private set; } = null!;

    /// <summary>SubViewport 节点</summary>
    public SubViewport Viewport { get; private set; } = null!;

    // ═══════════════════════════════════════════
    // 动态存储容器（拓扑解耦）
    // ═══════════════════════════════════════════

    private readonly Dictionary<string, Node2D> _bones = new();
    private readonly Dictionary<string, Sprite2D> _slots = new();

    // ═══════════════════════════════════════════
    // 2D 骨骼节点（在 SubViewport 内）- 只读 Shortcut 属性保障兼容
    // ═══════════════════════════════════════════

    /// <summary>2D 骨骼根</summary>
    public Node2D BoneRoot { get; private set; } = null!;

    /// <summary>躯干骨骼</summary>
    public Node2D BoneTorso => _bones.GetValueOrDefault("Torso")!;

    /// <summary>头部骨骼</summary>
    public Node2D BoneHead => _bones.GetValueOrDefault("Head")!;

    /// <summary>左臂骨骼</summary>
    public Node2D BoneArmL => _bones.GetValueOrDefault("ArmL")!;

    /// <summary>左前臂骨骼</summary>
    public Node2D BoneForearmL => _bones.GetValueOrDefault("ForearmL")!;

    /// <summary>右臂骨骼</summary>
    public Node2D BoneArmR => _bones.GetValueOrDefault("ArmR")!;

    /// <summary>右前臂骨骼</summary>
    public Node2D BoneForearmR => _bones.GetValueOrDefault("ForearmR")!;

    /// <summary>武器挂载骨骼</summary>
    public Node2D BoneWeapon => _bones.GetValueOrDefault("Weapon")!;

    /// <summary>盾牌挂载骨骼</summary>
    public Node2D BoneShield => _bones.GetValueOrDefault("Shield")!;

    // ═══════════════════════════════════════════
    // Sprite2D 部件（在 SubViewport 内）- 只读 Shortcut 属性保障兼容
    // ═══════════════════════════════════════════

    public Sprite2D SpriteBody => _slots.GetValueOrDefault("Body")!;
    public Sprite2D SpriteCostume => _slots.GetValueOrDefault("Costume")!;
    public Sprite2D SpriteHead => _slots.GetValueOrDefault("Head")!;
    public Sprite2D SpriteHair => _slots.GetValueOrDefault("Hair")!;
    public Sprite2D SpriteHelmet => _slots.GetValueOrDefault("Helmet")!;
    public Sprite2D SpriteHands => _slots.GetValueOrDefault("Hands")!;
    public Sprite2D SpriteHandsL => _slots.GetValueOrDefault("HandsL")!;
    public Sprite2D SpriteWeapon => _slots.GetValueOrDefault("Weapon")!;
    public Sprite2D SpriteShield => _slots.GetValueOrDefault("Shield")!;


    // ─── 配置 ───
    public BoneConfig Config { get; private set; } = null!;

    // ─── Sprite 按 slot 索引 ───
    private readonly Dictionary<ItemData.EquipSlot, Sprite2D> _slotSprites = new();

    public Sprite2D? GetSlotSprite(ItemData.EquipSlot slot)
        => _slotSprites.GetValueOrDefault(slot);

    public IReadOnlyDictionary<ItemData.EquipSlot, Sprite2D> SlotSprites => _slotSprites;

    // ═══════════════════════════════════════════
    // 构建
    // ═══════════════════════════════════════════

    /// <summary>
    /// 构建骨骼系统并挂载到 parent（3D 节点）下。
    /// </summary>
    public void Build(Node3D parent, BoneConfig config)
    {
        Config = config;

        // ─── 3D 侧：Root + SubViewport + Billboard ───
        Root = new Node3D { Name = "SkeletonRoot" };
        Root.Position = new Vector3(0, config.PedestalTopY * config.PixelSize, 0);
        parent.AddChild(Root);

        Viewport = new SubViewport
        {
            Name = "CharViewport",
            Size = new Vector2I(ViewportWidth, ViewportHeight),
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa2D = SubViewport.Msaa.Msaa4X,
            // 不需要自己的 World2D/3D，共享父级即可
            // 但我们需要隔离 2D 内容，所以用独立 world
            OwnWorld3D = false,
        };
        Root.AddChild(Viewport);

        Billboard = new Sprite3D
        {
            Name = "CharBillboard",
            Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
            AlphaCut = SpriteBase3D.AlphaCutMode.Disabled,
            Transparent = true,
            PixelSize = config.PixelSize,
            Centered = true,
            // Offset 补偿：角色脚底在画布 85% 高度处，需要向上偏移使脚底对齐 3D 原点
            Offset = new Vector2(0, -(ViewportHeight * 0.5f - ViewportHeight * 0.85f)),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
            Shaded = false,
            // 底座+半身风格：不投真实影(半身剪影会显怪)，单位接地靠底座 + 脚下 blob 接触阴影
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        Root.AddChild(Billboard);

        // ─── 2D 侧：骨骼树（在 SubViewport 内） ───
        BoneRoot = new Node2D { Name = "BoneRoot" };
        BoneRoot.Position = CanvasCenter;
        Viewport.AddChild(BoneRoot);

        BuildBones2D(config);
        BuildSprites2D(config);

        // 绑定 ViewportTexture — SubViewport 已在树中，GetTexture() 返回有效引用
        Billboard.Texture = Viewport.GetTexture();
    }

    private void BuildBones2D(BoneConfig config)
    {
        // 2D 坐标系：Y 向下为正，所以骨骼向上用负 Y
        // 单位：像素（SubViewport 内的像素坐标）

        // 躯干
        var torso = new Node2D { Name = "Bone_Torso" };
        torso.Position = Vector2.Zero;
        BoneRoot.AddChild(torso);
        _bones["Torso"] = torso;

        // 头部（躯干上方）
        var head = new Node2D { Name = "Bone_Head" };
        head.Position = new Vector2(0, -config.HeadOffsetY); // 向上
        torso.AddChild(head);
        _bones["Head"] = head;

        // 左臂（肩关节）
        var armL = new Node2D { Name = "Bone_ArmL" };
        armL.Position = new Vector2(-config.ShoulderWidth, -config.ShoulderY);
        torso.AddChild(armL);
        _bones["ArmL"] = armL;

        // 左前臂
        var forearmL = new Node2D { Name = "Bone_ForearmL" };
        forearmL.Position = new Vector2(0, config.UpperArmLength); // 向下（上臂末端）
        armL.AddChild(forearmL);
        _bones["ForearmL"] = forearmL;

        // 盾牌挂载
        var shield = new Node2D { Name = "Bone_Shield" };
        shield.Position = new Vector2(config.ShieldMountOffset.X, -config.ShieldMountOffset.Y);
        forearmL.AddChild(shield);
        _bones["Shield"] = shield;

        // 右臂
        var armR = new Node2D { Name = "Bone_ArmR" };
        armR.Position = new Vector2(config.ShoulderWidth, -config.ShoulderY);
        torso.AddChild(armR);
        _bones["ArmR"] = armR;

        // 右前臂
        var forearmR = new Node2D { Name = "Bone_ForearmR" };
        forearmR.Position = new Vector2(0, config.UpperArmLength);
        armR.AddChild(forearmR);
        _bones["ForearmR"] = forearmR;

        // 武器挂载
        var weapon = new Node2D { Name = "Bone_Weapon" };
        weapon.Position = new Vector2(config.WeaponMountOffset.X, -config.WeaponMountOffset.Y);
        forearmR.AddChild(weapon);
        _bones["Weapon"] = weapon;
    }

    private void BuildSprites2D(BoneConfig config)
    {
        // ZIndex 层级（越大越靠前）：
        // Body=0, Head=5, Costume=10, Helmet=30, ArmL=-5, Shield=-10, ArmR=5, Hands=40, Weapon=50

        // 身体
        var body = CreateSprite2D("Sprite_Body", 0);
        body.Position = new Vector2(0, -config.TorsoHeight * 0.5f);
        BoneTorso.AddChild(body);
        _slots["Body"] = body;
        _slotSprites[ItemData.EquipSlot.Body] = body;

        // 护甲
        var costume = CreateSprite2D("Sprite_Costume", 10);
        costume.Position = new Vector2(0, -config.TorsoHeight * 0.35f);
        costume.Visible = false;
        BoneTorso.AddChild(costume);
        _slots["Costume"] = costume;
        _slotSprites[ItemData.EquipSlot.Costume] = costume;

        // 头部
        var head = CreateSprite2D("Sprite_Head", 5);
        head.Position = Vector2.Zero;
        BoneHead.AddChild(head);
        _slots["Head"] = head;
        _slotSprites[ItemData.EquipSlot.Head] = head;

        // 发型+胡须合并层 (ZIndex = 25，纹理内已包含胡须)
        var hair = CreateSprite2D("Sprite_Hair", 25);
        hair.Position = Vector2.Zero;
        hair.Visible = false;
        BoneHead.AddChild(hair);
        _slots["Hair"] = hair;
        _slotSprites[ItemData.EquipSlot.Hair] = hair;

        // 头盔
        var helmet = CreateSprite2D("Sprite_Helmet", 30);
        helmet.Position = new Vector2(0, -16);
        helmet.Visible = false;
        BoneHead.AddChild(helmet);
        _slots["Helmet"] = helmet;
        _slotSprites[ItemData.EquipSlot.Helmet] = helmet;

        // 盾牌（左前臂末端，在身体后面）
        var shield = CreateSprite2D("Sprite_Shield", -10);
        shield.Position = Vector2.Zero;
        shield.Visible = false;
        BoneShield.AddChild(shield);
        _slots["Shield"] = shield;
        _slotSprites[ItemData.EquipSlot.Shield] = shield;

        // 手甲（右前臂）
        var hands = CreateSprite2D("Sprite_Hands", 40);
        hands.Position = Vector2.Zero;
        hands.Visible = false;
        BoneForearmR.AddChild(hands);
        _slots["Hands"] = hands;
        _slotSprites[ItemData.EquipSlot.Hands] = hands;

        // 手甲（左前臂 — 新增，挂载在左前臂上以实现左手套的拆分同步）
        var handsL = CreateSprite2D("Sprite_HandsL", 40);
        handsL.Position = Vector2.Zero;
        handsL.Visible = false;
        BoneForearmL.AddChild(handsL);
        _slots["HandsL"] = handsL;

        // 武器（最前层）
        var weapon = CreateSprite2D("Sprite_Weapon", 50);
        weapon.Position = Vector2.Zero;
        weapon.Visible = false;
        BoneWeapon.AddChild(weapon);
        _slots["Weapon"] = weapon;
        _slotSprites[ItemData.EquipSlot.Weapon] = weapon;
    }

    private static Sprite2D CreateSprite2D(string name, int zIndex)
    {
        return new Sprite2D
        {
            Name = name,
            ZIndex = zIndex,
            Centered = true,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
        };
    }

    // ═══════════════════════════════════════════
    // 朝向
    // ═══════════════════════════════════════════

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ 【朝向规则 - 整体镜像翻转】                                      ║
    // ║                                                                  ║
    // ║ 美术资源默认面向右侧，但渲染输出时整体镜像了一次，实际呈现为面朝左。║
    // ║ 因此 Scale.X 符号需要相对于"贴图朝向"反转一次。                    ║
    // ║                                                                  ║
    // ║ 游戏规则：                                                       ║
    // ║   面朝右（facingLeft=false）→ Scale.X = -1（镜像，呈现朝右）       ║
    // ║   面朝左（facingLeft=true） → Scale.X = 1（不镜像，呈现朝左）      ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>设置朝向（2D 镜像）</summary>
    public void SetFacing(bool facingLeft)
    {
        BoneRoot.Scale = new Vector2(facingLeft ? 1 : -1, 1);
    }

    // ═══════════════════════════════════════════
    // 动画（保留旧接口兼容）
    // ═══════════════════════════════════════════

    /// <summary>AnimationPlayer（兼容旧代码，但新方案不再使用）</summary>
    public AnimationPlayer? AnimPlayer => null;

    /// <summary>播放动画（空实现，由 CharacterRenderNode 的 AnimClip 系统驱动）</summary>
    public void PlayAnimation(string animName) { }

    /// <summary>暂停 SubViewport 更新（单位静止时节省性能）</summary>
    public void PauseViewport()
    {
        if (Viewport != null)
            Viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    }

    /// <summary>恢复 SubViewport 更新（单位开始动画时）</summary>
    public void ResumeViewport()
    {
        if (Viewport != null)
            Viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
    }

    /// <summary>重置所有骨骼到零姿态（基于骨骼默认偏移回位，杜绝脱节）</summary>
    public void ResetPose()
    {
        // 1. 各骨骼节点恢复到 Config 对应的默认局部坐标
        if (_bones.TryGetValue("Torso", out var torso)) torso.Position = Vector2.Zero;
        if (_bones.TryGetValue("Head", out var head)) head.Position = new Vector2(0, -Config.HeadOffsetY);
        if (_bones.TryGetValue("ArmL", out var armL)) armL.Position = new Vector2(-Config.ShoulderWidth, -Config.ShoulderY);
        if (_bones.TryGetValue("ForearmL", out var forearmL)) forearmL.Position = new Vector2(0, Config.UpperArmLength);
        if (_bones.TryGetValue("Shield", out var shield)) shield.Position = new Vector2(Config.ShieldMountOffset.X, -Config.ShieldMountOffset.Y);
        if (_bones.TryGetValue("ArmR", out var armR)) armR.Position = new Vector2(Config.ShoulderWidth, -Config.ShoulderY);
        if (_bones.TryGetValue("ForearmR", out var forearmR)) forearmR.Position = new Vector2(0, Config.UpperArmLength);
        if (_bones.TryGetValue("Weapon", out var weapon)) weapon.Position = new Vector2(Config.WeaponMountOffset.X, -Config.WeaponMountOffset.Y);

        // 2. 清空所有的旋转与缩放
        foreach (var bone in _bones.Values)
        {
            bone.RotationDegrees = 0;
            bone.Scale = Vector2.One;
        }
    }
}
