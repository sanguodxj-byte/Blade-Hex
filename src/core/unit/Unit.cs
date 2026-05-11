using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Combat; // For MoraleSystem
using BladeHex.Strategic; // For CharacterSkillTree

namespace BladeHex.Combat;

/// <summary>
/// 战斗单位运行时类 (HD-2D 3D版本 - 融合深度 RPG 系统)
/// 渲染树总线集成版: 视觉逻辑委托给 CharacterRenderBus + CharacterRenderNode
/// 迁移自 GDScript Unit.gd
/// </summary>
[GlobalClass]
public partial class Unit : Node3D
{
    [Export] public UnitData? Data { get; set; }

    public int CurrentHp { get; set; }
    public Vector2I GridPos { get; set; } // 当前六边形坐标

    // 状态标记
    public bool HasMoved { get; set; } = false;
    public bool HasActed { get; set; } = false;
    public float CurrentAp { get; set; } = 0.0f; // 当前 AP
    public bool UsingPrimaryWeapon { get; set; } = true; // 是否正在使用第一套武器组

    // 技能盘引用（由 SkillTreeManager 管理）
    public CharacterSkillTree? SkillTree { get; set; } = null;

    // 渲染树总线引用（由 CombatScene 等场景注入）
    // public CharacterRenderBus? RenderBus { get; set; } = null;

    // 旧版视觉节点兼容（无总线时的回退）
    public Node3D? VisualNode { get; set; }

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
            if (HasMethod("play_anim")) Call("play_anim", "move");
            
            await ToSignal(tween, Tween.SignalName.Finished);
            
