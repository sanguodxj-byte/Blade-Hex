using BladeHex.Data;
using BladeHex.View.AssetSystem;
using Godot;
using System.Collections.Generic;

namespace BladeHex.View.Unit;

public static class CreatureTextureConfig
{
    public const string SpriteBasePath = "res://assets/legendary_sprites";
    public const int PlaceholderW = 128;
    public const int PlaceholderH = 160;

    private static readonly Dictionary<UnitData.EnemyType, Color> TypeColors = new()
    {
        { UnitData.EnemyType.Beast, new Color(0.55f, 0.70f, 0.30f) },
        { UnitData.EnemyType.Undead, new Color(0.50f, 0.55f, 0.70f) },
        { UnitData.EnemyType.Demon, new Color(0.80f, 0.25f, 0.25f) },
        { UnitData.EnemyType.Dragon, new Color(0.85f, 0.55f, 0.15f) },
        { UnitData.EnemyType.Giant, new Color(0.65f, 0.50f, 0.35f) },
        { UnitData.EnemyType.Construct, new Color(0.55f, 0.55f, 0.60f) },
        { UnitData.EnemyType.Legendary, new Color(0.75f, 0.40f, 0.85f) },
        { UnitData.EnemyType.Humanoid, new Color(0.40f, 0.70f, 1.00f) },
    };

    private static readonly Dictionary<string, Texture2D> PlaceholderCache = new();
    private static readonly Dictionary<string, Texture2D?> SpriteCache = new();

    public static bool IsCreature(UnitData data)
    {
        return data != null && data.enemyType != UnitData.EnemyType.Humanoid;
    }

    public static bool IsCreature(UnitData.EnemyType type)
    {
        return type != UnitData.EnemyType.Humanoid;
    }

    public static Texture2D? TryLoadSprite(UnitData data)
    {
        if (data == null)
            return null;

        string templateId = data.EnemyTemplateId;
        if (string.IsNullOrWhiteSpace(templateId))
            return null;

        if (SpriteCache.TryGetValue(templateId, out var cached))
            return cached;

        string fallbackPath = GetExpectedSpritePath(data);
        bool isLegendary = IsLegendaryCreature(data);
        var texture = isLegendary
            ? LoadLegendarySprite(templateId, fallbackPath)
            : TextureAssetResolver.LoadUnitSprite(templateId, fallbackPath);
        if (!isLegendary)
            texture = CharacterTextureNormalizer.Normalize(texture);
        SpriteCache[templateId] = texture;

        if (texture != null)
            GD.Print($"[CreatureTextureConfig] Loaded creature sprite: {data.UnitName} -> {templateId}");
        else
            GD.Print($"[CreatureTextureConfig] Missing creature sprite: {fallbackPath}; using placeholder.");

        return texture;
    }

    private static Texture2D? LoadLegendarySprite(string templateId, string fallbackPath)
    {
        if (AssetCatalog.TryGet(AssetKind.UnitSprite, templateId, out var catalogEntry))
        {
            var catalogTexture = TextureAssetResolver.LoadPath(catalogEntry.Path);
            if (catalogTexture != null)
                return catalogTexture;
        }

        return TextureAssetResolver.LoadPath(fallbackPath);
    }

