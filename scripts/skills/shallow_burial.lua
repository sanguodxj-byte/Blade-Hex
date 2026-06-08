-- shallow_burial.lua
-- 薄葬：使目标友军在1回合内免疫死亡，HP可降至负数
-- 替换原 resurrect

function execute(ctx)
    local target = require_ally(ctx.target_q, ctx.target_r)
    if not target then return end

    buff:apply_custom(target, "shallow_burial", 1, { death_immunity = true })
    result:add_effect(target, "shallow_burial", 1)
end
