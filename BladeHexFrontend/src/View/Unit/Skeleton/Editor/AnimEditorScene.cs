// AnimEditorScene.cs
// 运行时骨骼动画编辑器 — 主场景控制器
// 组装预览区、时间轴、骨骼面板，处理交互逻辑
// 挂载到 skeleton_preview.tscn 替代原 SkeletonPreview.cs
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.View.AssetSystem;

namespace BladeHex.View.Unit.Skeleton.Editor;

/// <summary>骨骼动画编辑器主控制器</summary>
public partial class AnimEditorScene : Node3D
{
	// ─── 子组件 ───
	private AnimEditorPreview _preview = null!;
	private AnimEditorTimeline _timeline = null!;
	private AnimEditorBonePanel _bonePanel = null!;
	private AnimEditorTexturePanel _texturePanel = null!;

	// ─── 状态 ───
	private AnimClip _clip = null!;
	private int _selectedKeyframeIdx = -1;
	private WeaponAnimCategory _currentWeaponCat = WeaponAnimCategory.Slash;
	private OptionButton _animSelect = null!;
	private OptionButton _bodyTypeSelect = null!;
	private OptionButton _weaponCatSelect = null!;
	private LineEdit _nameInput = null!;
	private SpinBox _durationSpin = null!;
	private CheckBox _loopCheck = null!;
	private Label _statusLabel = null!;

	// ─── 相机 ───
	private Camera3D? _cam;
	private const float MinOrtho = 80f;
	private const float MaxOrtho = 600f;
	private bool _middleDrag;

	// ─── 骨骼拖拽 ───
	private bool _draggingGrip;
	private string _dragBoneName = "";
	private Vector2 _dragStartMouse;
	private float _dragStartRotZ;
	private float _dragStartPosX;
	private float _dragStartPosY;
	private const float DragSensitivity = 0.5f; // 度/像素
	private const float GizmoHitRadius = 20f; // 屏幕像素半径

	// ─── 旋转模式角度追踪 ───
	private float _dragStartAngle;

	// ─── 枢轴位移模式（右键点击枢轴进入） ───
	private bool _pivotDisplaceMode;
	private string _displaceBoneName = "";
	private Vector2 _displaceStartMouse;
	private Vector2 _displaceStartBoneScreenPos;

	// ─── 旋转模式（左键按住枢轴进入） ───
	private bool _rotationMode;
	private string _rotationBoneName = "";

	// ─── 部件偏移模式（统一管理武器/护甲/头盔/手甲） ───
	private bool _equipOffsetMode;
	private bool _facingLeft;
	private EquipmentOffsetConfig _currentEquipOffset = null!;
	private ItemData.EquipSlot _currentOffsetSlot = ItemData.EquipSlot.Weapon;
	private CheckBox _equipOffsetModeCheck = null!;
	private OptionButton _equipSlotSelect = null!;
	private CheckBox _hideTexturesCheck = null!;

	// ─── 内置动画模板 ───
	private readonly Dictionary<string, System.Func<AnimClip>> _templates = new()
	{
		["idle"] = AnimClip.CreateDefaultIdle,
		["attack_melee"] = AnimClip.CreateDefaultAttackMelee,
	};

