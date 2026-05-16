// SaveManager.cs — 统一存档管理器（JSON 格式）
//
// 历史：项目初期有 V1 二进制版本（已移除）；本类原名 SaveManagerV2，
// 在架构优化 spec R9 期间正式接管 SaveManager 名字。
// 仍保留 V1 旧存档（.dat StoreVar 格式）的迁移读取能力（LoadLegacySave / ConvertLegacyData），
// 但目前没有 UI 入口调用，需要时由 UI 层显式触发。
using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using BladeHex.Data;
using BladeHex.Strategic;

namespace BladeHex.Data;

/// <summary>
/// 统一存档管理器 — JSON 格式 + 自动备份
/// </summary>
[GlobalClass]
public partial class SaveManager : Node
{
    private const string SavePath = "user://sword_and_hex_save.json";
    private const string BackupPath = "user://sword_and_hex_save_backup.json";
    private const string LegacyPath = "user://sword_and_hex_save.dat";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // ========================================
    // 存档检查
    // ========================================

    /// <summary>检查是否存在有效存档（V2 JSON 或 V1 legacy）</summary>
    public bool HasSave() => FileAccess.FileExists(SavePath) || FileAccess.FileExists(LegacyPath);

    /// <summary>检查是否存在 V1 旧存档（需要迁移）</summary>
    public bool HasLegacySave() => !FileAccess.FileExists(SavePath) && FileAccess.FileExists(LegacyPath);

    // ========================================
    // 保存
    // ========================================

    /// <summary>保存游戏 — 从运行时数据构建存档并写入 JSON</summary>
    public bool SaveGame(GameSaveData saveData)
    {
        saveData.Timestamp = (long)Time.GetUnixTimeFromSystem();
        try
        {
            // 备份旧存档
            if (FileAccess.FileExists(SavePath))
            {
                var src = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
                if (src != null)
                {
                    var content = src.GetAsText();
                    src.Close();
                    var backup = FileAccess.Open(BackupPath, FileAccess.ModeFlags.Write);
                    if (backup != null) { backup.StoreString(content); backup.Close(); }
                }
            }

            // 写入新存档
            var json = JsonSerializer.Serialize(saveData, JsonOpts);
            var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            if (file != null)
            {
                file.StoreString(json);
                file.Close();
                GD.Print($"[SaveV2] 游戏已保存: {ProjectSettings.GlobalizePath(SavePath)}");
                return true;
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[SaveV2] 保存失败: {e.Message}");
        }
        return false;
    }

    // ========================================
    // 读取
    // ========================================

    /// <summary>读取存档 — 优先 V2 JSON，回退到备份</summary>
    public GameSaveData? LoadGame()
    {
        if (FileAccess.FileExists(SavePath))
        {
            var data = LoadFromPath(SavePath);
            if (data != null) return data;
        }
        // 尝试备份
        return LoadFromPath(BackupPath);
    }

    /// <summary>读取 V1 旧存档并转换为 V2 格式</summary>
    public GameSaveData? LoadLegacySave()
    {
        if (!FileAccess.FileExists(LegacyPath)) return null;
        try
        {
            var file = FileAccess.Open(LegacyPath, FileAccess.ModeFlags.Read);
            if (file == null) return null;
            var raw = (Godot.Collections.Dictionary)file.GetVar();
            file.Close();
            return ConvertLegacyData(raw);
        }
        catch (Exception e)
        {
            GD.PrintErr($"[SaveV2] V1 旧存档读取失败: {e.Message}");
            return null;
        }
    }

    private GameSaveData? LoadFromPath(string path)
    {
        if (!FileAccess.FileExists(path)) return null;
        try
        {
            var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null) return null;
            var json = file.GetAsText();
            file.Close();
            return JsonSerializer.Deserialize<GameSaveData>(json, JsonOpts);
        }
        catch (Exception e)
        {
            GD.PrintErr($"[SaveV2] 读取 {path} 失败: {e.Message}");
            return null;
        }
    }

    // ========================================
    // 删除
    // ========================================

    /// <summary>删除所有存档（V2 + 备份 + V1 legacy）</summary>
    public void DeleteSave()
    {
        DeleteFileIfExists(SavePath);
        DeleteFileIfExists(BackupPath);
        DeleteFileIfExists(LegacyPath);
    }

