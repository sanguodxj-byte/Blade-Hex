-- cha_giant_apex.lua
-- Giant CHA apex: full-field cleanse and command aura until next own turn.

function execute(ctx)
    local function apply_to(ally, duration)
        remove_negative_effects(ally)
        buff:apply_custom(ally, "cha_giant_apex", duration, {
            attack_bonus = 3,
            damage = 0.50,
            critical_rate = 0.25,
            immune_fear = 1,
            immune_mind = 1,
            immune_negative = 1
        })
        result:add_effect(ally, "cha_giant_apex", duration)
    end

    apply_to(ctx.attacker, 1)

    local allies = ctx.allies
    for i = 0, allies.Length - 1 do
        local ally = allies[i]
        if unit:is_valid(ally) and ally.instance_id ~= ctx.attacker.instance_id then
            apply_to(ally, 2)
        end
    end
end
