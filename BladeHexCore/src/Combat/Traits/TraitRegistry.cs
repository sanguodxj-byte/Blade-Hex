// TraitRegistry.cs
// 特质注册表 — 静态注册器，提供批量分发方法
// T05: 建立 TraitRegistry 框架
using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.Combat.Traits;

/// <summary>
/// 特质注册表 — 所有特质效果的中央注册器
/// 模式参照 BuffRegistry
/// </summary>
public static class TraitRegistry
{
    private static readonly Dictionary<string, ITraitEffect> _effects = new();
    private static bool _initialized;

    /// <summary>获取特质效果</summary>
    public static ITraitEffect? Get(string traitId)
    {
        EnsureInitialized();
        return _effects.GetValueOrDefault(traitId);
    }

    /// <summary>获取所有已注册特质</summary>
    public static IReadOnlyDictionary<string, ITraitEffect> GetAll()
    {
        EnsureInitialized();
        return _effects;
    }

    /// <summary>运行时注册自定义特质效果（mod 支持）</summary>
    public static void Register(ITraitEffect effect)
    {
        _effects[effect.TraitId] = effect;
    }

    // ========================================================================
    // 批量分发方法 — 遍历单位所有特质并调用对应钩子
    // ========================================================================

    /// <summary>单位创建时应用所有特质</summary>
    public static void ApplyAllAtCreation(UnitData u)
    {
        if (u.CharacterTraits == null) return;
        foreach (var trait in u.CharacterTraits)
        {
            if (trait == null || trait.traitType != TraitData.TraitType.Functional) continue;
            var effect = Get(trait.FunctionalEffect);
            effect?.OnUnitCreated(u, trait.EffectValue);
        }
    }

    /// <summary>回合开始时应用所有特质</summary>
    public static void ApplyAllOnTurnStart(BattleUnitModel unit)
    {
        if (unit.Data.CharacterTraits == null) return;
        foreach (var trait in unit.Data.CharacterTraits)
        {
            if (trait == null || trait.traitType != TraitData.TraitType.Functional) continue;
            var effect = Get(trait.FunctionalEffect);
            effect?.OnTurnStart(unit, trait.EffectValue);
        }
    }

    /// <summary>攻击检定时应用所有特质</summary>
    public static void ApplyAllOnAttackRoll(BattleUnitModel attacker, BattleUnitModel defender, ref AttackInput input)
    {
        if (attacker.Data.CharacterTraits == null) return;
        foreach (var trait in attacker.Data.CharacterTraits)
        {
            if (trait == null || trait.traitType != TraitData.TraitType.Functional) continue;
            var effect = Get(trait.FunctionalEffect);
            effect?.OnAttackRoll(attacker, defender, ref input, trait.EffectValue);
        }
    }

    /// <summary>受伤时应用所有特质</summary>
    public static void ApplyAllOnDamageTaken(BattleUnitModel unit, ref DamageInput input)
    {
        if (unit.Data.CharacterTraits == null) return;
        foreach (var trait in unit.Data.CharacterTraits)
        {
            if (trait == null || trait.traitType != TraitData.TraitType.Functional) continue;
            var effect = Get(trait.FunctionalEffect);
            effect?.OnDamageTaken(unit, ref input, trait.EffectValue);
        }
    }

    /// <summary>施法时应用所有特质</summary>
    public static void ApplyAllOnSpellCast(BattleUnitModel caster, SpellData spell)
    {
        if (caster.Data.CharacterTraits == null) return;
        foreach (var trait in caster.Data.CharacterTraits)
        {
            if (trait == null || trait.traitType != TraitData.TraitType.Functional) continue;
            var effect = Get(trait.FunctionalEffect);
            effect?.OnSpellCast(caster, spell, trait.EffectValue);
        }
    }

    /// <summary>大地图每日经过时应用所有特质</summary>
    public static void ApplyAllOnPartyDayPass(UnitData unit, PartyContext ctx)
    {
        if (unit.CharacterTraits == null) return;
        foreach (var trait in unit.CharacterTraits)
        {
            if (trait == null || trait.traitType != TraitData.TraitType.Functional) continue;
            var effect = Get(trait.FunctionalEffect);
            effect?.OnPartyDayPass(ctx, trait.EffectValue);
        }
    }

    // ========================================================================
    // 初始化
    // ========================================================================

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        RegisterAll();
    }

    private static void RegisterAll()
    {
        // T06: 注册 11 个特质执行器
        Register(new TraitAlertness());
        Register(new TraitDarkVision());
        Register(new TraitIronStomach());
        Register(new TraitAdaptability());
        Register(new TraitThickSkin());
        Register(new TraitIndomitable());
        Register(new TraitEtherResonance());
        Register(new TraitPremonition());
        Register(new TraitOldWound());
        Register(new TraitGluttony());
        Register(new TraitTimid());
        Register(new TraitXenophobia());
    }
}
