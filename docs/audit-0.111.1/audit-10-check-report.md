# 审计：Jaina mod 检查（0.1.1 / 游戏 0.111.1）

> 检查范围：`E:\MOD\sts2\godot_project\jaina`（269 个 .cs）、部署产物 `E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\Jaina`
> 旁证：`E:\MOD\sts2\bug日志\godot.log`（客机侧，76561199747643361，2026-08-28 13:41–14:52）
> 游戏源码：`E:\MOD\sts2\sts2\0.111.1\src`（反编译，用于核对原版行为）

## 1. 构建 / 部署

| 项目 | 结果 |
|---|---|
| `dotnet build -t:Compile -c Debug` | **0 error**，61 warning（全部 CS8600/8602/8603/8604 可空引用） |
| `mods\Jaina\Jaina.dll` | 562,688 B，2026-08-28 23:08:23（与最新源码 RiggedFaireGameCard.cs 23:08:09 一致，部署是最新的） |
| `mods\Jaina\Jaina.pck` | 12,312,192 B，23:08:28（最新） |
| `mods\Jaina\Jaina.pdb` | 2026-08-28 **15:05:54** —— 比 dll 旧，断点/异常行号会错位，建议重建 |
| manifest | 已有 `min_version` 依赖，不会触发日志里那条 "old-style dependencies" 警告 |

客机那局跑的是 **0.1.0**（分歧报告 `version=0.1.0`，日志无 `[JainaFaire]` 行），且客机 mods 目录名是 `Jaina (1)`——正是 `JainaTradeAction` 注释里警告过的"旧副本目录"场景。

## 2. 已定位根因：原版 targetId 只有 6 bit，CombatId ≥ 64 目标回绕

### 证据（日志侧，一一对应）

- 客机整局 **584** 次出牌请求中，恰好 **21** 次 `targetid >= 64`：64×8、65×9、66/67/68 各 1。
- 整局恰好 **21** 条 `[ERROR] GameAction PlayCardAction ... finished execution, but was in state Canceled!`，**全部**是 `CARD.JAINA_CARD_RIGGED_FAIRE_GAME_CARD`（巫卜）。
- 其中 5 处可直接看到回绕：`targetid 64 → target: 0`、`65 → 1`。

### 证据（原版源码侧）

```
NetPlayCardAction.cs:26-31
  writer.WriteBool(targetId.HasValue);
  if (targetId.HasValue) writer.WriteUInt(targetId.GetValueOrDefault(), 6);   // ← 只有 0..63
BitSerializationUtil.WriteBytes(...)                                          // 只写低 N 位 → 64→0, 65→1

PlayCardAction.cs:40   public Creature? Target => ...GetCreature(TargetId);    // 用回绕后的 id 找生物
PlayCardAction.cs:66   Creature target = await ...GetCreatureAsync(TargetId, 10.0);
PlayCardAction.cs:85-89 if (!_card.CanPlay(...) || !_card.IsValidTarget(target)) { Cancel(); return; }
GameAction.cs:203-209  Cancel() { State = Canceled; ... }
GameAction.cs:141-159  执行体已完成但 State != Executing → Log.Error("... in state {state}! ...")

CombatState.cs:257-262 AttachCreature: CombatId = _nextCreatureId++;            // 每只生成过的生物都占一个号，死亡不回收
```

### 结论

每回合造小精灵/随从的玩法，一场战斗累计生成生物数很容易越过 63。之后：

1. 出牌消息里的 `targetId` 被截断成 `id & 63`；
2. 收到端按回绕后的 id 解析到**别的生物**（通常是 CombatId 0/1 的玩家本体）；
3. 目标校验失败 → `Cancel()` → 卡**没有实际效果**（小精灵没被消灭），同时刷出那条 Canceled 报错；
4. 这正是"巫卜打不出去 / 打了没反应 / 连刷 ERROR"的完整解释，也是两端动作/状态分歧的高危来源。

当前源码里的 `[JainaFaire]` 兜底只保证"目标无效也要杀一只自己的小精灵"，**不解决**目标回绕本身，也不解决 `Canceled` 刷屏。

### 修复建议（按优先级）

1. **巫卜改成不依赖原版 targetId**：`TargetType.None` + `OnPlay` 内部确定性选目标（例：存活自有小精灵中 CombatId 最小者）。网络消息不再携带 targetId → 根除回绕。
2. 或把目标放进自定义托管动作载荷（`uint32` CombatId，参照 `JainaTradeAction` 的 `RitsuLibManagedNetActions` 做法）。
3. 加防御：出牌前若 `target.CombatId > 63`，本地直接拒绝出牌并提示，而不是静默 Cancel。
4. 内容侧：小精灵/随从单场累计生成数做上限或复用，降低触发概率（治标）。
5. 可向官方反馈：`NetPlayCardAction` 的 6 bit targetId 对 mod 内容规模是硬上限。

## 3. 状态分歧（StateDivergence）现状

