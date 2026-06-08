-- unyielding_stand.lua
function execute(ctx)
    local caster = ctx.attacker
    local healed = unit:heal(caster, percent_of_max_hp(caster, 0.25))
    result:add_heal(caster, healed)
    buff:apply_custom(caster, "unyielding_stand", 1, { damage_taken = -0.30 })
    result:add_effect(caster, "unyielding_stand", 1)
end
