// ParchmentButtonEffect.cs
// 给任意 Button/TextureButton 挂载羊皮纸按钮Shader效果
// 用法: var effect = new ParchmentButtonEffect(myButton);
using Godot;

namespace BladeHex.View.UI;

/// <summary>
/// 将 parchment_button.gdshader 应用到按钮上，自动处理 hover/pressed 状态切换。
/// 支持 Button 和 TextureButton。
/// </summary>
public partial class ParchmentButtonEffect
{
    private readonly Control _button;
    private readonly ShaderMaterial _material;

    private static readonly Shader ParchmentShader = GD.Load<Shader>(
        "res://BladeHexFrontend/src/View/UI/Shaders/parchment_button.gdshader");

    /// <summary>创建并绑定效果到按钮</summary>
    public ParchmentButtonEffect(Control button, Color? glowColor = null)
    {
        _button = button;
        _material = new ShaderMaterial { Shader = ParchmentShader };

        if (glowColor.HasValue)
            _material.SetShaderParameter("glow_color", glowColor.Value);

        _button.Material = _material;

        // 绑定信号
        if (_button is BaseButton baseBtn)
        {
            baseBtn.MouseEntered += OnHover;
            baseBtn.MouseExited += OnNormal;
            baseBtn.ButtonDown += OnPressed;
            baseBtn.ButtonUp += OnHover; // 松开后如果还在按钮上就是hover
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

    /// <summary>手动设置发光颜色</summary>
    public void SetGlowColor(Color color) =>
        _material.SetShaderParameter("glow_color", color);

    /// <summary>解除绑定</summary>
    public void Detach()
    {
        _button.Material = null;
        if (_button is BaseButton baseBtn)
        {
            baseBtn.MouseEntered -= OnHover;
            baseBtn.MouseExited -= OnNormal;
            baseBtn.ButtonDown -= OnPressed;
            baseBtn.ButtonUp -= OnHover;
        }
    }
}
