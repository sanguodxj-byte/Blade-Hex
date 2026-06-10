// HitEffectManager.cs
// 受击特效管理器 — Autoload 单例
// 架构遵循 docs/hiteffect.md 设计:
//   HitEffectManager (Autoload)
//   ├── ObjectPool (3 个独立粒子池: blood/spark/magic)
//   ├── DamageCurve (伤害→强度归一化)
//   ├── ScreenShake (Camera3D 震动)
//   └── HitStop (AnimationPlayer 顿帧)
//
// 用法 (在战斗逻辑层):
//   HitEffectManager.Instance.OnUnitHit(attacker, defender, damage, defender.GetMaxHp(), hitType, isCrit);
//
// ================================================================
// 池化验证
// All pools use NodePool<GpuParticles3D> from UnitViewPool.cs.
//   - bloodPool  → SpawnBlood()   → AcquireParticle("blood")
//   - sparkPool  → SpawnSpark()   → AcquireParticle("spark")
//   - magicPool  → SpawnMagic()   → AcquireParticle("magic")
//
// Fallback path creates new GpuParticles3D() only when a pool is null.
// ================================================================

using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Combat;
using CombatUnit = BladeHex.Combat.Unit;

namespace BladeHex.View.Combat;

/// <summary>
/// 受击材质类型 — 决定使用哪种粒子特效
/// </summary>
public enum HitEffectType
{
    Flesh,   // 血肉/无护甲 → 血液飞溅
    Armor,   // 金属盔甲 → 火花
    Magic,   // 魔法伤害 → 魔法粒子
    Shield,  // 盾牌格挡 → 减弱火花
}

/// <summary>
/// 受击特效管理器 (Autoload 单例)
/// </summary>
/// <para>所属场景: 全局 (Autoload)，在 project.godot 中注册为 HitEffectManager。</para>
/// <para>生命周期: 游戏全程存活，_Ready 中初始化池、_ExitTree 中清理。</para>
/// <para>职责: 提供 OnUnitHit() 入口，根据命中类型/伤害强度生成相应粒子特效、屏幕震动、打击顿帧。</para>
[GlobalClass]
public partial class HitEffectManager : Node
{
    // ========================================
    // 单例
    // ========================================

    public static HitEffectManager? Instance { get; private set; }

    // ========================================
    // 池参数
    // ========================================

    private const int MaxPoolSize = 32;
    private const int PrewarmCount = 4;

    // 统一粒子池 — 所有受击粒子共用
    private NodePool<GpuParticles3D>? _particlePool;

    // 地面血迹 Decal 池
    private NodePool<Decal>? _bloodDecalPool;

    // 程序化生成的圆形血迹纹理
    private Texture2D? _bloodSplatTexture;

    // ========================================
    // 活跃效果追踪（Node 基类，可存 GpuParticles3D 或 Decal）
    // ========================================

    private readonly List<(Node node, double expireAt)> _activeEffects = new();

    // ========================================
    // 生命周期
    // ========================================

    public override void _Ready()
    {
        Instance = this;
        Name = "HitEffectManager";

        // 一、程序化生成圆形血迹纹理（Decal 使用，在 Decal 池之前就绪）
        _bloodSplatTexture = GenerateBloodSplatTexture();

        // 二、粒子池
        _particlePool = new NodePool<GpuParticles3D>(
            factory: () =>
            {
                var p = new GpuParticles3D();
                p.DrawPass1 = new QuadMesh { Size = new Vector2(3f, 3f) };
                return p;
            },
            onRetrieve: p =>
            {
                p.Visible = true;
                p.Emitting = true;
            },
            onReturn: ResetParticle,
            maxSize: MaxPoolSize
        );
        _particlePool.SetParent(this);
        _particlePool.Prewarm(PrewarmCount);

        // 三、地面血迹 Decal 池
        _bloodDecalPool = new NodePool<Decal>(
            factory: () =>
            {
                var d = new Decal();
                d.TextureAlbedo = _bloodSplatTexture;
                d.TextureNormal = null;
                d.UpperFade = 0.0f;
                d.LowerFade = 0.0f;
                d.RotationDegrees = new Vector3(-90, 0, 0); // Y 朝下投影
                return d;
            },
            onRetrieve: d =>
            {
                d.Visible = true;
                d.Modulate = new Color(0.4f, 0.02f, 0.02f, 0.7f);
            },
            onReturn: d =>
            {
                d.Visible = false;
                d.Modulate = Colors.White;
                d.Position = Vector3.Zero;
                d.Scale = Vector3.One;
                d.Size = new Vector3(10f, 40f, 10f);
            },
            maxSize: 24
        );
        _bloodDecalPool.SetParent(this);

        GD.Print("[HitEffectManager] 初始化完成 — 粒子池 + 血迹 Decal 池已就绪");
    }

