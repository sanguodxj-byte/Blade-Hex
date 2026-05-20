-- life_circle.lua
-- 生命之环：若未使用过（life_circle_used==0），治疗周围友军1d10+floor(sqrt(CON/4))，标记已使用

function execute(ctx)
    local caster = ctx.attacker

    if caster.life_circle_used ~= 0 then
        result:fail("生命之环已使用过")
        return
    end

    local con = caster.con
    local bonus = math.floor(math.sqrt(con / 4))

    aoe_neighbors(caster, "allies", function(ally, pos)
        local heal_roll = combat:roll_dice(1, 10) + bonus
        local actual = unit:heal(ally, heal_roll)
        result:add_heal(ally, actual)
    end)

    caster.life_circle_used = 1
end
