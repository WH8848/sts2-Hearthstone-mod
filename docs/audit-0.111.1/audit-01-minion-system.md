# 随从系统审计（0.111.1）

> 审计范围：`Scripts/Character/Minions/*`（JainaMinionBase、JainaAttackAction、JainaConditionalAttackIntent、JainaMinionPool、JainaMinionCardMap、JainaHandOrderTracker、JainaMinionTooltip、OrbStyleMinionLayout、14 个随从类）与 `Scripts/Character/Cards/JainaMinionCardTemplate.cs`、`JainaCardTypePatches.cs`。
> 依据：`E:\MOD\sts2\sts2\0.111.1\src`（3545 个 cs）逐项核对；MinionLib 0.6.2 / RitsuLib 0.5.11 为外部库（以 DLL 字符串扫描 + 项目内精读文档交叉验证其依赖的游戏侧 API）。
> 结论：mod 直接调用的游戏 API 在 0.111.1 全部存在且签名一致；发现 1 个高危依赖链风险（随从意图的 NextMove 填充），4 个中低危行为/显示差异，若干改进机会。

---

## 兼容性问题（高 × 1，中 × 3，低 × 6）

### 高

**高** | JainaMinionBase.cs:277-291（RefreshIntentDisplay）与 203-240（GenerateMoveStateMachine） | **玩家侧随从（Pet）的 `Monster.NextMove` 在 0.111.1 原版流程中永远不会被填充**，整个"条件意图显示"依赖外部机制（RitsuLib `AddMonsterIntentManager` 一类）代为填充；该机制失效时意图会静默消失（无异常）。游戏侧 0.111.1 证据：
- `CombatManager.AfterCreatureAdded` 只对 `creature.IsEnemy` 调用 `Monster.RollMove`（CombatManager.cs:1131-1134）——玩家侧 Pet 不满足；
- `CreatureCmd.Add` 对非敌人怪物调用 `PrepareForNextTurn(..., rollNewMove: false)`（CreatureCmd.cs:72-75）——显式不 RollMove；
- `MonsterModel.NextMove` 默认 `new MoveState()`（UNSET_MOVE，空 Intents）（MonsterModel.cs:240）；`NCreature.UpdateIntent` 无条件读 `Entity.Monster.NextMove.Intents`（NCreature.cs:290）；
- 全源码仅 5 处 RollMove 调用点（CombatManager.cs:1133、Creature.cs:552、MonsterModel.cs:416、MonsterMoveStateMachine.cs:34），无任何为 Pet 服务的路径。
建议：在 `OnSummon` 末尾显式 `await Creature.PrepareForNextTurn(combatState.PlayerCreatures)`（0.111.1 Creature.cs:547，rollNewMove 默认 true：RollMove + RefreshIntents），自包含填充 NextMove，摆脱对 RitsuLib 意图管理器的隐式依赖；并在 0.111.1 运行时验证意图出现/消失/隐藏三条路径。

### 中

**中** | JainaConditionalAttackIntent.cs:17-71 | **意图数值标签永不显示**：`NIntent.UpdateVisuals` 只为 `intent is AttackIntent` 或 `StatusIntent` 实例渲染数值文本，其余一律置空（NIntent.cs:135 `((intent is AttackIntent) ? ... : ((!(intent is StatusIntent)) ? string.Empty : ...))`）。`JainaConditionalAttackIntent` 直接继承 `AbstractIntent`（不是 AttackIntent），故即使可攻击，攻击数字也不显示（图标、粒子、悬停均正常）。0.111.1 证据：Core/Nodes/Combat/NIntent.cs:117-137。建议：给 `JainaConditionalAttackIntent` 实现 RitsuLib 的 `IIntentExtraCornerAmountLabelsProvider`/`SpecsProvider` 角标（ritsulib-r6/r7 文档有现成 API），在意图上绘制攻击数值，绕开 NIntent 的原生标签分支。

**中** | JainaMinionBase.cs:231（自动攻击）与 JainaAttackAction.cs:60（手动攻击） | **意图显示伤害与实际命中伤害口径不一致**：显示走 `SingleAttackIntent.GetSingleDamage` → `Hook.ModifyDamage(..., ValueProp.Move, ModifyDamageHookType.All, ...)`（AttackIntent.cs:84-93），力量/虚弱会缩放显示值；实际命中走 `CreatureCmd.Damage(..., ValueProp.Unpowered, ...)`（不吃力量）。玩家有力量加成时，意图数字 ≠ 实际伤害。0.111.1 证据：Core/MonsterMoves/Intents/AttackIntent.cs:84-93 与 CreatureCmd.cs:258-266。建议：明确"显示为参考值"并注释，或在 `JainaConditionalAttackIntent` 中覆写 `GetIntentLabel`/`GetTotalDamage` 返回 `BaseAttackValue` 原值（但 NIntent 标签分支同样不认，需配合上一条角标方案）。

