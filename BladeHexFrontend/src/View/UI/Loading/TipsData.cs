// TipsData.cs
// 游戏提示数据源
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace BladeHex.UI;

/// <summary>
/// 单条提示
/// </summary>
[GlobalClass]
public partial class Tip : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string Text { get; set; } = "";
    [Export] public string Category { get; set; } = "general"; // combat, exploration, skill, story, general
    [Export] public int Priority { get; set; } = 0;
    [Export] public string GamePhase { get; set; } = "";

    public Tip() { }

    public Tip(string id, string text, string category = "general", int priority = 0, string phase = "")
    {
        Id = id;
        Text = text;
        Category = category;
        Priority = priority;
        GamePhase = phase;
    }
}

/// <summary>
/// Tips 数据管理
/// </summary>
[GlobalClass]
public partial class TipsData : RefCounted
{
    private List<Tip> _tips = new();
    private List<int> _shownIndices = new();
    private int _currentIndex = -1;
    private static readonly Random _random = new();

    public TipsData()
    {
        LoadDefaultTips();
    }

    public List<Tip> GetAllTips() => _tips;

    public List<Tip> GetTipsByCategory(string category)
    {
        return _tips.Where(t => t.Category == category).ToList();
    }

    public Tip? GetNextTip()
    {
        if (_tips.Count == 0) return null;

        if (_shownIndices.Count >= _tips.Count)
        {
            _shownIndices.Clear();
        }

        var available = Enumerable.Range(0, _tips.Count)
            .Where(i => !_shownIndices.Contains(i))
            .ToList();

        if (available.Count == 0)
        {
            _currentIndex = 0;
        }
        else
        {
            // 按权重排序
            available.Sort((a, b) => _tips[b].Priority.CompareTo(_tips[a].Priority));
            
            // 在前 25% 权重中随机选取
            int topCount = Math.Max(1, (int)(available.Count / 4.0));
            _currentIndex = available[_random.Next(topCount)];
        }

        _shownIndices.Add(_currentIndex);
        return _tips[_currentIndex];
    }

    public void ResetRotation()
    {
        _shownIndices.Clear();
        _currentIndex = -1;
    }

    private void LoadDefaultTips()
    {
        // 优先从JSON加载
        if (TryLoadFromJson()) return;

        // JSON加载失败时使用硬编码回退
        LoadHardcodedTips();
    }

    private bool TryLoadFromJson()
    {
        string path = "res://BladeHexFrontend/src/View/UI/Loading/tips.json";
        if (!FileAccess.FileExists(path)) return false;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return false;
        var json = new Json();
        if (json.Parse(file.GetAsText()) != Error.Ok)
        {
            GD.PrintErr($"[TipsData] JSON parse error: {json.GetErrorMessage()}");
            return false;
        }
        var data = json.Data.AsGodotDictionary();
        if (!data.ContainsKey("tips")) return false;
        var arr = data["tips"].AsGodotArray();
        foreach (var item in arr)
        {
            var dict = item.AsGodotDictionary();
            string id = dict.ContainsKey("id") ? dict["id"].AsString() : "";
            string text = dict.ContainsKey("text") ? dict["text"].AsString() : "";
            string category = dict.ContainsKey("category") ? dict["category"].AsString() : "general";
            int priority = dict.ContainsKey("priority") ? dict["priority"].AsInt32() : 0;
            if (text != "")
                _tips.Add(new Tip(id, text, category, priority));
        }
        GD.Print($"[TipsData] Loaded {_tips.Count} tips from JSON");
        return _tips.Count > 0;
    }

    private void LoadHardcodedTips()
    {
        // ============ 战斗提示 ============
        _tips.Add(new Tip("combat_1", "占据高地不只是谚语——地形高度差会为攻击者提供真实的命中加成。聪明的将军从不放弃山丘。", "combat", 3));
        _tips.Add(new Tip("combat_2", "当士气的最后一道防线崩溃，士兵将丢盔弃甲、四散奔逃，再也无法接受任何指令。不要让他们走到那一步。", "combat", 4));
        _tips.Add(new Tip("combat_3", "正面强攻是勇者的选择，但侧翼突袭是智者的选择。从侧面发起的攻击会让守备者措手不及，大幅削弱防御效果。", "combat", 3));
        _tips.Add(new Tip("combat_4", "低阶法术只需消耗魔力即可施展，但真正改变战局的力量——那些让天地变色的禁术——需要消耗珍贵的法术位。好钢要用在刀刃上。", "combat", 2));
        _tips.Add(new Tip("combat_5", "冲锋不止是一种移动方式。带着速度的重量撞击敌阵，惯性会转化为额外伤害。移动的距离越长，冲击的力道越重。", "combat", 3));
        _tips.Add(new Tip("combat_6", "光明与生命的力量对亡灵而言如同烈焰灼身。治疗法术落在骷髅与幽灵身上，会产生意想不到的\"逆转\"效果。", "combat", 5));
        
        // ============ 探索提示 ============
        _tips.Add(new Tip("explore_1", "大地图上的每一处异常都值得调查——被遗弃的营地、不自然的岩石排列、飞鸟盘旋不去的空地。宝物和支线故事往往藏在最不起眼的角落。", "exploration", 2));
        _tips.Add(new Tip("explore_2", "同样的距离，平原上半日可达，山地却需要三天。行军路线的规划不是小事——补给耗尽在荒野中意味着死亡。", "exploration", 3));
        _tips.Add(new Tip("explore_7", "天气不只是装饰。暴雨会熄灭火焰法术，浓雾会遮蔽远程视线，而雷电交加时，站在高处的人会成为最醒目的目标。", "exploration", 4));
        
        // ============ 技能盘提示 ============
        _tips.Add(new Tip("skill_1", "技能盘上点亮每一颗星辰都需要付出不可撤回的代价。没有后悔药，没有重来——正因如此，每一次选择才弥足珍贵。", "skill", 5));
        _tips.Add(new Tip("skill_3", "跳跃是穿越技能盘的捷径，能让你瞬间抵达远处的节点，跳过漫长的路径——但这种力量一生只有四次。用在哪，决定了你是谁。", "skill", 4));
        
        // ============ 故事与角色 ============
        _tips.Add(new Tip("char_4", "职业称号不过是世人对你的称呼——\"魔剑士\"或\"守护骑士\"只取决于你在技能盘上的足迹。称号不会限制你的能力，它只是在描述你已经成为的那个人。", "character", 3));
        _tips.Add(new Tip("story_5", "魔法不是无代价的恩赐。每一次施展法术都会在施法者的灵魂上留下细微的刻痕，积累到一定程度时……没有人知道会发生什么。", "story", 2));
        
        // ============ 通用提示 ============
        _tips.Add(new Tip("general_1", "经验丰富的冒险者有一个共同的习惯：在踏入未知的危险之前，先找一个安全的角落存档。这个世界不会因为你的死亡而停下脚步。", "general", 3));
        _tips.Add(new Tip("general_3", "按 Tab 键可以切换显示信息的层级——从简洁到详尽，选择最适合当前局势的信息密度。战场上的混乱中，少即是多。", "general", 1));
    }
}
