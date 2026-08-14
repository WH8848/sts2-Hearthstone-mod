# RitsuLib 源码笔记：CardPiles / Cards / CardTags（公共 API 与用法）

> 来源：STS2-RitsuLib `src/CardPiles`、`src/Cards`、`src/CardTags`（含 `Interop/AutoRegistration` 注册特性）。
> 只收录对 mod 制作有实用价值的公共 API，跳过内部实现（Patches、internal 类）。
> 命名空间前缀：`STS2RitsuLib.*`。属性注册命名空间：`STS2RitsuLib.Interop.AutoRegistration`。

---

## 1. CardPiles —— 自定义卡牌牌堆（`STS2RitsuLib.CardPiles`）

### 1.1 核心注册器：`ModCardPileRegistry`（sealed class）
为单个 mod 注册自定义牌堆并解析动态 `PileType`。注册信息进程内共享，**在模型初始化前冻结**（冻结后注册抛异常）。全局动态枚举值保留高值区间 `[0x4000_0000, 0x7FFF_FFFF]`。

| 成员 | 签名 |
| --- | --- |
| 获取注册器 | `static ModCardPileRegistry For(string modId)` |
| 冻结状态 | `static bool IsFrozen { get; }` |
| 注册（mod 限定） | `ModCardPileDefinition RegisterOwned(string localStem, ModCardPileSpec spec)` |
| 注册（全局 ID） | `ModCardPileDefinition Register(string id, ModCardPileSpec spec)` |
| 按 ID 查 | `static bool TryGet(string id, out ModCardPileDefinition)` / `static ModCardPileDefinition Get(string id)` |
| 按 PileType 查 | `static bool TryGetByPileType(PileType, out …)` / `static ModCardPileDefinition Get(PileType)` / `static bool IsModPileType(PileType)` |
| ID ↔ 值 | `static PileType GetPileType(string id)`（无需注册，确定性值）、`static bool TryGetPileType(...)`、`static bool TryResolvePileType(string idOrEnumName, out PileType)`（已注册 ID > 原版枚举名 > 动态值）、`static bool TryGetId(PileType, out string id)` |
| 归属查询 | `static bool TryGetOwnerModId(string pileId, out string modId)` |
| 悬停提示 | `static HoverTip CreateHoverTip(string id)` / `static HoverTip CreateHoverTip(PileType)` |
| 快照 | `static ModCardPileDefinition[] GetDefinitionsSnapshot()`（按 ID 排序） |

### 1.2 配置与定义
- **`ModCardPileSpec`**（sealed record，注册时配置）：`Scope`、`Style`、`Anchor`、`IconPath`、`Hotkeys`、`CardShouldBeVisible`、`ExtraHand`、`HoverTipScreenOffset`、`HoverTipPlacement`、`VisibleWhen`、`View`、`OnOpen`、`FlightTargetPositionResolver`、`FlightStartPositionResolver`（全部 `init`）。常量 `HoverTipLocTable = "static_hover_tips"`。默认值：`Scope=CombatOnly`、`Style=Headless`。
- **`ModCardPileDefinition`**（sealed record，注册结果）：`ModId`、`Id`、`PileType`、`Scope`、`Style`、`Anchor`、`IconPath`、`Hotkeys`、`CardShouldBeVisible`、`ExtraHand`、`OnOpen`、`View`、`HoverTipScreenOffset`、`HoverTipPlacement`、`VisibleWhen`、`FlightTargetPositionResolver`、`FlightStartPositionResolver`；便捷本地化属性 `Title`/`Description`/`EmptyPileMessage`（键 `{Id}.title/.description/.empty`，表 `static_hover_tips`）。
- **`ModCardPile`**（sealed class : `CardPile`）：运行时牌堆实例，`ModCardPile(ModCardPileDefinition)`，属性 `Definition`。运行时通过 `ModCardPileRegistry.Get(id).PileType` 配合牌堆操作使用。

