-- stonesaint_stone_body.lua
-- 石化之躯 (四属性 STR+CON+DEX+WIS)
-- 进入石化状态: 无法行动, DR+5, 免疫负面, HP 最低为 1

function execute(ctx)
    local tier = get_tier(ctx)

    -- 给自己挂 stone_body buff (基线 3 回合)
    -- DR 额外缩放
    local dr_bonus = scale_int(5, tier)
    buff:apply_custom(ctx.attacker, "stone_body", scale_duration(3, tier), { dr_threshold = dr_bonus })
    result:add_effect(ctx.attacker, "stone_body", scale_duration(3, tier))
end
