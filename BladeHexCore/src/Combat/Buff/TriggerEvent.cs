namespace BladeHex.Combat.Buff;

public enum TriggerEvent
{
    OnTurnStart,
    OnTurnEnd,
    OnBeforeAttack,
    OnAfterAttack,
    OnBeforeDefend,
    OnAfterDefend,
    OnDealDamage,
    OnTakeDamage,
    OnHeal,
    OnKill,
    OnDeath,
    OnMove,
    OnCastSpell,
    OnBuffApplied,
    OnBuffRemoved,
    OnBuffTick,
}
