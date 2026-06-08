using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Audio;
using BladeHex.Combat.Skills;

namespace BladeHex.View.Effects;

public enum EffectTrackType
{
    SkillVfx,
    Sfx,
    CameraShake,
}

public readonly record struct EffectTrack(
    EffectTrackType Type,
    float Delay,
    string Key,
    Vector3 Position,
    float Amount = 1.0f,
    float Duration = 0.18f,
    int BudgetLimit = -1);

/// <summary>
/// Scene-local presentation orchestrator. Gameplay code submits semantic cues;
/// this node sequences VFX, SFX and camera feedback with a small runtime budget.
/// </summary>
[GlobalClass]
public partial class EffectOrchestrator : Node
{
    public EffectBudgetManager Budget { get; } = new();

    public override void _ExitTree()
    {
        Budget.Clear();
    }

    public void PlaySkillCast(
        Node parent,
        Vector3 targetWorldPosition,
        string skillId,
        AudioManager? audioManager)
    {
        if (string.IsNullOrEmpty(skillId) || parent == null) return;

        var config = SkillVfxExecutor.GetConfig(skillId);
        var tracks = new List<EffectTrack>(3);

        if (!string.IsNullOrEmpty(config.SfxName))
            tracks.Add(new EffectTrack(EffectTrackType.Sfx, 0.0f, config.SfxName, targetWorldPosition));

        tracks.Add(new EffectTrack(EffectTrackType.SkillVfx, 0.03f, skillId, targetWorldPosition, BudgetLimit: 18));

        if (config.VfxCategory is SkillVfxExecutor.VfxCategory.Explosion or SkillVfxExecutor.VfxCategory.Arcane)
            tracks.Add(new EffectTrack(EffectTrackType.CameraShake, 0.05f, "", targetWorldPosition, 0.18f, 0.14f, 4));

        Play(parent, tracks, audioManager);
    }

    public void Play(Node parent, IReadOnlyList<EffectTrack> tracks, AudioManager? audioManager = null)
    {
        for (int i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            if (track.Delay <= 0.001f)
                ExecuteTrack(parent, track, audioManager);
            else
                RunDelayed(parent, track, audioManager);
        }
    }

    private async void RunDelayed(Node parent, EffectTrack track, AudioManager? audioManager)
    {
        if (!GodotObject.IsInstanceValid(parent)) return;
        await ToSignal(GetTree().CreateTimer(track.Delay), SceneTreeTimer.SignalName.Timeout);
        if (GodotObject.IsInstanceValid(parent))
            ExecuteTrack(parent, track, audioManager);
    }

    private void ExecuteTrack(Node parent, EffectTrack track, AudioManager? audioManager)
    {
        string channel = track.Type.ToString();
        if (!Budget.TryAcquire(channel, track.BudgetLimit)) return;

        try
        {
            switch (track.Type)
            {
                case EffectTrackType.SkillVfx:
                    SkillVfxExecutor.ExecuteVfx(parent, track.Position, track.Key);
                    break;
                case EffectTrackType.Sfx:
                    audioManager?.PlaySfxName(track.Key);
                    break;
                case EffectTrackType.CameraShake:
                    TryPlayCameraShake(parent, track.Amount, track.Duration);
                    break;
            }
        }
        finally
        {
            ReleaseLater(channel, Math.Max(track.Duration, 0.25f));
        }
    }

    private async void ReleaseLater(string channel, float delay)
    {
        await ToSignal(GetTree().CreateTimer(delay), SceneTreeTimer.SignalName.Timeout);
        Budget.Release(channel);
    }

    private static void TryPlayCameraShake(Node parent, float amount, float duration)
    {
        var camera = parent.GetViewport()?.GetCamera3D();
        if (camera == null) return;

        var tween = camera.CreateTween();
        var start = camera.HOffset;
        tween.TweenProperty(camera, "h_offset", start + amount, duration * 0.25f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(camera, "h_offset", start - amount, duration * 0.35f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(camera, "h_offset", start, duration * 0.40f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
    }
}
