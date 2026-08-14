# MinionLib 0.6.2（工坊版）源码精读 — Component / Layout / RightClick（m4）

> 精读范围：`E:\MOD\sts2\MinionLib\MinionLib\{Component, Layout, RightClick}` 三目录（共 50 个 .cs）。
> 随从本体（MinionModel、召唤/行动/死亡逻辑）位于 `Minion/`、`Action/`、`Powers/`、`Targeting/` 目录（范围外），本文在核心机制处给出其与三目录的触点与补丁点，便于回查。

---

## 一、公共类型清单

### 1. Component 目录（命名空间 `MinionLib.Component`）

| 类型 | 用途 | 关键成员 |
|---|---|---|
| `CardComponent`（abstract partial，实现 `ICardComponent`） | 卡牌组件基类：每个组件是挂在卡上的"可序列化修饰块"，提供描述前缀/后缀、右击、升级钩子 | `abstract string ComponentId`；`Attach(IComponentsCardModel, bool isInternal=false)` / `Detach(...)`；`virtual ICardComponent DeepClone()`；`virtual bool TryMergeWith(incoming, ApplyComponentOptions, out merged)` / `TrySubtractiveMergeWith`；`virtual void Serialize(ArrayBufferWriter<byte>)` / `bool Deserialize(ref ReadOnlySpan<byte>)`；`DynamicVarSet DynamicVars`（= SmartVars+ExtraVars，懒构建）；`virtual bool ShouldGlowGoldInternal/ShouldGlowRedInternal`、`Color? GlowColor`、`TargetType? ExtraTargetType`、`CardType?/CardRarity? Override`、`IEnumerable<CardTag> ExtraTags`、`bool IsPlayable`、`bool HasTurnEndInHandEffect`、`IEnumerable<IHoverTip> HoverTips`；`GetFormattedPrefix/Postfix(Dictionary<string,object> argsFromCard)`（`LocString` "cards" 表，`ComponentId + ".prefix/.postfix"`，`SmartAddArgs` 注入 EnergyVar 颜色）；`CanHandleRightClick(Local)(RightClickContext)`、`Task OnRightClick(PlayerChoiceContext, RightClickContext)`；`virtual void OnUpgrade/AfterDowngraded(ComponentContext)`；`OnAttach()/OnDetach()` |
| `ComponentsCardModel`（abstract partial，继承 `CardModel`，实现 `IComponentsCardModel`、`IEasyRightClickableCard`、`IBetterAddExtraArgsCard`、`ICustomGlowColorCard`、`IDescriptionPostProcessCard`） | 带组件的卡牌模型基类：**mod 卡继承它**即可获得组件能力；把所有 CardModel 的虚方法 sealed 后转发给组件再交给 `C` 结尾虚方法 | `[SavedProperty(SaveIfNotTypeDefault)] int[] MinionLibComponentStateBlob`（序列化载体，getter 会重序列化）；`protected virtual IEnumerable<ICardComponent> CanonicalComponents`（默认组件）；`sealed override CardType/Rarity/TargetType/Tags/IsPlayable/HasTurnEndInHandEffect/ExtraHoverTips` 等聚合；`IReadOnlyList<ICardComponent> Components`；`AddComponent/SubtractComponent/ApplyComponent<T>(incoming, options)`（合并/减性合并/升级）；`RemoveComponent<T>/RemoveComponents<T>/RefRemoveComponent`；`GetComponent<T>/GetComponents<T>`；`EnsureComponentsInitialized()`（blob 为空→克隆 CanonicalComponents，否则反序列化 blob）；`BetterAddExtraArgsToDescription`（按序拼接每组件前缀 `CompPre`、反序拼接后缀 `CompPost`）；`CanHandleRightClickLocal/OnRightClick`（只让第一个能处理的组件执行）；`DeepCloneFields/AfterDeserialized` 重写；`sealed override OnUpgrade()/AfterDowngraded()`（内部构造 `ComponentContext(ComponentPhase.Core)` 广播给组件） |
| `ComponentExtensions`（static） | 通过 `AccessTools.FieldRefAccess` 反射写 `DynamicVar` 的私有 `WasJustUpgraded` 后备字段 | `SetWasJustUpgraded(this DynamicVar, bool value=true)` |

**`MinionLib.Component.Interfaces`**：

- `ICardComponent`（partial interface）：组件契约。`ComponentId`、`ComponentsCard`、`Card`（= ComponentsCard as CardModel）、`DynamicVars`；默认实现虚成员（`ShouldGlowGoldInternal=false`、`ExtraTargetType=null`、`IsPlayable=true`、`CanHandleRightClick=false`、`OnRightClick=CompletedTask` 等）；`Attach/Detach/DeepClone/TryMergeWith/TrySubtractiveMergeWith/Serialize/Deserialize`；`GetFormattedPrefix/Postfix`；`OnUpgrade/AfterDowngraded(ComponentContext)`。
- `IComponentsCardModel`：卡模型侧契约。`AsCardModel`、`Components`、`AddComponent/SubtractComponent/ApplyComponent/RemoveComponent(s)/RefRemoveComponent/GetComponent(s)`；#region Deprecated：`ComponentCallBack/ComponentPredicate/ComponentQuery/ComponentQueryAsync`（`[Obsolete]`，改用接口约束或 DelegateRegistry）。
- `IGeneratedBinarySerializable`：`Serialize(ArrayBufferWriter<byte>)`、`bool Deserialize(ref ReadOnlySpan<byte>)`、默认 `ToLogString`（反射遍历所有属性）。