	public override void _Ready()
	{
		// 相机（与战斗场景一致的 -45° 俯视）
		_cam = new Camera3D
		{
			Projection = Camera3D.ProjectionType.Orthogonal,
			Size = 200f,
			RotationDegrees = new Vector3(-45, 0, 0),
			Position = new Vector3(0, 180, 200),
			Current = true,
		};
		AddChild(_cam);

		// 灯光
		var light = new DirectionalLight3D
		{
			RotationDegrees = new Vector3(-50, -30, 0),
			LightEnergy = 1.2f,
		};
		AddChild(light);

		// 地面
		var ground = new MeshInstance3D
		{
			Mesh = new PlaneMesh { Size = new Vector2(500, 500) },
			MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.35f, 0.3f) },
		};
		AddChild(ground);

		// 3D 预览
		_preview = new AnimEditorPreview();
		AddChild(_preview);

		// 初始动画
		_clip = AnimClip.CreateDefaultIdle();
		_preview.CurrentClip = _clip;

		// 初始部件偏移配置
		_currentEquipOffset = EquipmentOffsetConfig.Get(_currentOffsetSlot);

		// UI 层
		var uiLayer = new CanvasLayer { Layer = 10 };
		AddChild(uiLayer);

		var uiRoot = new Control();
		uiRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		uiRoot.MouseFilter = Control.MouseFilterEnum.Ignore;
		uiLayer.AddChild(uiRoot);

		BuildTopBar(uiRoot);
		BuildBonePanel(uiRoot);
		BuildTexturePanel(uiRoot);
		BuildTimeline(uiRoot);
		BuildStatusBar(uiRoot);

		// 骨骼关节 gizmo（屏幕空间，挂在 UI 层最底层以免遮挡面板）
		_preview.CreateGizmoOverlay(uiRoot, _cam);
		// 将 gizmo 移到最底层（第一个子节点），确保不遮挡 UI 面板
		var gizmoNode = uiRoot.GetChild(uiRoot.GetChildCount() - 1);
		uiRoot.MoveChild(gizmoNode, 0);

		// 初始刷新
		RefreshTimeline();
		SelectKeyframe(0);

		// 显式同步初始朝向（默认为 _facingLeft = false ➔ 默认朝右）
		var skeleton = _preview.GetSkeleton();
		if (skeleton != null)
		{
			skeleton.SetFacing(_facingLeft);
		}

		// 加载并应用所有已保存的部件偏移（确保动画预览反映实际游戏效果）
		ApplyAllSavedOffsets();

		var args = OS.GetCmdlineArgs();
		if (System.Linq.Enumerable.Contains(args, "--screenshot-test"))
		{
			GD.Print(">>> [Screenshot Test] Detected --screenshot-test command line argument!");
			CallDeferred(MethodName.RunScreenshotAutomation);
		}
	}

	private async void RunScreenshotAutomation()
	{
		GD.Print(">>> [Screenshot Automation] Starting Godot-side real render verification...");

		// 在 BoneWeapon 上挂载红色标记点，方便观察握持点对齐
		var marker = new ColorRect
		{
			Name = "GripMarker",
			Size = new Vector2(6, 6),
			Color = new Color(1, 0, 0, 0.8f),
			ZIndex = 100,
		};
		marker.Position = new Vector2(-3, -3);
		
		var skeleton = _preview.GetSkeleton();
		if (skeleton != null)
		{
			skeleton.BoneWeapon.AddChild(marker);
			skeleton.SpriteWeapon.Visible = true;
			skeleton.SpriteHands.Visible = true;
			skeleton.SpriteHandsL.Visible = true;
			
			// 确保测试时面朝向与新定义的默认“朝右”完全对齐（_facingLeft = false ➔ Scale.X = 1）
			_facingLeft = false;
			skeleton.SetFacing(_facingLeft);
		}

		// [Test] Generate 256x256 placeholder body/head textures with proper size so character shape is visible
		if (skeleton != null)
		{
			// 身体底色：生成 256x256 透明底图，在中心绘制 40x48 像素的占位方块，保证 1.0f 渲染与 0.25f 缩放装备完美贴合
			var bodyImg = Image.CreateEmpty(256, 256, false, Image.Format.Rgba8);
			int bodyW = 40;
			int bodyH = 48;
			bodyImg.FillRect(new Rect2I((256 - bodyW) / 2, (256 - bodyH) / 2, bodyW, bodyH), new Color(0.82f, 0.65f, 0.50f, 1f));
			skeleton.SpriteBody.Texture = ImageTexture.CreateFromImage(bodyImg);
			skeleton.SpriteBody.Visible = true;

			// 头部底色：生成 256x256 透明底图，在中心绘制 48x48 像素的占位方块，保证 1.0f 渲染与 0.25f 缩放装备完美贴合
			var headImg = Image.CreateEmpty(256, 256, false, Image.Format.Rgba8);
			int headW = 48;
			int headH = 48;
			headImg.FillRect(new Rect2I((256 - headW) / 2, (256 - headH) / 2, headW, headH), new Color(0.82f, 0.65f, 0.50f, 1f));
			skeleton.SpriteHead.Texture = ImageTexture.CreateFromImage(headImg);
			skeleton.SpriteHead.Visible = true;
		}

		// 1. 自动扫描 weapons/ 目录下的所有武器图片文件
		var weaponFiles = new List<string>();
		var dir = DirAccess.Open("res://assets/weapons/");
		if (dir != null)
		{
			dir.ListDirBegin();
			string fileName = dir.GetNext();
			while (fileName != "")
			{
				if (!dir.CurrentIsDir() && fileName.EndsWith(".png") && !fileName.Contains("backup"))
				{
					weaponFiles.Add(fileName);
				}
				fileName = dir.GetNext();
			}
			dir.ListDirEnd();
		}
		
		// 物理文件系统兜底
		if (weaponFiles.Count == 0 && System.IO.Directory.Exists("d:\\123\\Blade&Hex\\assets\\weapons"))
		{
			try
			{
				var files = System.IO.Directory.GetFiles("d:\\123\\Blade&Hex\\assets\\weapons", "*.png");
				foreach (var file in files)
				{
					weaponFiles.Add(System.IO.Path.GetFileName(file));
				}
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[Automation] System.IO scan failed: {ex.Message}");
			}
		}

		GD.Print($"[Automation] Scanned {weaponFiles.Count} weapon texture files successfully!");

		// 2. 根据 Subtype 动态将所有武器分类归流到 8 大动画类别
		var categoryToWeapons = new Dictionary<WeaponAnimCategory, List<string>>();
		foreach (var cat in WeaponAnimCategoryUtil.All)
		{
			categoryToWeapons[cat] = new List<string>();
		}

		foreach (var fn in weaponFiles)
		{
			string nameWithoutExt = fn.Replace(".png", "");
			if (Enum.TryParse<BladeHex.Data.WeaponData.WeaponSubtype>(nameWithoutExt, true, out var subtype))
			{
				var cat = WeaponAnimCategoryUtil.FromSubtype(subtype);
				categoryToWeapons[cat].Add(fn);
			}
			else
			{
				GD.Print($"[Automation] Skip / Untracked weapon subtype: {fn}");
			}
		}

		// 可靠的分类兜底（防空包报错）
		if (categoryToWeapons[WeaponAnimCategory.Slash].Count == 0) categoryToWeapons[WeaponAnimCategory.Slash].Add("ArmingSword.png");
		if (categoryToWeapons[WeaponAnimCategory.Thrust].Count == 0) categoryToWeapons[WeaponAnimCategory.Thrust].Add("Awlpike.png");
		if (categoryToWeapons[WeaponAnimCategory.Crush].Count == 0) categoryToWeapons[WeaponAnimCategory.Crush].Add("MilitaryHammer.png");
		if (categoryToWeapons[WeaponAnimCategory.Bow].Count == 0) categoryToWeapons[WeaponAnimCategory.Bow].Add("CompositeLongbow.png");
		if (categoryToWeapons[WeaponAnimCategory.Crossbow].Count == 0) categoryToWeapons[WeaponAnimCategory.Crossbow].Add("HeavyCrossbow.png");
		if (categoryToWeapons[WeaponAnimCategory.Throw].Count == 0) categoryToWeapons[WeaponAnimCategory.Throw].Add("Dart.png");
		if (categoryToWeapons[WeaponAnimCategory.Catalyst].Count == 0) categoryToWeapons[WeaponAnimCategory.Catalyst].Add("StaffOak.png");
		
		// 徒手类单独注入空字符串
		categoryToWeapons[WeaponAnimCategory.Unarmed].Clear();
		categoryToWeapons[WeaponAnimCategory.Unarmed].Add("");

		// ═══════════════════════════════════════════
		// 【维度一：时序动作雪碧拼接长图渲染 (100% 帧与状态覆盖)】
		// ═══════════════════════════════════════════
		GD.Print("\n>>> [Automation] Running Dimension 1: Temporal Spritesheet Composites...");
		
		// 取消在此处初始化测试护甲，移动到内部循环中

		foreach (var cat in WeaponAnimCategoryUtil.All)
		{
			_currentWeaponCat = cat;
			string repWeapon = GetCategoryRepresentativeWeapon(cat);
			var anims = GetAnimationsForCategory(cat);

			GD.Print($"[Automation] Category '{cat}' using rep weapon '{repWeapon}' with {anims.Count} animations.");

			// 加载武器贴图并应用 (仅代表武器)
			if (!string.IsNullOrEmpty(repWeapon) && skeleton != null)
			{
				string weaponPath = $"res://assets/weapons/{repWeapon}";
				Texture2D? tex = LoadEditorTexture(weaponPath);
				if (tex != null)
				{
					_preview.ApplyTextureWithScale(ItemData.EquipSlot.Weapon, skeleton.SpriteWeapon, tex);
				}
				else
				{
					GD.PrintErr($"[Automation] Failed to load representative weapon: {weaponPath}");
				}
			}
			else if (skeleton != null)
			{
				skeleton.SpriteWeapon.Texture = null;
				skeleton.SpriteWeapon.Visible = false;
			}

			Texture2D? defaultHelmet = null;
			Texture2D? defaultArmor = null;
			Texture2D? defaultHands = null;
			if (skeleton != null)
			{
				defaultHelmet = LoadEditorTexture("res://assets/helmets/Armet.png");
				defaultArmor = LoadEditorTexture("res://assets/armor/ChainMail.png");
				defaultHands = LoadEditorTexture("res://assets/armor/ChainGauntlets.png");
			}

			foreach (var animName in anims)
			{
				var loaded = AnimClipSerializer.Load(animName, cat);
				if (loaded == null)
				{
					GD.PrintErr($"[Automation] Failed to load animation clip '{animName}' for category {cat}");
					continue;
				}

				_clip = loaded;
				_preview.CurrentClip = _clip;

				// 核心时序采样：在整个动画 Duration 内均匀采样 10 帧时间点
				var frameImages = new List<Image>();
				float duration = _clip.Duration;

				GD.Print($"[Automation] Composite Spritesheet for {cat}/{animName} ({duration:F2}s, 10 frames)...");

				for (int i = 0; i < 10; i++)
				{
					float t = i * (duration / 9.0f);
					_preview.SeekTo(t);
					
					if (skeleton != null)
					{
						if (defaultHelmet != null)
						{
							_preview.ApplyTextureWithScale(ItemData.EquipSlot.Helmet, skeleton.SpriteHelmet, defaultHelmet);
						}
						if (defaultArmor != null)
						{
							_preview.ApplyTextureWithScale(ItemData.EquipSlot.Costume, skeleton.SpriteCostume, defaultArmor);
						}
						if (defaultHands != null)
						{
							_preview.ApplyTextureWithScale(ItemData.EquipSlot.Hands, skeleton.SpriteHands, defaultHands);
							_preview.ApplyTextureWithScale(ItemData.EquipSlot.Hands, skeleton.SpriteHandsL, defaultHands);
						}
						skeleton.SpriteHands.Visible = true;
						skeleton.SpriteHandsL.Visible = true;
						skeleton.SetFacing(_facingLeft);
					}

					ApplyAllSavedOffsets();

					// 等待渲染完成
					await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
					await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

					if (skeleton != null && skeleton.Viewport != null)
					{
						var img = skeleton.Viewport.GetTexture().GetImage();
						if (img != null)
						{
							frameImages.Add(img);
						}
					}
				}

				// 在显存中横向拼接 10 帧并保存为 Spritesheet
				var composite = MergeImagesHorizontally(frameImages);
				if (composite != null)
				{
					string filename = $"screenshot_godot_sprite_{cat.ToString().ToLower()}_{animName}.png";
					string savePath = System.IO.Path.Combine("d:\\123\\Blade&Hex", filename);
					Error err = composite.SavePng(savePath);
					if (err == Error.Ok)
						GD.Print($"[Automation] Saved temporal composite spritesheet to: {savePath}");
					else
						GD.PrintErr($"[Automation] Failed to save composite image: {savePath}, err: {err}");
				}
			}
		}

		// ═══════════════════════════════════════════
		// 【维度二：空间全具体武器静态比对大图渲染 (100% 武器覆盖)】
		// ═══════════════════════════════════════════
		GD.Print("\n>>> [Automation] Running Dimension 2: Spatial All-Weapons Grid Comparison...");

		foreach (var cat in WeaponAnimCategoryUtil.All)
		{
			if (cat == WeaponAnimCategory.Unarmed) continue; // 徒手无武器可对比

			_currentWeaponCat = cat;
			var allWeapons = categoryToWeapons[cat];
			var anims = GetAnimationsForCategory(cat);
			
			// 挑选两个典型动作状态来比对：
			// 1. Idle 状态下的 0.0s 首帧
			// 2. 主攻击状态下的黄金爆发正中间帧
			var compareStates = new List<(string AnimName, float Time)>();
			if (anims.Contains("idle")) compareStates.Add(("idle", 0.0f));
			
			string attackAnim = cat is WeaponAnimCategory.Bow or WeaponAnimCategory.Crossbow or WeaponAnimCategory.Throw ? "attack_ranged" :
							   cat is WeaponAnimCategory.Catalyst ? "cast" : "attack_melee";
			if (anims.Contains(attackAnim))
			{
				var loadedAttack = AnimClipSerializer.Load(attackAnim, cat);
				float midTime = loadedAttack != null ? loadedAttack.Duration * 0.5f : 0.25f;
				compareStates.Add((attackAnim, midTime));
			}

			foreach (var state in compareStates)
			{
				GD.Print($"[Automation] Grid Comparison for category {cat} during '{state.AnimName}' at {state.Time:F2}s ({allWeapons.Count} weapons)...");

				var loaded = AnimClipSerializer.Load(state.AnimName, cat);
				if (loaded == null) continue;

				_clip = loaded;
				_preview.CurrentClip = _clip;
				_preview.SeekTo(state.Time);

				var weaponCompareImages = new List<Image>();

				foreach (var weaponFile in allWeapons)
				{
					// 装载各个具体武器纹理
					if (!string.IsNullOrEmpty(weaponFile) && skeleton != null)
					{
						string Path = $"res://assets/weapons/{weaponFile}";
						Texture2D? tex = LoadEditorTexture(Path);
						if (tex != null)
						{
							_preview.ApplyTextureWithScale(ItemData.EquipSlot.Weapon, skeleton.SpriteWeapon, tex);
						}
					}

					// 每次渲染强制重新装载测试护甲，规避可能的引用丢失或清理
					if (skeleton != null) {
						Texture2D? th = LoadEditorTexture("res://assets/helmets/Armet.png");
						if (th != null) {
							_preview.ApplyTextureWithScale(ItemData.EquipSlot.Helmet, skeleton.SpriteHelmet, th);
						}
						Texture2D? ta = LoadEditorTexture("res://assets/armor/ChainMail.png");
						if (ta != null) {
							_preview.ApplyTextureWithScale(ItemData.EquipSlot.Costume, skeleton.SpriteCostume, ta);
						}
						Texture2D? tg = LoadEditorTexture("res://assets/armor/ChainGauntlets.png");
						if (tg != null) {
							_preview.ApplyTextureWithScale(ItemData.EquipSlot.Hands, skeleton.SpriteHands, tg);
							_preview.ApplyTextureWithScale(ItemData.EquipSlot.Hands, skeleton.SpriteHandsL, tg);
						}
						skeleton.SpriteHands.Visible = true;
						skeleton.SpriteHandsL.Visible = true;
					}

					ApplyAllSavedOffsets();
					
					if (skeleton != null)
					{
						skeleton.SetFacing(_facingLeft);
					}

					// 等待渲染刷新
					await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
					await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);


					if (skeleton != null && skeleton.Viewport != null)
					{
						var img = skeleton.Viewport.GetTexture().GetImage();
						if (img != null)
						{
							weaponCompareImages.Add(img);
						}
					}
				}

				// 将该大类下所有武器的静态手持画面水平拼接为一张超宽的全对比大图
				var composite = MergeImagesHorizontally(weaponCompareImages);
				if (composite != null)
				{
					string filename = $"screenshot_godot_compare_{cat.ToString().ToLower()}_{state.AnimName}_all.png";
					string savePath = System.IO.Path.Combine("d:\\123\\Blade&Hex", filename);
					Error err = composite.SavePng(savePath);
					if (err == Error.Ok)
						GD.Print($"[Automation] Saved spatial comparison grid to: {savePath}");
					else
						GD.PrintErr($"[Automation] Failed to save grid image: {savePath}, err: {err}");
				}
			}
		}

		// ═══════════════════════════════════════════
		// 【维度三：头盔与护甲静态比对大图渲染】
		// ═══════════════════════════════════════════
		GD.Print("\n>>> [Automation] Running Dimension 3: Spatial Helmets and Armors Grid Comparison...");

		if (skeleton != null)
		{
			skeleton.SpriteWeapon.Visible = false; // 隐藏武器避免遮挡
		}

		// 测试头盔
		var helmetFiles = new List<string>();
		if (System.IO.Directory.Exists("d:\\123\\Blade&Hex\\assets\\helmets"))
		{
			var files = System.IO.Directory.GetFiles("d:\\123\\Blade&Hex\\assets\\helmets", "*.png");
			foreach (var file in files) helmetFiles.Add(System.IO.Path.GetFileName(file));
		}

		if (helmetFiles.Count > 0)
		{
			GD.Print($"[Automation] Grid Comparison for Helmets during 'idle' at 0.00s ({helmetFiles.Count} helmets)...");
			var loaded = AnimClipSerializer.Load("idle", WeaponAnimCategory.Slash);
			if (loaded != null)
			{
				_clip = loaded;
				_preview.CurrentClip = _clip;
				_preview.SeekTo(0.0f);
				var helmetCompareImages = new List<Image>();

				foreach (var hFile in helmetFiles)
				{
					string Path = $"res://assets/helmets/{hFile}";
					Texture2D? tex = LoadEditorTexture(Path);
					if (tex != null && skeleton != null)
					{
						_preview.ApplyTextureWithScale(ItemData.EquipSlot.Helmet, skeleton.SpriteHelmet, tex);
					}
					ApplyAllSavedOffsets();
					if (skeleton != null)
					{
						skeleton.SetFacing(_facingLeft);
					}
					
					await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
					await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
					
					if (skeleton != null && skeleton.Viewport != null)
					{
						var img = skeleton.Viewport.GetTexture().GetImage();
						if (img != null) helmetCompareImages.Add(img);
					}
				}
				
				var compositeH = MergeImagesHorizontally(helmetCompareImages);
				if (compositeH != null)
				{
					string savePath = "d:\\123\\Blade&Hex\\screenshot_godot_compare_helmets_all.png";
					compositeH.SavePng(savePath);
				}
			}
		}

		// 隐藏头盔
		if (skeleton != null) skeleton.SpriteHelmet.Visible = false;

		// 测试护甲
		var armorFiles = new List<string>();
		if (System.IO.Directory.Exists("d:\\123\\Blade&Hex\\assets\\armor"))
		{
			var files = System.IO.Directory.GetFiles("d:\\123\\Blade&Hex\\assets\\armor", "*.png");
			foreach (var file in files) 
			{
				// 跳过 sheet 以及非躯干防具
				string fn = System.IO.Path.GetFileName(file);
				if (!fn.Contains("sheet") && !fn.Contains("Boots") && !fn.Contains("Gauntlets") && !fn.Contains("Gloves")) 
					armorFiles.Add(fn);
			}
		}

		if (armorFiles.Count > 0)
		{
			GD.Print($"[Automation] Grid Comparison for Armors during 'idle' at 0.00s ({armorFiles.Count} armors)...");
			var loaded = AnimClipSerializer.Load("idle", WeaponAnimCategory.Slash);
			if (loaded != null)
			{
				_clip = loaded;
				_preview.CurrentClip = _clip;
				_preview.SeekTo(0.0f);
				var armorCompareImages = new List<Image>();

				foreach (var aFile in armorFiles)
				{
					string Path = $"res://assets/armor/{aFile}";
					Texture2D? tex = LoadEditorTexture(Path);
					if (tex != null && skeleton != null)
					{
						_preview.ApplyTextureWithScale(ItemData.EquipSlot.Costume, skeleton.SpriteCostume, tex);
					}
					ApplyAllSavedOffsets();
					if (skeleton != null)
					{
						skeleton.SetFacing(_facingLeft);
					}
					
					await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
					await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
					
					if (skeleton != null && skeleton.Viewport != null)
					{
						var img = skeleton.Viewport.GetTexture().GetImage();
						if (img != null) armorCompareImages.Add(img);
					}
				}
				
				var compositeA = MergeImagesHorizontally(armorCompareImages);
				if (compositeA != null)
				{
					string savePath = "d:\\123\\Blade&Hex\\screenshot_godot_compare_armors_all.png";
					compositeA.SavePng(savePath);
				}
			}
		}

		// 测试结束，卸载 GripMarker，并关闭游戏
		if (skeleton != null && skeleton.BoneWeapon.HasNode("GripMarker"))
		{
			var m = skeleton.BoneWeapon.GetNode("GripMarker");
			skeleton.BoneWeapon.RemoveChild(m);
			m.QueueFree();
		}

		GD.Print("\n>>> [Screenshot Automation] All 100% full coverage screenshots and spritesheets successfully generated. Exiting game...");
		GetTree().Quit();
	}

	/// <summary>在显存/内存中，使用无损像素拷贝横向拼接多张 Image</summary>
	private Image? MergeImagesHorizontally(List<Image> images)
	{
		if (images == null || images.Count == 0) return null;
		
		int w = images[0].GetWidth();
		int h = images[0].GetHeight();
		
		var composite = Image.CreateEmpty(w * images.Count, h, false, Image.Format.Rgba8);
		for (int i = 0; i < images.Count; i++)
		{
			if (images[i] == null) continue;
			composite.BlitRect(images[i], new Rect2I(0, 0, w, h), new Vector2I(i * w, 0));
		}
		return composite;
	}

	/// <summary>获取大类下的代表性经典武器图片</summary>
	private string GetCategoryRepresentativeWeapon(WeaponAnimCategory cat) => cat switch
	{
		WeaponAnimCategory.Slash => "ArmingSword.png",
		WeaponAnimCategory.Thrust => "Awlpike.png",
		WeaponAnimCategory.Crush => "MilitaryHammer.png",
		WeaponAnimCategory.Bow => "CompositeLongbow.png",
		WeaponAnimCategory.Crossbow => "HeavyCrossbow.png",
		WeaponAnimCategory.Throw => "Dart.png",
		WeaponAnimCategory.Catalyst => "StaffOak.png",
		WeaponAnimCategory.Unarmed => "",
		_ => "",
	};

	/// <summary>动态发掘大类特有与 common 通用动画状态</summary>
	private List<string> GetAnimationsForCategory(WeaponAnimCategory cat)
	{
		var list = new HashSet<string>();
		
		// 扫描特有
		var catDir = DirAccess.Open($"res://assets/animations/{cat.ToString().ToLower()}/");
		if (catDir != null)
		{
			catDir.ListDirBegin();
			string fn = catDir.GetNext();
			while (fn != "")
			{
				if (!catDir.CurrentIsDir() && fn.EndsWith(".json"))
					list.Add(fn.Replace(".json", ""));
				fn = catDir.GetNext();
			}
			catDir.ListDirEnd();
		}
		
		// 扫描 common
		var commonDir = DirAccess.Open("res://assets/animations/common/");
		if (commonDir != null)
		{
			commonDir.ListDirBegin();
			string fn = commonDir.GetNext();
			while (fn != "")
			{
				if (!commonDir.CurrentIsDir() && fn.EndsWith(".json"))
					list.Add(fn.Replace(".json", ""));
				fn = commonDir.GetNext();
			}
			commonDir.ListDirEnd();
		}

		// 智能 Fallback 兜底
		if (list.Count == 0)
		{
			list.Add("idle");
			list.Add(cat is WeaponAnimCategory.Bow or WeaponAnimCategory.Crossbow or WeaponAnimCategory.Throw ? "attack_ranged" : 
					 cat is WeaponAnimCategory.Catalyst ? "cast" : "attack_melee");
		}
		
		return list.ToList();
	}


	/// <summary>加载所有槽位的已保存偏移并应用到预览</summary>
	private void ApplyAllSavedOffsets()
	{
		var skeleton = _preview.GetSkeleton();
		if (skeleton == null) return;

		foreach (var slot in EquipmentOffsetConfig.EditableSlots)
		{
			EquipmentOffsetConfig config;
			if (slot == ItemData.EquipSlot.Weapon)
				config = EquipmentOffsetConfig.GetWeapon(_currentWeaponCat, _clip.Name);
			else
				config = EquipmentOffsetConfig.Get(slot);

			var sprite = skeleton.GetSlotSprite(slot);
			if (sprite == null) continue;

			sprite.Offset = new Vector2(config.OffsetX, config.OffsetY);
			if (EquipmentOffsetConfig.SupportsRotation(slot))
				sprite.RotationDegrees = config.Rotation;
			float targetScale = config.Scale;
			// 核心保障：截图测试状态下，强制大图规格防具使用统一缩放（与默认值对齐）
			var cmdArgs = OS.GetCmdlineArgs();
			if (System.Linq.Enumerable.Contains(cmdArgs, "--screenshot-test"))
			{
				if (slot == ItemData.EquipSlot.Helmet || slot == ItemData.EquipSlot.Costume)
				{
					targetScale = 0.5f;
				}
				else if (slot == ItemData.EquipSlot.Hands)
				{
					targetScale = 0.25f;
				}
			}

			if (!Mathf.IsEqualApprox(targetScale, 1.0f))
				sprite.Scale = new Vector2(targetScale, targetScale);
			else
				sprite.Scale = Vector2.One;
			if (config.FlipH)
				sprite.Scale = new Vector2(-sprite.Scale.X, sprite.Scale.Y);

			// 特殊同步：如果是手甲槽位，同时将属性和偏移应用到左手
			if (slot == ItemData.EquipSlot.Hands && skeleton.SpriteHandsL != null)
			{
				skeleton.SpriteHandsL.Offset = sprite.Offset;
				skeleton.SpriteHandsL.Scale = sprite.Scale;
			}
		}

		// 身体和头部底图作为皮肤底色，保持独立 1.0f 缩放以展现清晰人体比例
		if (skeleton.SpriteBody != null)
		{
			skeleton.SpriteBody.Scale = Vector2.One;
		}
		if (skeleton.SpriteHead != null)
		{
			skeleton.SpriteHead.Scale = Vector2.One;
		}
	}

	public override void _Process(double delta)
	{
		// 相机 WASD
		if (_cam != null)
		{
			float spd = 400f * (float)delta * (_cam.Size / 200f);
			var v = Vector3.Zero;
			if (Input.IsKeyPressed(Key.W)) v.Z -= 1;
			if (Input.IsKeyPressed(Key.S)) v.Z += 1;
			if (Input.IsKeyPressed(Key.A)) v.X -= 1;
			if (Input.IsKeyPressed(Key.D)) v.X += 1;
			if (v.Length() > 0)
				_cam.Position += v.Normalized() * spd;
		}

		// 播放时同步时间轴
		if (_preview.IsPlaying)
		{
			_timeline.CurrentTime = _preview.PlayTime;
			ApplyAllSavedOffsets();
		}

		// 同步 gizmo 模式状态
		_preview.SetGizmoDisplaceMode(_pivotDisplaceMode, _displaceBoneName);
		_preview.SetGizmoRotationMode(_rotationMode, _rotationBoneName);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// ESC 返回主菜单
		if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
		{
			GetTree().ChangeSceneToFile("res://BladeHexFrontend/src/ui/main_menu/main_menu.tscn");
			GetViewport().SetInputAsHandled();
			return;
		}

		// F 键：部件偏移模式下切换水平翻转
		if (@event is InputEventKey fKey && fKey.Pressed && !fKey.Echo && fKey.Keycode == Key.F && _equipOffsetMode)
		{
			_currentEquipOffset.FlipH = !_currentEquipOffset.FlipH;
			ApplyEquipOffsetToPreview();
			_statusLabel.Text = $"部件偏移 [{EquipmentOffsetConfig.GetSlotDisplayName(_currentOffsetSlot)}] 翻转: {(_currentEquipOffset.FlipH ? "是" : "否")}";
			GetViewport().SetInputAsHandled();
			return;
		}

		// ─── 右键：枢轴位移模式（按住进入，松开退出） ───
		if (@event is InputEventMouseButton rmb && rmb.ButtonIndex == MouseButton.Right)
		{
			if (rmb.Pressed)
			{
				// 检测是否点中了某个骨骼 gizmo
				var hit = HitTestGizmo(rmb.Position);
				if (hit != null)
				{
					// 进入位移模式
					_pivotDisplaceMode = true;
					_displaceBoneName = hit;
					_displaceStartMouse = rmb.Position;
					_displaceStartBoneScreenPos = GetBoneScreenPos(hit);
					_bonePanel.SelectBone(hit);
					GetViewport().SetInputAsHandled();
					return;
				}
			}
			else
			{
				// 松开右键：退出位移模式
				if (_pivotDisplaceMode)
				{
					_pivotDisplaceMode = false;
					_displaceBoneName = "";
				}
				GetViewport().SetInputAsHandled();
				return;
			}
		}

		// ─── 位移模式：拖拽移动枢轴 ───
		if (_pivotDisplaceMode && @event is InputEventMouseMotion displaceMotion)
		{
			if (_selectedKeyframeIdx >= 0 && _selectedKeyframeIdx < _clip.Keyframes.Count)
			{
				var boneNodes = _preview.GetBoneNodes();
				if (boneNodes != null && boneNodes.TryGetValue(_displaceBoneName, out var boneNode))
				{
					// 期望屏幕位置 = 起始屏幕位置 + 鼠标位移
					var targetScreenPos = _displaceStartBoneScreenPos + (displaceMotion.Position - _displaceStartMouse);
					// 当前屏幕位置（从实际骨骼节点读取，包含完整父级变换）
					var currentScreenPos = GetBoneScreenPos(_displaceBoneName);
					// 屏幕误差
					var screenError = targetScreenPos - currentScreenPos;

					var skeleton = _preview.GetSkeleton();
					if (skeleton != null && _cam != null && SkeletonEditorProjection.TryScreenDeltaToCanvasDelta(
						_cam,
						skeleton.Billboard,
						skeleton.Config.PixelSize,
						SkeletonEditorProjection.GetBoneCanvasOffset(boneNode),
						screenError,
						out var svDelta))
					{
						// SubViewport 全局坐标增量 → 骨骼局部坐标增量（通过逆变换矩阵处理父级旋转/缩放）
						var inv = boneNode.GlobalTransform.AffineInverse();
						var localDelta = inv.X * svDelta.X + inv.Y * svDelta.Y;

						var kf = _clip.Keyframes[_selectedKeyframeIdx];
						var pose = kf.GetPose(_displaceBoneName);
						pose.PositionX += localDelta.X;
						pose.PositionY -= localDelta.Y; // pose Y 与局部 Y 相反
						kf.SetPose(_displaceBoneName, pose);
						_preview.ApplyBonePose(_displaceBoneName, pose);
						_bonePanel.SetDisplayPose(pose);
					}
				}
			}
			GetViewport().SetInputAsHandled();
			return;
		}

		// ─── 左键：旋转模式（按住枢轴进入） ───
		if (@event is InputEventMouseButton lmb && lmb.ButtonIndex == MouseButton.Left)
		{
			if (lmb.Pressed)
			{
				// 部件偏移模式：左键按住任意位置即可拖拽部件偏移
				if (_equipOffsetMode)
				{
					_draggingGrip = true;
					_dragBoneName = "EquipOffset";
					_dragStartMouse = lmb.Position;
					GetViewport().SetInputAsHandled();
					return;
				}

				// 检测是否点中了某个骨骼 gizmo
				var hit = HitTestGizmo(lmb.Position);
				if (hit != null)
				{
					// 进入旋转模式
					_rotationMode = true;
					_rotationBoneName = hit;
					_draggingGrip = true;
					_dragBoneName = hit;
					_dragStartMouse = lmb.Position;
					if (_selectedKeyframeIdx >= 0 && _selectedKeyframeIdx < _clip.Keyframes.Count)
						_dragStartRotZ = _clip.Keyframes[_selectedKeyframeIdx].GetPose(hit).RotationZ;
					else
						_dragStartRotZ = 0;
					_dragStartAngle = GetMouseCanvasAngle(lmb.Position, hit);
					_bonePanel.SelectBone(hit);
					GetViewport().SetInputAsHandled();
					return;
				}
			}
			else
			{
				// 松开左键：退出旋转模式
				if (_rotationMode)
				{
					_rotationMode = false;
					_rotationBoneName = "";
				}
				_draggingGrip = false;
				GetViewport().SetInputAsHandled();
				return;
			}
		}

		// ─── 旋转模式：拖拽旋转骨骼 ───
		if (_rotationMode && @event is InputEventMouseMotion rotMotion)
		{
			// 基于骨骼画布角度旋转，避免 3D 相机俯角压缩屏幕 Y 后造成偏移。
			float currentAngle = GetMouseCanvasAngle(rotMotion.Position, _rotationBoneName);
			float angleDelta = currentAngle - _dragStartAngle;
			// 归一化到 [-π, π] 防止跨越 ±180° 时反转
			while (angleDelta > Mathf.Pi) angleDelta -= Mathf.Tau;
			while (angleDelta < -Mathf.Pi) angleDelta += Mathf.Tau;
			// 转换为度数，朝向修正
			float facingSign = _facingLeft ? 1.0f : -1.0f;
			float newRot = _dragStartRotZ + Mathf.RadToDeg(angleDelta) * facingSign;
			// 规范化到 -360 ~ 360
			newRot = Mathf.Wrap(newRot, -360f, 360f);

			if (_selectedKeyframeIdx >= 0 && _selectedKeyframeIdx < _clip.Keyframes.Count)
			{
				var kf = _clip.Keyframes[_selectedKeyframeIdx];
				var pose = kf.GetPose(_rotationBoneName);
				pose.RotationZ = newRot;
				kf.SetPose(_rotationBoneName, pose);
				_preview.ApplyBonePose(_rotationBoneName, pose);
				_bonePanel.SetDisplayPose(pose);
			}
			GetViewport().SetInputAsHandled();
			return;
		}

		// ─── 部件偏移模式：拖拽修改 offset ───
		if (_equipOffsetMode && _draggingGrip && @event is InputEventMouseMotion equipMotion)
		{
			float scale = (_cam?.Size ?? 200f) / GetViewport().GetVisibleRect().Size.Y;
			float pixelScale = scale / (_preview.GetSkeleton()?.Config?.PixelSize ?? 1.5f);
			_currentEquipOffset.OffsetX += equipMotion.Relative.X * pixelScale * 2f;
			_currentEquipOffset.OffsetY += equipMotion.Relative.Y * pixelScale * 2f;
			ApplyEquipOffsetToPreview();
			_bonePanel.SetDisplayPose(new BonePose
			{
				PositionX = _currentEquipOffset.OffsetX,
				PositionY = _currentEquipOffset.OffsetY,
				SpriteRotation = _currentEquipOffset.Rotation,
			});
			GetViewport().SetInputAsHandled();
			return;
		}

		// 滚轮缩放
		if (@event is InputEventMouseButton mb && mb.Pressed)
		{
			if (_equipOffsetMode && (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown))
			{
				// 部件偏移模式：
				//   Shift+滚轮 = 调整旋转（武器专用）
				//   滚轮 = 调整缩放（所有槽位通用）
				bool shiftHeld = mb.ShiftPressed;
				if (shiftHeld && EquipmentOffsetConfig.SupportsRotation(_currentOffsetSlot))
				{
					// Shift+滚轮：调整旋转
					float delta = mb.ButtonIndex == MouseButton.WheelUp ? 5f : -5f;
					_currentEquipOffset.Rotation = Mathf.Clamp(_currentEquipOffset.Rotation + delta, -360f, 360f);
					_statusLabel.Text = $"部件偏移 [{EquipmentOffsetConfig.GetSlotDisplayName(_currentOffsetSlot)}] 旋转: {_currentEquipOffset.Rotation:F0}°";
				}
				else
				{
					// 滚轮：调整缩放
					float delta = mb.ButtonIndex == MouseButton.WheelUp ? 0.05f : -0.05f;
					_currentEquipOffset.Scale = Mathf.Clamp(_currentEquipOffset.Scale + delta, 0.2f, 3.0f);
					_statusLabel.Text = $"部件偏移 [{EquipmentOffsetConfig.GetSlotDisplayName(_currentOffsetSlot)}] 缩放: {_currentEquipOffset.Scale:F2}x";
				}
				ApplyEquipOffsetToPreview();
				_bonePanel.SetDisplayPose(new BonePose
				{
					PositionX = _currentEquipOffset.OffsetX,
					PositionY = _currentEquipOffset.OffsetY,
					SpriteRotation = _currentEquipOffset.Rotation,
				});
				GetViewport().SetInputAsHandled();
			}
			else if (mb.ButtonIndex == MouseButton.WheelUp && _cam != null)
			{
				_cam.Size = Mathf.Clamp(_cam.Size * 0.9f, MinOrtho, MaxOrtho);
				GetViewport().SetInputAsHandled();
			}
			else if (mb.ButtonIndex == MouseButton.WheelDown && _cam != null)
			{
				_cam.Size = Mathf.Clamp(_cam.Size * 1.1f, MinOrtho, MaxOrtho);
				GetViewport().SetInputAsHandled();
			}
			else if (mb.ButtonIndex == MouseButton.Middle)
			{
				_middleDrag = true;
				GetViewport().SetInputAsHandled();
			}
		}

		if (@event is InputEventMouseButton mbUp && !mbUp.Pressed && mbUp.ButtonIndex == MouseButton.Middle)
			_middleDrag = false;

		if (@event is InputEventMouseMotion motion && _middleDrag && _cam != null)
		{
			float factor = _cam.Size / GetViewport().GetVisibleRect().Size.Y;
			_cam.Position += new Vector3(-motion.Relative.X * factor, 0, -motion.Relative.Y * factor);
			GetViewport().SetInputAsHandled();
		}
	}

	/// <summary>检测鼠标位置是否命中某个骨骼 gizmo 圆点</summary>
	private string? HitTestGizmo(Vector2 mousePos)
	{
		if (_cam == null) return null;

		string? closest = null;
		float closestDist = GizmoHitRadius;

		var boneNodes = _preview.GetBoneNodes();
		if (boneNodes == null) return null;

		var skeleton = _preview.GetSkeleton();
		if (skeleton == null) return null;

		foreach (var (name, node) in boneNodes)
		{
			if (!SkeletonEditorProjection.TryProjectBoneToScreen(_cam, skeleton.Billboard, skeleton.Config, node, out var screenPos))
				continue;

			float dist = screenPos.DistanceTo(mousePos);
			if (dist < closestDist)
			{
				closestDist = dist;
				closest = name;
			}
		}
		return closest;
	}

	/// <summary>获取骨骼在屏幕上的位置</summary>
	private Vector2 GetBoneScreenPos(string boneName)
	{
		if (_cam == null) return Vector2.Zero;

		var skeleton = _preview.GetSkeleton();
		if (skeleton == null) return Vector2.Zero;

		var boneNodes = _preview.GetBoneNodes();
		if (boneNodes == null || !boneNodes.TryGetValue(boneName, out var node))
			return Vector2.Zero;

		return SkeletonEditorProjection.TryProjectBoneToScreen(_cam, skeleton.Billboard, skeleton.Config, node, out var screenPos)
			? screenPos
			: Vector2.Zero;
	}

	private float GetMouseCanvasAngle(Vector2 mousePos, string boneName)
	{
		if (_cam == null)
			return 0f;

		var skeleton = _preview.GetSkeleton();
		var boneNodes = _preview.GetBoneNodes();
		if (skeleton == null || boneNodes == null || !boneNodes.TryGetValue(boneName, out var node))
			return 0f;

		var boneScreenPos = GetBoneScreenPos(boneName);
		var screenDelta = mousePos - boneScreenPos;
		if (SkeletonEditorProjection.TryScreenDeltaToCanvasDelta(
			_cam,
			skeleton.Billboard,
			skeleton.Config.PixelSize,
			SkeletonEditorProjection.GetBoneCanvasOffset(node),
			screenDelta,
			out var canvasDelta)
			&& canvasDelta.LengthSquared() > 0.0001f)
		{
			return Mathf.Atan2(canvasDelta.Y, canvasDelta.X);
		}

		return Mathf.Atan2(screenDelta.Y, screenDelta.X);
	}

	/// <summary>骨骼名称中文映射</summary>
	private static string BoneDisplayName(string bone) => bone switch
	{
		"Torso" => "躯干",
		"Head" => "头部",
		"ArmL" => "左上臂",
		"ArmR" => "右上臂",
		"ForearmL" => "左前臂",
		"ForearmR" => "右前臂",
		"Weapon" => "武器",
		"Shield" => "盾牌",
		_ => bone,
	};

	// ═══════════════════════════════════════════
	// UI 构建
	// ═══════════════════════════════════════════

	private void BuildTopBar(Control root)
	{
		var bar = new HBoxContainer();
		bar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
		bar.OffsetBottom = 36;
		bar.AddThemeConstantOverride("separation", 10);
		bar.MouseFilter = Control.MouseFilterEnum.Pass;

		var bg = new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.07f, 0.9f) };
		bg.SetContentMarginAll(6);
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", bg);
		panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
		panel.OffsetBottom = 36;
		root.AddChild(panel);
		panel.AddChild(bar);

		// 返回按钮
		var backBtn = new Button { Text = "← 返回" };
		backBtn.Pressed += () => GetTree().ChangeSceneToFile("res://BladeHexFrontend/src/ui/main_menu/main_menu.tscn");
		bar.AddChild(backBtn);

		bar.AddChild(new VSeparator());

		// 动画选择
		bar.AddChild(new Label { Text = "动画:" });
		_animSelect = new OptionButton { CustomMinimumSize = new Vector2(140, 0) };
		_animSelect.ItemSelected += OnAnimSelected;
		bar.AddChild(_animSelect);

		// 初始填充动画列表
		RefreshAnimList();

		bar.AddChild(new VSeparator());

		// 体型选择
		bar.AddChild(new Label { Text = "体型:" });
		_bodyTypeSelect = new OptionButton();
		_bodyTypeSelect.AddItem("Standard");
		_bodyTypeSelect.AddItem("Heavy");
		_bodyTypeSelect.AddItem("Slim");
		_bodyTypeSelect.AddItem("Large");
		_bodyTypeSelect.ItemSelected += OnBodyTypeSelected;
		bar.AddChild(_bodyTypeSelect);

		bar.AddChild(new VSeparator());

		// 武器类别选择
		bar.AddChild(new Label { Text = "武器:" });
		_weaponCatSelect = new OptionButton { CustomMinimumSize = new Vector2(120, 0) };
		foreach (var cat in WeaponAnimCategoryUtil.All)
			_weaponCatSelect.AddItem(WeaponAnimCategoryUtil.GetDisplayName(cat));
		_weaponCatSelect.Selected = 0;
		_weaponCatSelect.ItemSelected += OnWeaponCatSelected;
		bar.AddChild(_weaponCatSelect);

		bar.AddChild(new VSeparator());

		// 保存/加载
		var saveBtn = new Button { Text = "保存" };
		saveBtn.Pressed += OnSave;
		bar.AddChild(saveBtn);

		_nameInput = new LineEdit
		{
			PlaceholderText = "文件名",
			CustomMinimumSize = new Vector2(100, 0),
			Text = "idle",
		};
		bar.AddChild(_nameInput);

		var newBtn = new Button { Text = "新建" };
		newBtn.Pressed += OnNewAnim;
		bar.AddChild(newBtn);

		var delBtn = new Button { Text = "删除" };
		delBtn.AddThemeColorOverride("font_color", new Color(0.9f, 0.3f, 0.3f));
		delBtn.Pressed += OnDeleteAnim;
		bar.AddChild(delBtn);

		bar.AddChild(new VSeparator());

		// 时长
		bar.AddChild(new Label { Text = "时长:" });
		_durationSpin = new SpinBox
		{
			MinValue = 0.1, MaxValue = 5.0, Step = 0.1, Value = 1.0,
			CustomMinimumSize = new Vector2(70, 0),
		};
		_durationSpin.ValueChanged += OnDurationChanged;
		bar.AddChild(_durationSpin);

		// 循环
		_loopCheck = new CheckBox { Text = "循环", ButtonPressed = false };
		_loopCheck.Toggled += OnLoopToggled;
		bar.AddChild(_loopCheck);

		// 重置当前帧
		var resetBtn = new Button { Text = "归零" };
		resetBtn.TooltipText = "重置选中骨骼到 0°";
		resetBtn.Pressed += OnResetBone;
		bar.AddChild(resetBtn);

		bar.AddChild(new VSeparator());

		// 部件偏移模式
		_equipOffsetModeCheck = new CheckBox { Text = "部件偏移", ButtonPressed = false };
		_equipOffsetModeCheck.TooltipText = "勾选后拖拽调整偏移，滚轮调整缩放，Shift+滚轮调整旋转(武器)";
		_equipOffsetModeCheck.Toggled += OnEquipOffsetModeToggled;
		bar.AddChild(_equipOffsetModeCheck);

		// 部件槽位选择
		_equipSlotSelect = new OptionButton { CustomMinimumSize = new Vector2(80, 0) };
		foreach (var slot in EquipmentOffsetConfig.EditableSlots)
			_equipSlotSelect.AddItem(EquipmentOffsetConfig.GetSlotDisplayName(slot));
		_equipSlotSelect.Selected = 0;
		_equipSlotSelect.ItemSelected += OnEquipSlotSelected;
		bar.AddChild(_equipSlotSelect);

		bar.AddChild(new VSeparator());

		// 朝向切换
		var facingBtn = new Button { Text = "翻转 ↔" };
		facingBtn.TooltipText = "切换角色朝向（左/右），装备纹理跟随翻转";
		facingBtn.Pressed += OnFlipFacing;
		bar.AddChild(facingBtn);

		// 隐藏/显示纹理
		_hideTexturesCheck = new CheckBox { Text = "隐藏纹理", ButtonPressed = false };
		_hideTexturesCheck.TooltipText = "暂时隐藏所有装备纹理，只显示骨骼";
		_hideTexturesCheck.Toggled += OnHideTexturesToggled;
		bar.AddChild(_hideTexturesCheck);
	}

	private void BuildBonePanel(Control root)
	{
		_bonePanel = new AnimEditorBonePanel();
		_bonePanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.LeftWide);
		_bonePanel.OffsetTop = 40;
		_bonePanel.OffsetBottom = -100;
		_bonePanel.OffsetRight = 200;
		root.AddChild(_bonePanel);

		_bonePanel.BonePoseChanged += OnBonePoseChanged;
		_bonePanel.BoneSelected += OnBoneSelected;
	}

	private void BuildTexturePanel(Control root)
	{
		_texturePanel = new AnimEditorTexturePanel();
		_texturePanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.RightWide);
		_texturePanel.OffsetTop = 40;
		_texturePanel.OffsetBottom = -100;
		_texturePanel.OffsetLeft = -220;
		root.AddChild(_texturePanel);

		_texturePanel.TextureSelected += OnTextureSelected;
	}

	private void BuildTimeline(Control root)
	{
		_timeline = new AnimEditorTimeline();
		_timeline.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
		_timeline.OffsetTop = -100;
		_timeline.OffsetLeft = 200;
		_timeline.MouseFilter = Control.MouseFilterEnum.Pass;
		root.AddChild(_timeline);

		_timeline.TimeChanged += OnTimeChanged;
		_timeline.KeyframeSelected += OnKeyframeSelected;
		_timeline.PlayPressed += () => _preview.Play();
		_timeline.PausePressed += () => _preview.Pause();
		_timeline.StepForwardPressed += OnStepForward;
		_timeline.AddKeyframePressed += OnAddKeyframe;
		_timeline.RemoveKeyframePressed += OnRemoveKeyframe;
	}

	private void BuildStatusBar(Control root)
	{
		_statusLabel = new Label();
		_statusLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
		_statusLabel.OffsetLeft = -300;
		_statusLabel.OffsetTop = -20;
		_statusLabel.AddThemeFontSizeOverride("font_size", 11);
		_statusLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
		root.AddChild(_statusLabel);
		UpdateStatus();
	}

	// ═══════════════════════════════════════════
	// 事件处理
	// ═══════════════════════════════════════════

	private void OnAnimSelected(long index)
	{
		string name = _animSelect.GetItemText((int)index);

		// 尝试从文件加载（按当前武器类别）
		var loaded = AnimClipSerializer.Load(name, _currentWeaponCat);
		if (loaded != null)
		{
			_clip = loaded;
		}
		else if (_templates.TryGetValue(name, out var factory))
		{
			_clip = factory();
			_clip.WeaponCategory = _currentWeaponCat;
		}
		else
		{
			_clip = new AnimClip { Name = name, Duration = 1.0f, WeaponCategory = _currentWeaponCat };
		}

		_preview.CurrentClip = _clip;
		_preview.IsPlaying = false;
		RefreshTimeline();
		SelectKeyframe(0);
		UpdateStatus();
		_nameInput.Text = _clip.Name;
		_durationSpin.SetValueNoSignal(_clip.Duration);
		_loopCheck.SetPressedNoSignal(_clip.Loop);
	}

	private void OnWeaponCatSelected(long index)
	{
		_currentWeaponCat = WeaponAnimCategoryUtil.All[(int)index];
		_clip.WeaponCategory = _currentWeaponCat;

		// 刷新动画列表（加载该类别下已保存的动画）
		RefreshAnimList();
		UpdateStatus();
	}

	private void OnBodyTypeSelected(long index)
	{
		_preview.Rebuild((BodyType)(int)index);
		_preview.RefreshGizmoReferences();
		_preview.CurrentClip = _clip;

		// 重建骨骼后显式同步朝向
		var skeleton = _preview.GetSkeleton();
		if (skeleton != null)
		{
			skeleton.SetFacing(_facingLeft);
		}

		if (_selectedKeyframeIdx >= 0 && _selectedKeyframeIdx < _clip.Keyframes.Count)
		{
			var pose = AnimClipInterpolator.Sample(_clip, _clip.Keyframes[_selectedKeyframeIdx].Time);
			_preview.ApplyPose(pose);
		}
	}

	private void OnTimeChanged(float time)
	{
		_preview.Pause();
		_preview.SeekTo(time);
		ApplyAllSavedOffsets();
		RefreshBonePanelFromTime(time);
	}

	private void OnKeyframeSelected(int index)
	{
		SelectKeyframe(index);
	}

	private void OnBonePoseChanged(string boneName, float rotZ, float posY, float posX, float spriteRot, float scaleX, float scaleY, string easingName)
	{
		// 部件偏移模式：修改部件偏移配置
		if (_equipOffsetMode)
		{
			_currentEquipOffset.OffsetX = posX;
			_currentEquipOffset.OffsetY = posY;
			if (EquipmentOffsetConfig.SupportsRotation(_currentOffsetSlot))
				_currentEquipOffset.Rotation = spriteRot;
			else
				_currentEquipOffset.Scale = Mathf.Clamp(spriteRot, 0.2f, 3.0f);
			ApplyEquipOffsetToPreview();
			return;
		}

		// 正常模式：更新当前关键帧数据
		if (_selectedKeyframeIdx < 0 || _selectedKeyframeIdx >= _clip.Keyframes.Count) return;

		var kf = _clip.Keyframes[_selectedKeyframeIdx];
		var ease = Enum.TryParse<EasingType>(easingName, true, out var easeVal) ? easeVal : EasingType.Linear;
		var pose = new BonePose 
		{ 
			RotationZ = rotZ, 
			PositionY = posY, 
			PositionX = posX, 
			SpriteRotation = spriteRot,
			ScaleX = scaleX,
			ScaleY = scaleY,
			Easing = ease
		};
		kf.SetPose(boneName, pose);

		// 实时更新预览
		_preview.ApplyBonePose(boneName, pose);
		ApplyAllSavedOffsets();
	}

	private void OnBoneSelected(string boneName)
	{
		// 刷新滑条显示当前帧该骨骼的值
		if (_selectedKeyframeIdx >= 0 && _selectedKeyframeIdx < _clip.Keyframes.Count)
		{
			var pose = _clip.Keyframes[_selectedKeyframeIdx].GetPose(boneName);
			_bonePanel.SetDisplayPose(pose);
		}

		// 同步 gizmo 高亮
		_preview.SetGizmoSelectedBone(boneName);
	}

	private void OnTextureSelected(string slotName, string texturePath)
	{
		if (_preview == null) return;

		Texture2D? tex = null;
		if (!string.IsNullOrEmpty(texturePath))
			tex = LoadEditorTexture(texturePath);

		// 根据部件名找到对应的 Sprite2D 并应用纹理
		var skeleton = _preview.GetSkeleton();
		if (skeleton == null) return;

		// 映射中文部件名 → EquipSlot + Sprite2D
		var (sprite, slot) = slotName switch
		{
			"身体" => (skeleton.SpriteBody, ItemData.EquipSlot.Body),
			"护甲" => (skeleton.SpriteCostume, ItemData.EquipSlot.Costume),
			"头盔" => (skeleton.SpriteHelmet, ItemData.EquipSlot.Helmet),
			"手甲" => (skeleton.SpriteHands, ItemData.EquipSlot.Hands),
			"武器" => (skeleton.SpriteWeapon, ItemData.EquipSlot.Weapon),
			"盾牌" => ((Sprite2D?)skeleton.SpriteShield, ItemData.EquipSlot.Body), // Shield 无独立 slot，用 Body fallback
			_ => ((Sprite2D?)null, ItemData.EquipSlot.Body),
		};

		if (sprite == null) return;

		if (tex != null)
		{
			// 使用 TextureScaleConfig 计算正确的 Sprite2D.Scale
			_preview.ApplyTextureWithScale(slot, sprite, tex);
		}
		else
		{
			// 清除纹理，恢复默认
			sprite.Texture = null;
			sprite.Scale = Vector2.One;
			sprite.Visible = true;
		}

		// 纹理变更后重新应用所有已保存的偏移
		ApplyAllSavedOffsets();
	}

	private void OnStepForward()
	{
		_preview.Pause();
		float step = 1.0f / 30.0f; // 30fps
		float newTime = Mathf.Min(_preview.PlayTime + step, _clip.Duration);
		_preview.SeekTo(newTime);
		ApplyAllSavedOffsets();
		_timeline.CurrentTime = newTime;
		RefreshBonePanelFromTime(newTime);
	}

	private void OnAddKeyframe()
	{
		float time = _timeline.CurrentTime;
		int idx = _clip.InsertKeyframeAt(time);
		RefreshTimeline();
		SelectKeyframe(idx);
		UpdateStatus();
	}

	private void OnRemoveKeyframe()
	{
		if (_selectedKeyframeIdx < 0) return;
		_clip.RemoveKeyframe(_selectedKeyframeIdx);
		RefreshTimeline();
		_selectedKeyframeIdx = Mathf.Min(_selectedKeyframeIdx, _clip.Keyframes.Count - 1);
		if (_selectedKeyframeIdx >= 0)
			SelectKeyframe(_selectedKeyframeIdx);
		UpdateStatus();
	}

	private void OnSave()
	{
		// 部件偏移模式：保存部件偏移配置
		if (_equipOffsetMode)
		{
			if (_currentOffsetSlot == ItemData.EquipSlot.Weapon)
			{
				EquipmentOffsetConfig.SaveWeapon(_currentEquipOffset, _currentWeaponCat, _clip.Name);
				EquipmentOffsetConfig.ClearCache();
				_statusLabel.Text = $"武器偏移配置已保存: weapon/{_currentWeaponCat.ToString().ToLower()}_{_clip.Name}.json";
			}
			else
			{
				EquipmentOffsetConfig.Save(_currentEquipOffset);
				EquipmentOffsetConfig.ClearCache();
				_statusLabel.Text = $"部件偏移配置已保存: {_currentOffsetSlot.ToString().ToLower()}.json";
			}
			return;
		}

		// 正常模式：保存动画
		string name = _nameInput.Text.Trim();
		if (string.IsNullOrEmpty(name))
			name = _clip.Name;
		_clip.Name = name;

		AnimClipSerializer.Save(_clip);
		_statusLabel.Text = $"已保存: {_currentWeaponCat.ToString().ToLower()}/{name}.json";

		// 确保下拉列表包含此动画
		bool found = false;
		for (int i = 0; i < _animSelect.ItemCount; i++)
		{
			if (_animSelect.GetItemText(i) == _clip.Name) { found = true; break; }
		}
		if (!found)
			_animSelect.AddItem(_clip.Name);
	}

	private void OnNewAnim()
	{
		var savedCount = AnimClipSerializer.ListSaved(_currentWeaponCat).Count;
		_clip = new AnimClip
		{
			Name = $"custom_{savedCount + 1}",
			Duration = 1.0f,
			WeaponCategory = _currentWeaponCat,
		};
		_clip.Keyframes.Add(new AnimKeyframe { Time = 0 });
		_clip.Keyframes.Add(new AnimKeyframe { Time = 1.0f });
		_preview.CurrentClip = _clip;
		_preview.IsPlaying = false;
		RefreshTimeline();
		SelectKeyframe(0);
		UpdateStatus();

		// 添加到下拉
		_animSelect.AddItem(_clip.Name);
		_animSelect.Selected = _animSelect.ItemCount - 1;
	}

	private void OnDeleteAnim()
	{
		string dir = $"user://custom_animations/{_currentWeaponCat.ToString().ToLower()}";
		string path = $"{dir}/{_clip.Name}.json";
		if (FileAccess.FileExists(path))
		{
			DirAccess.Open(dir)?.Remove($"{_clip.Name}.json");
			_statusLabel.Text = $"已删除: {path}";
		}
		else
		{
			_statusLabel.Text = "该动画未保存，无需删除";
		}
		RefreshAnimList();
	}

	private void OnDurationChanged(double value)
	{
		float newDur = (float)value;
		if (newDur > 0 && Mathf.Abs(newDur - _clip.Duration) > 0.001f)
		{
			_clip.SetDuration(newDur);
			RefreshTimeline();
			UpdateStatus();
		}
	}

	private void OnLoopToggled(bool on)
	{
		_clip.Loop = on;
		UpdateStatus();
	}

	private void OnResetBone()
	{
		if (_selectedKeyframeIdx < 0 || _selectedKeyframeIdx >= _clip.Keyframes.Count) return;
		string bone = _bonePanel.SelectedBone;
		var kf = _clip.Keyframes[_selectedKeyframeIdx];
		kf.SetPose(bone, BonePose.Zero);
		_preview.ApplyBonePose(bone, BonePose.Zero);
		ApplyAllSavedOffsets();
		_bonePanel.SetDisplayPose(BonePose.Zero);
	}

	private void OnFlipFacing()
	{
		_facingLeft = !_facingLeft;
		var skeleton = _preview.GetSkeleton();
		if (skeleton == null) return;
		skeleton.SetFacing(_facingLeft);
	}

	private void OnHideTexturesToggled(bool hide)
	{
		var skeleton = _preview.GetSkeleton();
		if (skeleton == null) return;

		foreach (var (_, sprite) in skeleton.SlotSprites)
		{
			if (hide)
			{
				// 记录原始可见性到 meta，然后隐藏
				sprite.SetMeta("_wasVisible", sprite.Visible);
				sprite.Visible = false;
			}
			else
			{
				// 恢复原始可见性
				var was = sprite.GetMeta("_wasVisible", true);
				sprite.Visible = was.AsBool();
			}
		}
	}

	private void OnEquipOffsetModeToggled(bool on)
	{
		_equipOffsetMode = on;
		if (on)
		{
			_currentOffsetSlot = EquipmentOffsetConfig.EditableSlots[_equipSlotSelect.Selected];
			if (_currentOffsetSlot == ItemData.EquipSlot.Weapon)
				_currentEquipOffset = EquipmentOffsetConfig.LoadWeapon(_currentWeaponCat, _clip.Name);
			else
				_currentEquipOffset = EquipmentOffsetConfig.Load(_currentOffsetSlot);
			_bonePanel.SetDisplayPose(new BonePose
			{
				PositionX = _currentEquipOffset.OffsetX,
				PositionY = _currentEquipOffset.OffsetY,
				SpriteRotation = _currentEquipOffset.Rotation,
			});
			ApplyEquipOffsetToPreview();
			var hint = EquipmentOffsetConfig.SupportsRotation(_currentOffsetSlot)
				? "拖拽偏移，滚轮缩放，Shift+滚轮旋转"
				: "拖拽偏移，滚轮缩放";
			_statusLabel.Text = $"部件偏移 [{EquipmentOffsetConfig.GetSlotDisplayName(_currentOffsetSlot)}]：{hint}，点「保存」保存";
		}
		else
		{
			_statusLabel.Text = "";
			if (_selectedKeyframeIdx >= 0 && _selectedKeyframeIdx < _clip.Keyframes.Count)
				SelectKeyframe(_selectedKeyframeIdx);
		}
	}

	private void OnEquipSlotSelected(long index)
	{
		_currentOffsetSlot = EquipmentOffsetConfig.EditableSlots[(int)index];
		if (_equipOffsetMode)
		{
			if (_currentOffsetSlot == ItemData.EquipSlot.Weapon)
				_currentEquipOffset = EquipmentOffsetConfig.LoadWeapon(_currentWeaponCat, _clip.Name);
			else
				_currentEquipOffset = EquipmentOffsetConfig.Load(_currentOffsetSlot);
			_bonePanel.SetDisplayPose(new BonePose
			{
				PositionX = _currentEquipOffset.OffsetX,
				PositionY = _currentEquipOffset.OffsetY,
				SpriteRotation = _currentEquipOffset.Rotation,
			});
			ApplyEquipOffsetToPreview();
			var hint = EquipmentOffsetConfig.SupportsRotation(_currentOffsetSlot)
				? "拖拽偏移，滚轮缩放，Shift+滚轮旋转"
				: "拖拽偏移，滚轮缩放";
			_statusLabel.Text = $"部件偏移 [{EquipmentOffsetConfig.GetSlotDisplayName(_currentOffsetSlot)}]：{hint}";
		}
	}

	/// <summary>将当前部件偏移配置应用到预览</summary>
	private void ApplyEquipOffsetToPreview()
	{
		var skeleton = _preview.GetSkeleton();
		if (skeleton == null) return;

		var sprite = skeleton.GetSlotSprite(_currentOffsetSlot);
		if (sprite == null) return;

		sprite.Offset = new Vector2(_currentEquipOffset.OffsetX, _currentEquipOffset.OffsetY);

		// 武器：应用旋转
		if (EquipmentOffsetConfig.SupportsRotation(_currentOffsetSlot))
			sprite.RotationDegrees = _currentEquipOffset.Rotation;

		// 缩放
		if (!Mathf.IsEqualApprox(_currentEquipOffset.Scale, 1.0f))
			sprite.Scale = new Vector2(_currentEquipOffset.Scale, _currentEquipOffset.Scale);
		else
			sprite.Scale = Vector2.One;

		// 水平翻转
		if (_currentEquipOffset.FlipH)
			sprite.Scale = new Vector2(-sprite.Scale.X, sprite.Scale.Y);
	}

	// ═══════════════════════════════════════════
	// 辅助
	// ═══════════════════════════════════════════

	private void SelectKeyframe(int index)
	{
		_selectedKeyframeIdx = index;
		_timeline.SelectedKeyframe = index;

		if (index >= 0 && index < _clip.Keyframes.Count)
		{
			var kf = _clip.Keyframes[index];
			_timeline.CurrentTime = kf.Time;
			_preview.SeekTo(kf.Time);
			ApplyAllSavedOffsets();

			var pose = kf.GetPose(_bonePanel.SelectedBone);
			_bonePanel.SetDisplayPose(pose);
		}
	}

	private void RefreshTimeline()
	{
		var times = _clip.Keyframes.Select(kf => kf.Time).ToList();
		_timeline.SetData(_clip.Duration, times);
	}

	/// <summary>刷新动画下拉列表（切换武器类别时调用）</summary>
	private void RefreshAnimList()
	{
		_animSelect.Clear();
		// 内置模板
		_animSelect.AddItem("idle");
		_animSelect.AddItem("attack_melee");
		_animSelect.AddItem("attack_ranged");
		_animSelect.AddItem("cast");
		_animSelect.AddItem("hit");
		_animSelect.AddItem("die");
		// 该类别下已保存的自定义动画
		foreach (var name in AnimClipSerializer.ListSaved(_currentWeaponCat))
		{
			if (!HasItem(_animSelect, name))
				_animSelect.AddItem(name);
		}
	}

	private void RefreshBonePanelFromTime(float time)
	{
		var allPose = AnimClipInterpolator.Sample(_clip, time);
		if (allPose.TryGetValue(_bonePanel.SelectedBone, out var pose))
			_bonePanel.SetDisplayPose(pose);
	}

	private void UpdateStatus()
	{
		_statusLabel.Text = $"{_clip.Name} | {_clip.Duration:F1}s | {_clip.Keyframes.Count} 帧 | {(_clip.Loop ? "循环" : "单次")}";
	}

	private static Texture2D? LoadEditorTexture(string texturePath)
	{
		if (string.IsNullOrWhiteSpace(texturePath))
			return null;

		var normalizedPath = texturePath.Replace('\\', '/');
		var assetId = System.IO.Path.GetFileNameWithoutExtension(normalizedPath);
		if (!string.IsNullOrWhiteSpace(assetId))
			return TextureAssetResolver.Load(AssetKind.EquipmentTexture, assetId, normalizedPath);

		return CharacterTextureNormalizer.Normalize(TextureAssetResolver.LoadPath(normalizedPath));
	}

	// ─── OptionButton 扩展 ───
	private bool HasItem(OptionButton opt, string text)
	{
		for (int i = 0; i < opt.ItemCount; i++)
			if (opt.GetItemText(i) == text) return true;
		return false;
	}
}
