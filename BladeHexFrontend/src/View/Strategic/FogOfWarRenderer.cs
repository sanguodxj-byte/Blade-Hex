using Godot;

namespace BladeHex.Strategic;

/// <summary>
/// [T-503] FogOfWarRenderer — 战争迷雾 2D 渲染器
/// 使用 ImageTexture + ShaderMaterial 实现高效的像素级迷雾更新。
/// Image 仅在 Initialize 时创建一次，后续使用 Update() 避免 GC 压力。
///
/// ╔══════════════════════════════════════════════════════════════════╗
/// ║ 强制要求：未探索区域必须是纯黑、完全不可见。                      ║
/// ║ visibility=0 的像素必须输出完全不透明黑色 (alpha=1.0)。           ║
/// ║ 禁止让未探索区域透出任何地形颜色。任何修改必须维持此约束。        ║
/// ╚══════════════════════════════════════════════════════════════════╝
/// </summary>
[GlobalClass]
public partial class FogOfWarRenderer : Node2D
{
    private ImageTexture _fogTex = null!;
    private Image _fogImage = null!;
    private Sprite2D _fogSprite = null!;
    private ShaderMaterial _fogMaterial = null!;
    private FogOfWar? _fogData;

    /// <summary>上一帧渲染状态快照，用于脏像素检测</summary>
    private byte[,] _lastUpdatedState = new byte[0, 0];

    /// <summary>地图像素尺寸（用于 shader uniform）</summary>
    private int _mapWidthPx;
    private int _mapHeightPx;

    // ========================================
    // 初始化（仅调用一次）
    // ========================================

    /// <summary>
    /// 初始化迷雾渲染器。
    /// 创建 R8 ImageTexture，挂载 fog_of_war.gdshader 材质。
    /// </summary>
    /// <param name="fogData">核心层 FogOfWar 数据模型</param>
    /// <param name="mapWidthPx">大地图像素宽度（用于 sprite scale）</param>
    /// <param name="mapHeightPx">大地图像素高度（用于 sprite scale）</param>
    public void Initialize(FogOfWar fogData, int mapWidthPx, int mapHeightPx)
    {
        _fogData = fogData;
        _mapWidthPx = mapWidthPx;
        _mapHeightPx = mapHeightPx;
        int gw = fogData.GridW;
        int gh = fogData.GridH;

        // 1. 创建 R8 格式 Image（单通道，R = 可见度阶梯）
        _fogImage = Image.CreateEmpty(gw, gh, false, Image.Format.R8);
        _lastUpdatedState = new byte[gh, gw];

        // 2. 从现有迷雾数据填充初始像素（可能已有种族初始揭示）
        for (int gy = 0; gy < gh; gy++)
        {
            for (int gx = 0; gx < gw; gx++)
            {
                byte state = fogData.ExploredGrid[gy, gx];
                _lastUpdatedState[gy, gx] = state;
                byte r = FogStateToRValue(state);
                _fogImage.SetPixel(gx, gy, new Color(r / 255f, 0, 0, 1));
            }
        }

        // 3. 创建 ImageTexture — 这是整个生命周期中唯一一次 CreateFromImage
        _fogTex = ImageTexture.CreateFromImage(_fogImage);

        // 4. 创建 Sprite2D 并设置纹理
        _fogSprite = new Sprite2D();
        _fogSprite.Texture = _fogTex;
        _fogSprite.Scale = new Vector2(mapWidthPx / (float)gw, mapHeightPx / (float)gh);
        _fogSprite.Position = new Vector2(mapWidthPx / 2f, mapHeightPx / 2f);

        // 5. 加载着色器 + 创建材质
        var shader = GD.Load<Shader>("res://src/assets/shaders/fog_of_war.gdshader");
        if (shader != null)
        {
            _fogMaterial = new ShaderMaterial();
            _fogMaterial.Shader = shader;
            _fogMaterial.SetShaderParameter("fog_disabled", fogData.DisableFog ? 1.0f : 0.0f);
            _fogMaterial.SetShaderParameter("fog_texture_size", new Vector2(gw, gh));
            _fogSprite.Material = _fogMaterial;
        }
        else
        {
            GD.PushWarning("[FogOfWarRenderer] fog_of_war.gdshader 加载失败，迷雾将使用纯色回退");
            // 无 shader 时使用半透明黑色 sprite 作为简易迷雾
            _fogSprite.Modulate = new Color(0, 0, 0, 0.8f);
            _fogMaterial = new ShaderMaterial(); // 空材质，避免后续 null 引用
        }

        AddChild(_fogSprite);
    }

    // ========================================
    // 每帧更新（或玩家移动时调用）
    // ========================================

    /// <summary>
    /// 将 FogOfWar.ExploredGrid 的变化同步到 ImageTexture。
    /// 仅更新脏像素，避免全量重绘。
    /// </summary>
    public void UpdateFog()
    {
        if (_fogData == null) return;

        // 同步 DisableFog 状态到 shader
        _fogMaterial?.SetShaderParameter("fog_disabled", _fogData.DisableFog ? 1.0f : 0.0f);

        if (_fogData.DisableFog) return;

        bool dirty = false;
        int gh = _fogData.GridH;
        int gw = _fogData.GridW;

        for (int gy = 0; gy < gh; gy++)
        {
            for (int gx = 0; gx < gw; gx++)
            {
                byte currentState = _fogData.ExploredGrid[gy, gx];
                if (_lastUpdatedState[gy, gx] == currentState)
                    continue;

                _lastUpdatedState[gy, gx] = currentState;
                dirty = true;

                byte r = FogStateToRValue(currentState);
                _fogImage.SetPixel(gx, gy, new Color(r / 255f, 0, 0, 1));
            }
        }

        if (dirty)
        {
            _fogTex.Update(_fogImage);
        }
    }

    // ========================================
    // 内部工具
    // ========================================

    /// <summary>
    /// 更新玩家位置（保留接口兼容，shader 不再使用圆形羽化）
    /// </summary>
    public void UpdatePlayerPosition(Vector2 playerWorldPos)
    {
        // Shader 不再使用 player_world_pos，迷雾完全由 grid 数据驱动
    }

    /// <summary>
    /// 更新视野半径（保留接口兼容）
    /// </summary>
    public void UpdateVisionRadius(float effectiveRange)
    {
        // Shader 不再使用 radius 参数
    }

    /// <summary>将 FogState 映射到 R 通道 8-bit 值</summary>
    private static byte FogStateToRValue(byte state)
    {
        return state switch
        {
            (byte)FogOfWar.FogState.Unexplored => 0,     // 完全不可见（黑色遮盖）
            (byte)FogOfWar.FogState.Revealed => 255,     // 已揭示 = 完全透明
            (byte)FogOfWar.FogState.InVision => 255,     // 当前视野 = 完全透明
            _ => 0
        };
    }
}
