-- shield_slam.lua
function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end
    local r = result:resolve_attack(ctx.attacker, target, { damage_mult = 1.2 })
    result:add_attack(r, target)
    if r.hit then
        local p1 = hex:get_neighbor(target.q, target.r, ctx.attacker.facing)
        local p2 = hex:get_neighbor(p1.X, p1.Y, ctx.attacker.facing)
        result:add_teleport(target, p2.X, p2.Y, target.q, target.r)
        buff:apply_custom(target, "shield_slam_shaken", 2, { attack_bonus = -4 })
        result:add_effect(target, "shield_slam_shaken", 2)
    end
end
