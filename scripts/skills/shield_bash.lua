-- shield_bash.lua
-- 盾击：攻击目标，命中则沿施法者朝向推开1格

function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    local r = result:resolve_attack(ctx.attacker, target)
    result:add_attack(r)

    if r.hit then
        local push_pos = hex:get_neighbor(target.q, target.r, ctx.attacker.facing)
        if unit:can_push_to(push_pos.X, push_pos.Y) then
            result:add_teleport(target, push_pos.X, push_pos.Y, target.q, target.r)
        else
            result:add_damage(target, percent_of_max_hp(target, 0.08))
        end
    end
end
