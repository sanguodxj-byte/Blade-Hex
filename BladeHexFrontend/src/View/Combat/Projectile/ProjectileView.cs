// ProjectileView.cs
// 投射物表现层 — 飞行动画 + 贴图
// 只关心"怎么飞得好看"，不关心伤害
// 由 ProjectilePool 路由调用 Play()，不自行订阅 EventBus
using Godot;
using BladeHex.Map;

namespace BladeHex.Combat;

/// <summary>
/// 投射物视图 — Node3D 场景节点
/// 由 ProjectilePool 路由调用 Play()，按轨迹飞行，到达后回收
/// </summary>
[GlobalClass]
public partial class ProjectileView : Node3D, IProjectileView
{
    // ========================================
    // 状态
    // ========================================
    private Sprite3D? _sprite;
    private bool _playing = false;
    private Vector3 _from;
    private Vector3 _to;
    private float _duration;
    private float _elapsed;
    private float _arcHeight;

    /// <summary>投射物类型 — 由 Pool 创建时设置</summary>
    public string ProjectileType { get; set; } = "arrow";

    // ========================================
    // 贴图配置
    // ========================================
    private static readonly string DefaultTexture = "res://assets/sprites/projectiles/arrow.png";

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
        // 创建子节点
        _sprite = new Sprite3D();
        _sprite.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _sprite.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
        AddChild(_sprite);

        // 不订阅 EventBus — 由 ProjectilePool 统一路由
    }

    public override void _Process(double delta)
    {
        if (!_playing) return;

        _elapsed += (float)delta;
        float t = Mathf.Clamp(_elapsed / _duration, 0.0f, 1.0f);

        // 计算轨迹位置
        GlobalPosition = ProjectileTrajectory.Evaluate(ProjectileType, _from, _to, t, _arcHeight);

        // 飞刀自旋
        if (ProjectileType == "throwing_knife" || ProjectileType == "throwing_axe")
        {
            if (_sprite != null)
                _sprite.RotationDegrees = new Vector3(0, 0, ProjectileTrajectory.KnifeSpin(t));
        }

        // 飞行完成
        if (t >= 1.0f)
        {
            _playing = false;
            OnFlightComplete();
        }
    }

    // ========================================
    // IProjectileView 实现
    // ========================================

    public void Play(ProjectileData data, float duration)
    {
        if (data == null) return;

        // 允许动态切换类型（同一实例复用不同投射物）
        ProjectileType = data.ProjectileType;
        _from = HexUtils.AxialToWorld3D(data.Origin.X, data.Origin.Y);
        _to = HexUtils.AxialToWorld3D(data.Target.X, data.Target.Y);
        _duration = duration;
        _elapsed = 0.0f;
        _arcHeight = data.ArcHeight;
        _playing = true;

        // 加载贴图
        LoadTexture(data);

        // 初始位置
        GlobalPosition = _from;
        Visible = true;
    }

    public void Stop()
    {
        _playing = false;
        Visible = false;
    }

    // ========================================
    // 内部方法
    // ========================================

    private void LoadTexture(ProjectileData data)
    {
        if (_sprite == null) return;

        string path = !string.IsNullOrEmpty(data.TexturePath)
            ? data.TexturePath
            : (TypeTextures.TryGetValue(data.ProjectileType, out var texPath) ? texPath : DefaultTexture);

        var tex = GD.Load<Texture2D>(path);
        if (tex != null)
        {
            _sprite.Texture = tex;
            // 根据贴图实际尺寸和目标世界尺寸计算 PixelSize
            _sprite.PixelSize = BladeHex.View.Combat.TextureScaleConfig.GetProjectilePixelSize(
                data.ProjectileType, tex);
        }
    }

    /// <summary>飞行完成 — 回收到对象池</summary>
    private void OnFlightComplete()
    {
        Visible = false;

        // 通知对象池回收
        var pool = GetParentOrNull<ProjectilePool>();
        if (pool != null)
            pool.Return(this);
        else
            Stop();
    }
}
