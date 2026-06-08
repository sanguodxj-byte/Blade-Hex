-- ironcommander_hold_the_line.lua
-- 坚守阵线 (双属性 STR+CON)
-- 光环 buff 给周围 2 格友军: ac +2, immune_fear, morale_floor -20

function execute(ctx)
    local tier = get_tier(ctx)
    local duration = scale_duration(2, tier)

    -- 遍历友军，给距离 2 以内的友军挂 buff
    for i = 0, ctx.allies.Length - 1 do
        local ally = ctx.allies[i]
        if unit:is_valid(ally) then
            local dist = unit:distance(ctx.attacker, ally)
            if dist <= 2 then
                buff:apply(ally, "hold_line", duration)
                result:add_effect(ally, "hold_line", duration)
            end
        end
    end
end