            // 更新逻辑坐标
            GridPos = nextCoord;
        }
        
        if (HasMethod("play_anim")) Call("play_anim", "default");
    }

    public override void _Ready()
    {
        if (Data != null)
        {
            CurrentHp = GetMaxHp();
        }
        SetupVisuals();
    }

    // ==========================================
    // 视觉初始化 — 优先使用渲染树总线，回退到旧版
    // ==========================================

    public void SetupVisuals()
    {
        // FIXME: CharacterRenderBus 未迁移到 C#，暂时使用旧版视觉
        // if (RenderBus != null) SetupViaBus(); else
        SetupLegacyVisuals();
    }

    private void SetupLegacyVisuals()
    {
        float texHeight = 120.0f;
        float currentPixelSize = 1.0f;

        if (Data?.SpriteFramesValue != null)
        {
            var animSprite = new AnimatedSprite3D();
            animSprite.SpriteFrames = Data.SpriteFramesValue;
            animSprite.PixelSize = 1.0f;
            animSprite.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
            
            if (Data.SpriteFramesValue.GetFrameCount("default") > 0)
            {
                var frameTex = Data.SpriteFramesValue.GetFrameTexture("default", 0);
                if (frameTex != null) texHeight = frameTex.GetHeight();
            }
            
            animSprite.Offset = new Vector2(0, texHeight / 2.0f);
            animSprite.Play("default");
            VisualNode = animSprite;
            currentPixelSize = animSprite.PixelSize;
        }
        else
        {
            var sprite = new Sprite3D();
            if (Data?.BattleSprite != null)
            {
                sprite.Texture = Data.BattleSprite;
                sprite.PixelSize = 1.0f;
                texHeight = sprite.Texture.GetHeight();
            }
            else
            {
                var tex = new PlaceholderTexture2D();
                tex.Size = new Vector2(80, 120);
                sprite.Texture = tex;
                sprite.PixelSize = 1.5f;
                texHeight = 120.0f;
                
                if (Name.ToString().StartsWith("Player"))
                    sprite.Modulate = new Color(0.2f, 0.5f, 1.0f);
                else
                    sprite.Modulate = new Color(1.0f, 0.2f, 0.2f);
            }
            
            sprite.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
            sprite.Offset = new Vector2(0, texHeight / 2.0f);
            VisualNode = sprite;
            currentPixelSize = sprite.PixelSize;
        }

        AddChild(VisualNode);

        var label = new Label3D();
        label.Text = $"{CurrentHp}/{GetMaxHp()}";
        label.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
        label.PixelSize = 3.0f;
        label.Position = new Vector3(0, texHeight * currentPixelSize + 20.0f, 0);
        AddChild(label);
    }

    // ==========================================
    // 动画播放 — 双通道（总线/旧版）
    // ==========================================

    public void PlayAnim(string animName)
    {
        // if (RenderBus != null) RenderBus.NotifySkillAnimation(this, animName); else
        if (VisualNode is AnimatedSprite3D animSprite)
        {
            if (animSprite.SpriteFrames.HasAnimation(animName))
                animSprite.Play(animName);
            else
                animSprite.Play("default");
        }
    }

    // ==========================================
    // RPG 属性与结算计算 (根据 DND/Pathfinder 规则)
    // ==========================================

    public int GetStatModifier(int score) => RPGRuleEngine.GetStatModifier(score);

    public int GetMaxHp()
    {
        if (Data == null) return 1;
        int hp = Data.BaseMaxHp;
        // if (SkillTree != null) hp += SkillTree.GetHpBonus();
        return Math.Max(1, hp);
    }

    public float GetAp()
    {
        // 如果未初始化，尝试取计算出的最大值
        if (CurrentAp <= 0 && !HasMoved && !HasActed)
            CurrentAp = GetMaxAp();
        return CurrentAp;
    }

    public int GetMaxAp()
    {
        if (Data == null) return 12;
        int maxAp = RPGRuleEngine.CalculateMaxAp(Data.BaseAp, Data.Dex, Data.Con);
        return Math.Max(1, maxAp - GetArmorApPenalty());
    }

    public int GetCritThreshold()
    {
        if (Data == null) return 20;
        int wisBonus = (int)Math.Floor(Math.Sqrt(Data.Wis / 2.0));
        return Math.Max(15, 20 - wisBonus);
    }

    public float GetCritDamageTakenMultiplier()
    {
        if (Data == null) return 1.0f;
        int wisBonus = (int)Math.Floor(Math.Sqrt(Data.Wis / 2.0));
        return Math.Max(0.2f, 1.0f - wisBonus * 0.1f);
    }

    public int GetAc()
    {
        if (Data == null) return 10;
        int ac = Data.BaseAc;

        // DEX 修正（受护甲 MaxDexBonus 限制）
        int dexAc = GetStatModifier(Data.Dex);
        if (Data.Armor != null && Data.Armor.MaxDexBonus > 0 && Data.Armor.CurrentArmorPoints > 0)
            dexAc = Math.Min(dexAc, Data.Armor.MaxDexBonus);

        // 护甲 AC 加成（装甲损毁后完全失效）
        int totalDr = 0;
        if (Data.Armor != null && Data.Armor.CurrentArmorPoints > 0)
        {
            ac += Data.Armor.AcBonus;
            totalDr += Data.Armor.DrThreshold;
        }

        // 盾牌 AC 加成（盾牌损毁后完全失效）
        var offHand = GetOffHand();
        if (offHand is ArmorData shield && shield.armorType == ArmorData.ArmorType.Shield
            && shield.CurrentArmorPoints > 0)
        {
            ac += shield.AcBonus;
            totalDr += shield.DrThreshold;
        }

        // sqrt(DR) 物理偏转加成（仅来自尚存的装甲）
        int drAcBonus = (int)Mathf.Floor(Mathf.Sqrt(totalDr));

        return ac + dexAc + drAcBonus;
    }

    /// <summary>
    /// 获取当前单位所有防具的总剩余装甲值
    /// </summary>
    public int GetTotalCurrentArmorPoints()
    {
        int total = 0;
        if (Data?.Armor != null) total += Data.Armor.CurrentArmorPoints;
        if (Data?.Shield != null) total += Data.Shield.CurrentArmorPoints;
        if (Data?.Helmet != null) total += Data.Helmet.CurrentArmorPoints;
        return total;
    }

    public int GetArmorApPenalty()
    {
        int penalty = 0;
        if (Data == null) return 0;
        if (Data.Armor != null) penalty += Data.Armor.ApPenalty;
        if (Data.Shield != null) penalty += Data.Shield.ApPenalty;
        if (Data.Helmet != null) penalty += Data.Helmet.ApPenalty;
        return penalty;
    }

    public int GetEffectiveAc(Unit? attacker = null)
    {
        int ac = GetAc();
        ac += PassiveSkillResolver.GetPassiveAcBonus(this);
        
        if (Data != null && Data.IsDefending) ac += 2;

        var moraleEffects = MoraleSystem.GetMoraleEffects(this);
        ac += moraleEffects.AcModifier;

        return ac;
    }

    public int GetDr() => Data != null ? Math.Max(0, Data.CurrentDr) : 0;

    public int GetDrThreshold()
    {
        if (Data == null || Data.CurrentDr <= 0) return 0;
        int threshold = 0;
        if (Data.Armor != null) threshold = Math.Max(threshold, Data.Armor.DrThreshold);
        if (Data.NaturalDrThreshold > 0) threshold = Math.Max(threshold, Data.NaturalDrThreshold);
        return threshold;
    }

    public int GetMaxDr()
    {
        if (Data == null) return 0;
        int dr = Data.NaturalDr;
        if (Data.Armor != null) dr += Data.Armor.DrThreshold;
        
        var offHand = GetOffHand();
        if (offHand is ArmorData shield && shield.armorType == ArmorData.ArmorType.Shield)
            dr += shield.DrThreshold;
            
        return dr;
    }

    public void InitDr()
    {
        if (Data != null)
        {
            Data.MaxDr = GetMaxDr();
            Data.CurrentDr = Data.MaxDr;
        }
    }

    public int TakeDrDamage(int amount)
    {
        if (Data == null || Data.CurrentDr <= 0) return 0;
        int actual = Math.Min(amount, Data.CurrentDr);
        Data.CurrentDr -= actual;
        return actual;
    }

    public ItemData? GetMainHand() => UsingPrimaryWeapon ? Data?.PrimaryMainHand : Data?.SecondaryMainHand;
    public ItemData? GetOffHand() => UsingPrimaryWeapon ? Data?.PrimaryOffHand : Data?.SecondaryOffHand;

    public void SwitchWeaponSet()
    {
        UsingPrimaryWeapon = !UsingPrimaryWeapon;
        // if (RenderBus != null) RenderBus.NotifySlotChanged(this, "weapon");
    }

    public int GetAttackBonus()
    {
        if (Data == null) return 0;
        var weapon = GetMainHand() as WeaponData;

        // 命中修正来源：精通加值 + 武器固有加成 + 技能加成(预留)
        // DEX 和 STR 不加命中，属性只影响 AC 和伤害
        int proficiency = RPGRuleEngine.GetProficiencyBonus(Data.Level);
        int weaponHitBonus = 0;
        if (weapon?.Subtype != null)
            weaponHitBonus = WeaponRegistry.GetConfig(weapon.Subtype).HitBonus;

        return proficiency + weaponHitBonus;
    }

    public Godot.Collections.Dictionary RollDamage()
    {
        var weapon = GetMainHand() as WeaponData;
        int dmgDice = 0;
        string dText = "徒手(1d20)";

        int levelExtra = Data != null ? RPGRuleEngine.GetDamageDiceCount(Data.Level) - 1 : 0;

        // 骰子结果
        if (weapon != null)
        {
            for (int i = 0; i < weapon.DamageDiceCount; i++)
                dmgDice += GD.RandRange(1, weapon.DamageDiceSides);
            if (levelExtra > 0)
                dmgDice += (int)RPGRuleEngine.RollNd20(levelExtra)["total"];
            dText = $"{weapon.DamageDiceCount}d{weapon.DamageDiceSides}";
            if (levelExtra > 0) dText += $"+{levelExtra}d20";
        }
        else
        {
            dmgDice = GD.RandRange(1, 20);
            if (levelExtra > 0)
                dmgDice += (int)RPGRuleEngine.RollNd20(levelExtra)["total"];
            dText = $"徒手({1 + levelExtra}d20)";
        }

        // 百分比乘法加成体系
        // STR加成: floor(sqrt(STR)) × 10%  e.g. STR=40→6→+60%, STR=25→5→+50%
        int strMod = Data != null ? (int)Mathf.Floor(Mathf.Sqrt(Data.Str)) : 0;
        float strBonus = strMod * 0.1f;

        // 武器精通加成: 精通等级 × 10%
        float masteryBonus = GetWeaponMasteryLevel(weapon) * 0.1f;

        float multiplier = 1.0f + strBonus + masteryBonus;
        int totalDmg = Math.Max(1, (int)(dmgDice * multiplier));

        return new Godot.Collections.Dictionary
        {
            { "dice", dmgDice },
            { "multiplier", multiplier },
            { "str_bonus_pct", (int)(strBonus * 100) },
            { "mastery_bonus_pct", (int)(masteryBonus * 100) },
            { "total", totalDmg },
            { "text", $"{dText}×{multiplier:F1}({(int)(multiplier*100)}%)" },
            { "weapon_subtype", weapon?.Subtype.ToString() ?? "Unarmed" }
        };
    }

    /// <summary>
    /// 获取当前武器的精通等级（0~10级，每级+10%伤害）
    /// 精通不加护甲/盾牌，仅加攻击输出
    /// </summary>
    private int GetWeaponMasteryLevel(WeaponData? weapon)
    {
        // TODO: WeaponMastery system not yet implemented
        return 0;
    }

    public int GetMoveRange()
    {
        int move = Data?.BaseMoveRange ?? 4;
        // if (SkillTree != null) move += SkillTree.GetSpeedBonus();
        return Math.Max(1, move);
    }

    public int GetInitiative()
    {
        int init = Data?.BaseInitiative ?? 0;
        // if (SkillTree != null) init += SkillTree.GetInitiativeBonus();
        return init;
    }

    // ==========================================
    // 动作执行
    // ==========================================

    public Godot.Collections.Dictionary AttackCheck(int targetAc)
    {
        int roll = RPGRuleEngine.RollD20();
        int bonus = GetAttackBonus();
        int total = roll + bonus;

        bool isCritical = (roll == 20);
        bool isMiss = (roll == 1);
        bool isHit = isCritical || (!isMiss && total >= targetAc);

        return new Godot.Collections.Dictionary
        {
            { "hit", isHit },
            { "critical", isCritical },
            { "roll", roll },
            { "bonus", bonus },
            { "total", total }
        };
    }

    /// <summary>
    /// 核心伤害结算逻辑 (符合策划案: 穿透检定 + 伤害类型分配)
    /// <summary>
    /// 受伤（简化版，默认纯物理伤害，自动穿透）
    /// </summary>
    public async void TakeDamage(int amount)
    {
        TakeDamageImpl(amount, WeaponData.DamageType.Slash, 20);
    }

    /// <summary>
    /// 受伤（完整版）——攻击方获得武器精通 XP
    /// </summary>
    /// <param name="amount">原始伤害值</param>
    /// <param name="damageType">武器伤害类型</param>
    /// <param name="naturalRoll">自然 d20 骰子（20=自动穿透）</param>
    /// <param name="attacker">攻击者单位（获受精通 XP，可为 null）</param>
    /// <param name="weaponSubtype">打击所用的武器子类型（处理精通类式）</param>
    public async void TakeDamage(
        int amount,
        WeaponData.DamageType damageType,
        int naturalRoll = 20,
        Unit? attacker = null,
        WeaponData.WeaponSubtype weaponSubtype = WeaponData.WeaponSubtype.Unarmed)
    {
        int totalDealt = TakeDamageImpl(amount, damageType, naturalRoll);

        // 攻击者获得武器精通 XP = 实际造成的总伤害（HP+DR）
        if (attacker?.Data != null && totalDealt > 0 &&
            weaponSubtype != WeaponData.WeaponSubtype.Unarmed)
        {
            bool leveledUp = attacker.Data.WeaponMastery.AddDamageXp(weaponSubtype, totalDealt);
            if (leveledUp)
            {
                int newLevel = attacker.Data.WeaponMastery.GetLevelBySubtype(weaponSubtype);
                GD.Print($"{attacker.Name} 武器精通升级! +{newLevel * 10}% 伤害");
            }
        }
    }

    private int TakeDamageImpl(int amount, WeaponData.DamageType damageType, int naturalRoll)
    {
        // 同步版，返回实际造成的总伤害量（HP+DR）
        int hpDamage = 0;
        int drDamage = 0;

        int targetDrValue = 0;
        if (Data?.Armor != null)
            targetDrValue = Data.Armor.DrThreshold;

        bool noArmor = Data?.Armor == null;
        bool isPenetrated = noArmor || (naturalRoll >= targetDrValue) || (naturalRoll == 20);

        if (noArmor)
        {
            hpDamage = amount;
        }
        else
        {
            switch (damageType)
            {
                case WeaponData.DamageType.Slash:
                    if (isPenetrated) { hpDamage = (int)(amount * 0.9f); drDamage = (int)(amount * 0.1f); }
                    else { drDamage = (int)(amount * 0.4f); }
                    break;
                case WeaponData.DamageType.Pierce:
                    if (isPenetrated) { hpDamage = amount; }
                    else { drDamage = (int)(amount * 0.1f); }
                    break;
                case WeaponData.DamageType.Crush:
                    if (isPenetrated) { hpDamage = (int)(amount * 0.3f); drDamage = (int)(amount * 0.7f); }
                    else { hpDamage = (int)(amount * 0.1f); drDamage = (int)(amount * 0.9f); }
                    break;
                default:
                    hpDamage = amount;
                    break;
            }
        }

        if (Data?.Armor != null && drDamage > 0)
        {
            Data.Armor.CurrentArmorPoints = Math.Max(0, Data.Armor.CurrentArmorPoints - drDamage);
            GD.Print($"{Name} 护甲损耗 {drDamage}，剩余: {Data.Armor.CurrentArmorPoints}");
            if (Data.Armor.CurrentArmorPoints <= 0)
            {
                GD.Print($"{Name} 的护甲已完全毁坏，被彻底移除！");
                Data.Armor = null; // 彻底移除护甲
            }
        }
        if (hpDamage > 0)
        {
            CurrentHp = Math.Max(0, CurrentHp - hpDamage);
            GD.Print($"{Name} 受伤 {hpDamage} HP，剩余: {CurrentHp}");
        }

        UpdateHpLabelLegacy();
        _ = HandleDeathAnimAsync();

        return hpDamage + drDamage;
    }

    private async System.Threading.Tasks.Task HandleDeathAnimAsync()
    {
        if (CurrentHp <= 0)
        {
            PlayAnim("die");
            await ToSignal(GetTree().CreateTimer(1.0f), Timer.SignalName.Timeout);
            Die();
        }
        else
        {
            PlayAnim("hit");
            await ToSignal(GetTree().CreateTimer(0.5f), Timer.SignalName.Timeout);
            PlayAnim("default");
        }
    }

    private void UpdateHpLabelLegacy()
    {
        foreach (var child in GetChildren())
        {
            if (child is Label3D label)
                label.Text = $"{CurrentHp}/{GetMaxHp()}";
        }
    }

    public bool HasSkillEffect(string effectId)
    {
        if (SkillTree == null) return false;
        // 这里的逻辑需要根据 SkillTree 的实现调整
        // 假设 ActivatedNodes 包含的是 NodeId，我们需要查表找 SkillEffect
        // 但为了简单，如果 ActivatedNodes 直接包含 effectId 也行（取决于数据结构设计）
        // 在 PassiveSkillResolver.gd 中，它是直接查 effectId。
        // 我们可能需要在 CharacterSkillTree 中维护一个 Effect 集合。
        return SkillTree.ActivatedNodes.Contains(effectId); 
    }

    public void Die()
    {
        QueueFree();
    }

    public void ConsumeAp(float amount)
    {
        CurrentAp = Math.Max(0, CurrentAp - amount);
    }
}
