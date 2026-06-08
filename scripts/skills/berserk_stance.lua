-- berserk_stance.lua
function execute(ctx)
    enter_stance(ctx.attacker, "stance_berserk")
    buff:apply_custom(ctx.attacker, "stance_berserk", -1, { damage = 0.15, damage_taken = 0.10 })
    result:add_effect(ctx.attacker, "stance_berserk", -1)
end
