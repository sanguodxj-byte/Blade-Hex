using Godot;

namespace BladeHex.Combat;

/// <summary>
/// Frontend 层 Buff 回合 tick 应用器。
/// Core 的 BuffTurnHooks 只计算/更新 Buff 状态；本组件负责把 HP delta 应用到 Unit 视图对象并触发回合事件。
/// </summary>
public static class BuffTurnApplier
{
    public static void ApplyTurnStart(Unit? unit)
    {
        if (unit?.Data == null || unit.CurrentHp <= 0) return;

        var tick = BladeHex.Combat.Buff.BuffTurnHooks.TickTurnStart(unit.Data);
        ApplyHpDelta(unit, tick.NetHpDelta);
        int bloodOathLoss = BladeHex.Strategic.SkillTreeKeystoneResolver.ApplyBloodOathTurnStartLoss(unit.Data);
        if (bloodOathLoss > 0)
        {
            unit.CurrentHp = unit.Data.Runtime.CurrentHp;
            unit.Model.CurrentHp = unit.CurrentHp;
            BladeHex.Events.EventBus.Instance?.PublishUnitDamaged(unit, bloodOathLoss, unit.CurrentHp);
            unit.UpdateHpBar();
        }
        BladeHex.Combat.Buff.BuffSystem.FireTriggers(unit.Data, BladeHex.Combat.Buff.TriggerEvent.OnTurnStart);
        CharacterRenderBus.Instance?.NotifyStatusEffects(unit, new Godot.Collections.Array());
    }

    private static void ApplyHpDelta(Unit unit, int netHpDelta)
    {
        if (netHpDelta > 0)
            unit.TakeDamage(netHpDelta);
        else if (netHpDelta < 0)
            unit.Heal(-netHpDelta);
    }
}
