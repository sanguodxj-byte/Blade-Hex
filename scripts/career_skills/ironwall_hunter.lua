-- ironwall_hunter.lua
-- 追迹猎杀 (四属性 STR+DEX+CON+WIS)
-- 标记目标 + 远程射击 + 给自己挂追迹猎手 buff

function execute(ctx)
    local tier = get_tier(ctx)

    -- 查找目标
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    -- 标记目标 (记录到结果)
    result:add_effect(target, "marked", scale_duration(3, tier))

    -- 远程射击
    local r = result:resolve_attack(ctx.attacker, target, { advantage = true })
    result:add_attack(r)

    -- 命中后额外伤害 (等级 / 2 * tier)
    if r and r.hit then
        local extra_dmg = math.ceil(ctx.attacker.level / 2.0 * tier)
        unit:take_damage(target, extra_dmg)
        result:add_damage(target, extra_dmg, "pierce")
    end

    -- 给自己挂 sky_hunter buff
    local duration = scale_duration(3, tier)
    buff:apply_custom(ctx.attacker, "sky_hunter", duration, { damage = math.ceil(ctx.attacker.level / 2.0 * tier) })
    result:add_effect(ctx.attacker, "sky_hunter", duration)
end
