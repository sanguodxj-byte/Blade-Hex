-- gathering_order.lua
function execute(ctx)
    local allies = ctx.allies
    for i = 0, allies.Length - 1 do
        local ally = allies[i]
        if unit:is_valid(ally) then
            buff:apply_custom(ally, "gathering_order", 1, { attack_bonus = 2, damage = 0.10 })
            result:add_effect(ally, "gathering_order", 1)
        end
    end
end
