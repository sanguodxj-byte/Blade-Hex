-- meteor_shower.lua
-- 流星雨：AOE区域，等级*骰 + DEX×0.75 伤害，类型远程

function execute(ctx)
    aoe_area(ctx.target_q, ctx.target_r, "enemies", function(target)
        local _, _, dmg = calc_skill_value(ctx, "meteor_shower")
        unit:take_damage(target, dmg)
        result:add_damage(target, dmg, "ranged")
    end)
end
