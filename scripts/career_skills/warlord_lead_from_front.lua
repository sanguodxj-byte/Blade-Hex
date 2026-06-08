-- warlord_lead_from_front.lua
-- 身先士卒 (双属性 STR+CHA)
-- 冲锋 3 格 + 友军跟随移动 + 给自己挂先驱光环 buff

function execute(ctx)
    local tier = get_tier(ctx)

    -- 冲锋攻击 (简化: 直接攻击目标)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if target then
        local r = result:resolve_attack(ctx.attacker, target, { damage_mult = 1.0 })
        result:add_attack(r)
    end

    -- 周围 1 格友军跟随移动 (简化: 记录效果)
    for i = 0, ctx.allies.Length - 1 do
        local ally = ctx.allies[i]
        if unit:is_valid(ally) and ally ~= ctx.attacker then
            local dist = unit:distance(ctx.attacker, ally)
            if dist <= 1 then
                -- 友军向同方向移动 1 格 (由外层处理)
                result:add_effect(ally, "ally_followup", 1)
            end
        end
    end

    -- 给自己挂 lead_front 光环 buff (基线 2 回合)
    self_buff(ctx, "lead_front", 2)
end
