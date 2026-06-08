-- morale_boost.lua
function execute(ctx)
    local caster = ctx.attacker
    local allies = ctx.allies
    for i = 0, allies.Length - 1 do
        local ally = allies[i]
        if unit:is_valid(ally) and hex:distance(caster.q, caster.r, ally.q, ally.r) <= 2 then
            buff:apply_custom(ally, "morale_boost", 2, { attack_bonus = 2 })
            result:add_effect(ally, "morale_boost", 2)
        end
    end
end