**`MinionLib.Component.Core`**：

- `ApplyComponentOptions`（readonly record struct）：`(bool AllowMerge=true, bool UseSubtractiveMerge=false, bool IsUpgrade=false, Dictionary<string,object?>? Extra=null)`。
- `CardComponentRegistry`（static）：组件 ID→类型/工厂注册表。`Register(componentId, type, factory)`（重复 ID 抛异常，同时 `StringIdPool.Register`）；`Create(componentId)`（未知 ID 抛异常）。
- `CardComponentStateSerializer`（static）：组件列表↔`int[]` blob。`Serialize(IReadOnlyList<ICardComponent>)`；`Deserialize(int[] state, IComponentsCardModel? owner)`（未知 ID 跳过并记 Debug、`TrySkipObjectBlock`；反序列化失败跳过）；`DeepClone(component)`（序列化→反序列化）。
- `ComponentDelegateAttribute`（`[AttributeUsage(Method)]`，空参构造）：代码生成器的标记属性，运行时本身不解释。
- `ComponentDescriptionRawCache`（internal static）：`Dictionary<string,string>` locEntryKey→原始描述文本缓存，带锁；`TryGet/Contains/Set/Clear`。
- `ComponentPhase`（enum）：`Init, Prime, Prefix, Core, Postfix, Final`；`ComponentPhaseExtensions.NextPhase()`；`ComponentContext(ComponentPhase phase)`：`Phase`（可 `MoveNextPhase()`）+ `Dictionary<string,object> State`。
- `ComponentStateAttribute`（`[AttributeUsage(Property)]`）：标记组件状态属性（日志/序列化生成用）；泛型 `ComponentStateAttribute<T>`（`T: DynamicVar`，参数数组）。
- `DelegateRegistry`（static）：`Register<T>(name, del)`（`T: Delegate`，注册时进 StringIdPool）；`Get<T>(name)`。
- `LocArgAttribute`（`[AttributeUsage(Property)]`，`string? Name`）、`NotLocArgAttribute`、`NestedLocStringAttribute`：本地化参数标记。
- `NoGeneratedSerializationAttribute`（`[AttributeUsage(Class, Inherited=false)]`）：类级"跳过自动生成序列化"开关。
- `SerializationUtils`（static）：自研紧凑二进制读写（LEB128 varint/zigzag、定长小端、decimal 标志位打包、字符串标签编码、对象块包裹、JSON、IPacketSerializable 包裹）。`WriteObjectBlock/TryReadObjectBlock/TrySkipObjectBlock/WriteSerializableBlock/TryReadSerializableBlock/ToIntArray/TryFromIntArray/Write(Read)Count/Boolean/Byte/Int16..64/UInt16..64/Single/Double/Decimal/String/Json<T>/IPacketSerializable<T>`。
- `StringIdPool`（static）：64 位 FNV-1a 风格哈希（`hash<<1` 做 ID）的字符串↔ID 双向池。`Register`（**哈希碰撞抛异常**）、`TryGetId`、`TryGetString`。
- `StringIdPoolCollectorPatch`（static，Harmony）：见补丁表。

**`MinionLib.Component.Extensions`**：

- `ComponentsBlobLogExtension`：C# extension block。对 `SerializableCard`：`MinionLibComponentStateBlob`（从 `save.Props.intArrays` 按属性名取）、`Components`（反序列化 blob）、`GetComponentsLogString(depth, indentChars, showEmpty)`；对 `IEnumerable<ICardComponent>`：`ToLogString`。
- `LocHelper`（static）：`AddMany(this LocString, IEnumerable<DynamicVar> / DynamicVarSet / IReadOnlyDictionary<string,object>)`。

**`MinionLib.Component.Patches`**：见第三节补丁表。

**`MinionLib.Component.Utils`**：

