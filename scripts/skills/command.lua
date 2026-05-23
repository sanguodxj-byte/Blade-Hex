-- command.lua
-- 指挥：为目标友军添加额外行动（需目标未使用额外行动）

function execute(ctx)
    local target = require_ally(ctx.target_q, ctx.target_r)
    if not target then return end

    if target.extra_actions ~= 0 then
        result:fail("目标已有额外行动")
        return
    end

    buff:apply_custom(target, "commanded", 1, { extra_action = true })
end
