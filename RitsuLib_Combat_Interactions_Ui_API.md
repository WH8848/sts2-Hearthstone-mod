# RitsuLib 源码分析：Combat / Interactions / Ui 公共 API 速查

> 依据 `E:\MOD\sts2\STS2-RitsuLib\src\` 下 Combat、Interactions、Ui 三目录精读整理（另含全库 Attribute 清单）。
> 命名空间均以 `STS2RitsuLib` 为根。仅收录对 mod 制作有实用价值的公共 API，内部实现细节从略。

## 0. 命名空间总览

| 目录 | 命名空间 |
|---|---|
| Combat | `STS2RitsuLib.Combat.{AttackHits,CardTargeting,HandSize,Healing,HealthBars,PlayerResources,Powers,Rewards,SecondaryResources,Ui.ExtraCornerAmountLabels}` |
| Interactions | `STS2RitsuLib.Interactions.RightClick` |
| Ui | `STS2RitsuLib.Ui.{Toast,RichTextEffects,Windows,Catalog,Shell.Theme,Overlay}` |
| 特性 | `STS2RitsuLib.Interop.AutoRegistration`（+ `Settings.ModSettings.Mirrors.RuntimeReflection`） |

---

## 1. Combat

### 1.1 攻击命中 AttackHits（`STS2RitsuLib.Combat.AttackHits`）

- **`IAttackHitHookListener`**（interface）— 单次攻击命中钩子，两个成员均有默认空实现：
  - `Task BeforeAttackHit(AttackHitContext context)`：伤害命令执行前，可 await 游戏命令、修改上下文输入（仅影响本次命中）。
  - `Task AfterAttackHit(AttackHitContext context)`：本次命中伤害结算后。
  - 由 `AttackCommand` 的每次命中自动触发（transpiler 接入）；模型/能力直接实现该接口即可被发现。
- **`AttackHitHook`**（static）— 分发器：
  - `Task BeforeAttackHit(AttackHitContext)` / `Task AfterAttackHit(AttackHitContext)`
  - `Task<IEnumerable<DamageResult>> DamageWithAttackHitHooks(...)`：带钩子的伤害执行（框架内部用）。
- **`AttackHitContext`**（sealed）— 单次命中可变上下文：
  - `CombatState`、`ChoiceContext`、`Attack`、`HitIndex`（0 基）、`HitNumber`（1 基）、`TotalHitCount`
  - `decimal Damage { get; set; }`、`ValueProp DamageProps { get; set; }`、`Dealer`、`CardSource`、`CardPlay`
  - `IReadOnlyList<Creature> Targets { get; set; }`、`Creature? SingleTarget`、`IReadOnlyList<DamageResult> Results`（后置钩子前为空）

### 1.2 治疗 Healing（`STS2RitsuLib.Combat.Healing`）

- **`IHealHookListener`**（interface）— 三段修正流水线（加法 → 乘法 → 晚期），均有默认值：
  - `decimal ModifyHealAdditive(HealContext, decimal amount)`（默认 0）
  - `decimal ModifyHealMultiplicative(HealContext, decimal amount)`（默认 1）
  - `decimal ModifyHealAmount(HealContext, decimal amount)`（默认原值）
- **`HealHook`**（static）：`RegisterGlobalListener(IHealHookListener)`；`decimal ModifyAmount(HealContext, decimal)`（结果钳制 ≥0）。
  由 `CreatureCmd.Heal` 路径自动触发。
- **`HealContext`**（sealed）：`Creature`、`OriginalAmount`、`PlayAnim`、`CombatState?`、`RunState?`、`MissingHp`。

### 1.3 手牌上限 HandSize（`STS2RitsuLib.Combat.HandSize`）

- **`IMaxHandSizeModifier`**（interface）：
  - `int ModifyMaxHandSize(Player, int current)`（早期阶段）
  - `int ModifyMaxHandSizeLate(Player, int current)`（后期阶段）
- **`MaxHandSizeCalculator`**（static）：`int Calculate(Player)`（BaseLib 值可用时以其为基，否则默认 10）；`int ApplyHookListenerModifiers(Player, int)`。结果钳制 ≥0。

### 1.4 玩家资源 PlayerResources（`STS2RitsuLib.Combat.PlayerResources`）

- **`PlayerResourceKind`**（enum）：`Energy`、`Stars`。
- **`IPlayerResourceHookListener`**（interface，均有空默认实现）：
  - `Task AfterPlayerEnergyGained(PlayerResourceGainContext)`
  - `Task AfterPlayerStarsGained(PlayerResourceGainContext)`
- **`PlayerResourceHook`**（static）：`RegisterGlobalListener(...)`、`AfterEnergyGained(ctx)`、`AfterStarsGained(ctx)`。
- **`PlayerResourceGainContext`**（readonly record struct）：`(CombatState, Player, Resource, Amount, OldAmount, NewAmount)`。

### 1.5 自定义目标类型 CardTargeting（`STS2RitsuLib.Combat.CardTargeting`）

- **`CustomTargetType`**（static）— 内置预注册类型 + 注册 API：
  - 预置 `TargetType`：`Everyone`、`Anyone`、`AllAttackingEnemies`、`AnyAttackingEnemy`、`AllBlockingEnemies`、`AnyBlockingEnemy`、`AllNonBlockingEnemies`、`AnyNonBlockingEnemy`、`AllHighestHpEnemies`、`AllLowestHpEnemies`、`AnyFullLifeEnemy`、`AllFullLifeEnemies`。
  - `TargetType RegisterSingleTargetType(string modId, string localStem, Func<Creature,bool> | Func<Creature,Player,bool> canTarget)`
  - `TargetType RegisterSingleTargetTypeWithContext(string modId, string localStem, Func<CustomTargetContext,bool>)`
  - `TargetType RegisterMultiTargetType(string modId, string localStem, Func<Creature,bool> | Func<Creature,Player,bool> includeTarget)`
  - 查询：`IsRitsuCustom(TargetType)`、`IsCustomSingleTargetType`、`IsCustomMultiTargetType`。
  - 也可经 `RitsuLibFramework.RegisterSingleTargetType(...)` 等便利入口调用。
- **`CustomTargetContext`**（sealed）：`TargetCreature`、`Player`、`Card?`、`Potion?`（来源卡牌或药水）。
- **`CardModelTargetingExtensions`**（static）：`List<Creature> GetTargets(this CardModel, Creature? selectedTarget = null)` —— 按当前 `TargetType` 解析目标（含自定义单/群体类型；`RandomEnemy` 会推进战斗目标 RNG）。`PotionModelTargetingExtensions` 同构提供 `GetTargets(this PotionModel, ...)`。
- 语义：**群体自定义目标**下卡牌/药水以"无选中目标"执行一次，受影响的生物用 `GetTargets` 解析；单体类型在无选中目标时解析为空。`CustomTargetTypeRegistry`/`CustomTargetTypeResolver`/`CustomTargetTypeSelectionContext` 均为 internal。

### 1.6 生命条预测 HealthBars（`STS2RitsuLib.Combat.HealthBars`）

- **`HealthBarForecastContext`**（readonly record struct）：`Creature`、`CombatState?`、`CurrentSide?`。
- **`IHealthBarForecastSource`**（interface）：`IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(HealthBarForecastContext)`。
  实现该接口的 **Power 模型自动从 `Creature.Powers` 发现**；其他来源走注册表。
- **`HealthBarForecastSegment`**（readonly record struct）：`(int Amount, Color Color, HealthBarForecastGrowthDirection Direction, int Order, Material? OverlayMaterial, Color? OverlaySelfModulate=null, HealthBarForecastLeftOriginLayout LeftOriginLayout=Chained, int LeftExclusiveZGroup=0, bool AffectsHpLabel=true)`，另有多组便捷构造。
- **`HealthBarForecastGrowthDirection`**（enum）：`FromRight`（中毒式，从当前 HP 边缘向内）、`FromLeft`（灾厄式，从空端向内）。
- **`HealthBarForecastLeftOriginLayout`**（enum）：`Chained`（首尾相接）、`OverlapFromOrigin`（同起点重叠，长段在下）。
- **`HealthBarForecastOrder`**（static）：`int ForSideTurnStart(Creature, CombatSide)`、`int ForSideTurnEnd(Creature, CombatSide)` —— 按"是否当前行动方"给出 0/1 排序键。
- **`HealthBarForecastRegistry`**（static）：
  - `void Register<TSource>(string modId, string? sourceId = null) where TSource : IHealthBarForecastSource, new()`
  - `void Register(string modId, string sourceId, IHealthBarForecastSource source)`、`bool Unregister(modId, sourceId)`
- 视觉扩展（溢出条）：**`HealthBarVisualGraftContext`**（record struct `Creature`）、**`HealthBarVisualGraftMetrics`**（`(int GraftHp, Color? GraftSelfModulate, Material? GraftMaterial)`）、**`IHealthBarVisualGraftSource`**（`GetHealthBarVisualGraft(ctx)`）、**`HealthBarVisualGraftRegistry`**（`Register<TSource>` / `Register` / `Unregister`）。同样可从 Power 自动发现。

### 1.7 临时能力模板 Powers（`STS2RitsuLib.Combat.Powers`）

- **`ModTemporaryPowerTemplate`**（abstract class，`: ModPowerTemplate, ITemporaryPower`）— 临时 buff 包装：应用一个内部能力，回合到期后移除。
  - 必须实现：`AbstractModel OriginModel { get; }`、`PowerModel InternallyAppliedPower { get; }`。
  - 可覆写：`bool IsPositive`（false 则反转数值呈现为减益）、`bool UntilEndOfOtherSideTurn`、`int LastForXExtraTurns`（>0 时每次应用创建独立实例）、`IEnumerable<DynamicVar> AdditionalCanonicalVars`。
  - 内置：`Type = Buff/Debuff`、`StackType = Counter`、`AllowNegative = true`；`int RemainingExtraTurnCycles`；`void IgnoreNextInstance()`；本地化动态变量 `{ExtraTurns}`（常量 `ExtraTurnCyclesVarName = "ExtraTurns"`）。
  - 便捷子类 **`ModTemporaryAppliedPowerTemplate<TOriginModel, TPower>`**：自动实现 `OriginModel`/`InternallyAppliedPower`（经 ModelDb 解析）。

### 1.8 自定义奖励 Rewards（`STS2RitsuLib.Combat.Rewards`）

- **`IModSerializableReward`**（interface）：`RewardType ModRewardType { get; }`、`string? ToModRewardJson()`（奖励类型足以恢复时返回 null）。
- **`ModCustomReward(Player player)`**（abstract class，`: Reward, IModSerializableReward`）— 自定义奖励基类：
  - 必须覆写 `ModRewardType`；可覆写 `DescriptionLocTable`（默认 `gameplay_ui`）、`DescriptionLocKey`、`string? RewardIconPath`。
  - 已实现：`CreateIcon()`（按图标路径加载 TextureRect）、存档 JSON 生成。
  - 注意：奖励副作用须在各客户端确定性执行或自行同步（原版只同步"选中的奖励"）。
- **`ModRewardRegistry`**（sealed）— 注册器：
  - `static ModRewardRegistry For(string modId)`（单例）。
  - `ModRewardDefinition RegisterOwned(string localRewardStem, ModRewardFactory factory)`（ID 遵循 `MODID_REWARD_LOCAL` 约定）。
  - `ModRewardDefinition RegisterOwned<TPayload>(string localRewardStem, JsonTypeInfo<TPayload>, ModRewardFactory<TPayload>)`（源生成 JSON 载荷反序列化）。
  - `static Register(string id, ModRewardFactory)` / `Register<TPayload>(...)` / `Register(RewardType, ModRewardFactory)`（覆写既有类型工厂）。
  - 查询：`TryGet/Get(id)`、`TryGetByRewardType/Get(RewardType)`、`GetRewardType(id)`、`TryGetId(RewardType, out id)`、`TryGetOwnerModId(id, out modId)`、`GetDefinitionsSnapshot()`。
  - 委托：`Reward ModRewardFactory(SerializableReward save, Player player, string? json)`；`ModRewardFactory<TPayload>` 同构。
- **`ModRewardDefinition`**（sealed record）：`(string ModId, string Id, RewardType RewardType)`。
- **`LinkedRewardSets`**（static）— 原版关联奖励（LinkedRewardSet）配置：
  - `LinkedRewardSet Create(IEnumerable<Reward> rewards, Player player, LinkedRewardSelectionMode mode = ChooseOne)`（校验：不可嵌套、同玩家、不重复、未属其他集合）。
  - `Configure(LinkedRewardSet, mode)`。上限常量：`MaximumEncodedChildren = 128` 等。
- **`LinkedRewardSelectionMode`**（enum）：`ChooseOne`（只取选中的子奖励）、`TakeAll`（优先选中的子奖励，然后尝试领取全部）。
- **`ModRewardSerialization`**（static）：序列化辅助（含 JSON 载荷桥接）。

### 1.9 次级资源 SecondaryResources（`STS2RitsuLib.Combat.SecondaryResources`）— 完整子系统

- **`ModSecondaryResourceRegistry`**（sealed partial）— 注册器：
  - `static ModSecondaryResourceRegistry For(string modId)`（单例）；`static bool HasAny`。
  - `SecondaryResourceDefinition Register(string localId, SecondaryResourceDefinition definition)`（返回绑定后定义；重复注册返回首次定义，跨 mod 抢 ID 抛异常）。
  - `static TryGet/Get(string resourceId)`、`GetDefinitionsSnapshot()`、`static string GetResourceId(modId, localId)`。
  - `static HoverTip CreateHoverTip(string resourceId, int amount = 0, int? maxAmount = null)`。
  - 战斗 UI 可见性：`RegisterCombatUiAlwaysVisibleWhen(localId, SecondaryResourceCombatUiVisibilityPredicate, order=0)`、`AlwaysShowInCombatUiForCharacter<TCharacter>(localId, order=-1000)`、`AlwaysShowInCombatUi(localId, order=-1000)`。
- **`SecondaryResourceDefinition`**（sealed record）— 构造参数（全可选）：
  `(int defaultAmount=0, int? baseMaxAmount=null, int minAmount=0, int hardMaxAmount=999_999_999, SecondaryResourceTurnStartPolicy turnStartPolicy=None, SecondaryResourcePersistencePolicy persistencePolicy=None, string? locTable=null, string? titleKey=null, string? descriptionKey=null, string? smallIconPath=null, string? largeIconPath=null)`。
  注册后注入 `Id/ModId/LocalId`；`IsVisibleInCombatUi(Player)`、`IsVisibleOnCard(CardModel, SecondaryResourcePaymentLine?)`；默认本地化表 `static_hover_tips`，键 `{Id}.title` / `{Id}.description`。
- **`SecondaryResourceCmd`**（static）— 运行时操作（都吃 hook 修正与钳制）：
  - `int Get(Player, string resourceId)`、`int? GetMax(Player, resourceId)`
  - `Task<int> Gain(Player, resourceId, int amount, AbstractModel? source = null)`、`Lose(...)`、`Set(...)`
  - `Task<bool> Spend(Player, resourceId, int amount, CardModel? card = null, AbstractModel? source = null)`
  - `Task<int> Reset(...)`、`Task ApplyTurnStartPolicies(Player, AbstractModel? source = null)`
- **`ISecondaryResourceHookListener`**（interface）— 全部有默认实现，模型直接实现即可：
  `ModifySecondaryResourceGain`、`ModifyMaxSecondaryResource`、`ModifySecondaryResourceCost`、`ModifySecondaryResourceCostLate`、`ModifySecondaryResourceXValue`、`ShouldGainSecondaryResource`、`ShouldSpendSecondaryResource`、`ModifySecondaryResourceInsufficientPayment`、`ResolveSecondaryResourceShortfall`、`ShouldResetSecondaryResource`、`AfterSecondaryResourceChanged`、`AfterSecondaryResourceSpent`、`AfterSecondaryResourceShortfallPayment`、`AfterSecondaryResourceReset`。
- **`SecondaryResourceHook`**（static）：`RegisterGlobalListener(...)` 及上表各分发方法（ModifyGain/ModifyCost/ModifyXValue/ShouldGain/ShouldSpend/ShouldReset/ModifyInsufficientPayment/ResolveShortfall/AfterChanged/AfterSpent/AfterShortfallPayment/AfterReset）。
- 关键枚举：
  - `SecondaryResourcePersistencePolicy`：`None` / `Combat`（仅显式战斗快照）/ `Run`（跨战斗持久）。
  - `SecondaryResourceTurnStartPolicy`：`None` / `ResetToMax` / `AddMaxToCurrent` / `Clear`。
  - 另有 `SecondaryResourceChangeReason`、`SecondaryResourceInsufficientPaymentMode`、`SecondaryResourceUseKind`、`SecondaryResourceCardCostColor`、`SecondaryResourceCostDuration`。
- 卡牌费用扩展：`SecondaryResourceCardExtensions`（`CostSecondaryResource` 等，partial）、`SecondaryResourceCostSet`、`SecondaryResourcePaymentResolver`；文本/变量：`SecondaryResourceVars`（DynamicVar）、`SecondaryResourceText`；存档：`SecondaryResourcePersistence`、`SecondaryResourceStateStore`。计数器 UI 节点 `NSecondaryResourceCounter`（Control，可在战斗中显示资源角标）。

### 1.10 战斗 UI 角标 ExtraCornerAmountLabels（`STS2RitsuLib.Combat.Ui.ExtraCornerAmountLabels`）

- 三件套提供接口（对 intent / power 图标 / relic 图标同构，实现其一即自动生效）：
  - `IIntentExtraCornerAmountLabelsProvider`：`IReadOnlyList<ExtraIconAmountLabelSlot> GetIntentExtraCornerAmountLabelSlots()`（纯文本）。
  - `IIntentExtraCornerAmountLabelSpecsProvider`：`IReadOnlyList<ExtraIconAmountLabelSpec> GetIntentExtraCornerAmountLabelSpecs()`（纯文本+富文本；同时实现时优先于前者）。
  - `IIntentExtraCornerAmountLabelsChangeSource`：`event Action? IntentExtraCornerAmountLabelsInvalidated`（宿主正常刷新间隔内的主动失效通知，Godot 主线程）。
  - 同构接口：`IPowerExtraIconAmountLabelsProvider` / `IPowerExtraIconAmountLabelSpecsProvider` / `IPowerExtraIconAmountLabelsChangeSource`；`IRelicExtraIconAmountLabelsProvider` / `...SpecsProvider` / `...ChangeSource`。
- **`ExtraIconAmountLabelSpec`**（readonly record struct）：`(string Text, ExtraIconAmountLabelCorner Corner, Rect2 CustomRect, Color? FontColor, Color? FontOutlineColor, ExtraIconAmountLabelTextMode TextMode = Plain)`。
  工厂：`Plain(corner, text)`、`RichText(corner, text)`、`PlainCustom(text, Rect2 | 四边距)`、`RichTextCustom(...)`；含隐式转换自 slot。
- **`ExtraIconAmountLabelCorner`**（enum，内置角落）、**`ExtraIconAmountLabelTextMode`**（enum：`Plain`/`RichText`）、`ExtraIconAmountLabelSlot` / `ExtraIconRichTextLabelSlot`。
- 规则：每个内置角只取第一个条目；自定义条目可重叠、后绘制在上；空白文本/非法角落/非法边界被忽略。

---

## 2. Interactions（右键交互，`STS2RitsuLib.Interactions.RightClick`）

- **`IModRightClickableModel`**（interface）— 推荐方式：模型实现后自动获得**同步**右键（跨客户端、含手柄 cancel 输入）：
  - `bool CanHandleRightClickLocal(ModRightClickContext)`（默认 true；只查稳定的本地 UI 事实）
  - `bool CanExecuteRightClick(ModRightClickExecutionContext)`（默认 true；各端解析出同步模型后调用）
  - `Task OnRightClick(ModRightClickExecutionContext)`（必须实现；进入行动队列后执行）
  - 空标记接口：`IModRightClickableCard` / `IModRightClickableRelic` / `IModRightClickablePower` / `IModRightClickablePotion` / `IModRightClickableOrb`。
- **`ModRightClickRegistry`**（static）— 注册与分发：
  - `IDisposable Register<TModel>(string modId, string localStem, Func<ModRightClickContext,bool> canHandle, Func<ModRightClickExecutionContext,Task> execute, int priority = 0) where TModel : AbstractModel`
  - `IDisposable Register<TModel>(string modId, string localStem, Func<ModRightClickExecutionContext,Task> execute, int priority = 0, Func<ModRightClickContext,bool>? canHandleLocal = null, Func<ModRightClickExecutionContext,bool>? canExecute = null)`
  - `void Register(IModRightClickHandler handler)`（自定义本地处理器，优先级高者先）、`bool TryDispatch(ModRightClickContext)`
  - 同步机制：战斗内仅在 PlayPhase 可发起；需要 v2 协议的来源（Orb、战斗牌堆卡牌）会做 peer 能力协商；`canExecute` 在所有端执行（勿用于纯本地 UI 过滤）。
- **`IModRightClickHandler`**（interface）：`int Priority`（默认 0）、`bool TryHandle(ModRightClickContext)`。
- **`ModRightClickContext`**（readonly record struct）：`(Player Player, AbstractModel Model, ModRightClickTrigger Trigger)`。
- **`ModRightClickExecutionContext`**（readonly record struct）：`(Player, AbstractModel Model, ModRightClickTrigger Trigger, GameActionPlayerChoiceContext? PlayerChoiceContext, GameAction? Action)`。
- **`ModRightClickTrigger`**（readonly record struct）：`(bool IsController = false, string? Metadata = null)`；扩展构造 `(isController, metadata, ModRightClickSource source, PileType? expectedCardPile = null)`；属性 `Source`、`ExpectedCardPile`。
- **`ModRightClickSource`**（enum）：`Unknown` / `HandCard` / `CombatPileCard` / `Relic` / `Power` / `Potion` / `Orb`。
- **`ModRightClickModelKind`**（enum）：`Card` / `Relic` / `Power` / `Potion` / `Orb`。
- **`ModRightClickBindingId`**（readonly record struct）：`(string Id)`，`ToString()` 返回 Id。
- 模型能力（`STS2RitsuLib.Models.Capabilities` 下 `IModelRightClickCapability`，本目录之外）也会被自动纳入右键候选并按 `RightClickPriority` 排序；`RightClickRunMode.Exclusive` 可独占执行。

---

## 3. Ui

### 3.1 浮动通知 Toast（`STS2RitsuLib.Ui.Toast`）

- **`RitsuToastService`**（static）— 全局入口：
  - `void Show(RitsuToastRequest)`、`RitsuToastHandle ShowTracked(RitsuToastRequest)`
  - 快捷：`ShowInfo/ShowWarning/ShowError(string body, string? title = null, Action? onClick = null)` 及 `...Tracked`。
  - 管理：`IsAlive(handle)`、`Close(handle, bool immediate = false)`、`CloseAll(bool immediate = false)`、`Update(handle, request, resetDuration = true)`、`UpdateBody/UpdateText/UpdateTitle`、`ResetDuration(handle, double? durationSeconds = null)`。
  - 宿主未就绪时请求自动排队；全局设置可禁用。
- **`RitsuToastRequest`**（sealed record）：`(string Body, string? Title=null, Texture2D? Image=null, RitsuToastLevel Level=Info, double? DurationSeconds=null, Action? OnClick=null, RitsuToastAnimationPreset? AnimationOverride=null)`；`WithBody/WithText/WithTitle/WithDuration`。
- **`RitsuToastHandle`**（sealed）：`Guid Id`；`IsAlive()`、`Close(bool immediate=false)`、`Dismiss`、`Update/UpdateBody/UpdateText/UpdateTitle`。
- 枚举：`RitsuToastLevel`、`RitsuToastAnimationPreset`、`RitsuToastAnchor`。

### 3.2 富文本特效 RichTextEffects（`STS2RitsuLib.Ui.RichTextEffects`）

- **`ModRichTextEffectRegistry`**（static）— 注册自定义 `RichTextEffect`（自动安装到所有启用 BBCode 的 `MegaRichTextLabel`）：
  - `ModRichTextEffectRegistration RegisterOwned<TEffect>(string modId, string localTagStem) where TEffect : RichTextEffect, new()`
  - `RegisterOwned(modId, localTagStem, RichTextEffect)`；`Register<TEffect>(modId[, string bbcode])`（全局标签）；`Register(modId, string bbcode, RichTextEffect)`。
  - `string GetQualifiedBbcode(modId, localTagStem)`（例：`My Mod` + `Glitch` → `mymod_richtext_glitch`）；`TryGet`、`GetRegistrationsSnapshot()`。
  - `string Wrap(string bbcode, string text, params ModRichTextTagParameter[])`、`WrapOwned(modId, localTagStem, text, params)`、`Wrap(registration, text, params)`。
  - BBCode 标签名全局唯一：跨 mod 冲突抛异常；特效须暴露可写 `bbcode` 字段/属性。
- **`ModRichTextEffectRegistration`**（sealed record）：`(string ModId, string Bbcode, RichTextEffect Effect)`。
- **`ModRichTextTag`**（static）：`ModRichTextTagParameter Param(string name, object? value)`；`Wrap(...)`。`ModRichTextTagParameter` 为 `(string Name, object? Value)`（值用 invariant 格式；Color 输出 `#RRGGBB[AA]`；null 参数省略）。

