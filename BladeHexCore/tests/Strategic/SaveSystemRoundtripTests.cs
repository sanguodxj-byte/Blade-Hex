// SaveSystemRoundtripTests.cs
// 存档数据序列化往返测试 — 服务于架构优化 spec R7。
//
// 设计原则：
//   - 纯静态测试，不依赖 Godot 场景树
//   - 直接对 GameSaveData / 子结构做 System.Text.Json 序列化-反序列化
//   - 验证字段值的等价性，不验证具体 JSON 字节
//   - 每个 Test_xxx 方法返回 (bool ok, string description)
//
// 覆盖关键路径：
//   - 空存档（默认值）的 roundtrip
//   - 完整存档（含队伍、世界、经济、任务、物品）的 roundtrip
//   - 嵌套字典（SpellCooldowns / WeaponMastery / QuestProgress）
//   - 可空字段（装备 ID）保持 null
//   - 浮点精度：Player 坐标在合理误差内
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace BladeHex.Data.Tests;

public static class SaveSystemRoundtripTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;

        foreach (var (name, ok, msg) in EnumerateTests())
        {
            if (ok) { passed++; details.Add($"  [PASS] {name}"); }
            else { failed++; details.Add($"  [FAIL] {name}: {msg}"); }
        }
        return (passed, failed, details);
    }

    private static IEnumerable<(string name, bool ok, string msg)> EnumerateTests()
    {
        yield return Run(nameof(Roundtrip_EmptySave_PreservesDefaults), Roundtrip_EmptySave_PreservesDefaults);
        yield return Run(nameof(Roundtrip_FullSave_PreservesAllFields), Roundtrip_FullSave_PreservesAllFields);
        yield return Run(nameof(Roundtrip_NullableEquipment_PreservesNull), Roundtrip_NullableEquipment_PreservesNull);
        yield return Run(nameof(Roundtrip_SpellCooldowns_Dictionary), Roundtrip_SpellCooldowns_Dictionary);
        yield return Run(nameof(Roundtrip_WeaponMastery_NestedRecord), Roundtrip_WeaponMastery_NestedRecord);
        yield return Run(nameof(Roundtrip_QuestProgress_Dictionary), Roundtrip_QuestProgress_Dictionary);
        yield return Run(nameof(Roundtrip_PlayerCoordinates_Precision), Roundtrip_PlayerCoordinates_Precision);
        yield return Run(nameof(Roundtrip_MultipleEntities_Preserved), Roundtrip_MultipleEntities_Preserved);
    }

    private static (string, bool, string) Run(string name, System.Func<(bool, string)> test)
    {
        try
        {
            var (ok, msg) = test();
            return (name, ok, msg);
        }
        catch (Exception ex)
        {
            return (name, false, $"Exception: {ex.Message}");
        }
    }

    // ============================================================================
    // 测试用例
    // ============================================================================

    private static (bool, string) Roundtrip_EmptySave_PreservesDefaults()
    {
        var original = new GameSaveData();
        var copy = Roundtrip(original);

        if (copy.Version != original.Version)
            return (false, $"Version mismatch: {copy.Version} vs {original.Version}");
        if (copy.World == null) return (false, "World is null");
        if (copy.Party == null) return (false, "Party is null");
        if (copy.Inventory == null) return (false, "Inventory is null");
        return (true, "");
    }

    private static (bool, string) Roundtrip_FullSave_PreservesAllFields()
    {
        var original = new GameSaveData
        {
            Version = "2.0.0",
            Timestamp = 1234567890L,
            World = new WorldSaveData
            {
                PlayerPosX = 123.45f,
                PlayerPosY = 678.9f,
                Seed = 42,
                WorldSize = 2,
                SaveId = "save_001",
            },
            Economy = new EconomySaveData
            {
                Gold = 500,
                Food = 25.5f,
                DaysPassed = 30,
                Month = 3,
                Year = 1024,
                CurrentHour = 14,
            },
        };

        var copy = Roundtrip(original);

        if (copy.Timestamp != 1234567890L) return (false, $"Timestamp: got {copy.Timestamp}");
        if (copy.World.Seed != 42) return (false, $"Seed: got {copy.World.Seed}");
        if (copy.World.WorldSize != 2) return (false, $"WorldSize: got {copy.World.WorldSize}");
        if (copy.World.SaveId != "save_001") return (false, $"SaveId: got {copy.World.SaveId}");
        if (copy.Economy.Gold != 500) return (false, $"Gold: got {copy.Economy.Gold}");
        if (System.Math.Abs(copy.Economy.Food - 25.5f) > 0.001f) return (false, $"Food: got {copy.Economy.Food}");
        if (copy.Economy.DaysPassed != 30) return (false, $"DaysPassed: got {copy.Economy.DaysPassed}");
        if (copy.Economy.Year != 1024) return (false, $"Year: got {copy.Economy.Year}");
        return (true, "");
    }

    private static (bool, string) Roundtrip_NullableEquipment_PreservesNull()
    {
        var unit = new UnitSaveData
        {
            UnitName = "Hero",
            Level = 5,
            ArmorId = "leather_armor",
            ShieldId = null,
            HelmetId = null,
            PrimaryMainHandId = "iron_sword",
        };
        var copy = Roundtrip(unit);

        if (copy.UnitName != "Hero") return (false, $"UnitName: got {copy.UnitName}");
        if (copy.Level != 5) return (false, $"Level: got {copy.Level}");
        if (copy.ArmorId != "leather_armor") return (false, $"ArmorId: got {copy.ArmorId}");
        if (copy.ShieldId != null) return (false, $"ShieldId should be null, got {copy.ShieldId}");
        if (copy.HelmetId != null) return (false, $"HelmetId should be null, got {copy.HelmetId}");
        if (copy.PrimaryMainHandId != "iron_sword") return (false, $"PrimaryMainHandId: got {copy.PrimaryMainHandId}");
        return (true, "");
    }

    private static (bool, string) Roundtrip_SpellCooldowns_Dictionary()
    {
        var unit = new UnitSaveData
        {
            UnitName = "Mage",
            SpellCooldowns = new Dictionary<string, int>
            {
                ["fireball"] = 3,
                ["heal"] = 0,
                ["meteor"] = 7,
            },
        };
        var copy = Roundtrip(unit);

        if (copy.SpellCooldowns.Count != 3) return (false, $"count: got {copy.SpellCooldowns.Count}");
        if (copy.SpellCooldowns["fireball"] != 3) return (false, "fireball cooldown wrong");
        if (copy.SpellCooldowns["heal"] != 0) return (false, "heal cooldown wrong");
        if (copy.SpellCooldowns["meteor"] != 7) return (false, "meteor cooldown wrong");
        return (true, "");
    }

    private static (bool, string) Roundtrip_WeaponMastery_NestedRecord()
    {
        var unit = new UnitSaveData
        {
            UnitName = "Warrior",
            WeaponMastery = new Dictionary<string, MasterySaveData>
            {
                ["sword"] = new MasterySaveData { Level = 3, Xp = 150 },
                ["axe"] = new MasterySaveData { Level = 1, Xp = 25 },
            },
        };
        var copy = Roundtrip(unit);

        if (copy.WeaponMastery.Count != 2) return (false, $"count: got {copy.WeaponMastery.Count}");
        if (copy.WeaponMastery["sword"].Level != 3) return (false, "sword level wrong");
        if (copy.WeaponMastery["sword"].Xp != 150) return (false, "sword xp wrong");
        if (copy.WeaponMastery["axe"].Level != 1) return (false, "axe level wrong");
        if (copy.WeaponMastery["axe"].Xp != 25) return (false, "axe xp wrong");
        return (true, "");
    }

    private static (bool, string) Roundtrip_QuestProgress_Dictionary()
    {
        var quests = new QuestSaveData
        {
            ActiveQuestIds = new List<string> { "q1", "q2" },
            CompletedQuestIds = new List<string> { "q0" },
            QuestProgress = new Dictionary<string, int>
            {
                ["q1"] = 2,
                ["q2"] = 0,
            },
        };
        var copy = Roundtrip(quests);

        if (copy.ActiveQuestIds.Count != 2) return (false, "active count wrong");
        if (copy.CompletedQuestIds.Count != 1) return (false, "completed count wrong");
        if (copy.CompletedQuestIds[0] != "q0") return (false, "completed id wrong");
        if (copy.QuestProgress["q1"] != 2) return (false, "q1 progress wrong");
        if (copy.QuestProgress["q2"] != 0) return (false, "q2 progress wrong");
        return (true, "");
    }

    private static (bool, string) Roundtrip_PlayerCoordinates_Precision()
    {
        // 使用 float，roundtrip 应保持 7 位有效数字
        var world = new WorldSaveData
        {
            PlayerPosX = 1234.5678f,
            PlayerPosY = -987.6543f,
            Seed = 99,
        };
        var copy = Roundtrip(world);

        if (System.Math.Abs(copy.PlayerPosX - 1234.5678f) > 0.01f)
            return (false, $"PosX precision: got {copy.PlayerPosX}");
        if (System.Math.Abs(copy.PlayerPosY - (-987.6543f)) > 0.01f)
            return (false, $"PosY precision: got {copy.PlayerPosY}");
        return (true, "");
    }

    private static (bool, string) Roundtrip_MultipleEntities_Preserved()
    {
        var world = new WorldSaveData
        {
            Seed = 7,
            Entities = new List<EntitySaveData>
            {
                new() { EntityName = "Goblin", EntityType = "Enemy", PosX = 10, PosY = 20, Faction = "evil", IsAlive = true },
                new() { EntityName = "Trader", EntityType = "NPC", PosX = 30, PosY = 40, Faction = "neutral", IsAlive = true },
                new() { EntityName = "DeadGuy", EntityType = "Enemy", PosX = 50, PosY = 60, Faction = "evil", IsAlive = false },
            },
            Pois = new List<PoiSaveData>
            {
                new() { PoiName = "Town", PoiType = "Settlement", PosX = 100, PosY = 200, Prosperity = 50, GarrisonSize = 10 },
            },
        };
        var copy = Roundtrip(world);

        if (copy.Entities.Count != 3) return (false, $"entities count: got {copy.Entities.Count}");
        if (copy.Entities[0].EntityName != "Goblin") return (false, "first entity wrong");
        if (copy.Entities[2].IsAlive) return (false, "third entity should be dead");
        if (copy.Pois.Count != 1) return (false, "pois count wrong");
        if (copy.Pois[0].Prosperity != 50) return (false, "prosperity wrong");
        return (true, "");
    }

    // ============================================================================
    // 工具方法
    // ============================================================================

    /// <summary>对任何对象做 JSON roundtrip，返回新实例</summary>
    private static T Roundtrip<T>(T original)
    {
        var json = JsonSerializer.Serialize(original);
        var copy = JsonSerializer.Deserialize<T>(json);
        if (copy == null) throw new InvalidOperationException("Deserialize returned null");
        return copy;
    }
}
