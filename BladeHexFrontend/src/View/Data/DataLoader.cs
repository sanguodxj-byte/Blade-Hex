// DataLoader.cs — 数据外部化框架（修复版：泛型接口替代反射）
using Godot;
using System.Collections.Generic;
using System.Text.Json;

namespace BladeHex.Data;

/// <summary>有 ID 的配置数据接口 — 替代反射</summary>
public interface IConfigData { string Id { get; set; } }

public class SkillConfigData : IConfigData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Passive";
    public int ActionCost { get; set; } = 4;
    public string Description { get; set; } = "";
}

public class EnemyTemplateConfig : IConfigData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string EnemyType { get; set; } = "Humanoid";
    public int Level { get; set; } = 1;
    public int Str { get; set; } = 10;
    public int Dex { get; set; } = 10;
    public int Con { get; set; } = 10;
    public int BaseHp { get; set; } = 10;
    public float ThreatLevel { get; set; }
}

[GlobalClass]
public partial class DataLoader : Node
{
    private const string DataPath = "res://resources/data/";
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private Dictionary<string, SkillConfigData> _skills = new();
    private Dictionary<string, EnemyTemplateConfig> _enemies = new();

    public void LoadAll()
    {
        _skills = LoadJsonFile<SkillConfigData>($"{DataPath}skills.json");
        _enemies = LoadJsonFile<EnemyTemplateConfig>($"{DataPath}enemies.json");
        GD.Print($"[DataLoader] Loaded: {_skills.Count} skills, {_enemies.Count} enemies");
    }

    public SkillConfigData? GetSkill(string id) => _skills.GetValueOrDefault(id);
    public EnemyTemplateConfig? GetEnemy(string id) => _enemies.GetValueOrDefault(id);

    // FIX: 泛型约束 IConfigData，不再用反射
    private Dictionary<string, T> LoadJsonFile<T>(string path) where T : class, IConfigData
    {
        var result = new Dictionary<string, T>();
        if (!FileAccess.FileExists(path)) return result;
        try
        {
            var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null) return result;
            var json = file.GetAsText();
            file.Close();
            var items = JsonSerializer.Deserialize<List<T>>(json, JsonOpts);
            if (items == null) return result;
            foreach (var item in items)
                if (!string.IsNullOrEmpty(item.Id)) result[item.Id] = item;
        }
        catch (System.Exception e) { GD.PrintErr($"[DataLoader] Failed {path}: {e.Message}"); }
        return result;
    }
}
