// OriginSelect.cs
// 玩家出身选择 — 两阶段：
// 阶段1：左插图+右侧"你是谁？"（种族+名字+性别）
// 阶段2：左插图+右侧问答
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.UI.Loading;

namespace BladeHex.UI;

[GlobalClass]
public partial class OriginSelect : CanvasLayer
{
    private UIFactory _factory = null!;

    // ── 数据 ──
    private List<RaceData> _allRaces = new();
    private RaceData? _selectedRace;
    private readonly Dictionary<string, int> _attrs = new()
    { ["str"] = 5, ["dex"] = 5, ["con"] = 5, ["intel"] = 5, ["wis"] = 5, ["cha"] = 5 };
    private int _currentQuestion = 0;
    private readonly List<AnswerRecord> _answers = new();
    private LineEdit _nameInput = null!;
    private ButtonGroup _genderGroup = null!;

    // ── UI ──
    private Control _phase1 = null!;
    private Control _phase2 = null!;
    private TextureRect _phase1Illust = null!;
    private TextureRect _phase2Illust = null!;
    private Label _questionText = null!;
    private VBoxContainer _choicesArea = null!;
    private Label _attrLabel = null!;
    private Label _itemLabel = null!;
    private readonly List<string> _items = new();

    private record AnswerRecord(string Question, string Choice, Dictionary<string, int> Mods);
    private List<QuestionData> _questions = new();
    private record QuestionData(string Text, List<ChoiceData> Choices);
    private record ChoiceData(string Text, string Summary, Dictionary<string, int> Mods);

    // ── 常量 ──
    private const string IllustBase = "res://assets/generated_origin_illust/";
    private static readonly Dictionary<string, string> RaceIllust = new()
    {
        ["人类"] = "race_human",
        ["精灵"] = "race_elf",
        ["矮人"] = "race_dwarf",
        ["半兽人"] = "race_halforc",
        ["半精灵"] = "race_halfelf",
    };

    public override void _Ready()
    {
        _factory = new UIFactory();
        _allRaces = new List<RaceData>(RaceData.GetAllRaces());
        _selectedRace = _allRaces.Count > 0 ? _allRaces[0] : null;
        _RegisterRaceBgm();
        _SetupUI();
        _ShowPhase1();
        _PreloadWorldTextures();
    }

