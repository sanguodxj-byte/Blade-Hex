-- guardian_link.lua
function execute(ctx)
    local target = require_ally(ctx.target_q, ctx.target_r)
    if not target then return end
    buff:remove(target, "guardian_link")
    buff:apply_custom(target, "guardian_link", 3, { damage_redirect_percent = 0.5 }, "guardian:" .. tostring(ctx.attacker.instance_id))
    result:add_effect(target, "guardian_link", 3)
end
