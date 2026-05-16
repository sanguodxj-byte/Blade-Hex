using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Combat;
using BladeHex.Combat.Commands;
using BladeHex.Events;
using BladeHex.Strategic;

namespace BladeHex.Combat;

/// <summary>
/// 战斗单位运行时类 (HD-2D 3D版本)
/// 架构：持有 BattleUnitModel（纯逻辑）+ CommandHistory + EventBus 广播
/// 实现 IFightable 接口，为 Phase 4 堆叠单位统一战斗协议。
/// </summary>
[GlobalClass]
public partial class Unit : Node3D, IFightable
{
    [Export] public UnitData? Data { get; set; }

    // ==========================================
    // 新架构组件
    // ==========================================
    private BattleUnitModel? _model;
    public BattleUnitModel Model => _model ??= new BattleUnitModel(Data!);
    public CommandHistory? CommandHistory { get; set; }

    public int CurrentHp { get; set; }
    public Vector2I GridPos { get; set; }

    /// <summary>是否为玩家阵营（由 CombatManager.RegisterUnit 设置）</summary>
    public bool IsPlayerSide { get; set; }

    public bool HasMoved { get; set; } = false;
    public bool HasActed { get; set; } = false;
    public float CurrentAp { get; set; } = 0.0f;
    public bool UsingPrimaryWeapon { get; set; } = true;

    public CharacterSkillTree? SkillTree { get; set; } = null;

    // ==========================================
    // IFightable 接口实现
    // ==========================================
    string IFightable.DisplayName => Data?.UnitName ?? Name.ToString();
    Vector2I IFightable.GridPosition { get => GridPos; set => GridPos = value; }
    int IFightable.MaxHp => GetMaxHp();
    bool IFightable.IsAlive => CurrentHp > 0;
    int IFightable.MaxAp => GetMaxAp();
    int IFightable.MoveRange => GetMoveRange();
    int IFightable.Ac => GetAc();
    int IFightable.AttackBonus => GetAttackBonus();
    int IFightable.Count => 1;
    bool IFightable.IsStack => false;

    // ==========================================
    // 移动与动画
    // ==========================================

    /// <summary>沿着路径平滑移动</summary>
    public async Task MoveAlongPath(List<Vector2I> path, HexGrid hexGrid)
    {
        if (path == null || path.Count <= 1) return;

        // 起点不需要移动
        for (int i = 1; i < path.Count; i++)
        {
            var nextCoord = path[i];
            var cell = hexGrid.GetCell(nextCoord.X, nextCoord.Y);
            int elevation = cell?.Elevation ?? 0;
            
            Vector3 targetPos = HexUtils.AxialToWorld3D(nextCoord.X, nextCoord.Y, elevation);
            
            // 使用 Tween 进行平滑位移
            var tween = GetTree().CreateTween();
            tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(this, "position", targetPos, 0.15f);
            
            // 播放移动动画
            PlayAnim("move");
            
            await ToSignal(tween, Tween.SignalName.Finished);
            
            // 更新逻辑坐标
            GridPos = nextCoord;
        }
        
        PlayAnim("default");
    }

    public override void _Ready()
    {
        if (Data != null)
        {
            int maxHp = GetMaxHp();
            CurrentHp = maxHp;
            Model.CurrentHp = maxHp;
        }
        SetupVisuals();
        SetupClickArea();
    }

    // ==========================================
    // 点击碰撞区域 — 让玩家可以直接点击单位模型选中
    // ==========================================

    [Signal] public delegate void UnitClickedEventHandler(Unit unit);
    [Signal] public delegate void UnitRightClickedEventHandler(Unit unit);

    private void SetupClickArea()
    {
        // 不创建独立的 Area3D — 单位点击通过 HexCell.Occupant 路径处理
        // Unit 的视觉节点不应拦截地面格子的 InputEvent
        // UnitClicked 信号保留供外部直接触发（如 UI 列表点击）
    }

    // ==========================================
    // 视觉初始化 — 通过 CharacterRenderNode 六层渲染系统
    // ==========================================

