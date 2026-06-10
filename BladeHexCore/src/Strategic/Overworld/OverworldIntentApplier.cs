namespace BladeHex.Strategic;

/// <summary>
/// Applies perception intent to overworld entity state.
/// This keeps Chasing/Fleeing state writes in one module while callers keep
/// ownership of movement/path refresh.
/// </summary>
public static class OverworldIntentApplier
{
    public static bool Apply(
        OverworldEntity entity,
        Intent intent,
        string? chaseSummary = null,
        string? fleeSummary = null)
    {
        if (intent.Target == null)
            return false;

        switch (intent.Type)
        {
            case Intent.IntentType.Chase:
                entity.CurrentAIState = OverworldEntity.AIState.Chasing;
                entity.ChaseTarget = intent.Target;
                entity.CurrentTacticalTarget = intent.Target;
                entity.LastIntentSummary = chaseSummary ?? $"追击 {intent.Target.EntityName}";
                return true;

            case Intent.IntentType.Flee:
                entity.CurrentAIState = OverworldEntity.AIState.Fleeing;
                entity.ChaseTarget = null;
                entity.CurrentTacticalTarget = intent.Target;
                entity.LastIntentSummary = fleeSummary ?? $"逃离 {intent.Target.EntityName}";
                return true;

            default:
                return false;
        }
    }
}
