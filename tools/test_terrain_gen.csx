// 模拟地形生成并输出 ASCII 地图
// 用 dotnet-script 运行: dotnet script tools/test_terrain_gen.csx
// 或直接用 node/python 模拟噪声逻辑

using System;

// 简化的噪声模拟（用 sin 组合近似 Simplex FBM）
static float PseudoNoise(float x, float y, int seed)
{
    float v = (float)(Math.Sin(x * 0.1f + seed * 0.7f) * Math.Cos(y * 0.13f + seed * 0.3f)
        + Math.Sin(x * 0.05f + y * 0.07f + seed) * 0.5f
        + Math.Sin(x * 0.02f - y * 0.03f + seed * 1.3f) * 0.25f);
    return (v / 1.75f + 1.0f) * 0.5f; // normalize to [0,1]
}

static float RidgeNoise(float x, float y, int seed)
{
    float v = PseudoNoise(x * 1.3f, y * 1.3f, seed + 500);
    v = Math.Abs(v * 2.0f - 1.0f); // ridge
    return v * v; // sharpen
}

int worldW = 80, worldH = 60;
int seed = 12345;
float seedVar = ((seed & 0xFF) / 255.0f) * 0.15f;
float fadeStart = 0.55f + seedVar;

char[,] map = new char[worldH, worldW];
int landCount = 0, waterCount = 0, mountainCount = 0, inlandSeaCount = 0;

for (int r = 0; r < worldH; r++)
{
    for (int q = 0; q < worldW; q++)
    {
        // 1. Elevation
        float baseElev = PseudoNoise(q, r, seed);
        float ridge = RidgeNoise(q, r, seed);
        float elev = baseElev * 0.7f + ridge * 0.3f;

        // 2. Edge falloff (ellipse)
        float nx = (float)q / worldW * 2.0f - 1.0f;
        float ny = (float)r / worldH * 2.0f - 1.0f;
        float dist = nx * nx + ny * ny;
        float edgeFalloff = 1.0f - SmoothStep(fadeStart, 1.1f, dist);

        // 3. Domain warp (simplified)
        float warpScale = Math.Max(worldW, worldH) * 0.12f;
        float warpX = PseudoNoise(q * 0.5f, r * 0.5f, seed + 3000) * 2.0f - 1.0f;
        float warpY = PseudoNoise(q * 0.5f + 200, r * 0.5f + 200, seed + 3000) * 2.0f - 1.0f;
        // Recalc with warp
        float wq = q + warpX * warpScale;
        float wr = r + warpY * warpScale;
        float wnx = wq / worldW * 2.0f - 1.0f;
        float wny = wr / worldH * 2.0f - 1.0f;
        float wdist = wnx * wnx + wny * wny;
        edgeFalloff = 1.0f - SmoothStep(fadeStart, 1.1f, wdist);

        // 4. Inland sea carve
        float seaCarve = PseudoNoise(q * 0.25f + 500, r * 0.25f + 500, seed + 3000);
        float carveStrength = Math.Clamp((seaCarve - 0.78f) * 4.5f, 0.0f, 1.0f);
        bool isCarved = edgeFalloff > 0.7f && carveStrength > 0.0f;

        if (isCarved)
        {
            elev = elev * edgeFalloff - carveStrength * 0.3f;
        }
        else
        {
            float inlandFloor = edgeFalloff > 0.5f ? 0.32f : 0.0f;
            elev = Math.Max(elev * edgeFalloff, inlandFloor * edgeFalloff);
        }
        elev = Math.Clamp(elev, 0.0f, 1.0f);

        // 5. Classify
        char c;
        if (elev < 0.22f) { c = '~'; waterCount++; } // deep water
        else if (elev < 0.27f) { c = '.'; waterCount++; } // shallow
        else if (elev < 0.30f) { c = ','; landCount++; } // beach/sand
        else if (elev > 0.78f) { c = '^'; mountainCount++; } // mountain
        else if (elev > 0.65f) { c = 'n'; landCount++; } // hills
        else { c = '#'; landCount++; } // land

        if (isCarved && elev < 0.27f) inlandSeaCount++;

        map[r, q] = c;
    }
}

// Output
Console.WriteLine($"=== Terrain Gen Test (seed={seed}, {worldW}x{worldH}) ===");
Console.WriteLine($"Land: {landCount}, Water: {waterCount}, Mountain: {mountainCount}, InlandSea: {inlandSeaCount}");
Console.WriteLine($"Land%: {landCount * 100.0 / (worldW * worldH):F1}%");
Console.WriteLine();

for (int r = 0; r < worldH; r++)
{
    for (int q = 0; q < worldW; q++)
        Console.Write(map[r, q]);
    Console.WriteLine();
}

// Check connectivity (BFS from center)
var visited = new bool[worldH, worldW];
var queue = new Queue<(int, int)>();
int startQ = worldW / 2, startR = worldH / 2;
if (map[startR, startQ] == '~' || map[startR, startQ] == '.')
{
    // Find nearest land
    for (int dr = 0; dr < 10; dr++)
        for (int dq = 0; dq < 10; dq++)
            if (startR + dr < worldH && startQ + dq < worldW && map[startR + dr, startQ + dq] != '~' && map[startR + dr, startQ + dq] != '.')
            { startR += dr; startQ += dq; goto found; }
    found:;
}

queue.Enqueue((startQ, startR));
visited[startR, startQ] = true;
int reachable = 0;
int[] dqs = { 1, -1, 0, 0 };
int[] drs = { 0, 0, 1, -1 };

while (queue.Count > 0)
{
    var (cq, cr) = queue.Dequeue();
    reachable++;
    for (int d = 0; d < 4; d++)
    {
        int nq = cq + dqs[d], nr = cr + drs[d];
        if (nq < 0 || nq >= worldW || nr < 0 || nr >= worldH) continue;
        if (visited[nr, nq]) continue;
        if (map[nr, nq] == '~' || map[nr, nq] == '.') continue;
        visited[nr, nq] = true;
        queue.Enqueue((nq, nr));
    }
}

int totalLand = landCount + mountainCount;
Console.WriteLine($"\n=== Connectivity Check ===");
Console.WriteLine($"Reachable from center: {reachable}/{totalLand} ({reachable * 100.0 / Math.Max(1, totalLand):F1}%)");
Console.WriteLine(reachable >= totalLand * 0.9 ? "✓ 大陆连通性良好" : "✗ 警告：大陆可能被切断！");

static float SmoothStep(float edge0, float edge1, float x)
{
    float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0.0f, 1.0f);
    return t * t * (3.0f - 2.0f * t);
}
