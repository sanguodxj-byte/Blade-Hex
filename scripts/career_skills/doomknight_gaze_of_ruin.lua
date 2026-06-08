-- doomknight_gaze_of_ruin.lua
-- 毁灭凝视 (三属性 STR+CON+CHA)
-- 标记目标: 被攻击时暴击阈值固定为 12

function execute(ctx)
    local tier = get_tier(ctx)

    -- 查找目标 (敌方)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    -- 应用 gaze_ruin debuff (基线 3 回合)
    local duration = scale_duration(3, tier)
    buff:apply(target, "gaze_ruin", duration)
    result:add_effect(target, "gaze_ruin", duration)
end
