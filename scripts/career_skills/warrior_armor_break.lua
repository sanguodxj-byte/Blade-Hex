-- warrior_armor_break.lua
-- 碎甲打击 (单属性 STR)
-- 近战攻击命中后挂碎甲 debuff: dr_threshold -3, ac -1

function execute(ctx)
    local tier = get_tier(ctx)

    -- 查找目标
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    -- 执行近战攻击
    local r = result:resolve_attack(ctx.attacker, target, { damage_mult = 1.0 })
    result:add_attack(r)

    -- 命中后应用 debuff
    if r and r.hit then
        local duration = scale_duration(2, tier)
        buff:apply(target, "armor_break", duration)
        result:add_effect(target, "armor_break", duration)
    end
end
