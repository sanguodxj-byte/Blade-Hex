// DragController.cs
// 拖拽中央控制器 — 状态机管理拖拽生命周期
// 注册多个容器，统一处理鼠标事件和命中检测
using Godot;
using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.View.UI.Inventory;

/// <summary>
/// 物品拖拽中央控制器。
/// 注册多个容器后，统一处理拖拽全流程：
/// - BeginDrag 启动拖拽
/// - _Process 跟踪鼠标，更新幽灵位置和命中高亮
/// - 鼠标释放 → 命中目标容器 → 调用 Accept
/// - 失败/取消 → 回退状态
/// </summary>
[GlobalClass]
public partial class DragController : Node
{
    /// <summary>当前拖拽状态</summary>
    public enum DragState { Idle, Dragging }

    private DragState _state = DragState.Idle;
    private DragSource? _source;
    private DragGhost? _ghost;
    private Vector2 _dragOffset;
    private IItemContainer? _lastHoveredContainer;

    private readonly List<IItemContainer> _containers = new();
    private Control _ghostParent = null!;

    /// <summary>当前是否在拖拽</summary>
    public bool IsDragging => _state == DragState.Dragging;

    /// <summary>当前拖拽来源</summary>
    public DragSource? CurrentSource => _source;

    /// <summary>开始拖拽时触发（订阅者可用于关闭 tooltip 等）</summary>
    [Signal] public delegate void DragStartedEventHandler();

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    /// <summary>设置幽灵的父节点（用于全局定位）</summary>
    public void SetGhostParent(Control parent) => _ghostParent = parent;

    /// <summary>注册一个参与拖拽的容器</summary>
    public void RegisterContainer(IItemContainer container)
    {
        if (!_containers.Contains(container))
            _containers.Add(container);
    }

    /// <summary>移除容器（容器销毁时调用）</summary>
    public void UnregisterContainer(IItemContainer container)
    {
        _containers.Remove(container);
    }

    /// <summary>清除所有容器（重建 UI 前调用）</summary>
    public void ClearContainers()
    {
        if (_state == DragState.Dragging) Cancel();
        _containers.Clear();
        _lastHoveredContainer = null;
    }

    /// <summary>开始拖拽（容器在收到鼠标按下事件时调用）</summary>
    public void BeginDrag(DragSource source, Vector2 mousePos, Vector2 sourceGlobalPos, Vector2 sourceSize)
    {
        if (_state == DragState.Dragging) Cancel();

        _source = source;
        _dragOffset = sourceGlobalPos - mousePos;

        _ghost = DragGhost.Create(source.Item, sourceSize);
        if (_ghostParent != null) _ghostParent.AddChild(_ghost);
        _ghost.GlobalPosition = mousePos + _dragOffset;

        _state = DragState.Dragging;
        EmitSignal(SignalName.DragStarted);
    }

    /// <summary>外部取消（如 ESC 键）</summary>
    public void Cancel()
    {
        if (_state == DragState.Idle) return;
        Cleanup();
    }

    public override void _Process(double delta)
    {
        if (_state != DragState.Dragging) return;

        var mouse = GetViewport().GetMousePosition();

        // 更新幽灵位置
        if (_ghost != null && IsInstanceValid(_ghost))
            _ghost.GlobalPosition = mouse + _dragOffset;

        // 命中检测 + 高亮
        var hit = HitTestContainers(mouse);
        var newContainer = hit?.Container;

        if (_lastHoveredContainer != newContainer)
        {
            _lastHoveredContainer?.ClearHighlights();
            _lastHoveredContainer = newContainer;
        }

        if (newContainer != null && _source != null)
            newContainer.HighlightDropTarget(_source, hit);
    }

    public override void _Input(InputEvent ev)
    {
        if (_state != DragState.Dragging) return;

        if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            CompleteDrop();
            GetViewport().SetInputAsHandled();
        }
        else if (ev is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Cancel();
            GetViewport().SetInputAsHandled();
        }
    }

    private void CompleteDrop()
    {
        if (_source == null) { Cleanup(); return; }

        var mouse = GetViewport().GetMousePosition();
        var hit = HitTestContainers(mouse);

        if (hit != null && hit.Container.CanAccept(_source, hit))
        {
            bool accepted = hit.Container.Accept(_source, hit);
            if (accepted)
            {
                // 同容器内移动 = TryMove/TrySwap 已经原地修改，跳过 RemoveFromSource
                bool sameContainer = hit.Container == _source.Container;
                if (!sameContainer)
                    _source.Container.RemoveFromSource(_source);

                hit.Container.Refresh();
                if (!sameContainer)
                    _source.Container.Refresh();
            }
        }

        Cleanup();
    }

    private ContainerHitInfo? HitTestContainers(Vector2 globalMousePos)
    {
        // 反向遍历：后注册的容器优先（通常是顶层 UI）
        for (int i = _containers.Count - 1; i >= 0; i--)
        {
            var c = _containers[i];
            var hit = c.HitTest(globalMousePos);
            if (hit != null) return hit;
        }
        return null;
    }

    private void Cleanup()
    {
        _lastHoveredContainer?.ClearHighlights();
        _lastHoveredContainer = null;

        if (_ghost != null && IsInstanceValid(_ghost))
            _ghost.QueueFree();
        _ghost = null;

        _source = null;
        _state = DragState.Idle;
    }
}
