// SaveManager.cs
// ⚠️ DEPRECATED — 已被 SaveManagerV2.cs (JSON 格式) 替代
// 保留此文件仅用于读取 V1 旧存档（.dat 二进制格式）
// 新存档全部使用 SaveManagerV2 的 JSON 格式
// 处理游戏的序列化与持久化存储（V1 legacy）
using Godot;
using System;
using BladeHex.Strategic;

namespace BladeHex.Data;

/// <summary>
/// 存档管理器 — Autoload 单例
/// </summary>
[GlobalClass]
public partial class SaveManager : Node
{
    private const string SavePath = "user://sword_and_hex_save.dat";

    // ========================================
    // 存档检查
    // ========================================

    /// <summary>检查是否存在有效存档</summary>
    public bool HasSave() => FileAccess.FileExists(SavePath);

    // ========================================
    // 保存
    // ========================================

    /// <summary>
    /// 执行保存逻辑
    /// context 结构:
    ///   "economy"        → EconomyManager node
    ///   "player_pos"     → Vector2 (position)
    ///   "player_unit"    → UnitData
    ///   "fog_of_war"     → FogOfWar serialized data (optional)
    ///   "player_race_id" → int (optional)
    /// </summary>
    public bool SaveGame(Godot.Collections.Dictionary context)
    {
        var econ = (EconomyManager)context["economy"];
        var partyPos = (Vector2)context["player_pos"];
        var unit = (UnitData)context["player_unit"];

        var data = new Godot.Collections.Dictionary
        {
            { "version", "0.3.0" },
            { "timestamp", Time.GetDatetimeDictFromSystem() },
        };

        // 经济数据
        var econData = new Godot.Collections.Dictionary
        {
            { "gold", econ.Gold },
            { "food", econ.Food },
            { "days", econ.DaysPassed },
            { "month", econ.Month },
            { "year", econ.Year },
            { "current_hour", econ.CurrentHour },
        };
        data["economy"] = econData;

        // 世界数据
        data["world"] = new Godot.Collections.Dictionary
        {
            { "player_pos_x", partyPos.X },
            { "player_pos_y", partyPos.Y },
        };

        // 角色完整数据
        var charData = new Godot.Collections.Dictionary
        {
            { "name", unit.UnitName },
            { "str", unit.Str },
            { "dex", unit.Dex },
            { "con", unit.Con },
            { "intel", unit.Intel },
            { "wis", unit.Wis },
            { "cha", unit.Cha },
            { "base_hp", unit.BaseMaxHp },
            { "current_hp", context.ContainsKey("current_hp") ? context["current_hp"] : unit.BaseMaxHp },
            { "xp", unit.Xp },
            { "level", unit.Level },
            { "morale", unit.Morale },
            { "current_mana", unit.CurrentMana },
            { "race_id", context.ContainsKey("player_race_id") ? context["player_race_id"] : 0 },
        };

        // 装备
        charData["primary_weapon"] = SerializeItem(unit.PrimaryMainHand);
        charData["secondary_weapon"] = SerializeItem(unit.SecondaryMainHand);
        charData["armor"] = SerializeItem(unit.Armor);
        charData["shield"] = SerializeItem(unit.Shield);
        charData["helmet"] = SerializeItem(unit.Helmet);
        charData["accessory_1"] = SerializeItem(unit.Accessory1);
        charData["accessory_2"] = SerializeItem(unit.Accessory2);
        charData["mount"] = SerializeItem(unit.Mount);

        // 已学法术
        var spells = new Godot.Collections.Array();
        if (unit.KnownSpells != null)
            foreach (var spell in unit.KnownSpells)
                spells.Add(SerializeSpell(spell));
        charData["known_spells"] = spells;

        // 法术冷却
        charData["spell_cooldowns"] = unit.SpellCooldowns;

        // 武器精通
        var masteryData = new Godot.Collections.Dictionary();
        foreach (WeaponData.WeaponSubtype subtype in Enum.GetValues(typeof(WeaponData.WeaponSubtype)))
        {
            int level = unit.WeaponMastery.GetLevelBySubtype(subtype);
            int xp = unit.WeaponMastery.GetXpBySubtype(subtype);
            if (level > 0 || xp > 0)
                masteryData[subtype.ToString()] = new Godot.Collections.Dictionary { { "level", level }, { "xp", xp } };
        }
        charData["weapon_mastery"] = masteryData;

        // 消耗品背包
        var consumables = new Godot.Collections.Array();
        if (unit.Consumables != null)
            foreach (var c in unit.Consumables)
                consumables.Add(SerializeItem(c));
        charData["consumables"] = consumables;

        data["character"] = charData;

        // 战争迷雾
        if (context.ContainsKey("fog_of_war"))
            data["fog_of_war"] = context["fog_of_war"];

        // 背包物品（完整序列化）
        var invItems = new Godot.Collections.Array();
        foreach (var item in econ.PlayerInventory)
            invItems.Add(SerializeItem(item));
        data["inventory"] = invItems;

        // 大地图 POI 与实体序列化
        if (context.ContainsKey("overworld_entity_manager"))
        {
            var oem = context["overworld_entity_manager"].As<OverworldEntityManager>();
            if (oem != null)
            {
                var poisData = new Godot.Collections.Array();
                foreach (var poi in oem.Pois)
                    poisData.Add(poi.Serialize());
                data["overworld_pois"] = poisData;

                var entitiesData = new Godot.Collections.Array();
                foreach (var entity in oem.Entities)
                    entitiesData.Add(entity.Serialize());
                data["overworld_entities"] = entitiesData;
            }
        }

        // 写入文件
        var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (file != null)
        {
            file.StoreVar(data);
            file.Close();
            GD.Print("游戏已成功保存到: ", ProjectSettings.GlobalizePath(SavePath));
            return true;
        }
        return false;
    }

