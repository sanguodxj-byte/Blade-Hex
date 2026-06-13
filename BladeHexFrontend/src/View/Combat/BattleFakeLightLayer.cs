using Godot;
using System.Collections.Generic;
using BladeHex.Combat;
using BladeHex.View.AssetSystem;

namespace BladeHex.View.Combat;

[GlobalClass]
public partial class BattleFakeLightLayer : Node3D
{
    private const string ShaderPath = "res://BladeHexFrontend/src/assets/shaders/battle_fake_light.gdshader";
    private const int MaxPoolSize = 48;
    private const int PrewarmCount = 8;

    public static BattleFakeLightLayer? Instance { get; private set; }

    private NodePool<MeshInstance3D>? _lightPool;
    private readonly List<ActiveFakeLight> _activeLights = new();

    public override void _Ready()
    {
        Instance = this;

        var shader = ShaderAssetResolver.Load("battle_fake_light", ShaderPath);
        if (shader == null)
        {
            GD.PushWarning($"[BattleFakeLightLayer] Missing shader: {ShaderPath}");
            return;
        }

        _lightPool = new NodePool<MeshInstance3D>(
            factory: () => CreateLightMesh(shader),
            onRetrieve: mesh => mesh.Visible = true,
            onReturn: ResetLightMesh,
            maxSize: MaxPoolSize);
        _lightPool.SetParent(this);
        _lightPool.Prewarm(PrewarmCount);
    }

    public override void _ExitTree()
    {
        _lightPool?.Clear();
        _lightPool = null;
        _activeLights.Clear();

        if (Instance == this)
            Instance = null;
    }

    public override void _Process(double delta)
    {
        if (_activeLights.Count == 0)
            return;

        double now = Time.GetTicksMsec() / 1000.0;
        for (int i = _activeLights.Count - 1; i >= 0; i--)
        {
            var active = _activeLights[i];
            float age = (float)(now - active.StartedAt);
            float t = active.Duration <= 0.0f ? 1.0f : Mathf.Clamp(age / active.Duration, 0.0f, 1.0f);

            if (t >= 1.0f)
            {
                _activeLights.RemoveAt(i);
                ReturnLight(active.Mesh);
                continue;
            }

            float fade = 1.0f - t;
            fade *= fade;
            if (active.Mesh.MaterialOverride is ShaderMaterial material)
                material.SetShaderParameter("intensity", active.Intensity * fade);
        }
    }

    public static void PlayBurst(
        Vector3 position,
        Color color,
        float radius = 120.0f,
        float intensity = 0.85f,
        float duration = 0.35f,
        float ringStrength = 0.0f)
    {
        Instance?.SpawnBurst(position, color, radius, intensity, duration, ringStrength);
    }

    public static Vector3 ProjectHitPositionToGround(Vector3 hitPosition)
    {
        float y = hitPosition.Y
            - CombatLayerHeight.CharacterLayer
            - 50.0f
            + CombatLayerHeight.FakeLightLayer;
        return new Vector3(hitPosition.X, y, hitPosition.Z);
    }

    private void SpawnBurst(
        Vector3 position,
        Color color,
        float radius,
        float intensity,
        float duration,
        float ringStrength)
    {
        if (_lightPool == null)
            return;

        var mesh = _lightPool.Retrieve();
        mesh.Position = position;
        mesh.Scale = new Vector3(radius * 2.0f, radius * 2.0f, 1.0f);

        if (mesh.MaterialOverride is ShaderMaterial material)
        {
            material.SetShaderParameter("light_color", color);
            material.SetShaderParameter("intensity", intensity);
            material.SetShaderParameter("ring_strength", ringStrength);
        }

        _activeLights.Add(new ActiveFakeLight(mesh, Time.GetTicksMsec() / 1000.0, duration, intensity));
    }

    private static MeshInstance3D CreateLightMesh(Shader shader)
    {
        var material = new ShaderMaterial
        {
            Shader = shader,
            RenderPriority = 3,
        };

        return new MeshInstance3D
        {
            Name = "FakeLight",
            Mesh = new QuadMesh { Size = Vector2.One },
            RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = material,
            Visible = false,
        };
    }

    private static void ResetLightMesh(MeshInstance3D mesh)
    {
        mesh.Visible = false;
        mesh.Position = Vector3.Zero;
        mesh.Scale = Vector3.One;

        if (mesh.MaterialOverride is ShaderMaterial material)
            material.SetShaderParameter("intensity", 0.0f);
    }

    private void ReturnLight(MeshInstance3D mesh)
    {
        if (!GodotObject.IsInstanceValid(mesh))
            return;

        _lightPool?.Return(mesh);
    }

    private readonly struct ActiveFakeLight
    {
        public readonly MeshInstance3D Mesh;
        public readonly double StartedAt;
        public readonly float Duration;
        public readonly float Intensity;

        public ActiveFakeLight(MeshInstance3D mesh, double startedAt, float duration, float intensity)
        {
            Mesh = mesh;
            StartedAt = startedAt;
            Duration = duration;
            Intensity = intensity;
        }
    }
}
