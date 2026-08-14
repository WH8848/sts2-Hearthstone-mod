# MinionLib（随从系统 mod 库）Action / Powers 源码精读文档

> 版本：工坊版 0.6.2（MinionLib.csproj 未标 Version，库依赖 BaseLib 3.1.7 / RitsuLib 0.4.24，TargetFramework net9.0）
> 范围：`E:\MOD\sts2\MinionLib\MinionLib\Action\` 与 `E:\MOD\sts2\MinionLib\MinionLib\Powers\` 全量精读；为讲清机制交叉精读了 MainFile / Initialization / Minion / Commands / Targeting 相关文件。
> 命名空间约定：库类型均在 `MinionLib.*` 下；随从动作（Action）本身是一个 `PowerModel`。

---

## 一、公共类型清单（Action 目录）

### 1. `MinionLib.Action.ActionModel`（abstract，继承 `PowerModel`）— Action\ActionModel.cs
随从"行动"的抽象基类：把随从的可执行行动建模成一个挂载在随从身上的 Power，玩家点击随从（或点其 Power 图标）触发。
- `public abstract TargetType TargetType { get; }` — 行动的目标类型（None=无目标/单目标/多目标，支持自定义 TargetType）
- `public virtual bool AutoRemoveAtTurnEnd => false` — 回合结束自动移除该行动
- `public virtual bool DecrementAfterAct => false` — 行动后 `PowerCmd.Decrement(this)` 扣一次层数
- `public virtual bool OnlyRespondIconClick => false` — 只响应 Power 图标点击、不响应点随从本体
- `protected override IEnumerable<IHoverTip> ExtraHoverTips` — 追加 "MinionLib-Action" 悬停提示（LocString: `static_hover_tips`/`MinionLib-Action.title|description`）
- `public new void Flash()` — 闪烁（隐藏基类实现）
- `public virtual bool CanAct(ICombatState combatState)` — `Amount > 0 && Owner.IsAlive && Owner.CombatState == combatState`
- `public bool IsValidTarget(ICombatState, Creature? target)` — 走 `CustomTargetTypeManager.TryGetCustomTargetType`；target 必须存活
- `public IReadOnlyList<Creature> GetValidTargets(ICombatState)` — 过滤所有合法目标
- `public async Task<bool> TryAct(PlayerChoiceContext, Creature? target)` — 分发：None→直接执行；单目标→校验目标后执行；多目标→有合法目标才执行；返回是否真的行动
- `private async Task ExecuteAct(...)` — `OnAct` → 若 `DecrementAfterAct` 则 `PowerCmd.Decrement` → `CombatManager.Instance.CheckWinCondition()`（若战斗仍在进行）
- `public override Task BeforeSideTurnEnd(PlayerChoiceContext, CombatSide, IEnumerable<Creature>)` — `AutoRemoveAtTurnEnd` 且属本方时 `PowerCmd.Remove(this)`
- `protected abstract Task OnAct(PlayerChoiceContext, Creature? target)` — 子类实现实际效果

### 2. `MinionLib.Action.CreatureActionQueueService`（internal static）— Action\CreatureActionQueueService.cs
把 Action 排入官方行动队列的入口。
- `public static bool TryEnqueue(ActionModel action, Creature? target)` — 前置校验：`CombatManager.Instance.IsInProgress`、actor 有 `CombatId`、`RunManager.Instance.ActionQueueSynchronizer.CombatState == ActionSynchronizerCombatState.PlayPhase`；再 `CreatureActionQueueThreshold.TryReserve`（失败即满）；构造 `ExecuteCreatureActionGameAction` 并 `queueSynchronizer.RequestEnqueue(queuedAction)`；异常时 `Release` 后 rethrow。

### 3. `MinionLib.Action.CreatureActionQueueThreshold`（internal static）— Action\CreatureActionQueueThreshold.cs
排队额度控制：防止同一行动被重复排队超过其 `Amount`。
- 内部字典 `Dictionary<(uint actorCombatId, ModelId actionId), int> QueuedCount`
- `IsExhausted(ActionModel)` — 已排队数 ≥ Amount 视为耗尽
- `TryReserve(ActionModel)` / `Release(uint actorCombatId, ModelId actionId)` / `Clear()`

### 4. `MinionLib.Action.GameActions.ExecuteCreatureActionGameAction`（sealed，继承 `GameAction`）— Action\GameActions\ExecuteCreatureActionGameAction.cs
排队用的可执行 GameAction（可联网）。
- `public override ulong OwnerId => Owner.NetId`；`ActionType => GameActionType.CombatPlayPhaseOnly`
- 构造1：`(ActionModel action, Creature? target)` — actor 无 CombatId 抛异常；`ResolveQueueOwner` 解析队列归属者：`actor.PetOwner` → `actor.Player` → `LocalContext.GetMe(actor.CombatState)`
- 构造2：`(Player owner, uint actorCombatId, ModelId actionModelId, uint? targetCombatId)` — 反序列化用
- `ExecuteAction()`：执行时二次校验——combatState 为空/actor 已死/`actor.Powers` 中找不到对应 `ActionModel`（按 Id）/`CanAct` 失败/目标失效（`Self` 类型且无目标时回退为 actor）/多目标无合法目标 → 全部 `Cancel()`；通过后 `await action.TryAct(new GameActionPlayerChoiceContext(this), target)`；`finally` 中 `CreatureActionQueueThreshold.Release`（无论成败都释放额度）
- `ToNetAction()` → `NetExecuteCreatureActionGameAction`

### 5. `MinionLib.Action.GameActions.NetExecuteCreatureActionGameAction`（struct，`INetAction`）— Action\GameActions\NetExecuteCreatureActionGameAction.cs
- 字段：`uint ActorCombatId`、`ModelId ActionModelId`、`uint? TargetCombatId`
- `ToGameAction(Player)` → 构造2
- `Serialize(PacketWriter)`：`WriteUInt(ActorCombatId, 6)` + `WriteModelEntry(ActionModelId)` + `WriteBool(TargetCombatId.HasValue)`（有值再 `WriteUInt(...,6)`）
- `Deserialize(PacketReader)`：`ReadUInt(6)` + `ReadModelIdAssumingType<PowerModel>()` + 可选 `ReadUInt(6)`

### 6. `MinionLib.Action.Patches.ActionClickPatch`（static）— Action\Patches\ActionClickPatch.cs
把"点击随从 → 触发行动"接进游戏 UI。
- `[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]` + `[HarmonyPostfix]`：给 `__instance.Hitbox` 连接 `Control.SignalName.GuiInput`
- `OnGuiInput`：鼠标左键释放 或 手柄 `MegaInput.select` 按下且 Hitbox 有焦点；`NTargetManager.Instance.IsInSelection` 时忽略；**同帧链式点击忽略**（`LastTargetingFinishedFrame == GetTree().GetFrame()` 时跳过，避免确认上一个目标的那次释放误触发下一个随从）；`TaskHelper.RunSafely(TryUseActionAsync(...))` 并 `SetInputAsHandled`
- `public static Task TryUseActionFromIconAsync(NCreature, ActionModel actionPower, Vector2 position)` — 图标点击入口（跳过 OnlyRespondIconClick 过滤）
- `TryUseActionAsync` 完整流程：本地/存活/战斗进行/`PlayerActionsDisabled` 否/PlayPhase/`PetOwner` 或 `Player` 必须是本地玩家/`combatState.CurrentSide == actor.Side`（只能在自己的回合点）；选行动（优先 `preferredAction`，否则取第一个未被额度耗尽的 ActionModel，图标触发时忽略 `OnlyRespondIconClick`）；`CanAct`；然后按 TargetType 分支：
  - `None` / 多目标（有合法目标）/ `Self` → 不弹目标 UI，直接 `Flash()` + `TryEnqueue`（Self 走无目标入队，执行时回退为 actor）
  - 单目标 → 静态 `HashSet<uint> TargetingActors` 防重入；`actionPower.StartPulsing()`；自定义 TargetType 用 `NTargetManager.StartTargeting(MinionTargetTypes.AnyCreature, ...)` + 自定义 predicate，否则用原版 TargetType；`await SelectionFinished()`；校验目标后 `TryEnqueue`；`finally` 里 `StopPulsing()` + 移除 TargetingActors

### 7. `MinionLib.Action.Patches.ActionPowerIconClickPatch`（static）— Action\Patches\ActionPowerIconClickPatch.cs
- `[HarmonyPatch(typeof(NPower), nameof(NPower._Ready))]` + `[HarmonyPostfix]`：连接 GuiInput
- 鼠标左键释放且 `powerNode.Model is ActionModel` 时：`NCombatRoom.Instance?.GetCreatureNode(actionPower.Owner)` 取随从节点，调 `ActionClickPatch.TryUseActionFromIconAsync`（起始位置 = 图标位置 + (20,20)）

---

## 二、公共类型清单（Powers 目录）

### 8. `MinionLib.Powers.MinionGuardianPower`（sealed，继承 `PowerModel`）— Powers\MinionGuardianPower.cs
"护卫"力量：我方随从替主人（及排位更靠后的同主人护卫随从）挡下攻击伤害。
- `Type => PowerType.Buff`；`StackType => PowerType.Single`
- `public override Creature ModifyUnblockedDamageTarget(Creature target, decimal amount, ValueProp props, Creature? dealer)`：
  - 自己不是 Front 位随从（`minion.Position != MinionPosition.Front`）→ 不挡
  - 只有目标是 `Owner.PetOwner?.Creature` 才挡；同主人的其他护卫随从：仅当 `pets.IndexOf(Owner) < pets.IndexOf(target)`（自己排位更靠前）才挡
  - `Owner.IsDead` → 不挡；仅挡 `ValueProp.Move` 伤害且**不是** `ValueProp.Unpowered`（即只挡"攻击"且带力量来源的伤害）
  - 满足条件返回 `Owner` 作为新目标

### 9. `MinionLib.Powers.Patches.MinionGuardianBlockToHpPatch`（static）— Powers\Patches\MinionGuardianBlockToHpPatch.cs
护卫随从获得格挡时改为"转化为最大生命+治疗"。
- `[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.GainBlock), typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardPlay), typeof(bool))]` + `[HarmonyPrefix]`：目标有 `MinionGuardianPower`、未死、amount>0 时接管（`__result = GainBlock(...)`，`return false`）
- 自实现 `GainBlock`：`Hook.BeforeBlockGained` → `Hook.ModifyBlock` → `AfterModifyingBlockAmount` → `SfxCmd.Play("event:/sfx/block_gain")` + `VfxCmd.PlayOnCreatureCenter` → `CreatureCmd.SetMaxHp(MaxHp + amount)` + `CreatureCmd.Heal(amount)` → `CombatManager.Instance.History.BlockGained` → 按 fast 等 0~0.03 或 0.1~0.25 秒 → `Hook.AfterBlockGained`；返回 0（不产生真实格挡）

### 10. `MinionLib.Powers.Patches.MinionGuardianOverkillPatch`（static）— Powers\Patches\MinionGuardianOverkillPatch.cs
溢出伤害（Overkill）沿护卫队列重定向。
- `[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Damage), typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel), typeof(CardPlay))]` + `[HarmonyPrefix]`
- `AsyncLocal<bool> IsHandling`（防递归）+ `public static readonly AsyncLocal<Creature?> SuppressedOwner`（给 OwnerDamageSuppressPatch 用）
- `ShouldHandle`：单目标、目标是玩家本人、存活、有 CombatState、`Move && !Unpowered`、玩家有存活的前排护卫
- `HandleWithOverkillRedirect`：设 `IsHandling=true`，`SuppressedOwner=owner` 后先让原版 `CreatureCmd.Damage` 跑一遍（拿原始 DamageResult，含首个护卫的 OverkillDamage，同时抑制 owner 的兜底 HP 损失）；再从 `PetOrderSnapshotManager.GetSnapshot(player, false)` 中取出所有前排护卫 combat id 快照；`firstGuardianResult` 用快照识别（死亡护卫会先丢 Power，只能靠 combat id 认）；把首个护卫的 `OverkillDamage` 依次喂给排位更靠后的存活前排护卫（`props | ValueProp.Unpowered`，避免再次触发本 patch），剩余再打回 owner；所有结果合并返回
- `IsFrontGuardian(Creature)`：有 `MinionGuardianPower` 且（非 `MinionModel` 或 `Position == Front`）

### 11. `MinionLib.Powers.Patches.MinionGuardianOwnerDamageSuppressPatch`（static）— Powers\Patches\MinionGuardianOwnerDamageSuppressPatch.cs
- `[HarmonyPatch(typeof(Creature), nameof(Creature.LoseHpInternal), typeof(decimal), typeof(ValueProp))]` + `[HarmonyPrefix]`
- 当 `__instance == MinionGuardianOverkillPatch.SuppressedOwner.Value` 且 amount>0：返回空 `DamageResult(__instance, props)` 并 `return false`——抑制原版重定向流程里 owner 的临时兜底掉血（稍后由 OverkillPatch 统一重分配）

---

## 三、核心机制说明

### 1. 注册
- `MainFile.Initialize()`（`[ModInitializer]`）：`new Harmony("MinionLib").PatchAll()` 打全部补丁 → `MinionHookInitializer.Initialize()` 订阅 `CombatManager` 全局事件：`TurnStarted`（玩家回合开始 `MinionAnimCmd.Rearrange()` 重排）、`TurnEnded`（`CreatureActionQueueThreshold.Clear()` + 敌方回合开始=刚结束玩家回合时重排）、`CombatSetUp`/`CombatEnded`（清额度 + `PetOrderSnapshotManager.ClearAllSnapshots()` 防内存泄漏）。
- 随从本体 `MinionModel : MonsterModel`（Minion\MinionModel.cs）：覆写 `DeathSfx`（osty 音效）；`MinionPosition Position`（Front/Back/FrontUpper/BackUpper/Upper）；`GenerateMoveStateMachine` 生成单一 `"MINION_IDLE"` 空闲状态机（随从不自己移动）；`virtual Task OnSummon(...)` 召唤钩子；`MinionSummonOptions`（MaxHp/PrimaryStatAmount/SecondaryStatAmount/TertiaryStatAmount/Source/Position）记录结构。

### 2. 召唤
- `MinionCmd.AddMinion<T>(choiceContext, player, MinionSummonOptions)`（Commands\MinionCmd.cs）：`PlayerCmd.AddPet<T>(player)` 生成 Creature → 写入 `Position` → `PetOrderSnapshotManager.TakeSnapshot(player)` → `minion.OnSummon(...)` → `MinionAnimCmd.Rearrange()`。
- UI 侧 `MinionInteractablePatch`（Minion\Patches\MinionInteractablePatch.cs）：`NCreature.ToggleIsInteractable` Prefix 强制本地玩家的随从保持可点击；`NCombatRoom.AddCreature` Prefix 记录当前随从布局、Postfix `MinionAnimCmd.InstantMove` 恢复布局并把新随从节点贴到主人节点位置。

### 3. 行动（Act）全链路
点击随从/图标 → `ActionClickPatch.OnGuiInput` / `ActionPowerIconClickPatch` → `TryUseActionAsync`（本地/回合/额度校验）→ 按 TargetType 分支 → `CreatureActionQueueService.TryEnqueue` → `ExecuteCreatureActionGameAction` 入官方 `ActionQueueSynchronizer`（PlayPhase 限定）→ 执行时二次校验后 `ActionModel.TryAct` → `OnAct`（子类效果）→ 可选 `PowerCmd.Decrement` → `CheckWinCondition`。随从的"行动次数"由 Power 的 `Amount` 承载，排队额度 `CreatureActionQueueThreshold` 保证不会超量排队。

### 4. 死亡清理
- `MinionKillPatch`（Minion\Patches\MinionKillPatch.cs）：`[HarmonyPatch(typeof(CreatureCmd), "KillWithoutCheckingWinCondition")]` Postfix 包装原 Task，完成后若死亡的是玩家方 `IsPet && Monster is MinionModel` 且 `Hook.ShouldCreatureBeRemovedFromCombatAfterDeath` 为真 → `CombatManager.Instance.RemoveCreature` + `combatState.RemoveCreature`。
- 背景（代码注释）：0.107.0 官方该函数只对 Enemy 做 CombatManager/CombatState 移除判定，友方随从死后会残留。

### 5. 目标选择
- `MinionTargetTypes`（Targeting\MinionTargetTypes.cs）：注册 `AnyMinion / AllMinions / Itself / AnyCreature / AllCreatures / AnyMinionOrOwner / Void` 到 `CustomTargetTypeManager.Register(type, nameof(MinionLib), name)`。
- `CustomTargetTypeManager`（Targeting\CustomTargetTypeManager.cs）：`Register` 用 FNV-1a 32 位哈希（`namespace.name`）生成 `TargetType` 枚举值；内置类型字典 + 注册集；`IsCustomTargetType` / `TryGetCustomTargetType(includeBuiltin)`；带 `[CallerArgumentExpression]` 的重载用 StackTrace 自动取命名空间（要求 `[MethodImpl(NoInlining)]`），名称取表达式去空白。
- `CustomTargetTypeCardPatch`（Targeting\Patches\CustomTargetTypeCardPatch.cs）把自定义 TargetType 注入卡片出牌流程（见下节补丁表），随从行动的瞄准则走 `ActionClickPatch` 里的 `NTargetManager.StartTargeting`（自定义类型统一用 `AnyCreature` + predicate 过滤）。

---

## 四、与游戏 API 的交互点（HarmonyPatch 目标全表，35 处）

| 补丁类 | 游戏目标（类.方法） | 方式 | 作用 |
|---|---|---|---|
| ActionClickPatch | `NCreature._Ready` | Postfix | 连接 Hitbox 点击，触发随从行动 |
| ActionPowerIconClickPatch | `NPower._Ready` | Postfix | Power 图标点击触发行动 |
| MinionGuardianBlockToHpPatch | `CreatureCmd.GainBlock(Creature, decimal, ValueProp, CardPlay, bool)` | Prefix | 护卫格挡→最大HP+治疗 |
| MinionGuardianOverkillPatch | `CreatureCmd.Damage(PlayerChoiceContext, IEnumerable<Creature>, decimal, ValueProp, Creature, CardModel, CardPlay)` | Prefix | 溢出伤害沿护卫队列重定向 |
| MinionGuardianOwnerDamageSuppressPatch | `Creature.LoseHpInternal(decimal, ValueProp)` | Prefix | 抑制 owner 兜底掉血 |
| MinionKillPatch | `CreatureCmd.KillWithoutCheckingWinCondition` | Postfix | 补清理死亡友方随从 |
| PersonalHivePowerPatch | `PersonalHivePower.AfterDamageReceived` | Prefix | dealer 非 Osty 的 Pet 改为 PetOwner（防 NRE） |
| MinionInteractablePatch | `NCreature.ToggleIsInteractable` | Prefix | 本地随从强制可交互 |
| MinionInteractablePatch | `NCombatRoom.AddCreature` | Pre+Post | 记录/恢复随从布局 |
| CustomTargetTypeCardPatch | `ActionTargetExtensions.IsSingleTarget` | Postfix | 自定义类型判定单目标 |
| CustomTargetTypeCardPatch | `NTargetManager.AllowedToTargetCreature` | Prefix | 自定义类型瞄准预览 |
| CustomTargetTypeCardPatch | `CardModel.IsValidTarget` | Prefix | 自定义类型合法性 |
| CustomTargetTypeCardPatch | `NCardPlay.TryPlayCard` | Prefix | 接管自定义类型出牌 |
| CustomTargetTypeCardPatch | `NCardPlay.ShowMultiCreatureTargetingVisuals` | Postfix | 多目标视觉 |
| CustomTargetTypeCardPatch | `NMouseCardPlay.MultiCreatureTargeting` | Prefix | 鼠标多→单 |
| CustomTargetTypeCardPatch | `NControllerCardPlay.MultiCreatureTargeting` | Prefix | 手柄多→单 |
| CustomTargetTypeCardPatch | `NControllerCardPlay.SingleCreatureTargeting` | Prefix | 手柄自定义单目标 |
| CustomTargetTypePotionPatch | `NPotionHolder.UsePotion` / `TargetNode` / `NPotionPopup._Ready` | — | 药水自定义目标（略） |
| Component 相关 | `CardModel.UpdateDynamicVarPreview` / `FinalizeUpgradeInternal` / `Description` getter / `FromSerializable`；`LocString.GetRawText`；`LocManager.SetLanguage`；`AbstractModel.InitId`；`NetFullCombatState.ToString` | — | 卡片组件/序列化/本地化（略） |
| 其他 | `NHandCardHolder.UpdateCard` / `Flash`（自定义辉光）；`NRelic._Ready` / `NPower._Ready` / `NPotion._Ready` / `NPlayerHand.AddCardHolder`（右键菜单） | — | 工具类（略） |

---

## 五、已知限制 / 陷阱

1. **Action 即 Power**：`ActionModel` 挂在随从 Powers 上，次数由 `Amount` 承载；删除/叠加走 `PowerCmd`，与其他 Power 共用 UI（图标点击路径）。
2. **执行时二次校验会静默取消**：入队后若 actor 死亡、Power 被移除、目标失效、`CanAct` 失败，`ExecuteCreatureActionGameAction` 直接 `Cancel()`——玩家可能看到"点了没反应"。
3. **额度与 Amount**：`CreatureActionQueueThreshold` 只按"排队数"计，执行失败的排队也会短暂占额度（finally 释放）；额度在 `TurnEnded / CombatSetUp / CombatEnded` 才清。
4. **同帧链式点击被忽略**：`ActionClickPatch` 用 `LastTargetingFinishedFrame` 防误触，确认上一个目标的同一帧释放不会连锁触发下一个随从。
5. **回合限制**：`CurrentSide != actor.Side` 直接 return，且队列要求 PlayPhase——随从行动只能在己方回合。
6. **OnlyRespondIconClick**：置 true 后点随从本体不会触发，只能点图标。
7. **死亡清理只补一条路径**：`MinionKillPatch` 只包装 `KillWithoutCheckingWinCondition`；其他死亡路径（如直接 `Kill`）可能仍残留随从（0.107.0 的官方行为）。
8. **Guardian 只挡"攻击"**：`ModifyUnblockedDamageTarget` 要求 `ValueProp.Move` 且非 `Unpowered`；格挡转化同理。毒/烧等非 Move 伤害不挡。
9. **OverkillPatch 只处理单目标伤害**：`targetList.Count != 1` 直接放行；多目标伤害不会重定向溢出。
10. **OverkillPatch 的递归防护**：重定向用 `props | ValueProp.Unpowered` 绕过自己 + `AsyncLocal IsHandling`；若其他 mod 也 patch `CreatureCmd.Damage` 需注意顺序。
11. **死亡护卫丢 Power 问题**：护卫死后 Power 可能在结果检查前消失，`firstGuardianResult` 识别依赖 `PetOrderSnapshotManager` 的 combat id 快照（`GetSnapshot(player, false)`，含已死者）；无 combat id 的随从不参与。
12. **Guardian 排位**：同主人多护卫时只有 `pets.IndexOf(Owner) < pets.IndexOf(target)` 的（更靠前的）才挡；非 Front 位的 `MinionModel` 护卫不挡。
13. **自定义 TargetType 哈希注册**：`CustomTargetTypeManager.Register` 用 `namespace.name` 的 FNV-1a 哈希；同名/命名空间冲突会 `Add` 重复 key 抛异常；自动命名空间重载依赖 StackTrace（NoInlining）。
14. **调试日志**：`DebugLogger.Debug` 全部 `[Conditional("DEBUG")]`，发布版无日志。
15. **网络**：`NetExecuteCreatureActionGameAction.Deserialize` 用 `ReadModelIdAssumingType<PowerModel>()`，要求 ID 已按 PowerModel 注册；队列归属者解析顺序 PetOwner → Player → LocalContext。
16. **Guardian 格挡转化**：`BlockToHpPatch` 复刻了格挡 Hook 链但**返回 0 格挡**（`__result = 0`），依赖该函数返回值计算的其他逻辑（如格挡相关卡牌）可能受行为差异影响；`fast` 参数只影响等待时长。

---

## 六、文件名索引（回查用）

```
Action\ActionModel.cs
Action\CreatureActionQueueService.cs
Action\CreatureActionQueueThreshold.cs
Action\GameActions\ExecuteCreatureActionGameAction.cs
Action\GameActions\NetExecuteCreatureActionGameAction.cs
Action\Patches\ActionClickPatch.cs
Action\Patches\ActionPowerIconClickPatch.cs
Powers\MinionGuardianPower.cs
Powers\Patches\MinionGuardianBlockToHpPatch.cs
Powers\Patches\MinionGuardianOverkillPatch.cs
Powers\Patches\MinionGuardianOwnerDamageSuppressPatch.cs
-- 交叉引用 --
MainFile.cs（注册/Harmony.PatchAll）
Initialization\MinionHookInitializer.cs（回合事件订阅/额度与快照清理）
Minion\MinionModel.cs（MinionModel/MinionSummonOptions/MinionPosition）
Minion\Patches\MinionKillPatch.cs（死亡清理）
Minion\Patches\MinionInteractablePatch.cs（可交互/布局）
Minion\Patches\PersonalHivePowerPatch.cs（Osty 硬编码修复）
Commands\MinionCmd.cs（AddMinion 召唤入口）
Commands\PetOrderSnapshotManager.cs（随从顺序快照）
Commands\MinionAnimCmd.cs（重排动画）
Targeting\MinionTargetTypes.cs / CustomTargetTypeManager.cs / Patches\CustomTargetTypeCardPatch.cs（目标系统）
Layout\MinionLayoutManager.cs / MinionLayoutContext.cs / DefaultMinionLayout.cs（布局算法）
```
