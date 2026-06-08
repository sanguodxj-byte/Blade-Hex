-- warband_inspiration.lua
function execute(ctx)
    local caster = ctx.attacker
    local allies = ctx.allies
    for i = 0, allies.Length - 1 do
        local ally = allies[i]
        if unit:is_valid(ally) and hex:distance(caster.q, caster.r, ally.q, ally.r) <= 2 then
            buff:apply_custom(ally, "warband_inspiration", 2, {
                temp_hp_amount = percent_of_max_hp(ally, 0.10)
            })
            result:add_effect(ally, "warband_inspiration", 2)
        end
    end
end
