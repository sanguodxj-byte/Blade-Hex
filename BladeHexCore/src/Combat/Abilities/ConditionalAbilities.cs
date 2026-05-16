// ConditionalAbilities.cs
// 条件触发能力 — 词缀和高级特性使用
//
// 这些能力检查战斗上下文（攻击者状态、目标类型等）才生效
using System;
using BladeHex.Data;

namespace BladeHex.Combat.Abilities;

/// <summary>
/// 条件触发的伤害骰加成 — 攻击命中后，若条件满足则添加额外骰子伤害。
/// 例如：vs_undead +1d6 火焰
/// </summary>
public sealed class ConditionalDamageDiceAbility : EquipmentAbility
{
    /// <summary>条件 ID（如 "vs_undead", "low_hp", "mounted"）</summary>
    public string Condition { get; init; } = "";

    /// <summary>骰子数量</summary>
    public int DiceCount { get; init; }

    /// <summary>骰子面数</summary>
    public int DiceSides { get; init; }

    /// <summary>伤害类型描述（仅用于 tooltip）</summary>
    public string DamageType { get; init; } = "";

    public override string GetTooltipText()
    {
        string condText = ConditionText(Condition);
        return $"{condText}+{DiceCount}d{DiceSides}{DamageType}伤害";
    }

    public override void OnDealDamage(DealDamageContext ctx)
    {
        if (!CheckCondition(ctx)) return;

        // 骰额外伤害
        int extraDamage = 0;
        for (int i = 0; i < DiceCount; i++)
            extraDamage += Godot.GD.RandRange(1, DiceSides);

        // 直接以额外伤害事件返回（由 CombatResolver 应用到 defender）
        ctx.ExtraDamageEvents.Add(new DamageEvent
        {
            Target = ctx.Defender,
            Damage = extraDamage,
            SourceAbilityId = AbilityId,
        });
    }

    private bool CheckCondition(DealDamageContext ctx) => Condition switch
    {
        "vs_undead" => ctx.Defender.Data.enemyType == UnitData.EnemyType.Undead,
        "vs_demon" => ctx.Defender.Data.enemyType == UnitData.EnemyType.Demon,
        "vs_beast" => ctx.Defender.Data.enemyType == UnitData.EnemyType.Beast,
        "vs_cavalry" => ctx.Defender.Data.IsMounted,
        "low_hp" => ctx.Attacker.CurrentHp < ctx.Attacker.GetMaxHp() / 2,
        "mounted" => ctx.Attacker.Data.IsMounted,
        // first_attack/high_ground/flanking 需要更多上下文，留待扩展
        _ => false,
    };

    private static string ConditionText(string cond) => cond switch
    {
        "vs_undead" => "对亡灵",
        "vs_cavalry" => "对骑兵",
        "vs_beast" => "对野兽",
        "vs_demon" => "对魔物",
        "low_hp" => "低HP时",
        "mounted" => "骑乘时",
        "first_attack" => "首次攻击",
        "flanking" => "包夹时",
        "high_ground" => "高地时",
        _ => cond,
    };
}

// ============================================================================
// 通用免疫/抗性能力
// ============================================================================

/// <summary>免疫指定状态/伤害类型（如 immune_fear, immune_poison）</summary>
public sealed class ImmunityAbility : EquipmentAbility
{
    public string ImmunityType { get; init; } = "";

    public override string GetTooltipText() => $"免疫{ImmunityTypeText(ImmunityType)}";

    private static string ImmunityTypeText(string t) => t switch
    {
        "fear" => "恐惧",
        "stun" => "眩晕",
        "poison" => "中毒",
        "charm" => "魅惑",
        _ => t,
    };
}

/// <summary>地形通过性（forest_walk, stealth_no_break 等）— 用于大地图/战斗外查询</summary>
public sealed class TerrainTraitAbility : EquipmentAbility
{
    public string TerrainTrait { get; init; } = "";

    public override string GetTooltipText() => TerrainTrait switch
    {
        "forest_walk" => "穿越森林不减速",
        "stealth_no_break" => "潜行不中断",
        _ => TerrainTrait,
    };
}

/// <summary>额外伤害骰子（无条件附加，如战熊的 extra_damage_1d4）</summary>
public sealed class ExtraDamageDiceAbility : EquipmentAbility
{
    public int DiceCount { get; init; }
    public int DiceSides { get; init; }

    public override string GetTooltipText()
        => $"附带{DiceCount}d{DiceSides}额外伤害";

    public override void OnDealDamage(DealDamageContext ctx)
    {
        int extra = 0;
        for (int i = 0; i < DiceCount; i++)
            extra += Godot.GD.RandRange(1, DiceSides);
        ctx.ExtraDamageEvents.Add(new DamageEvent
        {
            Target = ctx.Defender,
            Damage = extra,
            SourceAbilityId = AbilityId,
        });
    }
}
