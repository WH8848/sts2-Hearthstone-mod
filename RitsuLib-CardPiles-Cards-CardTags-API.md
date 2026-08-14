# RitsuLib 源码解析：CardPiles / Cards / CardTags（mod 制作向）

> 分析对象：`STS2-RitsuLib\src\` 下 `CardPiles`、`Cards`、`CardTags` 三目录（含 `Interop\AutoRegistration` 中的注册特性）。
> 命名空间：`STS2RitsuLib.CardPiles`（节点在 `STS2RitsuLib.CardPiles.Nodes`）、`STS2RitsuLib.Cards`（子命名空间 `…Cards.DynamicVars` / `…Cards.FreePlay` / `…Cards.Transforms`）、`STS2RitsuLib.CardTags`、特性在 `STS2RitsuLib.Interop.AutoRegistration`。
> 本文只列公共 API 与实用模式，跳过 Patch / 内部实现。

---

## 一、CardTags —— 自定义卡牌标签（动态 CardTag 枚举）

动态值由全局 `DynamicEnumValueRegistry<CardTag>` 铸造，注册在模型初始化前冻结（`IsFrozen` 后禁止注册）。

### 公共 API 清单

| 类型 | 用途与关键成员 |
|---|---|
| `ModCardTagDefinition`（record） | 已注册标签定义：`string ModId`、`string Id`、`CardTag CardTagValue` |
| `ModCardTagRegistry`（静态注册器） | `static ModCardTagRegistry For(string modId)` 取/建注册器；`static bool IsFrozen`；<br>`ModCardTagDefinition RegisterOwned(string localTagStem)`（ID 经 `GetQualifiedCardTagId` 限定）；<br>`ModCardTagDefinition Register(string id)`（全局 ID，跨 mod 冲突被拒）；<br>查找：`TryGet/Get(string id)`、`Get(CardTag)`、`TryGetByCardTag`、`IsModCardTag`、`TryGetOwnerModId`、`TryGetCardTag/GetCardTag(string id)`（确定性值，无需注册）、`TryResolveCardTag`（已注册 ID > 原版枚举名 > 动态值）、`TryGetId(CardTag)`、`GetDefinitionsSnapshot()` |
| `ModCardTagExtensions` | `CardModel.AddModCardTag(CardTag)`（要求 `Tags` 是可变 `HashSet<CardTag>`）、`RemoveModCardTag`、`HasModCardTag`；`string.GetModCardTag()`、`CardTag.TryGetModCardTagId(out id)`、`CardTag.GetModCardTagId()` |
| `CardTagRegistrationEntry`（record） | 声明式注册条目：`CardTagRegistrationEntry(string Id)`；`void Register(ModCardTagRegistry)`；`static Owned(string modId, string localTagStem)` |
| `Serialization.CardTagJsonConverter` | `JsonConverter<CardTag>`：模组标签写 ID、原版命名值写枚举名、其余写 32 位整数；读取时用 `TryResolveCardTag` |
| `Serialization.CardTagHashSetJsonConverter` | `JsonConverter<HashSet<CardTag>>`：JSON 数组 ↔ 标签集合（写出按值排序） |

### 用法模式

```csharp
// 初始化期（冻结前）注册
var tagDef = ModCardTagRegistry.For(MyModId).RegisterOwned("my_tag");
card.AddModCardTag(tagDef.CardTagValue);
bool has = card.HasModCardTag("MYMOD_MY_TAG".GetModCardTag());
// 声明式：[RegisterOwnedCardTag("my_tag")] 标注任意具体类即可
```

---

## 二、CardPiles —— 自定义卡牌牌堆

### 2.1 核心注册 API

**`ModCardPileRegistry`**（静态，按 modId 分注册器，进程级共享，模型初始化前冻结）：

- `static ModCardPileRegistry For(string modId)`
- `ModCardPileDefinition RegisterOwned(string localStem, ModCardPileSpec spec)` —— 推荐；ID 规范化为全限定形式（大写 `MODID_...`）
- `ModCardPileDefinition Register(string id, ModCardPileSpec spec)` —— 全局 ID，跨 mod 重复注册报错；同 mod 重复注册返回既有定义
- 解析：`TryGet/Get(string id)`、`Get(PileType)`、`TryGetByPileType`、`IsModPileType`、`TryGetOwnerModId`、`GetPileType(string id)`（确定性动态值，无需注册）、`TryResolvePileType`、`TryGetId(PileType)`
- 悬停提示：`static HoverTip CreateHoverTip(string id)` / `CreateHoverTip(PileType)`
- 快照：`GetDefinitionsSnapshot()`（按 ID 排序）

> 动态 `PileType` 由全局 `DynamicEnumValueRegistry<PileType>` 铸造，保留高值区间 `[0x4000_0000, 0x7FFF_FFFF]`。

**`ModCardPileDefinition`**（record，注册产物，全部只读）：

| 成员 | 说明 |
|---|---|
| `ModId` / `Id` / `PileType` / `Scope` / `Style` / `Anchor` | 基本标识与配置 |
| `LocString Title / Description / EmptyPileMessage` | 本地化文本，键为 `{Id}.title` / `{Id}.description` / `{Id}.empty`，表 `static_hover_tips` |
| `IconPath` / `Hotkeys` / `CardShouldBeVisible` | 图标路径、打开牌堆界面快捷键、ExtraHand 是否渲染卡牌节点 |
| `Action<ModCardPileOpenContext>? OnOpen` | 打开回调，null = 默认牌堆界面 |
| `Func<ModCardPileVisibilityContext, bool>? VisibleWhen` | 控件可见性谓词（false 隐藏并移除悬停提示；异常按隐藏处理） |
| `Func<ModCardPileFlightTargetContext, Vector2?>? FlightTargetPositionResolver` | 卡牌飞入动画目标位置解析器（null = 默认位置） |
| `Func<ModCardPileFlightStartContext, Vector2?>? FlightStartPositionResolver` | 洗牌飞行动画起始位置解析器 |
| `ModCardPileViewSpec? View` | 默认牌堆界面扩展 |
| `ModCardPileExtraHandSpec ExtraHand` | ExtraHand 样式配置 |
| `Vector2 HoverTipScreenOffset` / `ModCardPileHoverTipPlacement HoverTipPlacement` | 悬停提示偏移与放置规则 |

**`ModCardPileSpec`**（record，注册入参，`init` 属性，均有默认值）：`Scope=CombatOnly`、`Style=Headless`、`Anchor=Default`、`IconPath`、`Hotkeys`、`CardShouldBeVisible`、`ExtraHand=new()`、`HoverTipScreenOffset`、`HoverTipPlacement=Auto`、`VisibleWhen`、`View`、`OnOpen`、`FlightTargetPositionResolver`、`FlightStartPositionResolver`。文本统一走 `static_hover_tips` 表。

**`CardPileRegistrationEntry`**（record）：`CardPileRegistrationEntry(string id, ModCardPileSpec spec)`；`void Register(ModCardPileRegistry)`；`static Owned(string modId, string localPileStem, ModCardPileSpec)`。

### 2.2 运行时类型

- **`ModCardPile : CardPile`**（sealed）：`ModCardPile(ModCardPileDefinition)`；属性 `ModCardPileDefinition Definition`。可订阅牌堆事件 `ContentsChanged` / `CardAddFinished` / `CardRemoveFinished`（数量徽标由这些事件驱动）。
- **`ModCardPileScope`**（enum）：`CombatOnly`（挂在 `PlayerCombatState`，随战斗清理，参与 `AllPiles`/`IsCombatPile`）；`RunPersistent`（挂在 `Player`，跨战斗保留，随跑局存档序列化——由 `ModCardPilePersistence` 处理，保存键 `run_persistent_card_piles`，公开了存档类型 `ModCardPilePlayerSaveState`：`Dictionary<string, List<SerializableCard>> Piles`）。
- **`ModCardPileUiStyle`**（enum）：`Headless`（无 UI，飞行位置用锚点或视口中心）、`TopBarDeck`（顶栏牌组按钮旁）、`BottomLeft`（抽牌堆旁）、`BottomRight`（消耗牌堆旁）、`ExtraHand`（交互式额外手牌容器，支持原版焦点/悬停提示/高亮/布局/手动出牌）。
- **`ModCardPileAnchorKind`**（enum）：`StyleDefault`、`BottomLeftPrimary/Secondary`、`BottomRightPrimary/Secondary`、`TopBarAfterDeck`、`TopBarBeforeModifiers`、`ExtraHandAbove/Below`、`Custom`。
- **`ModCardPileAnchor`**（readonly record struct）：`(Kind, Offset, CustomPosition, CustomAuthoringPivot)`；静态 `Default`、`AtPosition(Vector2 upperLeft)`、`AtCenter(Vector2)`、`AtPivot(Vector2, Vector2 pivotFraction)`；`PivotUpperLeft`/`PivotCenter` 常量。
- **`ModCardPileHoverTipPlacement`**（enum）：`Auto`、`BelowButtonTrailingEdge`、`AboveButtonCentered`、`BelowButtonCentered`。
- **`ModCardPileSortOption`**（enum，牌堆界面排序）：`Obtained / Type / Cost / Alphabetical / Rarity`。
- **`ModCardPileHoverTipFactory`**：`static HoverTip Create(ModCardPileDefinition)`（标题+描述+可选图标）。
- **`ModCardPileHoverTipViewport`**：`static Vector2 ClampTipTopLeft(NHoverTipSet, Vector2)`（视口内约束，8px 边距）。
- **`ModCardPileOpenContext`**（打开回调入参）：`Definition`、`Pile`、`Player`、`Button`；方法 `ShowDefaultPileScreen()`（原版 `NCardPileScreen` + 注册快捷键）、`OpenCapstoneScreen(ICapstoneScreen)`。
- **`ModCardPileVisibilityContext`**：`Definition`、`Player?`、`Button?`、`Pile?`（初始化期间可能为 null，谓词需容错）。
- **`IModCardPileFlightContext`**：`Definition`、`DefaultPosition`、`StartPile?`、`TargetPile?`、`CardNode?`、`CardModel?`；具体实现 `ModCardPileFlightTargetContext`（含 `DefaultTargetPosition`）与 `ModCardPileFlightStartContext`（含 `DefaultStartPosition`）。
- **`IModCardPileHandler`**：`void OnOpen(ModCardPileOpenContext context)` —— 标注 `[RegisterOwnedCardPile]` 的类型若实现此接口，自动注册会经无参构造实例化并把 `OnOpen` 赋给 `Spec.OnOpen`。

### 2.3 界面（View）与额外手牌（ExtraHand）

**`ModCardPileViewSpec`**（默认牌堆界面扩展）：静态 `DeckLike`（检查+升级预览+排序栏）；`EnableCardInspect`、`EnableUpgradePreviewToggle`、`EnableSortBar`、`SortOptions`、`DefaultSorting`、工具栏/排序按钮贴图材质（`ToolbarBackgroundTexturePath/Material/Provider`、`SortButtonHueMaterial/Provider`、`DisableSortButtonHue`、`SortButtonBackgroundTexturePath/Material/Provider`）、`UpgradePreviewLabelColor/OutlineColor`。**`ModCardPileViewStyleContext`**（record）：`(Definition, Pile, NCardPileScreen Screen)`。

**`ModCardPileExtraHandSpec`**：`Direction`（`ModExtraHandLayoutDirection`：`Horizontal/Vertical/VanillaHand`）、`Spacing=110`、`CardScale=0.65`、`HoverScale=1`、`ShowPlayableGlow=true`、`AllowCardPlay=true`、`DisabledOffset=(0,100)`、`DisabledModulate=灰`、`DisabledTransitionDuration=0.2`、`Func<ModExtraHandCardContext, ModExtraHandCardTransform?>? LayoutResolver`（逐卡布局，null=内置）、`Action<ModExtraHandCardContext>? OnCardVisualCreated` / `OnCardArrived`（生命周期回调）。
**`ModExtraHandCardTransform`**（record struct）：`(Vector2 Position, Vector2 Scale, float RotationDegrees=0, int ZIndex=0)`。
**`ModExtraHandCardContext`**：`Definition`、`Container(NModExtraHand)`、`Card(CardModel)`、`Holder(NHandCardHolder)`、`CardNode`、`Index`、`Count`、`IsFocused`、`DefaultTransform`。

### 2.4 节点（`STS2RitsuLib.CardPiles.Nodes`）

- **`NModCardPileButton : Control`**（sealed，程序化按钮，双模式）：
  - 牌堆模式：`static Create(ModCardPileDefinition)`；`void Initialize(Player)`（解析并监听 ModCardPile，刷新数量/可见性）；`TriggerOpen()`（程序化等同点击）；`Definition`。
  - 操作模式（顶栏独立按钮）：`static CreateAction(ModTopBarButtonDefinition)`；`IsActionMode`；`ActionDefinition`。
- **`NModTopBarPileButton`**：`static NModCardPileButton Create(ModCardPileDefinition)`（薄封装）。
- **`NModExtraHand : Control`**（sealed）：`static Create(ModCardPileDefinition)`；`void Initialize(Player)`；`void SetCardPlayEnabled(bool)`（定义未允许出牌时开启会抛异常；禁用会取消活动目标选择并恢复卡牌）；`NCard? GetCard(CardModel)`、`NHandCardHolder? GetHolder(CardModel)`；属性 `Definition`、`CardPlayEnabled`。

---

## 三、Cards —— 卡牌扩展

### 3.1 打出钩子（`STS2RitsuLib.Cards`）

**`CardOnPlayHook`**（静态）：
- `interface ICardOnPlayHookListener`：`Task<bool> BeforeCardOnPlay(BeforeCardOnPlayContext)`（返回 true 跳过卡牌自身 `OnPlay`，但不跳过其余流程）；`Task AfterCardOnPlay(AfterCardOnPlayContext)`（在自身 OnPlay 之后、附魔/侵蚀/`Hook.AfterCardPlayed` 之前）。
- 上下文：`BeforeCardOnPlayContext(CombatState, PlayerChoiceContext, CardPlay)`；`AfterCardOnPlayContext(…, bool OriginalOnPlayRan)`。
- API：`RegisterGlobalListener(ICardOnPlayHookListener)`；`RunCardOnPlayHooks(CardModel, PlayerChoiceContext, CardPlay)`。
- 触发：模型/能力实现接口自动生效（经 ModelHookListenerDispatcher 从战斗状态收集），或注册全局监听器。

**`CardTypeTextHook`**（静态，BaseLib 兼容的卡牌类型文本修改）：
- `RegisterGlobalModifier(ICardTypeTextModifier)`。
- 契约：修改器条目含 `{Type}` 的**包裹**基础文本，不含的**替换**基础文本；来源优先级：卡牌自身实现 `ICustomTypeTextCard` → 模型能力 `ICardTypeTextModifier` → run/combat 监听器 → 全局注册。两接口定义于 `STS2RitsuLib.Models.Capabilities`（`ICustomTypeTextCard.GetTypeModifiers()`；`ICardTypeTextModifier.GetTypeModifiers(CardModel)`）。

### 3.2 动态变量（`STS2RitsuLib.Cards.DynamicVars`）

**`ModCardVars`**（静态工厂，创建游戏 `DynamicVar`）：
- 基础：`Int / String / Bool / Cards / Damage / OstyDamage / Block / Gold / Heal / HpLoss / MaxHp / Repeat / Forge / Summon / Energy / Stars`（均可带 name 重载）、`Power<T>(amount)`（以能力类型名命名）。
- 计算型：`Computed(name, baseValue, currentFactory[, previewFactory])`、`ComputedEnergy / ComputedStars / ComputedPower<T> / ComputedPowerAmountGiven<T>`、`ComputedDamage / ComputedOstyDamage / ComputedBlock`（预览自动走 `Hook.ModifyDamage/ModifyBlock/ModifyPowerAmountGiven` 与附魔修正；可带自定义伤害来源/格挡接收者工厂）。求值委托三形态：`Func<CardModel?, decimal>`、`Func<CardModel?, Creature?, decimal>`（目标感知）、`ComputedDynamicVarFactory`（上下文感知）。

**计算变量类型**：`ComputedDynamicVar : DynamicVar, IComputedDynamicVar`、`ComputedEnergyVar : EnergyVar, …`、`ComputedStarsVar : StarsVar, …`、`ComputedPowerVar<T> : PowerVar<T>, …`（`T : PowerModel`）。共同成员：`decimal Calculate(Creature? target = null)`、`override UpdateCardPreview(...)`；接口 `IComputedDynamicVar`（`decimal Calculate(Creature? target = null)`）。预览求值：`previewValueFactory(CardModel?, CardPreviewMode, Creature? target, bool runGlobalHooks)`，省略时回退 `currentValueFactory`。

**`ComputedDynamicVarContext`**（上下文求值入参）：`Variable / ModelOwner / Card? / Target? / PreviewMode? / RunGlobalHooks`；便捷判定：`IsPreview / IsCurrentValue / IsNormalPreview / IsUpgradePreview / IsMultiTargetPreview / IsEnchantmentPreview / ShouldRunGlobalHooks`；属性链：`Player / SourceCreature / RunState / CombatState / CardScope / CardVars` 及各自 `HasXxx`；读取：`TryGetCardVar(name[, TVar])`、`GetRequiredCardVar<TVar>`、`GetCardBaseValueOrDefault / GetCardIntOrDefault / EvaluateCardVarOrDefault`（带循环求值防护）。委托：`public delegate decimal ComputedDynamicVarFactory(ComputedDynamicVarContext)`。

**`DynamicVarExtensions`**（工具）：
- 工具提示：`WithTooltip(Func<DynamicVar, IHoverTip>)`、`WithTooltip(titleTable, titleKey[, descriptionTable, descriptionKey, iconPath])`、`WithSharedTooltip(entryPrefix[, iconPath])`（`static_hover_tips` 的 `{prefix}.title/.description`）、`CreateHoverTip()`。
- 读取：`DynamicVarSet.TryGet<TVar> / GetRequired<TVar> / GetIntOrDefault / GetValueOrDefault / HasPositiveValue / TryComputeValue / GetComputedValue / EvaluateValueOrDefault / ComputeDynamicValue / ComputeEnergyValue / ComputePowerValue<T> / ComputeStarsValue`。
- 工具提示注册底层为 `DynamicVarTooltipRegistry`：`Set / Get / Create / CopyTo`（弱关联，不泄漏）。

### 3.3 免费出牌（`STS2RitsuLib.Cards.FreePlay`）

**`CardModelFreePlayExtensions`**：`SetToFreeForRestOfTurn(this CardModel)` —— 固定能量/星星/已注册次级资源费用本回合免费，打出后**不**清除（区别于 `SetToFreeThisTurn`），回合结束清理时失效；X 费用保持原行为。

**`FreePlayBindingRegistry`**（静态）：
- `Register(string bindingId, Func<CardPlay, bool> detector)` —— 注册自定义免费判定器。
- 标记：`MarkCardFreeNextPlay(CardModel)`（一次性）、`MarkCardFreeThisTurn(CardModel)`（本回合）、`MarkCardFreeThisCombat(CardModel)`、`MarkCurrentPlayFree(CardPlay)`。
- 查询：`Resolve(CardPlay) → FreePlayResolution`（`IsAutoPlayNoSpend / IsCardBindingFree / IsRegisteredDetectorFree` + `IsFree`，按 CardPlay 缓存）、`IsFreeForPlay(CardPlay)`、`IsCardFreeForUpcomingPlay(CardModel)`（不消耗 next-play 次数）。
- 清理：`ClearCardFreeThisTurn(CardModel)`（回合结束）、`ClearCardFreeAfterPlayed(CardModel)`（打出后消耗/清除）。

### 3.4 卡牌转化监听（`STS2RitsuLib.Cards.Transforms`）

**`ModCardTransformContext`**（record struct）：`(CardModel Original, CardModel Replacement, CardPile OriginalPile, int OriginalPileIndex)` —— 每次游戏本体卡牌转化完成时产生。

**`ModCardTransformRegistry`**（按 mod 分注册器）：
- `static For(string modId)`；`bool Unregister(string listenerId)`。
- `Register(string listenerId, Action<…> / Func<…,Task>)`（全部转化）；
- `Register(string listenerId, Func<ModCardTransformContext,bool> predicate, Action/Func<Task>)`（谓词过滤）；
- 类型化：`Register<TOriginal,TReplacement>(…)`、`RegisterFrom<TOriginal>(…)`、`RegisterTo<TReplacement>(…)`（同步/异步双载）。同 ID 重复注册 = 替换；按注册顺序调用；监听器异常记录日志后向上抛。

---

## 四、特性（Attribute）清单

统一位于 `STS2RitsuLib.Interop.AutoRegistration`。目标均为 **class**（`AttributeTargets.Class`，`AllowMultiple=true`，默认 `Inherited=false`）。触发时机：**模组程序集加载时由 RitsuLib 自动注册管线（`AttributeAutoRegistrationTypeDiscoveryContributor`）扫描并注册，模型初始化前冻结**（冻结后注册抛异常）。基类公共属性：`int Order`（同阶段局部排序，小者先）、`bool Inherit`（基类声明可被派生类继承/就近覆盖）。

| 特性 | 参数/属性 | 触发行为 |
|---|---|---|
| `RegisterCardAttribute` | `Type poolType`（必填）；`StableEntryStem`、`FullPublicEntry`（可空） | 将标注类型注册进指定**卡牌池** |
| `RegisterRelicAttribute` | 同上 | 注册进遗物池 |
| `RegisterPotionAttribute` | 同上 | 注册进药水池 |
| `RegisterCharacterAttribute` | — | 注册为角色模型 |
| `RegisterActAttribute` | — | 注册为阶段模型 |
| `RegisterMonsterAttribute` | — | 注册为怪物模型 |
| `RegisterPowerAttribute` | — | 注册为能力模型 |
| `RegisterOrbAttribute` | — | 注册为充能球模型 |
| `RegisterEnchantmentAttribute` | — | 注册为附魔模型 |
| `RegisterAfflictionAttribute` | — | 注册为侵蚀模型 |
| `RegisterAchievementAttribute` | — | 注册为成就模型 |
| `RegisterSingletonAttribute` | — | 注册为单例模型 |
| `RegisterModelCapabilityAttribute` | `StableEntryStem`、`FullPublicEntry` | 注册为模型能力 |
| `RegisterDefaultModelCapabilityAttribute` | `Type targetModelType`；`ModifierId` | 将标注能力类型加入目标模型类型的默认能力集 |
| `RegisterGoodModifierAttribute` / `RegisterBadModifierAttribute` | `ModifierListSortOrder` | 注册为每日/自定模式的正面/负面特效 |
| `RegisterMutuallyExclusiveModifierGroupAttribute` | `params Type[] memberTypes` | 注册互斥特效组 |
| `RegisterSharedCardPoolAttribute` / `RegisterSharedRelicPoolAttribute` / `RegisterSharedPotionPoolAttribute` | — | 注册共享卡池/遗物池/药水池 |
| `RegisterSharedEventAttribute` / `RegisterSharedAncientAttribute` | — | 注册共享事件 / 先古事件 |
| `RegisterGlobalEncounterAttribute` | — | 注册全局遭遇 |
| `RegisterTrashHeapCardAttribute` / `RegisterTrashHeapRelicAttribute` | — | 加入“垃圾堆”事件 Grab/Dive 候选 |
| `RegisterCharacterStarterCardAttribute` | `Type characterType, int count=1` | 注册为角色初始卡牌 |
| `RegisterCharacterStarterRelicAttribute` / `RegisterCharacterStarterPotionAttribute` | 同上 | 角色初始遗物/药水 |
| `RegisterActEncounterAttribute` / `RegisterActEventAttribute` / `RegisterActAncientAttribute` | `Type actType` | 注册到指定阶段的遭遇/事件/先古事件 |
| `RegisterOwnedKeywordAttribute` | `string localKeywordStem`；`TitleTable="card_keywords"`、`TitleKey?`、`DescriptionTable?`、`DescriptionKey?`、`IconPath?`、`CardDescriptionPlacement`、`IncludeInCardHoverTip=true` | 注册模组关键词（含内联卡牌描述注入） |
| `RegisterOwnedCardKeywordAttribute` | `string localKeywordStem`；`IconPath?`、`CardDescriptionPlacement`、`IncludeInCardHoverTip=true` | 按游戏卡牌关键词本地化约定注册关键词 |
| `RegisterOwnedCardTagAttribute` | `string localCardTagStem` | 注册模组自定义 `CardTag`（经 `GetQualifiedCardTagId` 限定） |
| `RegisterOwnedCardPileAttribute` | `string localPileStem`；`Scope=CombatOnly`、`Style=Headless`、`AnchorKind=StyleDefault`、`AnchorOffsetX/Y`、`AnchorCustomX/Y`、`AnchorCustomPivotX/Y`、`IconPath?`、`Hotkeys?`、`CardShouldBeVisible`、`ExtraHandDirection=VanillaHand`、`ExtraHandSpacing=110`、`ExtraHandCardScale=0.65`、`ExtraHandHoverScale=1`、`ExtraHandShowPlayableGlow=true`、`ExtraHandAllowCardPlay=true`、`HoverTipOffsetX/Y`、`HoverTipPlacement` | 声明式注册模组牌堆（等价 `ModCardPileSpec`）；类型实现 `IModCardPileHandler` 则经无参构造实例化并绑定 `OnOpen`；悬停提示文本键 `{id}.title/.description/.empty` 写进 `static_hover_tips.json` |
| `RegisterOwnedTopBarButtonAttribute` | `string localButtonStem` | 注册顶栏独立按钮（`ModTopBarButtonRegistry`，见 TopBar 模块） |
| `RegisterSmartFormatterAttribute` / `RegisterSmartFormatSourceAttribute` | — | 注册 SmartFormat 格式化器 / 格式化源 |
| `RegisterNodeAttachmentAttribute` | `Type parentType, string localId` | 声明式节点挂载 |
| `RegisterNodeAttachmentFromSceneAttribute` | `Type parentType, string localId, string scenePath` | 从场景文件挂载节点 |
| `RegisterNodeAttachmentFromConvertedSceneAttribute` | 同上（转场版） | 挂载转换后的场景节点 |
| `RegisterEpochAttribute` | — | 注册时间线历史节点 |
| `RegisterStoryAttribute` / `RegisterStoryEpochAttribute(Type storyType)` | — | 注册故事 / 故事-历史节点归属 |
| `AutoTimelineSlotAttribute(EpochEra)` / `AutoTimelineSlotBeforeColumn(EpochEra)` / `AutoTimelineSlotAfterColumn(EpochEra)` / `AutoTimelineSlotInColumn(EpochEra)` | `EpochEra` 锚点 | 历史节点时间线列自动占位 |
| `AutoTimelineSlotBeforeEpochColumn(Type)` / `AutoTimelineSlotAfterEpochColumn(Type)` / `AutoTimelineSlotInEpochColumn(Type)` | `Type referenceEpochType` | 按参考历史节点列占位 |
| `RegisterEpochCardsAttribute(params Type[])` | `IReadOnlyList<Type> CardTypes` | 历史节点揭示的卡牌解锁内容 |
| `RequireAllCardsInPoolAttribute(Type poolType)` | — | 卡池内所有卡牌要求先揭示该历史节点 |
| `RegisterEpochRelicsFromPoolAttribute(Type poolType)` | — | 遗物池全部遗物作为历史节点解锁内容 |
| `RequireEpochAttribute(Type epochType)` | — | 标注内容解锁需先揭示指定历史节点 |
| `UnlockEpochAfterRunAsAttribute(Type)` / `UnlockEpochAfterWinAsAttribute(Type)` / `UnlockEpochAfterAscensionOneWinAttribute(Type)` / `UnlockEpochAfterAscensionWinAttribute(Type, int)` / `UnlockEpochAfterEliteVictoriesAttribute(Type, int=15)` / `UnlockEpochAfterBossVictoriesAttribute(Type, int=15)` / `RevealAscensionAfterEpochAttribute(Type)` / `UnlockCharacterAfterRunAsAttribute(Type)` | 均为 `Type epochType`（+数值） | 以标注角色达成条件后解锁/揭示历史节点或进阶界面 |
| `RegisterArchaicToothTranscendenceAttribute(Type ancientCardType)` | — | “古老牙齿”超越映射（初始卡→先古卡） |
| `RegisterDustyTomeCardAttribute(Type characterType)` | — | “尘封魔典”优先候选 |
| `RegisterTouchOfOrobasRefinementAttribute(Type upgradedRelicType)` | — | “欧洛巴斯之触”精炼映射（初始遗物→升级遗物） |
| `RitsuLibOwnedByAttribute(string modId)` | — | 覆盖标注类型上声明的自动注册特性的归属 mod（含继承注册） |

> 另有 Interop 属性（`STS2RitsuLib.Interop`）：`ModInteropAttribute(modId, type?)`、`AssemblyInteropAttribute(type?)`、`InteropAnyParamAttribute`、`InteropTargetAttribute` —— 用于跨 mod 互操作标记，与卡牌内容注册无直接关系，略。

---

## 五、关键用法模式

### 5.1 Entry 初始化与注册流程

```
Mod 入口初始化
  └─ 自动注册管线扫描模组程序集（[RegisterXxx] 特性）→ ModContentRegistry 分发
  └─ 或代码注册（须在模型初始化前、冻结前完成）：
       ModCardPileRegistry.For(modId).RegisterOwned(stem, spec)
       ModCardTagRegistry.For(modId).RegisterOwned(stem)
       ModCardTransformRegistry.For(modId).Register(...)
  └─ 模型初始化前：各注册表 FreezeRegistrations（IsFrozen=true，之后注册抛异常）
