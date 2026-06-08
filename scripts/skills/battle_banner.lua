-- battle_banner.lua
function execute(ctx)
    local allies = ctx.allies
    local source = "battle_banner:" .. tostring(ctx.target_q) .. ":" .. tostring(ctx.target_r)
    result:add_anchor("battle_banner", source, ctx.target_q, ctx.target_r, 3, true, 10)
    for i = 0, allies.Length - 1 do
        local ally = allies[i]
        if unit:is_valid(ally) and hex:distance(ctx.target_q, ctx.target_r, ally.q, ally.r) <= 2 then
            buff:apply_custom(ally, "battle_banner", 3, { attack_bonus = 2, ac = 1 }, source)
        end
    end
end
