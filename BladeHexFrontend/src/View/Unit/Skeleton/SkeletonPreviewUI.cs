// SkeletonPreviewUI.cs
// 预览场景的 UI 控制面板 — 独立于 SkeletonPreview 的 UI 层
// 可作为 CanvasLayer 子节点挂载，提供按钮控制动画和体型切换
using Godot;
using BladeHex.View.Unit.Skeleton;

namespace BladeHex.View.Unit;

/// <summary>
/// 骨骼预览 UI 面板。
/// 提供动画播放按钮和体型切换按钮。
/// 需要在场景中设置 SkeletonPreviewPath 指向 SkeletonPreview 节点。
/// </summary>
[GlobalClass]
public partial class SkeletonPreviewUI : Control
{
    [Export] public NodePath SkeletonPreviewPath { get; set; } = new("../SkeletonPreview");

    private SkeletonPreview? _preview;

    public override void _Ready()
    {
        _preview = GetNode<SkeletonPreview>(SkeletonPreviewPath);
        if (_preview == null)
        {
            GD.PushWarning("[SkeletonPreviewUI] 未找到 SkeletonPreview 节点");
            return;
        }

        BuildUI();
    }

    private void BuildUI()
    {
        // 构建 UI（程序化，不依赖 .tscn 中的按钮节点）
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        panel.OffsetRight = 260;
        panel.OffsetBottom = 500;
        panel.OffsetLeft = 10;
        panel.OffsetTop = 10;
        AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        margin.AddChild(vbox);

        // 标题
        var title = new Label { Text = "上半身骨骼动画预览", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 18);
        vbox.AddChild(title);

        vbox.AddChild(new HSeparator());

        // 动画按钮
        var animLabel = new Label { Text = "动画:" };
        vbox.AddChild(animLabel);

        AddAnimButton(vbox, "Idle (待机呼吸)", "idle");
        AddAnimButton(vbox, "Attack Melee (近战挥砍)", "attack_melee");
        AddAnimButton(vbox, "Attack Ranged (远程射击)", "attack_ranged");
        AddAnimButton(vbox, "Cast (施法)", "cast");
        AddAnimButton(vbox, "Hit (受击)", "hit");
        AddAnimButton(vbox, "Die (死亡)", "die");

        vbox.AddChild(new HSeparator());

        // 体型按钮
        var bodyLabel = new Label { Text = "体型:" };
        vbox.AddChild(bodyLabel);

        AddBodyTypeButton(vbox, "Standard (标准)", BodyType.Standard);
        AddBodyTypeButton(vbox, "Heavy (重型/矮人)", BodyType.Heavy);
        AddBodyTypeButton(vbox, "Slim (纤细/精灵)", BodyType.Slim);
        AddBodyTypeButton(vbox, "Large (大型/Boss)", BodyType.Large);

        vbox.AddChild(new HSeparator());

        // 朝向按钮
        var facingLabel = new Label { Text = "朝向:" };
        vbox.AddChild(facingLabel);

        var hbox = new HBoxContainer();
        vbox.AddChild(hbox);

        var btnRight = new Button { Text = "→ 右", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        btnRight.Pressed += () => _preview!.SetFacingLeft(false);
        hbox.AddChild(btnRight);

        var btnLeft = new Button { Text = "← 左", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        btnLeft.Pressed += () => _preview!.SetFacingLeft(true);
        hbox.AddChild(btnLeft);
    }

    private void AddAnimButton(VBoxContainer parent, string label, string animName)
    {
        var btn = new Button { Text = label };
        btn.Pressed += () =>
        {
            if (_preview != null)
                _preview.CurrentAnimation = animName;
        };
        parent.AddChild(btn);
    }

    private void AddBodyTypeButton(VBoxContainer parent, string label, BodyType bodyType)
    {
        var btn = new Button { Text = label };
        btn.Pressed += () =>
        {
            if (_preview != null)
                _preview.CurrentBodyType = bodyType;
        };
        parent.AddChild(btn);
    }
}
