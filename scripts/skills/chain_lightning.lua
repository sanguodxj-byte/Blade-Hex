-- chain_lightning.lua
-- 连锁闪电：对目标造成等级*骰+INT 伤害，跳跃相邻敌人（递减骰数+INT）

function execute(ctx)
    local first = require_enemy(ctx.target_q, ctx.target_r)
    if not first then return end

    local int_mod = combat:get_stat_mod(ctx.attacker.intel)

    -- 主目标
    local dice_count, dice_sides, dmg1 = calc_skill_value(ctx, "chain_lightning")
    unit:take_damage(first, dmg1)
    result:add_damage(first, dmg1, "lightning")

    -- 跳跃目标（递减骰数，最少1骰）
    local last_q, last_r = first.q, first.r
    local hit_set = { [first.q .. "," .. first.r] = true }
    local jumps = 0
    local chain_dice = math.max(1, dice_count - 1)

    local enemies = ctx.enemies
    for i = 0, enemies.Length - 1 do
        if jumps >= 2 then break end
        local enemy = enemies[i]
        if not unit:is_valid(enemy) then goto continue end

        local key = enemy.q .. "," .. enemy.r
        if hit_set[key] then goto continue end

        local dist = hex:distance(last_q, last_r, enemy.q, enemy.r)
        if dist <= 2 then
            local dmg = combat:roll_dice(chain_dice, dice_sides) + int_mod
            unit:take_damage(enemy, dmg)
            result:add_damage(enemy, dmg, "lightning")
            hit_set[key] = true
            last_q, last_r = enemy.q, enemy.r
            jumps = jumps + 1
        end

        ::continue::
    end
end
