-- dreadgeneral_iron_grip.lua
-- 铁腕统御 (四属性 STR+CON+CHA+WIS)
-- 给周围 3 格友军挂铁腕统御 buff: 免疫恐惧, 士气保底 -30, 攻击 +2, AC+1

function execute(ctx)
    local tier = get_tier(ctx)
    local duration = scale_duration(3, tier)

    -- 周围 3 格友军挂 iron_grip buff
    for i = 0, ctx.allies.Length - 1 do
        local ally = ctx.allies[i]
        if unit:is_valid(ally) and ally ~= ctx.attacker then
            local dist = unit:distance(ctx.attacker, ally)
            if dist <= 3 then
                -- AC 加成基于 CON 修正
                local con_mod = combat:get_stat_mod(ctx.attacker.con)
                local ac_bonus = math.max(1, math.ceil(con_mod / 2.0 * tier))
                buff:apply_custom(ally, "iron_grip", duration, { ac = ac_bonus })
                result:add_effect(ally, "iron_grip", duration)
            end
        end
    end
end
