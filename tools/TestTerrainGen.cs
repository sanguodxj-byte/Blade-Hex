// Standalone terrain generation test — compile and run with: 
// dotnet run --project tools/TestTerrainGen.csproj
// Or: csc tools/TestTerrainGen.cs && tools/TestTerrainGen.exe
using System;
using System.Collections.Generic;

class Program
{
    static float PseudoNoise(float x, float y, int seed)
    {
        float v = (float)(Math.Sin(x * 0.1f + seed * 0.7f) * Math.Cos(y * 0.13f + seed * 0.3f)
            + Math.Sin(x * 0.05f + y * 0.07f + seed) * 0.5f
            + Math.Sin(x * 0.02f - y * 0.03f + seed * 1.3f) * 0.25f);
        return (v / 1.75f + 1.0f) * 0.5f;
    }

    static float RidgeNoise(float x, float y, int seed)
    {
        float v = PseudoNoise(x * 1.3f, y * 1.3f, seed + 500);
        v = Math.Abs(v * 2.0f - 1.0f);
        return v * v;
    }

    static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0.0f, 1.0f);
        return t * t * (3.0f - 2.0f * t);
    }

    static void TestSeed(int seed)
    {
        int worldW = 80, worldH = 50;
        float seedVar = ((seed & 0xFF) / 255.0f) * 0.15f;
        float fadeStart = 0.55f + seedVar;

        char[,] map = new char[worldH, worldW];
        int landCount = 0, waterCount = 0, mountainCount = 0;

        for (int r = 0; r < worldH; r++)
        {
            for (int q = 0; q < worldW; q++)
            {
                float baseElev = PseudoNoise(q, r, seed);
                float ridge = RidgeNoise(q, r, seed);
                float elev = baseElev * 0.7f + ridge * 0.3f;

                // Warp
                float warpScale = Math.Max(worldW, worldH) * 0.12f;
                float warpX = (PseudoNoise(q * 0.5f, r * 0.5f, seed + 3000) * 2.0f - 1.0f) * warpScale;
                float warpY = (PseudoNoise(q * 0.5f + 200, r * 0.5f + 200, seed + 3000) * 2.0f - 1.0f) * warpScale;
                float wnx = (q + warpX) / worldW * 2.0f - 1.0f;
                float wny = (r + warpY) / worldH * 2.0f - 1.0f;
                float wdist = wnx * wnx + wny * wny;
                float edgeFalloff = 1.0f - SmoothStep(fadeStart, 1.1f, wdist);

                // Inland sea
                float seaCarve = PseudoNoise(q * 0.25f + 500, r * 0.25f + 500, seed + 3000);
                float carveStrength = Math.Clamp((seaCarve - 0.78f) * 4.5f, 0.0f, 1.0f);
                bool isCarved = edgeFalloff > 0.7f && carveStrength > 0.0f;

                if (isCarved)
                    elev = elev * edgeFalloff - carveStrength * 0.3f;
                else
                {
                    float inlandFloor = edgeFalloff > 0.5f ? 0.32f : 0.0f;
                    elev = Math.Max(elev * edgeFalloff, inlandFloor * edgeFalloff);
                }
                elev = Math.Clamp(elev, 0.0f, 1.0f);

                char c;
                if (elev < 0.22f) { c = '~'; waterCount++; }
                else if (elev < 0.27f) { c = '.'; waterCount++; }
                else if (elev < 0.30f) { c = ','; landCount++; }
                else if (elev > 0.78f) { c = '^'; mountainCount++; }
                else if (elev > 0.65f) { c = 'n'; landCount++; }
                else { c = '#'; landCount++; }

                map[r, q] = c;
            }
        }

        // Print
        Console.WriteLine($"\n{'='} Seed={seed} fadeStart={fadeStart:F2} {'='}");
        for (int r = 0; r < worldH; r++)
        {
            for (int q = 0; q < worldW; q++)
                Console.Write(map[r, q]);
            Console.WriteLine();
        }

        // Connectivity BFS
        int totalLand = landCount + mountainCount;
        int startQ = worldW / 2, startR = worldH / 2;
        // Find land near center
        for (int dr = -5; dr <= 5; dr++)
            for (int dq = -5; dq <= 5; dq++)
            {
                int tr = startR + dr, tq = startQ + dq;
                if (tr >= 0 && tr < worldH && tq >= 0 && tq < worldW && map[tr, tq] != '~' && map[tr, tq] != '.')
                { startR = tr; startQ = tq; goto found; }
            }
        found:

        var visited = new bool[worldH, worldW];
        var queue = new Queue<(int, int)>();
        queue.Enqueue((startQ, startR));
        visited[startR, startQ] = true;
        int reachable = 0;

        while (queue.Count > 0)
        {
            var (cq, cr) = queue.Dequeue();
            reachable++;
            int[] dqs = { 1, -1, 0, 0, 1, -1 };
            int[] drs = { 0, 0, 1, -1, 1, -1 };
            for (int d = 0; d < 6; d++)
            {
                int nq = cq + dqs[d], nr = cr + drs[d];
                if (nq < 0 || nq >= worldW || nr < 0 || nr >= worldH) continue;
                if (visited[nr, nq]) continue;
                if (map[nr, nq] == '~' || map[nr, nq] == '.') continue;
                visited[nr, nq] = true;
                queue.Enqueue((nq, nr));
            }
        }

        float pct = reachable * 100.0f / Math.Max(1, totalLand);
        Console.WriteLine($"Land={totalLand} Water={waterCount} Mtn={mountainCount} | Reachable={reachable}/{totalLand} ({pct:F0}%) {(pct >= 90 ? "OK" : "WARN")}");
    }

    static void Main()
    {
        int[] seeds = { 12345, 67890, 11111, 99999, 42, 777, 2024, 31415 };
        foreach (int s in seeds)
            TestSeed(s);
    }
}
