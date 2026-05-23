-- shadow_clone.lua
-- 影分身：为自身添加幻影效果，持续3回合

function execute(ctx)
    buff:apply_custom(ctx.attacker, "phantom", 3, { phantom_ac = 12, phantom_count = 1, redirect_chance = 1.0 })
end
