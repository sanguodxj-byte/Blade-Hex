-- taunt.lua
-- 嘲讽：对周围敌人施加魅惑效果，持续2回合

function execute(ctx)
    aoe_neighbors(ctx.attacker, "enemies", function(enemy, pos)
        buff:apply_custom(enemy, "charmed", 2, { forced_target_id = ctx.attacker.character_id })
    end)
end
