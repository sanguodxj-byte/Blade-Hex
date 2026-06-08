-- silentdeath_silent_strike.lua
-- 无声击 (四属性 DEX+INT+CHA+WIS)
-- 隐身 + 给自己挂无声击 buff: 潜行不破隐, 首次伤害 +50%

function execute(ctx)
    local tier = get_tier(ctx)
    local duration = scale_duration(3, tier)

    -- 给自己隐身
    result:add_effect(ctx.attacker, "invisibility", duration)

    -- 给自己挂 silent_strike buff
    buff:apply(ctx.attacker, "silent_strike", duration)
    result:add_effect(ctx.attacker, "silent_strike", duration)
end
