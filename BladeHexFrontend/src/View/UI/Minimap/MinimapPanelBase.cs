// MinimapPanelBase.cs
// 小地图面板通用基类 — 提供 UI 框架、Image 管理、地形纹理渲染、视野框、覆盖层绘制
// 大地图和战斗场景各自继承并提供数据绑定
using Godot;

namespace BladeHex.UI.Minimap;

/// <summary>
/// 小地图面板基类 — PanelContainer，包含收起/展开、地图纹理、覆盖层绘制框架。
/// 子类实现：地形颜色采样、标记绘制、增量更新逻辑。
/// </summary>
[GlobalClass]
public abstract partial class MinimapPanelBase : PanelContainer
{
	// ========================================
	// 信号
	// ========================================
	[Signal] public delegate void MinimapClickedEventHandler(Vector2 worldPos);

	// ========================================
	// 配置（子类可覆盖）
	// ========================================
	protected virtual int MapPixelWidth => 180;
	protected virtual int MapPixelHeight => 180;
	protected virtual int PanelMargin => 6;
	protected virtual float PanelAlpha => 0.75f;
	protected virtual string Title => "地图";
	protected virtual bool ShowHeader => true;
	protected virtual bool Collapsible => true;

	// ========================================
	// 颜色常量
	// ========================================
	protected static readonly Color DefaultBgColor = new(0.02f, 0.02f, 0.04f, 0.75f);
	protected static readonly Color DefaultBorderColor = new(0.4f, 0.35f, 0.25f, 0.7f);
	protected static readonly Color DefaultViewRectColor = new(1.0f, 1.0f, 1.0f, 0.6f);

	// ========================================
	// 内部控件
	// ========================================
	protected Image MapImage = null!;
	protected ImageTexture MapTexture = null!;
	protected TextureRect MapRect = null!;
	protected Control OverlayControl = null!;
	protected Control ContentContainer = null!;
	private Button? _toggleBtn;
	private bool _collapsed;
	protected bool Initialized;

	// ========================================
	// 生命周期
	// ========================================

	/// <summary>构建 UI 框架。子类在自己的初始化方法中调用。</summary>
	protected void BuildBaseUI()
	{
		var style = new StyleBoxFlat { BgColor = DefaultBgColor };
		style.SetBorderWidthAll(2);
		style.BorderColor = DefaultBorderColor;
		style.SetCornerRadiusAll(4);
		style.SetContentMarginAll(PanelMargin);
		AddThemeStyleboxOverride("panel", style);

		Modulate = new Color(1, 1, 1, PanelAlpha);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 4);
		AddChild(vbox);