    public override void _ExitTree()
    {
        _particlePool?.Clear();
        _particlePool = null;
        _bloodDecalPool?.Clear();
        _bloodDecalPool = null;
        _bloodSplatTexture = null;
        _activeEffects.Clear();

        if (Instance == this) Instance = null;
    }

    public override void _Process(double delta)
    {
        if (_activeEffects.Count == 0) return;

        double now = Time.GetTicksMsec() / 1000.0;
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            if (now >= _activeEffects[i].expireAt)
            {
                var node = _activeEffects[i].node;
                _activeEffects.RemoveAt(i);
                if (node is GpuParticles3D p)
                    ReturnParticle(p);
                else if (node is Decal d)
                    ReturnDecal(d);
            }
        }
    }

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>
    /// 受击特效主入口。
    /// </summary>
    /// <param name="attacker">攻击者（可选，用于计算命中位置）</param>
    /// <param name="defender">防御者（必选，受击目标）</param>
    /// <param name="damage">实际造成伤害值</param>
    /// <param name="maxHp">防御者最大 HP</param>
    /// <param name="hitType">受击材质类型</param>
    /// <param name="isCrit">是否暴击</param>
    public void OnUnitHit(
        Node3D? attacker,
        CombatUnit defender,
        int damage,
        int maxHp,
        HitEffectType hitType,
        bool isCrit)
    {
        if (defender == null || !GodotObject.IsInstanceValid(defender)) return;
        if (damage <= 0) return;

        // 1. 归一化伤害 → 强度 t (0~1)
        float t = Normalize(damage, maxHp);

        // 2. 计算命中点：攻击者与防御者之间的中点，偏向防御者
        Vector3 hitPos = CalculateHitPosition(attacker, defender);

        // 3. 根据受击类型生成特效
        switch (hitType)
        {
            case HitEffectType.Flesh:
                SpawnBlood(hitPos, t);
                break;
            case HitEffectType.Armor:
                SpawnSpark(hitPos, t);
                break;
            case HitEffectType.Magic:
                SpawnMagic(hitPos, t);
                break;
            case HitEffectType.Shield:
                SpawnSpark(hitPos, t * 0.6f); // 盾牌减弱
                break;
        }

        // 4. 暴击：追加屏幕震动 + 打击顿帧
        // 注意：如果 defender 已死亡（hp ≤ 0），跳过 Hit-Stop，
        // 因为死亡动画需要 AnimationPlayer 来播放，锁住 SpeedScale 会卡住死亡动画。
        if (isCrit)
        {
            TriggerScreenShake(t);
            if (defender.CurrentHp > 0)  // ✅ 已死亡时跳过顿帧，避免卡住死亡动画
                TriggerHitPause(defender, t);
        }
        else if (t > 0.6f)
        {
            // 大伤害也触发轻量震动
            TriggerScreenShake(t * 0.5f);
        }
    }

    /// <summary>
    /// 便捷重载: 自动从 defender 的 Data 获取 maxHp
    /// </summary>
    public void OnUnitHit(
        Node3D? attacker,
        CombatUnit defender,
        int damage,
        HitEffectType hitType,
        bool isCrit)
    {
        int maxHp = defender.GetMaxHp();
        OnUnitHit(attacker, defender, damage, maxHp, hitType, isCrit);
    }

    // ========================================
    // 伤害归一化
    // ========================================

    /// <summary>
    /// 将伤害归一化到 0~1 范围。
    /// 使用 pow(0.7) 让中低段伤害有更好区分度。
    /// </summary>
    private static float Normalize(int damage, int maxHp)
    {
        if (maxHp <= 0) return 1f;
        float raw = (float)damage / maxHp;
        raw = Mathf.Clamp(raw, 0f, 1f);
        // 幂函数让中低伤害视觉偏强
        return Mathf.Pow(raw, 0.7f);
    }

    // ========================================
    // 命中位置计算
    // ========================================

    private static Vector3 CalculateHitPosition(Node3D? attacker, CombatUnit defender)
    {
        Vector3 defPos = defender.GlobalPosition + Vector3.Up * 50f;

        if (attacker != null && GodotObject.IsInstanceValid(attacker))
        {
            Vector3 atkPos = attacker.GlobalPosition + Vector3.Up * 50f;
            // 取中点偏向防御者 (60% 偏向防御者)
            return atkPos.Lerp(defPos, 0.6f);
        }

        return defPos;
    }

    // ========================================
    // 血液飞溅
    // ========================================

    private void SpawnBlood(Vector3 pos, float t)
    {
        var particles = AcquireParticle(pos);
        if (particles == null) return;

        int amount = Mathf.RoundToInt(Mathf.Lerp(4f, 28f, t));
        float lifetime = Mathf.Lerp(0.3f, 0.7f, t);

        var mat = CreateParticleMaterial(
            color: new Color(0.55f, 0.04f, 0.04f),
            spread: Mathf.Lerp(50f, 170f, t),
            velMin: Mathf.Lerp(60f, 240f, t),
            velMax: Mathf.Lerp(80f, 300f, t),
            gravity: new Vector3(0, Mathf.Lerp(-600f, -1400f, t), 0),
            scaleMin: Mathf.Lerp(0.4f, 2.0f, t),
            scaleMax: Mathf.Lerp(0.8f, 3.0f, t)
        );

        ConfigureParticle(particles, pos, amount, lifetime, new Color(0.55f, 0.04f, 0.04f), mat);

        // 大伤害追加地面血迹溅射
        if (t > 0.75f)
        {
            float radius = Mathf.Lerp(8f, 24f, t);
            SpawnBloodSplat(pos + new Vector3(0, -5f, 0), radius);
        }

        ScheduleReturn(particles, lifetime + 0.3f);
    }

    /// <summary>
    /// 地面血迹 Decal — 使用 Decal 节点投射一个圆形血迹纹理到地面。
    /// 位置：defender 脚底（y 降到 CharacterLayer 以下，让 Decal 向下投射到地面）。
    /// 持久时间：轻伤 2s，重伤 5s，然后淡出消失。
    /// </summary>
    private void SpawnBloodSplat(Vector3 pos, float radius)
    {
        if (_bloodSplatTexture == null) return;

        var decal = AcquireBloodDecal();
        if (decal == null) return;

        // 位置计算：
        // pos 是命中点（defender.GlobalPosition + Up*50，即角色腰部高度）。
        // Decal 需要投射到地面（六棱柱顶面），地面 Y = defender.GlobalPosition.Y - CharacterLayer + HexTopOffset
        // defender.GlobalPosition.Y 已经包含 CharacterLayer 偏移
        float groundY = pos.Y - CombatLayerHeight.CharacterLayer + CombatLayerHeight.HexTopOffset;
        Vector3 decalPos = new Vector3(pos.X, groundY, pos.Z);
        decal.Position = decalPos;

        // Decal Size: x/z 是投影范围，y 是投影深度
        float decalSize = Mathf.Lerp(6f, 20f, radius / 24f);
        decal.Size = new Vector3(decalSize, 40f, decalSize);

        // 随机旋转让血迹方向不一致
        decal.RotationDegrees = new Vector3(-90, (float)(GD.Randi() % 360), 0);

        // 随机缩放微调
        float scale = Mathf.Lerp(0.6f, 1.4f, (float)GD.RandRange(0.0, 1.0));
        decal.Scale = new Vector3(scale, 1f, scale);

        // alpha = 根据半径大小决定不透明度，让轻伤血迹更淡
        float alpha = Mathf.Clamp(radius / 24f, 0.3f, 0.7f);
        decal.Modulate = new Color(0.35f, 0.02f, 0.02f, alpha);

        // 持久时间：小血迹 2s，大血迹 5s
        float persistDuration = Mathf.Lerp(2f, 5f, radius / 24f);
        // 然后淡出 0.5s
        _ = FadeOutDecal(decal, persistDuration, 0.5f);
        ScheduleReturn(decal, persistDuration + 0.6f);
    }

    /// <summary>
    /// 程序化生成圆形血迹纹理 — 使用 Image 绘制半透明红色径向渐变圆
    /// </summary>
    private static Texture2D GenerateBloodSplatTexture()
    {
        int size = 128;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        if (image == null)
        {
            // 降级：如果 Image.Create 返回 null，返回空纹理
            return new ImageTexture();
        }

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float maxDist = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pixel = new Vector2(x, y);
                float dist = pixel.DistanceTo(center) / maxDist; // 0~1

                if (dist > 1.0f)
                {
                    image.SetPixel(x, y, Colors.Transparent);
                    continue;
                }

                // 径向渐变：中心最深，边缘渐淡
                // 用 pow 制造硬边缘假象：中心 0~0.4 区域不透明，0.4~1.0 迅速淡出
                float alpha;
                if (dist < 0.35f)
                {
                    // 中心实心区
                    alpha = 0.7f;
                }
                else if (dist < 0.7f)
                {
                    // 过渡区：平滑下降
                    alpha = 0.7f * (1f - (dist - 0.35f) / 0.35f);
                }
                else
                {
                    // 溅射边缘：快速淡出 + 随机噪点模拟飞溅痕迹
                    float edge = (dist - 0.7f) / 0.3f;
                    float noise = (float)(GD.Randf() * 0.3f);
                    alpha = Mathf.Max(0f, 0.6f * (1f - edge) + noise * (1f - edge));
                }

                // 血迹颜色：暗红
                float r = 0.35f + 0.1f * (1f - dist);
                float g = 0.02f + 0.03f * (1f - dist);
                float b = 0.02f + 0.03f * (1f - dist);
                image.SetPixel(x, y, new Color(r, g, b, Mathf.Clamp(alpha, 0f, 0.75f)));
            }
        }

        var tex = ImageTexture.CreateFromImage(image);
        return tex ?? new ImageTexture();
    }

    /// <summary>
    /// 血迹 Decal 淡出协程 — 先持续显示 persistDuration 秒，再 0.5s 渐隐到透明
    /// </summary>
    private static async System.Threading.Tasks.Task FadeOutDecal(Decal decal, float persistDuration, float fadeDuration)
    {
        if (!GodotObject.IsInstanceValid(decal)) return;
        await decal.ToSignal(decal.GetTree().CreateTimer(persistDuration), SceneTreeTimer.SignalName.Timeout);

        if (!GodotObject.IsInstanceValid(decal)) return;

        float elapsed = 0f;
        Color startColor = decal.Modulate;
        while (elapsed < fadeDuration)
        {
            if (!GodotObject.IsInstanceValid(decal)) return;
            float t = elapsed / fadeDuration;
            decal.Modulate = new Color(startColor.R, startColor.G, startColor.B, startColor.A * (1f - t));
            elapsed += 0.033f;
            await decal.ToSignal(decal.GetTree().CreateTimer(0.033f), SceneTreeTimer.SignalName.Timeout);
        }

        if (GodotObject.IsInstanceValid(decal))
            decal.Modulate = new Color(startColor.R, startColor.G, startColor.B, 0f);
    }

    // ========================================
    // 盔甲火花
    // ========================================

    private void SpawnSpark(Vector3 pos, float t)
    {
        var particles = AcquireParticle(pos);
        if (particles == null) return;

        int amount = Mathf.RoundToInt(Mathf.Lerp(3f, 14f, t));
        float lifetime = Mathf.Lerp(0.1f, 0.45f, t);

        // 温度: 0=橙黄 1=白热
        float heat = Mathf.Lerp(0f, 1f, t);
        Color sparkColor = heat < 0.5f
            ? new Color(1f, 0.55f, 0.05f)   // 橙黄
            : new Color(1f, 0.95f, 0.8f);    // 白热

        var mat = CreateParticleMaterial(
            color: sparkColor,
            spread: 120f,
            velMin: Mathf.Lerp(80f, 250f, t),
            velMax: Mathf.Lerp(120f, 400f, t),
            gravity: new Vector3(0, -800f, 0),
            scaleMin: 0.2f,
            scaleMax: 0.8f
        );

        ConfigureParticle(particles, pos, amount, lifetime, sparkColor, mat);

        // 同时生成少量脱离飞出的火星
        SpawnEmberParticles(pos, t);

        ScheduleReturn(particles, lifetime + 0.3f);
    }

    /// <summary>
    /// 火星粒子（从盔甲火花中飞出的实体粒子）
    /// </summary>
    private void SpawnEmberParticles(Vector3 pos, float t)
    {
        var particles = AcquireParticle(pos);
        if (particles == null) return;

        int count = Mathf.RoundToInt(Mathf.Lerp(2f, 8f, t));
        float speed = Mathf.Lerp(40f, 160f, t);

        var mat = CreateParticleMaterial(
            color: new Color(1f, 0.7f, 0.2f, 0.8f),
            spread: 90f,
            velMin: speed * 0.5f,
            velMax: speed,
            gravity: new Vector3(0, -300f, 0),
            scaleMin: 0.15f,
            scaleMax: 0.4f
        );

        ConfigureParticle(particles, pos, count, 0.6f, new Color(1f, 0.7f, 0.2f, 0.8f), mat);
        ScheduleReturn(particles, 0.8f);
    }

    // ========================================
    // 魔法粒子
    // ========================================

    private void SpawnMagic(Vector3 pos, float t)
    {
        var particles = AcquireParticle(pos);
        if (particles == null) return;

        int amount = Mathf.RoundToInt(Mathf.Lerp(6f, 24f, t));
        float lifetime = Mathf.Lerp(0.3f, 0.8f, t);

        // 魔法颜色渐变: 蓝 → 紫 → 白
        float hue = Mathf.Lerp(0.65f, 0.75f, t); // blue(0.65) → purple(0.75) → white
        float sat = Mathf.Lerp(0.8f, 0.3f, t);
        float val = Mathf.Lerp(0.8f, 1.0f, t);
        Color magicColor = Color.FromHsv(hue, sat, val);

        var mat = CreateParticleMaterial(
            color: magicColor,
            spread: 150f,
            velMin: Mathf.Lerp(40f, 160f, t),
            velMax: Mathf.Lerp(80f, 300f, t),
            gravity: new Vector3(0, -100f, 0),
            scaleMin: 0.5f,
            scaleMax: 2.0f
        );

        ConfigureParticle(particles, pos, amount, lifetime, magicColor, mat);

        ScheduleReturn(particles, lifetime + 0.3f);
    }

    // ========================================
    // 屏幕震动
    // ========================================

    /// <summary>
    /// 触发屏幕震动 — 查找当前场景的 Camera3D，对 offset 做随机偏移。
    /// </summary>
    private void TriggerScreenShake(float intensity)
    {
        if (intensity <= 0f) return;

        // 从当前场景树中获取活动的 Camera3D
        var tree = GetTree();
        if (tree == null) return;

        var sceneRoot = tree.CurrentScene;
        if (sceneRoot == null) return;

        // 优先找 CombatCameraController，其次找任意 Camera3D
        Camera3D? cam = FindCameraRecursive(sceneRoot);
        if (cam == null) return;

        float duration = Mathf.Lerp(0.08f, 0.28f, intensity);
        float magnitude = Mathf.Lerp(1.0f, 8.0f, intensity);

        // 启动协程式震动
        _ = ShakeCamera(cam, duration, magnitude);
    }

    private static async System.Threading.Tasks.Task ShakeCamera(Camera3D cam, float duration, float magnitude)
    {
        if (!GodotObject.IsInstanceValid(cam)) return;

        float elapsed = 0f;
        Vector3 originalOffset = cam.Transform.Origin; // 记录原始位置

        while (elapsed < duration)
        {
            if (!GodotObject.IsInstanceValid(cam)) return;

            float x = (float)(GD.RandRange(-1.0, 1.0) * magnitude);
            float y = (float)(GD.RandRange(-1.0, 1.0) * magnitude);
            cam.Transform = cam.Transform with { Origin = originalOffset + new Vector3(x, y, 0) };

            elapsed += 0.016f; // ~60fps 帧间隔
            await cam.ToSignal(cam.GetTree().CreateTimer(0.016f), SceneTreeTimer.SignalName.Timeout);
        }

        if (GodotObject.IsInstanceValid(cam))
        {
            cam.Transform = cam.Transform with { Origin = originalOffset };
        }
    }

    private static Camera3D? FindCameraRecursive(Node node)
    {
        if (node is Camera3D cam && cam.Current) return cam;

        foreach (var child in node.GetChildren())
        {
            var found = FindCameraRecursive(child);
            if (found != null) return found;
        }
        return null;
    }

    // ========================================
    // 打击顿帧 (Hit-Stop)
    // ========================================

    /// <summary>
    /// 触发打击顿帧 — 只冻结防御者的 AnimationPlayer 速度，不影响全局时间。
    /// </summary>
    private void TriggerHitPause(CombatUnit defender, float intensity)
    {
        if (defender == null || !GodotObject.IsInstanceValid(defender)) return;

        // 查找防御者身上的 AnimationPlayer
        AnimationPlayer? animPlayer = null;
        foreach (var child in defender.GetChildren())
        {
            if (child is AnimationPlayer ap)
            {
                animPlayer = ap;
                break;
            }
        }
        // 也递归查找
        animPlayer ??= FindAnimationPlayerRecursive(defender);

        if (animPlayer == null) return;

        int frames = Mathf.RoundToInt(Mathf.Lerp(2f, 6f, intensity));
        float pauseDuration = frames * (1f / 60f); // 以 60fps 帧数换算秒

        animPlayer.SpeedScale = 0f;

        _ = ResumeAfterPause(animPlayer, pauseDuration);
    }

    private static async System.Threading.Tasks.Task ResumeAfterPause(AnimationPlayer animPlayer, float duration)
    {
        await animPlayer.ToSignal(animPlayer.GetTree().CreateTimer(duration), SceneTreeTimer.SignalName.Timeout);

        // 防守性检查：单位可能在暂停期间死亡并被 QueueFree，此时 animPlayer 的树可能已销毁
        if (GodotObject.IsInstanceValid(animPlayer) && animPlayer.GetTree() != null)
        {
            animPlayer.SpeedScale = 1f;
        }
    }

    private static AnimationPlayer? FindAnimationPlayerRecursive(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is AnimationPlayer ap) return ap;
            var found = FindAnimationPlayerRecursive(child);
            if (found != null) return found;
        }
        return null;
    }

    // ========================================
    // 血迹 Decal 池方法
    // ========================================

    /// <summary>
    /// 从池中获取一个血迹 Decal 节点。
    /// 降级路径：直接 new（安全兜底）。
    /// </summary>
    private Decal? AcquireBloodDecal()
    {
        if (_bloodDecalPool == null)
        {
            var fallback = new Decal();
            fallback.TextureAlbedo = _bloodSplatTexture;
            fallback.TextureNormal = null;
            fallback.UpperFade = 0.0f;
            fallback.LowerFade = 0.0f;
            fallback.RotationDegrees = new Vector3(-90, 0, 0);
            AddChild(fallback);
            return fallback;
        }

        var decal = _bloodDecalPool.Retrieve();
        return decal;
    }

    private void ReturnDecal(Decal? decal)
    {
        if (decal == null || !GodotObject.IsInstanceValid(decal)) return;
        _bloodDecalPool?.Return(decal);
    }

    // ========================================
    // 对象池 — 内部方法
    // ========================================

    /// <summary>
    /// 从统一对象池获取粒子节点，加入场景树。
    /// </summary>
    private GpuParticles3D? AcquireParticle(Vector3 pos)
    {
        if (_particlePool == null)
        {
            // 降级 — 池未初始化时直接创建（不应发生，但保证安全）
            var fallback = new GpuParticles3D();
            AddChild(fallback);
            fallback.GlobalPosition = pos;
            // 设置基本材质让它可见
            var drawMat = new StandardMaterial3D
            {
                ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded,
                BillboardMode = StandardMaterial3D.BillboardModeEnum.Enabled,
                Transparency = StandardMaterial3D.TransparencyEnum.Alpha,
                RenderPriority = 15,
                AlbedoColor = Colors.White
            };
            fallback.MaterialOverride = drawMat;
            fallback.DrawPass1 = new QuadMesh { Size = new Vector2(3f, 3f) };
            fallback.Emitting = true;
            fallback.OneShot = true;
            fallback.GetTree().CreateTimer(0.8f).Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(fallback)) fallback.QueueFree();
            };
            return fallback;
        }

        var particles = _particlePool.Retrieve();
        // Retrieve() 内部已通过 SetParent(this) + AddChild 加入场景树
        particles.GlobalPosition = pos;
        return particles;
    }

    private void ReturnParticle(GpuParticles3D? particles)
    {
        if (particles == null || !GodotObject.IsInstanceValid(particles)) return;

        // NodePool.Return 内部会 RemoveChild + 推入栈
        _particlePool?.Return(particles);
    }

    private static void ResetParticle(GpuParticles3D p)
    {
        p.Emitting = false;
        p.Visible = false;
        p.OneShot = false;
        p.Amount = 1;
        p.Lifetime = 1.0f;
        p.Explosiveness = 0.0f;
        p.GlobalPosition = Vector3.Zero;
        p.ProcessMaterial = null;
        p.MaterialOverride = null;
    }

    // ========================================
    // 粒子配置辅助方法
    // ========================================

    private static ParticleProcessMaterial CreateParticleMaterial(
        Color color,
        float spread,
        float velMin,
        float velMax,
        Vector3 gravity,
        float scaleMin,
        float scaleMax)
    {
        return new ParticleProcessMaterial
        {
            Spread = spread,
            InitialVelocityMin = velMin,
            InitialVelocityMax = velMax,
            Gravity = gravity,
            ScaleMin = scaleMin,
            ScaleMax = scaleMax,
            Color = color
        };
    }

    private static void ConfigureParticle(
        GpuParticles3D particles,
        Vector3 pos,
        int amount,
        float lifetime,
        Color color,
        ParticleProcessMaterial mat)
    {
        particles.GlobalPosition = pos;
        particles.Emitting = true;
        particles.OneShot = true;
        particles.Amount = amount;
        particles.Explosiveness = 0.9f;
        particles.Lifetime = lifetime;
        particles.ProcessMaterial = mat;

        // 绘制材质 — 非光照 billboard
        // RenderPriority 必须在单位 body（默认 0）之上、在投射物（40）之下
        // 参考: GrassOverlayBatcher=-1, 单位默认=0, DamageNumberPopup=20, ProjectileView=40
        var drawMat = new StandardMaterial3D
        {
            ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = StandardMaterial3D.BillboardModeEnum.Enabled,
            Transparency = StandardMaterial3D.TransparencyEnum.Alpha,
            RenderPriority = 15,
            AlbedoColor = color
        };
        particles.MaterialOverride = drawMat;
    }

    private void ScheduleReturn(Node node, float delay)
    {
        double expireAt = Time.GetTicksMsec() / 1000.0 + delay;
        _activeEffects.Add((node, expireAt));
    }
}
