// IOverworldContext.cs
// 大地图场景对外暴露的强类型接口 — 供 OverworldUI 等子系统访问场景数据
// 消除 GetParent().Get("PropertyName") 的反射式耦合
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
    // 系统管理器
    // ========================================

    /// <summary>经济管理器</summary>
    EconomyManager EconomyMgr { get; }

    /// <summary>实体管理器</summary>
    OverworldEntityManager EntityMgr { get; }

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
}
