-- life_circle.lua
-- 生命之环：若未使用过（life_circle_used==0），治疗周围友军1d10+floor(sqrt(CON/4))，标记已使用

function execute(ctx)
    local caster = ctx.attacker

    if caster.life_circle_used ~= 0 then
        result:fail("生命之环已使用过")
        return
    end

    aoe_neighbors(caster, "allies", function(ally, pos)
        local _, _, heal_roll = calc_skill_value(ctx, "life_circle")
        local actual = unit:heal(ally, heal_roll)
        result:add_heal(ally, actual)
    end)

    caster.life_circle_used = 1
end
