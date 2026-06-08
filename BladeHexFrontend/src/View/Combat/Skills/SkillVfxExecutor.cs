// SkillVfxExecutor.cs
// VFX/SFX 执行器 — 从 skill config 的 vfx/sfx 字段读取配置
// 替代 CombatSceneBase 中的硬编码字符串匹配
using Godot;
using System.Collections.Generic;
using BladeHex.Audio;
using BladeHex.View.Combat;

namespace BladeHex.Combat.Skills;

/// <summary>
/// 技能 VFX/SFX 执行器。
///
/// <para>从 skill_configs.json 的 vfx/sfx 字段读取配置，驱动 VFXManager 和 AudioManager。</para>
/// <para>VFX 类型映射到 VFXManager.PlaySkillVfx 的已有色表或特殊方法。</para>
/// <para>SFX 映射到 AudioManager.PlaySfxName 的已有音效名。</para>
/// </summary>
public static class SkillVfxExecutor
{
    // ========================================================================
    // VFX 类型 → 效果类别映射
    // ========================================================================

    /// <summary>
    /// 效果类别：决定 VFXManager 调用方式和位置偏移
    /// </summary>
    public enum VfxCategory
    {
        /// <summary>爆炸类：居中于目标，PlayExplosionEffect</summary>
        Explosion,
        /// <summary>奥术类：居中于目标，PlaySkillVfx</summary>
        Arcane,
        /// <summary>火焰类：居中于目标，PlaySkillVfx</summary>
        Fire,
        /// <summary>治疗类：目标上方偏移，PlaySkillVfx("heal")</summary>
        Heal,
        /// <summary>斩击类：目标处，PlayHitEffect</summary>
        Slash,
        /// <summary>远程类：目标处，PlayHitEffect</summary>
        Ranged,
        /// <summary>辅助/buff 类：目标上方偏移，PlaySkillVfx</summary>
        Support,
        /// <summary>默认：使用 PlaySkillVfx 直接传入 vfx 字符串</summary>
        Default,
    }

    // ========================================================================
    // VFX 标识符 → 效果类别映射表
    // 覆盖 skill_configs.json 中所有出现的 vfx 值
    // ========================================================================

    private static readonly Dictionary<string, VfxCategory> VfxCategoryMap = new()
    {
        // --- 近战 ---
        { "melee_combo",    VfxCategory.Slash },
        { "whirlwind",      VfxCategory.Explosion },
        { "blood_vortex",   VfxCategory.Explosion },
        { "shield_bash",    VfxCategory.Slash },
        { "poison_blade",   VfxCategory.Slash },
        { "shadow_strike",  VfxCategory.Slash },
        { "sword_dance",    VfxCategory.Slash },
        { "head_shot",      VfxCategory.Slash },
        { "assassinate",    VfxCategory.Slash },

        // --- 远程 ---
        { "aimed_shot",     VfxCategory.Ranged },
        { "double_shot",    VfxCategory.Ranged },
        { "scatter_shot",   VfxCategory.Ranged },
        { "trick_arrow",    VfxCategory.Ranged },
        { "multi_shot",     VfxCategory.Ranged },
        { "blind_arrow",    VfxCategory.Ranged },
        { "meteor_shower",  VfxCategory.Explosion },

        // --- 火焰 ---
        { "purifying_flame", VfxCategory.Fire },

        // --- 奥术 ---
        { "arcane_burst",   VfxCategory.Arcane },
        { "arcane_bomb",    VfxCategory.Arcane },
        { "arcane_judgment", VfxCategory.Arcane },
        { "mana_drain",     VfxCategory.Arcane },
        { "chain_lightning", VfxCategory.Arcane },
        { "void_gate",      VfxCategory.Arcane },

        // --- 治疗 ---
        { "heal",           VfxCategory.Heal },
        { "basic_heal",     VfxCategory.Heal },
        { "life_circle",    VfxCategory.Heal },
        { "group_heal",     VfxCategory.Heal },
        { "field_medic",    VfxCategory.Heal },

        // --- 辅助/buff ---
        { "blessing",       VfxCategory.Support },
        { "war_cry",        VfxCategory.Support },
        { "inspire",        VfxCategory.Support },
        { "command",        VfxCategory.Support },
        { "rally",          VfxCategory.Support },
        { "taunt",          VfxCategory.Support },
        { "intimidate",     VfxCategory.Support },
        { "heroic_call",    VfxCategory.Support },
        { "stealth",        VfxCategory.Support },
        { "shadow_clone",   VfxCategory.Support },
        { "trap",           VfxCategory.Support },
        { "shadow_deal",    VfxCategory.Support },
        { "oracle",         VfxCategory.Support },

        // --- 护盾 ---
        { "bulwark",        VfxCategory.Support },
        { "life_shield",    VfxCategory.Support },
        { "mana_shield",    VfxCategory.Support },
        { "time_warp",      VfxCategory.Support },
        { "guardian_spirit", VfxCategory.Support },
        { "shallow_burial", VfxCategory.Support },
        { "mana_surge",     VfxCategory.Support },
    };

