using BladeHex.View.AssetSystem;
using Godot;

namespace BladeHex.View.UI;

public partial class ParchmentButtonEffect
{
    private const string ParchmentShaderId = "parchment_button";
    private const string ParchmentShaderPath = "res://BladeHexFrontend/src/View/UI/Shaders/parchment_button.gdshader";

    private readonly Control _button;
    private readonly ShaderMaterial _material;

    private static readonly Shader? ParchmentShader =
        ShaderAssetResolver.Load(ParchmentShaderId, ParchmentShaderPath);

    public ParchmentButtonEffect(Control button, Color? glowColor = null)
    {
        _button = button;
        _material = new ShaderMaterial { Shader = ParchmentShader };

        if (glowColor.HasValue)
            _material.SetShaderParameter("glow_color", glowColor.Value);

        _button.Material = _material;

        if (_button is BaseButton baseButton)
        {
            baseButton.MouseEntered += OnHover;
            baseButton.MouseExited += OnNormal;
            baseButton.ButtonDown += OnPressed;
            baseButton.ButtonUp += OnHover;
        }
        else
        {
            _button.MouseEntered += OnHover;
            _button.MouseExited += OnNormal;
        }
    }

    public void OnNormal() => _material.SetShaderParameter("state", 0.0f);
    public void OnHover() => _material.SetShaderParameter("state", 1.0f);
    public void OnPressed() => _material.SetShaderParameter("state", 2.0f);

    public void SetGlowColor(Color color)
    {
        _material.SetShaderParameter("glow_color", color);
    }

    public void Detach()
    {
        _button.Material = null;
        if (_button is BaseButton baseButton)
        {
            baseButton.MouseEntered -= OnHover;
            baseButton.MouseExited -= OnNormal;
            baseButton.ButtonDown -= OnPressed;
            baseButton.ButtonUp -= OnHover;
        }
    }
}
