# Build 平衡测试报告

> 日期：2026-05-17
> 工具：`tools/scripts/sim.ps1 -Scenario combat_build`
> 矩阵：7 × 7 build × 60 battles/pair = 2940 battles per level
> 矩阵已修复**先手位置偏差**：每对 build 一半 player→enemy 一半 enemy→player

## 测试方法

`SimulationHarness.RunBuildMatrixBatch` — 7 种属性倾向 build 全互掐。每对 (i, j) 跑一半 i 在 player 位、一半 i 在 enemy 位，统计 i 的真实胜率。这样 mirror 对局必然 50%，可暴露真实 build 强度差异。

| ID | Build 名 | 主要属性权重 |
|---|---|---|
| 0 | Warrior | STR 2.5 / CON 1.8 |
| 1 | Ranger | DEX 2.5 / WIS 1.5 |
| 2 | Mage | INT 2.5 / WIS 1.5 |
| 3 | Tank | CON 2.2 / STR/DEX 1.2 |
| 4 | Leader | CHA 2.5 / DEX 1.5 |
| 5 | Sage | WIS 2.5 / INT 1.2 |
| 6 | Duelist | STR/DEX 1.8 / CHA 1.0 |

## 结果

### Lvl 5（早期游戏）

| 排名 | Build | 胜率 |
|---|---|---|
| 1 | Tank    | 55.5% |
| 2 | Duelist | 51.7% |
| 3 | Leader  | 51.0% |
| 4 | Warrior | 50.2% |
| 5 | Ranger  | 50.0% |
| 6 | Sage    | 49.0% |
| 7 | Mage    | 47.9% |

**结论**：早期游戏所有 build 平衡（差距 < 8 个百分点）。装备、等级、属性差距还没拉开。

### Lvl 30（中期）

| 排名 | Build | 胜率 |
|---|---|---|
| 1 | Duelist | 57.1% |
| 2 | Tank    | 56.4% |
| 3 | Sage    | 54.3% |
| 4 | Leader  | 52.6% |
| 5 | Warrior | 51.9% |
| 6 | Ranger  | 40.5% |
| 7 | Mage    | 36.7% ↓ 偏弱 |

**结论**：中期开始**远程系（Ranger/Mage）出现劣势**，差距约 15-20 pp。其他 build 仍在合理区间。

### Lvl 60（中后期）

| 排名 | Build | 胜率 | 标记 |
|---|---|---|---|
| 1 | Tank    | 75.7% | ⚠ OP |
| 2 | Warrior | 69.5% | ↑ 偏强 |
| 3 | Duelist | 60.5% | ↑ 偏强 |
| 4 | Sage    | 55.2% | 平均 |
| 5 | Ranger  | 35.5% | ↓ 偏弱 |
| 6 | Mage    | 27.4% | ⚠ 弱 |
| 7 | Leader  | 23.8% | ⚠ 弱 |

**结论**：中后期**STR+CON 三件套（Tank/Warrior/Duelist）vs 远程/法术/领袖差距拉到 50 个百分点**，明显失衡。

### 关键对位（lvl 60，60 battles 双向）

| 对位 | 强者胜率 | 备注 |
|---|---|---|
| Tank vs Mage | 93% | 法师 1-2 回合被秒 |
| Tank vs Leader | 98% | CHA 几乎无防御 |
| Warrior vs Mage | 92% | 同上 |
| Tank vs Ranger | 90% | 远程被盾兵 +DR 双重压制 |
| Mage vs Tank | 7% | 反向印证 |

## 关键发现

### 1. STR+CON 系（Tank / Warrior / Duelist）lvl 60+ 整体超模

数据：lvl 30 → 51.9-57.1%（合理），lvl 60 → 60.5-75.7%（严重）

**根因分析**：
- v0.6 重型武器骰子：Greataxe 2d10 (avg 11)、Maul 3d8 (avg 13.5)、Greatclub 2d12 (avg 13)，远超远程武器
- 重型武器 Lv.5 精通 暴击 ×1.2 倍数随级别复利累积
- Tank 通过重甲 DR 阈值（板甲 12+）+ 高 CON 双重减伤
- 节点 melee_damage 加成被 AP 归一化（×AP/4）只保护了 2 AP 武器，4-6 AP 武器照旧线性获利
- AI 不会给法师/远程角色读条法术，简化 AI 全部走"上前普攻"

### 2. Mage / Ranger 中后期失速

数据：Mage 47.9% (lvl 5) → 36.7% (lvl 30) → 27.4% (lvl 60)
       Ranger 50% (lvl 5) → 40.5% (lvl 30) → 35.5% (lvl 60)

**多重不利**：
- 远程武器骰子较低：长弓 1d8 (avg 4.5)、重弩 1d12 (avg 6.5) vs 巨剑 3d6 (avg 10.5)
- v0.6 6.2 盾兵远程减免：×0.25-0.5 进一步削弱
- v0.6 10.0 法师装备限制：必须穿布甲（DR ≤ 3）、不能持盾，AC 极低
- AI 不会施法（headless 简化版只用普攻）

