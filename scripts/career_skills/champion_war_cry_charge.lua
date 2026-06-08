-- champion_war_cry_charge.lua
-- 战吼冲锋 (三属性 STR+CHA+CON)
-- 冲锋攻击 + 友军士气大增 + 挂攻击优势 Buff，敌方士气大减

function execute(ctx)
    local tier = get_tier(ctx)

    -- 冲锋攻击
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if target then
        local r = result:resolve_attack(ctx.attacker, target, { advantage = true, damage_mult = scale_float(1.25, tier) })
        result:add_attack(r)
    end

    -- 友军士气 +12 (不缩放，绝对值)
    for i = 0, ctx.allies.Length - 1 do
        local ally = ctx.allies[i]
        if unit:is_valid(ally) then
            local dist = unit:distance(ctx.attacker, ally)
            if dist <= 4 then
                unit:change_morale(ally, 12)
            end
            -- 距离 3 以内的友军挂 champion_aura buff
            if dist <= 3 and ally ~= ctx.attacker then
                local duration = scale_duration(2, tier)
                buff:apply(ally, "champion_aura", duration)
                result:add_effect(ally, "champion_aura", duration)
            end
        end
    end

    -- 敌方士气 -8 (不缩放，绝对值)
    for i = 0, ctx.enemies.Length - 1 do
        local enemy = ctx.enemies[i]
        if unit:is_valid(enemy) then
            local dist = unit:distance(ctx.attacker, enemy)
            if dist <= 4 then
                unit:change_morale(enemy, -8)
            end
        end
    end
end
