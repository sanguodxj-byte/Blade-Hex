-- arcane_burst.lua
-- 奥术爆发：对目标造成等级*骰 + INT 修正的奥术伤害

function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    local _, _, dmg = calc_skill_value(ctx, "arcane_burst")

    -- 知识力量被动：额外加 INT 修正
    if ctx.attacker:has_skill("knowledge_power") then
        local int_mod = combat:get_stat_mod(ctx.attacker.intel)
        dmg = dmg + int_mod
    end

    unit:take_damage(target, dmg)
    result:add_damage(target, dmg, "arcane")
end
