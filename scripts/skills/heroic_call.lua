-- heroic_call.lua
-- 英雄号召：若本场战斗未使用，所有友军获得英雄效果3回合（攻击+2，AC+1）

function execute(ctx)
    local caster = ctx.attacker

    if caster.heroic_call_used then
        result:fail("英雄号召本场战斗已使用")
        return
    end

    local allies = ctx.allies
    for i = 0, allies.Length - 1 do
        local ally = allies[i]
        if unit:is_valid(ally) then
            buff:apply(ally, "heroic_call", 3)
        end
    end

    caster.heroic_call_used = true
end
