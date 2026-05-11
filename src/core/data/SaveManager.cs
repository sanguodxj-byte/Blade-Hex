// SaveManager.cs
// 处理游戏的序列化与持久化存储
// 迁移自 GDScript SaveManager.gd
using Godot;

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
    ///   "player_party"   → Node2D (position)
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
            { "version", "0.2.1" },
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

        // 角色数据
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
            { "xp", unit.Xp },
            { "level", unit.Level },
            { "race_id", context.ContainsKey("player_race_id") ? context["player_race_id"] : 0 },
        };
        data["character"] = charData;

        // 战争迷雾
        if (context.ContainsKey("fog_of_war"))
            data["fog_of_war"] = context["fog_of_war"];

        // 背包物品
        var invItems = new Godot.Collections.Array();
        foreach (var item in econ.PlayerInventory)
        {
            invItems.Add(new Godot.Collections.Dictionary
            {
                { "name", item.ItemName },
                { "type", item is WeaponData ? "weapon" : "armor" },
            });
        }
        data["inventory"] = invItems;

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
}
