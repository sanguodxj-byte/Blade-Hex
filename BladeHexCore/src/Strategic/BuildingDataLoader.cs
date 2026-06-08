// BuildingDataLoader.cs
// 从 buildings.json 加载封地建筑配置数据
using Godot;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 建筑配置数据（从JSON加载）
/// </summary>
public class BuildingConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsEdge { get; set; }
    public int Cost { get; set; }
    public int Days { get; set; }
    public int BaseHp { get; set; }
    public int HpPerLevel { get; set; }
    public int Attack { get; set; }
    public int AttackPerLevel { get; set; }
    public int Range { get; set; }
    public int RangePerLevel { get; set; }
    public int FoodBonus { get; set; }
    public int GoldBonus { get; set; }
    public int ProsperityBonus { get; set; }
    public int GarrisonBonus { get; set; }
}

/// <summary>
/// 建筑数据加载器 — 从 res://BladeHexCore/src/Strategic/buildings.json 加载
/// 失败时回退到硬编码数据
/// </summary>
public static class BuildingDataLoader
{
    private static bool _loaded = false;
    private static readonly Dictionary<FiefBuilding.BuildingType, BuildingConfig> _configs = new();

    public static BuildingConfig? GetConfig(FiefBuilding.BuildingType type)
    {
        EnsureLoaded();
        return _configs.TryGetValue(type, out var cfg) ? cfg : null;
    }

    public static Dictionary<FiefBuilding.BuildingType, BuildingConfig> GetAllConfigs()
    {
        EnsureLoaded();
        return _configs;
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        LoadFromJson();
    }

