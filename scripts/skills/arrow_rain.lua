-- arrow_rain.lua
function execute(ctx)
    local enemies = ctx.enemies
    for i = 0, enemies.Length - 1 do
        local e = enemies[i]
        if unit:is_valid(e) and hex:distance(ctx.target_q, ctx.target_r, e.q, e.r) <= 2 then
            local r = result:resolve_attack(ctx.attacker, e, { damage_mult = 0.6, node_flat_scale = 0.6 })
            result:add_attack(r, e)
        end
    end
end
