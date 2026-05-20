-- group_heal.lua
-- 群体治疗：治疗周围所有友军1d6+WIS（拥有生命精通时1.5倍）

function execute(ctx)
    local wis_mod = combat:get_stat_mod(ctx.attacker.wis)
    local has_mastery = ctx.attacker:has_skill("life_mastery")

    aoe_neighbors(ctx.attacker, "allies", function(ally, pos)
        local heal = combat:roll_dice(1, 6) + wis_mod
        if has_mastery then
            heal = math.floor(heal * 1.5)
        end
        local actual = unit:heal(ally, heal)
        result:add_heal(ally, actual)
    end)
end