    private static void LoadFromJson()
    {
        string path = "res://BladeHexCore/src/Strategic/buildings.json";
        if (!FileAccess.FileExists(path)) { LoadFallback(); return; }
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) { LoadFallback(); return; }
        var json = new Json();
        if (json.Parse(file.GetAsText()) != Error.Ok)
        {
            GD.PrintErr($"[BuildingDataLoader] JSON parse error: {json.GetErrorMessage()}");
            LoadFallback();
            return;
        }
        var data = json.Data.AsGodotDictionary();
        ParseBuildings(data);
        GD.Print($"[BuildingDataLoader] Loaded {_configs.Count} building configs from JSON");
    }

    private static void ParseBuildings(Godot.Collections.Dictionary root)
    {
        if (!root.ContainsKey("buildings")) return;
        var arr = root["buildings"].AsGodotArray();
        foreach (var item in arr)
        {
            var dict = item.AsGodotDictionary();
            string id = dict["id"].AsString();

            var buildingType = IdToBuildingType(id);
            if (buildingType == null)
            {
                GD.PrintErr($"[BuildingDataLoader] Unknown building id: {id}");
                continue;
            }

            var cfg = new BuildingConfig
            {
                Id = id,
                Name = dict.ContainsKey("name") ? dict["name"].AsString() : id,
                Description = dict.ContainsKey("desc") ? dict["desc"].AsString() : "",
                IsEdge = dict.ContainsKey("is_edge") && dict["is_edge"].AsBool(),
                Cost = dict.ContainsKey("cost") ? dict["cost"].AsInt32() : 100,
                Days = dict.ContainsKey("days") ? dict["days"].AsInt32() : 3,
                BaseHp = dict.ContainsKey("base_hp") ? dict["base_hp"].AsInt32() : 50,
                HpPerLevel = dict.ContainsKey("hp_per_level") ? dict["hp_per_level"].AsInt32() : 0,
                Attack = dict.ContainsKey("attack") ? dict["attack"].AsInt32() : 0,
                AttackPerLevel = dict.ContainsKey("attack_per_level") ? dict["attack_per_level"].AsInt32() : 0,
                Range = dict.ContainsKey("range") ? dict["range"].AsInt32() : 0,
                RangePerLevel = dict.ContainsKey("range_per_level") ? dict["range_per_level"].AsInt32() : 0,
                FoodBonus = dict.ContainsKey("food_bonus") ? dict["food_bonus"].AsInt32() : 0,
                GoldBonus = dict.ContainsKey("gold_bonus") ? dict["gold_bonus"].AsInt32() : 0,
                ProsperityBonus = dict.ContainsKey("prosperity_bonus") ? dict["prosperity_bonus"].AsInt32() : 0,
                GarrisonBonus = dict.ContainsKey("garrison_bonus") ? dict["garrison_bonus"].AsInt32() : 0,
            };
            _configs[buildingType.Value] = cfg;
        }
    }

    private static FiefBuilding.BuildingType? IdToBuildingType(string id) => id switch
    {
        "wood_fence" => FiefBuilding.BuildingType.WoodFence,
        "stone_wall" => FiefBuilding.BuildingType.StoneWall,
        "fortification" => FiefBuilding.BuildingType.Fortification,
        "gate" => FiefBuilding.BuildingType.Gate,
        "barricade" => FiefBuilding.BuildingType.Barricade,
        "arrow_tower" => FiefBuilding.BuildingType.ArrowTower,
        "watch_tower" => FiefBuilding.BuildingType.WatchTower,
        "magic_tower" => FiefBuilding.BuildingType.MagicTower,
        "trap_pit" => FiefBuilding.BuildingType.TrapPit,
        "farmland" => FiefBuilding.BuildingType.Farmland,
        "market" => FiefBuilding.BuildingType.Market,
        "barracks" => FiefBuilding.BuildingType.Barracks,
        "smithy" => FiefBuilding.BuildingType.Smithy,
        "lord_manor" => FiefBuilding.BuildingType.LordManor,
        "blacksmith_workshop" => FiefBuilding.BuildingType.BlacksmithWorkshop,
        "brew_workshop" => FiefBuilding.BuildingType.BrewWorkshop,
        "textile_workshop" => FiefBuilding.BuildingType.TextileWorkshop,
        "tannery_workshop" => FiefBuilding.BuildingType.TanneryWorkshop,
        _ => null,
    };

    // ========================================
    // 回退：硬编码数据
    // ========================================
    private static void LoadFallback()
    {
        GD.PrintErr("[BuildingDataLoader] Failed to load buildings.json, using fallback data");
        AddFallback(FiefBuilding.BuildingType.WoodFence, "木栅栏", "简易的木制栅栏，可阻挡敌人移动。", true, 50, 1, 20, 10, 0, 0, 0, 0);
        AddFallback(FiefBuilding.BuildingType.StoneWall, "石墙", "坚固的石墙，提供半掩体防护。", true, 200, 3, 60, 20, 0, 0, 0, 0);
        AddFallback(FiefBuilding.BuildingType.Fortification, "城墙", "厚重的城墙，提供全掩体防护。", true, 500, 7, 100, 30, 0, 0, 0, 0);
        AddFallback(FiefBuilding.BuildingType.Gate, "城门", "可开关的城门，允许友军通过。", true, 100, 2, 40, 15, 0, 0, 0, 0);
        AddFallback(FiefBuilding.BuildingType.Barricade, "拒马", "尖锐的拒马，使骑兵冲锋无效化。", true, 60, 1, 30, 0, 0, 0, 0, 0);
        AddFallback(FiefBuilding.BuildingType.ArrowTower, "箭塔", "每回合自动射击远程目标。", false, 300, 5, 40, 10, 4, 2, 5, 1);
        AddFallback(FiefBuilding.BuildingType.WatchTower, "瞭望塔", "提供先攻+4，防止敌人突袭。", false, 150, 2, 25, 0, 0, 0, 0, 0);
        AddFallback(FiefBuilding.BuildingType.MagicTower, "魔法塔", "每回合释放范围法术，AOE 2格。", false, 800, 10, 30, 10, 3, 2, 4, 0);
        AddFallback(FiefBuilding.BuildingType.TrapPit, "陷阱坑", "首个踏入的敌人受2d6伤害并倒地。", false, 80, 1, 1, 0, 0, 0, 0, 0);
        AddFallback(FiefBuilding.BuildingType.Farmland, "农田", "每日产出5单位食物。", false, 100, 3, 50, 0, 0, 0, 0, 0);
        AddFallback(FiefBuilding.BuildingType.Market, "市集", "每日产出10金币，繁荣度+5。", false, 500, 7, 50, 0, 0, 0, 0, 0);
        AddFallback(FiefBuilding.BuildingType.Barracks, "兵营", "驻军上限+8，驻军每日恢复HP。", false, 400, 7, 60, 0, 0, 0, 0, 0);
        AddFallback(FiefBuilding.BuildingType.Smithy, "铁匠坊", "驻军装备品质+1。", false, 350, 5, 50, 0, 0, 0, 0, 0);
        AddFallback(FiefBuilding.BuildingType.LordManor, "领主宅邸", "封地核心，不可拆除。", false, 0, 0, 100, 0, 0, 0, 0, 0);
        AddFallback(FiefBuilding.BuildingType.BlacksmithWorkshop, "武器作坊", "生产武器装备的作坊。", false, 8000, 4, 150, 20, 0, 0, 0, 0);
        AddFallback(FiefBuilding.BuildingType.BrewWorkshop, "酿酒坊", "生产啤酒等口粮的作坊。", false, 5000, 3, 100, 15, 0, 0, 0, 0);
        AddFallback(FiefBuilding.BuildingType.TextileWorkshop, "织布作坊", "生产精美布料的作坊。", false, 6000, 3, 110, 15, 0, 0, 0, 0);
        AddFallback(FiefBuilding.BuildingType.TanneryWorkshop, "制革坊", "生产皮甲皮料的作坊。", false, 7000, 4, 130, 18, 0, 0, 0, 0);
    }

    private static void AddFallback(FiefBuilding.BuildingType type, string name, string desc,
        bool isEdge, int cost, int days, int baseHp, int hpPerLevel,
        int attack, int attackPerLevel, int range, int rangePerLevel)
    {
        _configs[type] = new BuildingConfig
        {
            Id = type.ToString(),
            Name = name,
            Description = desc,
            IsEdge = isEdge,
            Cost = cost,
            Days = days,
            BaseHp = baseHp,
            HpPerLevel = hpPerLevel,
            Attack = attack,
            AttackPerLevel = attackPerLevel,
            Range = range,
            RangePerLevel = rangePerLevel,
        };
    }
}
