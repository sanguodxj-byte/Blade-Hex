-- chosenone_twist_of_fate.lua
-- 命运转折 (双属性 WIS+CHA)
-- 立即重掷下一个 d20 + 给自己挂天选 buff: attack +1, save +1

function execute(ctx)
    local tier = get_tier(ctx)

    -- 立即重掷下一个 d20 (标记，由 d20 系统消费)
    -- 注: result 中没有直接的 reroll API，通过 status_effects 传递
    result:add_effect(ctx.attacker, "reroll_next_d20", 1)

    -- 给自己挂 twist_fate buff (基线 2 回合)
    self_buff(ctx, "twist_fate", 2)
end
