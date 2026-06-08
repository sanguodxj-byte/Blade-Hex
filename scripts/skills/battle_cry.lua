-- battle_cry.lua
function execute(ctx)
    local caster = ctx.attacker
    local enemies = ctx.enemies
    for i = 0, enemies.Length - 1 do
        local e = enemies[i]
        if unit:is_valid(e) and hex:distance(caster.q, caster.r, e.q, e.r) <= 3 then
            buff:apply_custom(e, "battle_cry_shaken", 1, { attack_bonus = -4 })
            result:add_effect(e, "battle_cry_shaken", 1)
        end
    end
end
