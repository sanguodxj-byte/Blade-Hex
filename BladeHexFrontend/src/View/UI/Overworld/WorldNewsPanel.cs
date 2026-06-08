using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Strategic;
using BladeHex.Strategic.WorldEvents;
using BladeHex.UI.Common;

namespace BladeHex.View.UI.Overworld;

/// <summary>
/// 世界新闻大厅面板
/// </summary>
public partial class WorldNewsPanel : PanelContainer
{
    private OverworldEntityManager? _entityMgr;
    private VBoxContainer _newsListContainer = null!;
    private Label _statusLabel = null!;

    public override void _Ready()
    {
        // 1. 高端定制毛玻璃圆角面板
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.05f, 0.07f, 0.95f),
            BorderWidthTop = 3,
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 3,
            BorderColor = new Color(0.5f, 0.45f, 0.3f, 0.8f), // BorderHighlight
            CornerRadiusTopLeft = 16,
            CornerRadiusTopRight = 16,
            CornerRadiusBottomLeft = 16,
            CornerRadiusBottomRight = 16,
            ContentMarginLeft = 25,
            ContentMarginRight = 25,
            ContentMarginTop = 20,
            ContentMarginBottom = 20
        };
        AddThemeStyleboxOverride("panel", style);

        CustomMinimumSize = new Vector2(550, 480);
        OverlayPanelLayout.Center(this);

        // 2. 主垂直布局
        var mainVbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(mainVbox);

        // 头部标题与关闭按钮
        var titleHbox = new HBoxContainer();
        mainVbox.AddChild(titleHbox);

        var titleLabel = new Label { Text = " 📜 列国世界风云志" };
        titleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.5f)); // TextAccent
        titleLabel.AddThemeFontSizeOverride("font_size", 22);
        titleHbox.AddChild(titleLabel);

        var closeBtn = new Button { Text = " X " };
        closeBtn.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        closeBtn.Pressed += () => Visible = false;
        titleHbox.AddChild(closeBtn);

        mainVbox.AddChild(new HSeparator());

        // 新闻滚动容器
        var scroll = new ScrollContainer 
        { 
            CustomMinimumSize = new Vector2(0, 320), 
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        
        _newsListContainer = new VBoxContainer 
        { 
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _newsListContainer.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(_newsListContainer);
        mainVbox.AddChild(scroll);

        mainVbox.AddChild(new HSeparator());

        // 底部提示
        _statusLabel = new Label 
        { 
            Text = "※ 本报自动记录列国最新的攻伐、易手与宣战重大事实纪要", 
            HorizontalAlignment = HorizontalAlignment.Center 
        };
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
        _statusLabel.AddThemeFontSizeOverride("font_size", 12);
        mainVbox.AddChild(_statusLabel);

        Visible = false;
    }

    /// <summary>
    /// 初始化数据引用并刷新新闻列表
    /// </summary>
    public void Initialize(OverworldEntityManager entityMgr)
    {
        _entityMgr = entityMgr;
        Refresh();
    }

    /// <summary>
    /// 全量刷新新闻流
    /// </summary>
    public void Refresh()
    {
        ClearList();
        if (_entityMgr == null) return;

        var newsQueue = _entityMgr.WorldEngine.NewsQueue;

        if (newsQueue.Count == 0)
        {
            var emptyLabel = new Label { Text = "大陆目前海晏河清，暂无世界大事发生。" };
            emptyLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
            emptyLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            _newsListContainer.AddChild(emptyLabel);
            return;
        }

        // 按时间倒序展示
        foreach (var news in newsQueue.AsEnumerable().Reverse())
        {
            var rowPanel = new PanelContainer();
            
            // 行背景样式
            var rowStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.12f, 0.12f, 0.14f, 0.6f),
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6,
                ContentMarginLeft = 15,
                ContentMarginRight = 15,
                ContentMarginTop = 10,
                ContentMarginBottom = 10
            };
            rowPanel.AddThemeStyleboxOverride("panel", rowStyle);

            var itemHbox = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            rowPanel.AddChild(itemHbox);

            // 1. 日期标识
            var dayLabel = new Label { Text = $"[ 第 {news.Day} 天 ] " };
            dayLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.7f, 0.2f)); // 金黄色
            dayLabel.AddThemeFontSizeOverride("font_size", 13);
            itemHbox.AddChild(dayLabel);

            // 2. 状态类型图标
            string prefix = news.Type switch
            {
                "poi_captured"   => "🏰 [易手] ",
                "war_declared"   => "⚔ [宣战] ",
                "peace"          => "🕊 [媾和] ",
                "army_formed"    => "⚔ [集结] ",
                "army_marching"  => "👣 [行军] ",
                "army_disbanded" => "🗃 [解散] ",
                "hero_captured"  => "⛓ [俘获] ",
                "hero_released"  => "🕊 [释放] ",
                "hero_recruited" => "⚔ [招降] ",
                "hero_died"      => "💀 [战死] ",
                "subparty_victory"  => "🏆 [大捷] ",
                "subparty_rejoined" => "🛡 [归队] ",
                "tournament_champion" => "🏆 [冠军] ",
                "hero_succession" => "⚜ [继承] ",
                _                => "📢 [通告] "
            };

            var iconLabel = new Label { Text = prefix };
            Color typeColor = news.Type switch
            {
                "poi_captured"   => new Color(0.9f, 0.35f, 0.35f), // 红色
                "war_declared"   => new Color(0.9f, 0.2f, 0.2f),   // 深红
                "peace"          => new Color(0.35f, 0.85f, 0.35f), // 绿色
                "army_formed"    => new Color(0.95f, 0.65f, 0.1f),  // 战金
                "army_marching"  => new Color(0.6f, 0.9f, 0.5f),   // 草绿
                "army_disbanded" => new Color(0.6f, 0.6f, 0.65f),  // 灰蓝
                "hero_captured"  => new Color(0.85f, 0.55f, 0.2f),  // 橘色
                "hero_released"  => new Color(0.45f, 0.85f, 0.55f), // 浅绿
                "hero_recruited" => new Color(0.6f, 0.85f, 1.0f),   // 浅蓝
                "hero_died"      => new Color(0.6f, 0.6f, 0.6f),    // 灰
                "subparty_victory"  => new Color(0.95f, 0.75f, 0.25f), // 金
                "subparty_rejoined" => new Color(0.7f, 0.85f, 1.0f),   // 蓝
                "tournament_champion" => new Color(0.8f, 0.6f, 1.0f),   // 紫色
                "hero_succession" => new Color(0.95f, 0.85f, 0.5f),  // 浅黄
                _                => new Color(0.5f, 0.7f, 1.0f)    // 蓝色
            };
            iconLabel.AddThemeColorOverride("font_color", typeColor);
            iconLabel.AddThemeFontSizeOverride("font_size", 13);
            itemHbox.AddChild(iconLabel);

            // 3. 具体文本描述 (支持自动换行)
            var descLabel = new Label 
            { 
                Text = news.Description,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            descLabel.AddThemeFontSizeOverride("font_size", 13);
            descLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
            itemHbox.AddChild(descLabel);

            _newsListContainer.AddChild(rowPanel);
        }
    }

    private void ClearList()
    {
        foreach (var child in _newsListContainer.GetChildren())
        {
            child.QueueFree();
        }
    }
}
