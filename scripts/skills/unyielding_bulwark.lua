-- unyielding_bulwark.lua
-- 不屈壁垒：获得护盾（减伤50%）2回合，并获得等级*骰 + CON 修正的临时生命

function execute(ctx)
    local caster = ctx.attacker
    local _, _, temp_hp = calc_skill_value(ctx, "unyielding_bulwark")

    buff:apply_custom(caster, "shield", 2, { damage_reduction_percent = 0.5 })
    buff:apply_custom(caster, "temp_hp", 2, { temp_hp_amount = temp_hp })
end
