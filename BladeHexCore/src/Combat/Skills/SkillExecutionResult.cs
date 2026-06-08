// SkillExecutionResult.cs
// 技能执行结果的强类型表示，替代 Godot.Collections.Dictionary。
// 定义在 Core 层，使用 BattleUnitModel（纯数据，不依赖 Godot Node）。
using System;
using System.Collections.Generic;
using Godot;
using BladeHex.Data;

namespace BladeHex.Combat.Skills;

/// <summary>技能执行结果（强类型）。</summary>
public sealed record SkillExecutionResult
{
    public bool Success { get; init; }
    public string? FailureReason { get; init; }
    public IReadOnlyList<SkillSubResult> SubResults { get; init; } = Array.Empty<SkillSubResult>();
    public IReadOnlyList<StatusEffectApplication> StatusEffects { get; init; } = Array.Empty<StatusEffectApplication>();

    /// <summary>失败构造器。</summary>
    public static SkillExecutionResult Fail(string reason) => new()
    {
        Success = false,
        FailureReason = reason
    };

    /// <summary>成功构造器。</summary>
    public static SkillExecutionResult Ok(params SkillSubResult[] results) => new()
    {
        Success = true,
        SubResults = results
    };

    // ========================================================================
    // 向后兼容：转换为 Dictionary（供旧代码/Signal 使用）
    // ========================================================================

    /// <summary>转换为 Godot.Collections.Dictionary（向后兼容旧 API）。</summary>
    public Godot.Collections.Dictionary ToDictionary()
    {
        var dict = new Godot.Collections.Dictionary
        {
            { "success", Success },
        };

        if (!Success && FailureReason != null)
            dict["reason"] = FailureReason;

        if (Success)
        {
            var resultsArray = new Godot.Collections.Array();
            var statusArray = new Godot.Collections.Array();

            foreach (var sub in SubResults)
            {
                switch (sub)
                {
                    case DamageEvent dmg:
                        resultsArray.Add(new Godot.Collections.Dictionary
                        {
                            { "type", "damage" },
                            { "target_id", dmg.Target?.Data?.CharacterId.ToString() ?? "" },
                            { "damage", dmg.Damage },
                            { "is_critical", dmg.IsCritical },
                            { "killing_blow", dmg.WasKillingBlow },
                        });
                        break;
                    case HealEvent heal:
                        resultsArray.Add(new Godot.Collections.Dictionary
                        {
                            { "type", "heal" },
                            { "target_id", heal.Target?.Data?.CharacterId.ToString() ?? "" },
                            { "amount", heal.Amount },
                        });
                        break;
                    case TeleportEvent tp:
                        var tpDict = new Godot.Collections.Dictionary
                        {
                            { "type", "teleport" },
                            { "unit_id", tp.Unit?.Data?.CharacterId.ToString() ?? "" },
                            { "destination", new Godot.Collections.Dictionary
                            {
                                { "x", tp.Destination.X },
                                { "y", tp.Destination.Y },
                            }},
                        };
                        if (tp.PreviousPosition.HasValue)
                            tpDict["previous_position"] = new Godot.Collections.Dictionary
                            {
                                { "x", tp.PreviousPosition.Value.X },
                                { "y", tp.PreviousPosition.Value.Y },
                            };
                        resultsArray.Add(tpDict);
                        break;
                    case StatusEffectApplication se:
                        statusArray.Add(new Godot.Collections.Dictionary
                        {
                            { "effect_id", se.EffectId },
                            { "target_id", se.Target?.Data?.CharacterId.ToString() ?? "" },
                            { "duration", se.Duration },
                            { "special", se.Special.ToString() },
                        });
                        break;
                    case BuffApplication buff:
                        resultsArray.Add(new Godot.Collections.Dictionary
                        {
                            { "type", "buff" },
                            { "buff_id", buff.BuffId },
                            { "target_id", buff.Target?.Data?.CharacterId.ToString() ?? "" },
                        });
                        break;
                    case BattleAnchorEvent anchor:
                        resultsArray.Add(new Godot.Collections.Dictionary
                        {
                            { "type", "battle_anchor" },
                            { "anchor_id", anchor.AnchorId },
                            { "source", anchor.Source },
                            { "q", anchor.Position.X },
                            { "r", anchor.Position.Y },
                            { "duration", anchor.Duration },
                            { "destructible", anchor.Destructible },
                            { "hp", anchor.Hp },
                        });
                        break;
                    case ResultText txt:
                        resultsArray.Add(new Godot.Collections.Dictionary
                        {
                            { "type", "text" },
                            { "text", txt.Text },
                        });
                        break;
                }
            }

            // Also include dedicated StatusEffects list
            foreach (var se in StatusEffects)
            {
                statusArray.Add(new Godot.Collections.Dictionary
                {
                    { "effect_id", se.EffectId },
                    { "target_id", se.Target?.Data?.CharacterId.ToString() ?? "" },
                    { "duration", se.Duration },
                    { "special", se.Special.ToString() },
                });
            }

            dict["results"] = resultsArray;
            dict["status_effects"] = statusArray;
        }

        return dict;
    }
}

/// <summary>技能子结果抽象基类。</summary>
public abstract record SkillSubResult;

/// <summary>伤害事件。</summary>
public sealed record DamageEvent(
    BattleUnitModel Target,
    int Damage,
    bool IsCritical = false,
    bool WasKillingBlow = false
) : SkillSubResult;

/// <summary>治疗事件。</summary>
public sealed record HealEvent(
    BattleUnitModel Target,
    int Amount
) : SkillSubResult;

/// <summary>传送事件。</summary>
public sealed record TeleportEvent(
    BattleUnitModel Unit,
    Vector2I Destination,
    Vector2I? PreviousPosition = null
) : SkillSubResult;

/// <summary>状态效果应用。</summary>
public sealed record StatusEffectApplication(
    string EffectId,
    BattleUnitModel Target,
    int Duration = -1,
    StatusEffectSpecial Special = StatusEffectSpecial.None
) : SkillSubResult;

/// <summary>Buff 应用。</summary>
public sealed record BuffApplication(
    string BuffId,
    BattleUnitModel Target
) : SkillSubResult;

/// <summary>战场锚点事件。用于战旗、陷阱、地面光环等不属于单位的临时对象。</summary>
public sealed record BattleAnchorEvent(
    string AnchorId,
    string Source,
    Vector2I Position,
    int Duration,
    bool Destructible = false,
    int Hp = 1
) : SkillSubResult;

/// <summary>文本结果（用于日志/显示）。</summary>
public sealed record ResultText(
    string Text
) : SkillSubResult;

/// <summary>状态效果特殊操作。</summary>
public enum StatusEffectSpecial
{
    None,
    RemoveEffects,
    RemoveAllNegative
}
