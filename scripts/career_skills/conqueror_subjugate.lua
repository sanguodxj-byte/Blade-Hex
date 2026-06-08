-- conqueror_subjugate.lua
-- 镇压 (三属性 STR+CHA+CON)
-- 近战攻击 + 恐惧效果 + 士气大减

function execute(ctx)
    local tier = get_tier(ctx)

    -- 查找目标
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    -- 执行近战攻击
    local r = result:resolve_attack(ctx.attacker, target, { damage_mult = 1.0 })
    result:add_attack(r)

    -- 命中后恐惧 + 士气大减
    if r and r.hit then
        -- 恐惧效果 (3 回合)
        local duration = scale_duration(3, tier)
        buff:apply(target, "fear", duration)
        result:add_effect(target, "fear", duration)

        -- 士气 -15 (不缩放，绝对值)
        unit:change_morale(target, -15)
    end
end
