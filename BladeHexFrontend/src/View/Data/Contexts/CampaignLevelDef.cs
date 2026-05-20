using Godot;

namespace BladeHex.Data.Contexts;

/// <summary>
/// 单个战役关卡定义 — 描述敌方配置、地图模板、奖励等。
/// </summary>
public class CampaignLevelDef
{
    /// <summary>关卡显示名称。</summary>
    public string Name { get; set; } = "";

    /// <summary>关卡描述/剧情文本。</summary>
    public string Description { get; set; } = "";

    /// <summary>地图模板名（空=随机）。</summary>
    public string MapTemplate { get; set; } = "";

    /// <summary>战斗规模（0=小, 1=中, 2=大, 3=巨大）。</summary>
    public int BattleSize { get; set; } = 1;

    /// <summary>敌方数量。</summary>
    public int EnemyCount { get; set; } = 3;

    /// <summary>敌方等级。</summary>
    public int EnemyLevel { get; set; } = 1;

    /// <summary>敌方种类（0=人形, 1=亡灵, 2=野兽, 3=混合）。</summary>
    public int EnemyType { get; set; }

    /// <summary>难度（0=Easy, 1=Normal, 2=Hard）。</summary>
    public int Difficulty { get; set; } = 1;

    /// <summary>旧版通关金币奖励字段已废弃；战役奖励由 CampaignPricingService 根据关卡配置动态生成。</summary>
    [System.Obsolete("Use BladeHex.Strategic.Economy.CampaignPricingService.GetBattleGoldReward(...) instead.")]
    public int GoldReward { get; set; }

    /// <summary>是否为 Boss 关。</summary>
    public bool IsBoss { get; set; }
}
