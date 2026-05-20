// QuestTemplateLoader.cs
// 从JSON加载任务模板，供QuestGenerator使用
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Strategic.Economy;

namespace BladeHex.Strategic;

/// <summary>
/// 单个任务模板 — 从JSON反序列化
/// </summary>
public class QuestTemplate
{
    public string IdPrefix { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string TargetDesc { get; set; } = "";
    public string EnemyTemplate { get; set; } = "";
    public int[] DifficultyRange { get; set; } = { 0, 2 };
    public int[] BaseCount { get; set; } = { 4, 8 };
    public int[] BaseReward { get; set; } = { 30, 60 };
    public int Reputation { get; set; } = 5;
    public int TimeLimitDays { get; set; } = 7;
    public string AmbushNarrative { get; set; } = "";
    public string TerrainOverride { get; set; } = "";
    public float AmbushChance { get; set; } = 1.0f;
    public QuestData.QuestType QuestType { get; set; } = QuestData.QuestType.Extermination;
}

/// <summary>
/// 任务模板加载器 — 从 res://BladeHexCore/src/Quest/quest_templates.json 加载
/// </summary>
public static class QuestTemplateLoader
{
    private static List<QuestTemplate>? _exterminationTemplates;
    private static List<QuestTemplate>? _escortTemplates;
    private static List<QuestTemplate>? _explorationTemplates;
    private static List<QuestTemplate>? _collectionTemplates;
    private static List<QuestTemplate>? _bountyTemplates;
    private static bool _loaded = false;

    public static List<QuestTemplate> GetExterminationTemplates()
    {
        EnsureLoaded();
        return _exterminationTemplates!;
    }

    public static List<QuestTemplate> GetEscortTemplates()
    {
        EnsureLoaded();
        return _escortTemplates!;
    }

    public static List<QuestTemplate> GetExplorationTemplates()
    {
        EnsureLoaded();
        return _explorationTemplates!;
    }

    public static List<QuestTemplate> GetCollectionTemplates()
    {
        EnsureLoaded();
        return _collectionTemplates!;
    }

    public static List<QuestTemplate> GetBountyTemplates()
    {
        EnsureLoaded();
        return _bountyTemplates!;
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _exterminationTemplates = new();
        _escortTemplates = new();
        _explorationTemplates = new();
        _collectionTemplates = new();
        _bountyTemplates = new();

        var json = LoadJsonFile("res://BladeHexCore/src/Quest/quest_templates.json");
        if (json == null)
        {
            GD.PrintErr("[QuestTemplateLoader] Failed to load quest_templates.json");
            CreateFallbackTemplates();
            return;
        }

        ParseTemplates(json);
    }

    private static Godot.Collections.Dictionary? LoadJsonFile(string path)
    {
        if (!FileAccess.FileExists(path)) return null;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return null;

        string content = file.GetAsText();
        var jsonParser = new Json();
        var err = jsonParser.Parse(content);
        if (err != Error.Ok)
        {
            GD.PrintErr($"[QuestTemplateLoader] JSON parse error: {jsonParser.GetErrorMessage()}");
            return null;
        }

        return jsonParser.Data.AsGodotDictionary();
    }

    private static void ParseTemplates(Godot.Collections.Dictionary root)
    {
        if (root.ContainsKey("extermination"))
        {
            var arr = root["extermination"].AsGodotArray();
            foreach (var item in arr)
            {
                var dict = item.AsGodotDictionary();
                _exterminationTemplates!.Add(ParseOneTemplate(dict, QuestData.QuestType.Extermination));
            }
        }

        if (root.ContainsKey("escort"))
        {
            var arr = root["escort"].AsGodotArray();
            foreach (var item in arr)
            {
                var dict = item.AsGodotDictionary();
                _escortTemplates!.Add(ParseOneTemplate(dict, QuestData.QuestType.Escort));
            }
        }

        if (root.ContainsKey("exploration"))
        {
            var arr = root["exploration"].AsGodotArray();
            foreach (var item in arr)
            {
                var dict = item.AsGodotDictionary();
                _explorationTemplates!.Add(ParseOneTemplate(dict, QuestData.QuestType.Exploration));
            }
        }

        if (root.ContainsKey("collection"))
        {
            var arr = root["collection"].AsGodotArray();
            foreach (var item in arr)
            {
                var dict = item.AsGodotDictionary();
                _collectionTemplates!.Add(ParseOneTemplate(dict, QuestData.QuestType.Collection));
            }
        }

        if (root.ContainsKey("bounty"))
        {
            var arr = root["bounty"].AsGodotArray();
            foreach (var item in arr)
            {
                var dict = item.AsGodotDictionary();
                _bountyTemplates!.Add(ParseOneTemplate(dict, QuestData.QuestType.Bounty));
            }
        }

        GD.Print($"[QuestTemplateLoader] Loaded {_exterminationTemplates!.Count} extermination, {_escortTemplates!.Count} escort, {_explorationTemplates!.Count} exploration, {_collectionTemplates!.Count} collection, {_bountyTemplates!.Count} bounty templates");
    }