### 3.3 浮动窗口 Windows（`STS2RitsuLib.Ui.Windows`）

- **`RitsuFloatingWindow`**（sealed partial，`PanelContainer`）— 带主题的窗口（标题栏/拖动/八方向缩放/内容替换/几何存取）：
  - `RitsuFloatingWindow()` / `RitsuFloatingWindow(RitsuFloatingWindowOptions)`；`Options`（仅可读）、`Configure(options)`（进入场景树前调用）。
  - `bool InteractionLocked`；`event EventHandler? Closed`、`GeometryChanged`。
  - `Control? SetContent(Control content)`（返回旧内容）、`Control? TakeContent()`、`RitsuFloatingWindowGeometry CaptureGeometry()`、`ApplyGeometry(RitsuFloatingWindowGeometry)`、`void Close()`。
  - 仅可在 Godot 主线程创建/修改。
- **`RitsuFloatingWindowOptions`**（sealed）：`Title`、`InitialSize`、`FitInitialSizeToContent`、`MinimumSize`、`MaximumSize`（0 = 视口尺寸）、`Movable`、`Resizable`、`Closable`、`StartCentered`、`ConstrainToViewport`。
- **`RitsuFloatingWindowGeometry`**（readonly record struct）：`(Vector2 Position, Vector2 Size)`。

