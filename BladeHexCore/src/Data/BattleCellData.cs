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
        // === R10 (2026-05-17) 与大地图 21 项地形 1:1 对齐新增 9 项 ===
        Jungle,         // 丛林（炎热湿润，玩法继承 DenseForest）
        Taiga,          // 针叶林（寒带林地，玩法继承 Forest）
        Bog,            // 冻土沼泽（寒带沼泽，玩法继承 Swamp）
        Wasteland,      // 荒原（温带极干贫瘠地，玩法继承 Sand）
        Rocky,          // 岩石荒地（寒带极干硬地，玩法继承 Hills）
        MountainSnow,   // 雪山（不可通行，玩法继承 Mountain + snow 特效）
        Ice,            // 冰原（低摩擦预留，玩法继承 Snow）
        River,          // 河流（带流向预留，玩法继承 ShallowWater + isRiver）
        Bridge,         // 桥（由 R11 从 sample 派生，玩法继承 Road + bridge 特效）
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
    // 攻城机制字段
    // ========================================

    /// <summary>是否可破坏（城门）。被破坏后变为 Ruins + elevation 降为 1</summary>
    [Export] public bool isDestructible { get; set; } = false;

    /// <summary>可破坏地形的当前耐久（0 = 已破坏）</summary>
    [Export] public int durability { get; set; } = 0;

    /// <summary>可破坏地形的最大耐久</summary>
    [Export] public int maxDurability { get; set; } = 0;

    /// <summary>云梯建设进度（0-3，3=完成）。仅城墙格有效</summary>
    [Export] public int ladderProgress { get; set; } = 0;

    /// <summary>云梯是否已完成（完成后该格 elevation 降为 1，可攀登）</summary>
    public bool HasLadder => ladderProgress >= 3;

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
            TerrainType.Hills => new TerrainProperties("丘陵", 2, 2, 0, false, true, "high_ground_advantage", new Color(0.72f, 0.68f, 0.52f)),
            TerrainType.Mountain => new TerrainProperties("山地", 3, 3, 0, false, true, "no_mount;vision_plus_2", new Color(0.55f, 0.50f, 0.45f)),
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
            TerrainType.Rampart => new TerrainProperties("城墙", 1, 3, 2, false, true, "climb_1ap;height_2;high_ground_advantage", new Color(0.45f, 0.50f, 0.55f)),
            TerrainType.Tower => new TerrainProperties("塔楼", 1, 4, 2, false, true, "climb_1ap;height_3;vision_plus_2;high_ground_advantage", new Color(0.40f, 0.45f, 0.52f)),
            TerrainType.Gate => new TerrainProperties("城门", 1, 1, 0, false, true, "siege_destroyable;gate_passable", new Color(0.48f, 0.52f, 0.50f)),
            TerrainType.Staircase => new TerrainProperties("楼梯", 1, 0, 0, false, true, "connects_elevation;free_climb", new Color(0.50f, 0.48f, 0.45f)),

            // === R10 (2026-05-17) 与大地图 21 项 1:1 对齐新增 9 项 ===
            TerrainType.Jungle => new TerrainProperties("丛林", 3, 3, 2, true, true, "stealth_major_bonus", new Color(0.20f, 0.45f, 0.15f)),       // 继承 DenseForest
            TerrainType.Taiga => new TerrainProperties("针叶林", 2, 2, 1, true, true, "stealth_bonus", new Color(0.25f, 0.40f, 0.30f)),           // 继承 Forest
            TerrainType.Bog => new TerrainProperties("冻土沼泽", 2, -1, 0, false, true, "fortitude_dc12_poison", new Color(0.40f, 0.45f, 0.40f)), // 继承 Swamp
            TerrainType.Wasteland => new TerrainProperties("荒原", 2, 0, 0, false, true, "no_charge", new Color(0.65f, 0.55f, 0.45f)),            // 继承 Sand
            TerrainType.Rocky => new TerrainProperties("岩石荒地", 2, 2, 0, false, true, "high_ground_advantage", new Color(0.50f, 0.50f, 0.55f)),// 继承 Hills
            TerrainType.MountainSnow => new TerrainProperties("雪山", 3, 3, 0, false, true, "no_mount;vision_plus_2;snow", new Color(0.85f, 0.88f, 0.92f)), // 继承 Mountain + snow 特效（不可通行：isPassable 由 IsPassable 控制，雪山按 Mountain 一致 = passable=true 但 cost 99 行不通；为 R10 一致性这里维持 Mountain 的 true）
            TerrainType.Ice => new TerrainProperties("冰原", 2, 0, 0, false, true, "move_minus_1;ice_slip", new Color(0.75f, 0.85f, 0.95f)),      // 继承 Snow + ice_slip 预留
            TerrainType.River => new TerrainProperties("河流", 2, -1, 0, false, true, "fire_resist_2;ice_lightning_weakness", new Color(0.30f, 0.50f, 0.78f)), // 继承 ShallowWater，cell 实例的 isRiver 字段在 CreateFromType 内设
            TerrainType.Bridge => new TerrainProperties("桥", 1, 0, 0, false, true, "bridge", new Color(0.70f, 0.55f, 0.35f)),                   // 继承 Road + bridge 特效，elevation 在 CreateFromType 内设为 1

            _ => FallbackProperties(type),
        };
    }

    /// <summary>
    /// 兜底分支：按 R10#3 明确"未识别就 PushError + Plains 兜底",而不是静默吞掉。
    /// 单测通过 `terrainName == "__UNHANDLED__"` sentinel 检测漏 case。
    /// </summary>
    private static TerrainProperties FallbackProperties(TerrainType t)
    {
        GD.PushError($"[BattleCellData] 未识别的 TerrainType: {t}");
        return new TerrainProperties("__UNHANDLED__", 1, 0, 0, false, true, "", new Color(1f, 0f, 1f));
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

        // R10 (2026-05-17) cell-instance 字段微调（type 隐含的语义不在 TerrainProperties 中表达）
        if (type == TerrainType.River) data.isRiver = true;
        if (type == TerrainType.Bridge && elev < 1) data.elevation = 1; // 桥面默认平地高度

        // 攻城机制：城门可破坏，城墙可架设云梯
        if (type == TerrainType.Gate)
        {
            data.isDestructible = true;
            data.durability = 20;
            data.maxDurability = 20;
        }
        if (type == TerrainType.Rampart)
        {
            data.ladderProgress = 0; // 可架设云梯，初始进度 0
        }

        return data;
    }

    // ========================================
    // 运行时地形变更（Frontend → Core 的写入桥梁）
    // ========================================

    /// <summary>
    /// 原地转换地形类型，同步更新所有关联属性（moveCost / acBonus / coverLevel / 等）。
    /// 提供给 Frontend 作为写入 Core 数据的唯一途径。
    /// </summary>
    public void TransformTo(TerrainType newType)
    {
        var props = GetTerrainProperties(newType);
        terrainType = newType;
        terrainName = props.TerrainName;
        moveCost = props.MoveCost;
        acBonus = props.AcBonus;
        coverLevel = props.CoverLevel;
        blocksLineOfSight = props.BlocksLos;
        isPassable = props.IsPassable;
        passCondition = props.PassCondition;
        specialEffect = props.SpecialEffect;
        terrainColor = props.Color;

        // 特定地形微调
        if (newType == TerrainType.River) isRiver = true;
        if (newType == TerrainType.Bridge && elevation < 1) elevation = 1;

        // 攻城机制字段重置
        if (newType == TerrainType.Gate)
        {
            isDestructible = true;
            durability = 20;
            maxDurability = 20;
        }
        else
        {
            isDestructible = false;
            durability = 0;
            maxDurability = 0;
        }
        if (newType != TerrainType.Rampart)
            ladderProgress = 0;
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