    private static QuestTemplate ParseOneTemplate(Godot.Collections.Dictionary dict, QuestData.QuestType type)
    {
        var t = new QuestTemplate { QuestType = type };

        if (dict.ContainsKey("id_prefix")) t.IdPrefix = dict["id_prefix"].AsString();
        if (dict.ContainsKey("name")) t.Name = dict["name"].AsString();
        if (dict.ContainsKey("description")) t.Description = dict["description"].AsString();
        if (dict.ContainsKey("target_desc")) t.TargetDesc = dict["target_desc"].AsString();
        if (dict.ContainsKey("enemy_template")) t.EnemyTemplate = dict["enemy_template"].AsString();
        if (dict.ContainsKey("ambush_narrative")) t.AmbushNarrative = dict["ambush_narrative"].AsString();
        if (dict.ContainsKey("terrain_override")) t.TerrainOverride = dict["terrain_override"].AsString();
        if (dict.ContainsKey("reputation")) t.Reputation = dict["reputation"].AsInt32();
        if (dict.ContainsKey("time_limit_days")) t.TimeLimitDays = dict["time_limit_days"].AsInt32();
        if (dict.ContainsKey("ambush_chance")) t.AmbushChance = (float)dict["ambush_chance"].AsDouble();

        if (dict.ContainsKey("difficulty_range"))
        {
            var arr = dict["difficulty_range"].AsGodotArray();
            t.DifficultyRange = new int[] { arr[0].AsInt32(), arr[1].AsInt32() };
        }
        if (dict.ContainsKey("base_count"))
        {
            var arr = dict["base_count"].AsGodotArray();
            t.BaseCount = new int[] { arr[0].AsInt32(), arr[1].AsInt32() };
        }
        if (dict.ContainsKey("base_reward"))
        {
            var arr = dict["base_reward"].AsGodotArray();
            t.BaseReward = new int[] { arr[0].AsInt32(), arr[1].AsInt32() };
        }

        return t;
    }

    /// <summary>回退模板（JSON加载失败时使用）</summary>
    private static void CreateFallbackTemplates()
    {
        _exterminationTemplates!.Add(new QuestTemplate
        {
            IdPrefix = "kill_generic",
            Name = "清除威胁",
            Description = "{issuer}附近出现了危险的敌人，前去消灭它们。",
            TargetDesc = "敌人营地",
            EnemyTemplate = "bandit_fighter",
            BaseCount = new[] { 4, 8 },
            BaseReward = new[] { 30, 60 },
            QuestType = QuestData.QuestType.Extermination,
        });
    }

    /// <summary>
    /// 从模板生成QuestData实例
    /// </summary>
    public static QuestData CreateQuestFromTemplate(QuestTemplate template, string issuerName,
        Vector2 issuerPos, Vector2 targetPos, string destination, int currentDay, int index, Random rng)
    {
        int difficulty = template.DifficultyRange[0] + rng.Next(template.DifficultyRange[1] - template.DifficultyRange[0] + 1);
        difficulty = Math.Clamp(difficulty, 0, 3);
        int count = template.BaseCount[0] + rng.Next(template.BaseCount[1] - template.BaseCount[0] + 1);

        // 方向文本
        string targetDir = GetDirectionText(issuerPos, targetPos);

        // 替换模板变量
        string name = template.Name
            .Replace("{issuer}", issuerName)
            .Replace("{destination}", destination)
            .Replace("{target_dir}", targetDir);

        string desc = template.Description
            .Replace("{issuer}", issuerName)
            .Replace("{destination}", destination)
            .Replace("{target_dir}", targetDir);

        string targetDesc = template.TargetDesc
            .Replace("{destination}", destination);

        return new QuestData
        {
            QuestId = $"{template.IdPrefix}_{issuerName}_{currentDay}_{index}",
            QuestName = name,
            Description = desc,
            questType = template.QuestType,
            difficulty = (QuestData.QuestDifficulty)difficulty,
            IssuerName = issuerName,
            IssuerLocation = new Vector2I((int)issuerPos.X, (int)issuerPos.Y),
            TargetWorldPosition = targetPos,
            TargetDescription = targetDesc,
            TargetCount = count,
            RewardGold = RewardPricingService.GetQuestReward(template.QuestType, difficulty, count, DistanceFactor(issuerPos, targetPos)),
            RewardReputation = template.Reputation,
            HasTimeLimit = template.TimeLimitDays > 0,
            TimeLimitDays = template.TimeLimitDays,
        };
    }

    private static float DistanceFactor(Vector2 from, Vector2 to)
    {
        float distance = from.DistanceTo(to);
        return Math.Clamp(distance / 1000.0f, 0.75f, 1.6f);
    }

    private static string GetDirectionText(Vector2 from, Vector2 to)
    {
        var dir = (to - from).Normalized();
        if (dir.Y < -0.5f) return dir.X > 0.3f ? "东北" : dir.X < -0.3f ? "西北" : "北";
        if (dir.Y > 0.5f) return dir.X > 0.3f ? "东南" : dir.X < -0.3f ? "西南" : "南";
        return dir.X > 0 ? "东" : "西";
    }
}
