-- basic_heal.lua
-- 基础治疗：治疗目标格友军等级*骰 + WIS 修正

function execute(ctx)
    local target = require_ally(ctx.target_q, ctx.target_r)
    if not target then return end

    local _, _, heal = calc_skill_value(ctx, "basic_heal")
    local actual = unit:heal(target, heal)
    result:add_heal(target, actual)
end