```

### 5.2 牌堆（最常用组合）

```csharp
// 代码注册：战斗底部按钮 + 点击打开默认牌堆界面
var spec = new ModCardPileSpec {
    Scope = ModCardPileScope.CombatOnly,
    Style = ModCardPileUiStyle.BottomLeft,
    IconPath = "res://mod/icons/pile.png",
    OnOpen = ctx => ctx.ShowDefaultPileScreen(),
    View = ModCardPileViewSpec.DeckLike,          // 检查+升级预览+排序
};
ModCardPileRegistry.For(MyModId).RegisterOwned("my_pile", spec);

// 声明式：标注类 + 可选 IModCardPileHandler（自动实例化并绑定 OnOpen）
[RegisterOwnedCardPile("my_pile", Style = ModCardPileUiStyle.TopBarDeck,
    Scope = ModCardPileScope.RunPersistent, IconPath = "res://mod/icons/pile.png")]
public sealed class MyPileHandler : IModCardPileHandler {
    public void OnOpen(ModCardPileOpenContext ctx) => ctx.ShowDefaultPileScreen();
}
```
- 本地化：`static_hover_tips.json` 中提供 `{MODID_MY_PILE}.title` / `.description` / `.empty`。
- 运行期取牌堆：`ModCardPileRegistry.Get(pileId)` / `Get(PileType)`；`CardPile` 操作与普通牌堆一致（`AddInternal/RemoveInternal/Cards` 等）。
- 生命周期订阅：按钮/容器经 `Initialize(Player)` 自动订阅 `ContentsChanged`/`CardAddFinished`/`CardRemoveFinished` 刷新数量；`RunPersistent` 牌堆内容由 RitsuLib 随跑局存档自动序列化/恢复。

### 5.3 动态变量 + 工具提示

```csharp
// 卡牌定义处创建计算变量（支持预览钩子与工具提示）
var dmg = ModCardVars.ComputedDamage("Dmg", 6, (card, target) => 6 + card?.Owner.Creature.Strength ?? 0)
    .WithSharedTooltip("my_mod_keyword_dmg");   // static_hover_tips: my_mod_keyword_dmg.title/.description
