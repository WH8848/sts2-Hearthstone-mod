# MinionLib 0.6.2 — Action / Powers 目录精读笔记

> 精读范围：`E:\MOD\sts2\MinionLib\MinionLib\Action\`（10 个 .cs）与 `Powers\`（4 个 .cs）。
> 核心机制小节为准确引用，另核对了 `Minion\MinionModel.cs`、`Commands\MinionCmd.cs`、`Minion\Patches\MinionKillPatch.cs`、`Initialization\MinionHookInitializer.cs`、`Targeting\CustomTargetTypeManager.cs`、`Commands\PetOrderSnapshotManager.cs`、`MainFile.cs`。
> 全局约定：`MainFile.cs` 定义 `[ModInitializer] public partial class MainFile : Node`，`Initialize()` 里 `new Harmony("MinionLib").PatchAll()` + `MinionHookInitializer.Initialize()`；所有 `Debug` 日志带 `[Conditional("DEBUG")]`（发布版不输出）。

---

## 一、公共类型清单

### 命名空间 `MinionLib.Action`

| 类型 | 用途 | 关键成员 |
|---|---|---|
| `ActionModel`（abstract，继承 `PowerModel`）| 随从"行动"的基类：以 Power 形式挂在随从身上，`Amount` 即行动次数/充能 | `public abstract TargetType TargetType { get; }`；`public virtual bool AutoRemoveAtTurnEnd => false`；`public virtual bool DecrementAfterAct => false`；`public virtual bool OnlyRespondIconClick => false`；`public new void Flash()`；`public virtual bool CanAct(ICombatState)`（`Amount>0 && Owner.IsAlive && Owner.CombatState==combatState`）；`public bool IsValidTarget(ICombatState, Creature?)`（经 `CustomTargetTypeManager.TryGetCustomTargetType` 查 `ICustomTargetType.IsValidTarget`）；`public IReadOnlyList<Creature> GetValidTargets(ICombatState)`；`public async Task<bool> TryAct(PlayerChoiceContext, Creature?)`（按 TargetType 分派：None 直接执行；单目标校验 target；多目标要求有效目标数>0，执行时传 null）；`public override Task BeforeSideTurnEnd(...)`（`AutoRemoveAtTurnEnd` 且轮到自己方时 `PowerCmd.Remove` 自毁）；`protected abstract Task OnAct(PlayerChoiceContext, Creature?)`；静态 hover tip 用 loc key `static_hover_tips`/`MinionLib-Action.title|description` |
| `CreatureActionQueueService`（internal static）| 行动入队入口：校验战斗/队列状态后把 `ActionModel` 包装为 GameAction 提交给同步队列 | `public static bool TryEnqueue(ActionModel, Creature?)`（要求 `CombatManager.Instance.IsInProgress`、`actor.CombatId!=null`、`RunManager.Instance.ActionQueueSynchronizer.CombatState == ActionSynchronizerCombatState.PlayPhase`、`CreatureActionQueueThreshold.TryReserve` 成功；`RequestEnqueue` 抛异常时 `Release` 回滚） |
| `CreatureActionQueueThreshold`（internal static）| 每 actor×每 action 的已排队计数，防止超发（`Amount` 即上限） | `public static bool IsExhausted(ActionModel)`（`Amount <= QueuedCount`）；`public static bool TryReserve(ActionModel)`（`Amount > QueuedCount` 才 +1）；`public static void Release(uint actorCombatId, ModelId actionId)`；`public static void Clear()`（回合结束/战斗开始/战斗结束由 `MinionHookInitializer` 调用） |
| `GameActions.ExecuteCreatureActionGameAction`（sealed，继承 `GameAction`）| 实际的行动执行单元，走官方 ActionQueue 同步（含联机） | `public override ulong OwnerId => Owner.NetId`；`public override GameActionType ActionType => GameActionType.CombatPlayPhaseOnly`；两个 ctor（`(ActionModel, Creature?)` 运行时构造 / `(Player, uint actorCombatId, ModelId actionModelId, uint? targetCombatId)` 反序列化构造）；`protected override Task ExecuteAction()`；`public override INetAction ToNetAction()`；`public override string ToString()`；私有静态 `Player? ResolveQueueOwner(Creature)`（优先 `PetOwner` → `Player` → `LocalContext.GetMe(CombatState)`） |
| `GameActions.NetExecuteCreatureActionGameAction`（struct，`INetAction`）| 联机序列化载荷 | 字段 `uint ActorCombatId`、`ModelId ActionModelId`、`uint? TargetCombatId`；`GameAction ToGameAction(Player)`；`Serialize`（uint 用 6 位、`WriteModelEntry`、`WriteBool`）；`Deserialize`（`ReadModelIdAssumingType<PowerModel>()`，即要求该 ModelId 指向 PowerModel） |
| `Patches.ActionClickPatch`（static）| 点击随从本体触发行动 + 目标选择 UI | `[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]` `[HarmonyPostfix] static void Postfix(NCreature)`（把 `Hitbox.GuiInput` 接到 `OnGuiInput`）；`public static Task TryUseActionFromIconAsync(NCreature, ActionModel, Vector2)`（图标点击入口）；私有 `TryUseActionAsync(NCreature, bool useController, ActionModel?, Vector2?)`（校验→选 action→按 TargetType 分派→`NTargetManager` 选目标→`TryEnqueue`）；静态 `HashSet<uint> TargetingActors` 防重入 |
| `Patches.ActionPowerIconClickPatch`（static）| 点击 Power 图标直接触发对应行动 | `[HarmonyPatch(typeof(NPower), nameof(NPower._Ready))]` `[HarmonyPostfix] static void Postfix(NPower)`；`OnPowerGuiInput`：左键释放、`NTargetManager` 空闲、`powerNode.Model is ActionModel`、经 `NCombatRoom.Instance.GetCreatureNode(Owner)` 找到随从节点后调用 `ActionClickPatch.TryUseActionFromIconAsync`（起点位置 = 图标位置 + (20,20)） |

### 命名空间 `MinionLib.Powers`

| 类型 | 用途 | 关键成员 |
|---|---|---|
| `MinionGuardianPower`（sealed，继承 `PowerModel`）| 守护者：把指向主人的未格挡伤害重定向到自己（仅前排随从） | `public override PowerType Type => PowerType.Buff`；`public override PowerStackType StackType => PowerStackType.Single`；`public override Creature ModifyUnblockedDamageTarget(Creature target, decimal amount, ValueProp props, Creature? dealer)`（条件：`Owner.Monster is MinionModel && Position == MinionPosition.Front`；目标必须是主人本人，或同为该主人且更靠前的守护随从（按 `PlayerCombatState.Pets` 顺序，自己在 target 之后才算有效）；`Owner.IsDead` 或 `!props.HasFlag(ValueProp.Move) || props.HasFlag(ValueProp.Unpowered)` 时不拦截） |
| `Patches.MinionGuardianBlockToHpPatch`（static）| 守护者把"获得格挡"转为"提升最大生命 + 治疗" | `[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.GainBlock), typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardPlay), typeof(bool))]` `[HarmonyPrefix]`；`public static async Task<decimal> GainBlock(...)`（完整重演官方 Hook 流：`BeforeBlockGained`→`ModifyBlock`→`AfterModifyingBlockAmount`→`SetMaxHp(+amount)`→`Heal(amount)`→`History.BlockGained`→`AfterBlockGained`；`CombatManager.Instance.IsOverOrEnding` 时返回 0；fast 走 `Cmd.CustomScaledWait(0,0.03f)`，否则 `(0.1f,0.25f)`；播放 `block_gain` SFX 与 `vfx/vfx_block`） |
| `Patches.MinionGuardianOverkillPatch`（static）| 多前排守护时的溢出伤害重定向链 | `[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Damage), typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel), typeof(CardPlay))]` `[HarmonyPrefix]`；`public static readonly AsyncLocal<Creature?> SuppressedOwner`；私有 `ShouldHandle`（单目标、目标为存活玩家、`ValueProp.Move` 且非 `Unpowered`、该玩家有存活前排守护）与 `HandleWithOverkillRedirect`（见核心机制）、`IsFrontGuardian(Creature)` |
| `Patches.MinionGuardianOwnerDamageSuppressPatch`（static）| 重定向流程中压制主人那一下临时掉血 | `[HarmonyPatch(typeof(Creature), nameof(Creature.LoseHpInternal), typeof(decimal), typeof(ValueProp))]` `[HarmonyPrefix]`：当 `__instance == MinionGuardianOverkillPatch.SuppressedOwner.Value` 且 `amount>0` 时，直接返回 `new DamageResult(__instance, props)` 并跳过原逻辑 |

---

## 二、核心机制说明

### 1. 随从注册/召唤（`MinionCmd.cs` + `MinionModel.cs`）
- 随从 = 玩家 Pet（`MinionModel : MonsterModel`），召唤入口 `MinionCmd.AddMinion<T>(PlayerChoiceContext, Player, MinionSummonOptions options = default)`（`where T : MinionModel`）：
  1. `PlayerCmd.AddPet<T>(player)` 创建随从 Creature；
  2. 写入站位 `minionModel.Position = options.Position`（`MinionPosition` 枚举：Front/Back/FrontUpper/BackUpper/Upper）；
  3. `PetOrderSnapshotManager.TakeSnapshot(player)` 记录当前宠物 CombatId 顺序（供溢出重定向/站位参考）；
  4. `await minion.OnSummon(choiceContext, player, options)` 虚方法回调（mod 可覆写做进场效果）；
  5. `MinionAnimCmd.Rearrange()` 触发摆位动画。
- `MinionSummonOptions`（record struct）：`MaxHp / PrimaryStatAmount / SecondaryStatAmount / TertiaryStatAmount / Source(CardModel?) / Position(默认 Front)`。
- `MinionModel.GenerateMoveStateMachine()` 只生成一个自循环 `MINION_IDLE` 状态（无行为 AI）；`DeathSfx` 固定 `event:/sfx/characters/osty/osty_die`。

### 2. 行动注册与"充能"模型（`ActionModel.cs`）
- 行动本身是 `ActionModel : PowerModel`，`Amount` 即剩余可用次数；行动本体由 `OnAct` 覆写实现。
- 可选开关：`DecrementAfterAct`（执行后 `PowerCmd.Decrement` 扣次数）、`AutoRemoveAtTurnEnd`（自己回合结束 `BeforeSideTurnEnd` 里 `PowerCmd.Remove` 自毁）、`OnlyRespondIconClick`（只能点 Power 图标触发，点随从本体不触发）。

### 3. 触发与目标选择（`ActionClickPatch.cs` / `ActionPowerIconClickPatch.cs`）
- `ActionClickPatch` 在 `NCreature._Ready` 后挂 `Hitbox.GuiInput`；`ActionPowerIconClickPatch` 在 `NPower._Ready` 后挂图标 `GuiInput`。统一走 `TryUseActionAsync`。
- 触发前置校验（不满足即静默返回）：随从存活且有 `CombatId`；战斗进行中且 `PlayerActionsDisabled` 为 false；`ActionQueueSynchronizer.CombatState == PlayPhase`；随从是本地玩家/本地宠物（`PetOwner` 非我、或 `actor.IsPlayer` 非我则忽略）；`actor.CombatState.CurrentSide == actor.Side`（只能在己方回合行动）。
- 行动选择：`actor.Powers.OfType<ActionModel>().FirstOrDefault(p => !IsExhausted(p) && (triggeredFromIcon || !p.OnlyRespondIconClick))`。
- 目标分派：
  - `TargetType.None` → 直接 `TryEnqueue(action, null)`（先 `Flash()`）；
  - 非单目标（All 类）→ 要求 `GetValidTargets().Count > 0`，`TryEnqueue(action, null)`（由 `OnAct` 自行遍历目标）；
  - `TargetType.Self` → 不开 UI，直接 `TryEnqueue(action, null)`（执行时解析为 actor 自身）；
  - 单目标（其他）→ `NTargetManager.Instance.StartTargeting(...)`：自定义 TargetType 时以 `MinionTargetTypes.AnyCreature` + 过滤谓词（谓词里调 `customTargetType.IsValidTarget`）开启；内建类型直接传 `targetType`。期间 `actionPower.StartPulsing()`，结束 `StopPulsing()` 并移除 `TargetingActors` 标记。选中后二次校验 `IsValidTarget` 再 `TryEnqueue`。
- 防重入/防连点：`TargetingActors` HashSet（同一 actor 只允许一次选目标）；`targetManager.LastTargetingFinishedFrame == 当前帧` 时忽略（防止"刚确认上一个选目标释放的鼠标左键"立刻又触发一次点击）；`SetInputAsHandled()` 吞掉输入。

### 4. 入队与执行（`CreatureActionQueueService.cs` / `ExecuteCreatureActionGameAction.cs`）
- 入队：`TryEnqueue` 校验战斗进行、`PlayPhase`、阈值 `TryReserve`（保证排队中的行动数不超过 `Amount`），然后 `new ExecuteCreatureActionGameAction(action, target)` 交给 `queueSynchronizer.RequestEnqueue`（官方联机同步队列）。`RequestEnqueue` 异常时 `Release` 回滚。
- 执行 `ExecuteAction()`（PlayPhase-only 的 GameAction）：解析 `Owner.Creature.CombatState` → `GetCreature(ActorCombatId)` 拿 actor（死亡/不存在则 `Cancel()`）→ 在 `actor.Powers` 里按 `Id` 找回同一 `ActionModel`（丢了或易主则 `Cancel()`）→ 重跑 `CanAct` → 目标解析：`TargetCombatId` 存在时 `await combatState.GetCreatureAsync(TargetCombatId, 10.0)`（10 秒超时）；`TargetType==Self && target==null` 时补为 actor；再校验 `IsValidTarget`/有效目标数 → `action.TryAct(new GameActionPlayerChoiceContext(this), target)`；`TryAct` 返回 false 也 `Cancel()`。`finally` 里必然 `CreatureActionQueueThreshold.Release`。
- `TryAct` 内部：`CanAct` → `ExecuteAct`（`OnAct` → 若 `DecrementAfterAct` 则 `PowerCmd.Decrement` → 若 `CombatManager.Instance.IsInProgress` 则 `CheckWinCondition()`）。

### 5. 死亡清理（`Minion\Patches\MinionKillPatch.cs`）
- 官方 0.107.0 的 `CreatureCmd.KillWithoutCheckingWinCondition()` 只对 **Enemy** 做 CombatManager/CombatState 移除，友方 Minion 死后会残留。
- Patch 为 `[HarmonyPostfix]`，把返回的 Task 包装成 `AwaitAndCleanupAsync`：原 Task 结束后，若 `creature.Side == Player && creature.IsPet && creature.Monster is MinionModel && combatState != null` 且 `Hook.ShouldCreatureBeRemovedFromCombatAfterDeath` 允许，则补调 `CombatManager.Instance.RemoveCreature` + `combatState.RemoveCreature`。

### 6. 回合级清理与全局事件（`Initialization\MinionHookInitializer.cs`，非 Harmony，订阅 `CombatManager` 事件）
- `TurnStarted`：玩家回合开始 → `MinionAnimCmd.Rearrange()`。
- `TurnEnded`：先 `CreatureActionQueueThreshold.Clear()`；再判断 `combatState.CurrentSide == CombatSide.Enemy`（注释：TurnEnded 触发时 CurrentSide 已切换，Enemy 说明刚结束的是玩家回合）→ `Rearrange()`。
- `CombatSetUp` / `CombatEnded`：`Threshold.Clear()` + `PetOrderSnapshotManager.ClearAllSnapshots()`（防内存泄漏，ConditionalWeakTable 整体换新）。

### 7. 守护者（Guardian）机制（`Powers\`）
- 拦截判定：`MinionGuardianPower.ModifyUnblockedDamageTarget` —— 仅前排（`MinionPosition.Front`）守护有效；只拦"指向主人本人"或"指向更靠前的同主守护"的伤害；主人已死、或伤害不带 `ValueProp.Move` / 带 `ValueProp.Unpowered` 时不拦（即不拦非攻击类伤害与已重定向的伤害）。
- 格挡转血：`MinionGuardianBlockToHpPatch` 前缀替换 `CreatureCmd.GainBlock` 完整流程：格挡数值 → `SetMaxHp(+amount)` + `Heal(amount)`，保留全部官方 Hook 与历史记录。
- 溢出重定向链：`MinionGuardianOverkillPatch` 前缀替换 `CreatureCmd.Damage`（仅单目标、目标是存活玩家、Move 且非 Unpowered、玩家有存活前排守护时）：先让 vanilla 流程跑一遍（`SuppressedOwner` 压制主人那段临时掉血），找到第一个被重定向的守护者（守护死亡后 Power 可能已丢失，故用 `PetOrderSnapshotManager.GetSnapshot` 的 CombatId 快照辅助识别）；把 `OverkillDamage` 依次喂给快照顺序里后续的存活前排守护，剩余最后落到主人身上（`props | ValueProp.Unpowered` 防止再次触发守护重定向）；`AsyncLocal<bool> IsHandling` 防递归。

---

## 三、与游戏 API 的交互点（HarmonyPatch 目标汇总）

| 被 Patch 的游戏类型/方法 | Patch | 位置 |
|---|---|---|
| `NCreature._Ready`（Godot 节点） | Postfix：挂 Hitbox 输入 | `Action\Patches\ActionClickPatch.cs` |
| `NPower._Ready`（Godot 节点） | Postfix：挂 Power 图标输入 | `Action\Patches\ActionPowerIconClickPatch.cs` |
| `CreatureCmd.GainBlock(Creature, decimal, ValueProp, CardPlay, bool)` | Prefix：守护者格挡→最大生命+治疗 | `Powers\Patches\MinionGuardianBlockToHpPatch.cs` |
| `CreatureCmd.Damage(PlayerChoiceContext, IEnumerable<Creature>, decimal, ValueProp, Creature, CardModel, CardPlay)` | Prefix：守护者溢出重定向链 | `Powers\Patches\MinionGuardianOverkillPatch.cs` |
| `Creature.LoseHpInternal(decimal, ValueProp)` | Prefix：压制重定向期间主人的临时掉血 | `Powers\Patches\MinionGuardianOwnerDamageSuppressPatch.cs` |
| `CreatureCmd.KillWithoutCheckingWinCondition` | Postfix：补官方缺失的友方随从移除 | `Minion\Patches\MinionKillPatch.cs` |

依赖的非 Harmony 游戏 API：`CombatManager.Instance`（事件 `TurnStarted/TurnEnded/CombatSetUp/CombatEnded`、`IsInProgress/IsOverOrEnding/PlayerActionsDisabled`、`CheckWinCondition/RemoveCreature/History.BlockGained`）、`RunManager.Instance.ActionQueueSynchronizer`（`RequestEnqueue`、`ActionSynchronizerCombatState.PlayPhase`）、`NTargetManager.Instance`（`StartTargeting/SelectionFinished/IsInSelection/LastTargetingFinishedFrame`）、`NCombatRoom.Instance.GetCreatureNode`、`PlayerCmd.AddPet`、`PowerCmd.Decrement/Remove`、`CreatureCmd.SetMaxHp/Heal/Damage`、`SfxCmd/VfxCmd/Cmd.CustomScaledWait`、`LocalContext.IsMe/GetMe`、`Hook.BeforeBlockGained/ModifyBlock/AfterModifyingBlockAmount/AfterBlockGained/ShouldCreatureBeRemovedFromCombatAfterDeath`。

---

## 四、已知限制 / 陷阱

1. **`IsValidTarget` 依赖自定义/内建 TargetType 注册表**（`ActionModel.cs`）：`TryGetCustomTargetType` 查不到定义时一律返回 false（`CustomTypeDefinitions` 以 `BuiltInTargetType.All` 种子初始化，所以内建类型也能解析；但游戏新增 TargetType 未同步时行动会"无有效目标"）。
2. **只能在本方回合行动**：`ActionClickPatch` 要求 `combatState.CurrentSide == actor.Side`；且只在 `PlayPhase`（`ActionQueueSynchronizer`）允许入队，行动类型硬编码 `GameActionType.CombatPlayPhaseOnly`（`ExecuteCreatureActionGameAction.cs`）。
3. **行动次数 = `Amount`，且排队即占位**：`CreatureActionQueueThreshold` 按 (actorCombatId, actionId) 计数，`IsExhausted` 在 `Amount <= 已排队数` 时拒绝；`Release` 只在 `ExecuteAction` 的 `finally` 或 `RequestEnqueue` 抛异常时发生——若 GameAction 构造后从未执行（例如队列被重置），计数只能靠 `MinionHookInitializer` 在回合结束/战斗开始/结束的 `Clear()` 兜底。
4. **`OnlyRespondIconClick` 是过滤而非互斥**：从图标进入时 `preferredAction` 有效则直接用该行动；点随从本体时只挑 `!OnlyRespondIconClick` 的行动。
5. **目标合法性在执行时二次校验，可能静默取消**：执行时 actor 死亡、Power 丢失/易主、目标死亡或不再合法、`TryAct` 返回 false，都会 `Cancel()`（调试日志 `MinionAction` 模块打印原因）；联机目标用 `GetCreatureAsync` 带 10 秒超时。
6. **Self 目标不开选目标 UI**：`TargetType.Self` 直接入队 null 目标，执行时才补 `target = actor`；`TargetType.None` 与多目标（All 类）也都传 null 目标，多目标需要 `OnAct` 自己用 `GetValidTargets` 遍历（`TryAct` 只保证至少 1 个有效目标，否则返回 false 不执行）。
7. **点击与选目标 UI 的交互细节**：选目标期间 `TargetingActors` 防重入；同帧 `LastTargetingFinishedFrame` 防"确认上一个目标后鼠标释放被误判为新点击"；选目标取消（`selectedNode is not NCreature`）不算失败，只是不入队。
8. **守护溢出重定向的边界**（`MinionGuardianOverkillPatch.cs` 注释可见）：守护者死亡后 Power 可能先于结果检查被移除，依赖 `PetOrderSnapshotManager` 的 CombatId 快照识别"第一个被重定向的守护"；快照里找不到该 id（如重定向发生在快照外）时直接把剩余伤害还给主人；重定向伤害打 `ValueProp.Unpowered` 标记避免再次触发守护链。只处理**单目标** Damage（`targetList.Count != 1` 直接放行 vanilla）；`IsHandling` AsyncLocal 防递归。
9. **格挡转血仅在战斗未结束时生效**：`CombatManager.Instance.IsOverOrEnding` 时 `GainBlock` 直接返回 0；且 `amount <= 0`、无 `MinionGuardianPower`、随从已死时直接走原逻辑。转血 = `SetMaxHp(+n)` + `Heal(n)`，因此等价于"永久提升上限"，不是回合性格挡。
10. **官方死亡清理 bug 依赖版本**：`MinionKillPatch` 注释明确针对 0.107.0 官方只清理 Enemy 的问题；清理还受 `Hook.ShouldCreatureBeRemovedFromCombatAfterDeath` 裁决，若 Hook 返回 false 则随从仍会残留。
11. **事件时序陷阱**：`TurnEnded` 触发时 `CurrentSide` 已切换，判断"刚结束的是玩家回合"要用 `CurrentSide == Enemy`（`MinionHookInitializer.cs` 注释明确）。
12. **序列化假设**：`NetExecuteCreatureActionGameAction.Deserialize` 用 `ReadModelIdAssumingType<PowerModel>()`，反序列化方必须保证该 ModelId 解析为 PowerModel（ActionModel 是 PowerModel 子类，成立）；目标/actor 用 6 位 uint 压缩编码。
13. **随从无 AI**：`MinionModel` 的 MoveStateMachine 只有自循环 `MINION_IDLE`，一切行为由 mod 通过 `OnSummon`/`ActionModel.OnAct`/Powers 驱动。
14. **守护判定细节**：`MinionGuardianPower.ModifyUnblockedDamageTarget` 只拦 `ValueProp.Move` 且非 `Unpowered` 的伤害；"拦截同主更靠前的守护"的比较基于 `PlayerCombatState.Pets` 实时顺序（`pets.IndexOf(Owner) < pets.IndexOf(target)` 时不拦，即只挡比自己靠后的守护目标）；主人与守护同时在场时先挡主人的，再按宠物顺序依次挡后续守护。
15. **调试日志默认关闭**：`Debug` 全部 `[Conditional("DEBUG")]`，正式发布 build 无任何 `MinionAction` 日志。
