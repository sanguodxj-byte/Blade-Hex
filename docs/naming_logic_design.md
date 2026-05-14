# 角色与势力命名逻辑设计方案 (Bilingual Naming Logic Design)

## 1. 核心设计原则
*   **种族辨识度**：通过发音规律和字形偏好区分不同种族。
*   **阶层区分**：领主（Lords）拥有家族姓氏和头衔；冒险者（Adventurers）通常只有名，或带有绰号/职业前缀。
*   **中英双语适配**：
    *   **英文**：采用古典/中世纪西幻风格词根。
    *   **中文**：采用“信、达、雅”的音译或意译，避免廉价感，保持严肃奇幻基调。

---

## 2. 人类：艾尔特兰王国 (Eitran Kingdom)
**文化基调**：封建、英法中世纪融合风格。

### 2.1 领主/贵族 (Lords)
*   **命名结构**：[名] · [家族姓] ([Name] [House Name])
*   **名 (Given Names)**：侧重严谨、威严。
    *   *男*：Alaric, Cedric, Edward, Roland, Valerius. (中文：阿拉里克, 塞德里克, 爱德华, 罗兰, 瓦勒留)
    *   *女*：Eleanor, Beatrice, Isolde, Rowena, Gwendolyn. (中文：埃莉诺, 碧翠丝, 伊索尔德, 罗薇娜, 关德琳)
*   **姓 (Surnames/House Names)**：侧重领地特征或古老家族词根。
    *   *词根*：-ton, -ford, -field, -mont, -burg.
    *   *示例*：Blackwood, Ashmore, Redcliff, Valeront. (中文：布莱克伍德/黑林, 阿什莫尔/灰山, 瑞德克里夫/红岩, 瓦勒隆)

### 2.2 冒险者/平民 (Adventurers)
*   **命名结构**：[名] 或 [名] + [绰号/职业]
*   **名**：更短促、口语化。
    *   *示例*：Finn, Jax, Toby, Silas, Mira. (中文：芬恩, 贾克斯, 托比, 塞拉斯, 米拉)
*   **绰号后缀**：
    *   *示例*：Finn the Swift (疾风芬恩), Silas of Riverbend (河湾镇的塞拉斯).

---

## 3. 精灵：银叶王庭 (Silverleaf Court)
**文化基调**：优雅、古老、与自然/天体相关。

### 3.1 领主/长者 (Lords/Elders)
*   **命名结构**：[名] · [流派/星辰姓]
*   **发音特点**：多音节，柔和元音，常用 L, R, S, N。
*   **名**：
    *   *示例*：Thalathas, Elenariel, Sylvaron, Valindra. (中文：萨拉萨斯, 埃伦娜莉, 赛瓦隆, 瓦琳卓)
*   **姓 (House Names)**：语义与光、叶、月相关。
    *   *示例*：Moonwhisper, Starflower, Silverdew, Leafborn. (中文：月语, 星花, 银露, 叶生)

---

## 4. 矮人：铁炉堡联盟 (Ironforge Alliance)
**文化基调**：厚重、刚硬、与矿石/锻造相关。

### 4.1 领主/族长 (Lords/Thanes)
*   **命名结构**：[名] · [氏族姓]
*   **发音特点**：短促、多重辅音（B, D, G, K, T）。
*   **名**：
    *   *示例*：Balgruuf, Thrain, Durin, Grog, Helga. (中文：巴尔古夫, 索恩, 都灵, 格罗格, 赫尔加)
*   **姓 (Clan Names)**：硬核物化。
    *   *示例*：Ironfoot, Stonefist, Deepforge, Copperbeard. (中文：铁足, 石拳, 深锻, 铜须)

---

## 5. 半兽人：赤铜部落 (Copper Tribe)
**文化基调**：原始、狂野、强调力量。

### 5.1 酋长/英雄 (Warchiefs/Heroes)
*   **命名结构**：[名] · [战功绰号]
*   **名**：
    *   *示例*：Grom, Azog, Garosh, Mok, Krog. (中文：格罗姆, 阿佐格, 加尔鲁什, 莫克, 克罗格)
*   **绰号 (Epithets)**：暴力描述。
    *   *示例*：Skullcrusher, Bloodfang, Man-slayer. (中文：碎颅者, 血牙, 屠夫)

---

## 6. 国家与地理命名 (Political Entities)
*   **王国 (Kingdoms)**：[创始人/家族名] + [Land/ia/Domain]
    *   *Eitran* (艾尔特兰): 古语词根“Eit” (永恒) + “Ran” (土地).
