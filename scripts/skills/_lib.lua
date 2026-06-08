-- _lib.lua
-- 技能脚本公共工具库 — 所有技能脚本可直接使用这些函数
-- NLua 版：C# 对象方法用 : 调用，属性用 . 访问
--
-- v0.8 更新：添加 tier 缩放工具函数，与 CareerSkillExecutor.cs 的 Scale* 函数对齐

-- ============================================================================
-- 基础工具函数
-- ============================================================================

--- 查找目标格的敌人，找不到则自动 fail 并返回 nil
function require_enemy(q, r)
    local target = unit:find_at(q, r, "enemies")
    if not target then
        result:fail("目标格没有敌人")
        return nil
    end
    return target
end

--- 查找目标格的友军，找不到则自动 fail 并返回 nil
function require_ally(q, r)
    local target = unit:find_at(q, r, "allies")
    if not target then
        result:fail("目标格没有盟友")
        return nil
    end
    return target
end

--- 遍历施放者周围六邻格，对每个找到的指定阵营单位执行回调
function aoe_neighbors(caster, side, callback)
    local neighbors = hex:neighbors(caster.q, caster.r)
    for i = 0, neighbors.Length - 1 do
        local pos = neighbors[i]
        local target = unit:find_at(pos.X, pos.Y, side)
        if target and unit:is_valid(target) then
            callback(target, pos)
        end
    end
end

--- 遍历目标格 + 其六邻格（7 格 AOE），对每个找到的指定阵营单位执行回调
function aoe_area(center_q, center_r, side, callback)
    local center_target = unit:find_at(center_q, center_r, side)
    if center_target and unit:is_valid(center_target) then
        callback(center_target)
    end
    local neighbors = hex:neighbors(center_q, center_r)
    for i = 0, neighbors.Length - 1 do
        local pos = neighbors[i]
        local target = unit:find_at(pos.X, pos.Y, side)
        if target and unit:is_valid(target) then
            callback(target)
        end
    end
end

--- 检查魔力，不足则 fail；实际资源消耗由 CombatManager 统一处理
function check_mana(caster, cost)
    if caster.mana < cost then
        result:fail("魔力不足")
        return false
    end
    return true
end

-- ============================================================================
-- v0.8: Tier 缩放工具函数
-- 与 CareerSkillExecutor.cs 中的 Scale* 函数完全对齐
-- ============================================================================

--- 获取 tier_multiplier（从 ctx 注入）
--- @return number tier 倍率（1.0 / 1.25 / 1.5 / 1.75 / 2.0 / 2.5）
function get_tier(ctx)
    return ctx.tier or 1.0
end

--- 获取 attribute_count（从 ctx 注入）
--- @return integer 属性数（1-6）
function get_attribute_count(ctx)
    return ctx.attribute_count or 1
end

--- 持续回合缩放: max(1, floor(base * tier))
--- @param base_duration integer 基线持续回合
--- @param tier number tier 倍率
--- @return integer 缩放后的持续回合
function scale_duration(base_duration, tier)
    return math.max(1, math.floor(base_duration * tier))
end

--- 整数缩放: ceil(base * tier)
--- @param base_value integer 基线值
--- @param tier number tier 倍率
--- @return integer 缩放后的整数值
function scale_int(base_value, tier)
    return math.ceil(base_value * tier)
end

--- 骰子数缩放: max(1, floor(base * tier))
--- @param base_dice integer 基线骰子数
--- @param tier number tier 倍率
--- @return integer 缩放后的骰子数
function scale_dice(base_dice, tier)
    return math.max(1, math.floor(base_dice * tier))
end

--- 浮点数缩放: base * tier
--- @param base_value number 基线值
--- @param tier number tier 倍率
--- @return number 缩放后的浮点值
function scale_float(base_value, tier)
    return base_value * tier
end

-- ============================================================================
-- v0.8: 常用 buff 应用快捷函数
-- ============================================================================

--- 给自身应用 buff 并记录到 result
--- @param ctx table 执行上下文
--- @param buff_id string buff ID
--- @param base_duration integer 基线持续回合
function self_buff(ctx, buff_id, base_duration)
    local tier = get_tier(ctx)
    local duration = scale_duration(base_duration, tier)
    buff:apply(ctx.attacker, buff_id, duration)
    result:add_effect(ctx.attacker, buff_id, duration)
    return duration
end

