// OverworldSimulationContext.cs
// 大地图模拟上下文 — 集中保存模拟输入和运行时状态
//
// 设计目标:
//   - 替代当前散落在 OverworldEntityManager 的字段
//   - 不依赖 Godot Node、Control、场景树
//   - 所有非数据输入通过字段注入（不含信号绑定）
//   - 为测试提供单一的依赖点
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Strategic.Army;
using BladeHex.Strategic.Economy;
using BladeHex.Strategic.Hero;
using BladeHex.Strategic.Kingdom;
using BladeHex.Strategic.SubParty;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Strategic;

/// <summary>
/// 大地图模拟上下文 — OverworldSimulation 的运行时状态容器。
/// 
/// 使用规则:
///   - 字段由 OverworldSimulation 或外部代码在 Tick 前设置
///   - 不直接发射 Godot 信号（事件通过 OverworldSimulation.Tick*() 返回）
///   - 引用类型默认非 null（用空列表替代）
/// </summary>
public sealed class OverworldSimulationContext
{
    // ========================================
    // 实体与POI
    // ========================================
    
    /// <summary>所有活跃的大地图实体</summary>
    public List<OverworldEntity> Entities { get; set; } = new();
    
    /// <summary>所有 POI</summary>
    public List<OverworldPOI> Pois { get; set; } = new();
    
    // ========================================
    // 空间索引
    // ========================================
    
    /// <summary>实体空间索引（可选，用于优化空间查询）</summary>
    public EntitySpatialIndex? SpatialIndex { get; set; } = new EntitySpatialIndex(800);
    
    // ========================================
    // 导航系统（二选一）
    // ========================================
    
    /// <summary>Hex 地图网格（全图模式）</summary>
    public HexOverworldGrid? HexGrid { get; set; }
    
    /// <summary>Hex 寻路（全图模式）</summary>
    public HexOverworldAStar? HexAStar { get; set; }
    
    /// <summary>Chunk 管理器（chunk 模式）</summary>
    public ChunkManager? ChunkManager { get; set; }
    
    /// <summary>Chunk 寻路（chunk 模式）</summary>
    public ChunkAStar? ChunkAStar { get; set; }

    /// <summary>活跃大地图地形查询（chunk 模式运行时使用，不读全图缓存）</summary>
    public OverworldTerrainQuery? TerrainQuery { get; set; }
    
    // ========================================
    // 军队系统
    // ========================================
    
    /// <summary>军队注册表</summary>
    public ArmyRegistry Armies { get; set; } = new();
    
    // ========================================
    // 世界事件/外交
    // ========================================
    
    /// <summary>世界事件引擎</summary>
    public WorldEventEngine WorldEngine { get; set; } = new();
    
    /// <summary>经济事件引擎</summary>
    public EconomyEventEngine EconomyEvents { get; set; } = new();
    
    /// <summary>国家列表</summary>
    public List<NationConfig> Nations { get; set; } = new();
    
    /// <summary>NPC 封地容器</summary>
    public List<FiefData> NpcFiefs { get; set; } = new();
    
    // ========================================
    // 英雄网络
    // ========================================
    
    /// <summary>英雄注册表</summary>
    public HeroRegistry Heroes { get; set; } = new();
    
    /// <summary>英雄关系矩阵</summary>
    public HeroRelationMatrix Relations { get; set; } = new();
    
    /// <summary>俘虏账册</summary>
    public PrisonerLedger Prisoners { get; set; } = new();
    
    /// <summary>家族注册表</summary>
    public FamilyRegistry Families { get; set; } = new();
    
    // ========================================
    // 子队伍系统
    // ========================================
    
    /// <summary>子队伍注册表</summary>
    public SubPartyRegistry SubParties { get; set; } = new();
    
    // ========================================
    // 遭遇实体系统
    // ========================================
    
    /// <summary>遭遇实体生成器</summary>
    public EncounterEntitySpawner EncounterSpawner { get; set; } = new();
    
    /// <summary>不活跃实体池</summary>
    public DormantEntityPool DormantPool => EncounterSpawner.DormantPool;
    
    // ========================================
    // 时间状态
    // ========================================
    
    /// <summary>当前游戏天数（从 1 开始）</summary>
    public int CurrentDay { get; set; } = 1;
    
    /// <summary>累计游戏小时数</summary>
    public float GameHour { get; set; } = 0f;
    
    /// <summary>玩家世界像素位置</summary>
    public Godot.Vector2 PlayerPosition { get; set; }

    /// <summary>玩家当前战略阵营；未效忠国家或建国前使用 player。</summary>
    public string PlayerFaction { get; set; } = OverworldHostility.DefaultPlayerFaction;

    /// <summary>当前大地图天气移速倍率。由 Frontend 天气系统注入，Core 只消费纯数值。</summary>
    public float WeatherSpeedFactor { get; set; } = 1.0f;
    
    /// <summary>玩家等级（影响敌方等级缩放）</summary>
    public int PlayerLevel { get; set; } = 1;
    
    /// <summary>玩家种族 ID</summary>
    public int PlayerRaceId { get; set; } = 0;
    
    // ========================================
    // 玩家王国相关
    // ========================================
    
    /// <summary>玩家王国（null = 尚未创建）</summary>
    public PlayerKingdom? PlayerKingdom { get; set; }
    
    /// <summary>开国前占领列表</summary>
    public List<string> PendingConquests { get; set; } = new();
    
    
    // ========================================
    // ZoC 管理器（可选）
    // ========================================
    
    /// <summary>POI 控制区管理器</summary>
    public ZoneOfControlManager? ZocManager { get; set; }
    
    // ========================================
    // 工具方法
    // ========================================
    
    /// <summary>按名称查找实体（线性搜索，低频使用）</summary>
    public OverworldEntity? FindEntityByName(string name)
    {
        foreach (var e in Entities)
            if (e.EntityName == name) return e;
        return null;
    }
    
    /// <summary>按名称查找 POI（线性搜索，低频使用）</summary>
    public OverworldPOI? FindPoiByName(string name)
    {
        foreach (var p in Pois)
            if (p.PoiName == name) return p;
        return null;
    }
}

