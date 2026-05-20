-- rally.lua
-- 集结：为周围友军添加集结效果（攻击+2），持续2回合

function execute(ctx)
    aoe_neighbors(ctx.attacker, "allies", function(ally, pos)
        result:add_effect(ally, "rallied", 2, { attack_bonus = 2 })
    end)
end
