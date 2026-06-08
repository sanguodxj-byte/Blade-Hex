// RaceData.cs
// 种族数据资源 — 从 races.json 加载
// 对应策划案 12-种族与招募.md + 05-角色与职业.md
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Data;

[GlobalClass]
public partial class RaceData : Resource
{
    // ========================================
    // 种族枚举
    // ========================================

    public enum Race
    {
        Human,
        Elf,
        Dwarf,
        HalfOrc,
        HalfElf,
    }

    // ========================================
    // 数据字段
    // ========================================

    [Export] public Race raceId = Race.Human;
    [Export] public string RaceName { get; set; } = "人类";

    [Export] public int StrMod;
    [Export] public int DexMod;
    [Export] public int ConMod;
    [Export] public int IntMod;
    [Export] public int WisMod;
    [Export] public int ChaMod;

    [Export] public string[] RacialTraits = [];

    // ========================================
    // 以下字段为设计预留 — 当前游戏机制未消费这些数据。
    // 保留以避免破坏 mod 友好度（races.json 中已存在这些键），
    // 但运行时无任何效果。引入对应系统前不要依赖。
    // ========================================
    [Export] public string TraitsDescription { get; set; } = "";          // TODO: 引入种族详情面板时显示
    [Export] public float RecruitmentDifficulty { get; set; } = 1.0f;     // TODO: 招募系统价格修正
    [Export] public Godot.Collections.Dictionary StartingFavor = new();   // TODO: 派系声望系统初始关系
    [Export] public string[] SuitableTendencies = [];                     // TODO: 角色生成倾向引导

    // ========================================
    // JSON 驱动加载
    // ========================================

    private static RaceData[]? _cached;
    private const string JsonPath = "res://BladeHexCore/src/Data/character/races.json";
    private const string ModPath = "user://mods/races/";

    public static RaceData[] GetAllRaces()
    {
        if (_cached != null) return _cached;
        _cached = LoadFromJson();
        return _cached;
    }

    public static RaceData GetRaceById(Race id)
    {
        foreach (var r in GetAllRaces())
            if (r.raceId == id) return r;
        return GetAllRaces()[0];
    }

    public static string GetRaceName(Race id) => GetRaceById(id).RaceName;

    /// <summary>强制重新加载（热重载用）</summary>
    public static void Reload() { _cached = null; }

    // ========================================
    // JSON 解析
    // ========================================

    private static RaceData[] LoadFromJson()
    {
        var list = new List<RaceData>();

        // 内置种族
        LoadRacesFromFile(JsonPath, list);

        // Mod 种族
        if (DirAccess.DirExistsAbsolute(ModPath))
        {
            using var dir = DirAccess.Open(ModPath);
            if (dir != null)
            {
                dir.ListDirBegin();
                string fileName = dir.GetNext();
                while (!string.IsNullOrEmpty(fileName))
                {
                    if (fileName.EndsWith(".json"))
                        LoadRacesFromFile(ModPath + fileName, list);
                    fileName = dir.GetNext();
                }
                dir.ListDirEnd();
            }
        }

        if (list.Count == 0)
        {
            GD.PushError("[RaceData] No races loaded! Using emergency fallback.");
            list.Add(new RaceData { raceId = Race.Human, RaceName = "人类" });
        }

        GD.Print($"[RaceData] Loaded {list.Count} races");
        return list.ToArray();
    }

    private static void LoadRacesFromFile(string path, List<RaceData> list)
    {
        if (!FileAccess.FileExists(path)) return;

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return;

        var json = new Json();
        if (json.Parse(file.GetAsText()) != Error.Ok)
        {
            GD.PushError($"[RaceData] JSON parse error in {path}: {json.GetErrorMessage()}");
            return;
        }

        if (json.Data.VariantType != Variant.Type.Array) return;
        var arr = json.Data.AsGodotArray();

        for (int i = 0; i < arr.Count; i++)
        {
            if (arr[i].VariantType != Variant.Type.Dictionary) continue;
            var dict = arr[i].AsGodotDictionary();

            try
            {
                var race = ParseRaceEntry(dict);
                list.Add(race);
            }
            catch (Exception ex)
            {
                GD.PushError($"[RaceData] Failed to parse {path}[{i}]: {ex.Message}");
            }
        }
    }

    private static RaceData ParseRaceEntry(Godot.Collections.Dictionary dict)
    {
        string idStr = dict.ContainsKey("id") ? dict["id"].AsString() : "Human";
        if (!Enum.TryParse<Race>(idStr, out var raceEnum))
            raceEnum = Race.Human;

        var data = new RaceData
        {
            raceId = raceEnum,
            RaceName = dict.ContainsKey("name") ? dict["name"].AsString() : idStr,
            StrMod = OptInt(dict, "str_mod", 0),
            DexMod = OptInt(dict, "dex_mod", 0),
            ConMod = OptInt(dict, "con_mod", 0),
            IntMod = OptInt(dict, "int_mod", 0),
            WisMod = OptInt(dict, "wis_mod", 0),
            ChaMod = OptInt(dict, "cha_mod", 0),
            TraitsDescription = dict.ContainsKey("traits_desc") ? dict["traits_desc"].AsString() : "",
            RecruitmentDifficulty = dict.ContainsKey("recruitment_difficulty")
                ? (float)dict["recruitment_difficulty"].AsDouble() : 1.0f,
        };

        // 种族特性数组
        if (dict.ContainsKey("traits") && dict["traits"].VariantType == Variant.Type.Array)
        {
            var traitsArr = dict["traits"].AsGodotArray();
            data.RacialTraits = new string[traitsArr.Count];
            for (int i = 0; i < traitsArr.Count; i++)
                data.RacialTraits[i] = traitsArr[i].AsString();
        }

        // 初始好感度
        if (dict.ContainsKey("starting_favor") && dict["starting_favor"].VariantType == Variant.Type.Dictionary)
        {
            var favorDict = dict["starting_favor"].AsGodotDictionary();
            data.StartingFavor = new Godot.Collections.Dictionary();
            foreach (var key in favorDict.Keys)
                data.StartingFavor[key.AsString()] = favorDict[key].AsInt32();
        }

        // 适合职业
        if (dict.ContainsKey("suitable_tendencies") && dict["suitable_tendencies"].VariantType == Variant.Type.Array)
        {
            var tendArr = dict["suitable_tendencies"].AsGodotArray();
            data.SuitableTendencies = new string[tendArr.Count];
            for (int i = 0; i < tendArr.Count; i++)
                data.SuitableTendencies[i] = tendArr[i].AsString();
        }

        return data;
    }

    private static int OptInt(Godot.Collections.Dictionary dict, string key, int def)
        => dict.ContainsKey(key) ? dict[key].AsInt32() : def;
}
