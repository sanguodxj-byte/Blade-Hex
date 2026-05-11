using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Combat;

/// <summary>
/// 战术战斗特效管理器 (静态工具)
/// </summary>
public partial class VFXManager : Node
{
    private static readonly Dictionary<string, Color> VfxColors = new()
    {
        { "melee_combo", new Color(1.0f, 0.6f, 0.1f) },
        { "whirlwind", new Color(0.9f, 0.3f, 0.1f) },
        { "shield_bash", new Color(0.7f, 0.7f, 0.8f) },
        { "blood_vortex", new Color(0.8f, 0.1f, 0.1f) },
        { "poison_blade", new Color(0.2f, 0.8f, 0.2f) },
        { "aimed_shot", new Color(1.0f, 1.0f, 0.3f) },
        { "double_shot", new Color(0.9f, 0.9f, 0.3f) },
        { "scatter_shot", new Color(0.9f, 0.7f, 0.2f) },
        { "trick_arrow", new Color(0.5f, 0.2f, 1.0f) },
        { "mana_shield", new Color(0.3f, 0.5f, 1.0f) },
        { "time_warp", new Color(0.6f, 0.2f, 0.9f) },
        { "holy_judgment", new Color(1.0f, 1.0f, 0.6f) },
        { "nature_wrath", new Color(0.3f, 0.7f, 0.2f) },
        { "heal", new Color(0.3f, 1.0f, 0.5f) },
        { "mass_heal", new Color(0.3f, 1.0f, 0.5f) },
        { "holy_shield", new Color(0.8f, 0.9f, 1.0f) },
        { "blessing", new Color(1.0f, 1.0f, 0.8f) },
        { "war_cry", new Color(1.0f, 0.4f, 0.1f) },
        { "stealth", new Color(0.3f, 0.3f, 0.4f) },
        { "shadow_clone", new Color(0.4f, 0.3f, 0.7f) },
        { "taunt", new Color(1.0f, 0.3f, 0.0f) },
        { "bulwark", new Color(0.6f, 0.7f, 0.9f) },
        { "rally", new Color(1.0f, 0.9f, 0.3f) },
        { "intimidate", new Color(0.6f, 0.0f, 0.2f) },
        { "heroic_call", new Color(1.0f, 0.85f, 0.3f) },
        { "inspire", new Color(1.0f, 0.9f, 0.5f) },
        { "dispel", new Color(0.9f, 0.9f, 1.0f) }
    };

    private static readonly Dictionary<string, int> VfxParticleCount = new()
    {
        { "whirlwind", 40 },
        { "blood_vortex", 35 },
        { "scatter_shot", 30 },
        { "mass_heal", 25 },
        { "heroic_call", 30 },
        { "inspire", 20 }
    };

    public static void PlayHitEffect(Node parent, Vector3 pos)
    {
        var particles = CreateParticles(parent, pos + new Vector3(0, 50, 0), 15, 0.4f, new Color(1, 0.8f, 0.2f));
        AutoDestroy(parent, particles, 1.0f);
    }

    public static void PlayDeathEffect(Node parent, Vector3 pos)
    {
        var particles = CreateParticles(parent, pos, 30, 0.8f, new Color(0.3f, 0.3f, 0.3f, 0.6f));
        if (particles.ProcessMaterial is ParticleProcessMaterial mat)
        {
            mat.Gravity = new Vector3(0, 50, 0);
            mat.ScaleMin = 5.0f;
            mat.ScaleMax = 15.0f;
        }
        AutoDestroy(parent, particles, 1.2f);
    }

    public static void PlayExplosionEffect(Node parent, Vector3 pos)
    {
        var particles = CreateParticles(parent, pos + new Vector3(0, 10, 0), 50, 0.6f, new Color(1.0f, 0.4f, 0.1f));
        if (particles.ProcessMaterial is ParticleProcessMaterial mat)
        {
            mat.InitialVelocityMin = 150.0f;
            mat.InitialVelocityMax = 300.0f;
            mat.ScaleMin = 10.0f;
            mat.ScaleMax = 25.0f;
        }
        AutoDestroy(parent, particles, 1.0f);
    }

    public static void PlaySkillVfx(Node parent, Vector3 pos, string vfxType)
    {
        if (string.IsNullOrEmpty(vfxType)) return;

        Color color = VfxColors.GetValueOrDefault(vfxType, Colors.White);
        int count = VfxParticleCount.GetValueOrDefault(vfxType, 20);

        switch (vfxType)
        {
            case "heal":
            case "mass_heal":
                PlayHealVfx(parent, pos, color, count);
                break;
            case "holy_shield":
            case "mana_shield":
            case "bulwark":
            case "blessing":
                PlayShieldVfx(parent, pos, color, count);
                break;
            default:
                var particles = CreateParticles(parent, pos + new Vector3(0, 50, 0), count, 0.6f, color);
                AutoDestroy(parent, particles, 1.0f);
                break;
        }
    }

    private static void PlayHealVfx(Node parent, Vector3 pos, Color color, int count)
    {
        var particles = CreateParticles(parent, pos + new Vector3(0, 30, 0), count, 0.8f, color);
        if (particles.ProcessMaterial is ParticleProcessMaterial mat)
        {
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 30.0f;
            mat.InitialVelocityMin = 60.0f;
            mat.InitialVelocityMax = 120.0f;
            mat.Gravity = new Vector3(0, -30, 0);
        }
        AutoDestroy(parent, particles, 1.2f);
    }

    private static void PlayShieldVfx(Node parent, Vector3 pos, Color color, int count)
    {
        var particles = CreateParticles(parent, pos + new Vector3(0, 50, 0), count, 1.0f, color);
        if (particles.ProcessMaterial is ParticleProcessMaterial mat)
        {
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 60.0f;
            mat.InitialVelocityMin = 30.0f;
            mat.InitialVelocityMax = 60.0f;
            mat.Gravity = new Vector3(0, -20, 0);
        }
        AutoDestroy(parent, particles, 1.5f);
    }

    private static GpuParticles3D CreateParticles(Node parent, Vector3 pos, int amount, float lifetime, Color color)
    {
        var particles = new GpuParticles3D();
        parent.AddChild(particles);
        particles.GlobalPosition = pos;
        particles.Emitting = true;
        particles.OneShot = true;
        particles.Amount = amount;
        particles.Explosiveness = 0.9f;
        particles.Lifetime = lifetime;

        var mat = new ParticleProcessMaterial
        {
            Spread = 180.0f,
            InitialVelocityMin = 80.0f,
            InitialVelocityMax = 160.0f,
            Gravity = new Vector3(0, -300, 0),
            ScaleMin = 3.0f,
            ScaleMax = 8.0f,
            Color = color
        };
        particles.ProcessMaterial = mat;

        var mesh = new QuadMesh { Size = new Vector2(5, 5) };
        var drawMat = new StandardMaterial3D
        {
            ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = StandardMaterial3D.BillboardModeEnum.Enabled,
            Transparency = StandardMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = color
        };
        particles.DrawPass1 = mesh;
        particles.MaterialOverride = drawMat;

        return particles;
    }

    private static void AutoDestroy(Node parent, GpuParticles3D particles, float delay)
    {
        parent.GetTree().CreateTimer(delay).Timeout += () => particles.QueueFree();
    }
}