### 1.3 枚举与结构
- **`ModCardPileScope`**：`CombatOnly=0`（挂到 PlayerCombatState，随战斗销毁）、`RunPersistent=1`（挂到 Player，跨战斗保留并随局保存）。
- **`ModCardPileUiStyle`**：`Headless=0`（无 UI）、`TopBarDeck=1`（顶部栏按钮）、`BottomLeft=2`（抽牌堆旁）、`BottomRight=3`（消耗牌堆旁）、`ExtraHand=4`（交互式额外手牌容器）。
- **`ModCardPileAnchorKind`**：`StyleDefault`、`BottomLeftPrimary`、`BottomLeftSecondary`、`BottomRightPrimary`、`BottomRightSecondary`、`TopBarAfterDeck`、`TopBarBeforeModifiers`、`ExtraHandAbove`、`ExtraHandBelow`、`Custom`。
- **`ModCardPileAnchor`**（readonly record struct `(Kind, Offset, CustomPosition, CustomAuthoringPivot)`）：工厂 `Default`、`AtPosition(Vector2 upperLeft)`、`AtPivot(authoringPoint, pivotFraction)`、`AtCenter(center)`；枢轴常量 `PivotUpperLeft`、`PivotCenter`。
- **`ModCardPileHoverTipPlacement`**：`Auto`、`BelowButtonTrailingEdge`、`AboveButtonCentered`、`BelowButtonCentered`。
- **`ModCardPileSortOption`**：`Obtained`、`Type`、`Cost`、`Alphabetical`、`Rarity`（排序栏用，不改变牌堆顺序）。

### 1.4 展示与交互配置
- **`ModCardPileExtraHandSpec`**（ExtraHand 样式配置）：`Direction`（默认 `VanillaHand`）、`Spacing`（默认 110）、`CardScale`（默认 0.65）、`HoverScale`（默认 1）、`ShowPlayableGlow`（默认 true）、`AllowCardPlay`（默认 true，是否可手动出牌）、`DisabledOffset`/`DisabledModulate`/`DisabledTransitionDuration`（禁用表现）、`LayoutResolver`（`Func<ModExtraHandCardContext, ModExtraHandCardTransform?>`）、`OnCardVisualCreated`、`OnCardArrived`。
- **`ModCardPileViewSpec`**（默认牌堆界面扩展）：静态 `DeckLike`（开检查/升级预览/排序栏）；`EnableCardInspect`、`EnableUpgradePreviewToggle`、`EnableSortBar`、`SortOptions`、`DefaultSorting`、工具栏与排序按钮的贴图/材质/运行时提供器、`DisableSortButtonHue`、`UpgradePreviewLabelColor` 等。
- **`ModCardPileHoverTipFactory`**（static）：`HoverTip Create(ModCardPileDefinition)`。
- **`ModCardPileHoverTipViewport`**（static）：`Vector2 ClampTipTopLeft(NHoverTipSet, Vector2 globalTopLeft)`（8px 边距，超大时居中）。

### 1.5 上下文对象（回调参数）
- **`ModCardPileOpenContext`**（`OnOpen` 回调参数）：`Definition`、`Pile`、`Player`、`Button`；方法 `ShowDefaultPileScreen()`（打开原版 `NCardPileScreen`）、`OpenCapstoneScreen(ICapstoneScreen)`（打开自定义顶层界面）。空牌堆不触发回调。
- **`ModCardPileVisibilityContext`**（`VisibleWhen` 参数）：`Definition`、`Player?`、`Button?`、`Pile?`（初始化期间可为 null）。
- **`ModCardPileFlightTargetContext`** / **`ModCardPileFlightStartContext`**（飞行动画位置解析器参数，均实现 `IModCardPileFlightContext`）：`Definition`、`DefaultPosition`（RitsuLib 默认解析值，返回 null 即用此值）、`CardNode`/`CardModel`，起点上下文另有 `StartPile`、`TargetPile`。
- **`ModCardPileViewStyleContext`**（record，视图样式提供器参数）：`Definition`、`Pile`、`Screen`。
- **`ModExtraHandCardContext`**：`Definition`、`Container`、`Card`、`Holder`、`CardNode`、`Index`、`Count`、`IsFocused`、`DefaultTransform`。
- **`ModExtraHandCardTransform`**（readonly record struct）：`(Position, Scale, RotationDegrees, ZIndex)`。
- **`ModExtraHandLayoutDirection`**：`Horizontal`、`Vertical`、`VanillaHand`。

