-- hunter_stance.lua
function execute(ctx)
    enter_stance(ctx.attacker, "stance_hunter")
    buff:apply_custom(ctx.attacker, "stance_hunter", -1, { ranged_damage = 0.15, melee_damage = -0.20 })
    result:add_effect(ctx.attacker, "stance_hunter", -1)
end
