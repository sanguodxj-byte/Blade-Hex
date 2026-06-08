-- trick_arrow.lua
-- 诡计箭：等级*骰 + DEX×0.75 伤害，随机附加致盲/眩晕/恐惧之一，持续1回合

function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    local _, _, dmg = calc_skill_value(ctx, "trick_arrow")
    unit:take_damage(target, dmg)
    result:add_damage(target, dmg)

    local debuffs = { "blind", "stun", "fear" }
    local roll = combat:roll_dice(1, 3)
    local chosen = debuffs[roll]
    buff:apply(target, chosen, 1)
end
