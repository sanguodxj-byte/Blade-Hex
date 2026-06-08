-- veteran_old_timer.lua
-- 临危不乱 (双属性 STR+CON)
-- 恢复 25% HP + 给自己挂老兵 buff: ac +3, 反击范围 +6, 反击伤害 ×1.5

function execute(ctx)
    local tier = get_tier(ctx)

    -- 立即恢复 HP 上限 25%
    local heal_amount = percent_of_max_hp(ctx.attacker, 0.25)
    unit:heal(ctx.attacker, heal_amount)
    result:add_heal(ctx.attacker, heal_amount)

    -- 给自己挂 old_timer buff (基线 3 回合)
    self_buff(ctx, "old_timer", 3)
end