### 3.4 目录浏览器 Catalog（`STS2RitsuLib.Ui.Catalog`）

- **`RitsuCatalogBrowser`**（sealed partial，`Control`）— 可复用的目录/选择 UI：
  - `RitsuCatalogBrowser()` / `(RitsuCatalogBrowserOptions)`；`Items`、`SelectedItem`、`event EventHandler<RitsuCatalogSelectionChangedEventArgs>? SelectionChanged`。
  - `SetItems(IReadOnlyList<RitsuCatalogItem>)`、`SetFilters(IReadOnlyList<RitsuCatalogFilter>)`、`bool SelectItem(string? itemId)`、`Refresh()`。
  - 常量：`MaximumItemCount = 16384`、`MaximumSearchTextLength = 512`。
- **`RitsuCatalogModels`**：枚举 `RitsuCatalogPresentation`、`RitsuCatalogDetailPresentation`、`RitsuCatalogItemActionTone`；sealed 类 `RitsuCatalogItem`、`RitsuCatalogFilterOption`、`RitsuCatalogFilter`、`RitsuCatalogBrowserOptions`、`RitsuCatalogSelectionChangedEventArgs`。

### 3.5 Shell 主题 Theme（`STS2RitsuLib.Ui.Shell.Theme`）

- **`RitsuShellThemeRuntime`**（static）— 主题运行时：
  - `string ActiveThemeId`、`RitsuShellTheme Current`、`event Action? ThemeChanged`。
  - `EnsureBaseline()`、`ApplyThemeId(string? themeId)`、`ReapplyActiveTheme(bool forceReloadCatalog)`。
  - `RegisterModTokens(string modId, JsonElement? defaults, Action<RitsuShellTheme>? onApply)`、`UnregisterModTokens(string modId)` —— 模组向 `scopes.mod:<modId>` 贡献 DTF 默认令牌并订阅快照发布。
