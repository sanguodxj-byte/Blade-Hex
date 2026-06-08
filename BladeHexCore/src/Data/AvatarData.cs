// AvatarData.cs
// 纸娃娃捏脸头像数据模型 — 仅头部部分
// 由种族+性别决定的头部基底 + 发型胡须合并层 + 预留装饰槽位
using Godot;

namespace BladeHex.Data;

/// <summary>
/// 头像数据。存储捏脸所需的所有索引：
/// - 种族 + 性别决定头部基底纹理
/// - HairIndex 控制发型+胡须合并纹理（0 = 光头/无发须）
/// - DecorationId 预留装饰槽（未来扩展：头饰、帽子装饰、面部彩绘等）
/// </summary>
[GlobalClass]
public partial class AvatarData : Resource
{
    // ========================================
    // 核心字段
    // ========================================

    /// <summary>种族</summary>
    [Export] public RaceData.Race RaceId { get; set; } = RaceData.Race.Human;

    /// <summary>性别（"male" / "female"）</summary>
    [Export] public string Gender { get; set; } = "male";

    /// <summary>头部基底索引（1-based，不同种族/性别各有若干变体）</summary>
    [Export] public int HeadIndex { get; set; } = 1;

    /// <summary>发型+胡须合并索引（0 = 光头/无发须，同一纹理包含发型和胡须）</summary>
    [Export] public int HairIndex { get; set; } = 0;

    /// <summary>
    /// 预留装饰槽标识符（未来扩展：特殊头饰、面部纹身、眼罩等）。
    /// 空字符串表示无装饰。格式建议："decoration_xxx"。
    /// </summary>
    [Export] public string DecorationId { get; set; } = "";

    // ========================================
    // 便捷工厂方法
    // ========================================

    /// <summary>
    /// 根据种族和种子随机生成头像数据。
    /// 保证同一 (raceId, seed) 组合生成相同头像。
    /// </summary>
    public static AvatarData Generate(RaceData.Race raceId, int seed)
    {
        var rng = new System.Random(seed);
        bool isFemale = rng.Next(2) == 0;

        return new AvatarData
        {
            RaceId = raceId,
            Gender = isFemale ? "female" : "male",
            HeadIndex = rng.Next(1, 10), // 1..9
            HairIndex = rng.Next(0, 10), // 0..9 (0 = 光头)
            DecorationId = "",
        };
    }

    /// <summary>
    /// 从旧式分散字段迁移创建 AvatarData。
    /// beardIndex 参数保留兼容但不再使用（胡须已合并到发型纹理）。
    /// </summary>
    public static AvatarData FromLegacy(
        RaceData.Race raceId,
        string gender,
        int faceIndex, // 旧字段 FaceIndex 对应 HeadIndex
        int hairIndex,
        int beardIndex = 0) // 保留参数兼容，不再独立使用
    {
        return new AvatarData
        {
            RaceId = raceId,
            Gender = string.IsNullOrEmpty(gender) ? "male" : gender,
            HeadIndex = faceIndex > 0 ? faceIndex : 1,
            HairIndex = hairIndex,
            DecorationId = "",
        };
    }

    // ========================================
    // 工具
    // ========================================

    /// <summary>获取种族字符串标识（小写，用于路径拼接）</summary>
    public string RaceString => RaceId.ToString().ToLower();

    /// <summary>是否有发型（0 = 光头/无发须）</summary>
    public bool HasHair => HairIndex > 0;

    /// <summary>是否有装饰</summary>
    public bool HasDecoration => !string.IsNullOrEmpty(DecorationId);

    /// <summary>克隆</summary>
    public AvatarData Clone()
    {
        return new AvatarData
        {
            RaceId = RaceId,
            Gender = Gender,
            HeadIndex = HeadIndex,
            HairIndex = HairIndex,
            DecorationId = DecorationId,
        };
    }
}
