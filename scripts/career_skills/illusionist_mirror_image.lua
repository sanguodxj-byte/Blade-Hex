-- illusionist_mirror_image.lua
-- 镜像分身 (三属性 INT+DEX+CHA)
-- 创建分身，分身存在时所有攻击者劣势

function execute(ctx)
    local tier = get_tier(ctx)

    -- 创建幻象 (简化: 记录效果，由外层处理)
    result:add_effect(ctx.attacker, "phantom_create", 1)

    -- 给自己挂 mirror_image buff (基线 3 回合)
    self_buff(ctx, "mirror_image", 3)
end