    // ========================================================================
    // SFX 类型 → 音效名映射
    // skill_configs.json 的 sfx 字段值 → AudioManager.PlaySfxName 的参数
    // ========================================================================

    private static readonly Dictionary<string, string> SfxNameMap = new()
    {
        { "slash",   "skill_slash" },
        { "fire",    "skill_fire" },
        { "heal",    "skill_heal" },
        { "arcane",  "skill_arcane" },
        { "ranged",  "skill_ranged" },
        { "explosion", "skill_explosion" },
        { "buff",    "skill_buff" },
        { "debuff",  "skill_debuff" },
    };

    // ========================================================================
    // 位置偏移配置
    // ========================================================================

    /// <summary>VFX 位置偏移（相对 hex 顶面 + 角色层）</summary>
    private static readonly Dictionary<VfxCategory, Vector3> PositionOffsets = new()
    {
        { VfxCategory.Explosion, new Vector3(0, 10, 0) },
        { VfxCategory.Arcane,    new Vector3(0, 50, 0) },
        { VfxCategory.Fire,      new Vector3(0, 30, 0) },
        { VfxCategory.Heal,      new Vector3(0, 30, 0) },
        { VfxCategory.Slash,     new Vector3(0, 50, 0) },
        { VfxCategory.Ranged,    new Vector3(0, 50, 0) },
        { VfxCategory.Support,   new Vector3(0, 50, 0) },
        { VfxCategory.Default,   new Vector3(0, 50, 0) },
    };

    // ========================================================================
    // 公共 API
    // ========================================================================

    /// <summary>
    /// 从 skill config 读取 vfx/sfx 字段，返回解析后的 VFX/SFX 配置。
    /// 调用方可用此结果自行决定如何播放。
    /// </summary>
    public static SkillVfxConfig GetConfig(string skillId)
    {
        var cfg = SkillRegistry.GetSkillConfig(skillId);

        string vfxType = cfg.ContainsKey("vfx") ? cfg["vfx"].AsString() : "";
        string sfxKey = cfg.ContainsKey("sfx") ? cfg["sfx"].AsString() : "";

        var vfxCategory = VfxCategoryMap.GetValueOrDefault(vfxType, VfxCategory.Default);
        string sfxName = ResolveSfxName(sfxKey, vfxType, vfxCategory);
        Vector3 offset = PositionOffsets.GetValueOrDefault(vfxCategory, PositionOffsets[VfxCategory.Default]);

        return new SkillVfxConfig
        {
            VfxType = vfxType,
            VfxCategory = vfxCategory,
            SfxName = sfxName,
            PositionOffset = offset,
        };
    }

    /// <summary>
    /// 一站式执行：播放 VFX 和 SFX。
    /// </summary>
    /// <param name="parent">场景树父节点（通常是 CombatSceneBase）</param>
    /// <param name="targetPos">目标世界坐标（cell.GlobalPosition）</param>
    /// <param name="skillId">技能 ID（如 "double_attack", "basic_heal"）</param>
    /// <param name="audioManager">音频管理器实例（可为 null，跳过 SFX）</param>
    public static void Execute(Node parent, Vector3 targetPos, string skillId, AudioManager? audioManager)
    {
        var config = GetConfig(skillId);

        // 播放 SFX
        if (!string.IsNullOrEmpty(config.SfxName))
            audioManager?.PlaySfxName(config.SfxName);

        // 计算 VFX 世界坐标：目标位置 + hex 顶层偏移 + 角色层 + 效果偏移
        Vector3 vfxPos = targetPos
            + new Vector3(0, CombatLayerHeight.HexTopOffset + CombatLayerHeight.CharacterLayer, 0)
            + config.PositionOffset;

        // 播放 VFX
        PlayVfxByCategory(parent, vfxPos, config);
    }

