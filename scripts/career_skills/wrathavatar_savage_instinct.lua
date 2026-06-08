-- wrathavatar_savage_instinct.lua
-- 怒涛战神 (四属性 STR+CON+DEX+CHA)
-- 攻击 HP 最低敌方 (必中) + 给自己挂野蛮 buff

function execute(ctx)
    local tier = get_tier(ctx)

    -- 找 HP 最低的敌方 (范围 4 格)
    local lowest_hp_enemy = nil
    local min_hp = math.huge
    for i = 0, ctx.enemies.Length - 1 do
        local enemy = ctx.enemies[i]
        if unit:is_valid(enemy) then
            local dist = unit:distance(ctx.attacker, enemy)
            if dist <= 4 and enemy.hp < min_hp then
                min_hp = enemy.hp
                lowest_hp_enemy = enemy
            end
        end
    end

    -- 攻击 (必中)
    if lowest_hp_enemy then
        local r = result:resolve_attack(ctx.attacker, lowest_hp_enemy, { advantage = true })
        r.hit = true -- 必中 hack
        result:add_attack(r)
    end

    -- 给自己挂 savage buff (基线 4 回合)
    self_buff(ctx, "savage", 4)
end