- `AmountCardComponent`（abstract partial，继承 `CardComponent`）：**数值叠加组件基类**。`[ComponentState<DynamicVar>] partial decimal Amount { get; set; }`；`TryMergeWith`：`Amount += incoming.Amount`，`IsUpgrade` 时把所有 DynamicVar `SetWasJustUpgraded()`，`Amount==0` 时 merge 返回 null（组件被移除）；`TrySubtractiveMergeWith`：`Amount -= ...` 同理。
- `TimingCardComponent(params Timing[] timings)`（abstract partial）：**按 Timing 枚举开关的组件基类**。`[ComponentState] protected Timing[] Timings`；`OnTimingPrefix/OnTimingPostfix(OnTimingContext)`。生成文件 `TimingCardComponent.g.cs` 为每个 Timing 生成 `Override OnXxxPrefix/Postfix`，`Timings.Contains(Timing.X)` 时构造 `OnTimingContext` 并分发。`OnTimingContext` 是全字段 record（Timing、ActIndex、Orb、Osty、Map、WasRemovalPrevented、ItemPurchased、Power、Position、Type、Targets、Shuffler、Rewards、Context、ChoiceContext、Preventer、Player、OldPileType、FromHandDraw、CardSource、PileType、Potion、Blocker、Dealer、Delta、Card、Side、IsMimicked、Creator、Props、ModifiedAmount、Breaker、Result、CausedByEthereal、DeathAnimLength、CardPlay、CardLocation、Forger、Creature、Room、Command、Amount、Reward、Spender、Gainer、CombatState、Target、Applier、RewardsSet、Source、Summoner、GoldSpent、Creatures、FlushedCards、RetainedCards、Participants）。
- `Timing.g.cs`：`enum Timing`（117 项，OnPlay、BeforeCardPlayed、AfterCardPlayed、AfterSideTurnEnd、BeforeDeath、AfterDeath、AfterSummon……全量钩子名）。

**Partials（`MinionLib.Component.Partials`，均 `<auto-generated>`，勿手改）**：

- `CardComponent_Hooks.cs`（1083 行）：组件侧**全部事件钩子虚方法**，每个都成对 `XxxPrefix/XxxPostfix`，形如 `virtual Task OnPlayPrefix(PlayerChoiceContext, CardPlay, ComponentContext)`。覆盖：打出（OnPlay/EnqueuePlayVfx/TurnEndInHand/Before·After·AfterLateCardPlayed）、回合（AfterPlayerTurnStartEarly//Late、AutoPostPlay/AutoPrePlayPhaseEntered、SideTurnStart/End*）、战斗（CombatStart/End/Victory*、CreatureAddedToCombat、Death/Doom、DamageGiven/Received*、BlockGained/Cleared/Broken）、卡牌流转（ChangedPiles/Discarded/Drawn/EnteredCombat/Exhausted/AutoPlayed/Removed）、能量/费用（EnergyReset/Spent、ModifyingEnergyGain…）、奖励/商店/地图/篝火/事件、Orb、Potion、Power、Summon、OstyRevived 等。
- `CardComponent_Modifiers.cs`（407 行）：组件侧**数值/判定修改器**。`ModifyAttackHitCount`、`ModifyBlockAdditive/Multiplicative`、`ModifyCardPlayCount`、`ModifyCardPlayResultLocation`、`ModifyOrbPassiveTriggerCounts`、`ModifyDamageAdditive/Cap/Multiplicative`、`ModifyEnergyGain/MaxEnergy/HandDraw/GoldGained/SummonAmount/OrbValue/PowerAmountGivenAdd/Mul/RestSiteHealAmount/XValue`、`ModifyMerchantCardPool/Price/...`、`ModifyHpLostBefore/AfterOsty`、`TryModifyEnergyCostInCombat(StarCost/PowerAmountReceived/CardRewardOptions/CardBeingAddedToDeck/Rewards/RestSiteOptions...)`、以及一票 `ShouldXxx` 判定（`ShouldDie/ShouldDieLate/ShouldPlay/ShouldDraw/ShouldFlush/ShouldAllowTargeting/ShouldAllowHitting/ShouldCreatureBeRemovedFromCombatAfterDeath/ShouldStopCombatFromEnding/ShouldTakeExtraTurn...`）。
- `ComponentsCardModel_Hooks.cs`（8293 行）：`sealed override` 转发层，见核心机制②。
- `ComponentsCardModel_Modifiers.cs`（910 行）/ `ComponentsCardModel_ModifiersC.cs`（415 行）：前者 `sealed override` 依序把每个组件的 Modify/Should 串起来再调用后者（`protected virtual` 的 `XxxC` 版本，供子类覆写）。
- `ICardComponent_Hooks.cs`（1081 行）/ `ICardComponent_Modifiers.cs`（411 行）：接口默认实现的钩子/修改器。
- `ICardComponent_Log.cs`：`IGeneratedBinarySerializable.ToLogString` 显式实现，只反射带 `[ComponentState]` 的属性。

### 2. Layout 目录（命名空间 `MinionLib.Layout`）

