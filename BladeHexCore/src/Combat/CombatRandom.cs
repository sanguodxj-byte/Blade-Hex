// CombatRandom.cs
// Ambient random source for combat math.
//
// Production code (Godot scenes) keeps using GodotRandomSource.Default — same
// behaviour as before. Headless simulation / tests temporarily swap in a
// SeededRandomSource via Use(...) for deterministic replay.
//
// This avoids plumbing IRandomSource through every existing call site of
// CombatRuleEngine / RPGRuleEngine while still allowing controlled randomness
// where it matters.
using System;

namespace BladeHex.Combat;

/// <summary>
/// Ambient random source for combat-rule calculations. Defaults to
/// <see cref="GodotRandomSource.Default"/>.
/// </summary>
public static class CombatRandom
{
    private static IRandomSource _current = GodotRandomSource.Default;

    /// <summary>Currently active random source.</summary>
    public static IRandomSource Current => _current;

    /// <summary>
    /// Temporarily install <paramref name="source"/> as the ambient random.
    /// Dispose the returned scope to restore the previous source.
    /// </summary>
    public static Scope Use(IRandomSource source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        var prev = _current;
        _current = source;
        return new Scope(prev);
    }

    public readonly struct Scope : IDisposable
    {
        private readonly IRandomSource _previous;
        internal Scope(IRandomSource previous) { _previous = previous; }
        public void Dispose() { _current = _previous; }
    }

    // Convenience helpers — callers can use these instead of touching IRandomSource directly.
    public static int RollD20() => _current.RollD20();
    public static int RollDice(int count, int sides) => _current.RollDice(count, sides);
    public static int RandRange(int minInclusive, int maxInclusive) => _current.RandRange(minInclusive, maxInclusive);
}
