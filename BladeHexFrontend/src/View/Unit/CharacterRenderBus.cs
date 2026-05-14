// CharacterRenderBus.cs
// 角色渲染树总线 — 纯注册簿 + 信号广播层
// 职责: 管理 Unit→RenderNode 映射、选中管理、信号转发
// 不持有渲染节点（渲染节点是 Unit 的子节点，自动跟随位置）
using Godot;
using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.Combat;

[GlobalClass]
public partial class CharacterRenderBus : Node
{
    /// <summary>全局实例引用（由 CombatManager 或场景入口在 _Ready 时设置）</summary>
    public static 
        CharacterRenderBus? Instance { get; set; }

    // ========================================
    // 全局视觉常量
    // ========================================

    public const float DefaultPixelSize = 1.0f;
    public static readonly Vector2 PlaceholderTextureSize = new(80, 120);
    public static readonly Color PlayerColor = new(0.2f, 0.5f, 1.0f);
    public static readonly Color EnemyColor = new(1.0f, 0.2f, 0.2f);
    public const float DefaultTexHeight = 120.0f;

    // ========================================
    // 对外信号 — 统一用 Unit 做参数（UI 不需要知道 RenderNode）
    // ========================================

    [Signal] public delegate void UnitSelectedEventHandler(Unit unit);
    [Signal] public delegate void UnitDeselectedEventHandler(Unit unit);
    [Signal] public delegate void UnitHpChangedEventHandler(Unit unit, int currentHp, int maxHp);
    [Signal] public delegate void UnitDiedEventHandler(Unit unit);
    [Signal] public delegate void UnitAnimationPlayedEventHandler(Unit unit, string animName);
    [Signal] public delegate void UnitStatusEffectsChangedEventHandler(Unit unit, Godot.Collections.Array effects);
    [Signal] public delegate void UnitEquipmentChangedEventHandler(Unit unit, int slot);
    [Signal] public delegate void TurnChangedEventHandler(Unit activeUnit);

    // ========================================
    // 注册簿 — key = Unit instance ID, value = CharacterRenderNode
    // ========================================

    private readonly Dictionary<ulong, CharacterRenderNode> _registry = new();
    private Unit? _selectedUnit;

    // ========================================
    // 注册 / 注销
    // ========================================

    /// <summary>注册 Unit 和它的 RenderNode 到总线</summary>
    public bool Register(Unit unit, CharacterRenderNode renderNode)
    {
        if (unit == null || renderNode == null)
        {
            GD.PushWarning("CharacterRenderBus.Register: 参数为空");
            return false;
        }

        ulong uid = unit.GetInstanceId();
        if (_registry.ContainsKey(uid))
            Unregister(unit);

        _registry[uid] = renderNode;

        // 转发 RenderNode 的信号
        renderNode.HpUpdated += (current, max) => OnHpUpdated(current, max, unit);
        renderNode.Died += () => OnDied(unit);
        renderNode.EquipmentSlotChanged += (slot) => OnEquipChanged(slot, unit);

        return true;
    }

    /// <summary>注销</summary>
    public void Unregister(Unit unit)
    {
        if (unit == null) return;
        ulong uid = unit.GetInstanceId();
        if (!_registry.ContainsKey(uid)) return;

        if (_selectedUnit == unit)
            DeselectAll();

        var renderNode = _registry[uid];
        if (GodotObject.IsInstanceValid(renderNode))
        {
            // C# 事件不需要手动断开，GC 会处理
            // 但为安全起见，在 renderNode 销毁前清理引用
        }

        _registry.Remove(uid);
    }

    // ========================================
    // 查询
    // ========================================

    public CharacterRenderNode? GetRenderNode(Unit unit)
    {
        if (unit == null) return null;
        _registry.TryGetValue(unit.GetInstanceId(), out var node);
        return node;
    }

    public List<CharacterRenderNode> GetAllRenderNodes()
    {
        var result = new List<CharacterRenderNode>();
        foreach (var node in _registry.Values)
        {
            if (GodotObject.IsInstanceValid(node))
                result.Add(node);
        }
        return result;
    }

    public int GetCount() => _registry.Count;

    // ========================================
    // 场景切换时清除
    // ========================================