| 类型 | 用途 | 关键成员 |
|---|---|---|
| `IMinionLayout` | 布局器接口 | `bool IsActive`；`void ApplyLayout(MinionLayoutContext context)` |
| `OwnerWithMinionsNodes`（readonly record struct） | 主人节点+其随从节点对 | `(NCreature Owner, IReadOnlyList<NCreature> Minions)` |
| `MinionNodePosition`（readonly record struct） | 节点+目标坐标 | `(NCreature Node, Vector2 Position)` |
| `MinionLayoutContext` | 一次布局计算的工作上下文 | `NCombatRoom Room`；`IReadOnlyList<NCreature> AllMinions`（= `room.CreatureNodes.Where(IsMinionNode)`）；`Dictionary<NCreature,Vector2> Positions`；`IEnumerable<NCreature> UnhandledMinions`（未被任何布局器定位的随从） |
| `DefaultMinionLayout`（实现 `IMinionLayout`） | 默认网格布局器（按主人/站位排布） | `static readonly Vector2 MinionSize = (150,200)`；`IsActive => true`；`ApplyLayout` 只处理 UnhandledMinions；`static GenerateGridPoints(MinionPosition, count)`（Upper 两列/奇数量心中；其余用对数收窄曲线 `turningPoint=3/1.5, slope=0.75/0.25` 的 Lerp 排布）；`static GetMinionOwnerNodePairs(room, unhandledMinions)`（按 `Entity.PetOwner` 分组，顺序取 `PlayerCombatState.Pets`）；`static CalculateBaseOffset(MinionPosition, ILookup<MinionPosition,NCreature>)`；`static CalculateMinionPositions(room, unhandled)`（按 `((MinionModel)Entity.Monster).Position` 分组 → offset + 网格点×MinionSize + ownerNode.Position） |
| `MinionLayoutManager`（static） | 布局器注册表与总入口 | `Register(IMinionLayout, int priority=0)`（**priority 越大越先执行**，同优先级按注册序倒序；默认已注册 `DefaultMinionLayout`）；`Layouts`；`CalculateLayout(NCombatRoom)`（按序 ApplyLayout 累积 Positions）；`GetCurrentMinionPositions(room)`（读取节点当前坐标） |
| `NCreatureExtensions`（static，文件 `NCreatureExtension.cs`） | 随从节点判定 | `bool IsMinionNode(this NCreature)`：`Entity is { Monster: MinionModel, IsAlive: true, PetOwner: not null }` |

### 3. RightClick 目录

**`MinionLib.RightClick`**：

- `IRightClickHandler`：`int Priority => 0`；`bool Handle(RightClickContext context)`。
- `RightClickContext`（record）：`(Player Player, AbstractModel Model, Payload Extra = default)`；嵌套 `struct Payload : IPacketSerializable`：`bool IsController` + `string? Meta`（Serialize/Deserialize 手写）。
- `RightClickDispatcher`（static）：`Register(IRightClickHandler)`（去重、按 Priority 降序）；`TryDispatch(RightClickContext)`（逐个 handler，首个返回 true 即消费；全不中记 Debug）。默认 handlers：`LogIdRightClickHandler`（仅 DEBUG）+ `EasyRightClickableModelHandler`。

**`MinionLib.RightClick.Easy`**：

- `EasyRightClickableModelType`（enum）：`Card, Relic, Power, Potion`。
- `IEasyRightClickableModel`：`bool CanHandleRightClickLocal(RightClickContext)`（默认 true）+ `Task OnRightClick(PlayerChoiceContext, RightClickContext)`；空标记接口 `IEasyRightClickableCard/Relic/Power/Monster`。
- `EasyRightClickableModelHandler`（`IRightClickHandler`）：模型实现 `IEasyRightClickableModel` 且类型合法（Card/Relic/Potion 需 `Owner==player`；Power 允许 `Owner.Player==player || Owner.PetOwner==player || Owner.IsEnemy`，**即随从身上的 Power 也可右击**）且 `CanHandleRightClickLocal` 时，构造 `EasyRightClickCardAction` 并经 `RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue` 入队（**网络/指令同步入口**），返回 true。
- `EasyRightClickCardAction`（`GameAction`）：右键执行的游戏动作。`Player/Extra/WasEnqueuedInCombat`；`ActionType` = 战斗中 `CombatPlayPhaseOnly` 否则 `NonCombat`；构造时按模型类型解析 `Type`、`ModelId`、`NetCombatCard`（Card）、`CreatureCombatId`（Power，需有 CombatId）、`PotionIndex`（Potion，不在主人药水列表则抛异常）；`ExecuteAction`：重解析模型（Card 须在手牌 `PileType.Hand`，Power 需 `combatState.GetCreature(CombatId)`，Potion 按槽位），校验 `model.Id==ModelId` 且实现接口后调用 `rightClickable.OnRightClick(new GameActionPlayerChoiceContext(this), new RightClickContext(...))`，最后 `model.InvokeExecutionFinished()`；`ToNetAction()` → `NetEasyRightClickCardAction`。
- `NetEasyRightClickCardAction`（struct，`INetAction`）：网络传输载荷；`Serialize/Deserialize`（枚举、FullModelId、按类型写 NetCombatCard/CreatureCombatId/PotionIndex、Payload、WasEnqueuedInCombat）；`ToGameAction(Player)` 还原动作。

**`MinionLib.RightClick.Patches`**：见第三节补丁表（四个节点类的事件接线补丁，模式一致：`_Ready`/`AddCardHolder` Postfix 里 `Connect(Control.SignalName.GuiInput, ...)`；鼠标右键按下/松开、或手柄 `MegaInput.cancel` 且节点 `HasFocus`；`NTargetManager.Instance.IsInSelection` 时忽略；`LocalContext.GetMe(...)` 取本地玩家；`TryDispatch` 成功后 `SetInputAsHandled()`）。

