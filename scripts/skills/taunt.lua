-- taunt.lua
-- 嘲讽：对周围敌人施加魅惑效果，持续2回合

function execute(ctx)
    aoe_neighbors(ctx.attacker, "enemies", function(enemy, pos)
        result:add_effect(enemy, "charmed", 2)
    end)
end
