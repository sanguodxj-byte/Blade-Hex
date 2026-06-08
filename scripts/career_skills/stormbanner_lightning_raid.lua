-- stormbanner_lightning_raid.lua
-- 闪电突击 (四属性 STR+DEX+INT+CHA)
-- 攻击 + 友军获免费攻击 + 给周围友军挂风暴战旗 buff

function execute(ctx)
    local tier = get_tier(ctx)

    -- 攻击目标
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if target then
        local r = result:resolve_attack(ctx.attacker, target, { advantage = true, damage_mult = scale_float(1.25, tier) })
        result:add_attack(r)
    end

    -- 周围 2 格友军获免费攻击 (简化: 记录效果)
    local duration = scale_duration(2, tier)
    for i = 0, ctx.allies.Length - 1 do
        local ally = ctx.allies[i]
        if unit:is_valid(ally) and ally ~= ctx.attacker then
            local dist = unit:distance(ctx.attacker, ally)
            if dist <= 2 then
                buff:apply(ally, "storm_banner", duration)
                result:add_effect(ally, "storm_banner", duration)
            end
        end
    end
end
