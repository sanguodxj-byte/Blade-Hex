// DamageNumberPopup.cs
// 伤害数字弹出 — Label3D 浮动 + tween 上飘 + 淡出。
// 设计:无状态静态工厂,挂到 unit 父节点(Combat scene)上,自然受相机变换;
//      Billboard 朝向相机,字号大小由 PixelSize 决定。
//
// 用法:
//   DamageNumberPopup.Spawn(this, target.Position + Vector3.Up * 100, 23, isCritical: true);
using System;
using Godot;

namespace BladeHex.View.Combat;

public static class DamageNumberPopup
{
    private const float DefaultPixelSize = 1.5f;       // 字号
    private const float CritPixelSize = 2.2f;
    private const float FloatHeight = 80f;             // 上升世界单位
    private const float FloatDurationSec = 1.0f;       // 总时长(此值会被 CombatSpeed 缩放)
    private const float FontSize = 36f;

    /// <summary>
    /// 在世界位置弹出一个伤害数字,自动上飘并消失。
    /// </summary>
    /// <param name="parent">挂载父节点(战斗场景根)</param>
    /// <param name="worldPos">起始位置(通常单位头顶)</param>
    /// <param name="amount">数值;>0 显示为伤害(红/黄),≤0 显示为治疗(绿)</param>
    /// <param name="isCritical">是否暴击 — 字号更大、加 ! 标记</param>
    /// <param name="missLabel">非空时显示该字符串(如 "Miss" / "Block"),amount 被忽略</param>
    public static void Spawn(Node3D parent, Vector3 worldPos, int amount,
        bool isCritical = false, string? missLabel = null)
    {
        if (parent == null || !GodotObject.IsInstanceValid(parent)) return;

        var label = new Label3D();
        label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        label.NoDepthTest = true;
        label.PixelSize = isCritical ? CritPixelSize : DefaultPixelSize;
        label.FontSize = (int)FontSize;
        label.OutlineSize = 6;
        label.OutlineModulate = new Color(0, 0, 0, 0.9f);
        label.RenderPriority = 20;

        // 文本与颜色
        if (!string.IsNullOrEmpty(missLabel))
        {
            label.Text = missLabel;
            label.Modulate = new Color(0.7f, 0.7f, 0.7f);
        }
        else if (amount > 0)
        {
            label.Text = isCritical ? $"{amount}!" : amount.ToString();
            label.Modulate = isCritical
                ? new Color(1.0f, 0.85f, 0.25f)  // 暴击:金黄
                : new Color(1.0f, 0.4f, 0.3f);   // 普通:红
        }
        else
        {
            label.Text = $"+{-amount}";
            label.Modulate = new Color(0.4f, 1.0f, 0.4f); // 治疗:绿
        }

        label.Position = worldPos;
        parent.AddChild(label);

        // 动画:上飘 + 淡出。时长用 CombatSpeed 缩放,跟其它战斗动画节奏一致。
        double dur = CombatSpeed.ScaleSeconds(FloatDurationSec);
        var tween = label.CreateTween().SetParallel(true);
        tween.TweenProperty(label, "position", worldPos + Vector3.Up * FloatHeight, dur)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

        // 淡出在最后 40% 时间内
        var fadeTween = label.CreateTween();
        fadeTween.TweenInterval(dur * 0.6f);
        fadeTween.TweenProperty(label, "modulate:a", 0f, dur * 0.4f);
        fadeTween.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(label)) label.QueueFree();
        }));
    }

    /// <summary>便捷重载:从 Unit 上方弹出</summary>
    public static void SpawnAtUnit(Node3D parent, Node3D unit, int amount,
        bool isCritical = false, string? missLabel = null)
    {
        if (unit == null || !GodotObject.IsInstanceValid(unit)) return;
        Spawn(parent, unit.GlobalPosition + Vector3.Up * 90f, amount, isCritical, missLabel);
    }
}