---

## 二、核心机制说明

### ① 卡牌组件：注册 → 挂载 → 序列化 → 执行

1. **注册**：`CardComponentRegistry.Register(componentId, type, factory)`（ID 全局唯一，冲突抛异常；同时进 `StringIdPool`）。`DelegateRegistry.Register<T>(name, del)` 注册具名委托。`StringIdPoolCollectorPatch` 在 `AbstractModel.InitId` Postfix 里把每个模型的 `Id.Category/Entry/Type.FullName` 注册进 StringIdPool，保证序列化字符串能压成 8 字节哈希 ID。
2. **挂载/合并**：`IComponentsCardModel.ApplyComponent(incoming, options)`（`AddComponent`/`SubtractComponent` 是其便捷封装）。`AllowMerge` 时逐个组件尝试 `TryMergeWith`（减法合并走 `TrySubtractiveMergeWith`）；merge 返回 `existing` 则原地保留，返回 null 则移除旧组件，否则替换并 `Attach`。未合并则追加并 `Attach`。`AmountCardComponent` 的语义：数值相加/相减，归零即移除，升级时标记 `WasJustUpgraded`。
3. **序列化**：`ComponentsCardModel.MinionLibComponentStateBlob`（`int[]` SavedProperty）是持久化载体；`CardComponentStateSerializer` 把组件列表写成 `[count][id][block]...` 二进制再转 int 数组。`EnsureComponentsInitialized()` 懒初始化：blob 空 → 克隆 `CanonicalComponents`；非空 → 反序列化（未知 ID/损坏块跳过并 Debug）。**存盘恢复**：`FrickYanoPatch`（`CardModel.FromSerializable` Postfix，`Priority.Last`）从 `SerializableCard.Props.intArrays` 找回 blob 并 `EnsureComponentsInitialized()`。
4. **执行模型（phase 循环）**：`ComponentsCardModel_Hooks.cs` 里每个事件钩子（如 `OnPlay`、`OnEnqueuePlayVfx`、`BeforeDeath`…）都是同一模板：
   - `EnsureComponentsInitialized()` → 把组件列表快照进 `ArrayPool<ICardComponent>.Shared` 租借数组；
   - `var componentContext = new ComponentContext(ComponentPhase.Init)`；`for (transitions < 64 && Phase != Final)` 内 `MoveNextPhase()` 推进 `Init→Prime→Prefix→Core→Postfix→Final`；
   - `Prefix` 阶段按正序调所有 `OnXxxPrefix`，`Postfix` 阶段按**倒序**调 `OnXxxPostfix`；循环内组件若已 `Detach`（`component.ComponentsCard != this`）跳过；`Prime/Core` 阶段转发给 `OnPlayPhased` 等（子类覆写 `OnPlay(..., ComponentContext)`）；
   - 组件可在钩子里调 `componentContext.MoveNextPhase()` 提前切阶段（甚至触发再次执行其他阶段）；
   - 超过 `MaxPhaseTransitions=64` 未到 Final → `HandlePhaseTransitionLimitExceeded` 警告（疑似死循环，可用反射调大）。
5. **描述文本**：`BetterAddExtraArgsToDescription` 把每个组件的前缀按正序拼进 `CompPre`、后缀按倒序拼进 `CompPost`。`ComponentDescriptionRawCachePatch`：`CardModel.Description` getter Postfix 时把原始文本注入 `{CompPre}`/`{CompPost}` 占位符并缓存（按 locEntryKey）；`LocString.GetRawText` Prefix 对 "cards" 表命中缓存直接返回（防止游戏内部再去读表）；`LocManager.SetLanguage` Postfix 清缓存。
6. **DynamicVar 预览/升级**：`CardComponentDyanamicVarsUpdatePatch`：`CardModel.UpdateDynamicVarPreview` Postfix 逐组件刷新 `DynamicVars`；`FinalizeUpgradeInternal` Postfix 调各 `DynamicVarSet.FinalizeUpgrade()`。
7. **升级/降级**：`ComponentsCardModel.OnUpgrade()/AfterDowngraded()` 被 sealed，内部构造 `ComponentContext(ComponentPhase.Core)` 广播给组件（组件覆写带 `ComponentContext` 参数的重载）。

### ② 随从布局（Layout）

- 入口 `MinionLayoutManager.CalculateLayout(NCombatRoom)`：建 `MinionLayoutContext`（收集 `AllMinions = room.CreatureNodes.Where(IsMinionNode)`，即**存活、是 MinionModel、有 PetOwner** 的节点）→ 按注册序（priority 降序）执行各 `IMinionLayout.ApplyLayout` → 每个布局器把自己算好的 `Positions` 写进 context，未处理的随从留给下一布局器（`UnhandledMinions`）。
- `DefaultMinionLayout` 算法：按 `MinionModel.Position`（`Front/Back/FrontUpper/BackUpper/Upper`）分组 → `CalculateBaseOffset`（Front/Back 水平 ±200、若对应 Upper 排 ≥2 则再 +50y；Upper 类垂直 -350；Upper 正中 (0,-450)）→ `GenerateGridPoints` 生成相对网格点（Upper 两列交错，其余用对数压缩曲线防溢出）→ 最终 `pos = grid * MinionSize(150,200) + offset + ownerNode.Position`。
- 排序尊重主人视角：`GetMinionOwnerNodePairs` 按 `PlayerCombatState.Pets` 顺序排列随从，保证展示顺序与宠物列表一致。

