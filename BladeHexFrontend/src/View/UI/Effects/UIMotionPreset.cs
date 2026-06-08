using Godot;

namespace BladeHex.View.UI.Effects;

public enum UIMotionPreset
{
    PanelEnter,
    PanelExit,
    ButtonPress,
    RewardPop,
    ValuePulse,
}

public readonly record struct UIMotionSpec(
    float Duration,
    float AlphaFrom,
    float AlphaTo,
    Vector2 ScaleFrom,
    Vector2 ScaleTo,
    Vector2 OffsetFrom,
    Vector2 OffsetTo,
    Tween.TransitionType Transition,
    Tween.EaseType Ease)
{
    public static UIMotionSpec FromPreset(UIMotionPreset preset)
    {
        return preset switch
        {
            UIMotionPreset.PanelExit => new(0.16f, 1f, 0f, Vector2.One, new Vector2(0.98f, 0.98f), Vector2.Zero, new Vector2(0, 12), Tween.TransitionType.Quad, Tween.EaseType.In),
            UIMotionPreset.ButtonPress => new(0.08f, 1f, 1f, Vector2.One, new Vector2(0.94f, 0.94f), Vector2.Zero, Vector2.Zero, Tween.TransitionType.Sine, Tween.EaseType.Out),
            UIMotionPreset.RewardPop => new(0.22f, 0f, 1f, new Vector2(0.84f, 0.84f), Vector2.One, Vector2.Zero, Vector2.Zero, Tween.TransitionType.Back, Tween.EaseType.Out),
            UIMotionPreset.ValuePulse => new(0.18f, 1f, 1f, Vector2.One, new Vector2(1.08f, 1.08f), Vector2.Zero, Vector2.Zero, Tween.TransitionType.Sine, Tween.EaseType.Out),
            _ => new(0.20f, 0f, 1f, new Vector2(0.98f, 0.98f), Vector2.One, new Vector2(0, 12), Vector2.Zero, Tween.TransitionType.Cubic, Tween.EaseType.Out),
        };
    }
}
