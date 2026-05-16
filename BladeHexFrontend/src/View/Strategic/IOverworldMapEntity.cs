// IOverworldMapEntity.cs
// 大地图可交互实体接口 — 统一 OverworldParty/OverworldEnemy/OverworldTown 的公共契约
// InteractionManager 和 OverworldEntityManager 通过此接口操作实体，无需关心具体类型
using Godot;

namespace BladeHex.Strategic;

/// <summary>
/// 大地图可交互实体接口。
/// 所有出现在大地图上、可被玩家交互的实体（队伍/敌人/城镇）都实现此接口。
/// </summary>
public interface IOverworldMapEntity
{
    /// <summary>实体在大地图上的像素位置</summary>
    Vector2 Position { get; set; }

    /// <summary>获取显示名称（用于 UI 标签和交互面板标题）</summary>
    string GetDisplayName();

    /// <summary>获取描述文本（用于交互面板描述区域）</summary>
    string GetDescription();

    /// <summary>放置到指定像素坐标</summary>
    void PlaceAt(float px, float py);
}
