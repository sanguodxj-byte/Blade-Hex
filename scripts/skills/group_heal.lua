-- group_heal.lua
-- 群体治疗：治疗周围所有友军等级*骰 + WIS×1.0（拥有生命精通时1.5倍）

function execute(ctx)
    local has_mastery = ctx.attacker:has_skill("life_mastery")

    aoe_neighbors(ctx.attacker, "allies", function(ally, pos)
        local _, _, heal = calc_skill_value(ctx, "group_heal")
        if has_mastery then
            heal = math.floor(heal * 1.5)
        end
        local actual = unit:heal(ally, heal)
        result:add_heal(ally, actual)
    end)
end
