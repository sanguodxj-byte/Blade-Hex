-- ironweaver_rune_barricade.lua
-- 符文壁垒 (三属性 STR+INT+CON)
-- 召唤符文壁垒 + 给自己挂铁焰壁垒光环 buff: 相邻友军 DR+1

function execute(ctx)
    local tier = get_tier(ctx)

    -- 召唤壁垒 (简化: 记录效果，由外层处理)
    result:add_effect(ctx.attacker, "rune_barricade_summon", scale_duration(3, tier))

    -- 给自己挂 rune_aura 光环 buff (基线 2 回合)
    self_buff(ctx, "rune_aura", 2)
end
