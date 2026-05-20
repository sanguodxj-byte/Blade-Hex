-- blood_vortex.lua
-- 血之漩涡：攻击周围所有敌人（节点平伤50%），每命中一次治疗施法者1d6（上限3d6）

function execute(ctx)
    local total_heal = 0
    local heal_cap = 3

    aoe_neighbors(ctx.attacker, "enemies", function(target, pos)
        local r = result:resolve_attack(ctx.attacker, target, { node_flat_scale = 0.5 })
        result:add_attack(r)

        if r.hit and heal_cap > 0 then
            local heal_roll = combat:roll_dice(1, 6)
            local actual = unit:heal(ctx.attacker, heal_roll)
            result:add_heal(ctx.attacker, actual)
            total_heal = total_heal + actual
            heal_cap = heal_cap - 1
        end
    end)
end