### 3. Leader (CHA 系) lvl 60+ 暴跌

数据：51.0% (lvl 5) → 52.6% (lvl 30) → 23.8% (lvl 60)

**根因**：CHA 主属性几乎不直接增加战斗力，依赖：
- 周围友军命中 +1（贴脸才生效）
- 士气曲线（高昂 +2 命中、暴击 +20%）
- 主动技能（指挥 / 集结 / 英雄号召）— headless AI 不会用
- 一旦脱离队友 / 遇到大装备差距，CHA 角色没有应对手段

### 4. Sage (WIS) 中规中矩

数据：49.0% → 54.3% → 55.2%

WIS 暴击曲线（v0.6 4.4: floor(sqrt(WIS-14/4))）提供持续输出，且 WIS 高也带来：
- 暴击阈值降低
- 暴击受伤减免（防御方 WIS 高减少受暴击伤害）
- 法术 DC + 治疗骰子加成

是当前数值下唯一一个**稳定 50% 上下**的 INT/WIS 系 build。

## 平衡建议（按优先级）

### P0: 削弱 STR+CON 优势（影响最大）

1. **重型武器伤害骰下调**
   - Greataxe 2d10 → 1d12+1d8 (avg 11 → 11，方差降低)
   - Maul 3d8 → 2d8+1d6 (avg 13.5 → 12.5)
   - Greatclub 2d12 → 2d10 (avg 13 → 11)

2. **重型武器 Lv.5 暴击 ×1.2 → ×1.1**
   - 复利效果减半，避免 lvl 60+ 暴雪球

3. **节点 melee_damage 对 ≥5 AP 武器再缩减**
   - 当前公式：`bonus * AP / 4`，对 8 AP 重弩反而 ×2 放大
   - 建议改：`bonus * min(AP, 4) / 4`，4 AP 以上不再线性放大

### P1: 加强远程系（lvl 30+ 起步）

4. **远程武器骰子全面上调**
   - 长弓 1d8 → 1d10
   - 重弩 1d12 → 2d6
   - 短弓 1d6 → 1d8
   - 标枪 1d8 → 1d10

5. **远程武器穿透加值上调 +2**
   - 当前 PenBonus 0~+5，提到 +2~+7
   - 让重弩/复合弓能可靠破板甲

6. **盾兵远程减免微调**
   - 塔盾 ×0.25 → ×0.30
   - 步兵盾 ×0.35 → ×0.40
   - 给远程一个反盾的窗口

### P1: AI 智能化（最大提升 Mage/Leader 表现，工程量大）

7. **headless AI 加施法逻辑**
   - 高 INT 角色优先 cast 高伤害 Spell（ArcaneJudgment 3d10、ChainLightning 3d6×3）
   - 低 HP 角色 cast LifeShield / LifeCircle
   - CHA 高角色优先 cast HeroicCall（开战回合）+ Rally

8. **headless AI 装备评估**
   - Mage 让 AI 优先选 INT 高的装备 + Catalyst（魔杖类）
   - Tank 让 AI 优先选板甲 + 塔盾
   - 当前是随机装备，让 build 有"装备完整度"加分

### P2: 节点平衡（影响小）

9. **STR 系节点 nerf**
   - `str_b06 嗜血` 4 AP 池可能再缩到 3 AP
   - `str_ks01 狂暴之力` +50% 伤害可能改 +35%

10. **CHA 系节点 buff**
    - `cha_b03 统帅光环` 命中 +1 / AC +1 → +2 / +1（让 Leader 提供更明显支援）
    - `cha_b10 英雄号召` 持续 3 回合 → 4 回合

### P3: 测试方法（已部分完成）

11. ✅ 双向对位（已实装）
12. **Sim 生成时强制 build 装备倾向**
    - 当前 EquipFullSet 是随机装备，让 Mage 拿到重剑也很常见
    - 给 EquipmentGenerator 加 `preferredAttribute` 参数，让 STR build 优先重型武器、INT build 优先 Catalyst

## 后续 Action

1. ⏸ 先与设计讨论 P0 数值调整方向（不动可能 lvl 60+ 体验非常单调）
2. ⏸ 实装 headless AI 简单决策树（让 Mage/Leader 用上技能后再测）
3. ⏸ EquipmentGenerator 加 build-aware 选择（让生成器尊重 build 主属性）
4. 修完 P0 后重新跑 build matrix 验证收敛到 ±10%

## 复现命令

```powershell
$env:GODOT="C:\Users\Administrator\Desktop\Godot_v4.6.2-stable_mono_win64_console.exe"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "d:\123\Blade&Hex\tools\scripts\sim.ps1" `
    -SkipBuild -Battles 60 -Seed 42 -Level 30 -Scenario combat_build
```

`-Battles N` = 每对 build 跑 N 局（双向各 N/2 局）。`-Level` 决定单位等级。Seed 固定可复现。
