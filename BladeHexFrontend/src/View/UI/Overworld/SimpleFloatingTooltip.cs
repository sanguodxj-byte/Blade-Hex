// SimpleFloatingTooltip.cs
// 轻量悬浮提示 — 继承 FloatingPanel 基类，统一暗金描边样式
// 用于大地图底部栏按钮的鼠标悬停中文提示
using Godot;
using BladeHex.UI.Common;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class SimpleFloatingTooltip : FloatingPanel
{
    private Label _label = null!;

    // 脱离父容器布局，使用全局屏幕坐标，避免在 PanelContainer 内与按钮重叠闪烁
    protected override bool UseTopLevel => true;

    public void SetText(string text)
    {
        _label.Text = text;
    }

    protected override void BuildContent()
    {
        _label = new Label();
        _label.AddThemeFontSizeOverride("font_size", 13);
        _label.AddThemeColorOverride("font_color", new Color(0.95f, 0.93f, 0.88f));
        _label.AutowrapMode = TextServer.AutowrapMode.Off;
        Content.AddChild(_label);
    }
}
