-- aimed_shot.lua
-- 精准射击：对目标造成 2 倍伤害

function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    local r = result:resolve_attack(ctx.attacker, target, { damage_mult = 2.0 })
    result:add_attack(r)
end
