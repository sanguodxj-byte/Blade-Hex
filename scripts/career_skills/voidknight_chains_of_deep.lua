-- voidknight_chains_of_deep.lua
-- 深渊锁链 (四属性 STR+CON+INT+CHA)
-- 近战攻击 + 给目标挂深渊锁链 debuff: 无法移动与防御, 攻击劣势

function execute(ctx)
    local tier = get_tier(ctx)

    -- 查找目标
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    -- 执行近战攻击
    local r = result:resolve_attack(ctx.attacker, target, { damage_mult = 1.0 })
    result:add_attack(r)

    -- 命中后挂 deep_chains debuff
    if r and r.hit then
        local duration = scale_duration(3, tier)
        buff:apply(target, "deep_chains", duration)
        result:add_effect(target, "deep_chains", duration)
    end
end
