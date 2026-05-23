-- mana_surge.lua
-- 魔力涌动：恢复全部魔力（每场战斗限用一次，用mana_surge_used标记）

function execute(ctx)
    local caster = ctx.attacker
    if caster:has_effect("mana_surge_used") then
        result:fail("魔力涌动已使用过")
        return
    end

    caster.mana = caster.max_mana
    buff:apply_custom(caster, "mana_surge_used", 99, { used_marker = true })
end
