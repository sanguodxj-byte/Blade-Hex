// QuestGenerator.cs
// 动态委托生成器 — 根据世界状态为城镇任务板生成委托
//
// 设计：每个城镇维护一个 QuestPool（最多 5 个），按天刷新
// 类型：讨伐（杀附近敌方实体）/ 护送（到另一城镇）/ 探索（到废墟/巢穴）
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Strategic.Economy;

namespace BladeHex.Strategic;

public class QuestGenerator
{
    /// <summary>每个城镇最大可接委托数</summary>
    public int MaxQuestsPerPoi = 5;

    /// <summary>刷新间隔（天）</summary>
    public int RefreshIntervalDays = 3;

    private readonly Dictionary<string, QuestPoolEntry> _pools = new();
    private int _worldSeed;
    private List<OverworldPOI> _pois = new();

    public void Initialize(List<OverworldPOI> pois, int worldSeed)
    {
        _pois = pois;
        _worldSeed = worldSeed;
    }

    /// <summary>获取指定城镇的可接委托列表</summary>
    public List<QuestData> GetAvailableQuests(string poiId, int currentDay)
    {
        if (!_pools.TryGetValue(poiId, out var entry))
        {
            entry = new QuestPoolEntry { PoiId = poiId, LastRefreshDay = 0 };
            _pools[poiId] = entry;
        }

        if (currentDay - entry.LastRefreshDay >= RefreshIntervalDays)
        {
            RefreshPool(entry, currentDay);
        }

        return entry.Quests;
    }

    /// <summary>接取委托（从池中移除）</summary>
    public QuestData? AcceptQuest(string poiId, int questIndex, int currentDay)
    {
        var quests = GetAvailableQuests(poiId, currentDay);
        if (questIndex < 0 || questIndex >= quests.Count) return null;

        var quest = quests[questIndex];
        quest.Accept(currentDay);
        quests.RemoveAt(questIndex);
        return quest;
    }

    private void RefreshPool(QuestPoolEntry entry, int currentDay)
    {
        entry.LastRefreshDay = currentDay;
        entry.Quests.Clear();

        var rng = new Random(_worldSeed ^ entry.PoiId.GetHashCode() ^ (currentDay / RefreshIntervalDays));
        var issuerPoi = FindPoi(entry.PoiId);
        if (issuerPoi == null) return;

        int count = 3 + rng.Next(3); // 3-5 个
        count = Math.Min(count, MaxQuestsPerPoi);

        for (int i = 0; i < count; i++)
        {
            var quest = GenerateQuest(issuerPoi, rng, currentDay, i);
            if (quest != null)
                entry.Quests.Add(quest);
        }
    }

    private QuestData? GenerateQuest(OverworldPOI issuer, Random rng, int currentDay, int index)
    {
        // 按权重选类型
        var type = PickQuestType(rng);
        return type switch
        {
            QuestData.QuestType.Extermination => GenerateFromTemplate(QuestTemplateLoader.GetExterminationTemplates(), issuer, rng, currentDay, index),
            QuestData.QuestType.Escort => GenerateEscortFromTemplate(issuer, rng, currentDay, index),
            QuestData.QuestType.Exploration => GenerateFromTemplate(QuestTemplateLoader.GetExplorationTemplates(), issuer, rng, currentDay, index),
            QuestData.QuestType.Collection => GenerateFromTemplate(QuestTemplateLoader.GetCollectionTemplates(), issuer, rng, currentDay, index),
            QuestData.QuestType.Bounty => GenerateFromTemplate(QuestTemplateLoader.GetBountyTemplates(), issuer, rng, currentDay, index),
            _ => GenerateFromTemplate(QuestTemplateLoader.GetExterminationTemplates(), issuer, rng, currentDay, index),
        };
    }

    private static QuestData.QuestType PickQuestType(Random rng)
    {
        int roll = rng.Next(100);
        if (roll < 35) return QuestData.QuestType.Extermination;
        if (roll < 55) return QuestData.QuestType.Escort;
        if (roll < 70) return QuestData.QuestType.Exploration;
        if (roll < 85) return QuestData.QuestType.Collection;
        return QuestData.QuestType.Bounty;
    }

    /// <summary>从JSON模板池中随机选一个生成任务</summary>
    private QuestData? GenerateFromTemplate(List<QuestTemplate> templates, OverworldPOI issuer, Random rng, int currentDay, int index)
    {
        if (templates.Count == 0) return GenerateKillQuest(issuer, rng, currentDay, index);

        var template = templates[rng.Next(templates.Count)];

        // 目标位置：issuer 附近 500-1500 像素
        float angle = (float)(rng.NextDouble() * Math.PI * 2);
        float dist = 500f + (float)rng.NextDouble() * 1000f;
        var targetPos = issuer.Position + new Godot.Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);

