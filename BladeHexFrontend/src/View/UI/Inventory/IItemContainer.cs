// IItemContainer.cs
// 物品容器接口 — 任何能持有物品并参与拖拽的 UI 区域
// 实现者：背包网格、装备槽、商店货架、战利品堆等
using Godot;
using BladeHex.Data;
using BladeHex.Strategic;

namespace BladeHex.View.UI.Inventory;

/// <summary>
/// 物品拖拽来源信息 — 拖拽开始时由源容器创建，传递给目标容器。
/// </summary>
public class DragSource
{
    /// <summary>来源容器</summary>
    public IItemContainer Container { get; init; } = null!;

    /// <summary>拖拽的物品数据</summary>
    public ItemData Item { get; init; } = null!;

    /// <summary>来源标识：背包格物品则为 GridItem，装备槽则为 slotId 字符串，商店则为 null</summary>
    public object? Origin { get; init; }
}

/// <summary>
/// 容器命中信息 — HitTest 的返回值，描述具体的放置位置。
/// </summary>
public class ContainerHitInfo
{
    /// <summary>命中的容器</summary>
    public IItemContainer Container { get; init; } = null!;

    /// <summary>具体位置标识：网格则为 Vector2I 格坐标，装备槽则为 slotId 字符串</summary>
    public object? Target { get; init; }
}

/// <summary>
/// 物品容器接口。
/// 所有支持拖拽的 UI 区域都实现此接口，由 DragController 统一调度。
/// </summary>
public interface IItemContainer
{
    /// <summary>容器在屏幕上的命中区域（用于全局鼠标检测）</summary>
    Rect2 GetGlobalRect();

    /// <summary>
    /// 命中测试：判断鼠标位置是否在容器内，并返回具体的目标信息。
    /// 返回 null 表示未命中。
    /// </summary>
    ContainerHitInfo? HitTest(Vector2 globalMousePos);

    /// <summary>
    /// 验证：源物品是否可以放入此容器的指定位置？
    /// </summary>
    bool CanAccept(DragSource source, ContainerHitInfo hit);

    /// <summary>
    /// 接收物品到指定位置（在 CanAccept 返回 true 后调用）。
    /// 返回是否成功。
    /// </summary>
    bool Accept(DragSource source, ContainerHitInfo hit);

    /// <summary>
    /// 移除来源物品（拖拽完成后调用，由源容器自行实现移除逻辑）。
    /// </summary>
    void RemoveFromSource(DragSource source);

    /// <summary>
    /// 高亮显示拖拽目标位置（拖拽过程中实时调用）。
    /// </summary>
    void HighlightDropTarget(DragSource source, ContainerHitInfo? hit);

    /// <summary>清除所有高亮</summary>
    void ClearHighlights();

    /// <summary>刷新显示</summary>
    void Refresh();
}
