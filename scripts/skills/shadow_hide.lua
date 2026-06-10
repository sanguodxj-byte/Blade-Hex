-- shadow_hide.lua
function execute(ctx)
    buff:apply_custom(ctx.attacker, "shadow_hide", 4, { ranged_hit_taken = -0.50, ignore_zoc = 1, no_aoo_on_move = 1 })
    result:add_effect(ctx.attacker, "shadow_hide", 4)
end
