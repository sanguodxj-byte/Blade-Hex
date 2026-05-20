-- shadow_clone.lua
-- 影分身：为自身添加幻影效果，持续3回合

function execute(ctx)
    result:add_effect(ctx.attacker, "phantom", 3)
end
