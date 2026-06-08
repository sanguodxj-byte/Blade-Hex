-- shadow_lunge.lua
function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end
    local caster = ctx.attacker
    local best = nil
    for i = 0, 5 do
        local pos = hex:get_neighbor(target.q, target.r, i)
        if unit:can_push_to(pos.X, pos.Y) then
            best = pos
            break
        end
    end
    if not best then
        result:fail("目标周围没有可突进落点")
        return
    end
    unit:teleport(caster, best.X, best.Y)
    buff:apply_custom(caster, "shadow_lunge_next", 1, { critical_rate = 0.20 }, "marked_target:" .. tostring(target.instance_id))
    result:add_effect(caster, "shadow_lunge_next", 1)
end
