-- life_shield.lua
-- 生命护盾：若本场战斗未使用，获得30%最大生命值的临时生命，持续3回合

function execute(ctx)
    local caster = ctx.attacker

    if caster.life_shield_used then
        result:fail("生命护盾本场战斗已使用")
        return
    end

    local temp_hp = math.floor(caster.max_hp * 0.3)
    result:add_effect(caster, "temp_hp", 3, { temp_hp_amount = temp_hp })
    caster.life_shield_used = true
end