    /// <summary>注册各种族的BGM到AudioManager（Event场景的不同variant）</summary>
    private static void _RegisterRaceBgm()
    {
        var audio = BladeHex.Audio.AudioManager.Instance;
        if (audio == null) return;

        // 路径约定: res://src/assets/audio/bgm/race_{race}.ogg
        var bgms = new (RaceData.Race race, string variant, string path)[]
        {
            (RaceData.Race.Human,   "race_human",   "res://src/assets/audio/bgm/race_human.ogg"),
            (RaceData.Race.Elf,     "race_elf",     "res://src/assets/audio/bgm/race_elf.ogg"),
            (RaceData.Race.Dwarf,   "race_dwarf",   "res://src/assets/audio/bgm/race_dwarf.ogg"),
            (RaceData.Race.HalfOrc, "race_halforc", "res://src/assets/audio/bgm/race_halforc.ogg"),
            (RaceData.Race.HalfElf, "race_halfelf", "res://src/assets/audio/bgm/race_halfelf.ogg"),
        };
        foreach (var (_, variant, path) in bgms)
        {
            if (ResourceLoader.Exists(path))
                audio.AddBgmVariant(BladeHex.Audio.AudioManager.Scenario.Event, variant, path);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // UI 构建
    // ════════════════════════════════════════════════════════════════
    private void _SetupUI()
    {
        // 背景
        var bgTex = GD.Load<Texture2D>("res://assets/generated_ui_main/selected/MainMenu_Background.png");
        if (bgTex != null)
        {
            var bg = new TextureRect { Texture = bgTex, Modulate = new Color(0.45f, 0.45f, 0.45f) };
            bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            bg.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            bg.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            AddChild(bg);
        }
        else
        {
            var bg = new ColorRect { Color = new Color(0.03f, 0.03f, 0.05f) };
            bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            AddChild(bg);
        }

        _phase1 = new Control();
        _phase1.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_phase1);
        _BuildPhase1();

        _phase2 = new Control();
        _phase2.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _phase2.Visible = false;
        AddChild(_phase2);
        _BuildPhase2();
    }

    private void _ShowPhase1()
    {
        _phase1.Visible = true;
        _phase2.Visible = false;
        _UpdateRaceIllust();
        _UpdateRaceBgm();
    }

    private void _ShowPhase2()
    {
        _phase1.Visible = false;
        _phase2.Visible = true;
        _currentQuestion = 0;
        _answers.Clear();
        _items.Clear();
        _attrs["str"] = 5; _attrs["dex"] = 5; _attrs["con"] = 5;
        _attrs["intel"] = 5; _attrs["wis"] = 5; _attrs["cha"] = 5;
        _BuildQuestions();
        _ShowIllust(_phase2Illust, "chaos_default");
        _RefreshQuestion();
    }

    // ════════════════════════════════════════════════════════════════
    // 阶段1：你想成为谁？（左插图 + 右种族/名字/性别）
    // ════════════════════════════════════════════════════════════════
    private void _BuildPhase1()
    {
        var margin = _FullMargin();
        _phase1.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        margin.AddChild(vbox);

        // 标题
        var title = _Lbl("你是谁？", 36, new Color(0.9f, 0.8f, 0.5f));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        // 主体：左插图 + 右内容
        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 24);
        body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        vbox.AddChild(body);

        // 左：插图（3:4比例，完整显示，高度撑满后宽度需 高×0.75）
        _phase1Illust = _MakeIllustRect();
        var illustPanel = _WrapIllust(_phase1Illust, 720);
        body.AddChild(illustPanel);

        // 右：选择内容
        var rightPanel = new PanelContainer();
        rightPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        rightPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var rps = new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.07f, 0.88f) };
        rps.SetBorderWidthAll(1);
        rps.BorderColor = new Color(0.3f, 0.28f, 0.22f, 0.5f);
        rps.SetCornerRadiusAll(6);
        rps.SetContentMarginAll(30);
        rightPanel.AddThemeStyleboxOverride("panel", rps);
        body.AddChild(rightPanel);

        var rightVbox = new VBoxContainer();
        rightVbox.AddThemeConstantOverride("separation", 18);
        rightPanel.AddChild(rightVbox);

        // 顶部弹性空间，让表单居中偏上
        rightVbox.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        // 种族
        rightVbox.AddChild(_Lbl("选择血脉", 22, new Color(0.85f, 0.78f, 0.55f)));
        var raceRow = new HBoxContainer();
        raceRow.AddThemeConstantOverride("separation", 10);
        rightVbox.AddChild(raceRow);

        var raceGroup = new ButtonGroup();
        foreach (var race in _allRaces)
        {
            var btn = new Button { Text = race.RaceName, CustomMinimumSize = new Vector2(76, 42), ToggleMode = true };
            btn.AddThemeFontSizeOverride("font_size", 18);
            btn.ButtonGroup = raceGroup;
            if (race == _selectedRace) btn.ButtonPressed = true;
            var captured = race;
            btn.Pressed += () =>
            {
                _selectedRace = captured;
                _UpdateRaceIllust();
                // 播放种族专属 BGM
                var audio = GetNodeOrNull<BladeHex.Audio.AudioManager>("/root/AudioManager");
                audio?.PlayRaceBgm(_GetRaceId(captured), 1.5f);
            };
            _AttachSfx(btn);
            raceRow.AddChild(btn);
        }

        // 名字
        rightVbox.AddChild(_Lbl("你的名字", 22, new Color(0.85f, 0.78f, 0.55f)));
        _nameInput = new LineEdit { PlaceholderText = "输入名字...", CustomMinimumSize = new Vector2(0, 42) };
        _nameInput.AddThemeFontSizeOverride("font_size", 20);
        rightVbox.AddChild(_nameInput);

        // 性别
        rightVbox.AddChild(_Lbl("性别", 22, new Color(0.85f, 0.78f, 0.55f)));
        var genderRow = new HBoxContainer();
        genderRow.AddThemeConstantOverride("separation", 12);
        rightVbox.AddChild(genderRow);
        _genderGroup = new ButtonGroup();
        var maleBtn = new Button { Text = "男", CustomMinimumSize = new Vector2(80, 42), ToggleMode = true, ButtonPressed = true };
        maleBtn.AddThemeFontSizeOverride("font_size", 18);
        maleBtn.ButtonGroup = _genderGroup;
        maleBtn.Pressed += _UpdateRaceIllust;
        _AttachSfx(maleBtn);
        var femaleBtn = new Button { Text = "女", CustomMinimumSize = new Vector2(80, 42), ToggleMode = true };
        femaleBtn.AddThemeFontSizeOverride("font_size", 18);
        femaleBtn.ButtonGroup = _genderGroup;
        femaleBtn.Pressed += _UpdateRaceIllust;
        _AttachSfx(femaleBtn);
        genderRow.AddChild(maleBtn);
        genderRow.AddChild(femaleBtn);

        // 中间弹性空间，把按钮栏推到底部
        rightVbox.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        // 按钮栏（左下返回 + 右下前进）
        var btnRow = new HBoxContainer();
        rightVbox.AddChild(btnRow);

        var backBtn = new Button { Text = "← 返回", CustomMinimumSize = new Vector2(140, 44) };
        backBtn.AddThemeFontSizeOverride("font_size", 18);
        backBtn.Pressed += () => BladeHex.View.SceneTransition.ChangeSceneTo(GetTree(), "res://src/ui/main_menu/main_menu.tscn");
        _AttachSfx(backBtn);
        btnRow.AddChild(backBtn);

        btnRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var nextBtn = new Button { Text = "继续 →", CustomMinimumSize = new Vector2(160, 44) };
        nextBtn.AddThemeFontSizeOverride("font_size", 20);
        nextBtn.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.5f));
        nextBtn.Pressed += _ShowPhase2;
        _AttachSfx(nextBtn);
        btnRow.AddChild(nextBtn);
    }

    private void _UpdateRaceIllust()
    {
        if (_selectedRace == null || _phase1Illust == null) return;
        string key = RaceIllust.TryGetValue(_selectedRace.RaceName, out var id) ? id : "chaos_default";
        // 根据性别切换：race_human / race_human_f
        bool isFemale = (_genderGroup.GetPressedButton() as Button)?.Text == "女";
        if (isFemale) key += "_f";
        _ShowIllust(_phase1Illust, key);

        // 切换种族BGM
        _UpdateRaceBgm();
    }

    /// <summary>根据当前选中种族切换BGM</summary>
    private void _UpdateRaceBgm()
    {
        if (_selectedRace == null) return;
        var audio = BladeHex.Audio.AudioManager.Instance;
        if (audio == null) return;

        string variant = _selectedRace.raceId switch
        {
            RaceData.Race.Human => "race_human",
            RaceData.Race.Elf => "race_elf",
            RaceData.Race.Dwarf => "race_dwarf",
            RaceData.Race.HalfOrc => "race_halforc",
            RaceData.Race.HalfElf => "race_halfelf",
            _ => "default",
        };
        // 优先按变体播放Event场景BGM，找不到则保持当前BGM
        // 直接切换不交叉淡入（crossfadeTime=0）
        try
        {
            audio.PlayScenarioBgm(BladeHex.Audio.AudioManager.Scenario.Event, variant, 0.0f);
        }
        catch { /* 没注册该变体则忽略 */ }
    }

    // ════════════════════════════════════════════════════════════════
    // 阶段2：问答（左插图 + 右对话）
    // ════════════════════════════════════════════════════════════════
    private void _BuildPhase2()
    {
        var margin = _FullMargin();
        _phase2.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        margin.AddChild(vbox);

        // 标题（与阶段1对齐高度）
        var title = _Lbl("书写你的过往", 36, new Color(0.9f, 0.8f, 0.5f));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        // 主体：左插图 + 右对话
        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 24);
        body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        vbox.AddChild(body);

        _phase2Illust = _MakeIllustRect();
        body.AddChild(_WrapIllust(_phase2Illust, 720));

        // 右：问题+选项
        var rightPanel = new PanelContainer();
        rightPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        rightPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var rps = new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.07f, 0.88f) };
        rps.SetBorderWidthAll(1);
        rps.BorderColor = new Color(0.3f, 0.28f, 0.22f, 0.5f);
        rps.SetCornerRadiusAll(6);
        rps.SetContentMarginAll(30);
        rightPanel.AddThemeStyleboxOverride("panel", rps);
        body.AddChild(rightPanel);

        var rightVbox = new VBoxContainer();
        rightVbox.AddThemeConstantOverride("separation", 16);
        rightPanel.AddChild(rightVbox);

        // 顶部：左属性栏 + 右物品栏
        var topRow = new HBoxContainer();
        topRow.AddThemeConstantOverride("separation", 16);
        rightVbox.AddChild(topRow);

        // 左：属性栏
        var attrPanel = new PanelContainer();
        attrPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var aps = new StyleBoxFlat { BgColor = new Color(0.07f, 0.07f, 0.09f, 0.7f) };
        aps.SetBorderWidthAll(1);
        aps.BorderColor = new Color(0.25f, 0.22f, 0.18f, 0.5f);
        aps.SetCornerRadiusAll(4);
        aps.SetContentMarginAll(10);
        attrPanel.AddThemeStyleboxOverride("panel", aps);
        topRow.AddChild(attrPanel);

        var attrVbox = new VBoxContainer();
        attrVbox.AddThemeConstantOverride("separation", 4);
        attrPanel.AddChild(attrVbox);

        var attrTitle = _Lbl("属性", 14, new Color(0.6f, 0.55f, 0.45f));
        attrTitle.AutowrapMode = TextServer.AutowrapMode.Off;
        attrVbox.AddChild(attrTitle);

        _attrLabel = _Lbl("", 16, new Color(0.85f, 0.82f, 0.7f));
        _attrLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        attrVbox.AddChild(_attrLabel);

        // 右：物品栏
        var itemPanel = new PanelContainer();
        itemPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var ips = new StyleBoxFlat { BgColor = new Color(0.07f, 0.07f, 0.09f, 0.7f) };
        ips.SetBorderWidthAll(1);
        ips.BorderColor = new Color(0.25f, 0.22f, 0.18f, 0.5f);
        ips.SetCornerRadiusAll(4);
        ips.SetContentMarginAll(10);
        itemPanel.AddThemeStyleboxOverride("panel", ips);
        topRow.AddChild(itemPanel);

        var itemVbox = new VBoxContainer();
        itemVbox.AddThemeConstantOverride("separation", 4);
        itemPanel.AddChild(itemVbox);

        var itemTitle = _Lbl("行囊", 14, new Color(0.6f, 0.55f, 0.45f));
        itemTitle.AutowrapMode = TextServer.AutowrapMode.Off;
        itemVbox.AddChild(itemTitle);

        _itemLabel = _Lbl("（空）", 14, new Color(0.65f, 0.62f, 0.55f));
        _itemLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        itemVbox.AddChild(_itemLabel);

        // 弹性间隔，把问答区往下推
        rightVbox.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.3f });

        _questionText = new Label();
        _questionText.AddThemeFontSizeOverride("font_size", 22);
        _questionText.AddThemeColorOverride("font_color", new Color(0.92f, 0.9f, 0.85f));
        _questionText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        rightVbox.AddChild(_questionText);

        _choicesArea = new VBoxContainer();
        _choicesArea.AddThemeConstantOverride("separation", 10);
        rightVbox.AddChild(_choicesArea);

        // 弹性间隔，把按钮栏推到底部
        rightVbox.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        // 按钮栏（左下返回 + 右下踏上旅途）
        var btnRow = new HBoxContainer();
        rightVbox.AddChild(btnRow);

        var backBtn = new Button { Text = "← 重新选择", CustomMinimumSize = new Vector2(140, 44) };
        backBtn.AddThemeFontSizeOverride("font_size", 18);
        backBtn.Pressed += _ShowPhase1;
        _AttachSfx(backBtn);
        btnRow.AddChild(backBtn);

        btnRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var confirmBtn = new Button { Text = "踏上旅途 →", CustomMinimumSize = new Vector2(180, 44) };
        confirmBtn.AddThemeFontSizeOverride("font_size", 20);
        confirmBtn.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.5f));
        confirmBtn.Pressed += _OnConfirm;
        _AttachSfx(confirmBtn);
        btnRow.AddChild(confirmBtn);
    }

    // ════════════════════════════════════════════════════════════════
    // 刷新 & 交互
    // ════════════════════════════════════════════════════════════════
    private void _RefreshQuestion()
    {
        foreach (Node c in _choicesArea.GetChildren()) c.QueueFree();
        _RefreshAttr();

        if (_currentQuestion >= _questions.Count)
        {
            _questionText.Text = "你的过往已经书写完毕。\n准备好踏上旅途了吗？";
            return;
        }

        var q = _questions[_currentQuestion];
        _questionText.Text = q.Text;

        foreach (var choice in q.Choices)
        {
            var btn = new Button();
            btn.Text = choice.Text;
            btn.Alignment = HorizontalAlignment.Left;
            btn.CustomMinimumSize = new Vector2(0, 44);
            btn.AddThemeFontSizeOverride("font_size", 18);
            btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            var cap = choice;
            btn.Pressed += () => _OnChoiceSelected(cap);
            _AttachSfx(btn);
            _choicesArea.AddChild(btn);
        }
    }

    private void _RefreshAttr()
    {
        if (_attrLabel != null)
            _attrLabel.Text = $"力{_attrs["str"]} 敏{_attrs["dex"]} 体{_attrs["con"]} 智{_attrs["intel"]} 感{_attrs["wis"]} 魅{_attrs["cha"]}";
        _RefreshItems();
    }

    private void _RefreshItems()
    {
        if (_itemLabel == null) return;
        _itemLabel.Text = _items.Count == 0 ? "（空）" : string.Join("\n", _items);
    }

    private void _OnChoiceSelected(ChoiceData choice)
    {
        foreach (var kv in choice.Mods)
            _attrs[kv.Key] += kv.Value;
        _answers.Add(new AnswerRecord(_questions[_currentQuestion].Text, choice.Summary, choice.Mods));

        // 添加选项对应的物品
        if (ChoiceItems.TryGetValue(choice.Summary, out var item))
            _items.Add(item);

        _UpdateChoiceIllust(choice.Summary);
        _currentQuestion++;
        _RefreshQuestion();
    }

    // 选项 → 起始物品 映射
    private static readonly Dictionary<string, string> ChoiceItems = new()
    {
        // 人类童年
        ["田间劳作"] = "镰刀",
        ["师从学者"] = "羊皮纸笔记",
        ["街巷求生"] = "破旧匕首",
        ["神殿侍奉"] = "圣水瓶",
        // 人类城镇
        ["港口城市"] = "海图",
        ["边境要塞"] = "锁子甲片",
        ["学院城"] = "古籍",
        ["偏远山村"] = "猎弓",
        // 人类职业
        ["铁匠学徒"] = "铁锤",
        ["猎人向导"] = "兽皮",
        ["商队护卫"] = "短剑",
        ["抄写员"] = "墨水瓶",
        // 精灵童年
        ["林间嬉戏"] = "木叶护符",
        ["聆听史诗"] = "精灵歌谣集",
        ["星辰之术"] = "星象盘",
        ["月下剑舞"] = "训练用细剑",
        // 精灵青春
        ["剑舞修行"] = "精灵细剑",
        ["奥术研究"] = "奥术卷轴",
        ["世界树冥想"] = "世界树叶",
        ["游历人间"] = "人类货币若干",
        // 精灵族人评价
        ["天赋弓手"] = "精灵长弓",
        ["离经叛道"] = "破损徽记",
        ["沉默观察者"] = "观察笔记",
        ["精灵工匠"] = "魔法工具",
        // 矮人童年
        ["炉旁学徒"] = "小铁锤",
        ["矿洞拣石"] = "矿石样本",
        ["抄录卢恩"] = "卢恩石板",
        ["酒窖偷酒"] = "酒囊",
        // 矮人氏族
        ["铸造氏族"] = "氏族战锤",
        ["矿工氏族"] = "矿工镐",
        ["符文氏族"] = "符文护符",
        ["酿酒氏族"] = "上等矮人麦酒",
        // 矮人战斧
        ["自幼习武"] = "短斧",
        ["成年礼"] = "战斧",
        ["知识为刃"] = "石板典籍",
        ["被迫应战"] = "实用工兵斧",
        // 半兽人童年
        ["拳头立威"] = "兽牙项链",
        ["荒野独行"] = "粗制猎弓",
        ["藏身村庄"] = "母亲的护身符",
        ["自小流浪"] = "破布包袱",
        // 半兽人地位
        ["拳斗胜者"] = "战斗护腕",
        ["猎熊者"] = "熊皮披风",
        ["诡计者"] = "毒药小瓶",
        ["被驱逐者"] = "残破图腾",
        // 半兽人血脉
        ["克制怒火"] = "冥想念珠",
        ["拥抱狂暴"] = "战吼号角",
        ["融入社会"] = "整洁衣服",
        ["血脉冲突"] = "双面徽章",
        // 半精灵童年
        ["精灵森林"] = "精灵护身符",
        ["人类村庄"] = "人类护身符",
        ["两边轮住"] = "双语手册",
        ["独自长大"] = "自制弹弓",
        // 半精灵亲近
        ["精灵血脉"] = "精灵短弓",
        ["人类血脉"] = "人类短剑",
        ["独行者"] = "孤狼徽记",
        ["调停者"] = "双族信物",
        // 半精灵天赋
        ["双重体质"] = "夜行斗篷",
        ["读心直觉"] = "占卜骨签",
        ["跨文化魅力"] = "鲁特琴",
        ["灵巧创造"] = "工艺工具",
        // 离别（各种族都有）
        ["战火驱逐"] = "破损盾牌",
        ["求知欲望"] = "旅行笔记",
        ["复仇之路"] = "目标画像",
        ["命运召唤"] = "神秘符记",
        ["古林之债"] = "焦黑树枝",
        ["追寻真相"] = "残篇手稿",
        ["渴望炽烈"] = "人类纪念品",
        ["星辰指引"] = "星辰罗盘",
        ["圣物失窃"] = "圣物拓本",
        ["失落矿脉"] = "古老矿图",
        ["长老指引"] = "长老信物",
        ["追讨仇家"] = "通缉令",
        ["血染草原"] = "酋长牙坠",
        ["图腾召唤"] = "祖灵骨笛",
        ["厌倦争斗"] = "和平烟杆",
        ["传说猎物"] = "猎物足印拓本",
        ["因爱仇恨"] = "信物残片",
        ["寻找归宿"] = "旅行地图",
        ["血脉召唤"] = "古老吊坠",
        ["证明自己"] = "干粮包",
    };

    // ════════════════════════════════════════════════════════════════
    // 插图
    // ════════════════════════════════════════════════════════════════
    private static readonly Dictionary<string, string> ChoiceIllust = new()
    {
        // ── 人类 ──
        ["田间劳作"] = "human_childhood_farm",
        ["师从学者"] = "human_childhood_scholar",
        ["街巷求生"] = "human_childhood_street",
        ["神殿侍奉"] = "human_childhood_temple",
        ["港口城市"] = "human_port_city",
        ["边境要塞"] = "human_frontier_fort",
        ["学院城"] = "human_academy",
        ["偏远山村"] = "human_village",
        ["铁匠学徒"] = "human_blacksmith",
        ["猎人向导"] = "human_hunter",
        ["商队护卫"] = "human_caravan",
        ["抄写员"] = "human_scribe",
        ["战火驱逐"] = "human_departure_war",
        ["求知欲望"] = "human_departure_knowledge",
        ["复仇之路"] = "human_departure_revenge",
        ["命运召唤"] = "human_departure_fate",
        // ── 精灵 ──
        ["林间嬉戏"] = "elf_childhood_play",
        ["聆听史诗"] = "elf_childhood_epic",
        ["星辰之术"] = "elf_childhood_stars",
        ["月下剑舞"] = "elf_childhood_dance",
        ["剑舞修行"] = "elf_sword_dance",
        ["奥术研究"] = "elf_library",
        ["世界树冥想"] = "elf_world_tree",
        ["游历人间"] = "elf_among_humans",
        ["天赋弓手"] = "elf_archer",
        ["离经叛道"] = "elf_rebel",
        ["沉默观察者"] = "elf_observer",
        ["精灵工匠"] = "elf_artisan",
        ["古林之债"] = "elf_departure_war",
        ["追寻真相"] = "elf_departure_knowledge",
        ["渴望炽烈"] = "elf_departure_yearning",
        ["星辰指引"] = "elf_departure_fate",
        // ── 矮人 ──
        ["炉旁学徒"] = "dwarf_childhood_forge",
        ["矿洞拣石"] = "dwarf_childhood_mine",
        ["抄录卢恩"] = "dwarf_childhood_rune",
        ["酒窖偷酒"] = "dwarf_childhood_brewery",
        ["铸造氏族"] = "dwarf_forge_clan",
        ["矿工氏族"] = "dwarf_mine",
        ["符文氏族"] = "dwarf_rune",
        ["酿酒氏族"] = "dwarf_brewery",
        ["自幼习武"] = "dwarf_young_warrior",
        ["成年礼"] = "dwarf_coming_of_age",
        ["知识为刃"] = "dwarf_scholar",
        ["被迫应战"] = "dwarf_forced_fight",
        ["圣物失窃"] = "dwarf_departure_relic",
        ["失落矿脉"] = "dwarf_departure_mine",
        ["长老指引"] = "dwarf_departure_elder",
        ["追讨仇家"] = "dwarf_departure_revenge",
        // ── 半兽人 ──
        ["拳头立威"] = "halforc_childhood_fight",
        ["荒野独行"] = "halforc_childhood_wild",
        ["藏身村庄"] = "halforc_childhood_hidden",
        ["自小流浪"] = "halforc_childhood_wander",
        ["拳斗胜者"] = "halforc_arena",
        ["猎熊者"] = "halforc_hunt",
        ["诡计者"] = "halforc_cunning",
        ["被驱逐者"] = "halforc_outcast",
        ["克制怒火"] = "halforc_calm",
        ["拥抱狂暴"] = "halforc_rage",
        ["融入社会"] = "halforc_blend",
        ["血脉冲突"] = "halforc_conflict",
        ["血染草原"] = "halforc_departure_war",
        ["图腾召唤"] = "halforc_departure_totem",
        ["厌倦争斗"] = "halforc_departure_weary",
        ["传说猎物"] = "halforc_departure_hunt",
        // ── 半精灵 ──
        ["精灵森林"] = "halfelf_childhood_elven",
        ["人类村庄"] = "halfelf_childhood_human",
        ["两边轮住"] = "halfelf_childhood_split",
        ["独自长大"] = "halfelf_childhood_alone",
        ["精灵血脉"] = "halfelf_elven_side",
        ["人类血脉"] = "halfelf_human_side",
        ["独行者"] = "halfelf_loner",
        ["调停者"] = "halfelf_diplomat",
        ["双重体质"] = "halfelf_endurance",
        ["读心直觉"] = "halfelf_intuition",
        ["跨文化魅力"] = "halfelf_charm",
        ["灵巧创造"] = "halfelf_craft",
        ["因爱仇恨"] = "halfelf_departure_revenge",
        ["寻找归宿"] = "halfelf_departure_home",
        ["血脉召唤"] = "halfelf_departure_blood",
        ["证明自己"] = "halfelf_departure_prove",
    };

    private void _UpdateChoiceIllust(string summary)
    {
        if (ChoiceIllust.TryGetValue(summary, out var id))
            _ShowIllust(_phase2Illust, id);
    }

    private void _ShowIllust(TextureRect rect, string id)
    {
        if (rect == null) return;
        string path = $"{IllustBase}{id}.png";
        rect.Texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    // ════════════════════════════════════════════════════════════════
    // 确认
    // ════════════════════════════════════════════════════════════════
    private void _OnConfirm()
    {
        var unit = new UnitData();
        unit.UnitName = string.IsNullOrEmpty(_nameInput.Text) ? "无名旅人" : _nameInput.Text;
        unit.Race = _selectedRace;
        unit.Level = 1;
        unit.Str = _attrs["str"]; unit.Dex = _attrs["dex"]; unit.Con = _attrs["con"];
        unit.Intel = _attrs["intel"]; unit.Wis = _attrs["wis"]; unit.Cha = _attrs["cha"];

        var gs = GetNode<GlobalState>("/root/GlobalState");
        gs.IsLoadingSave = false;
        gs.WorldSize = 1;
        gs.PlayerOrigin = new Godot.Collections.Dictionary
        {
            { "race", _selectedRace! },
            { "unit_data", unit },
            { "gender", (_genderGroup.GetPressedButton() as Button)?.Text == "女" ? "female" : "male" },
        };
        LoadingScreen.LoadScene("res://src/scenes/overworld/overworld_scene.tscn", LoadingScreen.PhaseType.NewWorld);
    }

    // ════════════════════════════════════════════════════════════════
    // 问题生成
    // ════════════════════════════════════════════════════════════════
    private void _BuildQuestions()
    {
        _questions.Clear();
        if (_selectedRace == null) return;
        switch (_selectedRace.raceId)
        {
            case RaceData.Race.Human: _BuildHumanQuestions(); break;
            case RaceData.Race.Elf: _BuildElfQuestions(); break;
            case RaceData.Race.Dwarf: _BuildDwarfQuestions(); break;
            case RaceData.Race.HalfOrc: _BuildHalfOrcQuestions(); break;
            case RaceData.Race.HalfElf: _BuildHalfElfQuestions(); break;
        }
    }

    // ─── 人类（独立4问） ───
    private void _BuildHumanQuestions()
    {
        // 童年
        _questions.Add(new("你的人类童年是怎样度过的？", new List<ChoiceData> {
            new("在田间劳作，帮父母收割庄稼", "田间劳作", new() { ["str"] = 2, ["intel"] = -1, ["cha"] = -1 }),
            new("跟随一位老学者识字读书", "师从学者", new() { ["intel"] = 2, ["str"] = -1, ["con"] = -1 }),
            new("在街巷中摸爬滚打，学会了察言观色", "街巷求生", new() { ["dex"] = 2, ["con"] = -1, ["wis"] = -1 }),
            new("在神殿中侍奉祭司，聆听神谕", "神殿侍奉", new() { ["wis"] = 2, ["dex"] = -1, ["str"] = -1 }),
        }));
        _questions.Add(new("你在哪座城镇长大？", new List<ChoiceData> {
            new("繁华的港口城市，商贾云集", "港口城市", new() { ["cha"] = 2, ["str"] = -1, ["con"] = -1 }),
            new("边境要塞，时刻警惕蛮族入侵", "边境要塞", new() { ["str"] = 2, ["cha"] = -1, ["intel"] = -1 }),
            new("内陆学院城，书卷气息浓厚", "学院城", new() { ["intel"] = 2, ["str"] = -1, ["dex"] = -1 }),
            new("偏远山村，民风淳朴而坚韧", "偏远山村", new() { ["con"] = 2, ["cha"] = -1, ["intel"] = -1 }),
        }));
        _questions.Add(new("成年之前，你学会了什么谋生手段？", new List<ChoiceData> {
            new("铁匠学徒，锤炼了我的臂力", "铁匠学徒", new() { ["str"] = 2, ["dex"] = -1, ["cha"] = -1 }),
            new("猎人向导，在林中追踪猎物", "猎人向导", new() { ["dex"] = 2, ["intel"] = -1, ["cha"] = -1 }),
            new("商队护卫，见多识广", "商队护卫", new() { ["con"] = 2, ["intel"] = -1, ["wis"] = -1 }),
            new("抄写员，为贵族誊录文书", "抄写员", new() { ["intel"] = 2, ["str"] = -1, ["con"] = -1 }),
        }));
        _questions.Add(new("是什么让你离开故土，踏上未知的道路？", new List<ChoiceData> {
            new("家园被战火吞噬，我必须变得更强", "战火驱逐", new() { ["str"] = 2, ["cha"] = -1, ["intel"] = -1 }),
            new("我渴望见识这个世界的奥秘与真相", "求知欲望", new() { ["intel"] = 2, ["str"] = -1, ["con"] = -1 }),
            new("有人欠我一笔血债，我要亲手讨回", "复仇之路", new() { ["dex"] = 2, ["wis"] = -1, ["cha"] = -1 }),
            new("命运的低语引导着我，我无法抗拒", "命运召唤", new() { ["wis"] = 2, ["dex"] = -1, ["str"] = -1 }),
        }));
    }

    // ─── 精灵（独立4问） ───
    private void _BuildElfQuestions()
    {
        _questions.Add(new("精灵漫长的孩童时代，你是如何度过的？", new List<ChoiceData> {
            new("在林间嬉戏，与百年古树为伴", "林间嬉戏", new() { ["dex"] = 2, ["intel"] = -1, ["str"] = -1 }),
            new("聆听长老吟诵失落的史诗", "聆听史诗", new() { ["intel"] = 2, ["str"] = -1, ["con"] = -1 }),
            new("跟随母亲学习星辰与月相之术", "星辰之术", new() { ["wis"] = 2, ["str"] = -1, ["cha"] = -1 }),
            new("在月光下练习剑舞与轻步", "月下剑舞", new() { ["dex"] = 2, ["cha"] = -1, ["con"] = -1 }),
        }));
        _questions.Add(new("在漫长的精灵岁月中，你将青春献给了什么？", new List<ChoiceData> {
            new("星辉下的剑舞，追求刀锋的极致", "剑舞修行", new() { ["dex"] = 2, ["con"] = -1, ["cha"] = -1 }),
            new("古老图书馆中研读失落的奥术", "奥术研究", new() { ["intel"] = 2, ["str"] = -1, ["dex"] = -1 }),
            new("在世界树的枝桠间冥想百年", "世界树冥想", new() { ["wis"] = 2, ["str"] = -1, ["cha"] = -1 }),
            new("游历人类王国，学习他们短暂而炽热的生活方式", "游历人间", new() { ["cha"] = 2, ["wis"] = -1, ["con"] = -1 }),
        }));
        _questions.Add(new("你的族人如何看待你？", new List<ChoiceData> {
            new("天赋异禀的弓手，百步穿杨", "天赋弓手", new() { ["dex"] = 2, ["str"] = -1, ["intel"] = -1 }),
            new("离经叛道者，对古老传统不屑一顾", "离经叛道", new() { ["str"] = 2, ["wis"] = -1, ["intel"] = -1 }),
            new("沉默的观察者，洞悉一切却从不开口", "沉默观察者", new() { ["wis"] = 2, ["cha"] = -1, ["str"] = -1 }),
            new("精灵工匠，以魔法编织器物", "精灵工匠", new() { ["intel"] = 2, ["con"] = -1, ["str"] = -1 }),
        }));
        _questions.Add(new("是什么驱使你走出永恒之林，踏入凡俗世界？", new List<ChoiceData> {
            new("古林被铁与火侵蚀，我要追讨这笔债", "古林之债", new() { ["str"] = 2, ["cha"] = -1, ["intel"] = -1 }),
            new("一卷失落的手稿指向某个遥远的真相", "追寻真相", new() { ["intel"] = 2, ["str"] = -1, ["con"] = -1 }),
            new("永恒的宁静令人窒息，我渴望短暂者的炽烈", "渴望炽烈", new() { ["cha"] = 2, ["wis"] = -1, ["con"] = -1 }),
            new("梦境中的星辰指引我前行", "星辰指引", new() { ["wis"] = 2, ["dex"] = -1, ["str"] = -1 }),
        }));
    }

    // ─── 矮人（独立4问） ───
    private void _BuildDwarfQuestions()
    {
        _questions.Add(new("在矮人氏族的石厅中，你的童年是什么样的？", new List<ChoiceData> {
            new("在锻造炉旁递工具，看父辈打铁", "炉旁学徒", new() { ["str"] = 2, ["dex"] = -1, ["cha"] = -1 }),
            new("在矿洞深处帮长辈拣选矿石", "矿洞拣石", new() { ["con"] = 2, ["intel"] = -1, ["cha"] = -1 }),
            new("跟随符文师抄录古老的卢恩", "抄录卢恩", new() { ["intel"] = 2, ["str"] = -1, ["dex"] = -1 }),
            new("在酒窖里偷尝长辈酿造的烈酒", "酒窖偷酒", new() { ["cha"] = 2, ["wis"] = -1, ["dex"] = -1 }),
        }));
        _questions.Add(new("在矮人堡垒的深处，你的氏族以什么闻名？", new List<ChoiceData> {
            new("锻造传奇武器的铸造大师", "铸造氏族", new() { ["str"] = 2, ["dex"] = -1, ["cha"] = -1 }),
            new("开凿最深矿脉的矿工先驱", "矿工氏族", new() { ["con"] = 2, ["intel"] = -1, ["cha"] = -1 }),
            new("守护符文秘密的卢恩铭刻师", "符文氏族", new() { ["intel"] = 2, ["dex"] = -1, ["str"] = -1 }),
            new("酿造传世佳酿的酒窖守护者", "酿酒氏族", new() { ["cha"] = 2, ["str"] = -1, ["dex"] = -1 }),
        }));
        _questions.Add(new("你第一次拿起战斧是在什么时候？", new List<ChoiceData> {
            new("五岁时就在父亲的铁砧旁挥锤", "自幼习武", new() { ["str"] = 2, ["intel"] = -1, ["wis"] = -1 }),
            new("成年礼上，为了证明自己的勇气", "成年礼", new() { ["con"] = 2, ["cha"] = -1, ["dex"] = -1 }),
            new("从未拿过——我的武器是知识", "知识为刃", new() { ["intel"] = 2, ["str"] = -1, ["con"] = -1 }),
            new("当地精入侵时，被迫拿起武器", "被迫应战", new() { ["dex"] = 2, ["intel"] = -1, ["cha"] = -1 }),
        }));
        _questions.Add(new("是什么让你离开石厅，前往日光下的世界？", new List<ChoiceData> {
            new("氏族圣物失窃，我必须夺回它", "圣物失窃", new() { ["str"] = 2, ["cha"] = -1, ["intel"] = -1 }),
            new("古籍记载的失落矿脉在远方等待", "失落矿脉", new() { ["intel"] = 2, ["str"] = -1, ["con"] = -1 }),
            new("族中长老说，我的命运在地表之上", "长老指引", new() { ["wis"] = 2, ["dex"] = -1, ["str"] = -1 }),
            new("某个仇家逃到了人类的土地", "追讨仇家", new() { ["dex"] = 2, ["wis"] = -1, ["cha"] = -1 }),
        }));
    }

    // ─── 半兽人（独立4问） ───
    private void _BuildHalfOrcQuestions()
    {
        _questions.Add(new("作为混血孩子，你的成长岁月是怎样的？", new List<ChoiceData> {
            new("在部落中以拳头证明自己", "拳头立威", new() { ["str"] = 2, ["intel"] = -1, ["cha"] = -1 }),
            new("在荒野中独自捕猎为生", "荒野独行", new() { ["dex"] = 2, ["cha"] = -1, ["intel"] = -1 }),
            new("被人类母亲偷偷带走，藏在边境村庄", "藏身村庄", new() { ["wis"] = 2, ["str"] = -1, ["dex"] = -1 }),
            new("两边都不接纳——我自小流浪", "自小流浪", new() { ["con"] = 2, ["cha"] = -1, ["intel"] = -1 }),
        }));
        _questions.Add(new("在部落中，你靠什么赢得尊重？", new List<ChoiceData> {
            new("赤手空拳击败了最强的挑战者", "拳斗胜者", new() { ["str"] = 3, ["intel"] = -2, ["cha"] = -1 }),
            new("在荒野中独自猎杀了一头巨熊", "猎熊者", new() { ["con"] = 2, ["cha"] = -1, ["intel"] = -1 }),
            new("用计谋让两个敌对部落自相残杀", "诡计者", new() { ["intel"] = 2, ["str"] = -1, ["con"] = -1 }),
            new("我从未被接纳——我是被驱逐的弃儿", "被驱逐者", new() { ["wis"] = 2, ["str"] = -1, ["cha"] = -1 }),
        }));
        _questions.Add(new("人类的血液在你体内低语着什么？", new List<ChoiceData> {
            new("克制怒火，用冷静的头脑思考", "克制怒火", new() { ["wis"] = 2, ["str"] = -1, ["dex"] = -1 }),
            new("拥抱狂暴，让敌人在恐惧中颤抖", "拥抱狂暴", new() { ["str"] = 2, ["wis"] = -1, ["intel"] = -1 }),
            new("学会微笑，用人类的方式融入社会", "融入社会", new() { ["cha"] = 2, ["str"] = -1, ["con"] = -1 }),
            new("两种血脉的冲突让我比任何人都坚韧", "血脉冲突", new() { ["con"] = 2, ["cha"] = -1, ["dex"] = -1 }),
        }));
        _questions.Add(new("是什么驱使你离开荒原，走向文明世界？", new List<ChoiceData> {
            new("酋长被屠戮，我要让仇敌的鲜血染红草原", "血染草原", new() { ["str"] = 2, ["cha"] = -1, ["intel"] = -1 }),
            new("祖先的图腾召唤我前往远方", "图腾召唤", new() { ["wis"] = 2, ["dex"] = -1, ["str"] = -1 }),
            new("我厌倦了部落的争斗，想找一个属于自己的位置", "厌倦争斗", new() { ["cha"] = 2, ["str"] = -1, ["wis"] = -1 }),
            new("某个传说中的猎物，只有越过边境才能找到", "传说猎物", new() { ["dex"] = 2, ["wis"] = -1, ["cha"] = -1 }),
        }));
    }

    // ─── 半精灵（独立4问） ───
    private void _BuildHalfElfQuestions()
    {
        _questions.Add(new("在两族之间长大，你的童年记忆里有什么？", new List<ChoiceData> {
            new("在精灵母亲的森林家中度过", "精灵森林", new() { ["dex"] = 2, ["con"] = -1, ["str"] = -1 }),
            new("在人类父亲的村庄里长大", "人类村庄", new() { ["con"] = 2, ["wis"] = -1, ["dex"] = -1 }),
            new("两边轮流住，从未真正属于哪一方", "两边轮住", new() { ["wis"] = 2, ["cha"] = -1, ["con"] = -1 }),
            new("被双方亲族遗弃，独自长大", "独自长大", new() { ["intel"] = 2, ["str"] = -1, ["cha"] = -1 }),
        }));
        _questions.Add(new("你在两个世界之间长大，更亲近哪一边？", new List<ChoiceData> {
            new("精灵一侧——我继承了他们的优雅与敏锐", "精灵血脉", new() { ["dex"] = 2, ["con"] = -1, ["str"] = -1 }),
            new("人类一侧——我拥有他们的适应力与野心", "人类血脉", new() { ["con"] = 2, ["wis"] = -1, ["dex"] = -1 }),
            new("两边都不属于——我只属于我自己", "独行者", new() { ["wis"] = 2, ["cha"] = -1, ["con"] = -1 }),
            new("我学会了在两个世界之间斡旋调停", "调停者", new() { ["cha"] = 2, ["str"] = -1, ["wis"] = -1 }),
        }));
        _questions.Add(new("你的混血身份带给你最大的天赋是什么？", new List<ChoiceData> {
            new("精灵的夜视与人类的耐力", "双重体质", new() { ["con"] = 2, ["intel"] = -1, ["cha"] = -1 }),
            new("读懂任何人心思的直觉", "读心直觉", new() { ["wis"] = 2, ["str"] = -1, ["dex"] = -1 }),
            new("在任何文化中都能如鱼得水的魅力", "跨文化魅力", new() { ["cha"] = 2, ["con"] = -1, ["wis"] = -1 }),
            new("精灵的灵巧双手与人类的创造力", "灵巧创造", new() { ["dex"] = 2, ["str"] = -1, ["con"] = -1 }),
        }));
        _questions.Add(new("是什么驱使你独自上路？", new List<ChoiceData> {
            new("有人因我的混血身份伤害了我所爱的人", "因爱仇恨", new() { ["str"] = 2, ["cha"] = -1, ["intel"] = -1 }),
            new("我想找到一个真正属于我自己的归宿", "寻找归宿", new() { ["wis"] = 2, ["str"] = -1, ["con"] = -1 }),
            new("某种古老血脉的低语在召唤我", "血脉召唤", new() { ["intel"] = 2, ["str"] = -1, ["dex"] = -1 }),
            new("我想看看，混血的我能在世上走多远", "证明自己", new() { ["cha"] = 2, ["str"] = -1, ["wis"] = -1 }),
        }));
    }

    // ════════════════════════════════════════════════════════════════
    // 工具方法
    // ════════════════════════════════════════════════════════════════
    private static string _GetRaceId(RaceData? race)
    {
        if (race == null) return "human";
        return race.raceId switch
        {
            RaceData.Race.Human => "human",
            RaceData.Race.Elf => "elf",
            RaceData.Race.Dwarf => "dwarf",
            RaceData.Race.HalfOrc => "halforc",
            RaceData.Race.HalfElf => "halfelf",
            _ => "human",
        };
    }

    private static Label _Lbl(string text, int size, Color color)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return l;
    }

    /// <summary>给按钮挂上UI点击/悬停音效</summary>
    private static void _AttachSfx(BaseButton btn)
    {
        btn.Pressed += () => BladeHex.Audio.AudioManager.Instance?.PlaySfxName("ui_click");
        btn.MouseEntered += () => BladeHex.Audio.AudioManager.Instance?.PlaySfxName("ui_hover", -6.0f);
    }

    private static MarginContainer _FullMargin()
    {
        var m = new MarginContainer();
        m.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        m.AddThemeConstantOverride("margin_left", 50);
        m.AddThemeConstantOverride("margin_right", 50);
        m.AddThemeConstantOverride("margin_top", 30);
        m.AddThemeConstantOverride("margin_bottom", 30);
        return m;
    }

    private static TextureRect _MakeIllustRect()
    {
        var rect = new TextureRect();
        rect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        rect.StretchMode = TextureRect.StretchModeEnum.KeepAspect; // 完整显示，不裁切
        rect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        rect.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        return rect;
    }

    private static PanelContainer _WrapIllust(TextureRect rect, int minWidth)
    {
        var panel = new PanelContainer();
        // 高度由父HBox决定，宽度按比例（高度 × 3/4 = 宽度）。设置足够大的最小宽度避免压缩。
        panel.CustomMinimumSize = new Vector2(minWidth, 0);
        panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        panel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        var ps = new StyleBoxFlat { BgColor = new Color(0.04f, 0.04f, 0.06f, 0.9f) };
        ps.SetBorderWidthAll(2);
        ps.BorderColor = new Color(0.35f, 0.3f, 0.22f, 0.7f);
        ps.SetCornerRadiusAll(6);
        ps.SetContentMarginAll(4);
        panel.AddThemeStyleboxOverride("panel", ps);
        panel.AddChild(rect);
        return panel;
    }

    // ════════════════════════════════════════════════════════════════
    // 预加载
    // ════════════════════════════════════════════════════════════════
    private static readonly string[] PreloadPaths = {
        "res://src/assets/tiles/overworld/plains_0.png",
        "res://src/assets/tiles/overworld/grassland_0.png",
        "res://src/assets/tiles/overworld/forest_0.png",
        "res://src/assets/tiles/overworld/hills_0.png",
        "res://src/assets/tiles/overworld/mountain_0.png",
        "res://src/assets/tiles/overworld/deep_water_0.png",
        "res://src/scenes/overworld/overworld_scene.tscn",
    };
    private void _PreloadWorldTextures()
    {
        foreach (var p in PreloadPaths)
            if (ResourceLoader.Exists(p))
                ResourceLoader.LoadThreadedRequest(p);
    }
}