        return QuestTemplateLoader.CreateQuestFromTemplate(
            template, issuer.PoiName, issuer.Position, targetPos, "", currentDay, index, rng);
    }

    /// <summary>护送类：从JSON模板+随机目的地生成</summary>
    private QuestData? GenerateEscortFromTemplate(OverworldPOI issuer, Random rng, int currentDay, int index)
    {
        var templates = QuestTemplateLoader.GetEscortTemplates();
        if (templates.Count == 0) return GenerateEscortQuest(issuer, rng, currentDay, index);

        // 找另一个城镇作为目的地
        var candidates = new List<OverworldPOI>();
        foreach (var p in _pois)
        {
            if (p == issuer) continue;
            if (p.PoiTypeEnum != OverworldPOI.POIType.Town && p.PoiTypeEnum != OverworldPOI.POIType.Village) continue;
            candidates.Add(p);
        }
        if (candidates.Count == 0) return null;

        var dest = candidates[rng.Next(candidates.Count)];
        var template = templates[rng.Next(templates.Count)];

        return QuestTemplateLoader.CreateQuestFromTemplate(
            template, issuer.PoiName, issuer.Position, dest.Position, dest.PoiName, currentDay, index, rng);
    }

    private QuestData GenerateKillQuest(OverworldPOI issuer, Random rng, int currentDay, int index)
    {
        string[] targets = { "哥布林营地", "山贼据点", "狼群巢穴", "亡灵墓穴", "蜥蜴人窝点" };
        string target = targets[rng.Next(targets.Length)];
        int difficulty = 1 + rng.Next(3);
        int targetCount = 4 + difficulty * 2;

        // 目标位置：issuer 附近 500-1500 像素
        float angle = (float)(rng.NextDouble() * Math.PI * 2);
        float dist = 500f + (float)rng.NextDouble() * 1000f;
        var targetPos = issuer.Position + new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);

        return new QuestData
        {
            QuestId = $"kill_{issuer.PoiName}_{currentDay}_{index}",
            QuestName = $"清除{target}",
            Description = $"{issuer.PoiName}附近出现了{target}，威胁到了居民安全。消灭至少 {targetCount} 个敌人。",
            questType = QuestData.QuestType.Extermination,
            difficulty = (QuestData.QuestDifficulty)Math.Min(difficulty, 3),
            IssuerName = issuer.PoiName,
            IssuerLocation = new Vector2I((int)issuer.Position.X, (int)issuer.Position.Y),
            TargetWorldPosition = targetPos,
            TargetDescription = target,
            TargetCount = targetCount,
            RewardGold = RewardPricingService.GetQuestReward(QuestData.QuestType.Extermination, difficulty, targetCount, 1.2f),
            RewardReputation = 5 + difficulty * 2,
            RewardFaction = issuer.OwningFaction,
            HasTimeLimit = true,
            TimeLimitDays = 7 + difficulty * 3,
        };
    }

    private QuestData? GenerateEscortQuest(OverworldPOI issuer, Random rng, int currentDay, int index)
    {
        // 找另一个城镇作为目的地
        var candidates = new List<OverworldPOI>();
        foreach (var p in _pois)
        {
            if (p == issuer) continue;
            if (p.PoiTypeEnum != OverworldPOI.POIType.Town && p.PoiTypeEnum != OverworldPOI.POIType.Village) continue;
            candidates.Add(p);
        }
        if (candidates.Count == 0) return null;

        var dest = candidates[rng.Next(candidates.Count)];
        return new QuestData
        {
            QuestId = $"escort_{issuer.PoiName}_{currentDay}_{index}",
            QuestName = $"护送商队至{dest.PoiName}",
            Description = $"一支商队需要从{issuer.PoiName}前往{dest.PoiName}，路上可能遭遇劫匪。",
            questType = QuestData.QuestType.Escort,
            difficulty = QuestData.QuestDifficulty.Medium,
            IssuerName = issuer.PoiName,
            IssuerLocation = new Vector2I((int)issuer.Position.X, (int)issuer.Position.Y),
            TargetWorldPosition = dest.Position,
            TargetDescription = dest.PoiName,
            TargetCount = 1,
            RewardGold = RewardPricingService.GetQuestReward(QuestData.QuestType.Escort, 1, 1, issuer.Position.DistanceTo(dest.Position) / 1000.0f),
            RewardReputation = 8,
            RewardFaction = issuer.OwningFaction,
            HasTimeLimit = true,
            TimeLimitDays = 14,
        };
    }

    private QuestData GenerateExploreQuest(OverworldPOI issuer, Random rng, int currentDay, int index)
    {
        string[] sites = { "远古废墟", "被遗忘的矿洞", "神秘石碑", "古代祭坛", "失落的宝库" };
        string site = sites[rng.Next(sites.Length)];

        float angle = (float)(rng.NextDouble() * Math.PI * 2);
        float dist = 800f + (float)rng.NextDouble() * 1200f;
        var targetPos = issuer.Position + new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);

        return new QuestData
        {
            QuestId = $"explore_{issuer.PoiName}_{currentDay}_{index}",
            QuestName = $"探索{site}",
            Description = $"有传言说{issuer.PoiName}附近有一处{site}，前去调查并带回发现。",
            questType = QuestData.QuestType.Exploration,
            difficulty = QuestData.QuestDifficulty.Medium,
            IssuerName = issuer.PoiName,
            IssuerLocation = new Vector2I((int)issuer.Position.X, (int)issuer.Position.Y),
            TargetWorldPosition = targetPos,
            TargetDescription = site,
            TargetCount = 1,
            RewardGold = RewardPricingService.GetQuestReward(QuestData.QuestType.Exploration, 1, 1, issuer.Position.DistanceTo(targetPos) / 1000.0f),
            RewardReputation = 10,
            RewardFaction = issuer.OwningFaction,
            HasTimeLimit = false,
        };
    }

    private OverworldPOI? FindPoi(string poiId)
    {
        foreach (var p in _pois)
            if (p.PoiName == poiId) return p;
        return null;
    }

    private class QuestPoolEntry
    {
        public string PoiId = "";
        public int LastRefreshDay;
        public List<QuestData> Quests = new();
    }
}
