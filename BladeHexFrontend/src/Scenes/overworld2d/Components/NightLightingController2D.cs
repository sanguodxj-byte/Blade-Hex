using Godot;
using System;
using System.Collections.Generic;
using BladeHex.View.AssetSystem;
using BladeHex.Map;
using BladeHex.Strategic;

namespace BladeHex.Scenes.Overworld2d.Components;

[GlobalClass]
public partial class NightLightingController2D : Node2D
{
    private const int MaxLights = 192;
    private const int GlowTextureSize = 256;
    private const float HexSize = 156f;
    private const float BaseCollectionRadius = 4000f;
    private const float DuskStartHour = 18.0f;
    private const float NightStartHour = 20.0f;
    private const float DawnStartHour = 4.0f;
    private const float DawnEndHour = 6.0f;

    private static readonly Color LightColorTorch = new(1.0f, 0.72f, 0.32f);
    private static readonly Color LightColorTown = new(1.0f, 0.82f, 0.45f);
    private static readonly Color LightColorVillage = new(0.95f, 0.75f, 0.40f);
    private static readonly Color LightColorCastle = new(0.85f, 0.78f, 0.60f);
    private static readonly Color LightColorPort = new(0.80f, 0.82f, 0.55f);
    private static readonly Color LightColorShrine = new(0.75f, 0.60f, 0.85f);
    private static readonly Color LightColorPlayer = new(1.0f, 0.88f, 0.60f);
    private static readonly Color LightColorEntity = new(0.90f, 0.65f, 0.30f);

    private Node2D? _glowRoot;
    private readonly List<Sprite2D> _glowSprites = new(MaxLights);
    private readonly List<LightSourceCandidate> _candidates = new(MaxLights * 2);

    private static ImageTexture? _glowTexture;
    private static Material? _glowMaterial;

    private List<OverworldPOI>? _worldPois;
    private OverworldEntityManager? _entityMgr;
    private Func<Vector2>? _getPlayerPos;
    private Func<int>? _getPlayerPartySize;
    private Func<float>? _getHour;
    private FogOfWar? _fog;
    private Camera2D? _camera;

    public override void _Ready()
    {
        ProcessPriority = 100;
        ZIndex = 70;
    }

    public void Initialize(
        List<OverworldPOI> worldPois,
        OverworldEntityManager? entityMgr,
        Func<Vector2> getPlayerPos,
        Func<int>? getPlayerPartySize,
        Func<float>? getHour,
        FogOfWar? fog,
        Camera2D? camera)
    {
        _worldPois = worldPois;
        _entityMgr = entityMgr;
        _getPlayerPos = getPlayerPos;
        _getPlayerPartySize = getPlayerPartySize;
        _getHour = getHour;
        _fog = fog;
        _camera = camera;

        _glowRoot = new Node2D
        {
            Name = "NightGlowSprites",
            Visible = false,
            ZIndex = 0,
        };
        AddChild(_glowRoot);

        var glowTexture = GetGlowTexture();
        var glowMaterial = GetGlowMaterial();
        for (int i = 0; i < MaxLights; i++)
        {
            var sprite = new Sprite2D
            {
                Name = $"NightGlow_{i:D2}",
                Texture = glowTexture,
                Material = glowMaterial,
                Centered = true,
                Visible = false,
                TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            };
            _glowRoot.AddChild(sprite);
            _glowSprites.Add(sprite);
        }

        GD.Print("[NightLighting] initialized");
    }

    public void Tick()
    {
        if (_glowRoot == null)
            return;

        float nightStrength = ComputeNightStrength();
        if (nightStrength < 0.01f)
        {
            _glowRoot.Visible = false;
            HideUnusedSprites(0);
            return;
        }

        _glowRoot.Visible = true;

        _candidates.Clear();
        CollectLightSources(nightStrength);
        _candidates.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        Vector2 cameraCenter = GetCameraCenter();
        float now = Time.GetTicksMsec() / 1000.0f;

        int visibleCount = 0;
        for (int i = 0; i < _candidates.Count && visibleCount < MaxLights; i++)
        {
            var light = _candidates[i];
            float worldRadius = Mathf.Clamp(light.Radius, 20.0f, 1400.0f);

            if (light.Position.DistanceTo(cameraCenter) > GetCollectionRadius() + worldRadius)
                continue;

            float flicker = 0.92f + 0.08f * MathF.Sin(now * 2.7f + light.FlickerPhase);
            float alpha = Mathf.Clamp(light.Intensity * 0.95f * flicker, 0.0f, 0.95f);

            var sprite = _glowSprites[visibleCount++];
            sprite.Visible = true;
            sprite.Position = light.Position;
            sprite.Scale = Vector2.One * (worldRadius * 2.0f / GlowTextureSize);
            sprite.Modulate = new Color(light.Color.R, light.Color.G, light.Color.B, alpha);
        }

        HideUnusedSprites(visibleCount);
    }

