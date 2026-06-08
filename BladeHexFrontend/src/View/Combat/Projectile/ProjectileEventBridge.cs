// ProjectileEventBridge.cs
// Bridges Core ProjectileSystem events to the frontend EventBus.
using Godot;
using BladeHex.Events;
using BladeHex.Events.Payloads;

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
        // Extract typed data from Core dictionary
        var projData = (Godot.Collections.Dictionary)data["data"];
        var fromWorld = (Vector3)data["from_world"];
        var toWorld = (Vector3)data["to_world"];
        var projectileData = ProjectileData.Deserialize(projData);
        var projectileType = projectileData.ProjectileType;
        var travelTime = data["travel_time"].AsSingle();

        // Dual-publish: typed + string (backward compat)
        EventBus.Instance?.PublishProjectileLaunched(projectileData, fromWorld, toWorld, projectileType, travelTime);
    }

    private static void OnProjectileImpact(Godot.Collections.Dictionary data)
    {
        // Extract typed data from Core dictionary
        var projData = (Godot.Collections.Dictionary)data["data"];
        var projectileType = projData["type"].AsString();
        var damage = projData["damage"].AsInt32();

        // Dual-publish: typed + string (backward compat)
        EventBus.Instance?.PublishProjectileImpact(projectileType, damage);
    }
}