--- 给目标应用 buff 并记录到 result
--- @param target LuaUnitProxy 目标单位
--- @param buff_id string buff ID
--- @param base_duration integer 基线持续回合
function target_buff(ctx, target, buff_id, base_duration)
    local tier = get_tier(ctx)
    local duration = scale_duration(base_duration, tier)
    buff:apply(target, buff_id, duration)
    result:add_effect(target, buff_id, duration)
    return duration
end

local NEGATIVE_EFFECT_IDS = {
    "bleed", "poison", "burning", "blind", "fear", "stun", "root",
    "silence", "charmed", "confused", "slow", "freeze", "frozen",
    "poisoned", "bleeding", "slowed", "stunned"
}

function remove_negative_effects(target)
    if not target then return end
    buff:remove_many(target, NEGATIVE_EFFECT_IDS)
end

local STANCE_EFFECT_IDS = {
    "stance_berserk",
    "stance_guard",
    "stance_hunter"
}

function enter_stance(target, stance_id)
    if not target then return end
    for _, id in ipairs(STANCE_EFFECT_IDS) do
        if id ~= stance_id then
            buff:remove(target, id)
        end
    end
end

function get_proxy_stat(proxy, stat)
    if stat == "str" then return proxy.str or 10 end
    if stat == "dex" then return proxy.dex or 10 end
    if stat == "con" then return proxy.con or 10 end
    if stat == "intel" or stat == "int" then return proxy.intel or 10 end
    if stat == "wis" then return proxy.wis or 10 end
    if stat == "cha" then return proxy.cha or 10 end
    return 10
end

--- 属性豁免：DC = 10 + 施放者属性调整 + 熟练接口返回值；检定 = d20 + 目标属性调整 + 熟练接口返回值
--- @param caster LuaUnitProxy 施放者
--- @param target LuaUnitProxy 目标
--- @param dc_stat string 施放者 DC 属性名
--- @param save_stat string 目标豁免属性名
--- @return boolean success, integer roll, integer dc
function ability_save(caster, target, dc_stat, save_stat)
    local caster_stat = get_proxy_stat(caster, dc_stat)
    local target_stat = get_proxy_stat(target, save_stat)
    local dc = 10 + combat:get_stat_mod(caster_stat) + combat:get_proficiency(caster.level or 1)
    local roll = combat:roll_dice(1, 20)
        + combat:get_stat_mod(target_stat)
        + combat:get_proficiency(target.level or 1)
    return roll >= dc, roll, dc
end

-- ============================================================================
-- v0.8: 等级骰子计算
-- ============================================================================

--- 等级骰子: max(1, level/4) d sides
--- @param level integer 单位等级
--- @param sides integer 骰子面数（默认6）
--- @return integer count, integer sides
function get_level_dice(level, sides)
    sides = sides or 6
    return math.max(1, math.floor(level / 4)), sides
end

--- 百分比 HP 转绝对值
--- @param proxy LuaUnitProxy 单位代理
--- @param percent number 百分比（0.0-1.0）
--- @return integer 绝对 HP 值
function percent_of_max_hp(proxy, percent)
    return math.max(1, math.floor(proxy.max_hp * percent))
end

--- 百分比 Mana 转绝对值
--- @param proxy LuaUnitProxy 单位代理
--- @param percent number 百分比（0.0-1.0）
--- @return integer 绝对 Mana 值
function percent_of_max_mana(proxy, percent)
    return math.max(1, math.floor(proxy.max_mana * percent))
end

--- 属性修正型加值: ceil(stat_mod * level * multiplier)
--- @param stat_score integer 属性值
--- @param level integer 单位等级
--- @param multiplier number 倍率（默认1.0）
--- @return integer 加值
function stat_mod_x_level(stat_score, level, multiplier)
    multiplier = multiplier or 1.0
    local mod = combat:get_stat_mod(stat_score)
    return math.ceil(mod * level * multiplier)
end

-- ============================================================================
-- v0.8: 技能数值控制表（中心化配置）
-- 所有技能的伤害/治疗骰子和属性系数在此定义
-- ============================================================================