*   **村庄 (Villages)**：[地理特征] + [村/镇]
    *   *Riverbend* (河湾镇): 位于河流拐弯处。
    *   *Greencreek* (绿溪村): 靠近银溪。
*   **要塞 (Fortresses)**：[功能] + [石/堡]
    *   *Ironhammer* (铁锤堡): 矮人风格或重兵把守。
    *   *Stoneguard* (磐石要塞).

---

## 7. 程序实现逻辑 (Implementation logic)
```json
{
  "Race": "Human",
  "Role": "Lord",
  "Culture": "Eitran",
  "Result": {
    "EN": "Lord Alaric Ashmore",
    "ZH": "阿拉里克·阿什莫尔 领主"
  }
}
```
*   **生成器算法**：
    1.  根据种族和文化选择对应的**前缀池** (Prefixes) 和 **后缀池** (Suffixes)。
    2.  根据地位 (Status) 决定是否添加**姓氏**或**头衔**。
    ## 8. 高级角色称号生成逻辑 (Level 5+ Epithets)

    当角色达到5级或以上时，将获得一个反映其战斗风格、种族背景或成就的称号（Epithet）。称号采用“名词+名词”或“名词+动词”的组合方式。

    ### 8.1 生成结构与呈现格式
    *   **呈现格式**：[称号] + [空格] + [名] (Title First, Space Separated)
        *   *示例 (EN)*：Stormrage Onar, Windrunner Valindra
        *   *示例 (ZH)*：怒风 奥纳尔, 风行者 瓦琳卓
    *   **逻辑分类**：
        *   **结构 A (内敛型)**：[自然元素/核心特质] + [动作/载体]
            *   *示例*：Storm-rage (怒风), Wind-runner (风行者)

    *   **结构 B (霸气型)**：[战斗后果/遗迹] + [武器/肢体]
        *   *示例*：War-song (战争咆哮/战歌), Skull-crusher (碎颅者)
    *   **结构 C (抽象型)**：[颜色/状态] + [灵魂/核心]
        *   *示例*：Shadow-step (影步), Iron-will (铁志)

    ### 8.2 种族偏好称号池 (Bilingual Title Pools)

    #### 人类：艾尔特兰王国 (荣誉与正义)
    | 英文组合 (Prefix + Suffix) | 中文译名 | 风格 |
    |----------------------------|----------|------|
    | Lion-heart                 | 狮心     | 统帅 |
    | Light-bringer              | 光明使者 | 圣职 |
    | Oath-keeper                | 守誓者   | 骑士 |
    | Ash-walker                 | 灰烬行者 | 孤傲 |

    #### 精灵：银叶王庭 (自然与灵动)
    | 英文组合 (Prefix + Suffix) | 中文译名 | 风格 |
    |----------------------------|----------|------|
    | Star-gazer                 | 观星者   | 法师 |
    | Moon-blade                 | 月刃     | 刺客/剑士 |
    | Leaf-shade                 | 叶影     | 游侠 |
    | Spell-weaver               | 编织者   | 奥术 |

    #### 矮人：铁炉堡联盟 (坚韧与锻造)
    | 英文组合 (Prefix + Suffix) | 中文译名 | 风格 |
    |----------------------------|----------|------|
    | Stone-wall                 | 石壁     | 防御 |
    | Deep-delver                | 深潜者   | 探索 |
    | Anvil-breaker              | 碎砧者   | 力量 |
    | Mountain-eye               | 山脉之眼 | 远见 |

    #### 半兽人：赤铜部落 (残暴与征服)
    | 英文组合 (Prefix + Suffix) | 中文译名 | 风格 |
    |----------------------------|----------|------|
    | Blood-axe                  | 血斧     | 狂暴 |
    | Bone-chewer                | 食骨者   | 凶残 |
    | Thunder-hoof               | 雷蹄     | 骑兵 |
    | Gore-fiend                 | 血魔     | 屠戮 |

    ### 8.3 称号生成规则
    1.  **职业匹配权重**：
        *   战士/圣骑士：优先从“Heart, Shield, Blade, Iron”中抽取。
        *   法师/牧师：优先从“Spell, Star, Light, Shadow”中抽取。
        *   游侠/游荡者：优先从“Wind, Shadow, Step, Eye”中抽取。
    2.  **独特性检查**：
        *   史诗级NPC（领主）拥有唯一的固定称号。
        *   随机生成的冒险者在5级时从对应种族池中随机组合，确保不与现有英雄重复。

