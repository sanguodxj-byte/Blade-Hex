// IFightable.cs
// 战斗实体统一接口 — 英雄单体与堆叠单位共享的战斗协议
// Phase 4 军队系统将通过实现此接口引入 StackUnit

namespace BladeHex.Combat;

/// <summary>
/// 所有可参与战斗的实体必须实现此接口。
/// 当前实现者：Unit（英雄/单体单位）
/// Phase 4 新增：StackUnit（兵种堆叠单位）
/// </summary>
public interface IFightable
{
    // ========== 身份 ==========

    /// <summary>显示名称</summary>
    string DisplayName { get; }

    /// <summary>是否为玩家阵营</summary>
    bool IsPlayerSide { get; }

    /// <summary>网格坐标</summary>
    Godot.Vector2I GridPosition { get; set; }

    // ========== 生命 ==========

    /// <summary>当前 HP（堆叠单位为前排 HP）</summary>
    int CurrentHp { get; }

    /// <summary>最大 HP</summary>
    int MaxHp { get; }

    /// <summary>是否存活</summary>
    bool IsAlive { get; }

    // ========== 行动 ==========

    /// <summary>当前行动点</summary>
    float CurrentAp { get; set; }

    /// <summary>最大行动点</summary>
    int MaxAp { get; }

    /// <summary>是否已移动</summary>
    bool HasMoved { get; set; }

    /// <summary>是否已行动</summary>
    bool HasActed { get; set; }

    /// <summary>移动范围（格数）</summary>
    int MoveRange { get; }

    // ========== 战斗属性 ==========

    /// <summary>防御等级 (AC)</summary>
    int Ac { get; }

    /// <summary>攻击加值</summary>
    int AttackBonus { get; }

    // ========== 战斗操作 ==========

    /// <summary>承受伤害</summary>
    void TakeDamage(int amount);

    /// <summary>消耗行动点</summary>
    void ConsumeAp(float amount);

    /// <summary>死亡处理</summary>
    void Die();

    // ========== 堆叠相关（单体单位返回 1）==========

    /// <summary>当前数量（英雄=1，堆叠=剩余人数）</summary>
    int Count { get; }

    /// <summary>是否为堆叠单位</summary>
    bool IsStack { get; }
}
