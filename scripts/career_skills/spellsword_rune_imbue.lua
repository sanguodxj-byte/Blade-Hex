-- spellsword_rune_imbue.lua
-- 符文武器 (双属性 STR+INT)
-- 给自己挂符文武器 buff: 近战攻击额外 1d6 火焰, 命中燃烧 1d6

function execute(ctx)
    -- 自身 buff 技能 (基线 3 回合)
    self_buff(ctx, "rune_imbue", 3)
end