card.DynamicVars.Add(dmg);
// 上下文求值（读其它变量、目标、预览模式等）：
ModCardVars.Computed("X", 0, ctx => ctx.GetCardIntOrDefault("Dmg") * ctx.Target?.Count ?? 0);
```
- 预览求值：提供 `previewValueFactory(card, previewMode, target, runGlobalHooks)` 可区分正常/升级/附魔预览；`ComputedDamage/Block/PowerAmountGiven` 系列自动接入全局 `Hook.Modify*`。
- 读取：`card.DynamicVars.EvaluateValueOrDefault("Dmg", target: t)` / `GetComputedValue("Dmg", t)`。

### 5.4 打出钩子 / 类型文本 / 转化 / 免费出牌

```csharp
// 打出前后钩子：模型或能力直接实现 ICardOnPlayHookListener；进程级用：
CardOnPlayHook.RegisterGlobalListener(new MyListener());   // BeforeCardOnPlay 返回 true 可跳过原 OnPlay

// 类型文本修改（BaseLib 兼容）：含 {Type} 包裹、不含则替换
public sealed class MyCard : CardModel, ICustomTypeTextCard {
    public IEnumerable<LocString> GetTypeModifiers() => [new("card_keywords", "MY_TYPE")]; // 含 {Type} 则包裹
}

