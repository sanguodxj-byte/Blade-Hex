-- stargazer_star_map.lua
-- 星图定位 (三属性 INT+WIS+CHA)
-- 传送 + 给自己挂星图 buff: 每回合开始可免费瞬移

function execute(ctx)
    local tier = get_tier(ctx)

    -- 传送到目标格
    unit:teleport(ctx.attacker, ctx.target_q, ctx.target_r)

    -- 给自己挂 star_map buff (基线 3 回合)
    self_buff(ctx, "star_map", 3)
end
