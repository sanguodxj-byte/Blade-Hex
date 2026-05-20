-- whirlwind.lua
-- 旋风斩：攻击周围所有敌人，节点平伤每个目标 50%

function execute(ctx)
    aoe_neighbors(ctx.attacker, "enemies", function(target, pos)
        local r = result:resolve_attack(ctx.attacker, target, { node_flat_scale = 0.5 })
        result:add_attack(r)
    end)
end
