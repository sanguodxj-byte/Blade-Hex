-- seven_apertures.lua
function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end
    local r = result:resolve_attack(ctx.attacker, target, { damage_mult = 1.2 })
    result:add_attack(r, target)
    if r.hit then
        local duration = 2
        local saved = ability_save(ctx.attacker, target, "wis", "wis")
        if saved then duration = 1 end
        buff:apply(target, "root", duration)
        result:add_effect(target, "root", duration)
    end
end
