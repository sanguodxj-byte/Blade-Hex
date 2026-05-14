// CharacterAvatarControl.cs
// UI 角色头像控件 — 把 CharacterView2D 嵌入 Control 节点中，让所有 UI 复用同一套角色渲染。
// 用法示例：
//   var avatar = new CharacterAvatarControl();
//   avatar.CustomMinimumSize = new Vector2(80, 80);
//   parent.AddChild(avatar);
//   avatar.SetUnit(unitData);
using Godot;
using BladeHex.Data;

namespace BladeHex.View.Unit;

/// <summary>
/// UI 用角色头像/立绘控件。内部用 SubViewport 把 CharacterView2D 渲染成可在 Control 树里布局的纹理。
/// </summary>
[GlobalClass]
public partial class CharacterAvatarControl : SubViewportContainer
{
    /// <summary>展示模式：仅头像 / 半身 / 全身</summary>
    public enum DisplayMode
    {
        /// <summary>只看脸（裁出顶部）— 用于 64x64 头像框</summary>
        Head,
        /// <summary>半身（腰以上）— 用于战斗 UI 头像</summary>
        Bust,
        /// <summary>全身 — 用于队伍栏 / 角色详情 / 出身界面</summary>
        Full,
    }

    [Export] public DisplayMode Mode { get; set; } = DisplayMode.Bust;

    /// <summary>背景色（透明留空）</summary>
    [Export] public Color Background { get; set; } = new Color(0, 0, 0, 0);

    private SubViewport _viewport = null!;
    private CharacterView2D _view = null!;
    private UnitData? _data;
    private bool _initialized;

    public override void _Ready()
    {
        EnsureInitialized();
    }

    /// <summary>设置/更换要显示的角色</summary>
    public void SetUnit(UnitData? data, bool useSecondaryWeapon = false)
    {
        EnsureInitialized();
        _data = data;
        if (data != null)
        {
            _view.Setup(data, useSecondaryWeapon);
            FrameView();
            _view.PlayAnimation("idle");
        }
        else
        {
            // 清空所有层
            foreach (var child in _view.GetChildren())
                if (child is AnimatedSprite2D s) s.Visible = false;
        }
    }

    /// <summary>当装备/外观变化时刷新</summary>
    public void Refresh()
    {
        if (_data == null) return;
        _view.RefreshAll();
        FrameView();
    }

    /// <summary>播放动画（idle / hit / cheer 等）</summary>
    public void PlayAnimation(string animName)
    {
        EnsureInitialized();
        _view.PlayAnimation(animName);
    }

    public CharacterView2D View
    {
        get { EnsureInitialized(); return _view; }
    }

    // ===========================================================
    // 初始化
    // ===========================================================

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        // SubViewportContainer 默认会把 viewport size 设为 control size，无需手动 stretch
        Stretch = true;
        MouseFilter = MouseFilterEnum.Pass;

        _viewport = new SubViewport
        {
            Disable3D = true,
            TransparentBg = Background.A < 1.0f,
            Size = new Vector2I(80, 80),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            HandleInputLocally = false,
        };
        AddChild(_viewport);

        _view = new CharacterView2D { Name = "CharacterView2D" };
        _viewport.AddChild(_view);

        if (Background.A > 0)
        {
            var bg = new ColorRect
            {
                Color = Background,
                ZIndex = -100,
            };
            bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _viewport.AddChild(bg);
        }
    }

    private void FrameView()
    {
        // 视图尺寸跟随 Control 尺寸
        var sz = Size;
        if (sz.X < 4 || sz.Y < 4) sz = new Vector2(80, 80);
        _viewport.Size = new Vector2I((int)sz.X, (int)sz.Y);

        // 把角色锚到下方 + 中央，按 Mode 决定相机框
        // Body 的脚在 view.Position 处，向上延伸 BodyHeight 个像素
        float bodyH = Mathf.Max(_view.BodyHeight, 1.0f);

        // 计算合适的 ContentScale 让角色刚好充满 viewport
        // - Full：高度占满 90%
        // - Bust：从腰开始（高度的 60%），裁掉脚
        // - Head：取上 35%
        float visibleFrac = Mode switch
        {
            DisplayMode.Head => 0.35f,
            DisplayMode.Bust => 0.65f,
            _ => 0.95f,
        };
        float targetVisiblePx = sz.Y / Mathf.Max(visibleFrac, 0.1f);
        float scale = targetVisiblePx / bodyH;
        _view.ContentScale = scale;
        _view.Scale = new Vector2(scale, scale);

        // X 居中
        float anchorX = sz.X * 0.5f;
        // Y：脚部在 viewport 底部以上一点（Full）；Bust/Head 把脚放到下方更外侧
        float anchorY = Mode switch
        {
            DisplayMode.Head => sz.Y + bodyH * scale * 0.65f,
            DisplayMode.Bust => sz.Y + bodyH * scale * 0.20f,
            _ => sz.Y * 0.95f,
        };
        _view.Position = new Vector2(anchorX, anchorY);
    }

    // 控件尺寸变化时重新构图
    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationResized && _initialized && _data != null)
            FrameView();
    }
}
