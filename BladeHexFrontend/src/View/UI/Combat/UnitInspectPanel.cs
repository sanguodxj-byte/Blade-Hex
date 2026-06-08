// UnitInspectPanel.cs
// 战斗中右键单位弹出的详细信息面板 — 悬浮在鼠标位置
// 布局参考部队UI左侧：属性、装备、战斗数值
using Godot;
using BladeHex.Data;
using BladeHex.Combat;
using BladeHex.Strategic;
using BladeHex.UI.Common;

namespace BladeHex.UI.Combat;

/// <summary>
/// 单位检视面板 — 右键任何单位时在鼠标位置弹出
/// 显示：名称、等级、属性六维、HP/AP、AC/DR、武器信息、护甲信息
/// </summary>
[GlobalClass]
public partial class UnitInspectPanel : FloatingPanel
{
    private Label _nameLabel = null!;
    private Label _levelLabel = null!;
    private GridContainer _attrGrid = null!;
    private Label _hpLabel = null!;
    private Label _apLabel = null!;
    private Label _acLabel = null!;
    private Label _drLabel = null!;
    private Label _weaponLabel = null!;
    private Label _armorLabel = null!;
    private Label _shieldLabel = null!;
    private Label _helmetLabel = null!;
    private Label _gauntletsLabel = null!;
    private Label _bootsLabel = null!;
    private Label _accessoryLabel = null!;
    private Label _moveLabel = null!;
    private Label _strategyLabel = null!;
    private VBoxContainer _equipBox = null!;

    // ============================================================================
    // FloatingPanel 配置
    // ============================================================================

    protected override int PanelZIndex => 200;
    protected override float MinPanelWidth => 220f;
    protected override Vector2 MouseOffset => new(-15, -15);
    protected override FloatingPanelDismissMode PanelDismiss => FloatingPanelDismissMode.OnMouseExit;

    // ============================================================================
    // 构建内容
    // ============================================================================

    protected override void BuildContent()
    {
        // 名称
        _nameLabel = MakeTitleLabel("");
        Content.AddChild(_nameLabel);

        // 等级
        _levelLabel = MakeLabel("", 12, new Color(0.7f, 0.7f, 0.7f));
        Content.AddChild(_levelLabel);

        Content.AddChild(MakeSeparator());

        // 六维属性
        _attrGrid = new GridContainer { Columns = 6 };
        _attrGrid.AddThemeConstantOverride("h_separation", 6);
        _attrGrid.AddThemeConstantOverride("v_separation", 2);
        Content.AddChild(_attrGrid);

        Content.AddChild(MakeSeparator());

        // 战斗数值
        _hpLabel = MakeLabel("", 13, new Color(0.3f, 0.9f, 0.3f));
        Content.AddChild(_hpLabel);
        _apLabel = MakeLabel("", 13, new Color(0.3f, 0.7f, 1.0f));
        Content.AddChild(_apLabel);
        _acLabel = MakeLabel("", 13, Colors.White);
        Content.AddChild(_acLabel);
        _drLabel = MakeLabel("", 13, new Color(0.5f, 0.6f, 0.9f));
        Content.AddChild(_drLabel);
        _moveLabel = MakeLabel("", 13, Colors.White);
        Content.AddChild(_moveLabel);

        Content.AddChild(MakeSeparator());

        // 装备
        _equipBox = new VBoxContainer();
        _equipBox.AddThemeConstantOverride("separation", 2);
        Content.AddChild(_equipBox);

        _weaponLabel = MakeLabel("", 12, new Color(0.9f, 0.8f, 0.6f));
        _equipBox.AddChild(_weaponLabel);
        _armorLabel = MakeLabel("", 12, new Color(0.6f, 0.7f, 0.9f));
        _equipBox.AddChild(_armorLabel);
        _shieldLabel = MakeLabel("", 12, new Color(0.6f, 0.7f, 0.9f));
        _equipBox.AddChild(_shieldLabel);
        _helmetLabel = MakeLabel("", 12, new Color(0.6f, 0.7f, 0.9f));
        _equipBox.AddChild(_helmetLabel);
        _gauntletsLabel = MakeLabel("", 12, new Color(0.6f, 0.7f, 0.9f));
        _equipBox.AddChild(_gauntletsLabel);
        _bootsLabel = MakeLabel("", 12, new Color(0.6f, 0.7f, 0.9f));
        _equipBox.AddChild(_bootsLabel);
        _accessoryLabel = MakeLabel("", 12, new Color(0.7f, 0.65f, 0.85f));
        _equipBox.AddChild(_accessoryLabel);

        // AI 策略（仅敌方显示）
        _strategyLabel = MakeLabel("", 11, new Color(0.6f, 0.6f, 0.6f));
        Content.AddChild(_strategyLabel);
    }

