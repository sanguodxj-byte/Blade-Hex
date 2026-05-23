-- unyielding_bulwark.lua
-- 不屈壁垒：获得护盾（减伤50%）2回合，并获得临时生命2d6持续2回合

function execute(ctx)
    local caster = ctx.attacker
    local temp_hp = combat:roll_dice(2, 6)

    buff:apply_custom(caster, "shield", 2, { damage_reduction_percent = 0.5 })
    buff:apply_custom(caster, "temp_hp", 2, { temp_hp_amount = temp_hp })
end