### 1.6 接口
- **`IModCardPileHandler`**：`void OnOpen(ModCardPileOpenContext context)`。配合 `[RegisterOwnedCardPile]` 自动注册时，RitsuLib 通过公共无参构造函数实例化并绑定到 `Spec.OnOpen`。
- **`IModCardPileFlightContext`**：`Definition`、`DefaultPosition`、`StartPile?`、`TargetPile?`、`CardNode?`、`CardModel?`。

### 1.7 内容包 / 扩展与节点
- **`CardPileRegistrationEntry`**（sealed record）：内容包声明式注册项。`CardPileRegistrationEntry(string id, ModCardPileSpec spec)`；`void Register(ModCardPileRegistry)`；`static CardPileRegistrationEntry Owned(string modId, string localPileStem, ModCardPileSpec spec)`。
- **`ModCardPileExtensions`**（static）：`string.GetModCardPileType()`、`PileType.TryGetModCardPileId(out string)`、`PileType.GetModCardPileId()`。
- **`ModCardPilePlayerSaveState`**：RunPersistent 牌堆的序列化状态（`Dictionary<string, List<SerializableCard>> Piles`、`bool IsEmpty`）。
- **`STS2RitsuLib.CardPiles.Nodes`**：
  - `NModCardPileButton : Control`（sealed）：`static Create(ModCardPileDefinition)`、`static CreateAction(ModTopBarButtonDefinition)`、`void Initialize(Player)`、`void TriggerOpen()`；属性 `Definition?`、`ActionDefinition?`、`bool IsActionMode`。
  - `NModExtraHand : Control`（sealed）：`static Create(ModCardPileDefinition)`、`void Initialize(Player)`、`void SetCardPlayEnabled(bool)`（定义未允许出牌时抛异常）、`NCard? GetCard(CardModel)`、`NHandCardHolder? GetHolder(CardModel)`；属性 `Definition`、`bool CardPlayEnabled`。
  - `NModTopBarPileButton`（sealed，静态工厂）：`static NModCardPileButton Create(ModCardPileDefinition)`。

---

## 2. Cards —— 卡牌钩子 / 动态变量 / 免费打出 / 转化（`STS2RitsuLib.Cards`）

### 2.1 打出钩子（`CardOnPlayHook`）
在卡牌自身 `OnPlay` 方法前后注入逻辑，兼容原版 `CardModel.OnPlayWrapper` 流程。

- **`ICardOnPlayHookListener`**：`Task<bool> BeforeCardOnPlay(BeforeCardOnPlayContext)`（返回 true 跳过原版 `OnPlay`，但不跳过其余流程；默认 false）、`Task AfterCardOnPlay(AfterCardOnPlayContext)`（默认空实现）。
- **`BeforeCardOnPlayContext`**（readonly record struct）：`CombatState`、`ChoiceContext`、`CardPlay`。
- **`AfterCardOnPlayContext`**：同上 + `bool OriginalOnPlayRan`。
- **`CardOnPlayHook`**（static）：`void RegisterGlobalListener(ICardOnPlayHookListener)`（进程级监听器；模型持有的效果应直接实现接口）、`Task RunCardOnPlayHooks(CardModel, PlayerChoiceContext, CardPlay)`、`Task<bool> BeforeCardOnPlay(...)`、`Task AfterCardOnPlay(...)`。

### 2.2 卡牌类型文本钩子（`CardTypeTextHook`）
- **`CardTypeTextHook`**（static）：`void RegisterGlobalModifier(ICardTypeTextModifier)`。修改器来源优先级：卡片实现 `ICustomTypeTextCard` → 模型能力 `ICardTypeTextModifier` → run/combat 监听器 → 全局注册。引用 `{Type}` 占位符的修改器会包装旧文本，否则替换。
- 相关接口（定义于 `STS2RitsuLib.Models.Capabilities`）：`ICustomTypeTextCard`、`ICardTypeTextModifier`。