- 分歧报告 `_div_tmp\metadata.json`：`checksum #56`，由 **RitsuLib 托管动作** 触发（`opcode 14951495191492406621`，`type CombatPlayPhaseOnly`）。
- 本 mod 里唯一的 `RitsuLibManagedNetAction` 是交易 `jaina/tradeable-trade-v1`；两端都打印了注册行，但整局没有一次 `[JainaTrade] resolve` → 该局没做交易，触发点更可能是 **MinionLib 的随从行动托管动作**。
- 报告自身的状态快照里，`creatures` / `players` / `RNG` / `relicGrabBag` **全部一致**；6 处差异只剩 checksum 值、remotePeer 标记、模组清单（主机 37 vs 客机 14）与两个本地化名 → **没有实质状态分歧**，更像采样时序 / 假阳性（与 `AmazingCard` 注释里记录的 "fire-and-forget → 假阳性断联" 同一类）。
- 因此下一步重点是下面第 4 条的 fire-and-forget 收敛。

## 4. 代码审查发现

### P0

1. **targetId 6 bit 回绕**（见第 2 节）。
2. **状态变更类 fire-and-forget（11 处）** —— 在同步钩子/事件里点火即走，await 恢复时机不受动作队列控制：
   - `Scripts\Entry.cs:200,206,211,215`（CombatBegan 里异步挂 WeaponSlot / MinionSquadPower / IgniteClockPower / PlayedRecordHookPower）
   - `Scripts\Entry.cs:246`（玩家死亡时异步 Kill 随从）
   - `Scripts\Character\Powers\TemporaryPowerPetTurnEndFix.cs:61,100,137`（回合结束异步 PowerCmd.Remove + Apply）
   - `Scripts\Character\Relics\RummageThroughStockRelic.cs:60`（发现时异步造成 3 点伤害）
   - `Scripts\Character\Powers\MinionSquadPower.cs:100`（挡伤致死时异步 Kill）
   - `Scripts\Character\Powers\JainaPendingRewardQueue.cs:83,106`（`async void` Postfix 发牌）
   → 建议改走钩子/动作上下文 await（`HookPlayerChoiceContext`），或改成同步执行 + 在动作边界统一 flush。

### P1

3. `AutoPlayGuard.IsInAutoPlay()` / `IsInHellraiserAutoPlay()` 每次调用都 `new StackTrace`，而 `JainaTargetTypes.AnyTargetableType.IsValidTarget` 会调它 → 出牌校验、随机目标筛选（每个候选生物一次）都在抓栈；联机时帧抖动会被对手感知为卡顿。建议用 `AsyncLocal` 标记或只在"随机释放入口"抓一次栈后透传。
4. `JainaRandomPoolHelper.EnsureEnemyLimitedCache()` 在运行时读 `res://localization/zhs/cards.json` 文本判定"描述含敌人"→ 玩法随机池依赖本地化文案。语言固定 zhs 是对的，但文案改动即改变卡池，版本不一致就分歧。建议改为显式标记（自定义 attribute / 白名单）。
5. `DeterministicSavedPropertyNetIdPatch` 反射改私有静态表且整体 `catch { }` 无日志 —— 游戏升级后字段名变化会静默退化为"非确定性 net-id"，建议失败时 Warn。
6. `JainaPetResidueCleanupPower.ForceRemoveLingering` 反射摘 `_allies/_enemies` 并手动触发 `CreaturesChanged`：有效但版本脆弱，建议启动自检一次并告警。
7. `JainaTradeAction.ExecuteAsync` 使用 `NetCombatCard.ForTesting(...)`（原版注释标明是测试入口）走正式流程，建议换成正式 API 或注明理由。

### P2

8. 61 个可空引用 warning，建议逐步清掉以免掩盖真 NRE。
9. 75 个 Harmony 补丁 / 31 个文件，其中若干是 workaround（`ClearBlockDiagPatch`、`TriggerAnimSafePatch`、`CardStuckInPlayAfterPlayPatch`、`SandpitAfterRemovedFix`、`TemporaryPowerPetTurnEndFix`）；`Entry.VerifyDynamicHarmonyTargets` 已有"目标缺失"告警，建议纳入每次版本升级的人工检查清单。
10. `Jaina.pdb` 陈旧（15:05 vs dll 23:08）。

## 5. 建议顺序

1. 修 targetId（先巫卜改 `TargetType.None` 自选）→ 立即消掉 21 次 Canceled 与"巫卜无效"。
2. 收敛 fire-and-forget（先 `TemporaryPower*` 3 处、`RummageThroughStockRelic`、`Entry` 的 CombatBegan 挂载）。
3. 复现测试时两端**同版本同 mod 集**（当前朋友 0.1.0、你 0.1.1；分歧报告两端 37 vs 14 个 mod）。
4. 每局抓两端 `godot.log`，对齐 `targetid` 与 Canceled 计数（口令：grep "Sending message to host requesting to enqueue action PlayCardAction" → 统计 targetid ≥ 64）。
