-- bruiser_iron_rush.lua
-- 铁壁冲锋 (三属性 STR+CON+DEX)
-- 冲锋最多 4 格，推开路径上敌军 (等级骰 d8 钝伤)，终点近战攻击，给自己挂武圣 Buff

function execute(ctx)
    local tier = get_tier(ctx)

    -- 冲锋攻击目标
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    -- 执行冲锋攻击
    local r = result:resolve_attack(ctx.attacker, target, { advantage = true })
    result:add_attack(r)

    -- 路径上的敌军受等级骰 d8 钝伤 (简化: 直接对目标附加伤害)
    local count, sides = get_level_dice(ctx.attacker.level, 8)
    local crush_dmg = combat:roll_dice(count, sides)
    unit:take_damage(target, crush_dmg)
    result:add_damage(target, crush_dmg, "crush")

    -- 给自己挂 iron_rush buff (ac +4, damage +2)
    self_buff(ctx, "iron_rush", 2)
end
