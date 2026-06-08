-- rogue_misdirection.lua
-- 声东击西 (双属性 DEX+CHA)
-- 自身隐身 + 给目标挂被骗 debuff

function execute(ctx)
    local tier = get_tier(ctx)

    -- 查找目标
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    -- 给自己隐身 (3 回合)
    local self_duration = scale_duration(3, tier)
    result:add_effect(ctx.attacker, "invisibility", self_duration)

    -- 给目标挂 misdirected debuff (2 回合)
    local target_duration = scale_duration(2, tier)
    buff:apply(target, "misdirected", target_duration)
    result:add_effect(target, "misdirected", target_duration)
end