### ③ 右键系统（RightClick）

- **捕获**：四个补丁给手牌卡位（`NPlayerHand.AddCardHolder`）与药水/能力/遗物节点（`_Ready`）连 `GuiInput`；鼠标右键或手柄 cancel 键触发；战斗中正选目标（`NTargetManager.IsInSelection`）、手牌正在打出（`hand.InCardPlay`）时忽略。
- **分发**：`RightClickDispatcher.TryDispatch` 按 priority 降序试所有 handler。核心 handler `EasyRightClickableModelHandler`：模型实现 `IEasyRightClickableModel` → 本地预检 `CanHandleRightClickLocal` → 打包成 `EasyRightClickCardAction` 经 `ActionQueueSynchronizer.RequestEnqueue` **入队**（多端一致执行），本地返回 true 消费输入。
- **执行**：动作执行时按类型重解析模型（卡须仍在手牌、Power 按 CombatId、Potion 按槽位），校验后调用 `OnRightClick(new GameActionPlayerChoiceContext(this), clickContext)`，结束 `InvokeExecutionFinished()`。网络端通过 `NetEasyRightClickCardAction`（INetAction）序列化/还原。
- **组件集成**：`ComponentsCardModel` 自身实现 `IEasyRightClickableCard`；`CanHandleRightClickLocal/OnRightClick` 遍历组件，**只执行第一个 `CanHandleRightClick` 为真的组件**（`CardComponent.CanHandleRightClickLocal` 默认转发给 `CanHandleRightClick`）。即组件可声明"右键我"，mod 卡即可获得右键能力。

### ④ 随从生命周期与三目录的触点（范围外目录，仅回查指针）

随从的注册/召唤/行动/死亡清理主体在 `Minion/`、`Action/`、`Powers/`、`Targeting/` 目录，本文目录只消费其结果：

- **模型/召唤**：`Minion/MinionModel.cs`（范围外）：`abstract class MinionModel : MonsterModel`，`MinionPosition Position`、`enum MinionPosition { Front=0, Back, FrontUpper, BackUpper, Upper }`、`MinionSummonOptions(MaxHp/PrimaryStatAmount/SecondaryStatAmount/TertiaryStatAmount/Source/Position)`、`virtual Task OnSummon(PlayerChoiceContext, Player owner, MinionSummonOptions)`。Layout 与 NCreatureExtensions 依赖 `PetOwner`、`PlayerCombatState.Pets`、`MinionModel.Position`。
- **死亡清理**：`Minion/Patches/MinionKillPatch.cs`（范围外）：Postfix 于 `CreatureCmd.KillWithoutCheckingWinCondition`，await 原 Task 后对 `Side==Player && IsPet && Monster is MinionModel` 的随从按 `Hook.ShouldCreatureBeRemovedFromCombatAfterDeath` 补做 `CombatManager.RemoveCreature + CombatState.RemoveCreature`（修复 0.107.0 官方只清敌方、友方随从残留的问题）。组件侧对应判定为 `ICardComponent.ShouldCreatureBeRemovedFromCombatAfterDeath`。
- **目标选择**：`Targeting/Patches/CustomTargetTypeCardPatch.cs` 等（范围外）patch `CardModel.IsValidTarget`、`NTargetManager.AllowedToTargetCreature`、`NCardPlay.TryPlayCard/ShowMultiCreatureTargetingVisuals`、`NMouseCardPlay.MultiCreatureTargeting`、`NControllerCardPlay.SingleCreatureTargeting` 等，实现自定义目标类型；组件侧暴露 `ExtraTargetType` 参与聚合（`SingleTargetTypesUnionManager.GetWithBase`）。组件判定 `ShouldAllowTargeting/ShouldAllowHitting`。
- **行动**：`MinionModel.GenerateMoveStateMachine` 只给 `MINION_IDLE` 空状态机（随从不自主行动，行为由 mod 通过钩子/命令驱动）；组件钩子 `AfterSummon`、`BeforeAttack` 等可参与。

---

## 三、与游戏 API 的交互点（HarmonyPatch 目标）

