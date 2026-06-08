// MonsterTraitRegistry.cs
// 单位特质注册表 — 静态注册器
// T09: MonsterTraitRegistry + 5 trait MVP
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Combat.Traits;  // For AttackInput, DamageInput

namespace BladeHex.Combat.MonsterTraits;

/// <summary>
/// 单位特质注册表 — 所有单位特质的中央注册器
/// 模式参照 TraitRegistry / BuffRegistry
/// </summary>
public static class MonsterTraitRegistry
{
    private static readonly Dictionary<string, IMonsterTrait> _traits = new();
    private static bool _initialized;

    /// <summary>获取单位特质</summary>
    public static IMonsterTrait? Get(string traitId)
    {
        EnsureInitialized();
        return _traits.GetValueOrDefault(traitId);
    }

    /// <summary>获取所有已注册特质</summary>
    public static IReadOnlyDictionary<string, IMonsterTrait> GetAll()
    {
        EnsureInitialized();
        return _traits;
    }

    /// <summary>运行时注册自定义单位特质（mod 支持）</summary>
    public static void Register(IMonsterTrait trait)
    {
        _traits[trait.TraitId] = trait;
    }

    // ========================================================================
    // 批量分发方法
    // ========================================================================

    /// <summary>单位创建时应用所有单位特质</summary>
    public static void ApplyAll(UnitData u)
    {
        if (u.Traits == null) return;
        foreach (var traitId in u.Traits)
        {
            if (string.IsNullOrEmpty(traitId)) continue;
            var trait = Get(traitId);
            trait?.OnUnitCreated(u);
        }
    }

    /// <summary>回合开始时应用所有单位特质</summary>
    public static void ApplyAllOnTurnStart(BattleUnitModel unit)
    {
        if (unit.Data.Traits == null) return;
        foreach (var traitId in unit.Data.Traits)
        {
            if (string.IsNullOrEmpty(traitId)) continue;
            var trait = Get(traitId);
            trait?.OnTurnStart(unit);
        }
    }

    /// <summary>攻击检定时应用所有单位特质</summary>
    public static void ApplyAllOnAttackRoll(BattleUnitModel attacker, BattleUnitModel defender, ref AttackInput input)
    {
        if (attacker.Data.Traits == null) return;
        foreach (var traitId in attacker.Data.Traits)
        {
            if (string.IsNullOrEmpty(traitId)) continue;
            var trait = Get(traitId);
            trait?.OnAttackRoll(attacker, defender, ref input);
        }
    }

    /// <summary>受伤时应用所有单位特质</summary>
    public static void ApplyAllOnDamageTaken(BattleUnitModel unit, ref DamageInput input)
    {
        if (unit.Data.Traits == null) return;
        foreach (var traitId in unit.Data.Traits)
        {
            if (string.IsNullOrEmpty(traitId)) continue;
            var trait = Get(traitId);
            trait?.OnDamageTaken(unit, ref input);
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
        // T09: 注册 5 个单位特质 MVP
        Register(new UndeadResilience());
        Register(new FlyingTrait());
        Register(new PackTactics());
        Register(new FearAura());
        Register(new Regeneration());
    }
}