// 卡牌转化监听（所有转化 / 谓词过滤 / 类型化）
ModCardTransformRegistry.For(MyModId)
    .Register("on_to_strike", (MyCard src, Strike dst) => dst.Upgrade());

// 免费出牌
FreePlayBindingRegistry.MarkCardFreeThisTurn(card);      // 或 MarkCardFreeNextPlay / ThisCombat
FreePlayBindingRegistry.Register("my_rule", play => play.Card.HasTag(MyFreeTag)); // 自定义免费规则
card.SetToFreeForRestOfTurn();                            // 整回合免费且打出后不清除
```

### 5.5 ExtraHand 进阶

```csharp
new ModCardPileSpec {
    Style = ModCardPileUiStyle.ExtraHand,
    CardShouldBeVisible = true,
    Anchor = ModCardPileAnchor.AtCenter(new(960f, 540f)),
    ExtraHand = new ModCardPileExtraHandSpec {
        Direction = ModExtraHandLayoutDirection.Horizontal,
        LayoutResolver = ctx => new ModExtraHandCardTransform(
            new Vector2(ctx.Index * 110f, 0f), Vector2.One * 0.7f, ctx.IsFocused ? 0f : -5f),
        OnCardArrived = ctx => DoSomething(ctx.Card),
    },
};
```
- 手动出牌：`AllowCardPlay=true`（默认）即走原版目标选择/行动队列/费用支付流程；运行期可用 `NModExtraHand.SetCardPlayEnabled(false)` 临时禁用（会取消活动目标选择并恢复卡牌，但不会取消已排队行动）。

---

## 六、速查：三目录 API 一览（跳读索引）

- **CardTags**：注册 `ModCardTagRegistry` → 定义 `ModCardTagDefinition` → 使用 `ModCardTagExtensions`；序列化转换器在 `STS2RitsuLib.CardTags.Serialization`。
- **CardPiles**：入参 `ModCardPileSpec` → 注册 `ModCardPileRegistry` / `[RegisterOwnedCardPile]` → 产物 `ModCardPileDefinition` → 运行时 `ModCardPile`（`CardPile` 子类）+ 节点 `NModCardPileButton` / `NModExtraHand`；回调上下文 `Open/Visibility/Flight*`；存档 `ModCardPilePlayerSaveState`。
- **Cards**：`CardOnPlayHook`（打出前后）、`CardTypeTextHook`（类型文本）、`DynamicVars`（`ModCardVars` 工厂 + `Computed*Var` + `ComputedDynamicVarContext` + `DynamicVarExtensions`）、`FreePlay`（`CardModelFreePlayExtensions` + `FreePlayBindingRegistry`）、`Transforms`（`ModCardTransformRegistry` + `ModCardTransformContext`）。
