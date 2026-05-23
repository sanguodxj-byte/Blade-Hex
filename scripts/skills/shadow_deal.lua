-- shadow_deal.lua
-- 暗影交易：贿赂尝试，DC=10+CHA修正+熟练 vs 目标WIS豁免，失败则附加贿赂效果

function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    local caster = ctx.attacker
    local cha_mod = combat:get_stat_mod(caster.cha)
    local prof = combat:get_proficiency(caster.level)
    local dc = 10 + cha_mod + prof

    local wis_mod = combat:get_stat_mod(target.wis)
    local target_prof = combat:get_proficiency(target.level)
    local save_roll = combat:roll_dice(1, 20) + wis_mod + target_prof

    if save_roll < dc then
        buff:apply_custom(target, "bribed", 99, { ai_ignore = true })
    else
        result:fail("目标抵抗了贿赂")
    end
end
