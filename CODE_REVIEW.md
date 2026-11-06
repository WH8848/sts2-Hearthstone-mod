# Jaina Mod 代码审查报告

生成时间：2026-11-06
范围：`Scripts/` 全部 C# 代码（271 文件 / 28,057 行）

---

## 1. 规模与模块划分

| 模块 | 文件 | 行数 | 作用 |
|---|---:|---:|---|
| Cards | 116 | 11,657 | 卡牌内容主体（吉安娜卡池、衍生卡、英雄技能卡、升级形态） |
| Powers | 75 | 7,957 | 机制核心（冻结/灌注/随从军势/结界/任务线/英雄技能/诊断补丁） |
| Minions | 49 | 4,795 | 随从系统（MinionLib 集成、召唤池、死亡清理、UI） |
| Root | 9 | 1,427 | 入口、注册、公共工具、RitsuLib 初始化 |
| Weapons | 8 | 815 | 武器系统（攻击行动、耐久） |
| Relics | 11 | 722 | 遗物 |
| Keywords | 2 | 308 | 自定义关键词注册 |

结论：**Cards + Powers + Minions 占 87% 代码量**，是本 mod 的主体；其余为支撑系统。

---

## 2. 风险清单

### 2.1 Harmony Patch —— 高风险（86 个 / 36 文件）

目标分布（Top）：

| 目标类 | 数量 | 说明 |
|---|---:|---|
| `CardSelectCmd` | 15 | 卡牌选择流程（发现/升级/附魔/弃牌等），核心玩法 |
| `CardModel` | 11 | 卡牌行为（费用、可否打出、描述） |
| `NCard` | 6 | 卡牌 UI |
| `CardPileCmd` | 6 | 牌堆操作 |
| `CardTypeExtensions` | 4 | 卡牌类型判定 |
| `CardEnergyCost` | 3 | 费用计算 |
| 其余（Hook/CreatureCmd/ArtifactPower/奖励等） | 各 1 | — |

**风险**：游戏更新若改方法签名，对应 patch 直接失效（静默或抛异常）；`CardSelectCmd` 的 15 个 patch 集中在核心选择流程，冲突面最大。

**建议**：集中注册 + 启动时校验目标方法存在性（缺失打 `Error`），关键 patch 外层 try/catch 降级。

### 2.2 反射 —— 中高风险（44 处 / 13 文件）

依赖私有字段/方法（如 `CombatState._allies`、`_enemies`、`CreaturesChanged` 事件）。

现状：`JainaPetResidueCleanupPower.ForceRemoveLingering` 已加固（公开 API 优先 + 字段探测 + 失败告警），其余反射点未统一。

**建议**：抽 `GameReflection` 工具类，集中缓存 `AccessTools.Field/Method`，启动时校验并记录缺失。

### 2.3 `async void` —— 中风险（2 处）

位置：`Powers/JainaPendingRewardQueue.cs` 的两个 Harmony Postfix。

**风险**：fire-and-forget，异常被空 catch 吞掉；奖励发放可能延迟或静默失败。

**建议**：改为 `TaskHelper.RunSafely(FlushAsync())`（明确 fire-and-forget），catch 里至少 `Log.Warn`。

### 2.4 日志 —— 低风险（118 处 / 47 文件）

联机高频日志（冻结/伤害/召唤）会刷屏并拖慢节奏。

**建议**：高频日志降为 `Log.Debug`，或加联机开关。

### 2.5 空 catch —— 低风险（5 处）

静默吞异常，排查困难。建议至少 `Log.Warn`。

### 2.6 随机数 —— 良好

- 0 处 `new Random`；
- 61 处 `RunState.Rng`（确定性）；
- 已修复 1 处非确定性：`JainaMinionCardMap.MinionTypes`（Dictionary 迭代顺序）→ 候选表排序。

---

## 3. 优化建议（按优先级）

| 优先级 | 事项 | 工作量 | 收益 |
|---|---|---|---|
| P0 | `async void` → `RunSafely` + Warn（2 处） | 小 | 消除静默失败 |
| P0 | 反射集中化 + 启动校验（44 处） | 中 | 游戏更新时快速定位失效点 |
| P1 | Harmony patch 目标存在性校验（86 个） | 中 | 更新后不会静默失效 |
| P1 | 高频日志降级/联机开关 | 小 | 性能与可读性 |
| P2 | 空 catch 补 Warn（5 处） | 小 | 可观测性 |
| P2 | Cards 目录按派系/类型拆分（116 文件平铺） | 中 | 可维护性 |
| P2 | 统一随机池确定性排序工具 | 小 | 防其他 Dictionary 顺序问题 |

---

## 4. 总体结论

- 代码规模适中（28k 行），模块划分清晰；主体逻辑集中在 Cards/Powers/Minions。
- 联机确定性（RNG、候选表排序、预测副作用）已基本处理；断联根因（RandomForeseer 预测扣血、随机池顺序）已修。
- 最大长期风险是 **Harmony 86 个 patch + 44 处反射** 对游戏版本的依赖——建议优先做两项 P0，成本低、能在版本更新时避免"静默失效"。