    private static void DeleteFileIfExists(string path)
    {
        if (FileAccess.FileExists(path))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
    }

    // ========================================
    // 数据构建 — 从运行时状态构建 GameSaveData
    // ========================================

    /// <summary>从 OverworldScene3D 运行时状态构建完整存档数据</summary>
    public static GameSaveData BuildSaveData(
        UnitData playerUnit,
        int playerRaceId,
        Vector2 playerPos,
        EconomyManager economy,
        OverworldEntityManager? entityMgr = null,
        int worldSeed = 0,
        int worldSize = 1,
        string? saveId = null)
    {
        var save = new GameSaveData();

        // 世界数据
        save.World.PlayerPosX = playerPos.X;
        save.World.PlayerPosY = playerPos.Y;
        save.World.Seed = worldSeed;
        save.World.WorldSize = worldSize;
        save.World.SaveId = saveId;

        // 经济数据
        save.Economy.Gold = economy.Gold;
        save.Economy.Food = economy.Food;
        save.Economy.DaysPassed = economy.DaysPassed;
        save.Economy.Month = economy.Month;
        save.Economy.Year = economy.Year;
        save.Economy.CurrentHour = (int)economy.CurrentHour;

        // 玩家角色
        save.Party.PlayerRaceId = playerRaceId;
        save.Party.Units.Add(BuildUnitSaveData(playerUnit, isLeader: true));

        // 背包
        foreach (var item in economy.PlayerInventory)
        {
            save.Inventory.Add(new InventoryItemSaveData
            {
                ItemId = item.ItemId,
                ItemName = item.ItemName,
                ItemType = item switch
                {
                    WeaponData => "weapon",
                    ArmorData => "armor",
                    ConsumableData => "consumable",
                    _ => "misc"
                },
            });
        }

        // POI 与实体
        if (entityMgr != null)
        {
            foreach (var poi in entityMgr.Pois)
            {
                save.World.Pois.Add(new PoiSaveData
                {
                    PoiName = poi.PoiName,
                    PoiType = poi.PoiTypeEnum.ToString(),
                    PosX = poi.Position.X,
                    PosY = poi.Position.Y,
                    Prosperity = poi.Prosperity,
                    GarrisonSize = poi.GarrisonCurrent,
                });
            }

            foreach (var entity in entityMgr.Entities)
            {
                save.World.Entities.Add(new EntitySaveData
                {
                    EntityName = entity.EntityName,
                    EntityType = entity.EntityTypeEnum.ToString(),
                    PosX = entity.Position.X,
                    PosY = entity.Position.Y,
                    Faction = entity.Faction,
                    IsAlive = entity.IsAlive,
                });
            }
        }

        return save;
    }

    /// <summary>从 UnitData 构建单位存档数据</summary>
    public static UnitSaveData BuildUnitSaveData(UnitData unit, bool isLeader = false)
    {
        var data = new UnitSaveData
        {
            UnitName = unit.UnitName,
            Level = unit.Level,
            CurrentHp = unit.BaseMaxHp, // 运行时 HP 由调用方覆盖
            Xp = unit.Xp,
            Str = unit.Str,
            Dex = unit.Dex,
            Con = unit.Con,
            Intel = unit.Intel,
            Wis = unit.Wis,
            Cha = unit.Cha,
            BaseMaxHp = unit.BaseMaxHp,
            Morale = unit.Morale,
            CurrentMana = unit.CurrentMana,
            IsLeader = isLeader,

            // 装备
            PrimaryMainHandId = (unit.PrimaryMainHand as WeaponData)?.ItemId,
            SecondaryMainHandId = (unit.SecondaryMainHand as WeaponData)?.ItemId,
            ArmorId = unit.Armor?.ItemId,
            ShieldId = unit.Shield?.ItemId,
            HelmetId = unit.Helmet?.ItemId,
            Accessory1Id = unit.Accessory1?.ItemId,
            Accessory2Id = unit.Accessory2?.ItemId,
            MountId = unit.Mount?.MountId,
        };

        // 法术
        if (unit.KnownSpells != null)
        {
            foreach (var spell in unit.KnownSpells)
            {
                data.KnownSpells.Add(new SpellSaveData
                {
                    SpellId = spell.SpellId,
                    SpellName = spell.SpellName,
                    School = (int)spell.spellSchool,
                    Tier = (int)spell.tier,
                    ManaCost = spell.ManaCost,
                });
            }
        }

        // 法术冷却
        if (unit.SpellCooldowns != null)
        {
            foreach (var key in unit.SpellCooldowns.Keys)
                data.SpellCooldowns[key.AsString()] = unit.SpellCooldowns[key].AsInt32();
        }

        // 武器精通
        if (unit.WeaponMastery != null)
        {
            foreach (WeaponData.WeaponSubtype subtype in Enum.GetValues(typeof(WeaponData.WeaponSubtype)))
            {
                int level = unit.WeaponMastery.GetLevelBySubtype(subtype);
                int xp = unit.WeaponMastery.GetXpBySubtype(subtype);
                if (level > 0 || xp > 0)
                {
                    data.WeaponMastery[subtype.ToString()] = new MasterySaveData { Level = level, Xp = xp };
                }
            }
        }

        // 消耗品
        if (unit.Consumables != null)
        {
            foreach (var c in unit.Consumables)
                if (c is ConsumableData cd) data.ConsumableIds.Add(cd.ItemId);
        }

        return data;
    }

