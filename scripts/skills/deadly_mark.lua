-- deadly_mark.lua
function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end
    buff:apply_custom(target, "deadly_mark", 99, { critical_rate_taken = 0.20 }, "marker:" .. tostring(ctx.attacker.instance_id))
    result:add_effect(target, "deadly_mark", 99)
end
