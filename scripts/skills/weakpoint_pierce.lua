-- weakpoint_pierce.lua
function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end
    local r = result:resolve_attack(ctx.attacker, target, { target_ac_mult = 0.50, critical_rate = 0.30 })
    result:add_attack(r, target)
    if r.hit then
        buff:apply_custom(target, "crit_vulnerable_15", 2, { crit_taken = 0.15 })
        result:add_effect(target, "crit_vulnerable_15", 2)
    end
end
