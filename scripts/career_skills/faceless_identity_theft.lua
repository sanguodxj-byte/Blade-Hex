-- faceless_identity_theft.lua
-- 身份窃取 (三属性 DEX+INT+CHA)
-- 模仿敌方外观，AI 不主动攻击，挂伪装 buff

function execute(ctx)
    local tier = get_tier(ctx)

    -- 查找目标 (敌方)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    -- 给自己挂 disguise buff (AI 不主动攻击，攻击后失效)
    self_buff(ctx, "disguise", scale_duration(3, tier))
end
