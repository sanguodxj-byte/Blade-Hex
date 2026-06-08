-- bard_battle_hymn.lua
-- 军魂战歌 (单属性 CHA)
-- 光环 buff 给周围 3 格友军: move_ap_reduction +1, ac +1, damage +10%

function execute(ctx)
    local tier = get_tier(ctx)
    local duration = scale_duration(2, tier)

    -- 遍历友军，给距离 3 以内的友军挂 buff
    for i = 0, ctx.allies.Length - 1 do
        local ally = ctx.allies[i]
        if unit:is_valid(ally) then
            local dist = unit:distance(ctx.attacker, ally)
            if dist <= 3 then
                buff:apply(ally, "battle_hymn", duration)
                result:add_effect(ally, "battle_hymn", duration)
            end
        end
    end
end
