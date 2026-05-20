// SaveSystem.cs — 分层 JSON 存档数据模型
//
// 历史：原名 SaveSystemV2.cs，覆盖了已废弃 V1 二进制存档的全部字段。
// 在架构优化 spec R9 期间正式接管 SaveSystem 名字。
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BladeHex.Data;

public class GameSaveData
{
    public string Version { get; set; } = "2.0.0";
    public long Timestamp { get; set; }
    public WorldSaveData World { get; set; } = new();
    public PartySaveData Party { get; set; } = new();
    public EconomySaveData Economy { get; set; } = new();
    public QuestSaveData Quests { get; set; } = new();
    public List<InventoryItemSaveData> Inventory { get; set; } = new();
}

public class WorldSaveData
{
    public float PlayerPosX { get; set; }
    public float PlayerPosY { get; set; }
    public int Seed { get; set; }
    public int WorldSize { get; set; } = 1;
    public string? SaveId { get; set; }
    public List<EntitySaveData> Entities { get; set; } = new();
    public List<PoiSaveData> Pois { get; set; } = new();
}

public class EconomySaveData
{
    public int Gold { get; set; }
    public float Food { get; set; }
    public int DaysPassed { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public int CurrentHour { get; set; }

    // 生存系统持久化字段（防止读档时生存压力归零）
    /// <summary>连续欠饷天数</summary>
    public int ConsecutiveUnpaidDays { get; set; }
    /// <summary>连续断粮天数</summary>
    public int ConsecutiveStarveDays { get; set; }
    /// <summary>修整工具存量</summary>
    public float Tools { get; set; } = 20.0f;
    /// <summary>医疗物资存量</summary>
    public float Medicine { get; set; } = 20.0f;
}

public class PartySaveData
{
    public List<UnitSaveData> Units { get; set; } = new();
    public int PlayerRaceId { get; set; }
}

public class UnitSaveData
{
    public string UnitName { get; set; } = "";
    public int Level { get; set; }
    public int CurrentHp { get; set; }
    public int Xp { get; set; }
    public int Str { get; set; }
    public int Dex { get; set; }
    public int Con { get; set; }
    public int Intel { get; set; }
    public int Wis { get; set; }
    public int Cha { get; set; }
    public int BaseMaxHp { get; set; }
    public int Morale { get; set; }
    public int CurrentMana { get; set; }

    // 装备 ID
    public string? PrimaryMainHandId { get; set; }
    public string? SecondaryMainHandId { get; set; }
    public string? ArmorId { get; set; }
    public string? ShieldId { get; set; }
    public string? HelmetId { get; set; }
    public string? Accessory1Id { get; set; }
    public string? Accessory2Id { get; set; }
    public string? MountId { get; set; }

    // 法术
    public List<SpellSaveData> KnownSpells { get; set; } = new();
    public Dictionary<string, int> SpellCooldowns { get; set; } = new();

    // 武器精通
    public Dictionary<string, MasterySaveData> WeaponMastery { get; set; } = new();

    // 消耗品
    public List<string> ConsumableIds { get; set; } = new();

    // 是否为队长（玩家本人）
    public bool IsLeader { get; set; }

    // 技能盘强类型数据
    public SkillTreeSaveData SkillTree { get; set; } = new();
}

public class SpellSaveData
{
    public string SpellId { get; set; } = "";
    public string SpellName { get; set; } = "";
    public int School { get; set; }
    public int Tier { get; set; }
    public int ManaCost { get; set; }
}

public class MasterySaveData
{
    public int Level { get; set; }
    public int Xp { get; set; }
}

public class InventoryItemSaveData
{
    public string ItemId { get; set; } = "";
    public string ItemName { get; set; } = "";
    public string ItemType { get; set; } = "";
    public int Quantity { get; set; } = 1;
}

public class QuestSaveData
{
    public List<string> ActiveQuestIds { get; set; } = new();
    public List<string> CompletedQuestIds { get; set; } = new();
    public Dictionary<string, int> QuestProgress { get; set; } = new();
}

public class EntitySaveData
{
    public string EntityName { get; set; } = "";
    public string EntityType { get; set; } = "";
    public float PosX { get; set; }
    public float PosY { get; set; }
    public string Faction { get; set; } = "";
    public bool IsAlive { get; set; } = true;
}

public class PoiSaveData
{
    public string PoiName { get; set; } = "";
    public string PoiType { get; set; } = "";
    public float PosX { get; set; }
    public float PosY { get; set; }
    public int Prosperity { get; set; }
    public int GarrisonSize { get; set; }
}

public class SkillTreeSaveData
{
    public List<string> ActivatedNodes { get; set; } = new();
    public int AvailableSkillPoints { get; set; }
    public int TotalJumps { get; set; }
    public int UsedJumps { get; set; }
    public int CharacterLevel { get; set; } = 1;
    public Dictionary<string, int> CareerSkillUses { get; set; } = new();
}

public static class SaveDataConverter
{
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
            foreach (WeaponData.WeaponSubtype subtype in System.Enum.GetValues(typeof(WeaponData.WeaponSubtype)))
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

