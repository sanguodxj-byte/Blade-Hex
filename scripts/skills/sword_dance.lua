-- sword_dance.lua
-- 剑舞：攻击周围所有敌人，伤害1.5倍，节点平伤50%

function execute(ctx)
    aoe_neighbors(ctx.attacker, "enemies", function(target, pos)
        local r = result:resolve_attack(ctx.attacker, target, { damage_mult = 1.5, node_flat_scale = 0.5 })
        result:add_attack(r)
    end)
end
