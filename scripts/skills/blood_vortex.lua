-- blood_vortex.lua
function execute(ctx)
    local caster = ctx.attacker
    local enemies = ctx.enemies
    local healed = 0
    for i = 0, enemies.Length - 1 do
        local e = enemies[i]
        if unit:is_valid(e) then
            local d = hex:distance(caster.q, caster.r, e.q, e.r)
            if d <= 2 then
                local r = result:resolve_attack(caster, e, { damage_mult = 1.0 })
                result:add_attack(r, e)
                if r.hit and d == 2 then
                    healed = healed + unit:heal(caster, percent_of_max_hp(caster, 0.05))
                end
            end
        end
    end
    if healed > 0 then result:add_heal(caster, healed) end
end