    /// <summary>当前注册的渲染总线（委托到 CharacterRenderBus.Instance）</summary>
    public CharacterRenderBus? RenderBus => CharacterRenderBus.Instance;

    /// <summary>本单位的渲染节点（作为子节点）</summary>
    public CharacterRenderNode? RenderNode { get; private set; }

    public void SetupVisuals()
    {
        try
        {
            RenderNode = new CharacterRenderNode();
            RenderNode.Name = "CharacterRenderNode";
            AddChild(RenderNode);
            RenderNode.Setup(this);

            if (RenderBus != null)
                RenderBus.Register(this, RenderNode);
        }
        catch (System.Exception ex)
        {
            GD.PushError($"[Unit] SetupVisuals CharacterRenderNode failed: {ex.Message}");
        }

        // HP/装甲条独立于 CharacterRenderNode — 始终创建
        SetupHpBar();
    }

    // ==========================================
    // 头顶 HP 条 + 装甲条 — 委托给 UnitHealthBarComponent（架构优化 spec R10）
    // ==========================================
    private BladeHex.View.Unit.Components.UnitHealthBarComponent? _healthBar;

    private void SetupHpBar()
    {
        _healthBar = new BladeHex.View.Unit.Components.UnitHealthBarComponent();
        _healthBar.Name = "HealthBar";
        AddChild(_healthBar);
        // _Ready 时 _healthBar 还未触发 _Ready（子节点 _Ready 在父之后），
        // 所以延迟 SetState 一帧
        CallDeferred(nameof(InitHealthBarState));
    }

    private void InitHealthBarState()
    {
        if (_healthBar == null || !IsInstanceValid(_healthBar)) return;
        _healthBar.SetState(CurrentHp, GetMaxHp(), Data?.Armor);
    }

    /// <summary>更新 HP 条显示（血量变化时调用，向后兼容入口）。</summary>
    public void UpdateHpBar()
    {
        _healthBar?.SetHp(CurrentHp, GetMaxHp());
    }

    /// <summary>更新装甲条显示（护甲值变化时调用，向后兼容入口）。</summary>
    public void UpdateArmorBar()
    {
        if (_healthBar == null) return;
        int maxArmor = Data?.Armor?.MaxArmorPoints ?? 0;
        int currentArmor = Data?.Armor?.CurrentArmorPoints ?? 0;
        _healthBar.SetArmor(currentArmor, maxArmor);
    }

    // ==========================================
    // 动画播放
    // ==========================================

    public void PlayAnim(string animName)
    {
        RenderNode?.PlayAnimation(animName);
    }

    /// <summary>攻击微动画 — 纹理朝目标方向突进 20px 后弹回</summary>
    public void PlayAttackLunge(Vector3 targetWorldPos)
    {
        if (RenderNode == null) return;
        var direction = (targetWorldPos - GlobalPosition);
        direction.Y = 0; // 只在 XZ 平面移动
        if (direction.LengthSquared() < 0.01f) return;
        RenderNode.PlayAttackLunge(direction.Normalized());
    }

// ==========================================
    // RPG 属性与结算计算 — 全部委托给 BattleUnitModel / CombatStats
    // ==========================================

    public int GetStatModifier(int score) => CombatStats.GetStatModifier(score);

    public int GetMaxHp() => Model.GetMaxHp();

    /// <summary>读取当前 AP（纯 getter，无副作用）。初始化由 TurnManager 调用 Model.EnsureApInitialized()</summary>
    public float GetAp() => CurrentAp;

    public int GetMaxAp() => Model.GetMaxAp();

    public int GetCritThreshold() => Model.GetCritThreshold();

    public float GetCritDamageTakenMultiplier() => Model.GetCritDamageTakenMultiplier();

    public int GetAc() => Model.GetAc();

    public int GetEffectiveAc(Unit? attacker = null)
    {
        int passiveAcBonus = PassiveSkillResolver.GetPassiveAcBonus(this);
        var moraleEffects = MoraleSystem.GetMoraleEffects(this);
        bool isDefending = Data?.Runtime.IsDefending ?? false;
        return Model.GetEffectiveAc(isDefending, passiveAcBonus, moraleEffects.AcModifier);
    }

