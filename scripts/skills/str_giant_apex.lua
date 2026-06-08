-- str_giant_apex.lua
-- Giant STR apex: free action window; kills restore full AP until next own turn.

function execute(ctx)
    buff:apply_custom(ctx.attacker, "str_giant_apex", 1, { kill_full_ap_refund = 1 })
    result:add_effect(ctx.attacker, "str_giant_apex", 1)
end
