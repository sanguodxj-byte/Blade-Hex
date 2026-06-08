-- con_giant_apex.lua
-- Giant CON apex: active death-immunity marker consumed by BuffDamageHooks.

function execute(ctx)
    buff:apply_custom(ctx.attacker, "con_giant_apex", 3, { death_immunity = true })
    result:add_effect(ctx.attacker, "con_giant_apex", 3)
end
