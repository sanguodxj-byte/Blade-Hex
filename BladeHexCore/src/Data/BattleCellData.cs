// BattleCellData.cs
// 战斗地图格子扩展数据 — 包含地形类型枚举和完整地形属性
// 对应策划案 03-战术战斗系统 的地形表
using Godot;

namespace BladeHex.Data;

[GlobalClass]
public partial class BattleCellData : Resource
{
    // ========================================
    // 地形类型枚举
    // ========================================

    public enum TerrainType
    {
        Plains,         // 平地：默认地形
        Grassland,      // 草地
        Savanna,        // 稀树草原
        Forest,         // 森林
        DenseForest,    // 密林
        Hills,          // 丘陵
        Mountain,       // 山地
        ShallowWater,   // 浅水
        DeepWater,      // 深水
        Swamp,          // 沼泽
        Road,           // 道路
        Sand,           // 沙地
        Snow,           // 雪地
        Wall,           // 墙壁（不可通行，阻挡视线）
        Ruins,          // 建筑废墟
        PoisonMushroom, // 毒菇群
        LuckyGrass,     // 幸运草丛
        // === 据点建筑类型 ===
        Rampart,        // 城墙顶部（可通行，高度2，全掩体，1AP登墙）
        Tower,          // 塔楼（可通行，高度3，全掩体+视野加成，1AP登塔）
        Gate,           // 城门（可通行，高度1，可被攻城破坏）
        Staircase,      // 楼梯（可通行，高度1，连接城墙内外，有朝向）
    }

    // ========================================
    // 数据字段
    // ========================================

    [Export] public TerrainType terrainType = TerrainType.Plains;
    [Export] public string terrainName { get; set; } = "平地";
    [Export] public int moveCost { get; set; } = 1;
    [Export] public int acBonus { get; set; } = 0;
    [Export] public int coverLevel { get; set; } = 0;        // 0=无掩体, 1=半掩体, 2=全掩体
    [Export] public bool blocksLineOfSight { get; set; } = false;
    [Export] public int elevation { get; set; } = 1;          // 0=低地, 1=平地, 2=高地
    [Export] public bool isPassable { get; set; } = true;
    [Export] public string passCondition { get; set; } = "";  // "" = 无条件通行, "requires_swim" = 需要游泳能力, etc.
    [Export] public bool isRiver { get; set; } = false;
    [Export] public string specialEffect { get; set; } = "";  // 特殊效果标识
    [Export] public Color terrainColor = Colors.White;
    [Export] public int facingDirection { get; set; } = -1;   // 朝向（0-5 六方向，-1=无朝向）。楼梯用：指向城墙内侧

    // ========================================
    // 地形属性工厂
    // ========================================

