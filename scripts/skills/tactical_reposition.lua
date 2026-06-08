-- tactical_reposition.lua
function execute(ctx)
    local target= require_ally(ctx.target_q, ctx.target_r)
    if not target then return end
    target.ap = target.ap + 4
    buff:apply_custom(target, "tactical_reposition", 1, { extra_ap = 4 })
    result:add_effect(target, "tactical_reposition", 1)
end