- **`RitsuShellThemeCatalog`**（static）：`IReadOnlyList<string> RegisteredThemeIds`、`InvalidateCache()`、`EnsureLoaded()`（加载内嵌与磁盘 `.theme.json`，处理继承/作用域覆盖/令牌引用）。
- **`RitsuShellThemeModRegistration`**（sealed record）：`(string ModId, JsonElement? Defaults, Action<RitsuShellTheme>? OnApply)`。
- 其他：`RitsuShellTheme`、`RitsuShellThemeDocument`（`.theme.json` 文档）、`RitsuShellThemeBuilder/Merger/ValueCoerce`；令牌 records：`ColorTokens`、`TextTokens`、`SurfaceTokens`、`ComponentTokens`、`MetricTokens`、`FontTokens` 等（DTCG 设计令牌）。
- `RitsuUiLayer`（internal）：CanvasLayer 层级常量（Toast=160 等，供参考）。`RitsuOverlayHostService` 无公共 API。

---

## 4. 特性（Attribute）清单

全部位于 `STS2RitsuLib.Interop.AutoRegistration`；标注目标均为 **class**，均可多次标注（`AllowMultiple = true`），默认 `Inherited = false`。注册在 RitsuLib 初始化扫描程序集时（`ModTypeDiscoveryHub.RegisterModAssembly` 之后）自动执行；`ModContentRegistry.IsFrozen` 后不可再注册。

