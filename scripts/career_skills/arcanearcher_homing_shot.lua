-- arcanearcher_homing_shot.lua
-- 魔矢追踪 (双属性 DEX+INT)
-- 无视掩体远程攻击 + 额外奥术伤害 + 追踪 buff

function execute(ctx)
    local tier = get_tier(ctx)

    -- 查找目标
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    -- 执行远程攻击 (无视掩体)
    local r = result:resolve_attack(ctx.attacker, target, { advantage = true })
    result:add_attack(r)

    -- 命中后额外奥术伤害: 1d8 * tier
    if r and r.hit then
        local dice_count = scale_dice(1, tier)
        local arcane_dmg = combat:roll_dice(dice_count, 8)
        unit:take_damage(target, arcane_dmg)
        result:add_damage(target, arcane_dmg, "arcane")
    end

    -- 给自己挂 homing buff (无视半掩体)
    self_buff(ctx, "homing", 2)
end
