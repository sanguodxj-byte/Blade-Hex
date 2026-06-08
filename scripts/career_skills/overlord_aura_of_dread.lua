-- overlord_aura_of_dread.lua
-- 恐惧光环 (三属性 STR+CON+CHA)
-- 给自己挂恐惧光环 buff: 周围 3 格敌方每回合士气 -5, 敌方劣势

function execute(ctx)
    -- 自身 buff 技能 (基线 3 回合)
    self_buff(ctx, "dread_aura", 3)
end