--- 技能数值控制表
--- 每个技能定义：
---   sides     - 骰子面数 (d8=8, d6=6, etc.)
---   stat      - 关联属性名 (intel/wis/con/dex/str)
---   stat_mult - 属性修正系数 (默认1.0)
---   tags      - 额外标签数组（可选）
local SKILL_SCALING = {
    -- 奥术系：INT 为主属性
    arcane_burst     = { sides = 8,  stat = "intel", stat_mult = 1.0 },
    arcane_bomb      = { sides = 6,  stat = "intel", stat_mult = 1.0 },
    chain_lightning  = { sides = 6,  stat = "intel", stat_mult = 1.0 },
    mana_drain       = { sides = 6,  stat = "intel", stat_mult = 1.0 },
    -- 神圣/自然系：WIS 为主属性
    arcane_judgment  = { sides = 10, stat = "wis",   stat_mult = 1.0 },
    elemental_storm  = { sides = 8,  stat = "wis",   stat_mult = 1.0 },
    purifying_flame  = { sides = 8,  stat = "wis",   stat_mult = 1.0 },
    -- 治疗系：WIS 为主属性
    basic_heal       = { sides = 8,  stat = "wis",   stat_mult = 1.0 },
    field_medic      = { sides = 8,  stat = "wis",   stat_mult = 1.0 },
    group_heal       = { sides = 6,  stat = "wis",   stat_mult = 1.0 },
    life_circle      = { sides = 10, stat = "con",   stat_mult = 0.5 },
    -- 远程系：DEX 为主属性
    meteor_shower    = { sides = 8,  stat = "dex",   stat_mult = 0.75 },
    trick_arrow      = { sides = 10, stat = "dex",   stat_mult = 0.75 },
    assassinate      = { sides = 12, stat = "dex",   stat_mult = 1.5 },
    -- 近战系：STR 为主属性
    blood_vortex     = { sides = 6,  stat = "str",   stat_mult = 0.5 },
    -- 陷阱/防御系：DEX 为主属性
    trap_master      = { sides = 6,  stat = "dex",   stat_mult = 0.75 },
    unyielding_bulwark = { sides = 6, stat = "con",  stat_mult = 0.5 },
    -- 薄葬：纯 buff 技能，数值未使用（保留占位）
    shallow_burial   = { sides = 1,  stat = "wis",   stat_mult = 0 },
    -- 净化领域：WIS 为主属性
    purify_field     = { sides = 6,  stat = "wis",   stat_mult = 1.0 },
}

--- 获取技能缩放配置
--- @param skill_id string 技能ID
--- @return table|nil 缩放配置（sides, stat, stat_mult）
function get_skill_scaling(skill_id)
    return SKILL_SCALING[skill_id]
end

--- 计算技能基础骰数（等级缩放）
--- @param level integer 单位等级
--- @param sides integer 骰子面数
--- @return integer dice_count, integer dice_sides
function calc_skill_dice(level, sides)
    return get_level_dice(level, sides)
end

--- 计算技能伤害/治疗值
--- @param ctx table 执行上下文
--- @param skill_id string 技能ID
--- @param extra_mod integer? 额外修正（可选）
--- @return integer 骰子数, integer 面数, integer 总伤害/治疗量
function calc_skill_value(ctx, skill_id, extra_mod)
    local cfg = get_skill_scaling(skill_id)
    if not cfg then return 0, 0, 0 end

    local level = ctx.attacker.level or 1
    local stat_val = get_proxy_stat(ctx.attacker, cfg.stat)
    local stat_mod = combat:get_stat_mod(stat_val)
    local dice_count, dice_sides = calc_skill_dice(level, cfg.sides or 6)

    local roll = combat:roll_dice(dice_count, dice_sides)
    local bonus = math.floor(stat_mod * (cfg.stat_mult or 1.0))
    if extra_mod then bonus = bonus + extra_mod end

    local total = math.max(1, roll + bonus)
    return dice_count, dice_sides, total
end

--- 生成技能描述用公式字符串
--- @param skill_id string 技能ID
--- @return string 公式描述（如 "max(1,等级/4)d8 + INT×1.0"）
function describe_skill_formula(skill_id)
    local cfg = get_skill_scaling(skill_id)
    if not cfg then return "" end
    local stat_name = ({ intel="INT", wis="WIS", dex="DEX", str="STR", con="CON" })[cfg.stat] or cfg.stat:upper()
    local stat_part = stat_name
    if cfg.stat_mult ~= 1.0 then
        stat_part = stat_name .. "×" .. tostring(cfg.stat_mult)
    end
    return string.format("max(1,等级/4)d%d + %s", cfg.sides, stat_part)
end
