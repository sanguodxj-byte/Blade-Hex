-- chain_lightning.lua
-- 连锁闪电：对目标造成 3d6+INT 伤害，跳跃最多 2 个相邻敌人（2d6+INT）

function execute(ctx)
    local first = require_enemy(ctx.target_q, ctx.target_r)
    if not first then return end

    local int_mod = combat:get_stat_mod(ctx.attacker.intel)

    -- 主目标
    local dmg1 = combat:roll_dice(3, 6) + int_mod
    unit:take_damage(first, dmg1)
    result:add_damage(first, dmg1, "lightning")

    -- 跳跃目标
    local last_q, last_r = first.q, first.r
    local hit_set = { [first.q .. "," .. first.r] = true }
    local jumps = 0

    local enemies = ctx.enemies
    for i = 0, enemies.Length - 1 do
        if jumps >= 2 then break end
        local enemy = enemies[i]
        if not unit:is_valid(enemy) then goto continue end

        local key = enemy.q .. "," .. enemy.r
        if hit_set[key] then goto continue end

        local dist = hex:distance(last_q, last_r, enemy.q, enemy.r)
        if dist <= 2 then
            local dmg = combat:roll_dice(2, 6) + int_mod
            unit:take_damage(enemy, dmg)
            result:add_damage(enemy, dmg, "lightning")
            hit_set[key] = true
            last_q, last_r = enemy.q, enemy.r
            jumps = jumps + 1
        end

        ::continue::
    end
end
