using Godot;

namespace BladeHex.Combat.AI;

/// <summary>
/// 难度配置 —— 通过计算属性影响 AI 决策质量
/// 对应策划案 09-AI系统 → 三、难度差异化设计
/// </summary>
[GlobalClass]
public partial class AIDifficultyConfig : Resource
{
    public enum DifficultyLevel
    {
        Easy,       // 简单：AI笨拙，忽略地形/包夹，随机选目标，40%失误率
        Normal,     // 普通：使用策略风格，有基本战术意识，15%失误率
        Hard,       // 困难：完整战术意识，善用地形和包夹，5%失误率
        Legendary   // 传奇：完美决策，协同集火，0失误率
    }

    [Export] public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Normal;

    /// <summary>目标选择精度：0.0=随机，1.0=完美</summary>
    public float TargetSelectionAccuracy => Difficulty switch
    {
        DifficultyLevel.Easy => 0.3f,
        DifficultyLevel.Normal => 0.7f,
        DifficultyLevel.Hard => 0.9f,
        DifficultyLevel.Legendary => 1.0f,
        _ => 0.7f
    };

    /// <summary>AI 是否考虑地形加成</summary>
    public bool UsesTerrain => Difficulty >= DifficultyLevel.Hard;

    /// <summary>AI 是否尝试包夹机动</summary>
    public bool UsesFlanking => Difficulty >= DifficultyLevel.Normal;

    /// <summary>AI 是否协同集火</summary>
    public bool UsesFocusFire => Difficulty >= DifficultyLevel.Hard;

    /// <summary>撤退HP阈值倍率（基础阈值25%，乘以这个系数）</summary>
    public float RetreatThresholdMultiplier => Difficulty switch
    {
        DifficultyLevel.Easy => 0.5f,
        DifficultyLevel.Normal => 1.0f,
        DifficultyLevel.Hard => 1.0f,
        DifficultyLevel.Legendary => 0.8f,
        _ => 1.0f
    };

    /// <summary>AI 做出"失误"（降级为本能策略）的概率</summary>
    public float MistakeChance => Difficulty switch
    {
        DifficultyLevel.Easy => 0.4f,
        DifficultyLevel.Normal => 0.15f,
        DifficultyLevel.Hard => 0.05f,
        DifficultyLevel.Legendary => 0.0f,
        _ => 0.15f
    };

    /// <summary>AI 是否利用冲锋加成</summary>
    public bool UsesCharge => Difficulty >= DifficultyLevel.Normal;

    /// <summary>AI 是否考虑控制区</summary>
    public bool UsesZoneOfControl => Difficulty >= DifficultyLevel.Hard;

    /// <summary>获取难度显示名</summary>
    public string GetDifficultyName() => Difficulty switch
    {
        DifficultyLevel.Easy => "简单",
        DifficultyLevel.Normal => "普通",
        DifficultyLevel.Hard => "困难",
        DifficultyLevel.Legendary => "传奇",
        _ => "普通"
    };
}
