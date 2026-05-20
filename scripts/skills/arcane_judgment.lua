-- arcane_judgment.lua
-- 奥术审判：单体3d10+WIS伤害，若拥有知识力量则额外+INT，类型奥术

function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    local wis_mod = combat:get_stat_mod(ctx.attacker.wis)
    local dmg = combat:roll_dice(3, 10) + wis_mod

    if ctx.attacker:has_skill("knowledge_power") then
        local int_mod = combat:get_stat_mod(ctx.attacker.intel)
        dmg = dmg + int_mod
    end

    unit:take_damage(target, dmg)
    result:add_damage(target, dmg, "arcane")
end