### 4.1 基类

- **`AutoRegistrationAttribute`**（abstract）：`int Order`（同阶段局部排序，小者先）、`bool Inherit`（基类声明可被具体派生类继承；最近声明覆盖池/数量/路径等配置，不同 ID 的注册仍累加）。
- **`ContentRegistrationAttribute`**（abstract）：内容注册基类（经 `ModContentRegistry` 分发）。

### 4.2 内容注册（对应官方 `[Register*]` 家族）

| 特性 | 构造/参数 | 用途 |
|---|---|---|
| `[RegisterCard]` | `(Type poolType)`；可选 `StableEntryStem`、`FullPublicEntry` | 注册卡牌到指定卡牌池 |
| `[RegisterRelic]` | `(Type poolType)`；同上 | 注册遗物到指定遗物池 |
| `[RegisterPotion]` | `(Type poolType)`；同上 | 注册药水到指定药水池 |
| `[RegisterPower]` | 无参 | 注册能力模型 |
| `[RegisterCharacter]` / `[RegisterAct]` / `[RegisterMonster]` | 无参 | 角色 / 阶段 / 怪物模型 |
| `[RegisterOrb]` / `[RegisterEnchantment]` / `[RegisterAffliction]` / `[RegisterAchievement]` / `[RegisterSingleton]` | 无参 | 充能球 / 附魔 / 侵蚀 / 成就 / 单例模型 |
| `[RegisterModelCapability]` | 可选 `StableEntryStem`、`FullPublicEntry` | 注册模型能力（capability） |
| `[RegisterDefaultModelCapability]` | `(Type targetModelType)`；可选 `ModifierId` | 把标注能力类型加入某模型类型的默认能力集合 |
| `[RegisterGoodModifier]` / `[RegisterBadModifier]` | 可选 `ModifierListSortOrder` | 每日特效（正/负） |
| `[RegisterMutuallyExclusiveModifierGroup]` | `(params Type[] memberTypes)` | 特效互斥组 |
| `[RegisterSharedCardPool]` / `[RegisterSharedRelicPool]` / `[RegisterSharedPotionPool]` / `[RegisterSharedEvent]` / `[RegisterSharedAncient]` / `[RegisterGlobalEncounter]` | 无参 | 共享池 / 共享事件 / 全局遭遇 |
| `[RegisterTrashHeapCard]` / `[RegisterTrashHeapRelic]` | 无参 | "垃圾堆"事件候选 |
| `[RegisterCharacterStarterCard]` / `[RegisterCharacterStarterRelic]` / `[RegisterCharacterStarterPotion]` | `(Type characterType, int count = 1)` | 角色初始内容 |
| `[RegisterActEncounter]` / `[RegisterActEvent]` / `[RegisterActAncient]` | `(Type actType)` | 阶段遭遇 / 事件 / 先古事件 |

