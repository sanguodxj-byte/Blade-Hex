-- archsage_omnibus.lua
-- 万卷通鉴 (三属性 INT+WIS+CHA)
-- 揭示敌方最低豁免 + 给全场友军挂万卷通鉴 buff

function execute(ctx)
    local tier = get_tier(ctx)
    local duration = scale_duration(3, tier)

    -- 揭示敌方最低豁免 (简化: 记录信息)
    for i = 0, ctx.enemies.Length - 1 do
        local enemy = ctx.enemies[i]
        if unit:is_valid(enemy) then
            -- 计算最低豁免
            local fort = enemy.con
            local reflex = enemy.dex
            local will = enemy.wis
            local lowest_save = "fortitude"
            local lowest_val = fort
            if reflex < lowest_val then
                lowest_val = reflex
                lowest_save = "reflex"
            end
            if will < lowest_val then
                lowest_val = will
                lowest_save = "will"
            end
            -- 记录到结果
            result:add_effect(enemy, "lowest_save_" .. lowest_save, 1)
        end
    end

    -- 给全场友军挂 omnibus buff (命中 +2, 豁免 +2)
    for i = 0, ctx.allies.Length - 1 do
        local ally = ctx.allies[i]
        if unit:is_valid(ally) then
            buff:apply(ally, "omnibus", duration)
            result:add_effect(ally, "omnibus", duration)
        end
    end
end