| 游戏类型/方法 | 补丁类型 | 所在文件（目录） | 作用 |
|---|---|---|---|
| `AbstractModel.InitId` | Postfix | `Component/Core/StringIdPoolCollectorPatch.cs` | 所有模型 ID 注册进 StringIdPool |
| `CardModel.UpdateDynamicVarPreview` | Postfix | `Component/Patches/CardComponentDyanamicVarsUpdatePatch.cs` | 逐组件刷新 DynamicVar 预览 |
| `CardModel.FinalizeUpgradeInternal` | Postfix | 同上 | 组件 DynamicVars 升级收尾 |
| `CardModel.Description`（getter） | Postfix | `Component/Patches/ComponentDescriptionRawCachePatch.cs` | 注入 `{CompPre}/{CompPost}` 并缓存原始文本 |
| `LocString.GetRawText` | Prefix | 同上 | cards 表命中缓存直接返回 |
| `LocManager.SetLanguage` | Postfix | 同上 | 清描述缓存 |
| `CardModel.FromSerializable` | Postfix（`Priority.Last`） | `Component/Patches/FrickYanoPatch.cs` | 存盘恢复组件 blob |
| `NetFullCombatState.ToString` | Transpiler | `Component/Patches/NetFullCombatStateComponentsLogPatch.cs` | 调试日志里追加每张卡组件树（IL 匹配 `List<CardState>.Enumerator` 的 Current/MoveNext 注入 `AppendComponentInfo`） |
| `NPlayerHand.AddCardHolder` | Postfix | `RightClick/Patches/CardRightClickPatch.cs` | 手牌卡位右键/手柄 cancel 接线 |
| `NPotion._Ready` | Postfix | `RightClick/Patches/PotionRightClickPatch.cs` | 药水右键接线 |
| `NPower._Ready` | Postfix | `RightClick/Patches/PowerRightClickPatch.cs` | 能力右键接线 |
| `NRelic._Ready` | Postfix | `RightClick/Patches/RelicRightClickPatch.cs` | 遗物右键接线 |

（范围外但同库的补丁点，供回查：`CreatureCmd.KillWithoutCheckingWinCondition`（MinionKillPatch）、`NCreature.ToggleIsInteractable`+`NCombatRoom.AddCreature`（MinionInteractablePatch）、`NCreature._Ready`（ActionClickPatch）、`CardModel.IsValidTarget`/`NTargetManager.AllowedToTargetCreature`/`NCardPlay.TryPlayCard` 等（Targeting 系列）、`Creature.LoseHpInternal`/`CreatureCmd.Damage`/`CreatureCmd.GainBlock`（MinionGuardian 系列）。）

---

## 四、已知限制 / 陷阱

1. **StringIdPool 哈希碰撞即抛异常**（`Register` 对同 hash 不同串抛 `StringPool Hash Collision!`）；ID 需先注册才能用 `TryGetId` 压成 8 字节。`StringIdPoolCollectorPatch` 只覆盖经 `AbstractModel.InitId` 的模型串，mod 自定义字符串要显式 `Register`。（`Component/Core/StringIdPool.cs`）
2. **组件 ID 全局唯一**：`CardComponentRegistry.Register` 重复 ID 直接抛异常；反序列化遇到未知 ID 只跳过并打 Debug，**不会报错**——组件静默丢失（`Component/Core/CardComponentRegistry.cs`、`CardComponentStateSerializer.cs`）。
3. **phase 循环上限 64**：`MaxPhaseTransitions=64`，钩子内死循环会触发 `HandlePhaseTransitionLimitExceeded` 警告并停止推进（"no further phase transitions will be processed"），可经反射调大（`ComponentsCardModel.cs`）。
4. **sealed + [Obsolete] 陷阱**：`ComponentsCardModel` 把 `CardModel` 大量成员 sealed（`OnUpgrade`/`AfterDowngraded`/各 Modify/Should/Hook），直接覆写原签名会得到 Obsolete 警告且不生效；必须覆写 **C 结尾** 虚方法（如 `OnPlayC`、`ModifyDamageAdditiveC`）或带 `ComponentContext` 的重载（`ComponentsCardModel_ModifiersC.cs`、`ComponentsCardModel.cs`）。
5. **组件快照语义**：事件循环开始时用 ArrayPool 快照组件列表（`clearArray: true` 归还）；**循环中途新增的组件本回合不触发**；已 Detach 的组件通过 `component.ComponentsCard != this` 跳过（`ComponentsCardModel_Hooks.cs`）。
6. **合并语义**：merge 返回 null 表示"合并后归零，移除该组件"（`AmountCardComponent`）；`ReferenceEquals(merged, existing)` 原地保留。注意减法合并（`SubtractComponent`）不匹配时直接返回 null 不新增。
7. **描述缓存只对 "cards" 表 + 按 locEntryKey 缓存**；换语言才清空。组件前缀/后缀 LocString 不存在（`!loc.Exists()`）时返回空串。`{CompPre}`/`{CompPost}` 占位符由补丁自动注入原始文本，mod 自己的描述里不要手动写死这两个 token（会被去重逻辑跳过追加）（`ComponentDescriptionRawCachePatch.cs`）。
8. **blob 每次 getter 都重序列化**：`MinionLibComponentStateBlob` getter 在 `_components != null` 时重新 `Serialize`，热路径读取有开销；且 `int[]` 首元素存字节长度，跨版本格式不兼容需自行迁移。
9. **右键执行校验严格**：`EasyRightClickCardAction.ExecuteAction` 中卡不在手牌→**静默 return**；Power 需要 `CombatState` 与 `CreatureCombatId`；Potion 不在主人药水列表在构造时**抛异常**；目标选择中/手牌打出中右键被忽略；`IsInputHandled` 已消费时不再处理（`EasyRightClickCardAction.cs`、`CardRightClickPatch.cs`）。
10. **随从 Power 右击放行条件宽**：`power.Owner.Player == player || power.Owner.PetOwner == player || power.Owner.IsEnemy`——玩家可右键**敌人**身上的能力，mod 需自行在 `CanHandleRightClickLocal` 里过滤（`EasyRightClickableModelHandler.cs`）。
11. **DefaultMinionLayout 硬编码**：`MinionSize=(150,200)`、±200/350/450 像素偏移、网格对数压缩曲线均针对原版 UI 调参；大量随从/特殊站位需自写 `IMinionLayout`（priority>0 先算、<0 后处理）覆盖（`DefaultMinionLayout.cs`）。
12. **Transpiler 脆弱**：`NetFullCombatStateComponentsLogPatch` 强依赖 `NetFullCombatState.ToString` 的 IL 形态（`List<CardState>.Enumerator` 的 `Current`/`MoveNext` 位置与 label 分流），模式不匹配直接 `throw new Exception("Transpiler 失败...")`，游戏更新可能击穿（`NetFullCombatStateComponentsLogPatch.cs`）。
13. **生成代码勿手改**：`Partials/*.g.cs`/`*_Hooks.cs`/`*_Modifiers*.cs`/`Timing*.g.cs` 均为工具生成；`ComponentDelegateAttribute`、`ComponentStateAttribute` 是给生成器的标记，运行时无解释逻辑（只有 `ICardComponent_Log.cs` 用 `ComponentStateAttribute` 过滤日志属性）。
14. **`AmountCardComponent.Amount` 用 `[ComponentState<DynamicVar>] partial property`**：依赖生成器补全 `Serialize/Deserialize`；若类标了 `NoGeneratedSerializationAttribute` 则需手写序列化，否则反序列化后 Amount 可能丢失。
15. **`ComponentsBlobLogExtension` 用 C# extension block 新语法**：仅日志辅助；`GetComponentsLogString` 反序列化 blob 时 owner 传 null（组件 `ComponentsCard` 为空）。（`ComponentsBlobLogExtension.cs`）