### 4.3 关键词 / 标签 / 文本

- `[RegisterOwnedKeyword(string localKeywordStem)]`：`TitleTable="card_keywords"`、`TitleKey?`、`DescriptionTable?`、`DescriptionKey?`、`IconPath?`、`CardDescriptionPlacement`、`IncludeInCardHoverTip=true`。
- `[RegisterOwnedCardKeyword(string localKeywordStem)]`：按游戏卡牌关键词本地化约定（`IconPath?`、`CardDescriptionPlacement`、`IncludeInCardHoverTip`）。
- `[RegisterOwnedCardTag(string localCardTagStem)]`：自定义 `CardTag` ID。
- `[RegisterSmartFormatter]` / `[RegisterSmartFormatSource]`：本地化 SmartFormat 格式化器 / 选择器源。

### 4.4 时间线 / 解锁（Timeline & Unlocks）

- `[RegisterEpoch]` / `[RegisterStory]` / `[RegisterStoryEpoch(Type storyType)]`。
- 时间线槽位：`[AutoTimelineSlot(EpochEra era)]`、`[AutoTimelineSlotBeforeColumn(EpochEra)]`、`[AutoTimelineSlotBeforeEpochColumn(Type)]`、`[AutoTimelineSlotAfterColumn(...)]`、`[AutoTimelineSlotAfterEpochColumn(Type)]`、`[AutoTimelineSlotInColumn(...)]`、`[AutoTimelineSlotInEpochColumn(Type)]`。
- 解锁门控：`[RequireEpoch(Type)]`、`[RequireAllCardsInPool(Type poolType)]`、`[RegisterEpochCards(params Type[])]`、`[RegisterEpochRelicsFromPool(Type poolType)]`。
- 解锁条件（角色 → 历史节点）：`[UnlockEpochAfterRunAs]`、`[UnlockEpochAfterWinAs]`、`[UnlockEpochAfterAscensionWin(Type, int ascensionLevel)]`、`[UnlockEpochAfterEliteVictories(Type, int requiredEliteWins=15)]`、`[UnlockEpochAfterBossVictories(Type, int requiredBossWins=15)]`、`[UnlockEpochAfterAscensionOneWin]`、`[RevealAscensionAfterEpoch]`、`[UnlockCharacterAfterRunAs]`。
- 特殊内容：`[RegisterArchaicToothTranscendence(Type ancientCardType)]`、`[RegisterDustyTomeCard(Type characterType)]`、`[RegisterTouchOfOrobasRefinement(Type upgradedRelicType)]`。