    public void ClearAll()
    {
        DeselectAll();
        _registry.Clear();
    }

    // ========================================
    // 选中管理
    // ========================================

    public void Select(Unit unit)
    {
        DeselectAll();
        var node = GetRenderNode(unit);
        if (node != null)
        {
            _selectedUnit = unit;
            node.SetSelected(true);
            EmitSignal(SignalName.UnitSelected, unit);
        }
    }

    public void DeselectAll()
    {
        if (_selectedUnit != null && GodotObject.IsInstanceValid(_selectedUnit))
        {
            var node = GetRenderNode(_selectedUnit);
            node?.SetSelected(false);
            var old = _selectedUnit;
            _selectedUnit = null;
            EmitSignal(SignalName.UnitDeselected, old);
        }
    }

    public Unit? GetSelectedUnit() => _selectedUnit;

    // ========================================
    // 批量操作
    // ========================================

    public void RefreshAllHp()
    {
        foreach (var node in _registry.Values)
        {
            if (GodotObject.IsInstanceValid(node) && node.UnitRef != null)
                node.UpdateHp(node.UnitRef.CurrentHp, node.UnitRef.GetMaxHp());
        }
    }

    public void RefreshAllStatus()
    {
        foreach (var node in _registry.Values)
        {
            if (GodotObject.IsInstanceValid(node) && node.UnitRef?.Data != null)
            {
                var effects = new Godot.Collections.Array();
                foreach (var inst in node.UnitRef.Data.Runtime.ActiveStatusEffects)
                    effects.Add(inst.ToGodotDict());
                node.UpdateStatusEffects(effects);
            }
        }
    }

    public void PlayAnimAll(string animName)
    {
        foreach (var node in _registry.Values)
        {
            if (GodotObject.IsInstanceValid(node))
                node.PlayAnimation(animName);
        }
    }

    // ========================================
    // 通知接口 — 供 CombatManager / Unit 调用
    // ========================================

    /// <summary>通知某单位受击</summary>
    public void NotifyHit(Unit unit)
    {
        var node = GetRenderNode(unit);
        node?.PlayHit();
    }

    /// <summary>通知某单位死亡</summary>
    public void NotifyDeath(Unit unit)
    {
        var node = GetRenderNode(unit);
        node?.PlayDeath();
    }

    /// <summary>通知某单位播放技能动画</summary>
    public void NotifyAnimation(Unit unit, string animName)
    {
        var node = GetRenderNode(unit);
        if (node != null)
        {
            node.PlayAnimation(animName);
            EmitSignal(SignalName.UnitAnimationPlayed, unit, animName);
        }
    }

    /// <summary>通知回合切换</summary>
    public void NotifyTurnChanged(Unit activeUnit)
    {
        foreach (var node in _registry.Values)
        {
            if (GodotObject.IsInstanceValid(node))
                node.SetActiveTurn(false);
        }
        if (activeUnit != null)
        {
            var node = GetRenderNode(activeUnit);
            node?.SetActiveTurn(true);
        }
        EmitSignal(SignalName.TurnChanged, activeUnit != null ? (Variant)activeUnit : default);
    }

    /// <summary>通知装备全量刷新</summary>
    public void NotifyEquipmentRefresh(Unit unit)
    {
        var node = GetRenderNode(unit);
        node?.RefreshAllEquipment();
    }

    /// <summary>通知状态效果变化</summary>
    public void NotifyStatusEffects(Unit unit, Godot.Collections.Array effects)
    {
        var node = GetRenderNode(unit);
        if (node != null)
        {
            node.UpdateStatusEffects(effects);
            EmitSignal(SignalName.UnitStatusEffectsChanged, unit, effects);
        }
    }

    // ========================================
    // RenderNode 信号转发
    // ========================================

    private void OnHpUpdated(int current, int max, Unit unit)
        => EmitSignal(SignalName.UnitHpChanged, unit, current, max);

    private void OnDied(Unit unit)
        => EmitSignal(SignalName.UnitDied, unit);

    private void OnEquipChanged(int slot, Unit unit)
        => EmitSignal(SignalName.UnitEquipmentChanged, unit, slot);
}
