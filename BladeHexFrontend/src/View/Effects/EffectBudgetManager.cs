using Godot;
using System.Collections.Generic;

namespace BladeHex.View.Effects;

/// <summary>
/// Small runtime budget gate for visual effects. It keeps presentation systems from
/// spawning unlimited short-lived effects during dense combat turns.
/// </summary>
public sealed class EffectBudgetManager
{
    private readonly Dictionary<string, int> _activeByChannel = new();

    public int DefaultChannelLimit { get; set; } = 24;

    public bool TryAcquire(string channel, int limit = -1)
    {
        if (string.IsNullOrEmpty(channel)) channel = "default";
        int max = limit > 0 ? limit : DefaultChannelLimit;
        int active = _activeByChannel.GetValueOrDefault(channel, 0);
        if (active >= max) return false;
        _activeByChannel[channel] = active + 1;
        return true;
    }

    public void Release(string channel)
    {
        if (string.IsNullOrEmpty(channel)) channel = "default";
        int active = _activeByChannel.GetValueOrDefault(channel, 0);
        if (active <= 1) _activeByChannel.Remove(channel);
        else _activeByChannel[channel] = active - 1;
    }

    public void Clear() => _activeByChannel.Clear();
}
