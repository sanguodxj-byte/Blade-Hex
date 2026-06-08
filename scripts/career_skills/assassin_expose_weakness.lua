-- assassin_expose_weakness.lua
-- 致命标记 (单属性 DEX)
-- 标记敌方目标，挂致命标记 debuff: crit_threshold -3

function execute(ctx)
    local tier = get_tier(ctx)

    -- 查找目标（敌方）
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    -- 应用 debuff
    local duration = scale_duration(2, tier)
    buff:apply(target, "death_mark", duration)
    result:add_effect(target, "death_mark", duration)
end
