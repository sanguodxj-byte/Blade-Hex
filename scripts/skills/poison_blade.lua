-- poison_blade.lua
-- 影刃涂毒：下次武器攻击命中时附加中毒3回合

function execute(ctx)
    buff:apply_custom(ctx.attacker, "poison_blade_next", 1, { next_hit_poison_duration = 3 })
    result:add_effect(ctx.attacker, "poison_blade_next", 1)
end
