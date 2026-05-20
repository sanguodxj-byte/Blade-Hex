-- field_medic.lua
-- 战地医疗：治疗目标2d8+WIS，移除流血和中毒

function execute(ctx)
    local target = require_ally(ctx.target_q, ctx.target_r)
    if not target then return end

    local wis_mod = combat:get_stat_mod(ctx.attacker.wis)
    local heal = combat:roll_dice(2, 8) + wis_mod
    local actual = unit:heal(target, heal)
    result:add_heal(target, actual)
    result:add_remove_effect(target, { "bleed", "poison" })
end
