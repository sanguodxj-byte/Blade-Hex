using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.View.Map;
using BladeHex.Strategic;
using BladeHex.Data;
using BladeHex.Scenes.Overworld2d;
using BladeHex.Scenes.Overworld2d.Components;

namespace BladeHex.Tests.ShaderTests;

/// <summary>
/// 大地图及着色器环境系统专属单元测试
/// </summary>
public static class ShaderSystemTests
{
    public static bool TestDayNightController()
    {
        var controller = new DayNightController2D();
        var modulate = new CanvasModulate();
        controller.Initialize(modulate);

        // 测试半夜 00:00 (应为 NightBlue: 0.4, 0.4, 0.6)
        controller.Tick(0.0f);
        if (Mathf.Abs(modulate.Color.R - 0.40f) > 0.01f || 
            Mathf.Abs(modulate.Color.G - 0.40f) > 0.01f || 
            Mathf.Abs(modulate.Color.B - 0.60f) > 0.01f)
            return false;

        // 测试正午 12:00 (应为 DayWhite: 1.0, 1.0, 1.0)
        controller.Tick(12.0f);
        if (Mathf.Abs(modulate.Color.R - 1.0f) > 0.01f || 
            Mathf.Abs(modulate.Color.G - 1.0f) > 0.01f || 
            Mathf.Abs(modulate.Color.B - 1.0f) > 0.01f)
            return false;

        return true;
    }

    public static bool TestNightLightingController(Node testNode)
    {
        var controller = new NightLightingController2D();
        testNode.AddChild(controller);

        var pois = new List<OverworldPOI>();
        float hour = 12.0f; // 白天
        controller.Initialize(pois, null, () => Vector2.Zero, () => 4, () => hour, null, null);

        // 1. 白天 Tick()，发光根节点应该不显示
        controller.Tick();
        var glowRoot = controller.GetNodeOrNull<Node2D>("NightGlowSprites");
        if (glowRoot == null || glowRoot.Visible) 
        {
            controller.QueueFree();
            return false;
        }

        // 2. 更改时间为半夜 00:00，Tick()，发光根节点显示，且有玩家光源
        hour = 0.0f;
        controller.Tick();
        if (!glowRoot.Visible) 
        {
            controller.QueueFree();
            return false;
        }

        // 玩家光源应该是第一个 glow sprite
        var firstGlow = glowRoot.GetNodeOrNull<Sprite2D>("NightGlow_00");
        if (firstGlow == null || !firstGlow.Visible || firstGlow.Modulate.A <= 0.0f) 
        {
            controller.QueueFree();
            return false;
        }

        // 3. 验证是否加载了 ShaderMaterial
        if (firstGlow.Material is not ShaderMaterial shaderMat) 
        {
            controller.QueueFree();
            return false;
        }
        if (shaderMat.Shader == null) 
        {
            controller.QueueFree();
            return false;
        }

        controller.QueueFree();
        return true;
    }

    public static bool TestFogOverlayShader(Node testNode)
    {
        var fogData = new FogOfWar();
        fogData.Initialize(1000, 1000, 100); // 网格大小为 10x10

        var overlay = new FogOverlay2D();
        overlay.Initialize(fogData, 1000f, 1000f);
        testNode.AddChild(overlay);

        // 验证子节点 Sprite2D
        var sprite = overlay.GetNodeOrNull<Sprite2D>("FogSprite");
        if (sprite == null) 
        {
            overlay.QueueFree();
            return false;
        }

        // 验证材质是否为加载的 ShaderMaterial
        if (sprite.Material is not ShaderMaterial shaderMat) 
        {
            overlay.QueueFree();
            return false;
        }
        if (shaderMat.Shader == null) 
        {
            overlay.QueueFree();
            return false;
        }

        // 验证纹理大小
        var maskTex = overlay.GetFogMaskTexture();
        if (maskTex == null || maskTex.GetSize() != new Vector2(10, 10)) 
        {
            overlay.QueueFree();
            return false;
        }

        overlay.QueueFree();
        return true;
    }
}
