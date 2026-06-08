-- elemental_storm.lua
-- 元素风暴：AOE区域，等级*骰+WIS伤害（若有知识力量额外+INT），类型元素

function execute(ctx)
    local int_mod = 0
    if ctx.attacker:has_skill("knowledge_power") then
        int_mod = combat:get_stat_mod(ctx.attacker.intel)
    end

    aoe_area(ctx.target_q, ctx.target_r, "enemies", function(target)
        local _, _, dmg = calc_skill_value(ctx, "elemental_storm")
        dmg = dmg + int_mod
        unit:take_damage(target, dmg)
        result:add_damage(target, dmg, "elemental")
    end)
end
