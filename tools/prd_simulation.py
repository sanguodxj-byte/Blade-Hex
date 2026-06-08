"""
Blade&Hex PRD Monte Carlo Simulation v2
- Fixed PRD C-value computation (binary search at runtime, fast)
"""
import random, math
random.seed(42)

def find_prd_c(target_p, sim_trials=50000):
    """Binary search for PRD C value that yields target_p long-term rate."""
    lo, hi = 0.0, target_p * 2
    for _ in range(40):
        mid = (lo + hi) / 2
        # Quick sim
        counter = 0; procs = 0
        for _ in range(sim_trials):
            counter += 1
            if random.random() < min(1.0, mid * counter):
                procs += 1; counter = 0
        actual = procs / sim_trials
        if actual < target_p: lo = mid
        else: hi = mid
    return (lo + hi) / 2

# Precompute C values
print("Computing PRD C-values...")
random.seed(123)
PRD_C = {}
for p in [0.05, 0.10, 0.15, 0.20]:
    PRD_C[p] = find_prd_c(p)
    print(f"  p={p:.2f} -> C={PRD_C[p]:.6f}")
random.seed(42)

TRIALS = 5000
ATTACKS = 30

print("\n" + "=" * 72)
print("Blade&Hex PRD Monte Carlo Simulation v2")
print(f"Trials: {TRIALS}  |  Attacks per battle: {ATTACKS}")
print("=" * 72)

# === Scenario 1: Attack Roll ===
print("\n[Scenario 1] Attack Roll - Light PRD (advantage after 2 consecutive misses)")
print("-" * 72)

for hit_p in [0.55, 0.70, 0.85]:
    stats = {}
    for label, use_prd in [("No PRD", False), ("With PRD", True)]:
        total_hits = 0; max_miss = 0; battles_3plus = 0
        for _ in range(TRIALS):
            streak = 0; battle_max = 0; has_3plus = False; hits = 0
            for _ in range(ATTACKS):
                roll = random.randint(1, 20)
                if roll == 1:
                    streak += 1
                elif roll == 20:
                    hits += 1; streak = 0
                else:
                    p = hit_p
                    if use_prd and streak >= 2:
                        p = 1 - (1 - p) ** 2
                    if random.random() < p:
                        hits += 1; streak = 0
                    else:
                        streak += 1
                battle_max = max(battle_max, streak)
                if streak >= 3: has_3plus = True
            total_hits += hits
            max_miss = max(max_miss, battle_max)
            if has_3plus: battles_3plus += 1
        stats[label] = (total_hits / TRIALS, max_miss, battles_3plus / TRIALS * 100)

    a, b = stats["No PRD"], stats["With PRD"]
    print(f"\n  Hit chance {hit_p*100:.0f}%:")
    print(f"    No PRD: avg_hits={a[0]:.1f}/{ATTACKS}  max_consec_miss={a[1]}  battles_w/3+miss={a[2]:.1f}%")
    print(f"    PRD:    avg_hits={b[0]:.1f}/{ATTACKS}  max_consec_miss={b[1]}  battles_w/3+miss={b[2]:.1f}%")
    print(f"    Delta:  hits +{b[0]-a[0]:.2f}  3+miss_rate {b[2]-a[2]:+.1f}pp")

# === Scenario 2: Bonus Crit ===
print("\n\n[Scenario 2] Bonus Crit Chance - Dota2-style PRD (corrected C-values)")
print("-" * 72)

for crit_p in [0.05, 0.10, 0.15]:
    c = PRD_C[crit_p]
    hpb = int(ATTACKS * 0.7)  # ~21 hits per battle
    stats = {}
    for label, use_prd in [("No PRD", False), ("With PRD", True)]:
        total_c = 0; total_r = 0; zero_b = 0; mg = 0
        for _ in range(TRIALS):
            counter = 0; bc = 0; gap = 0; bmg = 0
            for _ in range(hpb):
                gap += 1; counter += 1
                prob = min(1.0, c * counter) if use_prd else crit_p
                if random.random() < prob:
                    bc += 1; bmg = max(bmg, gap); gap = 0; counter = 0
            total_c += bc; total_r += hpb
            if bc == 0: zero_b += 1
            mg = max(mg, bmg, gap)
        stats[label] = (total_c/total_r*100, zero_b/TRIALS*100, mg)

    a, b = stats["No PRD"], stats["With PRD"]
    print(f"\n  Target crit {crit_p*100:.0f}% (C={c:.6f}, hits/battle={hpb}):")
    print(f"    No PRD: actual={a[0]:.2f}%  max_gap={a[2]}  zero_crit_battles={a[1]:.1f}%")
    print(f"    PRD:    actual={b[0]:.2f}%  max_gap={b[2]}  zero_crit_battles={b[1]:.1f}%")
    print(f"    Delta:  rate {b[0]-a[0]:+.2f}pp  zero_battles {b[1]-a[1]:+.1f}pp  gap {b[2]-a[2]:+d}")

# === Scenario 3: Penetration ===
print("\n\n[Scenario 3] Armor Penetration - Natural 20 floor (no PRD needed)")
print("-" * 72)
N = 50000
for sb, dr, lbl in [(3,15,"Light"),(5,18,"Plate"),(8,18,"High STR vs Plate")]:
    pen = n20 = 0
    for _ in range(N):
        r = random.randint(1,20)
        if r == 20: pen += 1; n20 += 1
        elif r + sb >= dr: pen += 1
    print(f"  {lbl} (STR+{sb} vs DR{dr}): pen_rate={pen/N*100:.1f}%  nat20_contrib={n20/N*100:.1f}%")

# === Reference: Damage Dice ===
print("\n\n[Reference] Weapon damage dice variance (no PRD needed)")
print("-" * 72)
N = 30000
for cnt, sides, lbl in [(1,6,"1d6"),(2,6,"2d6"),(3,6,"3d6"),(2,8,"2d8"),(3,8,"3d8")]:
    rolls = [sum(random.randint(1,sides) for _ in range(cnt)) for _ in range(N)]
    avg = sum(rolls)/len(rolls)
    mn, mx = cnt, cnt*sides
    print(f"  {lbl}: avg={avg:.1f}  min({mn})_prob={rolls.count(mn)/len(rolls)*100:.2f}%  max({mx})_prob={rolls.count(mx)/len(rolls)*100:.2f}%")

print("\n" + "=" * 72)
print("Simulation complete.")
