-- prophet_fate_intervention.lua
-- 命运干涉 (双属性 WIS+CHA)
-- 给目标友军挂命运庇护 buff: 致死伤害骰强制重掷取低

function execute(ctx)
    local tier = get_tier(ctx)

    -- 查找目标友军
    local target = require_ally(ctx.target_q, ctx.target_r)
    if not target then return end

    -- 给目标挂 fate_protect buff (基线 3 回合)
    local duration = scale_duration(3, tier)
    buff:apply(target, "fate_protect", duration)
    result:add_effect(target, "fate_protect", duration)
end
