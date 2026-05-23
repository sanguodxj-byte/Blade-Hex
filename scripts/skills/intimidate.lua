-- intimidate.lua
-- 威吓：对周围敌人施加威吓效果（攻击-2），持续3回合

function execute(ctx)
    aoe_neighbors(ctx.attacker, "enemies", function(enemy, pos)
        buff:apply_custom(enemy, "intimidated", 3, { attack_bonus = -2 })
    end)
end