---

## 附：文件索引（按目录）

- `Component/`：`CardComponent.cs`、`ComponentExtensions.cs`、`ComponentsCardModel.cs`；`Core/`（`ApplyComponentOptions`、`CardComponentRegistry`、`CardComponentStateSerializer`、`ComponentDelegateAttribute`、`ComponentDescriptionRawCache`、`ComponentPhase`、`ComponentStateAttribute`、`DelegateRegistry`、`LocArgAttribute`、`NoGeneratedSerializationAttribute`、`SerializationUtils`、`StringIdPool`、`StringIdPoolCollectorPatch`）；`Extensions/`（`ComponentsBlobLogExtension`、`LocHelper`）；`Interfaces/`（`ICardComponent`、`IComponentsCardModel`、`IGeneratedBinarySerializable`）；`Partials/`（`CardComponent_Hooks`、`CardComponent_Modifiers`、`ComponentsCardModel_Hooks`、`ComponentsCardModel_Modifiers`、`ComponentsCardModel_ModifiersC`、`ICardComponent_Hooks`、`ICardComponent_Log`、`ICardComponent_Modifiers`）；`Patches/`（`CardComponentDyanamicVarsUpdatePatch`、`ComponentDescriptionRawCachePatch`、`FrickYanoPatch`、`NetFullCombatStateComponentsLogPatch`）；`Utils/`（`AmountCardComponent`、`Timing.g`、`TimingCardComponent`、`TimingCardComponent.g`）。
- `Layout/`：`DefaultMinionLayout.cs`、`IMinionLayout.cs`、`MinionLayoutContext.cs`、`MinionLayoutManager.cs`、`NCreatureExtension.cs`。
- `RightClick/`：`IRightClickHandler.cs`、`RightClickContext.cs`、`RightClickDispatcher.cs`；`Easy/`（`EasyRightClickCardAction`、`EasyRightClickableModelHandler`、`EasyRightClickableModelType`、`IEasyRightClickableModel`、`NetEasyRightClickCardAction`）；`Patches/`（`CardRightClickPatch`、`PotionRightClickPatch`、`PowerRightClickPatch`、`RelicRightClickPatch`）。
- 范围外回查：`Minion/MinionModel.cs`、`Minion/Patches/MinionKillPatch.cs`、`Minion/Patches/MinionInteractablePatch.cs`、`Minion/Patches/PersonalHivePowerPatch.cs`、`Action/Patches/ActionClickPatch.cs`、`Action/Patches/ActionPowerIconClickPatch.cs`、`Powers/Patches/MinionGuardian*.cs`、`Targeting/Patches/CustomTargetType{Card,Potion}Patch.cs`。