### 2.3 动态变量（`STS2RitsuLib.Cards.DynamicVars`）
- **`ModCardVars`**（static，推荐工厂入口）：
  - 基础变量：`Int`、`String`、`Bool`、`Cards`、`Damage`、`OstyDamage`、`Block`、`Gold`、`Heal`、`HpLoss`、`MaxHp`、`Repeat`、`Forge`、`Summon`、`Energy`、`Stars`、`Power<T>`（各带 `(name, amount)` 重载，`Damage/Block` 支持 `ValueProp props = ValueProp.Move`）。
  - 计算变量：`Computed(name, baseValue, currentValueFactory, previewValueFactory?)`（无目标 / 带 `Creature?` 目标 / `ComputedDynamicVarFactory` 上下文三种重载）、`ComputedEnergy`、`ComputedStars`、`ComputedPower<T>`；**`ComputedDamage` / `ComputedOstyDamage` / `ComputedBlock`** 系列：预览时自动跑全局 `Hook.ModifyDamage`/`ModifyBlock`（启用全局钩子且有 run+combat 上下文时）。
- **`ComputedDynamicVar : DynamicVar, IComputedDynamicVar`**：构造重载同 `ModCardVars.Computed`；`decimal Calculate(Creature? target = null)`、`Calculate()`；预览经 `UpdateCardPreview` 写 `PreviewValue`。
- **`ComputedEnergyVar : EnergyVar, IComputedDynamicVar`**、**`ComputedStarsVar : StarsVar, IComputedDynamicVar`**、**`ComputedPowerVar<T> : PowerVar<T>, IComputedDynamicVar`**（`T : PowerModel`）：同上，默认以 `typeof(T).Name` 命名。
- **`ComputedDynamicVarContext`**：`Variable`、`ModelOwner`、`Card`、`Target`、`PreviewMode`、`RunGlobalHooks`；便捷判定 `IsPreview/IsCurrentValue/IsNormalPreview/IsUpgradePreview/IsMultiTargetPreview`、`HasCard/HasTarget/HasPlayer/HasCombatState/...`（`[MemberNotNullWhen]`）；取值 `TryGetCardVar(name)` / `TryGetCardVar<TVar>` / `GetRequiredCardVar<TVar>` / `GetCardBaseValueOrDefault` / `GetCardIntOrDefault` / `EvaluateCardVarOrDefault`（防递归求值）。
- **`ComputedDynamicVarFactory`**（delegate）：`decimal (ComputedDynamicVarContext context)`。
- **`IComputedDynamicVar`**：`decimal Calculate(Creature? target = null)`。
- **`DynamicVarExtensions`**（static）：`DynamicVar.WithTooltip(Func<DynamicVar,IHoverTip>)` / `WithTooltip(titleTable, titleKey, descriptionTable?, descriptionKey?, iconPath?)` / `WithSharedTooltip(entryPrefix, iconPath?)`（`static_hover_tips` 表）；`DynamicVar.CreateHoverTip()`；`DynamicVarSet.TryGet<TVar>` / `GetRequired<TVar>` / `GetIntOrDefault` / `GetValueOrDefault` / `HasPositiveValue` / `TryComputeValue` / `GetComputedValue` / `EvaluateValueOrDefault` / `ComputeDynamicValue` / `ComputeEnergyValue` / `ComputePowerValue<T>` / `ComputeStarsValue`。
- **`DynamicVarTooltipRegistry`**（static）：`Set(DynamicVar, Func<DynamicVar,IHoverTip>)`、`Get(...)`、`Create(...)`（失败记警告返回 null）、`CopyTo(source, dest)`。

### 2.4 免费打出（`STS2RitsuLib.Cards.FreePlay`）
- **`FreePlayBindingRegistry`**（static）：
  - `void Register(string bindingId, Func<CardPlay, bool> detector)`：注册自定义"免费出牌"检测器（stable ID，可替换）。
  - `void MarkCardFreeNextPlay(CardModel)`、`void MarkCardFreeThisTurn(CardModel)`、`void MarkCardFreeThisCombat(CardModel)`、`void MarkCurrentPlayFree(CardPlay)`。
  - 查询：`FreePlayResolution Resolve(CardPlay)`、`bool IsFreeForPlay(CardPlay)`、`bool IsCardFreeForUpcomingPlay(CardModel)`、清理 `ClearCardFreeThisTurn(CardModel)`、`ClearCardFreeAfterPlayed(CardModel)`。
