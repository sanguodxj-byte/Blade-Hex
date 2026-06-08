-- command_intimidate.lua
function execute(ctx)
    local caster = ctx.attacker
    local enemies = ctx.enemies
    for i = 0, enemies.Length - 1 do
        local e = enemies[i]
        if unit:is_valid(e) and hex:distance(caster.q, caster.r, e.q, e.r) <= 3 then
            local duration = 3
            local saved = ability_save(caster, e, "cha", "wis")
            if saved then duration = 2 end
            buff:apply_custom(e, "command_intimidated", duration, { attack_bonus = -2 })
            result:add_effect(e, "command_intimidated", duration)
        end
    end
end
