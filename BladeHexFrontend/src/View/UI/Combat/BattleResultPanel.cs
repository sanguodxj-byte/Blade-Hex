// BattleResultPanel.cs
// 战斗结算面板 — 胜利/失败后显示经验/金币/掉落物
using Godot;
using BladeHex.Data;
using BladeHex.UI;

namespace BladeHex.UI.Combat;

/// <summary>
/// 战斗结算面板。在战斗结束后弹出,显示:
/// - 胜利/失败标题
/// - 获得经验值
/// - 获得金币
/// - 掉落物品列表
/// - "继续"按钮关闭面板
/// </summary>
[GlobalClass]
public partial class BattleResultPanel : CanvasLayer
{
    [Signal] public delegate void ContinueClickedEventHandler();

    private UITheme Theme => UITheme.Instance!;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    /// <summary>显示结算面板</summary>
    public void Show(bool victory, int xp, int gold, string[] lootNames)
    {
        // 全屏半透明遮罩
        var overlay = new ColorRect();
        overlay.Color = new Color(0, 0, 0, 0.6f);
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.MouseFilter = Control.MouseFilterEnum.Stop;
        AddChild(overlay);

        // 居中面板
        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(420, 320);
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        panel.AddThemeStyleboxOverride("panel",
            Theme.MakePanelStyle(Theme.BgPrimary, Theme.BorderDefault, 2, Theme.RadiusLg, Theme.SpacingLg));
        overlay.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", Theme.SpacingMd);
        panel.AddChild(vbox);

        // 标题
        var title = new Label();
        title.Text = victory ? "⚔ 战斗胜利!" : "✘ 战斗失败";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", victory ? Theme.TextPositive : new Color(1, 0.3f, 0.3f));
        vbox.AddChild(title);

        // 分隔线
        var sep = new HSeparator();
        vbox.AddChild(sep);

        // 奖励区
        if (victory)
        {
            var rewardGrid = new GridContainer();
            rewardGrid.Columns = 2;
            rewardGrid.AddThemeConstantOverride("h_separation", 20);
            rewardGrid.AddThemeConstantOverride("v_separation", 8);
            vbox.AddChild(rewardGrid);

            AddRewardRow(rewardGrid, "经验值", $"+{xp} XP", Theme.TextAccent);
            AddRewardRow(rewardGrid, "金币", $"+{gold} 金", new Color(1f, 0.85f, 0.3f));

            if (lootNames.Length > 0)
            {
                var lootLabel = new Label();
                lootLabel.Text = "掉落物品:";
                lootLabel.AddThemeFontSizeOverride("font_size", Theme.FontSizeMd);
                lootLabel.AddThemeColorOverride("font_color", Theme.TextSecondary);
                vbox.AddChild(lootLabel);

                foreach (var name in lootNames)
                {
                    var item = new Label();
                    item.Text = $"  • {name}";
                    item.AddThemeFontSizeOverride("font_size", Theme.FontSizeSm);
                    item.AddThemeColorOverride("font_color", Theme.TextPrimary);
                    vbox.AddChild(item);
                }
            }
        }
        else
        {
            var defeatMsg = new Label();
            defeatMsg.Text = "队伍被击败,撤退回营地...";
            defeatMsg.HorizontalAlignment = HorizontalAlignment.Center;
            defeatMsg.AddThemeFontSizeOverride("font_size", Theme.FontSizeMd);
            defeatMsg.AddThemeColorOverride("font_color", Theme.TextSecondary);
            vbox.AddChild(defeatMsg);
        }

        // 弹性占位
        var spacer = new Control();
        spacer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        vbox.AddChild(spacer);

        // 继续按钮
        var continueBtn = new Button();
        continueBtn.Text = "继续";
        continueBtn.CustomMinimumSize = new Vector2(120, 40);
        continueBtn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        Theme.ApplyButtonTheme(continueBtn);
        continueBtn.Pressed += () =>
        {
            Globals.AudioOrNull?.PlaySfxName("ui_click");
            EmitSignal(SignalName.ContinueClicked);
            QueueFree();
        };
        vbox.AddChild(continueBtn);
    }

    private void AddRewardRow(GridContainer grid, string label, string value, Color valueColor)
    {
        var lbl = new Label();
        lbl.Text = label;
        lbl.AddThemeFontSizeOverride("font_size", Theme.FontSizeMd);
        lbl.AddThemeColorOverride("font_color", Theme.TextSecondary);
        grid.AddChild(lbl);

        var val = new Label();
        val.Text = value;
        val.AddThemeFontSizeOverride("font_size", Theme.FontSizeMd + 2);
        val.AddThemeColorOverride("font_color", valueColor);
        grid.AddChild(val);
    }
}
