-- fearless_charge.lua
function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    local caster = ctx.attacker
    local dir = -1
    for i = 0, 5 do
        local step = hex:get_neighbor(caster.q, caster.r, i)
        local dq = step.X - caster.q
        local dr = step.Y - caster.r
        local current_q = caster.q
        local current_r = caster.r
        while current_q ~= target.q or current_r ~= target.r do
            current_q = current_q + dq
            current_r = current_r + dr
            if hex:distance(caster.q, caster.r, current_q, current_r) > hex:distance(caster.q, caster.r, target.q, target.r) then
                break
            end
        end
        if current_q == target.q and current_r == target.r then
            dir = i
            break
        end
    end

    if dir < 0 then
        result:fail("无畏冲锋需要直线路径")
        return
    end

    local reverse_dir = (dir + 3) % 6
    local landing = hex:get_neighbor(target.q, target.r, reverse_dir)
    if landing.X == caster.q and landing.Y == caster.r then
        result:fail("冲锋距离不足")
        return
    end
    if not unit:can_push_to(landing.X, landing.Y) then
        result:fail("冲锋落点被阻挡")
        return
    end

    local enemies = ctx.enemies
    local path_q = caster.q
    local path_r = caster.r
    local direction = hex:get_neighbor(caster.q, caster.r, dir)
    local dq = direction.X - caster.q
    local dr = direction.Y - caster.r
    while path_q ~= landing.X or path_r ~= landing.Y do
        path_q = path_q + dq
        path_r = path_r + dr
        for i = 0, enemies.Length - 1 do
            local e = enemies[i]
            if unit:is_valid(e) and e.instance_id ~= target.instance_id and e.q == path_q and e.r == path_r then
                local path_hit = result:resolve_attack(ctx.attacker, e, { damage_mult = 0.5 })
                result:add_attack(path_hit, e)
            end
        end
    end

    unit:teleport(caster, landing.X, landing.Y)
    local r = result:resolve_attack(ctx.attacker, target, { damage_mult = 0.5 })
    result:add_attack(r, target)
    if r.hit then
        buff:apply(target, "stun", 1)
        result:add_effect(target, "stun", 1)
    end
end