    /// <summary>
    /// 执行 VFX（不含 SFX），用于调用方需要单独控制音频时。
    /// </summary>
    public static void ExecuteVfx(Node parent, Vector3 targetPos, string skillId)
    {
        var config = GetConfig(skillId);
        Vector3 vfxPos = targetPos
            + new Vector3(0, CombatLayerHeight.HexTopOffset + CombatLayerHeight.CharacterLayer, 0)
            + config.PositionOffset;
        PlayVfxByCategory(parent, vfxPos, config);
    }

    /// <summary>
    /// 仅播放 SFX（不含 VFX），用于调用方需要单独控制特效时。
    /// </summary>
    public static void ExecuteSfx(string skillId, AudioManager? audioManager)
    {
        var config = GetConfig(skillId);
        if (!string.IsNullOrEmpty(config.SfxName))
            audioManager?.PlaySfxName(config.SfxName);
    }

    // ========================================================================
    // VFX 播放内部逻辑
    // ========================================================================

    private static void PlayVfxByCategory(Node parent, Vector3 pos, SkillVfxConfig config)
    {
        switch (config.VfxCategory)
        {
            case VfxCategory.Explosion:
                VFXManager.PlayExplosionEffect(parent, pos);
                break;

            case VfxCategory.Heal:
                VFXManager.PlaySkillVfx(parent, pos, "heal");
                break;

            case VfxCategory.Arcane:
            case VfxCategory.Fire:
            case VfxCategory.Support:
            case VfxCategory.Default:
                // 传入 vfxType 字符串，让 VFXManager 根据色表和 switch 分发
                if (!string.IsNullOrEmpty(config.VfxType))
                    VFXManager.PlaySkillVfx(parent, pos, config.VfxType);
                else
                    VFXManager.PlayHitEffect(parent, pos);
                break;

            case VfxCategory.Slash:
                VFXManager.PlayHitEffect(parent, pos);
                break;

            case VfxCategory.Ranged:
                VFXManager.PlayHitEffect(parent, pos);
                break;
        }
    }

    // ========================================================================
    // SFX 名称解析
    // ========================================================================

    /// <summary>
    /// 解析 SFX 名称。
    /// 优先使用 sfx 字段显式值；无配置时根据 vfx 类别推断默认值。
    /// </summary>
    private static string ResolveSfxName(string sfxKey, string vfxType, VfxCategory category)
    {
        // 1. 显式 sfx 字段匹配
        if (!string.IsNullOrEmpty(sfxKey) && SfxNameMap.TryGetValue(sfxKey, out var mapped))
            return mapped;

        // 2. sfx 字段直接就是完整音效名（不在映射表中）
        if (!string.IsNullOrEmpty(sfxKey))
            return sfxKey;

        // 3. 根据 vfx 类别推断默认 SFX
        return category switch
        {
            VfxCategory.Explosion => "skill_explosion",
            VfxCategory.Arcane => "skill_arcane",
            VfxCategory.Fire => "skill_fire",
            VfxCategory.Heal => "skill_heal",
            VfxCategory.Slash => "skill_slash",
            VfxCategory.Ranged => "skill_ranged",
            VfxCategory.Support => "skill_buff",
            _ => "skill_slash",
        };
    }
}

/// <summary>
/// 解析后的 VFX/SFX 配置结果
/// </summary>
public struct SkillVfxConfig
{
    /// <summary>VFX 标识符（来自 skill config 的 vfx 字段）</summary>
    public string VfxType;

    /// <summary>VFX 效果类别</summary>
    public SkillVfxExecutor.VfxCategory VfxCategory;

    /// <summary>解析后的 SFX 名称（传给 AudioManager.PlaySfxName）</summary>
    public string SfxName;

    /// <summary>VFX 位置偏移（相对 hex 顶面 + 角色层基准点）</summary>
    public Vector3 PositionOffset;
}
