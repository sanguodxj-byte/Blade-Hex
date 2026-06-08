-- field_medic.lua
-- 战地医疗：治疗目标等级*骰+WIS，移除流血和中毒

function execute(ctx)
    local target = require_ally(ctx.target_q, ctx.target_r)
    if not target then return end

    local _, _, heal = calc_skill_value(ctx, "field_medic")
    local actual = unit:heal(target, heal)
    result:add_heal(target, actual)
    buff:remove_many(target, { "bleed", "poison" })
    result:add_remove_effect(target, { "bleed", "poison" })
end