    // ============================================================================
    // 公共 API
    // ============================================================================

    /// <summary>显示指定单位的详细信息</summary>
    public void ShowForUnit(Unit unit, Vector2 screenPos)
    {
        if (unit?.Data == null) return;
        var data = unit.Data;

        _nameLabel.Text = data.UnitName;

        // 等级 + 职业称号
        string classTitle = GetClassTitle(data);
        string classTitleInfo = string.Empty;
        if (string.IsNullOrEmpty(classTitle))
        {
            // 敌方没有 SkillTree 时，尝试从 CharacterId 或模板数据获取职业名
            // 对于敌方，我们还可以尝试从单位名称推断职业
            classTitleInfo = "";
        }
        else
        {
            classTitleInfo = classTitle;
        }

        string sideTag = data.IsEnemy ? "[敌方]" : "[友方]";
        if (!string.IsNullOrEmpty(classTitleInfo))
            _levelLabel.Text = $"Lv.{data.Level} {classTitleInfo}  {sideTag}";
        else
            _levelLabel.Text = $"Lv.{data.Level}  {sideTag}";

        // 六维属性
        foreach (var child in _attrGrid.GetChildren()) child.QueueFree();
        AddAttr("力", CombatStats.GetEffectiveStr(data));
        AddAttr("敏", CombatStats.GetEffectiveDex(data));
        AddAttr("体", CombatStats.GetEffectiveCon(data));
        AddAttr("智", CombatStats.GetEffectiveInt(data));
        AddAttr("感", CombatStats.GetEffectiveWis(data));
        AddAttr("魅", CombatStats.GetEffectiveCha(data));

        // 战斗数值
        _hpLabel.Text = $"HP: {unit.CurrentHp}/{unit.GetMaxHp()}";
        _apLabel.Text = $"AP: {(int)unit.CurrentAp}/{unit.GetMaxAp()}";
        _acLabel.Text = $"AC: {unit.GetEffectiveAc()}";
        int dr = unit.GetDr();
        int maxDr = unit.GetMaxDr();
        _drLabel.Text = maxDr > 0 ? $"DR: {dr}/{maxDr}" : "DR: 无";
        _moveLabel.Text = $"移动: {unit.GetMoveRange()} 格";

        // 装备 — 使用 GetAllEquippedItems 确保所有装备都显示
        var weapons = unit.GetMainHand() as WeaponData;
        _weaponLabel.Text = weapons != null
            ? $"武器: {weapons.ItemName} ({weapons.DamageDiceCount}d{weapons.DamageDiceSides})"
            : "武器: 徒手";

        // 检查是否有副手武器或盾牌
        var offHand = unit.GetOffHand();
        // 显示副手物品（武器/盾牌统一显示在这里）
        string offHandText = "";
        bool hasDedicatedShieldSlot = false;
        if (offHand != null)
        {
            if (offHand is WeaponData offWeapon)
                offHandText = $"副手: {offWeapon.ItemName} ({offWeapon.DamageDiceCount}d{offWeapon.DamageDiceSides})";
            else if (offHand is ArmorData offArmor && offArmor.armorType == ArmorData.ArmorType.Shield)
            {
                offHandText = $"副手·盾: {offArmor.ItemName} (DR{offArmor.DrThreshold})";
                hasDedicatedShieldSlot = true;
            }
            else
                offHandText = $"副手: {offHand.GetFullName()}";
        }

        // 如果已经通过 offHand 显示了盾牌，shieldLabel 就隐藏
        var armorData = data.Armor;
        _armorLabel.Text = armorData != null
            ? $"护甲: {armorData.ItemName} (DR{armorData.DrThreshold})"
            : "护甲: 无";

        // 盾牌（独立 Shield 字段 — 兼容旧数据）
        var shield = data.Shield;
        if (shield != null && !hasDedicatedShieldSlot)
            _shieldLabel.Text = $"盾牌: {shield.ItemName} (DR{shield.DrThreshold})";
        else if (!hasDedicatedShieldSlot)
            _shieldLabel.Text = "盾牌: 无";
        else
            _shieldLabel.Visible = false;

        // 头盔
        var helmet = data.Helmet;
        _helmetLabel.Text = helmet != null
            ? $"头盔: {helmet.ItemName} (DR{helmet.DrThreshold})"
            : "头盔: 无";

        // 护手
        var gauntlets = data.Gauntlets;
        _gauntletsLabel.Text = gauntlets != null
            ? $"护手: {gauntlets.ItemName} (DR{gauntlets.DrThreshold})"
            : "护手: 无";

        // 鞋子
        var boots = data.Boots;
        _bootsLabel.Text = boots != null
            ? $"鞋子: {boots.ItemName} (DR{boots.DrThreshold})"
            : "鞋子: 无";

        // 饰品
        var acc1 = data.Accessory1;
        var acc2 = data.Accessory2;
        string accText = "";
        if (acc1 != null) accText += acc1.ItemName;
        if (acc2 != null) accText += (accText.Length > 0 ? ", " : "") + acc2.ItemName;
        _accessoryLabel.Text = accText.Length > 0
            ? $"饰品: {accText}"
            : "饰品: 无";

        // 显示副手文本（在装备区末尾）
        if (offHandText.Length > 0 && _weaponLabel.Text.Length > 0)
        {
            // 把副手文本追加到武器后面
            _weaponLabel.Text += $"\n{offHandText}";
        }

        // AI 策略 + 行为/特质
        if (data.IsEnemy)
        {
            string strategyInfo = $"行为: {GetStrategyName(data.aiStrategy)}";
            if (data.Traits != null && data.Traits.Length > 0)
                strategyInfo += $"\n特质: {string.Join(", ", data.Traits)}";
            _strategyLabel.Text = strategyInfo;
            _strategyLabel.Visible = true;
        }
        else
        {
            _strategyLabel.Visible = false;
        }

        // 将 _equipBox 中的所有标签设为可见（有装备内容的显示，没有的隐藏）
        foreach (var child in _equipBox.GetChildren())
        {
            if (child is Label lbl && string.IsNullOrEmpty(lbl.Text))
                lbl.Visible = false;
        }

        ShowAt(screenPos);
    }

    // ============================================================================
    // 内部方法
    // ============================================================================

    private void AddAttr(string name, int value)
    {
        var lbl = MakeLabel($"{name}{value}", 12, new Color(0.85f, 0.82f, 0.75f));
        _attrGrid.AddChild(lbl);
    }

    private static string GetClassTitle(UnitData data)
    {
        var stMgr = Globals.SkillTreesOrNull;
        if (stMgr == null) return "";

        long charId = data.CharacterId >= 0 ? data.CharacterId : (long)data.GetInstanceId();
        var tree = stMgr.GetSkillTree(charId);
        if (tree == null) return "";
        return tree.GetClassTitleName();
    }

    private static string GetStrategyName(UnitData.AIStrategy s) => s switch
    {
        UnitData.AIStrategy.Reckless => "鲁莽",
        UnitData.AIStrategy.Cautious => "谨慎",
        UnitData.AIStrategy.Tactical => "战术",
        UnitData.AIStrategy.Instinct => "本能",
        _ => "未知",
    };
}
