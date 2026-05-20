-- basic_heal.lua
-- 基础治疗：治疗目标格友军 1d8 + WIS 修正

function execute(ctx)
    local target = require_ally(ctx.target_q, ctx.target_r)
    if not target then return end

    local wis_mod = combat:get_stat_mod(ctx.attacker.wis)
    local heal = combat:roll_dice(1, 8) + wis_mod
    local actual = unit:heal(target, heal)
    result:add_heal(target, actual)
end
