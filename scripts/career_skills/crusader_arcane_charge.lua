-- crusader_arcane_charge.lua
-- 奥术冲锋 (三属性 STR+INT+CON)
-- 冲锋 + 奥术路径伤害 + 给自己挂先驱 buff

function execute(ctx)
    local tier = get_tier(ctx)

    -- 冲锋攻击
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if target then
        local r = result:resolve_attack(ctx.attacker, target, { advantage = true, damage_mult = 1.0 })
        result:add_attack(r)

        -- 奥术路径伤害: 等级骰 d6
        local count, sides = get_level_dice(ctx.attacker.level, 6)
        local arcane_dmg = combat:roll_dice(count, sides)
        unit:take_damage(target, arcane_dmg)
        result:add_damage(target, arcane_dmg, "arcane")
    end

    -- 给自己挂 vanguard buff (身后友军获冲锋优势)
    self_buff(ctx, "vanguard", 2)
end
