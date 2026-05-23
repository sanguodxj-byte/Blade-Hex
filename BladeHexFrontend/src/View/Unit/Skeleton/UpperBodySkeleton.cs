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
    // 2D 骨骼节点（在 SubViewport 内）
    // ═══════════════════════════════════════════

    /// <summary>2D 骨骼根</summary>
    public Node2D BoneRoot { get; private set; } = null!;

    /// <summary>躯干骨骼</summary>
    public Node2D BoneTorso { get; private set; } = null!;

    /// <summary>头部骨骼</summary>
    public Node2D BoneHead { get; private set; } = null!;

    /// <summary>左臂骨骼</summary>
    public Node2D BoneArmL { get; private set; } = null!;

    /// <summary>左前臂骨骼</summary>
    public Node2D BoneForearmL { get; private set; } = null!;

    /// <summary>右臂骨骼</summary>
    public Node2D BoneArmR { get; private set; } = null!;

    /// <summary>右前臂骨骼</summary>
    public Node2D BoneForearmR { get; private set; } = null!;

    /// <summary>武器挂载骨骼</summary>
    public Node2D BoneWeapon { get; private set; } = null!;

    /// <summary>盾牌挂载骨骼</summary>
    public Node2D BoneShield { get; private set; } = null!;

    // ═══════════════════════════════════════════
    // Sprite2D 部件（在 SubViewport 内，ZIndex 控制层级）
    // ═══════════════════════════════════════════

    public Sprite2D SpriteBody { get; private set; } = null!;
    public Sprite2D SpriteCostume { get; private set; } = null!;
    public Sprite2D SpriteHead { get; private set; } = null!;
    public Sprite2D SpriteHelmet { get; private set; } = null!;
    public Sprite2D SpriteHands { get; private set; } = null!;
    public Sprite2D SpriteWeapon { get; private set; } = null!;
    public Sprite2D SpriteShield { get; private set; } = null!;

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
        BoneTorso = new Node2D { Name = "Bone_Torso" };
        BoneTorso.Position = Vector2.Zero;
        BoneRoot.AddChild(BoneTorso);

        // 头部（躯干上方）
        BoneHead = new Node2D { Name = "Bone_Head" };
        BoneHead.Position = new Vector2(0, -config.HeadOffsetY); // 向上
        BoneTorso.AddChild(BoneHead);

        // 左臂（肩关节）
        BoneArmL = new Node2D { Name = "Bone_ArmL" };
        BoneArmL.Position = new Vector2(-config.ShoulderWidth, -config.ShoulderY);
        BoneTorso.AddChild(BoneArmL);

        // 左前臂
        BoneForearmL = new Node2D { Name = "Bone_ForearmL" };
        BoneForearmL.Position = new Vector2(0, config.UpperArmLength); // 向下（上臂末端）
        BoneArmL.AddChild(BoneForearmL);

        // 盾牌挂载
        BoneShield = new Node2D { Name = "Bone_Shield" };
        BoneShield.Position = new Vector2(config.ShieldMountOffset.X, -config.ShieldMountOffset.Y);
        BoneForearmL.AddChild(BoneShield);

        // 右臂
        BoneArmR = new Node2D { Name = "Bone_ArmR" };
        BoneArmR.Position = new Vector2(config.ShoulderWidth, -config.ShoulderY);
        BoneTorso.AddChild(BoneArmR);

        // 右前臂
        BoneForearmR = new Node2D { Name = "Bone_ForearmR" };
        BoneForearmR.Position = new Vector2(0, config.UpperArmLength);
        BoneArmR.AddChild(BoneForearmR);

        // 武器挂载
        BoneWeapon = new Node2D { Name = "Bone_Weapon" };
        BoneWeapon.Position = new Vector2(config.WeaponMountOffset.X, -config.WeaponMountOffset.Y);
        BoneForearmR.AddChild(BoneWeapon);
    }

    private void BuildSprites2D(BoneConfig config)
    {
        // ZIndex 层级（越大越靠前）：
        // Body=0, Costume=10, Head=20, Helmet=30, ArmL=-5, Shield=-10, ArmR=5, Hands=40, Weapon=50

        // 身体
        SpriteBody = CreateSprite2D("Sprite_Body", 0);
        SpriteBody.Position = new Vector2(0, -config.TorsoHeight * 0.5f);
        BoneTorso.AddChild(SpriteBody);
        _slotSprites[ItemData.EquipSlot.Body] = SpriteBody;

        // 护甲
        SpriteCostume = CreateSprite2D("Sprite_Costume", 10);
        SpriteCostume.Position = new Vector2(0, -config.TorsoHeight * 0.35f);
        SpriteCostume.Visible = false;
        BoneTorso.AddChild(SpriteCostume);
        _slotSprites[ItemData.EquipSlot.Costume] = SpriteCostume;

        // 头部
        SpriteHead = CreateSprite2D("Sprite_Head", 20);
        SpriteHead.Position = Vector2.Zero;
        BoneHead.AddChild(SpriteHead);
        _slotSprites[ItemData.EquipSlot.Head] = SpriteHead;

        // 头盔
        SpriteHelmet = CreateSprite2D("Sprite_Helmet", 30);
        SpriteHelmet.Position = new Vector2(0, -8);
        SpriteHelmet.Visible = false;
        BoneHead.AddChild(SpriteHelmet);
        _slotSprites[ItemData.EquipSlot.Helmet] = SpriteHelmet;

        // 盾牌（左前臂末端，在身体后面）
        SpriteShield = CreateSprite2D("Sprite_Shield", -10);
        SpriteShield.Position = Vector2.Zero;
        SpriteShield.Visible = false;
        BoneShield.AddChild(SpriteShield);

        // 手甲（右前臂）
        SpriteHands = CreateSprite2D("Sprite_Hands", 40);
        SpriteHands.Position = Vector2.Zero;
        SpriteHands.Visible = false;
        BoneForearmR.AddChild(SpriteHands);
        _slotSprites[ItemData.EquipSlot.Hands] = SpriteHands;

        // 武器（最前层）
        SpriteWeapon = CreateSprite2D("Sprite_Weapon", 50);
        SpriteWeapon.Position = Vector2.Zero;
        SpriteWeapon.Visible = false;
        BoneWeapon.AddChild(SpriteWeapon);
        _slotSprites[ItemData.EquipSlot.Weapon] = SpriteWeapon;
    }

    private static Sprite2D CreateSprite2D(string name, int zIndex)
    {
        return new Sprite2D
        {
            Name = name,
            ZIndex = zIndex,
            Centered = true,
        };
    }

    // ═══════════════════════════════════════════
    // 朝向
    // ═══════════════════════════════════════════

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ 【强制规则 - 禁止修改】                                          ║
    // ║                                                                  ║
    // ║ 实测确认：2D 正 X = 屏幕右侧（无 billboard 镜像）。              ║
    // ║ ArmR 在骨骼正 X 方向，不翻转时出现在屏幕右侧。                   ║
    // ║                                                                  ║
    // ║ 游戏规则：面朝某方向时，武器手（右手）在该方向的反侧。            ║
    // ║ 即：面朝右 → 武器在屏幕左侧。                                    ║
    // ║                                                                  ║
    // ║ 因此：                                                           ║
    // ║   面朝右（facingLeft=false）→ Scale.X = -1（翻转，武器到左侧）   ║
    // ║   面朝左（facingLeft=true） → Scale.X = 1（不翻转，武器到右侧）  ║
    // ║                                                                  ║
    // ║ 【禁止】以任何理由修改此函数的翻转方向。                          ║
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

    /// <summary>重置所有骨骼到零姿态</summary>
    public void ResetPose()
    {
        BoneTorso.Position = Vector2.Zero;
        BoneTorso.RotationDegrees = 0;
        BoneHead.RotationDegrees = 0;
        BoneArmL.RotationDegrees = 0;
        BoneArmR.RotationDegrees = 0;
        BoneForearmL.RotationDegrees = 0;
        BoneForearmR.RotationDegrees = 0;
        BoneWeapon.RotationDegrees = 0;
    }
}
