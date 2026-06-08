using Godot;
using BladeHex.UI;
using BladeHex.UI.Common;

namespace BladeHex.View.UI.Encyclopedia;

/// <summary>
/// Tutorial detail popup panel — FloatingPanel base class for unified dark gold border.
/// </summary>
[GlobalClass]
public partial class TutorialDetailPanel : FloatingPanel
{
    private Label _titleLabel = null!;
    private Label _contentLabel = null!;

    protected override bool UseTopLevel => true;

    public void ShowTutorial(string chapterTitle, string pageTitle, string content)
    {
        _titleLabel.Text = pageTitle;
        _contentLabel.Text = content;

        // Update window title by re-setting the panel title prefix
        // Title is handled by the chapter label or we can add a subtitle
        ShowAtMouse();
    }

    protected override void BuildContent()
    {
        _titleLabel = MakeTitleLabel("", 18);
        Content.AddChild(_titleLabel);

        Content.AddChild(MakeSeparator(0.3f));

        _contentLabel = new Label();
        _contentLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _contentLabel.AddThemeFontSizeOverride("font_size", 14);
        _contentLabel.AddThemeColorOverride("font_color",
            (UITheme.Instance?.TextPrimary) ?? new Color(0.95f, 0.93f, 0.88f));
        _contentLabel.CustomMinimumSize = new Vector2(380, 0);
        Content.AddChild(_contentLabel);
    }
}