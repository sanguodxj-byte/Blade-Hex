-- purifying_flame.lua
-- 净化之焰：AOE区域，等级*骰+WIS伤害，对亡灵1.5倍，类型奥术

function execute(ctx)
    aoe_area(ctx.target_q, ctx.target_r, "enemies", function(target)
        local _, _, dmg = calc_skill_value(ctx, "purifying_flame")
        if target.enemy_type == "Undead" then
            dmg = math.floor(dmg * 1.5)
        end
        unit:take_damage(target, dmg)
        result:add_damage(target, dmg, "arcane")
    end)
end