    private void HideUnusedSprites(int startIndex)
    {
        for (int i = startIndex; i < _glowSprites.Count; i++)
            _glowSprites[i].Visible = false;
    }

    private Vector2 GetCameraCenter()
    {
        return _camera?.GetScreenCenterPosition() ?? _getPlayerPos?.Invoke() ?? Vector2.Zero;
    }

    private float GetCameraZoom()
    {
        return Mathf.Max(_camera?.Zoom.X ?? 1.0f, 0.001f);
    }

    private float GetCollectionRadius()
    {
        Vector2 viewportSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1280.0f, 720.0f);
        float visibleRadius = viewportSize.Length() * 0.5f / GetCameraZoom();
        return Mathf.Max(BaseCollectionRadius, visibleRadius + HexSize * 6.0f);
    }

    private float ComputeNightStrength()
    {
        float hour = NormalizeHour(_getHour?.Invoke() ?? GetSystemHour());

        if (hour >= NightStartHour || hour < DawnStartHour)
            return 1.0f;

        if (hour >= DuskStartHour && hour < NightStartHour)
            return SmoothStep(DuskStartHour, NightStartHour, hour);

        if (hour >= DawnStartHour && hour < DawnEndHour)
            return 1.0f - SmoothStep(DawnStartHour, DawnEndHour, hour);

        return 0.0f;
    }

    private void CollectLightSources(float nightStrength)
    {
        Vector2 playerPos = _getPlayerPos?.Invoke() ?? Vector2.Zero;
        Vector2 cullCenter = _camera?.GetScreenCenterPosition() ?? playerPos;
        float collectionRadius = GetCollectionRadius();

        int playerPartySize = _getPlayerPartySize?.Invoke() ?? 4;
        float playerSizeFactor = MathF.Sqrt(playerPartySize / 4.0f);
        playerSizeFactor = Mathf.Clamp(playerSizeFactor, 0.6f, 2.5f);
        float playerRadius = 380f * playerSizeFactor;
        float playerIntensity = 0.45f * (0.8f + 0.2f * playerSizeFactor) * nightStrength;

        _candidates.Add(new LightSourceCandidate
        {
            Position = playerPos,
            Color = LightColorPlayer,
            Intensity = Mathf.Clamp(playerIntensity, 0.3f * nightStrength, 0.95f * nightStrength),
            Radius = playerRadius,
            FlickerPhase = 0f,
            Priority = 1000f,
        });

        if (_worldPois != null)
        {
            foreach (var poi in _worldPois)
            {
                if (_fog != null && !_fog.IsRevealed(poi.Position.X, poi.Position.Y))
                    continue;

                float dist = poi.Position.DistanceTo(cullCenter);
                var profile = POIScaleTable.Get(poi.Scale);
                float outerRadius = profile.LightRange * HexSize;
                if (dist > collectionRadius + outerRadius)
                    continue;

                var occupiedHexes = GetPoiOccupiedHexes(poi);
                float energy = profile.LightEnergy;
                Color color = GetPOILightColor(poi);

                if (poi.IsUnderSiege)
                {
                    color = color.Lerp(new Color(1f, 0.35f, 0.1f), 0.5f);
                    energy *= 1.4f;
                }

                float cellRadius = Mathf.Clamp(outerRadius * 1.60f, HexSize * 3.0f, HexSize * 6.0f);
                float footprintDimming = Mathf.Lerp(1.0f, 0.45f, Mathf.Clamp((occupiedHexes.Length - 1) / 6.0f, 0.0f, 1.0f));
                float cellIntensity = Mathf.Clamp(energy * footprintDimming * 0.80f, 0.25f, 0.75f) * nightStrength;
                float priority = 500f + energy * 20f - dist * 0.006f;

                for (int j = 0; j < occupiedHexes.Length; j++)
                {
                    var hex = occupiedHexes[j];
                    Vector2 lightPosition = HexOverworldTile.AxialToPixel(hex.X, hex.Y);
                    float cellDist = lightPosition.DistanceTo(cullCenter);
                    if (cellDist > collectionRadius + cellRadius)
                        continue;

                    float phase = HashPhase(poi.PoiName, j);

                    _candidates.Add(new LightSourceCandidate
                    {
                        Position = lightPosition,
                        Color = color,
                        Intensity = cellIntensity,
                        Radius = cellRadius,
                        FlickerPhase = phase,
                        Priority = priority - cellDist * 0.004f - j * 0.25f,
                    });
                }
            }
        }

        if (_entityMgr != null)
        {
            foreach (var entity in _entityMgr.Entities)
            {
                if (!entity.IsAlive)
                    continue;

                if (_fog != null && !_fog.IsRevealed(entity.Position.X, entity.Position.Y))
                    continue;

                float dist = entity.Position.DistanceTo(cullCenter);
                if (dist > collectionRadius)
                    continue;

                var entityLight = GetEntityLightProfile(entity);
                _candidates.Add(new LightSourceCandidate
                {
                    Position = entity.Position,
                    Color = entityLight.Color,
                    Intensity = entityLight.Intensity * nightStrength,
                    Radius = entityLight.Radius,
                    FlickerPhase = HashPhase(entity.EntityName, 0),
                    Priority = entityLight.Priority - dist * 0.004f,
                });
            }
        }
    }

    private static Vector2I[] GetPoiOccupiedHexes(OverworldPOI poi)
    {
        if (poi.OccupiedHexes.Length > 0)
            return poi.OccupiedHexes;

        poi.CenterHex = HexOverworldTile.PixelToAxial(poi.Position.X, poi.Position.Y);
        try
        {
            poi.RebuildOccupiedHexes();
        }
        catch (Exception)
        {
            poi.OccupiedHexes = new[] { poi.CenterHex };
        }

        if (poi.OccupiedHexes.Length == 0)
            poi.OccupiedHexes = new[] { poi.CenterHex };

        return poi.OccupiedHexes;
    }

    private static float GetSystemHour()
    {
        var now = DateTime.Now;
        return now.Hour + now.Minute / 60f + now.Second / 3600f;
    }

    private static float NormalizeHour(float hour)
    {
        hour %= 24.0f;
        return hour < 0.0f ? hour + 24.0f : hour;
    }

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp((x - edge0) / (edge1 - edge0), 0.0f, 1.0f);
        return t * t * (3.0f - 2.0f * t);
    }

    private static ImageTexture GetGlowTexture()
    {
        if (_glowTexture != null)
            return _glowTexture;

        var image = Image.CreateEmpty(GlowTextureSize, GlowTextureSize, false, Image.Format.Rgba8);
        float center = (GlowTextureSize - 1) * 0.5f;
        float radius = GlowTextureSize * 0.5f;

        for (int y = 0; y < GlowTextureSize; y++)
        {
            for (int x = 0; x < GlowTextureSize; x++)
            {
                float dist = new Vector2(x - center, y - center).Length();
                float t = Mathf.Clamp(dist / radius, 0.0f, 1.0f);
                float core = MathF.Exp(-t * t * 6.0f);
                float halo = MathF.Pow(1.0f - t, 2.4f);
                float alpha = Mathf.Clamp(Mathf.Max(core * 0.72f, halo), 0.0f, 1.0f);
                image.SetPixel(x, y, new Color(1.0f, 1.0f, 1.0f, alpha));
            }
        }

        _glowTexture = ImageTexture.CreateFromImage(image);
        return _glowTexture;
    }

    private static Material GetGlowMaterial()
    {
        if (_glowMaterial != null)
            return _glowMaterial;

        string shaderPath = "res://BladeHexFrontend/src/assets/shaders/night_glow.gdshader";
        if (ResourceLoader.Exists(shaderPath))
        {
            var shader = ShaderAssetResolver.Load("night_glow", shaderPath);
            _glowMaterial = new ShaderMaterial
            {
                Shader = shader
            };
            GD.Print("[NightLighting] initialized with custom shader: night_glow.gdshader");
        }
        else
        {
            GD.Print("[NightLighting] custom shader not found, falling back to CanvasItemMaterial Add");
            _glowMaterial = new CanvasItemMaterial
            {
                BlendMode = CanvasItemMaterial.BlendModeEnum.Add,
            };
        }
        return _glowMaterial;
    }

    private static Color GetPOILightColor(OverworldPOI poi)
    {
        if (poi.PoiTypeEnum == OverworldPOI.POIType.Town && poi.IsPortCity)
            return LightColorPort;

        return poi.PoiTypeEnum switch
        {
            OverworldPOI.POIType.Town => LightColorTown,
            OverworldPOI.POIType.Village => LightColorVillage,
            OverworldPOI.POIType.Castle => LightColorCastle,
            _ => LightColorTorch,
        };
    }

    private static EntityLightProfile GetEntityLightProfile(OverworldEntity entity)
    {
        // 基础配置
        Color baseColor = entity.EntityTypeEnum switch
        {
            OverworldEntity.EntityType.LordArmy => entity.IsMarshal ? LightColorCastle : LightColorEntity,
            OverworldEntity.EntityType.Caravan => new Color(1.0f, 0.78f, 0.36f),
            OverworldEntity.EntityType.EpicMonster => new Color(0.65f, 0.45f, 0.95f),
            OverworldEntity.EntityType.RaidingParty
                or OverworldEntity.EntityType.BanditParty
                or OverworldEntity.EntityType.RobberParty
                or OverworldEntity.EntityType.PirateCrew => new Color(1.0f, 0.42f, 0.20f),
            _ => LightColorEntity,
        };

        float baseIntensity = entity.EntityTypeEnum switch
        {
            OverworldEntity.EntityType.LordArmy => entity.IsMarshal ? 0.62f : 0.52f,
            OverworldEntity.EntityType.Caravan => 0.50f,
            OverworldEntity.EntityType.EpicMonster => 0.50f,
            OverworldEntity.EntityType.RaidingParty
                or OverworldEntity.EntityType.BanditParty
                or OverworldEntity.EntityType.RobberParty
                or OverworldEntity.EntityType.PirateCrew => 0.50f,
            _ => 0.46f,
        };

        float baseRadius = entity.EntityTypeEnum switch
        {
            OverworldEntity.EntityType.LordArmy => entity.IsMarshal ? 500f : 430f,
            OverworldEntity.EntityType.Caravan => 400f,
            OverworldEntity.EntityType.EpicMonster => 540f,
            OverworldEntity.EntityType.RaidingParty
                or OverworldEntity.EntityType.BanditParty
                or OverworldEntity.EntityType.RobberParty
                or OverworldEntity.EntityType.PirateCrew => 380f,
            _ => 350f,
        };

        float priority = entity.EntityTypeEnum switch
        {
            OverworldEntity.EntityType.LordArmy => entity.IsMarshal ? 730f : 700f,
            OverworldEntity.EntityType.Caravan => 690f,
            OverworldEntity.EntityType.EpicMonster => 680f,
            OverworldEntity.EntityType.RaidingParty
                or OverworldEntity.EntityType.BanditParty
                or OverworldEntity.EntityType.RobberParty
                or OverworldEntity.EntityType.PirateCrew => 670f,
            _ => 650f,
        };

        // 根据部队规模（PartySize）动态计算缩放因子，基本盘以 4 人为基准
        float partySize = Math.Max(1, entity.PartySize);
        float sizeFactor = MathF.Sqrt(partySize / 4.0f);
        sizeFactor = Mathf.Clamp(sizeFactor, 0.6f, 2.5f);

        float finalRadius = baseRadius * sizeFactor;
        // 强度稍微受人数影响，但限制在 [0.3, 0.95] 之间，且低于 POI 常规亮度上限
        float finalIntensity = Mathf.Clamp(baseIntensity * (0.8f + 0.2f * sizeFactor) * 0.80f, 0.25f, 0.75f);

        return new EntityLightProfile(baseColor, finalIntensity, finalRadius, priority);
    }

    private static float HashPhase(string name, int index)
    {
        unchecked
        {
            ulong hash = 5381;
            foreach (char c in name)
                hash = (hash * 33) ^ c;
            hash = (hash * 33) ^ (ulong)index;
            return (float)(hash % 10000) / 10000f * MathF.PI * 2f;
        }
    }

    private struct LightSourceCandidate
    {
        public Vector2 Position;
        public Color Color;
        public float Intensity;
        public float Radius;
        public float FlickerPhase;
        public float Priority;
    }

    private readonly struct EntityLightProfile
    {
        public readonly Color Color;
        public readonly float Intensity;
        public readonly float Radius;
        public readonly float Priority;

        public EntityLightProfile(Color color, float intensity, float radius, float priority)
        {
            Color = color;
            Intensity = intensity;
            Radius = radius;
            Priority = priority;
        }
    }
}