        // 技能盘强类型数据提取 (从运行时 unit.SkillTreeData 字典)
        if (unit.SkillTreeData != null)
        {
            var st = unit.SkillTreeData;
            var skillSave = new SkillTreeSaveData();

            if (st.ContainsKey("activated_nodes"))
            {
                var arr = st["activated_nodes"].AsGodotArray();
                foreach (var item in arr)
                    skillSave.ActivatedNodes.Add(item.AsString());
            }
            skillSave.AvailableSkillPoints = st.ContainsKey("available_skill_points") ? st["available_skill_points"].AsInt32() : 0;
            skillSave.TotalJumps = st.ContainsKey("total_jumps") ? st["total_jumps"].AsInt32() : 0;
            skillSave.UsedJumps = st.ContainsKey("used_jumps") ? st["used_jumps"].AsInt32() : 0;
            skillSave.CharacterLevel = st.ContainsKey("character_level") ? st["character_level"].AsInt32() : 1;

            if (st.ContainsKey("career_skill_uses"))
            {
                var uses = st["career_skill_uses"].AsGodotDictionary();
                foreach (var key in uses.Keys)
                    skillSave.CareerSkillUses[key.ToString()!] = uses[key].AsInt32();
            }

            data.SkillTree = skillSave;
        }

        return data;
    }

    public static void RestoreUnitSkillTree(UnitData unit, UnitSaveData data)
    {
        if (data.SkillTree == null) return;

        var st = new Godot.Collections.Dictionary();
        st["activated_nodes"] = new Godot.Collections.Array<string>(data.SkillTree.ActivatedNodes.ToArray());
        st["available_skill_points"] = data.SkillTree.AvailableSkillPoints;
        st["total_jumps"] = data.SkillTree.TotalJumps;
        st["used_jumps"] = data.SkillTree.UsedJumps;
        st["character_level"] = data.SkillTree.CharacterLevel;

        var careerUses = new Godot.Collections.Dictionary();
        if (data.SkillTree.CareerSkillUses != null)
        {
            foreach (var kvp in data.SkillTree.CareerSkillUses)
                careerUses[kvp.Key] = kvp.Value;
        }
        st["career_skill_uses"] = careerUses;

        unit.SkillTreeData = st;
    }

    public static void RestoreUnitData(UnitData unit, UnitSaveData data)
    {
        unit.UnitName = data.UnitName;
        unit.Level = data.Level;
        unit.Xp = data.Xp;
        unit.Str = data.Str;
        unit.Dex = data.Dex;
        unit.Con = data.Con;
        unit.Intel = data.Intel;
        unit.Wis = data.Wis;
        unit.Cha = data.Cha;
        unit.BaseMaxHp = data.BaseMaxHp;
        unit.Morale = data.Morale;
        unit.CurrentMana = data.CurrentMana;

        // 1. 还原装备 ID
        if (data.PrimaryMainHandId != null) unit.PrimaryMainHand = new WeaponData { ItemId = data.PrimaryMainHandId };
        else unit.PrimaryMainHand = null;

        if (data.SecondaryMainHandId != null) unit.SecondaryMainHand = new WeaponData { ItemId = data.SecondaryMainHandId };
        else unit.SecondaryMainHand = null;

        if (data.ArmorId != null) unit.Armor = new ArmorData { ItemId = data.ArmorId };
        else unit.Armor = null;

        if (data.ShieldId != null) unit.Shield = new ArmorData { ItemId = data.ShieldId };
        else unit.Shield = null;

        if (data.HelmetId != null) unit.Helmet = new ArmorData { ItemId = data.HelmetId };
        else unit.Helmet = null;

        if (data.Accessory1Id != null) unit.Accessory1 = new AccessoryData { ItemId = data.Accessory1Id };
        else unit.Accessory1 = null;

        if (data.Accessory2Id != null) unit.Accessory2 = new AccessoryData { ItemId = data.Accessory2Id };
        else unit.Accessory2 = null;

        // 2. 还原已知法术 (KnownSpells)
        unit.KnownSpells.Clear();
        if (data.KnownSpells != null)
        {
            foreach (var sp in data.KnownSpells)
            {
                unit.KnownSpells.Add(new SpellData
                {
                    SpellId = sp.SpellId,
                    SpellName = sp.SpellName,
                    spellSchool = (SpellData.SpellSchool)sp.School,
                    tier = (SpellData.SpellTier)sp.Tier,
                    ManaCost = sp.ManaCost
                });
            }
        }

        // 3. 还原法术冷却 (SpellCooldowns)
        unit.SpellCooldowns.Clear();
        if (data.SpellCooldowns != null)
        {
            foreach (var kvp in data.SpellCooldowns)
                unit.SpellCooldowns[kvp.Key] = kvp.Value;
        }

        // 4. 还原武器精通 (WeaponMastery)
        if (data.WeaponMastery != null)
        {
            foreach (var kvp in data.WeaponMastery)
            {
                if (System.Enum.TryParse<WeaponData.WeaponSubtype>(kvp.Key, out var subtype))
                {
                    // 直接设置 XP，等级由 XP 自动推算（LevelFromXp）
                    unit.WeaponMastery.SetXpBySubtype(subtype, kvp.Value.Xp);
                }
            }
        }

        // 5. 还原消耗品
        unit.Consumables.Clear();
        if (data.ConsumableIds != null)
        {
            foreach (var id in data.ConsumableIds)
                unit.Consumables.Add(new ConsumableData { ItemId = id });
        }

        // 6. 还原技能树
        RestoreUnitSkillTree(unit, data);
    }
}
