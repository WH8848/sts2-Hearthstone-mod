# MinionLib 核心模块源码精读笔记（工坊版 0.6.2）

> 精读范围：`E:\MOD\sts2\MinionLib\MinionLib\` 下 `Minion`、`Commands`、`Initialization`、`Utilities` 四个目录（全量），
> 并延伸阅读 `Layout`、`Action`、`Targeting`、`Powers`（随从机制必需）。所有类均在命名空间 `MinionLib.*` 下。

---

## 一、公共类型清单

### Minion/（命名空间 `MinionLib.Minion`）

| 类型 | 一句话用途 | 关键成员 |
|---|---|---|
| `abstract class MinionModel : MonsterModel`（Minion/MinionModel.cs） | 所有随从的基类；**直接继承游戏怪物模型**，随从本质是挂在玩家侧、带 `PetOwner` 的 Pet 怪物 | `override string DeathSfx`（osty 死亡音效）、`override bool HasDeathSfx => true`；`MinionPosition Position { get; internal set; }`；`protected override MonsterMoveStateMachine GenerateMoveStateMachine()`（只生成 `MINION_IDLE` 自循环状态机，随从**永不自发行动**）；`virtual Task OnSummon(PlayerChoiceContext, Player owner, MinionSummonOptions)`（召唤钩子，默认空实现） |
| `readonly record struct MinionSummonOptions` | 召唤参数 | `decimal? MaxHp / PrimaryStatAmount / SecondaryStatAmount / TertiaryStatAmount`；`CardModel? Source`；`MinionPosition Position = Front` |
| `enum MinionPosition` | 随从站位槽位 | `Front=0, Back, FrontUpper, BackUpper, Upper` |

### Commands/（命名空间 `MinionLib.Commands`）

| 类型 | 一句话用途 | 关键成员 |
|---|---|---|
| `static class MinionCmd`（Commands/MinionCmd.cs） | **召唤入口**：调游戏 `PlayerCmd.AddPet<T>` 造随从，设置站位、快照顺序、触发 `OnSummon`、重排动画 | `static Task<Creature> AddMinion<T>(PlayerChoiceContext, Player player, MinionSummonOptions options = default) where T : MinionModel` |
| `static class MinionAnimCmd`（Commands/MinionAnimCmd.cs） | 随从布局动画（Godot Tween） | `static Task Rearrange(bool animated = true, float duration = 0.25f)`；`static void InstantMove(IEnumerable<MinionNodePosition>)`；`static Task AnimatedMove(IEnumerable<MinionNodePosition>, float duration = 0.25f)`（全局单 Tween，会 Kill 上一个）；`static Task PlayBumpAttackAsync(Creature attacker, Creature target, Action? onHit = null)`（撞击攻击动画，命中回调） |
| `static class PetOrderSnapshotManager`（Commands/PetOrderSnapshotManager.cs） | 用 `ConditionalWeakTable<Player, SnapshotEntry>` 记录玩家宠物 CombatId 顺序（防内存泄漏、随玩家 GC） | `static void TakeSnapshot(Player)`；`static IReadOnlyList<Creature> GetSnapshot(Player, bool onlyAlive = true, bool includeMissing = true)`（无 CombatId 的宠物追加到尾部）；`static void ClearAllSnapshots()` |

### Initialization/（命名空间 `MinionLib.Initialization`）

| 类型 | 一句话用途 | 关键成员 |
|---|---|---|
| `static class MinionHookInitializer`（Initialization/MinionHookInitializer.cs） | 订阅 `CombatManager.Instance` 全局事件，在回合开始/结束、战斗开始/结束时自动重排并清理缓存 | `static void Initialize() / Deinitialize()`；`OnTurnStarted`（玩家回合开始→重排）；`OnTurnEnded`（清行动阈值；**此时 CurrentSide 已切换**，靠 `CurrentSide == Enemy` 判断刚结束的是玩家回合→重排）；`OnCombatSetUp / OnCombatEnded`（清阈值 + 清宠物顺序快照） |

### Utilities/（命名空间 `MinionLib.Utilities` 及子命名空间）

| 类型 | 一句话用途 | 关键成员 |
|---|---|---|
| `enum DescriptionPreviewType`（Utilities/BetterExtraArgs/DescriptionPreviewType.cs） | 描述预览类型（复制游戏内部枚举） | `None, Upgrade` |
| `interface IBetterAddExtraArgsCard`（Utilities/BetterExtraArgs/） | 卡片可重载"追加描述参数"（在游戏 `AddExtraArgsToDescription` 之后追加） | `void BetterAddExtraArgsToDescription(LocString description, PileType pileType, DescriptionPreviewType previewType, Creature? target = null)` |
| `static class BetterExtraArgsPatch`（Utilities/BetterExtraArgs/BetterExtraArgsPatch.cs） | Transpiler 补丁：在 `CardModel.GetDescriptionForPile` 中 `AddExtraArgsToDescription` 调用后注入 `TryBetterAddExtraArgs` | `MethodBase TargetMethod()`（动态取 `CardModel` 内部枚举 `DescriptionPreviewType` 定位方法）；`IEnumerable<CodeInstruction> Transpiler(...)` |
| `interface ICustomGlowColorCard`（Utilities/CustomGlowColor/） | 卡片自定义辉光颜色 | `Color? GlowColor { get; }` |
| `static class CustomGlowColorPatch`（Utilities/CustomGlowColor/CustomGlowColorPatch.cs） | 将自定义辉光色应用到手牌高亮/闪光节点 | Postfix 于 `NHandCardHolder.UpdateCard`、`NHandCardHolder.Flash` |
| `interface IDescriptionPostProcessCard`（Utilities/DescriptionPostProcess/） | 卡片对最终描述字符串做后处理 | `string PostProcessDescription(string description, PileType pileType, DescriptionPreviewType previewType, Creature? target = null)` |
| `static class DescriptionPostProcessPatch`（Utilities/DescriptionPostProcess/DescriptionPostProcessPatch.cs） | Postfix 替换 `GetDescriptionForPile` 的返回描述 | 同上 `TargetMethod()`（与 BetterExtraArgs 同一目标方法） |
| `class NetBombConsoleCmd : AbstractConsoleCmd`（Utilities/NetBombConsoleCmd.cs） | 仅 `DEBUG/EXPORTDEBUG` 编译的联机测试控制台命令 | `CmdName => "netbomb"`、`IsNetworked => true`、`Process(...)` 给本地玩家 +5 格挡 |
| `class PetsOrderAccessor : IDisposable`（Utilities/PetsOrderAccessor.cs） | 安全重排宠物的访问器：暴露原生 `Pets` 列表，`Dispose()` 时校验数量未变并重新快照+重排 | `readonly List<Creature>? Pets`；`void Dispose()`（数量变化则抛 `InvalidOperationException`）；`void SetManualRearranged(bool = true)`；`static List<Creature>? GetRawPetsList(Player)` |

### 机制相关（延伸目录，非指定范围）

| 类型 | 一句话用途 | 关键成员 |
|---|---|---|
| `abstract class ActionModel : PowerModel`（Action/ActionModel.cs，`MinionLib.Action`） | **随从行动即"力量"**：行动实现为挂在随从身上的 Power | `abstract TargetType TargetType`；`virtual bool AutoRemoveAtTurnEnd/DecrementAfterAct/OnlyRespondIconClick`；`virtual bool CanAct(ICombatState)`（Amount>0 且存活且同战斗）；`bool IsValidTarget(ICombatState, Creature?)`、`IReadOnlyList<Creature> GetValidTargets(ICombatState)`；`Task<bool> TryAct(PlayerChoiceContext, Creature?)`（校验→`OnAct`→可选 `PowerCmd.Decrement`→`CheckWinCondition`）；`abstract Task OnAct(...)`；`override Task BeforeSideTurnEnd(...)`（AutoRemoveAtTurnEnd 时 `PowerCmd.Remove`） |
| `internal static class CreatureActionQueueService`（Action/CreatureActionQueueService.cs） | 把行动入队到联机动作队列 | `static bool TryEnqueue(ActionModel action, Creature? target)`（要求 `ActionQueueSynchronizer` 处于 PlayPhase，先 `CreatureActionQueueThreshold.TryReserve`，再 `RequestEnqueue(new ExecuteCreatureActionGameAction(...))`，失败 Release） |
| `internal static class CreatureActionQueueThreshold`（Action/CreatureActionQueueThreshold.cs） | 按 `(actorCombatId, actionId)` 统计已入队次数，防止超过 `Amount` 重复入队 | `IsExhausted / TryReserve / Release / Clear` |
| `sealed class ExecuteCreatureActionGameAction : GameAction`（Action/GameActions/） | 入队的行动实体：序列化 actor/target CombatId + 行动 ModelId，执行时重新校验 | `override ulong OwnerId`（NetId）；`ActionType => CombatPlayPhaseOnly`；`ExecuteAction()`（actor 存活→行动仍存在→`CanAct`→目标仍有效→`TryAct`，finally Release 阈值）；`ToNetAction()` |
| `struct NetExecuteCreatureActionGameAction : INetAction`（Action/GameActions/） | 联机传输结构 | `uint ActorCombatId; ModelId ActionModelId; uint? TargetCombatId`；`Serialize/Deserialize` |
| `static class ActionClickPatch`（Action/Patches/ActionClickPatch.cs） | 点击生物（随从/玩家）触发行动：鼠标+手柄，含自选目标流程 | 连接到 `NCreature.Hitbox.GuiInput`；`TargetType.None`/多目标/Self 直接入队；单目标走 `NTargetManager.StartTargeting`（自定义目标类型用 `MinionTargetTypes.AnyCreature` + 过滤谓词） |
| `static class ActionPowerIconClickPatch`（Action/Patches/ActionPowerIconClickPatch.cs） | 点击行动力量图标触发行动 | 连接 `NPower.GuiInput`，转发 `ActionClickPatch.TryUseActionFromIconAsync` |
| `static class MinionLayoutManager`（Layout/，`MinionLib.Layout`） | 布局器注册与计算（按 priority 降序、同优先级按注册序） | `Register(IMinionLayout, int priority = 0)`（默认注册 `DefaultMinionLayout`）；`IEnumerable<MinionNodePosition> CalculateLayout(NCombatRoom)`；`GetCurrentMinionPositions(NCombatRoom)` |
| `interface IMinionLayout` + `record struct OwnerWithMinionsNodes / MinionNodePosition`（Layout/IMinionLayout.cs） | 布局插件接口与数据结构 | `bool IsActive`；`void ApplyLayout(MinionLayoutContext)` |
| `class MinionLayoutContext`（Layout/MinionLayoutContext.cs） | 布局计算上下文（未处理随从 = 没被赋位置的） | `NCombatRoom Room`、`IReadOnlyList<NCreature> AllMinions`、`Dictionary<NCreature, Vector2> Positions`、`IEnumerable<NCreature> UnhandledMinions` |
| `class DefaultMinionLayout`（Layout/DefaultMinionLayout.cs） | 默认布局：按 PetOwner 分组→按 `MinionPosition` 分组→网格点×`MinionSize(150,200)`+基准偏移+主人节点位置 | `static IReadOnlyList<Vector2> GenerateGridPoints(MinionPosition, int count)`（Upper 两列折行，其余前/后弯折排布）；`static Vector2 CalculateBaseOffset(MinionPosition, ILookup<...>)`（含 FrontUpper/BackUpper 避让 Upper 的逻辑） |
| `static class NCreatureExtensions`（Layout/NCreatureExtension.cs） | 判断是否随从节点 | `static bool IsMinionNode(this NCreature)` → `Entity is { Monster: MinionModel, IsAlive: true, PetOwner: not null }` |
| `static class CustomTargetTypeManager`（Targeting/CustomTargetTypeManager.cs，`MinionLib.Targeting`） | 自定义目标类型注册表 | `static TargetType Register(ICustomTargetType, string @namespace, string name)`（**32 位 FNV-1a 哈希**作 TargetType）；`Register(customTargetType, [CallerArgumentExpression]...)`（自动取调用者声明类型命名空间第一段）；`IsCustomTargetType / TryGetCustomTargetType` |
| `interface ICustomTargetType`、`abstract class CustomTargetType`（Targeting/） | 自定义目标类型接口/基类 | `bool IsSingleTarget`；`IsValidTargetPreview(Creature)`；分别对 `CardModel / PotionModel / ActionModel` 的 `IsValidTarget` |
| `static class MinionTargetTypes`（Targeting/MinionTargetTypes.cs） | 内置随从目标类型常量 | `AnyMinion / AllMinions / Itself / AnyCreature / AllCreatures / AnyMinionOrOwner / Void`（命名空间 `MinionLib` 下注册） |
| `static class BuiltInTargetType`（Targeting/Utilities/BuiltInTargetType.cs） | 把游戏原生 `TargetType` 映射为 `ICustomTargetType`（**重定义了 Self/AnyAlly/AllAllies 的随从语义**） | 字典 `All`；`From(TargetType)` |
| `sealed class MinionGuardianPower : PowerModel`（Powers/MinionGuardianPower.cs，`MinionLib.Powers`） | 守护随从：把对主人的未格挡伤害重定向到 Front 位守护者 | `ModifyUnblockedDamageTarget(...)`：仅 Front 位、目标需为主人或更靠前的守护者、且 `ValueProp.Move` 且非 `Unpowered` 才接管 |

---

## 二、核心机制说明

### 1. 注册与初始化
- `MainFile.Initialize()`（MainFile.cs，`[ModInitializer]`）：`new Harmony("MinionLib").PatchAll()` + `MinionHookInitializer.Initialize()`。
- **无需额外注册即可用随从**：只要 `T : MinionModel`，`MinionCmd.AddMinion<T>` 即可召唤；行动/布局/目标类型按需注册。

### 2. 召唤流程（Commands/MinionCmd.cs `AddMinion<T>`）
1. `PlayerCmd.AddPet<T>(player)`——游戏 API，在玩家侧创建 Pet 怪物（写入 `PlayerCombatState.Pets`）；
2. `minionModel.Position = options.Position`——设定站位槽；
3. `PetOrderSnapshotManager.TakeSnapshot(player)`——记录当前宠物 CombatId 顺序（守护/重排依赖）；
4. `minion.OnSummon(choiceContext, player, options)`——mod 钩子（可加行动、赋属性）；
5. `_ = MinionAnimCmd.Rearrange()`——布局动画（fire-and-forget）。
- 召唤同时触发 `NCombatRoom.AddCreature` 补丁（见下）。

### 3. 行动机制（Action/，随从行为 = Power 实例）
- `ActionModel : PowerModel`：随从"会做的事"就是挂在其身上的力量；`Amount` 即行动次数/层数；力量图标可点击。
- 触发链：点击随从 `Hitbox`（`ActionClickPatch`，patch `NCreature._Ready` 后连接 `GuiInput`，鼠标左键释放/手柄 `select`）或点击行动力量图标（`ActionPowerIconClickPatch`，patch `NPower._Ready`）。
- `ActionClickPatch.TryUseActionAsync`：
  - 本地玩家限定（`PetOwner != null && !LocalContext.IsMe(PetOwner)` 直接 return）；要求战斗进行中、`PlayerActionsDisabled == false`、队列处于 PlayPhase；
  - 选第一个未被阈值耗尽的 `ActionModel`；`CanAct` 校验；
  - `TargetType.None` / 多目标 / `Self` → 直接 `CreatureActionQueueService.TryEnqueue(action, null)`（Self 在执行时解析为 actor 自身）；
  - 单目标 → `NTargetManager.StartTargeting(...)`（自定义类型走 `MinionTargetTypes.AnyCreature` + 过滤谓词），选择后再次 `IsValidTarget` 校验再入队；`TargetingActors` HashSet 防止同一随从重复发起选择。
- 入队：`CreatureActionQueueService.TryEnqueue` → 阈值 `TryReserve` → `RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue`（仅 PlayPhase）→ 广播 `NetExecuteCreatureActionGameAction`。
- 执行（本地或远端）：`ExecuteCreatureActionGameAction.ExecuteAction` 重新按 CombatId 解析 actor、按 ModelId 找行动力量，逐项校验（存活/存在/CanAct/目标有效）后 `action.TryAct` → `OnAct`；`DecrementAfterAct` 则 `PowerCmd.Decrement`；随后 `CombatManager.CheckWinCondition`；finally 释放阈值计数。
- `BeforeSideTurnEnd`：`AutoRemoveAtTurnEnd` 的随从行动在回合结束自动移除。

### 4. 死亡清理（Minion/Patches/MinionKillPatch.cs）
- 背景：官方 0.107.0 的 `CreatureCmd.KillWithoutCheckingWinCondition` 只对 Enemy 做 `CombatManager/CombatState` 移除，友方随从死后残留。
- 做法：`[HarmonyPostfix]` 包裹返回的 `Task`，`await` 原任务后，对 `Side == Player && IsPet && Monster is MinionModel && CombatState != null` 的随从，若 `Hook.ShouldCreatureBeRemovedFromCombatAfterDeath` 为真则 `CombatManager.Instance.RemoveCreature(creature)` + `combatState.RemoveCreature(creature)`。

### 5. 目标选择（Targeting/）
- 统一入口 `ActionModel.IsValidTarget/GetValidTargets` → `CustomTargetTypeManager.TryGetCustomTargetType(TargetType, ...)`。
- 原生目标类型经 `BuiltInTargetType` 映射为 `ICustomTargetType`（LambdaTargetType 实现）；其中 `Self`/`AnyAlly`/`AllAllies` 对卡片/药水**排除随从**（避免把随从当"队友"目标），对行动则按随从语义（`target.Player == action.Owner.PetOwner || target == action.Owner` 等）。
- 自定义目标类型注册：`CustomTargetTypeManager.Register(type, "命名空间", "名字")` → FNV 哈希成 `TargetType`；`MinionTargetTypes` 提供现成常量；`AnyMinion` 系类型自带 owner 锁定（卡片/药水只能选 `target.PetOwner == card.Owner` 的随从，Action 可允许同队随从）。
- 卡片出牌管线补丁：`CustomTargetTypeCardPatch` patch `ActionTargetExtensions.IsSingleTarget`、`NTargetManager.AllowedToTargetCreature`、`CardModel.IsValidTarget`、`NCardPlay.TryPlayCard`、`ShowMultiCreatureTargetingVisuals`、`NMouseCardPlay.MultiCreatureTargeting`、`NControllerCardPlay.MultiCreatureTargeting/SingleCreatureTargeting`（手柄自选目标：连接 hover 信号 + `RestrictControllerNavigation`）。
- 药水管线补丁：`CustomTargetTypePotionPatch` patch `NPotionHolder.UsePotion`、`NPotionHolder.TargetNode`、`NPotionPopup._Ready`（按钮文案 throw/drink 按类型切换）。

### 6. 布局/站位
- `MinionInteractablePatch`（patch `NCombatRoom.AddCreature`）：Prefix 记录当前随从位置（`__state`），Postfix 先 `MinionAnimCmd.InstantMove(__state)` 稳住旧位置，再把新随从节点位置设为**主人节点位置**（随后由 `Rearrange` 重新计算）。
- `MinionLayoutManager.CalculateLayout`：按 priority 依次让活跃布局器 `ApplyLayout`，未处理随从留给下一布局器（后处理器用负 priority）。
- `DefaultMinionLayout`：按 `PetOwner` 分组→按 `MinionPosition` 分组→`GenerateGridPoints`（Upper 双列，其余按前/后弯折）+ `CalculateBaseOffset`（含 Upper 数量影响的避让）+ 主人节点位置。
- 重排时机：召唤后、玩家回合开始（`OnTurnStarted`）、玩家回合结束（`OnTurnEnded`）。

### 7. 可点击性与守护
- `MinionInteractablePatch2`（patch `NCreature.ToggleIsInteractable`）：本地玩家的随从强制 `on = true` 保持可点击。
- `PersonalHivePowerPatch`（patch `PersonalHivePower.AfterDamageReceived`）：官方硬编码只认 Osty，把任何带 PetOwner 的宠物攻击者替换为其主人，防 NRE。
- `MinionGuardianPower` + 三个守护补丁：格挡转 MaxHp+治疗（`CreatureCmd.GainBlock` Prefix 重写全流程）；溢出伤害按宠物快照顺序级联到后续守护者再落到主人（`CreatureCmd.Damage` Prefix，`AsyncLocal SuppressedOwner` 抑制主人临时掉血，`Creature.LoseHpInternal` Prefix 配合）。

---

## 三、与游戏 API 的交互点（Harmony 补丁清单）

| 文件 | Patch 目标（游戏类/方法） | 类型 |
|---|---|---|
| Minion/Patches/MinionInteractablePatch.cs | `NCreature.ToggleIsInteractable` | Prefix |
| Minion/Patches/MinionInteractablePatch.cs | `NCombatRoom.AddCreature` | Prefix + Postfix |
| Minion/Patches/MinionKillPatch.cs | `CreatureCmd.KillWithoutCheckingWinCondition` | Postfix（包裹 Task 补清理） |
| Minion/Patches/PersonalHivePowerPatch.cs | `PersonalHivePower.AfterDamageReceived` | Prefix |
| Action/Patches/ActionClickPatch.cs | `NCreature._Ready` | Postfix（连接 Hitbox 输入） |
| Action/Patches/ActionPowerIconClickPatch.cs | `NPower._Ready` | Postfix（连接力量图标输入） |
| Utilities/BetterExtraArgs/BetterExtraArgsPatch.cs | `CardModel.GetDescriptionForPile(PileType, 内部枚举 DescriptionPreviewType, Creature)` | Transpiler |
| Utilities/DescriptionPostProcess/DescriptionPostProcessPatch.cs | 同上 | Postfix |
| Utilities/CustomGlowColor/CustomGlowColorPatch.cs | `NHandCardHolder.UpdateCard`、`NHandCardHolder.Flash` | Postfix |
| Targeting/Patches/CustomTargetTypeCardPatch.cs | `ActionTargetExtensions.IsSingleTarget`；`NTargetManager.AllowedToTargetCreature`；`CardModel.IsValidTarget`；`NCardPlay.TryPlayCard`；`NCardPlay.ShowMultiCreatureTargetingVisuals`；`NMouseCardPlay.MultiCreatureTargeting`；`NControllerCardPlay.MultiCreatureTargeting`；`NControllerCardPlay.SingleCreatureTargeting` | Postfix / Prefix / Prefix 接管 |
| Targeting/Patches/CustomTargetTypePotionPatch.cs | `NPotionHolder.UsePotion`、`NPotionHolder.TargetNode`、`NPotionPopup._Ready` | Prefix / Prefix / Postfix |
| Powers/Patches/MinionGuardianBlockToHpPatch.cs | `CreatureCmd.GainBlock(Creature, decimal, ValueProp, CardPlay, bool)` | Prefix（整体接管） |
| Powers/Patches/MinionGuardianOverkillPatch.cs | `CreatureCmd.Damage(PlayerChoiceContext, IEnumerable<Creature>, decimal, ValueProp, Creature, CardModel, CardPlay)` | Prefix（整体接管） |
| Powers/Patches/MinionGuardianOwnerDamageSuppressPatch.cs | `Creature.LoseHpInternal(decimal, ValueProp)` | Prefix |
| （非本次范围，仅记录）Component/、RightClick/、Targeting/Utilities 亦有多处 Patch | `CardModel.FromSerializable`、`AbstractModel.InitId`、`LocString.GetRawText`、`LocManager.SetLanguage`、`NPlayerHand.AddCardHolder`、`NPotion._Ready`、`NPower._Ready`、`NRelic._Ready`、`NetFullCombatState.ToString`、`CardModel.UpdateDynamicVarPreview/FinalizeUpgradeInternal` 等 | — |

事件订阅（非 Harmony）：`CombatManager.Instance` 的 `TurnStarted / TurnEnded / CombatSetUp / CombatEnded`（Initialization/MinionHookInitializer.cs）。

---

## 四、已知限制 / 陷阱

1. **官方 0.107.0 死亡清理 Bug 需补丁兜底**：`CreatureCmd.KillWithoutCheckingWinCondition` 只清理 Enemy；`MinionKillPatch`（Minion/Patches/MinionKillPatch.cs）靠 Postfix 包裹 Task 后补清理——依赖 `Hook.ShouldCreatureBeRemovedFromCombatAfterDeath` 判定，其他 mod 若自行处理死亡需注意时序。
2. **`TurnEnded` 时 `CurrentSide` 已切换**：`MinionHookInitializer.OnTurnEnded` 用 `CurrentSide == Enemy` 反推"刚结束的是玩家回合"（Initialization/MinionHookInitializer.cs），不要直接读 CurrentSide 判断当前回合。
3. **动画全局单 Tween**：`MinionAnimCmd.AnimatedMove` 会强制 `EmitSignal(Finished)` + `Kill()` 上一个 Tween（并发 `Rearrange` 会互相打断）；`InstantMove` 跳过动画（Commands/MinionAnimCmd.cs）。
4. **行动队列阈值是进程内静态字典**：`CreatureActionQueueThreshold` 仅在回合结束/战斗开始/结束时 `Clear()`；若在异常路径离开战斗可能残留计数；键依赖 `CombatId`（`CombatId == null` 时按耗尽处理）（Action/CreatureActionQueueThreshold.cs）。
5. **`ExecuteCreatureActionGameAction` 构造即抛异常**：actor 无 CombatId、目标无 CombatId、或 `ResolveQueueOwner`（PetOwner > Player > LocalContext.GetMe）解析不到玩家时抛 `InvalidOperationException`——非本地怪物的行动入队会直接失败（Action/GameActions/ExecuteCreatureActionGameAction.cs）。
6. **行动是 Power**：`ActionModel : PowerModel` 受力量堆叠/序列化规则约束；`DecrementAfterAct` 用 `PowerCmd.Decrement`，注意 `Amount` 即可用次数（Action/ActionModel.cs）。
7. **随从永不自发行动**：`MinionModel.GenerateMoveStateMachine` 只有 `MINION_IDLE` 自循环，所有行为必须靠 `ActionModel` 力量或 `OnSummon` 钩子驱动（Minion/MinionModel.cs）。
8. **`CustomTargetTypeManager.Register` 自动命名空间只取第一段**：`CallerArgumentExpression` 版本用调用者 `DeclaringType.FullName.Split('.').First()`，嵌套命名空间会被截断（如 `MinionLib.Targeting` → `MinionLib`）；跨 mod 哈希碰撞理论存在（FNV-1a 32 位），建议显式传 `@namespace`+`name`（Targeting/CustomTargetTypeManager.cs）。
9. **两个描述补丁依赖游戏内部枚举**：`AccessTools.Inner(typeof(CardModel), "DescriptionPreviewType")` + `GetDescriptionForPile` 三参签名，游戏更新改名/改签名会静默失效（Utilities/BetterExtraArgs、DescriptionPostProcess）。Transpiler 只匹配**第一处** `AddExtraArgsToDescription` 调用，依赖 IL 形态（前一条指令加载 description），脆弱。
10. **`PetsOrderAccessor.Dispose` 会抛异常**：若宠物列表数量变化（用于非重排操作）抛 `InvalidOperationException`；重排后自动重新快照+重排（Utilities/PetsOrderAccessor.cs）。
11. **守护溢出重定向**：`MinionGuardianOverkillPatch` 仅处理单目标伤害，用 `AsyncLocal IsHandling` 防递归；重定向伤害强制加 `ValueProp.Unpowered`（防二次重定向）；守护者死亡后力量消失，靠 `PetOrderSnapshotManager` 的 CombatId 快照识别顺序（Powers/Patches/）。`MinionGuardianBlockToHpPatch` 整体重写 `GainBlock` 全流程，需与游戏版本保持同步。
12. **可点击性仅对本地主人生效**：`ToggleIsInteractable` Prefix 只对 `LocalContext.IsMe(PetOwner)` 的随从强制可点击；`ActionClickPatch` 也只允许本地玩家操作自己的随从（联机下不能点别人的随从）。
13. **`AddCreature` 补丁先 InstantMove 旧位置再对齐主人节点**：新随从节点会被瞬移到主人位置，之后才由 `Rearrange` 动画排布；依赖 `GetCurrentMinionPositions` 的 `__state` 快照，若节点在 Prefix/Postfix 之间失效会跳过（GodotObject.IsInstanceValid 兜底）。
14. **`NetBombConsoleCmd` 仅 DEBUG/EXPORTDEBUG 编译**，发布版不存在（Utilities/NetBombConsoleCmd.cs）。
15. **`MinionInteractablePatch2` 的类名与 `MinionInteractablePatch` 不同但都作用于随从交互**：前者管 ToggleIsInteractable，后者管 AddCreature 位置，别混淆（Minion/Patches/MinionInteractablePatch.cs）。
16. **`AnyCreatureTargetType`/`VoidTargetType`/`ItselfTargetType` 的 `IsValidTargetPreview` 语义**：Void 永假（占位/无目标），Itself 只对 Action 有效（`target == action.Owner`），卡片/药水走基类恒假——"指定自己"只能用于行动（Targeting/Pets/）。
