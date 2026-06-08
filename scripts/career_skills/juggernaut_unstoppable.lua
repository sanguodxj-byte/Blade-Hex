-- juggernaut_unstoppable.lua
-- 不可阻挡 (四属性 STR+CON+DEX+WIS)
-- 冲锋 3 格 + 1.5x 伤害 + 临时 HP + 免疫控制

function execute(ctx)
    local tier = get_tier(ctx)

    -- 冲锋攻击 (1.5x 伤害)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if target then
        local r = result:resolve_attack(ctx.attacker, target, { advantage = true, damage_mult = 1.5 })
        result:add_attack(r)
    end

    -- 给自己挂 unstoppable buff (临时 HP + 免疫控制)
    local duration = scale_duration(2, tier)
    local temp_hp = stat_mod_x_level(ctx.attacker.con, ctx.attacker.level, 0.5 * tier)
    buff:apply_custom(ctx.attacker, "unstoppable", duration, { temp_hp_amount = temp_hp })
    result:add_effect(ctx.attacker, "unstoppable", duration)
end
