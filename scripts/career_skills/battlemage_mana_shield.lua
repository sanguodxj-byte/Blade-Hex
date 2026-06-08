-- battlemage_mana_shield.lua
-- 法力护盾 (双属性 INT+CON)
-- 给自己挂法力护盾 buff: 受伤时消耗法力减免伤害

function execute(ctx)
    -- 自身 buff 技能 (基线 3 回合)
    self_buff(ctx, "mana_shield", 3)
end