**中** | JainaMinionBase.cs:154-180（SetupTooltipContent） | **`ModelDb.GetById<CardModel>` 无 try/catch**：若 `JainaMinionCardMap.GetCardType` 返回的类型尚未注册（卡片注册失败/加载顺序问题），`GetById<T>` 抛 `ModelNotFoundException`（ModelDb.cs:549-553），异常从 `TryCreateCreatureVisuals` 冒出，可能导致 RitsuLib 视觉工厂调用失败、随从节点创建异常。0.111.1 证据：Core/Models/ModelDb.cs:549-553。建议：`SetupTooltipContent` 整体包 try/catch，失败时仅跳过悬停面板（视觉照常）。

### 低

**低** | JainaMinionBase.cs:284 | `RefreshIntentDisplay` 为 fire-and-forget（`_ = node.RefreshIntents()`），异步异常不可观测；且 `RefreshIntents` 内部调用 `RevealIntents`（NCreature.cs:330-347），每次调用都重放意图淡入动画（攻击后/回合开始意图会"闪"一下）。0.111.1 证据：NCreature.cs:330-347。建议：`try/catch` 包裹并记录日志；若只想更新内容不想淡入，可改调 `UpdateIntent`。

**低** | MinionLib 0.6.2 `MinionKillPatch`（项目内文档 MinionLib_Action_Powers_文档.md:110） | MinionLib 补丁目标 `CreatureCmd.KillWithoutCheckingWinCondition` 在 0.111.1 为 **private static 且签名 `(Creature creature, bool force, int recursion = 0)`**（CreatureCmd.cs:515）。Harmony 若按方法名绑定（`[HarmonyPatch(typeof(CreatureCmd), "KillWithoutCheckingWinCondition")]`）可命中；若按参数表绑定，新增的 `recursion` 参数会使补丁静默失效 → 友方随从死亡残留（0.107.0 的老问题复现）。0.111.1 证据：Core/Commands/CreatureCmd.cs:515。建议：0.111.1 运行时验证随从死亡后从 `Pets`/场面正确移除；必要时升级 MinionLib 或补一个同名补丁兜底（mod 自身 AfterDamageReceivedLate 已有双保险）。

**低** | JainaMinionBase.cs:68（VisualsPath 覆写为 PNG） | `MonsterModel.AssetPaths` 会把 `VisualsPath`（PNG 路径）纳入预加载（MonsterModel.cs:77-93），PNG 非场景，`PreloadManager.Cache.GetScene` 失败走 fallback（MonsterModel.cs:280-295）——视觉正确性完全依赖 RitsuLib `IModCreatureVisualsFactory` 拦截 `CreateVisuals`；若拦截失效，随从视觉退化为 fallback 场景。0.111.1 证据：MonsterModel.cs:278-295。建议：运行时确认视觉工厂在 0.111.1 生效（`NCreature.Create`/`MonsterModel.CreateVisuals` 未变，风险低）。

**低** | JainaMinionCardTemplate.cs:97-103 | `OnPlay` 忽略 `SummonMinionByType` 返回的 null（随从满 7 个时静默"白打"一张牌）。0.111.1 无相关签名问题（JainaMinionPool.SummonMinion 的上限逻辑在 mod 侧）。建议：满员时给出反馈（或保持现有行为但记日志）。

**低** | JainaMinionBase.cs:311-312 | `PowerCmd.Apply<MinionPower>` 对**玩家侧** Pet 基本是惰性标记：`MinionPower.OwnerIsSecondaryEnemy` 只对 `Side == CombatSide.Enemy` 生效（Creature.cs:269-278），玩家侧随从不参与击杀结算判定，此调用无副作用也无必要。0.111.1 证据：Core/Models/Powers/MinionPower.cs:13、Creature.cs:269-278。建议：可保留（防御性）或移除。

**低** | Zealot.cs:38-55 | 召唤当回合立即攻击（OnSummon）但未置 `_hasAttackedThisTurn` → 召唤当回合意图仍显示"可攻击"，直到回合结束才隐藏。行为不一致（其他自动随从召唤当回合不显示意图）。建议：OnSummon 攻击后置 `_hasAttackedThisTurn = true` 并刷新意图。

---

## 潜在 Bug / 行为差异（mod 逻辑 vs 0.111.1 语义）

