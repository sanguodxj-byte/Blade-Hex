// SkillCooldownTracker.cs
// 技能冷却追踪器 — 战斗级单例，管理所有单位的技能冷却状态
// 每个单位的每个技能独立追踪冷却回合数
using System.Collections.Generic;

namespace BladeHex.Combat;

/// <summary>
/// 技能冷却追踪器。
/// 追踪每单位每技能的冷却状态，战斗开始时创建，战斗结束时销毁。
/// 冷却值来源优先级：SkillRegistry JSON "cooldown" 字段 > SkillData.Cooldown > 默认 0（无冷却）。
/// </summary>
public class SkillCooldownTracker
{
    // 外层 key = 单位 InstanceId，内层 key = skillId，value = 剩余冷却回合数
    private readonly Dictionary<long, Dictionary<string, int>> _cooldowns = new();

    // ========================================================================
    // 核心 API
    // ========================================================================

    /// <summary>
    /// 使用技能后进入冷却。
    /// cooldown &lt;= 0 表示该技能无冷却，不记录。
    /// </summary>
    public void UseSkill(long unitId, string skillId, int cooldown)
    {
        if (cooldown <= 0 || string.IsNullOrEmpty(skillId)) return;

        if (!_cooldowns.TryGetValue(unitId, out var unitCooldowns))
        {
            unitCooldowns = new Dictionary<string, int>();
            _cooldowns[unitId] = unitCooldowns;
        }

        unitCooldowns[skillId] = cooldown;
    }

    /// <summary>
    /// 回合开始时调用，将该单位所有技能冷却 -1。
    /// 冷却归零时自动移除记录。
    /// </summary>
    public void OnTurnStart(long unitId)
    {
        if (!_cooldowns.TryGetValue(unitId, out var unitCooldowns)) return;

        var toRemove = new List<string>();
        foreach (var kvp in unitCooldowns)
        {
            int remaining = kvp.Value - 1;
            if (remaining <= 0)
                toRemove.Add(kvp.Key);
            else
                unitCooldowns[kvp.Key] = remaining;
        }

        foreach (var key in toRemove)
            unitCooldowns.Remove(key);
    }

    /// <summary>检查指定技能是否在冷却中。</summary>
    public bool IsOnCooldown(long unitId, string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return false;
        return _cooldowns.TryGetValue(unitId, out var unitCooldowns)
            && unitCooldowns.ContainsKey(skillId);
    }

    /// <summary>获取指定技能的剩余冷却回合数。不在冷却中返回 0。</summary>
    public int GetRemainingCooldown(long unitId, string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return 0;
        if (_cooldowns.TryGetValue(unitId, out var unitCooldowns)
            && unitCooldowns.TryGetValue(skillId, out int remaining))
            return remaining;
        return 0;
    }

    // ========================================================================
    // 辅助
    // ========================================================================

    /// <summary>
    /// 获取技能冷却配置值。
    /// 优先读 SkillRegistry JSON 的 "cooldown" 字段；
    /// 若单位装备了该技能且 SkillData 有 Cooldown 字段，取较大值。
    /// 未配置返回 0（无冷却）。
    /// </summary>
    public static int GetSkillCooldown(Unit caster, string skillId)
    {
        // 1. 从 SkillRegistry JSON 读取
        int registryCooldown = 0;
        var cfg = SkillRegistry.GetSkillConfig(skillId);
        if (cfg.ContainsKey("cooldown"))
            registryCooldown = cfg["cooldown"].AsInt32();

        // 2. 从 SkillData Resource 读取（如果单位装备了该技能）
        int dataCooldown = 0;
        if (caster.Data != null)
        {
            foreach (var skill in caster.Data.Skills)
            {
                if (skill != null && skill.SkillName == skillId)
                {
                    dataCooldown = skill.Cooldown;
                    break;
                }
            }
            // 也检查 EquippedSkills 对应的 SkillData
            if (dataCooldown == 0)
            {
                foreach (var equipped in caster.Data.EquippedSkills)
                {
                    if (equipped == skillId)
                    {
                        foreach (var skill in caster.Data.Skills)
                        {
                            if (skill != null && skill.SkillName == skillId)
                            {
                                dataCooldown = skill.Cooldown;
                                break;
                            }
                        }
                        break;
                    }
                }
            }
        }

        return System.Math.Max(registryCooldown, dataCooldown);
    }

    /// <summary>移除指定单位的所有冷却记录（单位死亡时调用）。</summary>
    public void RemoveUnit(long unitId)
    {
        _cooldowns.Remove(unitId);
    }

    /// <summary>清空所有冷却记录（战斗开始/结束时调用）。</summary>
    public void Reset()
    {
        _cooldowns.Clear();
    }

    /// <summary>获取指定单位所有在冷却中的技能及其剩余回合（调试/UI 用）。</summary>
    public IReadOnlyDictionary<string, int> GetUnitCooldowns(long unitId)
    {
        if (_cooldowns.TryGetValue(unitId, out var unitCooldowns))
            return unitCooldowns;
        return new Dictionary<string, int>();
    }
}