    // ========================================
    // 读取
    // ========================================

    /// <summary>执行读取逻辑</summary>
    public Godot.Collections.Dictionary LoadGameData()
    {
        if (!HasSave()) return new();

        var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        if (file != null)
        {
            var data = (Godot.Collections.Dictionary)file.GetVar();
            file.Close();
            return data;
        }
        return new();
    }

    // ========================================
    // 删除
    // ========================================

    /// <summary>删除存档</summary>
    public void DeleteSave()
    {
        if (HasSave())
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SavePath));
    }

    // ========================================
    // 序列化辅助
    // ========================================

    private static Godot.Collections.Dictionary SerializeItem(Resource? item)
    {
        if (item == null) return new Godot.Collections.Dictionary { { "is_null", true } };
        var dict = new Godot.Collections.Dictionary
        {
            { "is_null", false },
            { "item_name", (item as ItemData)?.ItemName ?? (item as MountData)?.MountName ?? "" },
            { "item_id", (item as ItemData)?.ItemId ?? (item as MountData)?.MountId ?? "" },
        };
        if (item is WeaponData w)
        {
            dict["item_type"] = "weapon";
            dict["damage_dice_count"] = w.DamageDiceCount;
            dict["damage_dice_sides"] = w.DamageDiceSides;
            dict["is_ranged"] = w.IsRanged;
            dict["range_cells"] = w.RangeCells;
            dict["ap_cost"] = w.ApCost;
        }
        else if (item is ArmorData a)
        {
            dict["item_type"] = "armor";
            dict["armor_type"] = (int)a.armorType;
            dict["ac_bonus"] = a.AcBonus;
            dict["dr_threshold"] = a.DrThreshold;
            dict["max_armor_points"] = a.MaxArmorPoints;
            dict["current_armor_points"] = a.CurrentArmorPoints;
        }
        else if (item is MountData m)
        {
            dict["item_type"] = "mount";
            dict["mount_id"] = m.MountId;
        }
        else if (item is ConsumableData c)
        {
            dict["item_type"] = "consumable";
            dict["consumable_type"] = (int)c.consumableType;
        }
        return dict;
    }

    private static Godot.Collections.Dictionary SerializeSpell(SpellData spell)
    {
        return new Godot.Collections.Dictionary
        {
            { "spell_id", spell.SpellId },
            { "spell_name", spell.SpellName },
            { "school", (int)spell.spellSchool },
            { "tier", (int)spell.tier },
            { "mana_cost", spell.ManaCost },
        };
    }
}