    public static string GetExpectedSpritePath(UnitData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.EnemyTemplateId))
            return string.Empty;

        return $"{SpriteBasePath}/{data.EnemyTemplateId}.png";
    }

    public static bool IsLegendaryCreature(UnitData data)
    {
        if (data == null)
            return false;

        return data.enemyType == UnitData.EnemyType.Legendary
            || data.LegendaryResistanceUses > 0
            || data.LegendaryActionPoints > 0
            || data.LegendaryActions.Count > 0;
    }

    public static void ClearSpriteCache()
    {
        SpriteCache.Clear();
        PlaceholderCache.Clear();
        CharacterTextureNormalizer.ClearCache();
    }

    public static Texture2D GeneratePlaceholder(UnitData data)
    {
        string key = data?.EnemyTemplateId ?? data?.enemyType.ToString() ?? "unknown";
        if (PlaceholderCache.TryGetValue(key, out var cached))
            return cached;

        var typeColor = GetTypeColor(data);
        var image = Image.CreateEmpty(PlaceholderW, PlaceholderH, false, Image.Format.Rgba8);

        for (int y = 0; y < PlaceholderH; y++)
        {
            for (int x = 0; x < PlaceholderW; x++)
                image.SetPixel(x, y, Colors.Transparent);
        }

        var main = typeColor;
        var dark = typeColor * 0.55f;
        var light = typeColor * 1.3f;
        int centerX = PlaceholderW / 2;

        switch (data?.enemyType ?? UnitData.EnemyType.Humanoid)
        {
            case UnitData.EnemyType.Beast:
                DrawBeastSilhouette(image, centerX, main, dark, light);
                break;
            case UnitData.EnemyType.Undead:
                DrawUndeadSilhouette(image, centerX, main, dark, light);
                break;
            case UnitData.EnemyType.Demon:
                DrawDemonSilhouette(image, centerX, main, dark, light);
                break;
            case UnitData.EnemyType.Dragon:
                DrawDragonSilhouette(image, centerX, main, dark, light);
                break;
            case UnitData.EnemyType.Giant:
                DrawGiantSilhouette(image, centerX, main, dark, light);
                break;
            case UnitData.EnemyType.Construct:
                DrawConstructSilhouette(image, centerX, main, dark, light);
                break;
            case UnitData.EnemyType.Legendary:
                DrawLegendarySilhouette(image, centerX, main, dark, light);
                break;
            default:
                DrawBeastSilhouette(image, centerX, main, dark, light);
                break;
        }

        var texture = ImageTexture.CreateFromImage(image);
        PlaceholderCache[key] = texture;
        return texture;
    }

    public static Color GetTypeColor(UnitData? data)
    {
        if (data == null)
            return TypeColors[UnitData.EnemyType.Humanoid];

        return TypeColors.TryGetValue(data.enemyType, out var color)
            ? color
            : TypeColors[UnitData.EnemyType.Humanoid];
    }

    public static string GetTypeName(UnitData.EnemyType type)
    {
        return type switch
        {
            UnitData.EnemyType.Beast => "Beast",
            UnitData.EnemyType.Undead => "Undead",
            UnitData.EnemyType.Demon => "Demon",
            UnitData.EnemyType.Dragon => "Dragon",
            UnitData.EnemyType.Giant => "Giant",
            UnitData.EnemyType.Construct => "Construct",
            UnitData.EnemyType.Legendary => "Legendary",
            _ => "Humanoid",
        };
    }

    private static void DrawBeastSilhouette(Image image, int centerX, Color main, Color dark, Color light)
    {
        FillEllipse(image, centerX, 80, 42, 26, main);
        FillEllipse(image, centerX + 30, 58, 18, 16, main);
        FillTriangle(image, centerX + 38, 42, centerX + 44, 28, centerX + 48, 44, dark);
        FillTriangle(image, centerX + 26, 44, centerX + 20, 30, centerX + 16, 46, dark);
        FillRect(image, centerX - 28, 100, 8, 40, dark);
        FillRect(image, centerX - 10, 100, 8, 40, dark);
        FillRect(image, centerX + 12, 100, 8, 40, dark);
        FillRect(image, centerX + 28, 100, 8, 40, dark);
        DrawCurve(image, centerX - 40, 74, centerX - 60, 50, 4, dark);
        SetPixel(image, centerX + 38, 54, light);
        SetPixel(image, centerX + 42, 54, light);
    }

    private static void DrawUndeadSilhouette(Image image, int centerX, Color main, Color dark, Color light)
    {
        FillEllipse(image, centerX, 36, 20, 18, main);
        FillEllipse(image, centerX - 7, 34, 4, 5, dark);
        FillEllipse(image, centerX + 7, 34, 4, 5, dark);

        for (int y = 54; y <= 130; y++)
        {
            float t = (float)(y - 54) / 76;
            int half = (int)(16 + t * 28);
            float alpha = 1.0f - t * 0.6f;
            var color = new Color(main.R, main.G, main.B, alpha);
            for (int x = centerX - half; x <= centerX + half; x++)
                SetPixel(image, x, y, color);
        }

        for (int x = centerX - 44; x <= centerX + 44; x += 6)
        {
            int height = 130 + (x % 12 == 0 ? 12 : 6);
            FillRect(image, x, 130, 4, height - 130, new Color(main.R, main.G, main.B, 0.3f));
        }

        DrawThickLine(image, centerX - 20, 80, centerX - 40, 95, dark, 3);
        DrawThickLine(image, centerX + 20, 80, centerX + 40, 95, dark, 3);
    }

    private static void DrawDemonSilhouette(Image image, int centerX, Color main, Color dark, Color light)
    {
        FillEllipse(image, centerX, 80, 30, 36, main);
        FillEllipse(image, centerX, 36, 16, 14, main);
        FillTriangle(image, centerX - 14, 26, centerX - 22, 6, centerX - 8, 22, dark);
        FillTriangle(image, centerX + 14, 26, centerX + 22, 6, centerX + 8, 22, dark);
        FillTriangle(image, centerX - 28, 60, centerX - 62, 30, centerX - 50, 90, dark);
        FillTriangle(image, centerX + 28, 60, centerX + 62, 30, centerX + 50, 90, dark);
        FillRect(image, centerX - 14, 112, 10, 34, dark);
        FillRect(image, centerX + 4, 112, 10, 34, dark);
        DrawCurve(image, centerX + 24, 100, centerX + 54, 120, 3, dark);
        FillTriangle(image, centerX + 52, 118, centerX + 60, 112, centerX + 56, 126, light);
        SetPixel(image, centerX - 6, 34, light);
        SetPixel(image, centerX + 6, 34, light);
    }

    private static void DrawDragonSilhouette(Image image, int centerX, Color main, Color dark, Color light)
    {
        FillEllipse(image, centerX, 90, 36, 28, main);
        DrawThickLine(image, centerX + 20, 70, centerX + 36, 36, main, 10);
        FillEllipse(image, centerX + 40, 30, 14, 10, main);
        DrawThickLine(image, centerX + 50, 28, centerX + 60, 26, dark, 3);
        FillTriangle(image, centerX - 10, 66, centerX - 56, 16, centerX - 44, 80, dark);
        FillTriangle(image, centerX + 10, 66, centerX + 56, 16, centerX + 44, 80, dark);
        FillRect(image, centerX - 22, 112, 12, 32, dark);
        FillRect(image, centerX + 10, 112, 12, 32, dark);
        DrawCurve(image, centerX - 34, 94, centerX - 58, 130, 5, dark);
        SetPixel(image, centerX + 44, 26, light);
    }

    private static void DrawGiantSilhouette(Image image, int centerX, Color main, Color dark, Color light)
    {
        FillEllipse(image, centerX, 24, 14, 12, main);
        FillEllipse(image, centerX, 72, 36, 40, main);
        DrawThickLine(image, centerX - 34, 52, centerX - 52, 100, dark, 8);
        DrawThickLine(image, centerX + 34, 52, centerX + 52, 100, dark, 8);
        FillRect(image, centerX - 20, 108, 16, 42, dark);
        FillRect(image, centerX + 4, 108, 16, 42, dark);
        SetPixel(image, centerX - 5, 22, light);
        SetPixel(image, centerX + 5, 22, light);
    }

    private static void DrawConstructSilhouette(Image image, int centerX, Color main, Color dark, Color light)
    {
        FillRect(image, centerX - 14, 14, 28, 22, main);
        FillRect(image, centerX - 10, 22, 20, 4, light);
        FillRect(image, centerX - 26, 40, 52, 56, main);
        SetPixel(image, centerX - 20, 44, dark);
        SetPixel(image, centerX + 20, 44, dark);
        SetPixel(image, centerX - 20, 88, dark);
        SetPixel(image, centerX + 20, 88, dark);
        FillRect(image, centerX - 42, 44, 12, 44, dark);
        FillRect(image, centerX + 30, 44, 12, 44, dark);
        FillRect(image, centerX - 20, 100, 14, 48, dark);
        FillRect(image, centerX + 6, 100, 14, 48, dark);
        FillEllipse(image, centerX, 66, 8, 8, light);
    }

    private static void DrawLegendarySilhouette(Image image, int centerX, Color main, Color dark, Color light)
    {
        DrawDemonSilhouette(image, centerX, main, dark, light);

        for (int angle = 0; angle < 360; angle += 8)
        {
            float radians = angle * Mathf.Pi / 180.0f;
            int x = centerX + (int)(50 * Mathf.Cos(radians));
            int y = 80 + (int)(60 * Mathf.Sin(radians));
            SetPixel(image, x, y, new Color(light.R, light.G, light.B, 0.5f));
            SetPixel(image, x + 1, y, new Color(light.R, light.G, light.B, 0.3f));
        }
    }

    private static void SetPixel(Image image, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= image.GetWidth() || y >= image.GetHeight())
            return;

        image.SetPixel(x, y, color);
    }

    private static void FillEllipse(Image image, int centerX, int centerY, int radiusX, int radiusY, Color color)
    {
        if (radiusX <= 0 || radiusY <= 0)
            return;

        float radiusX2 = radiusX * radiusX;
        float radiusY2 = radiusY * radiusY;
        for (int y = centerY - radiusY; y <= centerY + radiusY; y++)
        {
            for (int x = centerX - radiusX; x <= centerX + radiusX; x++)
            {
                float dx = x - centerX;
                float dy = y - centerY;
                if (dx * dx / radiusX2 + dy * dy / radiusY2 <= 1.0f)
                    SetPixel(image, x, y, color);
            }
        }
    }

    private static void FillRect(Image image, int x, int y, int width, int height, Color color)
    {
        for (int currentY = y; currentY < y + height; currentY++)
        {
            for (int currentX = x; currentX < x + width; currentX++)
                SetPixel(image, currentX, currentY, color);
        }
    }

    private static void FillTriangle(Image image, int x1, int y1, int x2, int y2, int x3, int y3, Color color)
    {
        int minY = Mathf.Max(0, Mathf.Min(y1, Mathf.Min(y2, y3)));
        int maxY = Mathf.Min(image.GetHeight() - 1, Mathf.Max(y1, Mathf.Max(y2, y3)));
        int minX = Mathf.Max(0, Mathf.Min(x1, Mathf.Min(x2, x3)));
        int maxX = Mathf.Min(image.GetWidth() - 1, Mathf.Max(x1, Mathf.Max(x2, x3)));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (PointInTriangle(x, y, x1, y1, x2, y2, x3, y3))
                    SetPixel(image, x, y, color);
            }
        }
    }

    private static bool PointInTriangle(int px, int py, int x1, int y1, int x2, int y2, int x3, int y3)
    {
        int d1 = Sign(px, py, x1, y1, x2, y2);
        int d2 = Sign(px, py, x2, y2, x3, y3);
        int d3 = Sign(px, py, x3, y3, x1, y1);
        bool hasNegative = d1 < 0 || d2 < 0 || d3 < 0;
        bool hasPositive = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNegative && hasPositive);
    }

    private static int Sign(int px, int py, int x1, int y1, int x2, int y2)
    {
        return (px - x2) * (y1 - y2) - (x1 - x2) * (py - y2);
    }

    private static void DrawThickLine(Image image, int x1, int y1, int x2, int y2, Color color, int thickness)
    {
        int dx = Mathf.Abs(x2 - x1);
        int dy = -Mathf.Abs(y2 - y1);
        int sx = x1 < x2 ? 1 : -1;
        int sy = y1 < y2 ? 1 : -1;
        int err = dx + dy;
        int x = x1;
        int y = y1;
        int half = thickness / 2;

        while (true)
        {
            for (int tx = -half; tx <= thickness - half - 1; tx++)
            {
                for (int ty = -half; ty <= thickness - half - 1; ty++)
                    SetPixel(image, x + tx, y + ty, color);
            }

            if (x == x2 && y == y2)
                break;

            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x += sx;
            }

            if (e2 <= dx)
            {
                err += dx;
                y += sy;
            }
        }
    }

    private static void DrawCurve(Image image, int x1, int y1, int x2, int y2, int thickness, Color color)
    {
        int controlX = (x1 + x2) / 2;
        int controlY = Mathf.Min(y1, y2) - 20;
        const int steps = 32;

        for (int i = 0; i < steps; i++)
        {
            float t = (float)i / steps;
            float u = 1 - t;
            int x = (int)(u * u * x1 + 2 * u * t * controlX + t * t * x2);
            int y = (int)(u * u * y1 + 2 * u * t * controlY + t * t * y2);
            for (int dx = -thickness / 2; dx <= thickness / 2; dx++)
            {
                for (int dy = -thickness / 2; dy <= thickness / 2; dy++)
                    SetPixel(image, x + dx, y + dy, color);
            }
        }
    }
}