- **`FreePlayResolution`**（sealed record）：`IsAutoPlayNoSpend`、`IsCardBindingFree`、`IsRegisteredDetectorFree`；`bool IsFree` 为三者或。
- **`CardModelFreePlayExtensions`**：`void SetToFreeForRestOfTurn(this CardModel)`（与 `SetToFreeThisTurn` 不同，打出后不清除，回合结束清理时失效；X 费用不受影响）。

### 2.5 卡牌转化（`STS2RitsuLib.Cards.Transforms`）
- **`ModCardTransformRegistry`**（sealed）：`static For(string modId)`；`Register(listenerId, Action<ModCardTransformContext>)` / `Register(listenerId, Func<Context,Task>)` / `Register(listenerId, predicate, listener)` / 类型化 `Register<TOriginal,TReplacement>(listenerId, Action<TOriginal,TReplacement>)`、`RegisterFrom<TOriginal>`、`RegisterTo<TReplacement>`（同步/异步各一）；`bool Unregister(listenerId)`。同 ID 注册 = 替换；按注册顺序调用。
- **`ModCardTransformContext`**（readonly record struct）：`Original`、`Replacement`、`OriginalPile`、`OriginalPileIndex`。

---

## 3. CardTags —— 自定义卡牌标签（`STS2RitsuLib.CardTags`）

### 3.1 注册器：`ModCardTagRegistry`（sealed class）
与 `ModCardPileRegistry` 同构，注册在模型初始化前冻结。

| 成员 | 签名 |
| --- | --- |
| 获取 | `static ModCardTagRegistry For(string modId)` |
| 冻结状态 | `static bool IsFrozen { get; }` |
| 注册 | `ModCardTagDefinition RegisterOwned(string localTagStem)`（限定 ID `MODID_CARDTAG_STEM`）、`ModCardTagDefinition Register(string id)` |
| 按 ID 查 | `static bool TryGet(string id, out ModCardTagDefinition)` / `static ModCardTagDefinition Get(string id)` |
| 按 CardTag 查 | `static bool TryGetByCardTag(CardTag, out …)` / `static ModCardTagDefinition Get(CardTag)` / `static bool IsModCardTag(CardTag)` |
| ID ↔ 值 | `static CardTag GetCardTag(string id)`（确定性，无需注册）、`static bool TryGetCardTag(...)`、`static bool TryResolveCardTag(string idOrEnumName, out CardTag)`、`static bool TryGetId(CardTag, out string id)` |
| 归属 | `static bool TryGetOwnerModId(string tagId, out string modId)` |
| 快照 | `static ModCardTagDefinition[] GetDefinitionsSnapshot()` |

### 3.2 其余类型
- **`ModCardTagDefinition`**（sealed record）：`(string ModId, string Id, CardTag CardTagValue)`。
- **`CardTagRegistrationEntry`**（sealed record `(string Id)`）：`void Register(ModCardTagRegistry)`；`static CardTagRegistrationEntry Owned(string modId, string localTagStem)`。
- **`ModCardTagExtensions`**（static）：`card.AddModCardTag(CardTag)`（要求 `CardModel.Tags` 为可变 `HashSet<CardTag>`，否则抛异常）、`card.RemoveModCardTag(CardTag)`、`card.HasModCardTag(CardTag)`、`string.GetModCardTag()`、`CardTag.TryGetModCardTagId(out string)` / `CardTag.GetModCardTagId()`。
- **`STS2RitsuLib.CardTags.Serialization`**：
  - `CardTagJsonConverter : JsonConverter<CardTag>`：读支持字符串（ID/枚举名）与整数；写时 mod 标签写 ID，有名原版写枚举名，未命名写 32 位整数。
  - `CardTagHashSetJsonConverter : JsonConverter<HashSet<CardTag>>`：JSON 数组，写时按值排序。

---

## 4. 特性（Attribute）清单 —— `STS2RitsuLib.Interop.AutoRegistration`

