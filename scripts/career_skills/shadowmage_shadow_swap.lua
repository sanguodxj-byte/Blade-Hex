-- shadowmage_shadow_swap.lua
-- 暗影置换 (三属性 INT+DEX+CHA)
-- 传送 + 给自己挂暗影 buff: 每回合首次移动可瞬移

function execute(ctx)
    local tier = get_tier(ctx)

    -- 传送到目标格
    unit:teleport(ctx.attacker, ctx.target_q, ctx.target_r)

    -- 给自己挂 shadow_form buff (基线 3 回合)
    self_buff(ctx, "shadow_form", 3)
end
