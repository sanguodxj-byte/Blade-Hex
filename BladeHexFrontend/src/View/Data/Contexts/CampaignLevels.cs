using System.Collections.Generic;

namespace BladeHex.Data.Contexts;

/// <summary>
/// 预设战役关卡列表 — 定义所有战役关卡的敌方配置。
/// 金币奖励不再在此硬编码，由 CampaignPricingService 按关卡参数动态生成。
/// </summary>
public static class CampaignLevels
{
    public static List<CampaignLevelDef> GetDefaultCampaign()
    {
        return new List<CampaignLevelDef>
        {
            new()
            {
                Name = "第一章：林间伏击",
                Description = "一群盗贼在林间小道设下埋伏，你必须击退他们。",
                MapTemplate = "forest",
                BattleSize = 0,
                EnemyCount = 3,
                EnemyLevel = 1,
                EnemyType = 0,
                Difficulty = 0,
            },
            new()
            {
                Name = "第二章：亡灵墓穴",
                Description = "古老墓穴中的亡灵苏醒，阻挡了前进的道路。",
                MapTemplate = "cave",
                BattleSize = 0,
                EnemyCount = 4,
                EnemyLevel = 2,
                EnemyType = 1,
                Difficulty = 0,
            },
            new()
            {
                Name = "第三章：荒野猎场",
                Description = "凶猛的野兽占据了必经之路，只有战斗才能通过。",
                MapTemplate = "plains",
                BattleSize = 1,
                EnemyCount = 5,
                EnemyLevel = 3,
                EnemyType = 2,
                Difficulty = 1,
            },
            new()
            {
                Name = "第四章：山贼据点",
                Description = "山贼头目盘踞在山间要塞，必须正面突破。",
                MapTemplate = "mountain",
                BattleSize = 1,
                EnemyCount = 5,
                EnemyLevel = 4,
                EnemyType = 0,
                Difficulty = 1,
            },
            new()
            {
                Name = "第五章：暗影沼泽",
                Description = "沼泽深处潜伏着各种危险生物，小心前行。",
                MapTemplate = "swamp",
                BattleSize = 1,
                EnemyCount = 6,
                EnemyLevel = 5,
                EnemyType = 3,
                Difficulty = 1,
            },
            new()
            {
                Name = "第六章：废弃矿坑",
                Description = "矿坑中的亡灵矿工仍在无尽地劳作，它们不欢迎来客。",
                MapTemplate = "cave",
                BattleSize = 1,
                EnemyCount = 6,
                EnemyLevel = 6,
                EnemyType = 1,
                Difficulty = 1,
            },
            new()
            {
                Name = "第七章：兽人营地",
                Description = "兽人部落的前哨营地，精锐战士严阵以待。",
                MapTemplate = "plains",
                BattleSize = 2,
                EnemyCount = 7,
                EnemyLevel = 7,
                EnemyType = 0,
                Difficulty = 1,
            },
            new()
            {
                Name = "第八章：龙巢外围",
                Description = "接近龙巢的路上，各种被龙威慑服的生物发起攻击。",
                MapTemplate = "mountain",
                BattleSize = 2,
                EnemyCount = 7,
                EnemyLevel = 8,
                EnemyType = 3,
                Difficulty = 2,
            },
            new()
            {
                Name = "第九章：黑暗祭坛",
                Description = "邪教徒在此举行黑暗仪式，必须在仪式完成前阻止他们。",
                MapTemplate = "cave",
                BattleSize = 2,
                EnemyCount = 8,
                EnemyLevel = 9,
                EnemyType = 3,
                Difficulty = 2,
            },
            new()
            {
                Name = "终章：暗影领主",
                Description = "最终决战！暗影领主率领精锐部队等待着你的挑战。",
                MapTemplate = "castle_defense",
                BattleSize = 3,
                EnemyCount = 8,
                EnemyLevel = 10,
                EnemyType = 3,
                Difficulty = 2,
                IsBoss = true,
            },
        };
    }
}