    public int GetDr() => Model.GetDr();

    public int GetDrThreshold() => Model.GetDrThreshold();

    public int GetMaxDr() => Model.GetMaxDr();

    public void InitDr() => Model.InitDr();

    public int TakeDrDamage(int amount) => Model.TakeDrDamage(amount);

    public int GetTotalCurrentArmorPoints() => Model.GetTotalCurrentArmorPoints();

    public int GetArmorApPenalty() => Model.GetArmorApPenalty();

    public ItemData? GetMainHand() => Model.GetMainHand();

    public ItemData? GetOffHand() => Model.GetOffHand();

    public int GetAttackBonus() => Model.GetAttackBonus();

    public Godot.Collections.Dictionary RollDamage() => Model.RollDamage();

    public int GetMoveRange() => Model.GetMoveRange();

    /// <summary>切换主/副武器组</summary>
    public void SwitchWeaponSet()
    {
        UsingPrimaryWeapon = !UsingPrimaryWeapon;
        // 同步到 Model（确保 GetMainHand/GetAttackBonus/RollDamage 使用正确武器）
        Model.Runtime.UsingPrimaryWeapon = UsingPrimaryWeapon;
        // 刷新渲染外观
        RenderNode?.RefreshAllEquipment();
    }

    // ==========================================
    // 伤害接收
    // ==========================================

    /// <summary>
    /// 直接扣除 HP（不经过护甲穿透，由调用方负责穿透结算）
    /// </summary>
    public void ApplyResolvedDamage(int hpDamage)
    {
        if (hpDamage <= 0) return;

        // 通过 Model.ApplyDamage 统一扣血（DamageSource.Other + naturalRoll=20 跳过穿透判定）
        var result = Model.ApplyDamage(DamageSource.Other, hpDamage, naturalRoll: 20);
        SyncHpFromModel(result);
    }

    /// <summary>
    /// 受伤（简化版）— 直接扣除 HP，不经过护甲穿透
    /// 用于法术伤害、环境伤害、消耗品等非物理攻击来源
    /// </summary>
    public void TakeDamage(int amount)
    {
        ApplyResolvedDamage(amount);
    }

    /// <summary>
    /// 治疗 — 统一治疗入口，同步 Unit.CurrentHp 与 Model.CurrentHp
    /// 所有治疗效果必须通过此方法，禁止直接修改 CurrentHp
    /// </summary>
    /// <param name="amount">治疗量（正数）</param>
    /// <returns>实际治疗量</returns>
    public int Heal(int amount)
    {
        if (amount <= 0) return 0;
        int maxHp = Model.GetMaxHp();
        int before = CurrentHp;
        CurrentHp = Math.Min(CurrentHp + amount, maxHp);
        Model.CurrentHp = CurrentHp;
        int actual = CurrentHp - before;
        if (actual > 0)
            Events.EventBus.Instance?.Publish("unit_healed", new Godot.Collections.Dictionary
            {
                { "unit", this }, { "amount", actual }, { "current_hp", CurrentHp },
            });
        UpdateHpBar();
        return actual;
    }

    /// <summary>
    /// 设置 HP 到指定值 — 用于复活等特殊场景，同步双源
    /// </summary>
    public void SetHp(int hp)
    {
        CurrentHp = Math.Clamp(hp, 0, Model.GetMaxHp());
        Model.CurrentHp = CurrentHp;
        UpdateHpBar();
    }

