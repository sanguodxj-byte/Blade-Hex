using Godot;

namespace BladeHex.View.UI.Effects;

public static class UIMotionPlayer
{
    public static Tween? Play(Control control, UIMotionPreset preset)
    {
        if (control == null || !GodotObject.IsInstanceValid(control)) return null;
        return Play(control, UIMotionSpec.FromPreset(preset));
    }

    public static Tween Play(Control control, UIMotionSpec spec)
    {
        var startPos = control.Position;
        control.PivotOffset = control.Size * 0.5f;
        control.Position = startPos + spec.OffsetFrom;
        control.Scale = spec.ScaleFrom;
        control.Modulate = new Color(control.Modulate.R, control.Modulate.G, control.Modulate.B, spec.AlphaFrom);

        var tween = control.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(control, "position", startPos + spec.OffsetTo, spec.Duration)
            .SetTrans(spec.Transition)
            .SetEase(spec.Ease);
        tween.TweenProperty(control, "scale", spec.ScaleTo, spec.Duration)
            .SetTrans(spec.Transition)
            .SetEase(spec.Ease);
        tween.TweenProperty(control, "modulate:a", spec.AlphaTo, spec.Duration * 0.75f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        return tween;
    }
}
