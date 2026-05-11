using Godot;
using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.Combat.AI;

/// <summary>
/// AI 决策的输出数据类 —— 表示一个敌方单位计划执行的动作
/// 对应策划案 09-AI系统 → 动作指令定义
/// </summary>
public class AIAction
{
    public enum ActionType
    {
        MoveThenAttack,  // 先移动再攻击
        Attack,          // 从当前位置攻击
        MoveOnly,        // 仅移动（重新定位/巡逻）
        Retreat,         // 向撤退点逃跑
        Overwatch,       // 进入防御姿态
        UseSkill,        // 使用技能/能力
        Idle             // 待机
    }

    public ActionType Type { get; set; } = ActionType.Idle;
    public Unit? Actor { get; set; }
    public Unit? TargetUnit { get; set; }
    public Vector2I TargetPosition { get; set; } = new(-1, -1);
    public Vector2I AttackPosition { get; set; } = new(-1, -1);
    public float PriorityScore { get; set; } = 0.0f;
    public string Description { get; set; } = "";
    public List<Vector2I> MovePath { get; set; } = new();

    // 冲锋标记
    public bool IsCharge { get; set; } = false;

    // 包夹信息
    public bool IsFlanking { get; set; } = false;
    public bool IsBackstab { get; set; } = false;

    // 技能/物品 ID (用于 UseSkill 类型)
    public string SkillId { get; set; } = "";
}
