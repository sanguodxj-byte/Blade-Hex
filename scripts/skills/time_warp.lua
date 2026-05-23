-- time_warp.lua
-- 时间扭曲：消耗10魔力，为自身添加加速效果1回合

function execute(ctx)
    if not check_mana(ctx.attacker, 10) then return end
    buff:apply(ctx.attacker, "haste", 1)
end
