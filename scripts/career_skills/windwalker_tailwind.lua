-- windwalker_tailwind.lua
-- 顺风传递 (三属性 DEX+WIS+CHA)
-- 传送友军 + 给自己和友军挂顺风 buff

function execute(ctx)
    local tier = get_tier(ctx)

    -- 查找目标格的友军
    local ally = unit:find_at(ctx.target_q, ctx.target_r, "allies")

    if ally and unit:is_valid(ally) then
        -- 传送友军到施法者位置 (简化: 记录效果)
        result:add_effect(ally, "teleport_ally", 1)

        -- 给友军挂 tailwind buff
        local duration = scale_duration(2, tier)
        buff:apply(ally, "tailwind", duration)
        result:add_effect(ally, "tailwind", duration)
    end

    -- 给自己挂 tailwind buff
    self_buff(ctx, "tailwind", 2)
end
