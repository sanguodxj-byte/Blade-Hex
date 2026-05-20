-- war_cry.lua
-- 战吼：为周围友军添加祝福效果（攻击+1），持续2回合

function execute(ctx)
    aoe_neighbors(ctx.attacker, "allies", function(ally, pos)
        result:add_effect(ally, "bless", 2, { attack_bonus = 1 })
    end)
end
