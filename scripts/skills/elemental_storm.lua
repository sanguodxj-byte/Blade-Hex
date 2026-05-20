-- elemental_storm.lua
-- 元素风暴：AOE区域，2d8+WIS伤害（若有知识力量额外+INT），类型元素

function execute(ctx)
    local wis_mod = combat:get_stat_mod(ctx.attacker.wis)
    local int_mod = 0
    if ctx.attacker:has_skill("knowledge_power") then
        int_mod = combat:get_stat_mod(ctx.attacker.intel)
    end

    aoe_area(ctx.target_q, ctx.target_r, "enemies", function(target)
        local dmg = combat:roll_dice(2, 8) + wis_mod + int_mod
        unit:take_damage(target, dmg)
        result:add_damage(target, dmg, "elemental")
    end)
end
