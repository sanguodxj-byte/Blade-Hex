-- warking_domination.lua
-- 战争之王 (四属性 STR+CON+CHA+DEX)
-- 友军士气 +18, 敌方士气 -18, 周围 3 格友军挂战争之王 buff

function execute(ctx)
    local tier = get_tier(ctx)
    local duration = scale_duration(3, tier)

    -- 友军士气 +18 (不缩放，绝对值)
    for i = 0, ctx.allies.Length - 1 do
        local ally = ctx.allies[i]
        if unit:is_valid(ally) then
            local dist = unit:distance(ctx.attacker, ally)
            if dist <= 4 then
                unit:change_morale(ally, 18)
            end
        end
    end

    -- 敌方士气 -18 (不缩放，绝对值)
    for i = 0, ctx.enemies.Length - 1 do
        local enemy = ctx.enemies[i]
        if unit:is_valid(enemy) then
            local dist = unit:distance(ctx.attacker, enemy)
            if dist <= 4 then
                unit:change_morale(enemy, -18)
            end
        end
    end

    -- 周围 3 格友军挂 war_king buff (攻击 +2, 伤害 +2, HP 上限 +5)
    for i = 0, ctx.allies.Length - 1 do
        local ally = ctx.allies[i]
        if unit:is_valid(ally) and ally ~= ctx.attacker then
            local dist = unit:distance(ctx.attacker, ally)
            if dist <= 3 then
                -- 动态伤害基于 STR
                local dmg_bonus = stat_mod_x_level(ctx.attacker.str, ctx.attacker.level, 0.15 * tier)
                local hp_bonus = stat_mod_x_level(ctx.attacker.con, ctx.attacker.level, 0.4 * tier)
                buff:apply_custom(ally, "war_king", duration, { damage = dmg_bonus, max_hp_bonus = hp_bonus })
                result:add_effect(ally, "war_king", duration)
            end
        end
    end
end
