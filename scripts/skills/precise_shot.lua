-- precise_shot.lua
-- Star chart DEX active: one accurate high-damage ranged attack.

function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    local r = result:resolve_attack(ctx.attacker, target, { damage_mult = 2.0, advantage = true })
    result:add_attack(r)
end