### 4.1 基类（公共）
| 类型 | 说明 |
| --- | --- |
| `AutoRegistrationAttribute` | 自动注册管线基类。属性：`int Order`（同阶段排序，小者先跑）、`bool Inherit`（基类声明时设为 true 才传给派生类；同一逻辑槽位最近声明胜出，不同目标 ID 累加，同类重复声明同一槽位报错）。 |
| `ContentRegistrationAttribute` | 内容注册基类（经 `ModContentRegistry` 分发）。 |
| `ModelPublicEntryRegistrationAttributeBase(Type poolType)` | 池模型注册基类：`PoolType`、`string? StableEntryStem`、`string? FullPublicEntry`。 |
| `RitsuLibOwnedByAttribute(string modId)` | 声明类型归属的 mod。 |

### 4.2 内容注册特性（标注目标均为 **Class**，`AllowMultiple = true, Inherited = false`；触发时机：**模型初始化前**，由自动注册管线扫描已注册程序集执行，注册后冻结）
| 特性 | 参数 | 作用 |
| --- | --- | --- |
| `RegisterCard(Type poolType)` | 卡池类型 | 注册为卡池中的卡牌（支持 `StableEntryStem`/`FullPublicEntry`） |
| `RegisterRelic(Type poolType)` / `RegisterPotion(Type poolType)` | 池类型 | 注册遗物 / 药水 |
| `RegisterCharacter` / `RegisterAct` / `RegisterMonster` | — | 注册角色 / 阶段 / 怪物模型 |
| `RegisterPower` / `RegisterOrb` | — | 注册能力 / 充能球模型 |
| `RegisterEnchantment` / `RegisterAffliction` | — | 注册附魔 / 侵蚀模型 |
| `RegisterAchievement` / `RegisterSingleton` | — | 注册成就 / 单例模型 |
| `RegisterModelCapability` | `StableEntryStem?`、`FullPublicEntry?` | 注册模型能力 |
| `RegisterDefaultModelCapability(Type targetModelType)` | `ModifierId?` | 把标注能力加入目标模型默认能力集 |
| `RegisterGoodModifier` / `RegisterBadModifier` | `ModifierListSortOrder` | 注册每日正面/负面特效（负值插段前，非负插段后） |
| `RegisterMutuallyExclusiveModifierGroup(params Type[] memberTypes)` | — | 特效互斥组 |
| `RegisterSharedCardPool` / `RegisterSharedRelicPool` / `RegisterSharedPotionPool` | — | 注册共享池 |
| `RegisterSharedEvent` / `RegisterSharedAncient` / `RegisterGlobalEncounter` | — | 共享事件 / 先古事件 / 全局遭遇 |
| `RegisterActEncounter(Type actType)` / `RegisterActEvent(Type actType)` / `RegisterActAncient(Type actType)` | 阶段类型 | 阶段限定遭遇 / 事件 / 先古事件 |
| `RegisterTrashHeapCard` / `RegisterTrashHeapRelic` | — | “垃圾堆”事件候选 |
| `RegisterCharacterStarterCard(Type characterType, int count = 1)` / `...StarterRelic` / `...StarterPotion` | 角色 + 份数 | 角色初始内容（`Order` 决定最终列表顺序） |
| `RegisterOwnedKeyword(string localKeywordStem)` | `TitleTable`(默认 `card_keywords`)、`TitleKey?`、`DescriptionTable?`、`DescriptionKey?`、`IconPath?`、`CardDescriptionPlacement`、`IncludeInCardHoverTip`(默认 true) | 注册 mod 关键词定义 |
| `RegisterOwnedCardKeyword(string localKeywordStem)` | `IconPath?`、`CardDescriptionPlacement`、`IncludeInCardHoverTip` | 按游戏卡牌关键词本地化约定注册关键词 |
| `RegisterOwnedCardTag(string localCardTagStem)` | — | 注册 mod 自定义 `CardTag`（经 `GetQualifiedCardTagId` 组合） |
| `RegisterOwnedCardPile(string localPileStem)` | 见下 | **声明式注册牌堆**（`ModCardPileRegistry`） |
| `RegisterOwnedTopBarButton(string localButtonStem)` | — | 声明式注册顶部栏按钮 |

