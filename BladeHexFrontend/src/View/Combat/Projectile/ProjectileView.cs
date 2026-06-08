using BladeHex.Map;
using BladeHex.View.AssetSystem;
using BladeHex.View.Combat;
using Godot;

namespace BladeHex.Combat;

/// <summary>
/// Visual node for a pooled combat projectile.
/// </summary>
[GlobalClass]
public partial class ProjectileView : Node3D, IProjectileView
{
    private Sprite3D? _sprite;
    private bool _playing;
    private Vector3 _from;
    private Vector3 _to;
    private float _duration;
    private float _elapsed;
    private float _arcHeight;

    private const float ProjectileLayerOffset = 22.0f;
    private const float ArcHeightWorldScale = HexUtils.Size * 0.45f;
    private const float MinVisualArcHeight = 12.0f;
    private const string DefaultTexture = "res://assets/sprites/projectiles/arrow.png";

    public string ProjectileType { get; set; } = "arrow";

    private static readonly System.Collections.Generic.Dictionary<string, string> TypeTextures = new()
    {
        { "arrow", "res://assets/sprites/projectiles/arrow.png" },
        { "crossbow_bolt", "res://assets/sprites/projectiles/crossbow_bolt.png" },
        { "throwing_knife", "res://assets/sprites/projectiles/throwing_knife.png" },
        { "throwing_axe", "res://assets/sprites/projectiles/throwing_axe.png" },
        { "fireball", "res://assets/sprites/projectiles/fireball.png" },
        { "magic_bolt", "res://assets/sprites/projectiles/magic_bolt.png" },
        { "ice_shard", "res://assets/sprites/projectiles/ice_shard.png" },
        { "lightning", "res://assets/sprites/projectiles/lightning.png" },
    };

    public override void _Ready()
    {
        _sprite = new Sprite3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            NoDepthTest = true,
            RenderPriority = 40,
        };
        AddChild(_sprite);
    }

    public override void _Process(double delta)
    {
        if (!_playing)
            return;

        _elapsed += (float)delta;
        float t = Mathf.Clamp(_elapsed / _duration, 0.0f, 1.0f);
        GlobalPosition = ProjectileTrajectory.Evaluate(ProjectileType, _from, _to, t, _arcHeight);

        if (ProjectilePool.DebugLogging && (int)(_elapsed * 60) % 10 == 0)
            GD.Print($"[ProjectileView] _Process: t={t:F3}, pos={GlobalPosition}, Visible={Visible}");

        if ((ProjectileType == "throwing_knife" || ProjectileType == "throwing_axe") && _sprite != null)
            _sprite.RotationDegrees = new Vector3(0, 0, ProjectileTrajectory.KnifeSpin(t));

        if (t >= 1.0f)
        {
            if (ProjectilePool.DebugLogging)
                GD.Print("[ProjectileView] Flight complete");

            _playing = false;
            OnFlightComplete();
        }
    }

    public void Play(ProjectileData data, float duration)
    {
        if (data == null)
        {
            GD.PrintErr("[ProjectileView] Play called with NULL data.");
            return;
        }

        if (ProjectilePool.DebugLogging)
            GD.Print($"[ProjectileView] Play: type={data.ProjectileType}, origin={data.Origin}, target={data.Target}, duration={duration}");

        ProjectileType = data.ProjectileType;
        _from = GetVisualWorldPosition(data.Origin);
        _to = GetVisualWorldPosition(data.Target);
        _duration = duration;
        _elapsed = 0.0f;
        _arcHeight = GetVisualArcHeight(data.ArcHeight);
        _playing = true;

        LoadTexture(data);

        GlobalPosition = _from;
        Visible = true;

        if (ProjectilePool.DebugLogging)
            GD.Print($"[ProjectileView] Play complete: _playing={_playing}, Visible={Visible}, GlobalPosition={GlobalPosition}");
    }

    public void Stop()
    {
        _playing = false;
        Visible = false;
    }

    private void LoadTexture(ProjectileData data)
    {
        if (_sprite == null)
        {
            GD.PrintErr("[ProjectileView] LoadTexture called before _Ready.");
            return;
        }

        string fallbackPath = !string.IsNullOrEmpty(data.TexturePath)
            ? data.TexturePath
            : (TypeTextures.TryGetValue(data.ProjectileType, out var texPath) ? texPath : DefaultTexture);

        if (ProjectilePool.DebugLogging)
            GD.Print($"[ProjectileView] LoadTexture: type={data.ProjectileType}, fallback={fallbackPath}");

        var tex = TextureAssetResolver.LoadProjectileTexture(data.ProjectileType, fallbackPath);
        if (tex == null)
        {
            GD.PrintErr($"[ProjectileView] Failed to load projectile texture {data.ProjectileType} ({fallbackPath}).");
            return;
        }

        _sprite.Texture = tex;
        _sprite.PixelSize = TextureScaleConfig.GetProjectilePixelSize(data.ProjectileType, tex);
        _sprite.Scale = Vector3.One;

        if (ProjectilePool.DebugLogging)
            GD.Print($"[ProjectileView] LoadTexture: texture loaded, PixelSize={_sprite.PixelSize}");
    }

    private void OnFlightComplete()
    {
        Visible = false;
        var pool = GetParentOrNull<ProjectilePool>();
        if (pool != null)
            pool.Return(this);
        else
            Stop();
    }

    public static Vector3 GetVisualWorldPosition(Vector2I gridPos)
    {
        return HexUtils.AxialToWorld3D(gridPos.X, gridPos.Y)
            + new Vector3(0, CombatLayerHeight.HexTopOffset + CombatLayerHeight.CharacterLayer + ProjectileLayerOffset, 0);
    }

    public static float GetVisualArcHeight(float arcHeight)
    {
        return Mathf.Max(MinVisualArcHeight, arcHeight * ArcHeightWorldScale);
    }
}
