-- bladedancer_whirling_strike.lua
-- 连旋斩 (双属性 DEX+INT)
-- 正面 3 格扇形攻击 (70% 伤害) + 反击范围扩展 buff

function execute(ctx)
    local tier = get_tier(ctx)

    -- 获取施法者朝向
    local facing = ctx.attacker.facing or 0

    -- 扇形 3 方向: 正面, 左前, 右前
    local fan_dirs = { facing, (facing + 5) % 6, (facing + 1) % 6 }

    -- 对每个方向的敌人做攻击 (70% 伤害)
    for _, dir in ipairs(fan_dirs) do
        local pos = unit:get_neighbor(ctx.attacker.q, ctx.attacker.r, dir)
        local target = unit:find_at(pos.X, pos.Y, "enemies")
        if target and unit:is_valid(target) then
            local r = result:resolve_attack(ctx.attacker, target, { damage_mult = 0.7 })
            result:add_attack(r)
        end
    end

    -- 给自己挂 blade_dance buff (反击范围扩展)
    self_buff(ctx, "blade_dance", 2)
end