    /// <summary>
    /// 受伤（完整版）—— 含护甲穿透结算 + 武器精通 XP
    /// 所有伤害路径统一委托 BattleUnitModel.ApplyDamage 进行规则解算，
    /// View 层只负责表现（HP 动画、VFX、音效、EventBus 广播）。
    /// </summary>
    /// <param name="amount">原始伤害值</param>
    /// <param name="damageType">武器伤害类型</param>
    /// <param name="naturalRoll">自然 d20 骰子（20=自动穿透）</param>
    /// <param name="attacker">攻击者单位（获得精通 XP，可为 null）</param>
    /// <param name="weaponSubtype">打击所用的武器子类型</param>
    /// <param name="weaponWeight">武器重量类别（影响穿透系数分桶）</param>
    public void TakeDamageWithPenetration(
        int amount,
        WeaponData.DamageType damageType,
        int naturalRoll = 20,
        Unit? attacker = null,
        WeaponData.WeaponSubtype weaponSubtype = WeaponData.WeaponSubtype.Unarmed,
        WeaponData.WeightCategory weaponWeight = WeaponData.WeightCategory.Medium)
    {
        var result = Model.ApplyDamage(
            DamageSource.WeaponAttack,
            amount,
            damageType,
            naturalRoll,
            weaponWeight,
            attacker?.Data?.WeaponMastery,
            weaponSubtype);
        SyncHpFromModel(result);
    }

    /// <summary>
    /// 将 Model.ApplyDamage 的结果同步到 View 层（HP、VFX、EventBus、死亡动画）
    /// </summary>
    private void SyncHpFromModel(DamageResult result)
    {
        // 同步 HP
        CurrentHp = result.RemainingHp;
        UpdateHpBar();

        if (result.TotalDealt > 0)
        {
            GD.Print($"{Name} 受伤 {result.HpDamage} HP / {result.DrDamage} DR，剩余: {CurrentHp}");
            if (RenderBus != null)
                RenderBus.NotifyHit(this);
        }

        if (result.ArmorBroken)
            GD.Print($"{Name} 的护甲已完全毁坏，被彻底移除！");

        Events.EventBus.Instance?.PublishUnitDamaged(this, result.TotalDealt, CurrentHp);
        _ = HandleDeathAnimAsync();
    }

    /// <summary>供 CombatResolver 使用的死亡动画触发入口</summary>
    public async System.Threading.Tasks.Task HandleDeathAnimIfDead()
    {
        await HandleDeathAnimAsync();
    }

    private async System.Threading.Tasks.Task HandleDeathAnimAsync()
    {
        if (CurrentHp <= 0)
        {
            RenderBus?.NotifyDeath(this);
            await ToSignal(GetTree().CreateTimer(1.0f), Timer.SignalName.Timeout);
            Die();
        }
        else
        {
            RenderBus?.NotifyHit(this);
            await ToSignal(GetTree().CreateTimer(0.5f), Timer.SignalName.Timeout);
            RenderNode?.PlayAnimation("default");
        }
    }

    public void Die()
    {
        if (RenderBus != null)
            RenderBus.Unregister(this);
        Events.EventBus.Instance?.PublishUnitDied(this, Data?.IsEnemy == false);
        Data?.ClearRuntimeState();
        QueueFree();
    }

    public bool HasSkillEffect(string effectId)
    {
        if (SkillTree == null) return false;
        // 正确实现：遍历已激活节点，比较其 SkillEffect 字段
        return SkillTree.HasSkillEffect(effectId);
    }

    /// <summary>角色是否拥有某个职业技能效果</summary>
    public bool HasCareerSkillEffect(string effectId)
    {
        if (SkillTree == null) return false;
        return SkillTree.HasCareerSkill(effectId);
    }

    /// <summary>获取当前职业对应的技能数据（可能为null）</summary>
    public CareerSkillData? GetCareerSkill()
    {
        if (SkillTree == null) return null;
        return SkillTree.GetCareerSkill();
    }

    /// <summary>职业技能是否可以使用</summary>
    public bool CanUseCareerSkill()
    {
        if (SkillTree == null) return false;
        return SkillTree.CanUseCareerSkill();
    }

    /// <summary>记录职业技能使用一次</summary>
    public void RecordCareerSkillUse()
    {
        SkillTree?.RecordCareerSkillUse();
    }

    public void ConsumeAp(float amount)
    {
        CurrentAp = Math.Max(0, CurrentAp - amount);
    }
}