1. **意图数字不显示（上述中 1）**：`JainaConditionalAttackIntent` 非 `AttackIntent`，NIntent 的数值标签分支（NIntent.cs:135）不渲染 → "攻击力数字"功能在当前 0.111.1 语义下实际不可见；注释宣称"等同于攻击力"的显示并未兑现。
2. **意图显示伤害 ≠ 实际伤害（上述中 2）**：显示经 `Hook.ModifyDamage(ValueProp.Move)` 吃力量/虚弱；命中用 `ValueProp.Unpowered`。示例：玩家力量 +5 时意图显示 8，命中只打 3。
3. **随从意图的刷新链路**：`CombatStateTracker.CombatStateChanged` 在 0.111.1 是**延迟一帧**触发且要求 `Creatures.Count > 0`（CombatStateTracker.cs:212-234），NIntent 靠它自动重算（NIntent.cs:109-115）。mod 的攻击后手动刷新（RefreshIntentDisplay）与 0.111.1 原生意图揭示（PerformIntent 冻结/RevealIntents 淡入）叠加时会重播淡入动画——视觉上"闪烁"，非功能错误。
4. **手动模式双计数**：`AttackPointsRemaining`（mod 侧）与 `JainaAttackAction` 的 Power `Amount`（MinionLib 侧）各自递减（JainaMinionBase.cs:265-271 + JainaAttackAction.cs:63 + MinionLib `DecrementAfterAct` → `PowerCmd.Decrement`，PowerCmd.cs:184）。0.111.1 下两者语义已核对一致（Decrement/Remove/Apply 均存在），但任何第三方把 `JainaAttackAction` 当普通 Power 增删都会造成两计数漂移。
5. **多玩家额外回合**：`BeforeSideTurnStart` 的 `participants` 在额外回合可能不含其他玩家（AbstractModel.cs:1241-1246 注释）；mod 未按 participants 过滤，仅检查 `side == CombatSide.Player`——本地玩家视角基本正确，但"召唤当回合"判定依赖 `PlayerCombatState.TurnNumber`（SwitchSides 时递增，CombatManager.cs:1882），额外回合语义一致，无需改动。
6. **`CombatState.RemoveCreature(Creature, true)` 的 detach 语义**：0.111.1 中 `unattach: true` 会把 `creature.CombatState = null`（CombatState.cs:300-303），随从无法再被重新加入战斗；mod 的死亡清理顺序（先 CombatState 后 CombatManager，JainaMinionBase.cs:461-470）与 MinionLib MinionKillPatch 均为幂等且 try/catch 保护，未发现新问题。
7. **意图标签路径在隐藏态的兜底**：`JainaConditionalAttackIntent.GetIntentLabel` 隐藏态调用 `base.GetIntentLabel` → `FORMAT_EMPTY`（AbstractIntent.cs:47-50）；因 NIntent 不渲染非 AttackIntent 标签，此路径实际不产生可见文本——与上述 1 同源。

---

## 改进机会（0.111.1 新 API 可替代/增强现有实现）

1. **意图数值显示**：用 RitsuLib `IIntentExtraCornerAmountLabelsProvider`/`SpecsProvider`（项目内 ritsulib-r6/r7 文档）在 `JainaConditionalAttackIntent` 上绘制攻击数值角标——绕开 NIntent 的 `AttackIntent`/`StatusIntent` 专用标签分支（NIntent.cs:135），且与 0.111.1 意图 UI 刷新机制天然兼容。
2. **意图自举**：`Creature.PrepareForNextTurn(IEnumerable<Creature>, bool rollNewMove = true)`（Creature.cs:547-559）在召唤后主动 RollMove + RefreshIntents，可让随从意图显示完全不依赖 RitsuLib 意图管理器（消除高 1 风险）。
3. **多意图支持**：`NCreature.UpdateIntent` 原生支持 `NextMove.Intents` 多意图渲染（多个 NIntent 子节点，NCreature.cs:290-311）——未来可为随从挂"防御/增益"等多意图 MoveState 复用原版渲染。
4. **条件意图自动重算**：0.111.1 的 `CombatStateTracker.CombatStateChanged` 延迟事件（CombatStateTracker.cs:203-235）会在力量/HP/Power 变化后自动触发 NIntent.UpdateVisuals → `_isVisible()` 重算；若把意图伤害口径改为吃力量（见中 2 的取舍），显示值会自动跟随，无需额外手动刷新。
5. **`MonsterModel.IntendsToAttack`**（MonsterModel.cs:242-246）+ RitsuLib `AnyAttackingEnemy` 目标类型：若未来出现"敌方随从"，可直接复用 0.111.1 原生的"攻击意图敌人"目标语义。

---

## 结论摘要

1. mod 直接依赖的 0.111.1 游戏 API（MonsterMoveStateMachine/MoveState、AbstractIntent 及全部覆写点、NCombatRoom/NCreature/NIntent 意图链、CreatureCmd/PowerCmd/CombatState/CombatManager、MonsterModel 与回合钩子、ModelDb/EnergyCost/Rng）经逐一核对**全部存在且签名一致**，无 MissingMethodException 级别风险。
2. 最高风险在意图显示链路：0.111.1 原版不为玩家侧 Pet 调用 RollMove，`Monster.NextMove` 默认空意图——随从条件意图是否显示完全取决于 RitsuLib 意图管理器的填充行为，属"静默失效"类风险，需在 0.111.1 运行时验证并建议在 OnSummon 自举填充。
3. 两个显示层行为差异（意图数值不渲染、显示伤害与实际伤害口径不一）不影响战斗结算，但会让"攻击力数字"宣传与实际不符，建议用 RitsuLib 角标 API 修复。
4. MinionLib 0.6.2 的死亡清理补丁目标在 0.111.1 仍存在（签名多出 recursion 参数），需运行时确认补丁绑定；mod 自身已有幂等双保险清理。
5. 总体：随从系统在 0.111.1 下可安全运行，核心战斗逻辑（召唤/行动点/自动攻击/亡语/清理）签名全部兼容，按本文建议做意图链路验证与显示修复即可。
