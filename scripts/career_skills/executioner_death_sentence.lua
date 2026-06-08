-- executioner_death_sentence.lua
-- 终结宣告 (四属性 STR+DEX+CON+CHA)
-- 给自己挂终结宣告 buff: 攻击 HP≤30% 目标时暴击阈值 -4

function execute(ctx)
    -- 自身 buff 技能 (基线 3 回合)
    self_buff(ctx, "death_sentence", 3)
end
