-- guardian_spirit.lua
-- 守护之灵：为目标友军添加守护之灵效果，持续3回合

function execute(ctx)
    local target = require_ally(ctx.target_q, ctx.target_r)
    if not target then return end

    result:add_effect(target, "guardian_spirit", 3)
end
