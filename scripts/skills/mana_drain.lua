-- mana_drain.lua
-- 法力吸取：从目标吸取2d6魔力，添加到施法者

function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    local drain = combat:roll_dice(2, 6)
    -- 不能吸取超过目标拥有的魔力
    if drain > target.mana then
        drain = target.mana
    end

    target.mana = target.mana - drain
    ctx.attacker.mana = ctx.attacker.mana + drain
    result:add_damage(target, drain, "mana_drain")
end
