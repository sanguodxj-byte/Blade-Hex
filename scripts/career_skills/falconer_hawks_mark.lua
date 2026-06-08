-- falconer_hawks_mark.lua
-- 鹰眼锁定 (双属性 DEX+WIS)
-- 标记目标: 失去地形 AC 与掩护, 暴击阈值 -2

function execute(ctx)
    local tier = get_tier(ctx)

    -- 查找目标 (敌方)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    -- 应用 hawks_mark debuff (基线 3 回合)
    local duration = scale_duration(3, tier)
    buff:apply(target, "hawks_mark", duration)
    result:add_effect(target, "hawks_mark", duration)
end
