-- poison_blade.lua
-- 毒刃：攻击目标并附加中毒效果3回合

function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    local r = result:resolve_attack(ctx.attacker, target)
    result:add_attack(r)

    if r.hit then
        result:add_effect(target, "poison", 3)
    end
end
