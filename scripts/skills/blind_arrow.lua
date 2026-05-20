-- blind_arrow.lua
-- 致盲箭：攻击目标，命中则附加致盲效果2回合（攻击-4）

function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    local r = result:resolve_attack(ctx.attacker, target)
    result:add_attack(r)

    if r.hit then
        result:add_effect(target, "blind", 2, { attack_bonus = -4 })
    end
end
