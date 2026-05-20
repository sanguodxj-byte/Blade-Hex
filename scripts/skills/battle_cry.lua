-- battle_cry.lua
-- 战斗怒吼：震慑周围敌人（攻击-2，2回合），友军士气+3

function execute(ctx)
    -- 震慑周围敌人
    aoe_neighbors(ctx.attacker, "enemies", function(enemy, pos)
        result:add_effect(enemy, "fear", 2, { attack_bonus = -2 })
    end)

    -- 鼓舞友军士气
    local allies = ctx.allies
    for i = 0, allies.Length - 1 do
        local ally = allies[i]
        if unit:is_valid(ally) then
            unit:change_morale(ally, 3)
        end
    end
end
