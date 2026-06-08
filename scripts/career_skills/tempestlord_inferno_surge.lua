-- tempestlord_inferno_surge.lua
-- 烈焰喷涌 (四属性 STR+INT+CON+DEX)
-- AOE 火焰伤害 (等级骰 d6) + 燃烧 + 恢复法力

function execute(ctx)
    local tier = get_tier(ctx)

    -- 等级骰 d6 火焰伤害
    local count, sides = get_level_dice(ctx.attacker.level, 6)
    local fire_dice = scale_dice(count, tier)

    -- AOE 范围 2 格敌方
    local hits = 0
    for i = 0, ctx.enemies.Length - 1 do
        local enemy = ctx.enemies[i]
        if unit:is_valid(enemy) then
            local dist = unit:distance(ctx.attacker, enemy)
            if dist <= 2 then
                local dmg = combat:roll_dice(fire_dice, 6)
                unit:take_damage(enemy, dmg)
                result:add_damage(enemy, dmg, "fire")

                -- 燃烧效果 (2 回合)
                local burn_duration = scale_duration(2, tier)
                buff:apply(enemy, "burning", burn_duration)
                result:add_effect(enemy, "burning", burn_duration)

                hits = hits + 1
            end
        end
    end

    -- 每命中一个敌人恢复法力
    if hits > 0 then
        local mana_recover = hits * scale_int(5, tier)
        ctx.attacker.mana = math.min(ctx.attacker.max_mana, ctx.attacker.mana + mana_recover)
        result:add_heal(ctx.attacker, mana_recover)
    end
end