### 4.3 `RegisterOwnedCardPile` 完整参数（均与 `ModCardPileSpec`/`ModCardPileAnchor` 对应）
`Scope`（默认 `CombatOnly`）、`Style`（默认 `Headless`）、`AnchorKind`（默认 `StyleDefault`）、`AnchorOffsetX/Y`、`AnchorCustomX/Y`、`AnchorCustomPivotX/Y`（Custom 模式）、`IconPath?`、`Hotkeys?`、`CardShouldBeVisible`、`ExtraHandDirection`（默认 `VanillaHand`）、`ExtraHandSpacing`（110）、`ExtraHandCardScale`（0.65）、`ExtraHandHoverScale`（1）、`ExtraHandShowPlayableGlow`（true）、`ExtraHandAllowCardPlay`（true）、`HoverTipOffsetX/Y`、`HoverTipPlacement`。
标注类型若实现 `IModCardPileHandler`，RitsuLib 以公共无参构造函数实例化并绑定 `OnOpen`。

### 4.4 其他注册特性（同命名空间）
- 节点挂载：`RegisterNodeAttachment(Type parentType, string localId)`、`RegisterNodeAttachmentFromScene(Type parentType, string localId, string scenePath)`、`RegisterNodeAttachmentFromConvertedScene(...)`。
- 智能格式：`RegisterSmartFormatter`、`RegisterSmartFormatSource`。
- 时间线：`RegisterStory`、`RegisterEpoch`、`RegisterStoryEpoch(Type storyType)`、`AutoTimelineSlot(EpochEra)`、`AutoTimelineSlotBeforeColumn/AfterColumn/InColumn(EpochEra)`、`AutoTimelineSlotBeforeEpochColumn/AfterEpochColumn/InEpochColumn(Type)`。
- 解锁：`RequireEpoch(Type epochType)`、`RequireAllCardsInPool(Type poolType)`、`RegisterEpochCards(params Type[])`、`RegisterEpochRelicsFromPool(Type poolType)`、`UnlockEpochAfterRunAs/WinAs/AscensionWin(epoch, level)/EliteVictories(epoch, n=15)/BossVictories(epoch, n=15)/AscensionOneWin(Type)`、`RevealAscensionAfterEpoch(Type)`、`UnlockCharacterAfterRunAs(Type)`。
- 特殊映射：`RegisterArchaicToothTranscendence(Type ancientCardType)`、`RegisterDustyTomeCard(Type characterType)`、`RegisterTouchOfOrobasRefinement(Type upgradedRelicType)`。

---

## 5. 关键用法模式

### 5.1 Entry 初始化与注册流程
三种入口（任选其一，注册须在**模型初始化前**完成，之后冻结）：

1. **属性式**（注册关系紧贴模型类型）：
   ```csharp
   // mod 初始化（Entry）中：
   ModTypeDiscoveryHub.RegisterModAssembly(modId, typeof(MyModEntry).Assembly);
   ```
   之后扫描到 `[RegisterCard(typeof(MyPool))]`、`[RegisterPower]`、`[RegisterOwnedCardTag("tag")]`、`[RegisterOwnedCardPile("pile")]` 等自动执行。

2. **Builder 式**（`CreateContentPack(modId)` 链式）：`.Card<TPool,TCard>()`、`.Power<TPower>()`、`.CardTagOwned(...)`、`.CardPileOwned(...)`、`.KeywordOwned(...)`，最后调用一次 `.Apply()`。

3. **直接注册器**（共享辅助库 / 条件注册）：
   ```csharp
   // 牌堆
   var def = ModCardPileRegistry.For(modId).RegisterOwned("overflow", new ModCardPileSpec {
       Scope = ModCardPileScope.CombatOnly,
       Style = ModCardPileUiStyle.BottomLeft,
       Anchor = ModCardPileAnchor.AtPosition(new(100, 500)),
       HoverTipPlacement = ModCardPileHoverTipPlacement.Auto,
       OnOpen = ctx => { /* 打开自定义界面或 ctx.ShowDefaultPileScreen() */ },
   });
   // 标签
   ModCardTagRegistry.For(modId).RegisterOwned("my_tag");
   // 内容包条目（声明式，可批量）
   var entry = CardPileRegistrationEntry.Owned(modId, "overflow", spec);
   entry.Register(ModCardPileRegistry.For(modId));
   ```

