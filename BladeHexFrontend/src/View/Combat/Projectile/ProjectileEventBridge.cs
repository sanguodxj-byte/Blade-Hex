// ProjectileEventBridge.cs
// Bridges Core ProjectileSystem events to the frontend EventBus.
using BladeHex.Events;

namespace BladeHex.Combat;

/// <summary>
/// Frontend adapter that publishes Core projectile events to the existing EventBus.
/// Keeps CombatSceneBase focused on composition instead of inline event plumbing.
/// </summary>
public sealed class ProjectileEventBridge
{
    public void Bind(ProjectileSystem system)
    {
        system.ProjectileLaunched += OnProjectileLaunched;
        system.ProjectileImpact += OnProjectileImpact;
    }

    private static void OnProjectileLaunched(Godot.Collections.Dictionary data)
    {
        EventBus.Instance?.Publish(EventBus.Signals.ProjectileLaunched, data);
    }

    private static void OnProjectileImpact(Godot.Collections.Dictionary data)
    {
        EventBus.Instance?.Publish(EventBus.Signals.ProjectileImpact, data);
    }
}
