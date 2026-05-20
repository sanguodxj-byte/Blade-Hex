-- double_shot.lua
-- 双重射击：对目标射击2次，每次命中-2

function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    local r1 = result:resolve_attack(ctx.attacker, target, { hit_mod = -2 })
    result:add_attack(r1)

    local r2 = result:resolve_attack(ctx.attacker, target, { hit_mod = -2 })
    result:add_attack(r2)
end
