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
        PedestalTopY = 40.0f,
        PixelSize = 0.65f,
        TorsoHeight = 48.0f,
        TorsoWidth = 40.0f,
        ShoulderWidth = 24.0f,
        ShoulderY = 40.0f,
        UpperArmLength = 20.0f,
        ForearmLength = 18.0f,
        HeadOffsetY = 62.0f,
        WeaponMountOffset = new Vector3(0, -8, 0),
        ShieldMountOffset = new Vector3(0, -6, 0),
    };

    public static BoneConfig Heavy => new()
    {
        PedestalTopY = 40.0f,
        PixelSize = 0.65f,
        TorsoHeight = 44.0f,
        TorsoWidth = 52.0f,
        ShoulderWidth = 30.0f,
        ShoulderY = 36.0f,
        UpperArmLength = 18.0f,
        ForearmLength = 16.0f,
        HeadOffsetY = 50.0f,
        WeaponMountOffset = new Vector3(0, -8, 0),
        ShieldMountOffset = new Vector3(0, -6, 0),
    };

    public static BoneConfig Slim => new()
    {
        PedestalTopY = 40.0f,
        PixelSize = 0.65f,
        TorsoHeight = 52.0f,
        TorsoWidth = 34.0f,
        ShoulderWidth = 20.0f,
        ShoulderY = 44.0f,
        UpperArmLength = 22.0f,
        ForearmLength = 20.0f,
        HeadOffsetY = 60.0f,
        WeaponMountOffset = new Vector3(0, -8, 0),
        ShieldMountOffset = new Vector3(0, -6, 0),
    };

    public static BoneConfig Large => new()
    {
        PedestalTopY = 50.0f,
        PixelSize = 0.85f,
        TorsoHeight = 56.0f,
        TorsoWidth = 56.0f,
        ShoulderWidth = 32.0f,
        ShoulderY = 48.0f,
        UpperArmLength = 24.0f,
        ForearmLength = 22.0f,
        HeadOffsetY = 64.0f,
        WeaponMountOffset = new Vector3(0, -10, 0),
        ShieldMountOffset = new Vector3(0, -8, 0),
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
