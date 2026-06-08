// RelationGraphView.cs
// 极简 2D 圆形辐射布局关系图谱 — 用 Godot 纯 UI 节点实现
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Strategic;
using BladeHex.Strategic.Hero;

namespace BladeHex.View.UI.Encyclopedia;

public partial class RelationGraphView : Control
{
    private static readonly Color TextPositive = new(0.3f, 0.85f, 0.3f);
    private static readonly Color TextNegative = new(0.9f, 0.3f, 0.25f);
    private static readonly Color TextAccent = new(0.9f, 0.8f, 0.5f);
    private static readonly Color TextPrimary = new(0.95f, 0.93f, 0.88f);

    private HeroData? _centerHero;
    private OverworldEntityManager? _entityMgr;

    public void Initialize(HeroData centerHero, OverworldEntityManager entityMgr)
    {
        _centerHero = centerHero;
        _entityMgr = entityMgr;
        
        CustomMinimumSize = new Vector2(400, 400);
        Rebuild();
    }

    public void Rebuild()
    {
        // 1. 清理全部旧连线和节点
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        if (_centerHero == null || _entityMgr == null) return;

        Vector2 center = new(200, 200);

        // 2. 绘制中心节点
        var centerNode = _CreateNodeWidget(_centerHero.DisplayName, _centerHero.FactionId, true);
        centerNode.Position = center - centerNode.CustomMinimumSize / 2.0f;
        AddChild(centerNode);

        // 3. 收集所有强关联领主 (好感度绝对值 >= 10，最多8人)
        var allRelations = new List<(string heroId, int value)>();
        
        // 遍历所有英雄以获取好感度
        foreach (var hero in _entityMgr.Heroes.AllHeroes)
        {
            if (hero.HeroId == _centerHero.HeroId) continue;
            int rVal = _entityMgr.Relations.Get(_centerHero.HeroId, hero.HeroId);
            if (Math.Abs(rVal) >= 10)
            {
                allRelations.Add((hero.HeroId, rVal));
            }
        }

        // 按绝对值降序排序，取前8个
        var topRelations = allRelations
            .OrderByDescending(r => Math.Abs(r.value))
            .Take(8)
            .ToList();

        // 如果包含玩家，确保玩家始终以第一顺位置顶强力展示
        int playerRel = _entityMgr.Relations.Get("player", _centerHero.HeroId);
        bool hasPlayer = Math.Abs(playerRel) >= 10 || true; // 强行包含玩家以增进沉浸感
        if (hasPlayer)
        {
            // 剔除可能已存在的玩家
            topRelations.RemoveAll(r => r.heroId == "player");
            topRelations.Insert(0, ("player", playerRel));
        }

        // 4. 辐射排列节点
        float radius = 135.0f;
        for (int i = 0; i < topRelations.Count; i++)
        {
            var rel = topRelations[i];
            float angle = i * (MathF.PI * 2.0f) / topRelations.Count;
            Vector2 offset = new(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
            Vector2 targetPos = center + offset;

            // 获取显示名和势力
            string name = "你";
            string faction = "player";
            if (rel.heroId != "player")
            {
                var h = _entityMgr.Heroes.Get(rel.heroId);
                if (h != null)
                {
                    name = h.DisplayName;
                    faction = h.FactionId;
                }
            }

            var nodeWidget = _CreateNodeWidget(name, faction, false);
            nodeWidget.Position = targetPos - nodeWidget.CustomMinimumSize / 2.0f;

            // Tooltip 展示好感度
            nodeWidget.TooltipText = $"与中心英雄关系好感度: {(rel.value >= 0 ? "+" : "")}{rel.value}";

            // 5. 绘制 Line2D 连线 (柔和半透明)
            var line = new Line2D();
            line.AddPoint(center);
            line.AddPoint(targetPos);
            line.Width = Math.Max(1.5f, (Math.Abs(rel.value) / 100.0f) * 5.0f);
            var baseCol = rel.value >= 0 ? TextPositive : TextNegative;
            line.DefaultColor = new Color(baseCol.R, baseCol.G, baseCol.B, 0.6f);
            line.ZIndex = -1; // 连线显示在节点下方
            AddChild(line);

            // 加入节点
            AddChild(nodeWidget);
        }
    }

    private Control _CreateNodeWidget(string name, string factionId, bool isCenter)
    {
        var node = new PanelContainer();
        node.CustomMinimumSize = new Vector2(96, 44);

        // 势力颜色解析
        Color factionColor = isCenter ? TextAccent : new Color(0.7f, 0.7f, 0.75f);
        if (_entityMgr != null)
        {
            var nation = _entityMgr.Nations.FirstOrDefault(n => n.Id == factionId);
            if (nation != null)
            {
                float hue = (Math.Abs(nation.Id.GetHashCode()) % 360) / 360.0f;
                factionColor = Color.FromHsv(hue, 0.7f, 0.9f);
            }
            else if (factionId == "player")
            {
                factionColor = new Color(0.3f, 0.7f, 1.0f);
            }
        }

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.04f, 0.06f, 0.95f),
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderColor = factionColor,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
            ShadowSize = 6,
            ShadowColor = new Color(0, 0, 0, 0.5f)
        };
        node.AddThemeStyleboxOverride("panel", style);

        var lbl = new Label
        {
            Text = name,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ClipText = true
        };
        lbl.AddThemeFontSizeOverride("font_size", isCenter ? 14 : 12);
        lbl.AddThemeColorOverride("font_color", TextPrimary);
        node.AddChild(lbl);

        return node;
    }
}
