// BoneConfig.cs
// 上半身骨骼参数配置 — 定义骨骼尺寸、关节位置等
// 支持多种体型变体（标准/重型/纤细/大型）
using Godot;

namespace BladeHex.View.Unit.Skeleton;

/// <summary>体型枚举</summary>
public enum BodyType
{
    /// <summary>标准人类体型（人类、精灵）</summary>
    Standard = 0,
    /// <summary>宽肩厚胸（矮人、兽人）</summary>
    Heavy = 1,
    /// <summary>纤细（精灵法师、刺客）</summary>
    Slim = 2,
    /// <summary>大型（Boss、巨人）</summary>
    Large = 3,
}

/// <summary>
/// 上半身骨骼参数配置。
/// 所有数值为像素坐标，运行时乘以 PixelSize 转换为世界坐标。
/// </summary>
public sealed class BoneConfig
{
    // ─── 整体参数 ───

    /// <summary>底座顶部 Y 坐标（骨骼根起点）</summary>
    public float PedestalTopY { get; init; } = 40.0f;

    /// <summary>Sprite3D 像素到世界单位的换算</summary>
    public float PixelSize { get; init; } = 0.65f;

    // ─── 躯干 ───

    /// <summary>躯干高度（肩到腰）</summary>
    public float TorsoHeight { get; init; } = 48.0f;

    /// <summary>躯干宽度（用于 Body/Costume sprite 定位）</summary>
    public float TorsoWidth { get; init; } = 40.0f;

    // ─── 肩部 ───

    /// <summary>肩宽（中心到肩关节的水平距离）</summary>
    public float ShoulderWidth { get; init; } = 24.0f;

    /// <summary>肩关节相对躯干底部的 Y 偏移</summary>
    public float ShoulderY { get; init; } = 40.0f;

    // ─── 手臂 ───

    /// <summary>上臂长度</summary>
    public float UpperArmLength { get; init; } = 20.0f;

    /// <summary>前臂长度</summary>
    public float ForearmLength { get; init; } = 18.0f;

    // ─── 头部 ───

    /// <summary>头部中心相对躯干底部的 Y 偏移</summary>
    public float HeadOffsetY { get; init; } = 56.0f;

    // ─── 武器挂载 ───

    /// <summary>武器挂载点相对前臂末端的偏移</summary>
    public Vector3 WeaponMountOffset { get; init; } = new(0, -8, 0);

    // ─── 盾牌挂载 ───

    /// <summary>盾牌挂载点相对左前臂末端的偏移</summary>
    public Vector3 ShieldMountOffset { get; init; } = new(0, -6, 0);

    // ─── 预设体型 ───

    public static BoneConfig Standard => new()
    {
        PedestalTopY = 80.0f,
        PixelSize = 0.65f,
        TorsoHeight = 96.0f,
        TorsoWidth = 80.0f,
        ShoulderWidth = 48.0f,
        ShoulderY = 80.0f,
        UpperArmLength = 40.0f,
        ForearmLength = 36.0f,
        HeadOffsetY = 124.0f,
        WeaponMountOffset = new Vector3(0, -16, 0),
        ShieldMountOffset = new Vector3(0, -12, 0),
    };

    public static BoneConfig Heavy => new()
    {
        PedestalTopY = 80.0f,
        PixelSize = 0.65f,
        TorsoHeight = 88.0f,
        TorsoWidth = 104.0f,
        ShoulderWidth = 60.0f,
        ShoulderY = 72.0f,
        UpperArmLength = 36.0f,
        ForearmLength = 32.0f,
        HeadOffsetY = 100.0f,
        WeaponMountOffset = new Vector3(0, -16, 0),
        ShieldMountOffset = new Vector3(0, -12, 0),
    };

    public static BoneConfig Slim => new()
    {
        PedestalTopY = 80.0f,
        PixelSize = 0.65f,
        TorsoHeight = 104.0f,
        TorsoWidth = 68.0f,
        ShoulderWidth = 40.0f,
        ShoulderY = 88.0f,
        UpperArmLength = 44.0f,
        ForearmLength = 40.0f,
        HeadOffsetY = 120.0f,
        WeaponMountOffset = new Vector3(0, -16, 0),
        ShieldMountOffset = new Vector3(0, -12, 0),
    };

    public static BoneConfig Large => new()
    {
        PedestalTopY = 100.0f,
        PixelSize = 0.85f,
        TorsoHeight = 112.0f,
        TorsoWidth = 112.0f,
        ShoulderWidth = 64.0f,
        ShoulderY = 96.0f,
        UpperArmLength = 48.0f,
        ForearmLength = 44.0f,
        HeadOffsetY = 128.0f,
        WeaponMountOffset = new Vector3(0, -20, 0),
        ShieldMountOffset = new Vector3(0, -16, 0),
    };

    /// <summary>根据体型枚举获取配置</summary>
    public static BoneConfig FromBodyType(BodyType type) => type switch
    {
        BodyType.Heavy => Heavy,
        BodyType.Slim => Slim,
        BodyType.Large => Large,
        _ => Standard,
    };
}