    /// <summary>
    /// 根据 TerrainType 返回完整属性，供生成器使用
    /// 对应策划案 03-战术战斗系统 → 二、地形系统
    /// </summary>
    public static TerrainProperties GetTerrainProperties(TerrainType type)
    {
        return type switch
        {
            TerrainType.Plains => new TerrainProperties("平地", 1, 0, 0, false, true, "", new Color(0.85f, 0.82f, 0.65f)),
            TerrainType.Grassland => new TerrainProperties("草地", 1, 0, 0, false, true, "", new Color(0.45f, 0.72f, 0.35f)),
            TerrainType.Savanna => new TerrainProperties("稀树草原", 1, 1, 0, false, true, "", new Color(0.65f, 0.78f, 0.40f)),
            TerrainType.Forest => new TerrainProperties("森林", 2, 2, 1, true, true, "stealth_bonus", new Color(0.25f, 0.55f, 0.20f)),
            TerrainType.DenseForest => new TerrainProperties("密林", 3, 3, 2, true, true, "stealth_major_bonus", new Color(0.15f, 0.40f, 0.12f)),
            TerrainType.Hills => new TerrainProperties("丘陵", 2, 2, 1, false, true, "high_ground_advantage", new Color(0.72f, 0.68f, 0.52f)),
            TerrainType.Mountain => new TerrainProperties("山地", 3, 3, 2, true, true, "no_mount;vision_plus_2", new Color(0.55f, 0.50f, 0.45f)),
            TerrainType.ShallowWater => new TerrainProperties("浅水", 2, -1, 0, false, true, "fire_resist_2;ice_lightning_weakness", new Color(0.40f, 0.60f, 0.85f)),
            TerrainType.DeepWater => new TerrainProperties("深水", 3, -2, 0, false, false, "casting_disadvantage", new Color(0.20f, 0.35f, 0.70f), "requires_swim"),
            TerrainType.Swamp => new TerrainProperties("沼泽", 2, -1, 0, false, true, "fortitude_dc12_poison", new Color(0.45f, 0.55f, 0.30f)),
            TerrainType.Road => new TerrainProperties("道路", 1, 0, 0, false, true, "move_cost_half", new Color(0.75f, 0.65f, 0.45f)),
            TerrainType.Sand => new TerrainProperties("沙地", 2, 0, 0, false, true, "no_charge", new Color(0.90f, 0.82f, 0.55f)),
            TerrainType.Snow => new TerrainProperties("雪地", 2, 0, 0, false, true, "move_minus_1", new Color(0.92f, 0.95f, 0.98f)),
            TerrainType.Wall => new TerrainProperties("墙壁", 99, 0, 2, true, false, "siege_destroyable", new Color(0.40f, 0.40f, 0.42f)),
            TerrainType.Ruins => new TerrainProperties("建筑废墟", 2, 2, 1, true, true, "destroyable_to_plains", new Color(0.58f, 0.54f, 0.48f)),
            TerrainType.PoisonMushroom => new TerrainProperties("毒菇群", 1, 0, 0, false, true, "poison_2_turns", new Color(0.55f, 0.30f, 0.60f)),
            TerrainType.LuckyGrass => new TerrainProperties("幸运草丛", 1, 0, 0, false, true, "crit_rate_plus_10_one_attack", new Color(0.30f, 0.80f, 0.45f)),
            TerrainType.Rampart => new TerrainProperties("城墙", 1, 3, 2, false, true, "climb_1ap;height_2;high_ground_advantage", new Color(0.50f, 0.48f, 0.44f)),
            TerrainType.Tower => new TerrainProperties("塔楼", 1, 4, 2, false, true, "climb_1ap;height_3;vision_plus_2;high_ground_advantage", new Color(0.45f, 0.42f, 0.40f)),
            TerrainType.Gate => new TerrainProperties("城门", 1, 1, 0, false, true, "siege_destroyable;gate_passable", new Color(0.60f, 0.50f, 0.35f)),
            TerrainType.Staircase => new TerrainProperties("楼梯", 1, 0, 0, false, true, "connects_elevation;free_climb", new Color(0.55f, 0.52f, 0.46f)),
            _ => new TerrainProperties("平地", 1, 0, 0, false, true, "", new Color(0.85f, 0.82f, 0.65f)),
        };
    }

    /// <summary>
    /// 从 TerrainType 快速创建一个已填充属性的 BattleCellData
    /// </summary>
    public static BattleCellData CreateFromType(TerrainType type, int elev = 1)
    {
        var data = new BattleCellData();
        var props = GetTerrainProperties(type);
        data.terrainType = type;
        data.terrainName = props.TerrainName;
        data.moveCost = props.MoveCost;
        data.acBonus = props.AcBonus;
        data.coverLevel = props.CoverLevel;
        data.blocksLineOfSight = props.BlocksLos;
        data.isPassable = props.IsPassable;
        data.passCondition = props.PassCondition;
        data.specialEffect = props.SpecialEffect;
        data.terrainColor = props.Color;
        data.elevation = elev;
        return data;
    }
}

/// <summary>
/// 地形属性数据结构
/// </summary>
    public class TerrainProperties
    {
        public string TerrainName;
        public int MoveCost;
        public int AcBonus;
        public int CoverLevel;
        public bool BlocksLos;
        public bool IsPassable;
        public string PassCondition;   // "" = unconditional, "requires_swim" = needs swimming ability
        public string SpecialEffect;
        public Color Color;

        public TerrainProperties(string name, int move, int ac, int cover, bool blocksLos,
            bool passable, string effect, Color color, string passCondition = "")
        {
            TerrainName = name;
            MoveCost = move;
            AcBonus = ac;
            CoverLevel = cover;
            BlocksLos = blocksLos;
            IsPassable = passable;
            PassCondition = passCondition;
            SpecialEffect = effect;
            Color = color;
        }
    }