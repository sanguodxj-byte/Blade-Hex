-- _lib.lua
-- 技能脚本公共工具库 — 所有技能脚本可直接使用这些函数
-- NLua 版：C# 对象方法用 : 调用，属性用 . 访问

--- 查找目标格的敌人，找不到则自动 fail 并返回 nil
function require_enemy(q, r)
    local target = unit:find_at(q, r, "enemies")
    if not target then
        result:fail("目标格没有敌人")
        return nil
    end
    return target
end

--- 查找目标格的友军，找不到则自动 fail 并返回 nil
function require_ally(q, r)
    local target = unit:find_at(q, r, "allies")
    if not target then
        result:fail("目标格没有盟友")
        return nil
    end
    return target
end

--- 遍历施放者周围六邻格，对每个找到的指定阵营单位执行回调
function aoe_neighbors(caster, side, callback)
    local neighbors = hex:neighbors(caster.q, caster.r)
    for i = 0, neighbors.Length - 1 do
        local pos = neighbors[i]
        local target = unit:find_at(pos.X, pos.Y, side)
        if target and unit:is_valid(target) then
            callback(target, pos)
        end
    end
end

--- 遍历目标格 + 其六邻格（7 格 AOE），对每个找到的指定阵营单位执行回调
function aoe_area(center_q, center_r, side, callback)
    local center_target = unit:find_at(center_q, center_r, side)
    if center_target and unit:is_valid(center_target) then
        callback(center_target)
    end
    local neighbors = hex:neighbors(center_q, center_r)
    for i = 0, neighbors.Length - 1 do
        local pos = neighbors[i]
        local target = unit:find_at(pos.X, pos.Y, side)
        if target and unit:is_valid(target) then
            callback(target)
        end
    end
end

--- 检查并扣除魔力，不足则 fail
function check_mana(caster, cost)
    if caster.mana < cost then
        result:fail("魔力不足")
        return false
    end
    caster.mana = caster.mana - cost
    return true
end