### 5.2 牌堆 ID 与本地化约定
- 限定 ID：`MODID_CARDPILE_STEM`（`ModContentRegistry.GetQualifiedCardPileId`），如 `com.example.my-mod` + `overflow_pile` → `MYMOD_CARDPILE_OVERFLOW_PILE`。
- 本地化键（`static_hover_tips` 表）：`{id}.title`、`{id}.description`、`{id}.empty`（空牌堆提示）。图标经 `IconPath` 指向 Godot 资源路径。
- `PileType` 可先用 `id.GetModCardPileType()` 取确定性值（无需注册），或 `ModCardPileRegistry.Get(id).PileType` 取已注册值。

### 5.3 生命周期与事件订阅
| 需求 | 方式 |
| --- | --- |
| 卡牌打出前/后钩子 | 效果/模型实现 `ICardOnPlayHookListener`，或 `CardOnPlayHook.RegisterGlobalListener(listener)`；`BeforeCardOnPlay` 返回 true 可跳过原版 `OnPlay` |
| 卡牌类型文本修改 | 卡牌实现 `ICustomTypeTextCard`、能力实现 `ICardTypeTextModifier`，或 `CardTypeTextHook.RegisterGlobalModifier(modifier)`；引用 `{Type}` 的文本为包装，否则替换 |
| 牌堆打开 | `ModCardPileSpec.OnOpen` / `[RegisterOwnedCardPile]` + `IModCardPileHandler.OnOpen`；回调内 `ShowDefaultPileScreen()` 或 `OpenCapstoneScreen(...)` |
| 牌堆可见性 | `Spec.VisibleWhen`（返回 false 隐藏控件并移除悬停提示；初始化期间 Player/Pile 可能为 null） |
| 飞行动画位置 | `Spec.FlightTargetPositionResolver` / `FlightStartPositionResolver`（返回 null 用默认位置） |
| 额外手牌回调 | `Spec.ExtraHand.LayoutResolver` / `OnCardVisualCreated` / `OnCardArrived`；运行时可 `NModExtraHand.SetCardPlayEnabled(bool)` 开关手动出牌 |
| 免费出牌 | `FreePlayBindingRegistry.Register(bindingId, detector)`（自定义判定）、`MarkCardFreeNextPlay/ThisTurn/ThisCombat`、`MarkCurrentPlayFree`、`CardModel.SetToFreeForRestOfTurn()` |
| 卡牌转化 | `ModCardTransformRegistry.For(modId).Register(listenerId, ...)` / `RegisterFrom<TOriginal>` / `RegisterTo<TReplacement>` |
| 跨战斗/跨局保存 | `Scope = RunPersistent` 的牌堆由 RitsuLib 自动写入 run saved data（`ModCardPilePlayerSaveState`），无需手动处理 |

### 5.4 卡片标签使用
```csharp
var tag = ModCardTagRegistry.For(modId).RegisterOwned("my_tag").CardTagValue; // 或 "ID".GetModCardTag()
card.AddModCardTag(tag);   // 要求 card.Tags 为可变 HashSet<CardTag>
card.HasModCardTag(tag);
// JSON 序列化：把 CardTagJsonConverter / CardTagHashSetJsonConverter 加入 JsonSerializerOptions.Converters
```

### 5.5 计算型动态变量使用
```csharp
// 在卡牌模型定义 DynamicVars 时：
new ModCardVars.Computed("MyVar", 3, card => /* 当前值 */)      // 简单
new ModCardVars.ComputedDamage("D", 5, (card, t) => /* 伤害 */) // 预览自动过 ModifyDamage
new ModCardVars.Computed(name, baseValue, ctx => {              // 上下文感知
    return ctx.HasTarget && ctx.Target.HasBlock ? 99 : ctx.BaseValue;
}).WithSharedTooltip("MyVar");                                  // 附悬停提示
// 读取：dynamicVars.TryGet<ComputedDynamicVar>(key, out var v) → v.Calculate(target)
```