		// 顶部：标题 + 收起按钮（可选）
		if (ShowHeader)
		{
			var header = new HBoxContainer();
			header.AddThemeConstantOverride("separation", 4);
			vbox.AddChild(header);

			var titleLabel = new Label { Text = Title };
			titleLabel.AddThemeFontSizeOverride("font_size", 11);
			titleLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.65f, 0.5f));
			titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			header.AddChild(titleLabel);

			if (Collapsible)
			{
				_toggleBtn = new Button { Text = "▼", CustomMinimumSize = new Vector2(24, 20) };
				_toggleBtn.AddThemeFontSizeOverride("font_size", 10);
				_toggleBtn.Pressed += ToggleCollapse;
				header.AddChild(_toggleBtn);
			}
		}

		// 内容容器
		ContentContainer = new Control { CustomMinimumSize = new Vector2(MapPixelWidth, MapPixelHeight) };
		vbox.AddChild(ContentContainer);

		// 地图纹理
		MapImage = Image.CreateEmpty(MapPixelWidth, MapPixelHeight, false, Image.Format.Rgba8);
		MapImage.Fill(DefaultBgColor);
		MapTexture = ImageTexture.CreateFromImage(MapImage);

		MapRect = new TextureRect
		{
			Texture = MapTexture,
			CustomMinimumSize = new Vector2(MapPixelWidth, MapPixelHeight),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspect,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
		};
		ContentContainer.AddChild(MapRect);

		// 覆盖层（用于绘制动态元素：视野框、玩家标记等）
		OverlayControl = new Control { Name = "Overlay" };
		OverlayControl.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		OverlayControl.MouseFilter = MouseFilterEnum.Ignore;
		MapRect.AddChild(OverlayControl);
		OverlayControl.Draw += OnOverlayDrawInternal;

		// 输入
		MapRect.GuiInput += OnMapInputInternal;

		CustomMinimumSize = new Vector2(MapPixelWidth + PanelMargin * 2, 0);
	}

	// ========================================
	// 收起/展开
	// ========================================

	private void ToggleCollapse()
	{
		_collapsed = !_collapsed;
		ContentContainer.Visible = !_collapsed;
		if (_toggleBtn != null)
			_toggleBtn.Text = _collapsed ? "🗺" : "▼";

		if (_collapsed)
		{
			MouseFilter = MouseFilterEnum.Ignore;
			AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
			Modulate = new Color(1, 1, 1, 1.0f);
		}
		else
		{
			MouseFilter = MouseFilterEnum.Stop;
			var style = new StyleBoxFlat { BgColor = DefaultBgColor };
			style.SetBorderWidthAll(2);
			style.BorderColor = DefaultBorderColor;
			style.SetCornerRadiusAll(4);
			style.SetContentMarginAll(PanelMargin);
			AddThemeStyleboxOverride("panel", style);
			Modulate = new Color(1, 1, 1, PanelAlpha);
		}
	}

	protected bool IsCollapsed => _collapsed;

	// ========================================
	// 覆盖层绘制
	// ========================================

	private void OnOverlayDrawInternal()
	{
		DrawOverlay(OverlayControl);
	}

	/// <summary>子类实现覆盖层绘制（视野框、玩家标记、单位标记等）</summary>
	protected abstract void DrawOverlay(Control overlay);

	/// <summary>请求覆盖层重绘</summary>
	protected void RequestOverlayRedraw()
	{
		if (OverlayControl != null && IsInstanceValid(OverlayControl))
			OverlayControl.QueueRedraw();
	}

	// ========================================
	// 绘制辅助
	// ========================================

	/// <summary>在覆盖层上绘制视野矩形框</summary>
	protected void DrawViewRect(Control overlay, Rect2 rectInMapPixels, Color? color = null)
	{
		if (rectInMapPixels.Size.X <= 0 || rectInMapPixels.Size.Y <= 0) return;
		overlay.DrawRect(rectInMapPixels, color ?? DefaultViewRectColor, false, 1.5f);
	}

	/// <summary>在覆盖层上绘制闪烁圆点标记</summary>
	protected void DrawPulsingDot(Control overlay, Vector2 pos, Color color, float radius = 3.5f)
	{
		float pulse = 0.7f + 0.3f * Mathf.Sin((float)Time.GetTicksMsec() / 300.0f);
		overlay.DrawCircle(pos, radius, new Color(color.R, color.G, color.B, pulse));
	}

	/// <summary>在覆盖层上绘制实心圆点标记</summary>
	protected static void DrawDot(Control overlay, Vector2 pos, Color color, float radius = 2.0f)
	{
		overlay.DrawCircle(pos, radius, color);
	}

	// ========================================
	// 地图纹理更新
	// ========================================

	/// <summary>更新纹理（在修改 MapImage 后调用）</summary>
	protected void FlushTexture()
	{
		MapTexture.Update(MapImage);
	}

	// ========================================
	// 输入处理
	// ========================================

	private void OnMapInputInternal(InputEvent ev)
	{
		HandleMapInput(ev);
	}

	/// <summary>子类实现小地图输入处理（点击跳转等）</summary>
	protected virtual void HandleMapInput(InputEvent ev)
	{
		if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			var worldPos = MinimapToWorld(mb.Position);
			EmitSignal(SignalName.MinimapClicked, worldPos);
		}
	}

	// ========================================
	// 坐标转换（子类必须实现）
	// ========================================

	/// <summary>世界坐标 → 小地图像素坐标</summary>
	protected abstract Vector2 WorldToMinimap(Vector2 worldPos);

	/// <summary>小地图像素坐标 → 世界坐标</summary>
	protected abstract Vector2 MinimapToWorld(Vector2 minimapPos);
}