    // ========================================
    // V1 旧存档转换
    // ========================================

    private static GameSaveData ConvertLegacyData(Godot.Collections.Dictionary raw)
    {
        var save = new GameSaveData { Version = "2.0.0-migrated" };

        // 经济
        if (raw.ContainsKey("economy"))
        {
            var econ = raw["economy"].AsGodotDictionary();
            save.Economy.Gold = econ.ContainsKey("gold") ? econ["gold"].AsInt32() : 0;
            save.Economy.Food = econ.ContainsKey("food") ? econ["food"].AsInt32() : 0;
            save.Economy.DaysPassed = econ.ContainsKey("days") ? econ["days"].AsInt32() : 0;
            save.Economy.Month = econ.ContainsKey("month") ? econ["month"].AsInt32() : 1;
            save.Economy.Year = econ.ContainsKey("year") ? econ["year"].AsInt32() : 1;
            save.Economy.CurrentHour = econ.ContainsKey("current_hour") ? econ["current_hour"].AsInt32() : 8;
        }

        // 世界位置
        if (raw.ContainsKey("world"))
        {
            var world = raw["world"].AsGodotDictionary();
            save.World.PlayerPosX = world.ContainsKey("player_pos_x") ? world["player_pos_x"].AsSingle() : 0;
            save.World.PlayerPosY = world.ContainsKey("player_pos_y") ? world["player_pos_y"].AsSingle() : 0;
        }

        // 角色
        if (raw.ContainsKey("character"))
        {
            var ch = raw["character"].AsGodotDictionary();
            var unit = new UnitSaveData
            {
                IsLeader = true,
                UnitName = ch.ContainsKey("name") ? ch["name"].AsString() : "Unknown",
                Level = ch.ContainsKey("level") ? ch["level"].AsInt32() : 1,
                CurrentHp = ch.ContainsKey("current_hp") ? ch["current_hp"].AsInt32() : 10,
                Xp = ch.ContainsKey("xp") ? ch["xp"].AsInt32() : 0,
                Str = ch.ContainsKey("str") ? ch["str"].AsInt32() : 10,
                Dex = ch.ContainsKey("dex") ? ch["dex"].AsInt32() : 10,
                Con = ch.ContainsKey("con") ? ch["con"].AsInt32() : 10,
                Intel = ch.ContainsKey("intel") ? ch["intel"].AsInt32() : 10,
                Wis = ch.ContainsKey("wis") ? ch["wis"].AsInt32() : 10,
                Cha = ch.ContainsKey("cha") ? ch["cha"].AsInt32() : 10,
                BaseMaxHp = ch.ContainsKey("base_hp") ? ch["base_hp"].AsInt32() : 10,
                Morale = ch.ContainsKey("morale") ? ch["morale"].AsInt32() : 50,
                CurrentMana = ch.ContainsKey("current_mana") ? ch["current_mana"].AsInt32() : 0,
            };
            save.Party.PlayerRaceId = ch.ContainsKey("race_id") ? ch["race_id"].AsInt32() : 0;
            save.Party.Units.Add(unit);
        }

        return save;
    }
}
