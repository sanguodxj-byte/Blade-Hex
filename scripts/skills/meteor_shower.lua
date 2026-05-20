-- meteor_shower.lua
-- 流星雨：AOE区域，对每个敌人造成2d8伤害，类型远程

function execute(ctx)
    aoe_area(ctx.target_q, ctx.target_r, "enemies", function(target)
        local dmg = combat:roll_dice(2, 8)
        unit:take_damage(target, dmg)
        result:add_damage(target, dmg, "ranged")
    end)
end
