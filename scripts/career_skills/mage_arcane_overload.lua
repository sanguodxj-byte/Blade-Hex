-- mage_arcane_overload.lua
-- 以太过载 (单属性 INT)
-- 给自己挂过载 buff: next_spell_dc_bonus +5

function execute(ctx)
    -- 自身 buff 技能
    self_buff(ctx, "arcane_overload", 2)
end
