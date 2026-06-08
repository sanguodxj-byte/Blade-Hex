// IOverworldContext.cs
// 大地图场景对外暴露的强类型接口 — 供 OverworldUI 等子系统访问场景数据
// 消除 GetParent().Get("PropertyName") 的反射式耦合
//
// 设计原则：UI 层只看到查询结果，不暴露完整管理器。
// EconomyManager / OverworldEntityManager 的窄化查询直接声明在本接口上。
using Godot;
using BladeHex.Data;
using BladeHex.Strategic;

namespace BladeHex.Scenes.Overworld;

/// <summary>
/// 大地图场景上下文接口 — UI 层通过此接口访问场景数据，
/// 避免字符串属性名的反射式访问。
/// </summary>
public interface IOverworldContext
{
    // ========================================
    // 玩家数据
    // ========================================

    /// <summary>玩家队伍节点</summary>
    OverworldParty PlayerParty { get; }

    /// <summary>玩家单位数据</summary>
    UnitData PlayerUnitData { get; }

    /// <summary>玩家种族 ID</summary>
    int PlayerRaceId { get; }

    // ========================================
    // 声望
    // ========================================

    /// <summary>声望追踪器</summary>
    ReputationTracker ReputationTracker { get; }

    // ========================================
    // 窄化经济查询（替代 EconomyManager 整体暴露）
    // ========================================

    /// <summary>当前游戏天数</summary>
    int CurrentDay { get; }

    /// <summary>增加玩家金币</summary>
    void AddGold(int amount);

    // ========================================
    // 状态
    // ========================================

    /// <summary>是否正在等待/扎营</summary>
    bool IsWaiting { get; set; }

    // ========================================
    // 操作
    // ========================================

    /// <summary>持久化世界数据（手动保存时调用）</summary>
    void SaveWorldData();

    /// <summary>待触发的走私伏击战队实体</summary>
    OverworldEntity? PendingSmuggleAmbushEntity { get; set; }
}
