-- dex_giant_apex.lua
-- Giant DEX apex: ranged hits refund attack AP up to 5 times; kills refresh AP for movement.

function execute(ctx)
    buff:apply_custom(ctx.attacker, "dex_giant_apex", 1, { refund_ranged_action = 5 })
    result:add_effect(ctx.attacker, "dex_giant_apex", 1)
end
