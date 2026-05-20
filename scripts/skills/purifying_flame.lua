-- purifying_flame.lua
-- 净化之焰：AOE区域，2d8+WIS伤害，对亡灵1.5倍，类型奥术

function execute(ctx)
    local wis_mod = combat:get_stat_mod(ctx.attacker.wis)

    aoe_area(ctx.target_q, ctx.target_r, "enemies", function(target)
        local dmg = combat:roll_dice(2, 8) + wis_mod
        if target.enemy_type == "Undead" then
            dmg = math.floor(dmg * 1.5)
        end
        unit:take_damage(target, dmg)
        result:add_damage(target, dmg, "arcane")
    end)
end
