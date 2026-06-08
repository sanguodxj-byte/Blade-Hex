-- spellweaver_instant_glyph.lua
-- 奥战姿态 (三属性 STR+INT+DEX)
-- 给自己挂奥战 Buff: 命中使下次施法 DC+1, 施法使下次近战伤害+1d

function execute(ctx)
    -- 自身 buff 技能 (基线 3 回合)
    self_buff(ctx, "spellweave", 3)
end
