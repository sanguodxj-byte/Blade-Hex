-- guard_stance.lua
function execute(ctx)
    enter_stance(ctx.attacker, "stance_guard")
    buff:apply_custom(ctx.attacker, "stance_guard", -1, { damage_taken = -0.15, damage = -0.15 })
    result:add_effect(ctx.attacker, "stance_guard", -1)
end