### 4.5 界面 / 节点 / 杂项

- `[RegisterOwnedTopBarButton(string localButtonStem)]`：标注类实现 `IModTopBarButtonHandler`；可选 `IconPath`、`ButtonOrder`、`OffsetX` 等；hover tip 键 `"{id}.title"/"{id}.description"`（`static_hover_tips`）。
- `[RegisterOwnedCardPile(string localPileStem)]`：标注类可实现 `IModCardPileHandler`（绑定 `OnOpen`）；`Scope=CombatOnly`、`Style=Headless`、`AnchorKind` 及偏移等；hover tip 键 `"{id}.title/.description/.empty"`。
- `[RegisterNodeAttachment(Type parentType, string localId)]`、`[RegisterNodeAttachmentFromScene(Type parentType, string localId, string scenePath)]`、`[RegisterNodeAttachmentFromConvertedScene(...)]`：父节点 `_Ready` 时挂载子节点；可选 `NodeType`、`NodeName`、`UniqueNameInOwner`、`IncludeDerivedParentTypes=true`、`DuplicatePolicy`、`AddMode`、`SetupTiming`、`ChildIndex`、`InsertBeforeName/InsertAfterName`、`QueueFreeReplacedNode=true`。
- `[RitsuLibOwnedBy(string modId)]`（类级，`Inherited=false`）：覆盖该类型上自动注册特性的归属 mod ID（用于共享库代码）。
- 模组设置（`STS2RitsuLib.Settings.ModSettings.Mirrors.RuntimeReflection`，运行时反射 provider）：`[ModSettingsBinding]`、`[ModSettingsPage(modId, pageId?)]`、`[ModSettingsSection(id)]`，条目特性 `[ModSettingsToggle]`、`[ModSettingsSlider]`、`[ModSettingsIntSlider]`、`[ModSettingsString]`、`[ModSettingsMultilineString]`、`[ModSettingsColor]`、`[ModSettingsKeyBinding]`、`[ModSettingsChoice]`、`[ModSettingsButton]`、`[ModSettingsParagraph]`、`[ModSettingsHeader]`、`[ModSettingsInfoCard]`、`[ModSettingsRuntimeHotkeySummary]`、`[ModSettingsImage]`、`[ModSettingsSubpage]`、`[ModSettingsCustomEntry]`（均为 `(string id, string sectionId, ...)`）。

