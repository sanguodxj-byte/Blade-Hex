-- arcane_bomb.lua
-- 奥术炸弹：AOE区域，基础伤害3d6+INT，每个目标受到 max(1, base/2 + 1d4)（知识力量则+INT），类型arcane

function execute(ctx)
    local int_mod = combat:get_stat_mod(ctx.attacker.intel)
    local base_dmg = combat:roll_dice(3, 6) + int_mod
    local half_base = math.floor(base_dmg / 2)

    aoe_area(ctx.target_q, ctx.target_r, "enemies", function(target)
        local bonus = combat:roll_dice(1, 4)
        local dmg = half_base + bonus
        if ctx.attacker:has_effect("knowledge_power") then
            dmg = dmg + int_mod
        end
        dmg = math.max(1, dmg)
        unit:take_damage(target, dmg)
        result:add_damage(target, dmg, "arcane")
    end)
end
