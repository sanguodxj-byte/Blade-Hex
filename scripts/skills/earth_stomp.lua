-- earth_stomp.lua
function execute(ctx)
    local caster = ctx.attacker
    local enemies = ctx.enemies
    for i = 0, enemies.Length - 1 do
        local e = enemies[i]
        if unit:is_valid(e) and hex:distance(caster.q, caster.r, e.q, e.r) <= 1 then
            local r = result:resolve_attack(caster, e, { damage_mult = 0.8, node_flat_scale = 0.8 })
            result:add_attack(r, e)
            if r.hit then
                local saved = ability_save(caster, e, "con", "con")
                if not saved then
                    buff:apply(e, "stun", 1)
                    result:add_effect(e, "stun", 1)
                end
            end
        end
    end
end