---

## 5. 关键用法模式

### 5.1 Entry 初始化（官方 getting-started 模式）

```csharp
[ModInitializer(nameof(Initialize))]
public static class MyModEntry
{
    public const string ModId = "MyMod";
    public static Logger Logger { get; private set; } = null!;

    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();
        Logger = RitsuLibFramework.CreateLogger(ModId);
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);   // 关键：启用 CLR 特性自动注册
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger); // 仅含 .tscn 脚本时必需

        var patcher = RitsuLibFramework.CreatePatcher(ModId, "main");
        patcher.RegisterPatches<MyModPatches>();
        RitsuLibFramework.ApplyRequiredPatcher(patcher, DisableMod);
    }
}
```

### 5.2 注册流程（"For(modId) 单例 + RegisterOwned 限定 ID"）

- 通用入口：`RitsuLibFramework.BeginModDataRegistration(modId, ...)`（包裹注册）、`GetContentRegistry(modId)`、`GetSecondaryResourceRegistry(modId)`、`ModRewardRegistry.For(modId)`、`RitsuLibFramework.GetTopBarButtonRegistry(modId)` 等。
- 动态 ID 约定：`ModContentRegistry.GetCompoundId(modId, stem, name)` → 小写 `modid_stem_name`；`GetQualifiedRewardId` / `GetQualifiedTargetTypeId` / `GetQualifiedRightClickId` 等按类型封装。
- 注册器共性：重复注册同 ID 冲突保护（同 mod 重复注册返回首次定义；跨 mod 抢 ID 抛异常）；注册后冻结期不可再注册。
- 示例（次级资源）：`RitsuLibFramework.GetSecondaryResourceRegistry(ModId).Register("mana", new SecondaryResourceDefinition(defaultAmount: 0, baseMaxAmount: 10, turnStartPolicy: ResetToMax, persistencePolicy: Run, smallIconPath: "res://.../mana.png"))`，随后用 `SecondaryResourceCmd.Gain(player, id, 3)` 增/扣。

### 5.3 Hook 监听器（模型驱动 + 全局兜底）

- **推荐**：让模型（卡牌/能力/遗物等）直接实现对应接口（`IAttackHitHookListener`、`IHealHookListener`、`IPlayerResourceHookListener`、`ISecondaryResourceHookListener`、`IMaxHandSizeModifier`、`IHealthBarForecastSource`），框架经 `ModelHookListenerDispatcher` 从战斗状态 / 运行模型 / 模型能力自动发现，无需注册。
- 进程级兜底：`XxxHook.RegisterGlobalListener(listener)`（如 `HealHook`、`PlayerResourceHook`、`SecondaryResourceHook`）。
- 修改上下文只影响当前事件（如 `AttackHitContext.Damage` 只改本次命中）；多段修正顺序固定（如治疗：加法 → 乘法 → 晚期，再钳制 ≥0）。

### 5.4 生命周期订阅

```csharp
using var sub = RitsuLibFramework.SubscribeLifecycle<GameReadyEvent>(evt =>
{
    // 游戏就绪后初始化 UI 等
}, replayCurrentState: false); // 默认 true：若事件已发生则立即回放
```
- 亦可用 `SubscribeLifecycle(ILifecycleObserver, bool)` 观察者形式；返回 `IDisposable` 取消订阅。`RitsuShellThemeRuntime.ThemeChanged` 等静态事件按需订阅。

### 5.5 右键交互（同步）推荐姿势

- 简单场景：模型实现 `IModRightClickableModel`（`OnRightClick` 内发命令），框架自动完成本地预检、模型身份同步、行动队列执行。
- 绑定场景：`ModRightClickRegistry.Register<TModel>(ModId, "inspect", canHandle: ctx => ..., execute: async ctx => { ... }, priority: 0)`，保留返回的 `IDisposable` 以便注销。
- 注意：`canHandleLocal`/`CanHandleRightClickLocal` 只读稳定本地 UI 事实；可变游戏状态放 `canExecute`/`CanExecuteRightClick`/`OnRightClick`；战斗内只在 PlayPhase 生效。

### 5.6 一次性 UI 调用（无需注册）

- Toast：`RitsuToastService.ShowInfo("战斗胜利", "提示")`（或 `ShowTracked` 获取句柄后可更新/关闭）。
- 富文本：`ModRichTextEffectRegistry.RegisterOwned<GlitchEffect>(ModId, "glitch")` 后 `ModRichTextEffectRegistry.WrapOwned(ModId, "glitch", "文本", ModRichTextTag.Param("strength", 3))`。
- 窗口/目录：`RitsuFloatingWindow`（`Configure` 后 `SetContent`、`CaptureGeometry/ApplyGeometry`）；`RitsuCatalogBrowser`（`SetItems`/`SetFilters`/`SelectItem`）。

---

*说明：`Combat\CardTargeting\Patches`、各 `Patches\` 与 `Internal\` 目录为实现细节，未列入；`Ui\Overlay`（debug dock 等）无公开 API。*
