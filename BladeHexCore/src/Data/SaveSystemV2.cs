// SaveSystemV2.cs — 分层 JSON 存档数据模型
// 覆盖 V1 (SaveManager.cs) 的全部字段 + 扩展
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
