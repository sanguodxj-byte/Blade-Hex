using System.Collections.Generic;

namespace BladeHex.Combat.Buff;

/// <summary>
/// Buff 模板注册表。所有 buff 的"原型"在这里定义。
/// BuffSystem.Apply 从这里取模板克隆为实例。
/// </summary>
public static class BuffRegistry
{
    private static readonly Dictionary<string, BuffInstance> _templates = new();
    private static bool _initialized;

    public static BuffInstance? Get(string buffId)
    {
        EnsureInitialized();
        return _templates.GetValueOrDefault(buffId);
    }

    public static IReadOnlyDictionary<string, BuffInstance> GetAll()
    {
        EnsureInitialized();
        return _templates;
    }

    /// <summary>运行时注册自定义 buff(mod 支持)</summary>
    public static void Register(string id, BuffInstance template)
    {
        _templates[id] = template;
    }

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        RegisterAll();
    }

    private static void RegisterAll()
    {
        // ====== 负面状态 ======
        Register("poison", new BuffInstance
        {
            Id = "poison", Name = "中毒", Description = "每回合受到 1d4 毒素伤害",
            IconId = "IconPoison", IsNegative = true, Duration = 3,
            Tags = new[] { "dot", "poison" },
            OnTick = new TickEffect { DiceCount = 1, DiceSides = 4, DamageType = "poison" },
            SaveToRemove = "fortitude", SaveDc = 12,
        });

        Register("burning", new BuffInstance
        {
            Id = "burning", Name = "燃烧", Description = "每回合受到 1d6 火焰伤害,可蔓延",
            IconId = "IconFire", IsNegative = true, Duration = 3,
            Tags = new[] { "dot", "fire" },
            CancelTags = new[] { "ice" },
            OnTick = new TickEffect { DiceCount = 1, DiceSides = 6, DamageType = "fire" },
            CanSpread = true,
        });

        Register("frozen", new BuffInstance
        {
            Id = "frozen", Name = "冰冻", Description = "无法行动,AC-2",
            IconId = "IconFreeze", IsNegative = true, Duration = 1,
            Tags = new[] { "cc", "ice" },
            CancelTags = new[] { "fire" },
            Modifiers = new() { new StatModifier { Stat = "ac", Layer = ModifierLayer.Base, Value = -2 } },
        });

        Register("bleed", new BuffInstance
        {
            Id = "bleed", Name = "流血", Description = "每回合受到 1d4 伤害,可叠加",
            IconId = "IconSlash", IsNegative = true, Duration = -1, MaxStacks = 5,
            Tags = new[] { "dot", "bleed" },
            OnTick = new TickEffect { DiceCount = 1, DiceSides = 4, DamageType = "bleed" },
        });

        Register("slow", new BuffInstance
        {
            Id = "slow", Name = "减速", Description = "移动力-2",
            IconId = "IconIce", IsNegative = true, Duration = 2,
            Tags = new[] { "debuff" },
            Modifiers = new() { new StatModifier { Stat = "speed", Layer = ModifierLayer.Base, Value = -2 } },
        });

        Register("stun", new BuffInstance
        {
            Id = "stun", Name = "眩晕", Description = "本回合只能移动或攻击(二选一)",
            IconId = "IconStun", IsNegative = true, Duration = 1,
            Tags = new[] { "cc" },
        });

        Register("fear", new BuffInstance
        {
            Id = "fear", Name = "恐惧", Description = "必须远离恐惧源,不可主动攻击",
            IconId = "IconDark", IsNegative = true, Duration = 2,
            Tags = new[] { "cc", "fear" },
            SaveToRemove = "will", SaveDc = 15,
        });

        Register("silence", new BuffInstance
        {
            Id = "silence", Name = "沉默", Description = "无法施放法术",
            IconId = "IconCrush", IsNegative = true, Duration = 2,
            Tags = new[] { "cc" },
        });

        // ====== 正面状态 ======
        Register("bless", new BuffInstance
        {
            Id = "bless", Name = "祝福", Description = "攻击+2,AC+1",
            IconId = "IconBless", IsNegative = false, Duration = 3,
            Tags = new[] { "buff", "holy" },
            Modifiers = new()
            {
                new StatModifier { Stat = "attack_bonus", Layer = ModifierLayer.Base, Value = 2 },
                new StatModifier { Stat = "ac", Layer = ModifierLayer.Base, Value = 1 },
            },
        });

        Register("haste", new BuffInstance
        {
            Id = "haste", Name = "加速", Description = "移动力+2,攻击速度+25%",
            IconId = "IconLightning", IsNegative = false, Duration = 3,
            Tags = new[] { "buff" },
            Modifiers = new()
            {
                new StatModifier { Stat = "speed", Layer = ModifierLayer.Base, Value = 2 },
                new StatModifier { Stat = "damage", Layer = ModifierLayer.Increased, Value = 0.25f },
            },
        });

        Register("shield", new BuffInstance
        {
            Id = "shield", Name = "魔法护盾", Description = "AC+5",
            IconId = "IconShield", IsNegative = false, Duration = 1,
            Tags = new[] { "buff", "shield" },
            Modifiers = new() { new StatModifier { Stat = "ac", Layer = ModifierLayer.Base, Value = 5 } },
        });

        Register("regen", new BuffInstance
        {
            Id = "regen", Name = "再生", Description = "每回合恢复 1d6 HP",
            IconId = "IconHoly", IsNegative = false, Duration = 3,
            Tags = new[] { "buff", "heal" },
            OnTick = new TickEffect { DiceCount = 1, DiceSides = 6, IsHeal = true },
        });

        Register("battle_fury", new BuffInstance
        {
            Id = "battle_fury", Name = "战斗狂怒", Description = "伤害+30%(更多),AC-2",
            IconId = "IconFire", IsNegative = false, Duration = 3,
            Tags = new[] { "buff", "rage" },
            Modifiers = new()
            {
                new StatModifier { Stat = "damage", Layer = ModifierLayer.More, Value = 0.3f },
                new StatModifier { Stat = "ac", Layer = ModifierLayer.Base, Value = -2 },
            },
        });

        Register("heroic_call", new BuffInstance
        {
            Id = "heroic_call", Name = "英雄号召", Description = "全体友军攻击+2,AC+1",
            IconId = "IconBless", IsNegative = false, Duration = 3,
            Tags = new[] { "buff", "aura" },
            Modifiers = new()
            {
                new StatModifier { Stat = "attack_bonus", Layer = ModifierLayer.Base, Value = 2 },
                new StatModifier { Stat = "ac", Layer = ModifierLayer.Base, Value = 1 },
            },
        });

        // ====== 触发器型 buff ======
        Register("thorns", new BuffInstance
        {
            Id = "thorns", Name = "荆棘", Description = "被近战攻击时反弹 1d4 伤害",
            IconId = "IconPierce", IsNegative = false, Duration = 5,
            Tags = new[] { "buff", "thorns" },
            Triggers = new()
            {
                new BuffTrigger
                {
                    Event = TriggerEvent.OnTakeDamage,
                    Effect = "deal_damage:1d4:pierce",
                    Condition = "melee_only",
                },
            },
        });

        Register("vampiric", new BuffInstance
        {
            Id = "vampiric", Name = "吸血", Description = "造成伤害时恢复 25% HP",
            IconId = "IconDark", IsNegative = false, Duration = 3,
            Tags = new[] { "buff", "lifesteal" },
            Triggers = new()
            {
                new BuffTrigger
                {
                    Event = TriggerEvent.OnDealDamage,
                    Effect = "heal_percent:25",
                },
            },
        });

        // ====== 职业大招 buff (v0.8) — 已拆分到 CareerBuffRegistry.cs ======
        CareerBuffRegistry.RegisterAll();
    }
}
