using System.Collections.Generic;
using Godot;

namespace BladeHex.Combat.Buff;

/// <summary>
/// Buff 运行时实例。挂在单位身上,代表一个活跃的正面/负面效果。
/// 替代旧的 StatusEffectInstance。
/// </summary>
public class BuffInstance
{
    // ============================================================
    // 身份
    // ============================================================

    /// <summary>Buff 模板 ID(对应 BuffRegistry 中的 key)</summary>
    public string Id = "";

    /// <summary>显示名</summary>
    public string Name = "";

    /// <summary>描述文本</summary>
    public string Description = "";

    /// <summary>图标 ID(用于 UI 显示)</summary>
    public string IconId = "";

    /// <summary>是否负面效果</summary>
    public bool IsNegative;

    /// <summary>标签(用于互斥/交互判定)。如 ["fire", "dot"], ["ice", "cc"]</summary>
    public string[] Tags = System.Array.Empty<string>();

    // ============================================================
    // 持续时间 & 叠加
    // ============================================================

    /// <summary>剩余持续回合数。-1 = 永久(直到被移除)</summary>
    public int Duration;

    /// <summary>最大叠加层数。1 = 不可叠加(刷新持续时间)</summary>
    public int MaxStacks = 1;

    /// <summary>当前叠加层数。通过 BuffSystem 或 internal 代码修改。</summary>
    public int CurrentStacks { get; internal set; } = 1;

    // ============================================================
    // 属性修正(多乘区)
    // ============================================================

    /// <summary>该 buff 提供的所有属性修正</summary>
    public List<StatModifier> Modifiers = new();

    // ============================================================
    // 触发器
    // ============================================================

    /// <summary>该 buff 挂载的所有触发器</summary>
    public List<BuffTrigger> Triggers = new();

    // ============================================================
    // Tick 效果
    // ============================================================

    /// <summary>每回合 tick 效果(可为 null = 无 tick)</summary>
    public TickEffect? OnTick;

    // ============================================================
    // 来源 & 互斥
    // ============================================================

    /// <summary>施加者单位 ID(-1 = 系统/环境)</summary>
    public int SourceUnitId = -1;

    /// <summary>来源标识(同源同 ID 的 buff 不叠加,刷新持续时间)</summary>
    public string Source = "";

    /// <summary>互斥标签:施加时自动移除目标身上含这些标签的 buff</summary>
    public string[] CancelTags = System.Array.Empty<string>();

    // ============================================================
    // 豁免
    // ============================================================

    /// <summary>每回合可尝试豁免解除的属性("fortitude"/"reflex"/"will",空=不可豁免)</summary>
    public string SaveToRemove = "";

    /// <summary>豁免 DC</summary>
    public int SaveDc;

    // ============================================================
    // 特殊标记
    // ============================================================

    /// <summary>攻击后自动解除(如隐身)</summary>
    public bool BreaksOnAttack;

    /// <summary>可蔓延到相邻单位(如燃烧)</summary>
    public bool CanSpread;

    /// <summary>是否在死亡时保留(如诅咒)</summary>
    public bool PersistOnDeath;

    /// <summary>
    /// 转为前端/UI 兼容的 Dictionary。
    /// 这是迁移期适配层: 让新 Buff 能进入旧状态图标/列表管线,不改变旧 StatusEffect 结算语义。
    /// </summary>
    public Godot.Collections.Dictionary ToGodotDict()
    {
        var mods = new Godot.Collections.Dictionary();
        foreach (var modifier in Modifiers)
        {
            if (string.IsNullOrEmpty(modifier.Stat)) continue;
            mods[modifier.Stat] = modifier.Value * CurrentStacks;
        }

        return new Godot.Collections.Dictionary
        {
            { "id", Id }, { "name", Name }, { "description", Description },
            { "duration", Duration }, { "is_negative", IsNegative },
            { "icon_id", IconId }, { "tags", Tags },
            { "source_unit_id", SourceUnitId }, { "source", Source },
            { "stacks", CurrentStacks }, { "max_stacks", MaxStacks },
            { "stat_modifiers", mods },
        };
    }
}
