-- blessing.lua
-- 祝福：为目标友军添加祝福效果，持续3回合

function execute(ctx)
    local target = require_ally(ctx.target_q, ctx.target_r)
    if not target then return end

    buff:apply(target, "bless", 3)
end
