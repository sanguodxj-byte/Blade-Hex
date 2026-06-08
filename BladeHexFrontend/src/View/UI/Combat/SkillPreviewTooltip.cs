// SkillPreviewTooltip.cs
// 技能效果预览浮窗 — 施法瞄准悬浮时显示技能伤害/治疗预估、受影响目标
// 设计原则：不暴露骰子术语，只显示直观信息，提供 premium 视觉感受。严禁使用任何 emoji 符号。
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Combat;
using BladeHex.UI;
using BladeHex.UI.Common;

namespace BladeHex.UI.Combat;

/// <summary>
/// 技能效果预览提示 — 悬浮目标格时显示技能伤害/治疗预估、受影响目标
/// </summary>
[GlobalClass]
public partial class SkillPreviewTooltip : FloatingPanel
{
    // ============================================================================
    // 子控件
    // ============================================================================
    private RichTextLabel _titleLabel = null!;
    private RichTextLabel _effectLabel = null!;
    private RichTextLabel _targetsLabel = null!;
    private RichTextLabel _detailsLabel = null!;

    // ============================================================================
    // 颜色常量
    // ============================================================================

    // ============================================================================
    // FloatingPanel 配置
    // ============================================================================

    protected override Color PanelBgColor => new(0.08f, 0.07f, 0.12f, 0.98f); // 战斗预览特殊暗紫灰底色
    protected override Color PanelBorderColor => new(0.72f, 0.58f, 0.36f, 0.9f); // 战斗预览亮金色边框
    protected override bool FollowMouseContinuously => true;

    // ============================================================================
    // 构建内容
    // ============================================================================

    protected override void BuildContent()
    {
        // 技能名称
        _titleLabel = MakeRichText(220);
        Content.AddChild(_titleLabel);

        Content.AddChild(MakeSeparator(0.25f));

        // 效果类型（伤害/治疗/buff/debuff）
        _effectLabel = MakeRichText(220);
        Content.AddChild(_effectLabel);

        // 受影响目标列表
        _targetsLabel = MakeRichText(220);
        Content.AddChild(_targetsLabel);

        Content.AddChild(MakeSeparator(0.25f));

        // 详细信息（技能描述、消耗等）
        _detailsLabel = MakeRichText(220);
        Content.AddChild(_detailsLabel);
    }

    // ============================================================================
    // 公共 API
    // ============================================================================

    /// <summary>
    /// 显示技能效果预览。
    /// </summary>
    /// <param name="caster">施法者</param>
    /// <param name="info">技能瞄准信息</param>
    /// <param name="affectedUnits">受影响的单位列表</param>
    public void ShowPreview(Unit caster, SkillTargetingInfo info, List<Unit> affectedUnits)
    {
        if (caster == null || caster.Data == null) return;

        string skillName = GetSkillDisplayName(info);
        var preview = SkillDamagePreview.Calculate(info.SkillEffect, caster);
        string effectType = SkillDamagePreview.GetEffectType(info.SkillEffect);
        Color effectColor = SkillDamagePreview.GetEffectColor(info.SkillEffect);

        // 标题：技能名称 + 目标类型
        string targetTypeLabel = GetTargetTypeLabel(info);
        _titleLabel.Text = $"[font_size=15][b]{skillName}[/b][/font_size] [color=#8a8a9a]{targetTypeLabel}[/color]";

        // 效果类型 + 伤害/治疗数值
        if (preview.HasScaling)
        {
            string valueDesc = preview.IsHeal ? "治疗" : "伤害";
            string colorHex = effectColor.ToHtml();
            _effectLabel.Text = $"[color={colorHex}]{valueDesc}: {preview.MinValue} - {preview.MaxValue}[/color]\n"
                + $"[color=#8a8a9a]({preview.DiceDesc} + {preview.StatDesc})[/color]";
        }
        else
        {
            _effectLabel.Text = $"[color={effectColor.ToHtml()}]{effectType}[/color]";
        }

        // 受影响目标
        if (affectedUnits.Count == 0)
        {
            _targetsLabel.Text = "[color=#8a8a9a]无受影响目标[/color]";
        }
        else
        {
            string targetsText = "";
            int maxShow = 5; // 最多显示5个目标
            for (int i = 0; i < Mathf.Min(affectedUnits.Count, maxShow); i++)
            {
                var unit = affectedUnits[i];
                if (unit?.Data == null) continue;
                string sideTag = unit.Data.IsEnemy ? "[color=#ff6666][敌][/color]" : "[color=#66ff66][友][/color]";
                string hpInfo = $"{unit.CurrentHp}/{unit.GetMaxHp()}";

                // 预测效果：对每个目标显示预期伤害/治疗
                string predictText = "";
                if (preview.HasScaling && affectedUnits.Count > 0)
                {
                    int avgValue = (preview.MinValue + preview.MaxValue) / 2;
                    if (preview.IsHeal)
                    {
                        int actualHeal = Mathf.Min(avgValue, unit.GetMaxHp() - unit.CurrentHp);
                        predictText = $" → [color=#66ff99]+{actualHeal} HP[/color]";
                    }
                    else
                    {
                        predictText = $" → [color=#ff6666]-{avgValue} HP[/color]";
                    }
                }

                targetsText += $"{sideTag} {unit.Data.UnitName} [color=#8a8a9a]({hpInfo})[/color]{predictText}\n";
            }
            if (affectedUnits.Count > maxShow)
            {
                targetsText += $"[color=#8a8a9a]...及另外 {affectedUnits.Count - maxShow} 个目标[/color]";
            }
            _targetsLabel.Text = targetsText.TrimEnd();
        }

        // 详细信息
        string details = "";
        int apCost = info.ActionCost;
        int manaCost = SkillRegistry.GetManaCost(info.SkillEffect);
        details += $"[color=#8a8a9a]消耗: {apCost} AP";
        if (manaCost > 0) details += $" / {manaCost} 法力";
        details += "[/color]\n";

        // 技能描述
        var cfg = SkillEffectExecutor.GetSkillConfig(info.SkillEffect);
        if (cfg.ContainsKey("description"))
        {
            string desc = cfg["description"].AsString();
            details += $"[color=#aaaacc]{desc}[/color]";
        }
        _detailsLabel.Text = details;

        ShowAtMouse();
    }

    /// <summary>隐藏预览</summary>
    public void HidePreview() => HidePanel();

    // ============================================================================
    // 内部方法
    // ============================================================================

    private static string GetSkillDisplayName(SkillTargetingInfo info)
    {
        if (info.CareerSkill != null)
            return info.CareerSkill.DisplayName ?? "职业技能";

        var cfg = SkillEffectExecutor.GetSkillConfig(info.SkillEffect);
        if (cfg.ContainsKey("name"))
            return cfg["name"].AsString();

        return info.SkillEffect;
    }

    private static string GetTargetTypeLabel(SkillTargetingInfo info)
    {
        return info.TargetType switch
        {
            "Self" => "[自身]",
            "SingleEnemy" => "[单体敌人]",
            "RangedSingle" => "[远程单体]",
            "SingleAlly" => "[单体友军]",
            "AllAdjacent" => "[周围范围]",
            "RangedAoe" => $"[范围 {info.GetAoeRadius()}格]",
            "AoeSmall" => "[小范围]",
            "AoeCone" => "[锥形]",
            "AllAllies" => "[全体友军]",
            _ => "",
        };
    }
}
