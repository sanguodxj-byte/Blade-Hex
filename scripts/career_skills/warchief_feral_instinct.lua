-- warchief_feral_instinct.lua
-- 野性直觉 (三属性 STR+CON+WIS)
-- 给自己挂野性 buff + 周围 3 格友军挂狼群 buff

function execute(ctx)
    local tier = get_tier(ctx)
    local duration = scale_duration(3, tier)

    -- 给自己挂 feral_self buff (移动力 +2, 侦测潜行, 击兽伤害 +2)
    buff:apply(ctx.attacker, "feral_self", duration)
    result:add_effect(ctx.attacker, "feral_self", duration)

    -- 周围 3 格友军挂 wolf_pack buff (对野兽攻击优势, 移耗 -1)
    for i = 0, ctx.allies.Length - 1 do
        local ally = ctx.allies[i]
        if unit:is_valid(ally) and ally ~= ctx.attacker then
            local dist = unit:distance(ctx.attacker, ally)
            if dist <= 3 then
                buff:apply(ally, "wolf_pack", duration)
                result:add_effect(ally, "wolf_pack", duration)
            end
        end
    end
end
