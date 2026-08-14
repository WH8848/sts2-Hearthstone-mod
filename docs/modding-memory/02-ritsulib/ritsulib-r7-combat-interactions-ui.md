# RitsuLib（STS2 官方 mod 库）— Combat / Interactions / Ui 公共 API 手册

> 来源：`E:\MOD\sts2\STS2-RitsuLib\src\` 下 `Combat`、`Interactions`、`Ui` 三个目录（r7 快照）。
> 命名空间一律为 `STS2RitsuLib.*`。仅收录对 mod 制作有实用价值的 public 类型/成员；
> 内部实现、`*Patches`（Harmony 补丁）、生成代码（`*.g.cs`、`*MethodName*` 等）已跳过，
> 仅在必要时一句话说明补丁提供了什么能力。
> 注册特性（Attribute）本体位于 `STS2RitsuLib.Interop.AutoRegistration`，一并收录于第 4 节。

---

## 1. Combat（战斗系统）

### 1.1 攻击命中钩子（`STS2RitsuLib.Combat.AttackHits`）

- **`AttackHitContext`** — 提供 `AttackCommand` 单次命中的可变上下文。关键成员: `CombatState`; `ChoiceContext`; `Attack`; `HitIndex`（从 0 起）; `int HitNumber => HitIndex + 1`; `TotalHitCount`; `Damage { get; set; }`; `DamageProps { get; set; }`; `Dealer { get; set; }`; `CardSource { get; set; }`; `CardPlay { get; set; }`; `Targets { get; set; }`（set 时校验非空）; `SingleTarget`（仅单目标时非 null）; `Results`（命中结算后填充 `DamageResult[]`，后置钩子前为空）
- **`AttackHitHook`** — 静态分发器，将单次命中钩子分发给战斗钩子监听器及附加模型能力。关键成员: `Task<IEnumerable<DamageResult>> DamageWithAttackHitHooks(...)`（供 `AttackCommand.Execute` 转换器调用）; `Task BeforeAttackHit(AttackHitContext)`; `Task AfterAttackHit(AttackHitContext)`
- **`IAttackHitHookListener`** — 单次命中可选钩子接口（全部默认空实现）。关键成员: `Task BeforeAttackHit(AttackHitContext)`; `Task AfterAttackHit(AttackHitContext)`
- **用法要点**: 无需手动注册——监听器由 `ModelHookListenerDispatcher.FromCombat` 自动收集（战斗模型/能力），并附加 `Attack.ModelSource` 与 `CardSource` 两个附加模型。流程：先 `BeforeAttackHit`（可修改上下文，仅影响本次命中），再执行 `CreatureCmd.Damage`，填充 `Results` 后跑 `AfterAttackHit`。

### 1.2 卡牌目标（`STS2RitsuLib.Combat.CardTargeting`）

- **`CustomTargetType`** — 定义 RitsuLib 目标类型常量并注册确定性模组自有 `TargetType`。关键成员: `static TargetType Everyone/Anyone/AllAttackingEnemies/AnyAttackingEnemy/AllBlockingEnemies/AnyBlockingEnemy/AllNonBlockingEnemies/AnyNonBlockingEnemy/AllHighestHpEnemies/AllLowestHpEnemies/AnyFullLifeEnemy/AllFullLifeEnemies { get; }`; `static bool IsRitsuCustom(TargetType)`; `static bool IsCustomSingleTargetType(TargetType)`; `static bool IsCustomMultiTargetType(TargetType)`; `static TargetType RegisterSingleTargetType(string modId, string localStem, Func<Creature,bool>)`; `static TargetType RegisterSingleTargetType(string modId, string localStem, Func<Creature,Player,bool>)`; `static TargetType RegisterSingleTargetTypeWithContext(string modId, string localStem, Func<CustomTargetContext,bool>)`; `static TargetType RegisterMultiTargetType(string modId, string localStem, Func<Creature,bool>)`; `static TargetType RegisterMultiTargetType(string modId, string localStem, Func<Creature,Player,bool>)`
- **`CustomTargetContext`** — 自定义目标谓词的候选/玩家/来源上下文。关键成员: `CustomTargetContext(Creature targetCreature, Player player, CardModel? card = null, PotionModel? potion = null)`; `TargetCreature`; `Player`; `Card`; `Potion`
- **`CardModelTargetingExtensions`**（扩展方法）— 按卡牌 `TargetType` 解析目标。关键成员: `static List<Creature> GetTargets(this CardModel card, Creature? selectedTarget = null)`（`RandomEnemy` 会推进战斗目标 RNG；`Self` 解析为所有者；自定义类型委托给解析器）
- **`PotionModelTargetingExtensions`**（扩展方法）— 按药水 `TargetType` 解析目标。关键成员: `static List<Creature> GetTargets(this PotionModel potion, Creature? selectedTarget = null)`
- **`AttackCommandTargetingExtensions`**（扩展方法）— 为攻击命令指定自定义目标快照。关键成员: `static AttackCommand TargetingFiltered(this AttackCommand command, IEnumerable<Creature> targets)`
- **用法要点**: 目标类型注册 = `DynamicEnumValueRegistry<TargetType>.RegisterOwned(modId, localStem)` 铸造确定性枚举值 + 谓词入库；内置类型由内部 `CustomTargetTypeRegistry.RegisterBuiltIns()` 注册。解析链同时查 RitsuLib 注册表与 BaseLib 桥。同类型不能同时注册为单体和群体（`InvalidOperationException`）。

### 1.3 手牌上限（`STS2RitsuLib.Combat.HandSize`）

- **`IMaxHandSizeModifier`** — 手牌上限可扩展修正器接口。关键成员: `int ModifyMaxHandSize(Player player, int currentMaxHandSize)`（早期阶段，默认原样返回）; `int ModifyMaxHandSizeLate(Player player, int currentMaxHandSize)`（后期阶段，默认原样返回）
- **`MaxHandSizeCalculator`** — 计算玩家实际手牌上限。关键成员: `static int Calculate(Player player)`; `static int ApplyHookListenerModifiers(Player player, int currentMaxHandSize)`（先跑一轮 `ModifyMaxHandSize` 再跑一轮 `ModifyMaxHandSizeLate`，结果钳制 ≥ 0）
- **用法要点**: 默认基础值 10；BaseLib 存在时以其计算结果为基础值。修正器从战斗钩子监听器自动收集（`FromCombat<IMaxHandSizeModifier>`），无需注册。内部 `MaxHandSizePatchInstaller` 用 Harmony IL 重写把原版硬编码 10 替换为 `Calculate`，并支持 >10 张手牌扇形布局。

### 1.4 治疗钩子（`STS2RitsuLib.Combat.Healing`）

- **`HealContext`** — 修正生物所受治疗量的上下文。关键成员: `Creature`; `OriginalAmount`; `PlayAnim`; `CombatState`（战斗外为 null）; `RunState`; `MissingHp => Max(0, MaxHp - CurrentHp)`
- **`IHealHookListener`** — 治疗量修正可选钩子接口（默认值实现）。关键成员: `decimal ModifyHealAdditive(HealContext, decimal amount)`（默认 0）; `decimal ModifyHealMultiplicative(HealContext, decimal amount)`（默认 1）; `decimal ModifyHealAmount(HealContext, decimal amount)`（默认原样）
- **`HealHook`** — 治疗量钩子分发器。关键成员: `static void RegisterGlobalListener(IHealHookListener listener)`; `static decimal ModifyAmount(HealContext context, decimal amount)`
- **用法要点**: 无需订阅——由 `CreatureCmd.Heal` 补丁自动触发；监听器 = 全局注册表 + 从 run/combat 自动发现的模型。修改顺序：全部 `ModifyHealAdditive` 求和 → 全部 `ModifyHealMultiplicative` 连乘 → 全部 `ModifyHealAmount` → 钳制 ≥ 0。全局监听仅用于非模型效果；模型持有效果应直接实现接口。

### 1.5 生命条预测与视觉扩展（`STS2RitsuLib.Combat.HealthBars`）

- **`HealthBarForecastContext`** — 预测上下文（readonly record struct）。关键成员: `Creature`; `CombatState`; `CurrentSide`（`CombatSide?`）
- **`IHealthBarForecastSource`** — 预测片段来源接口。关键成员: `IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(HealthBarForecastContext context)`（实现接口的能力模型会被 `Creature.Powers` 自动发现）
- **`HealthBarForecastSegment`** — 单个预测片段（readonly record struct）。关键成员: `(int Amount, Color Color, HealthBarForecastGrowthDirection Direction, int Order, Material? OverlayMaterial, Color? OverlaySelfModulate, HealthBarForecastLeftOriginLayout LeftOriginLayout, int LeftExclusiveZGroup, bool AffectsHpLabel)` 及多个便捷构造函数
- **`HealthBarForecastGrowthDirection`** — 枚举: `FromRight = 0`（从当前 HP 边缘向内，如中毒）、`FromLeft = 1`（从空白边缘向内，如灾厄）
- **`HealthBarForecastLeftOriginLayout`** — 枚举: `Chained = 0`（首尾相接）、`OverlapFromOrigin = 1`（各自从空白边缘起算叠放）
- **`HealthBarForecastOrder`** — 排序键工具。关键成员: `static int ForSideTurnStart(Creature, CombatSide)`; `static int ForSideTurnEnd(Creature, CombatSide)`
- **`HealthBarForecastRegistry`** — 全局预测来源注册表。关键成员: `static void Register<TSource>(string modId, string? sourceId = null) where TSource : IHealthBarForecastSource, new()`; `static void Register(string modId, string sourceId, IHealthBarForecastSource source)`; `static bool Unregister(string modId, string sourceId)`
- **`HealthBarForecasts`** — 片段构建便捷工厂。关键成员: `static HealthBarForecastSequenceBuilder For(HealthBarForecastContext)`; `static HealthBarForecastLaneBuilder FromRight/FromLeft(...)`; `static IEnumerable<HealthBarForecastSegment> Single(...)`（amount ≤ 0 返回空）
- **`HealthBarForecastSequenceBuilder`** — 单来源片段序列构建器。关键成员: `Context`; `Add(...)`（相邻兼容片段自动合并、数值封顶 `int.MaxValue`）; `AddRange(...)`; `AddSideTurnStart(CombatSide, Color, HealthBarForecastGrowthDirection, params int[])`; `AddSideTurnEnd(...)`; `FromRight/FromLeft(...)`; `IReadOnlyList<HealthBarForecastSegment> Build()`
- **`HealthBarForecastLaneBuilder`** — 固定颜色轨道构建器。关键成员: `Sequence`; `Add(int amount, int order = 0, Material? overlayMaterial = null)`; `AddRange(...)`; `AtSideTurnStart(CombatSide, params int[])`; `AtSideTurnEnd(...)`; `ThenFromRight/ThenFromLeft(Color)`; `Build()`
- **`HealthBarVisualGraftContext`** — 视觉扩展上下文（readonly record struct）。关键成员: `Creature`
- **`HealthBarVisualGraftMetrics`** — 视觉扩展参数（readonly record struct）。关键成员: `(int GraftHp, Color? GraftSelfModulate, Material? GraftMaterial)`
- **`IHealthBarVisualGraftSource`** — 视觉扩展来源接口。关键成员: `HealthBarVisualGraftMetrics GetHealthBarVisualGraft(HealthBarVisualGraftContext context)`
- **`HealthBarVisualGraftRegistry`** — 视觉扩展来源注册与汇总。关键成员: `static void Register<TSource>(string modId, string? sourceId = null)`; `static void Register(string modId, string sourceId, IHealthBarVisualGraftSource source)`; `static bool Unregister(string modId, string sourceId)`
- **用法要点**: 两种来源自动发现——实现接口的 `Creature.Powers` 能力 + 全局注册来源（键为 (modId, sourceId)，忽略大小写，同键替换）。收集时先能力后注册来源；只保留 `Amount > 0` 的片段，来源异常仅记日志不中断。BaseLib 存在时通过内部桥接入其渲染协议。

### 1.6 玩家资源钩子（`STS2RitsuLib.Combat.PlayerResources`）

- **`PlayerResourceKind`** — 支持的内置玩家战斗资源枚举: `Energy`、`Stars`
- **`PlayerResourceGainContext`** — 资源成功获得时的上下文（readonly record struct）。关键成员: `(CombatState, Player, Resource, int Amount, int OldAmount, int NewAmount)`
- **`IPlayerResourceHookListener`** — 内置玩家资源钩子接口（默认空实现）。关键成员: `Task AfterPlayerEnergyGained(PlayerResourceGainContext)`; `Task AfterPlayerStarsGained(PlayerResourceGainContext)`
- **`PlayerResourceHook`** — 资源钩子分发器。关键成员: `static void RegisterGlobalListener(IPlayerResourceHookListener listener)`; `static Task AfterEnergyGained(PlayerResourceGainContext)`; `static Task AfterStarsGained(PlayerResourceGainContext)`
- **用法要点**: 无需订阅——由 `PlayerCmd.GainEnergy` / `PlayerCmd.GainStars` 补丁自动调用，仅在 `NewAmount > OldAmount` 时构造上下文并分发；监听器 = 全局注册表 + 战斗模型自动发现。模型持有效果应直接实现接口。

### 1.7 临时能力模板（`STS2RitsuLib.Combat.Powers`）

- **`ModTemporaryPowerTemplate`** — 抽象临时能力包装：应用一个内部能力，到期时移除等量数值。关键成员: `public const string ExtraTurnCyclesVarName = "ExtraTurns"`; `protected virtual bool IsPositive => true`（false 时反转符号并显示为减益）; `protected virtual bool UntilEndOfOtherSideTurn => false`; `protected virtual int LastForXExtraTurns => 0`（>0 时每次应用独立实例，负数非法）; `protected virtual IEnumerable<DynamicVar> AdditionalCanonicalVars => []`（不得占用 `ExtraTurns`）; `Type => IsPositive ? Buff : Debuff`; `StackType => Counter`; `AllowNegative => true`; `InstanceType/IsInstanced => LastForXExtraTurns > 0`; `public int RemainingExtraTurnCycles { get; set; }`（负数钳 0）; `public abstract AbstractModel OriginModel`; `public abstract PowerModel InternallyAppliedPower`; `public void IgnoreNextInstance()`; `protected virtual decimal SignedAmount(decimal)`; 重写 `BeforeApplied` / `AfterPowerAmountChanged` / `AfterSideTurnEnd`（≥0.106 用 participants 判断，避免额外回合误到期）
- **`ModTemporaryAppliedPowerTemplate<TOriginModel, TPower>`** — 绑定具体来源模型与内部能力类型的便利抽象。关键成员: `OriginModel => ModelDb.GetById<AbstractModel>(ModelDb.GetId<TOriginModel>())`; `InternallyAppliedPower => ModelDb.Power<TPower>()`
- **用法要点**: 子类需提供 `OriginModel`（解析标题与来源悬停提示）与 `InternallyAppliedPower`（`CanonicalVars` 始终含 `ExtraTurns` 动态变量）。应用流程：首次应用把 `RemainingExtraTurnCycles` 置为 `LastForXExtraTurns`，并以 `SignedAmount(amount)` 对内部能力执行 `PowerCmd.Apply`；数量变化时镜像同步内部能力（`IgnoreNextInstance` 可抑制下一次）；到期回合结束时 `Flash()`、`PowerCmd.Remove(this)` 并以 `-SignedAmount(Amount)` 撤销内部能力。

### 1.8 战斗奖励（`STS2RitsuLib.Combat.Rewards`）

- **`IModSerializableReward`** — 自定义奖励随战斗房间存档/恢复所需的持久化接口。关键成员: `RewardType ModRewardType { get; }`; `string? ToModRewardJson()`
- **`LinkedRewardSelectionMode`** — 关联奖励集合的子奖励被选中后的结算枚举。取值: `ChooseOne`（只领取选中项并跳过其余）; `TakeAll`（从选中项起尝试全部领取）
- **`LinkedRewardSets`** — 静态工厂，创建/配置原版 `LinkedRewardSet`。关键成员: `LinkedRewardSet Create(IEnumerable<Reward> rewards, Player player, LinkedRewardSelectionMode mode = ChooseOne)`; `LinkedRewardSet Configure(LinkedRewardSet set, LinkedRewardSelectionMode mode)`; `LinkedRewardSelectionMode GetSelectionMode(LinkedRewardSet set)`
- **`ModRewardDefinition`** — 已注册自定义奖励类型的描述（sealed record）。成员: `ModRewardDefinition(string ModId, string Id, RewardType RewardType)`
- **`ModCustomReward`** — 自定义奖励的抽象基类（`: Reward, IModSerializableReward`），内置本地化、可选图标加载与存档创建。关键成员: `abstract RewardType ModRewardType { get; }`; `virtual string? ToModRewardJson()`; `override SerializableReward ToSerializable()`; `override Control? CreateIcon()`; `override LocString Description`; 受保护成员: `virtual string DescriptionLocTable => "gameplay_ui"`; `virtual string DescriptionLocKey`（默认取注册 ID）; `virtual string? RewardIconPath => null`
- **`ModRewardRegistry`** — 模组自定义奖励注册表（sealed，按 modId 单例）。关键成员: `delegate Reward ModRewardFactory(SerializableReward save, Player player, string? json)`; `static ModRewardRegistry For(string modId)`; `ModRewardDefinition RegisterOwned(string localRewardStem, ModRewardFactory factory)`; `ModRewardDefinition RegisterOwned<TPayload>(string localRewardStem, JsonTypeInfo<TPayload> jsonTypeInfo, ModRewardFactory<TPayload> factory)`; `static ModRewardDefinition Register(string id, ModRewardFactory factory)`; `static void Register(RewardType rewardType, ModRewardFactory factory)`; `static bool TryGet(string id, out ModRewardDefinition definition)`; `static RewardType GetRewardType(string id)`; `static ModRewardDefinition[] GetDefinitionsSnapshot()`
- **`ModRewardSerialization`** — 静态工具。关键成员: `static SerializableReward CreateSerializable(IModSerializableReward reward)`; `static SerializableReward CreateSerializable(RewardType rewardType, string? json = null)`; `static SerializableReward CreateSerializable<TPayload>(RewardType rewardType, TPayload payload, JsonTypeInfo<TPayload> jsonTypeInfo)`
- **用法要点**: 注册：`ModRewardRegistry.For(modId).RegisterOwned(stem, factory)` → 用 `ModContentRegistry.GetQualifiedRewardId` 生成限定 ID（`MODID_REWARD_LOCAL` 约定）；或 `Register(id, factory)` 用原始全局 ID；`Register(rewardType, factory)` 覆盖既有/原版奖励类型。序列化：自定义奖励派生 `ModCustomReward` 并实现 `ModRewardType`；有载荷时用源生成 `JsonTypeInfo<TPayload>` 强类型载荷存 `custom_reward_json`。链接奖励用 `LinkedRewardSets.Create` 构建（校验 1–128 子项、不可嵌套、同玩家、不重复）。

### 1.9 次级资源系统（`STS2RitsuLib.Combat.SecondaryResources`）

- **`ISecondaryResourceHookListener`** — 参与次级资源计算与状态变化的钩子接口，全部成员带默认实现（透传/放行），mod 可只实现需要的。关键成员: `decimal ModifySecondaryResourceGain(SecondaryResourceContext, decimal)`; `decimal ModifyMaxSecondaryResource(SecondaryResourceMaxContext, decimal)`; `decimal ModifySecondaryResourceCost(SecondaryResourceCostContext, decimal)`; `decimal ModifySecondaryResourceCostLate(SecondaryResourceCostContext, decimal)`; `int ModifySecondaryResourceXValue(SecondaryResourceXContext, int)`; `bool ShouldGainSecondaryResource(SecondaryResourceContext, decimal)`; `bool ShouldSpendSecondaryResource(SecondaryResourceSpendContext)`; `SecondaryResourceInsufficientPayment ModifySecondaryResourceInsufficientPayment(SecondaryResourceInsufficientPaymentContext, SecondaryResourceInsufficientPayment)`; `SecondaryResourceShortfallResolution ResolveSecondaryResourceShortfall(SecondaryResourceShortfallResolutionContext, SecondaryResourceShortfallResolution)`; `bool ShouldResetSecondaryResource(SecondaryResourceContext)`; `Task AfterSecondaryResourceChanged(SecondaryResourceChangeContext)`; `Task AfterSecondaryResourceSpent(SecondaryResourceSpendContext)`; `Task AfterSecondaryResourceShortfallPayment(SecondaryResourceShortfallContext)`; `Task AfterSecondaryResourceReset(SecondaryResourceChangeContext)`
- **`ModSecondaryResourceRegistry`**（sealed partial）— 每个 mod 一份的注册表，负责资源定义注册、查询、悬停提示与 UI 可见性/挂载注册。关键成员: `static ModSecondaryResourceRegistry For(string modId)`; `static string GetResourceId(string modId, string localId)`; `SecondaryResourceDefinition Register(string localId, SecondaryResourceDefinition definition)`; `static bool TryGet(string resourceId, out SecondaryResourceDefinition)`; `static SecondaryResourceDefinition Get(string resourceId)`; `static SecondaryResourceDefinition[] GetDefinitionsSnapshot()`; `static HoverTip CreateHoverTip(string resourceId, int amount = 0, int? maxAmount = null)`; `void RegisterCombatUiAlwaysVisibleWhen(string localId, SecondaryResourceCombatUiVisibilityPredicate predicate, int order = 0)`; `void AlwaysShowInCombatUiForCharacter<TCharacter>(string localId, int order = -1000)`; `void AlwaysShowInCombatUiForCharacter(string localId, Type characterType, int order = -1000)`; `void AlwaysShowInCombatUi(string localId, int order = -1000)`; 另在分部 `SecondaryResourceUi.cs`: `NodeAttachmentDefinition RegisterCombatUi<TParent,TNode>(string localId, Func<TParent,TNode> factory, Action<SecondaryResourceCombatUiContext<TParent,TNode>> update, [changed,] NodeAttachmentOptions? options = null)`; `RegisterCardUi<TParent,TNode>(...)`; `RegisterMultiplayerPlayerStateUi<TNode>(...)`
- **`ICardSecondaryResourceUseContributor`** — 卡牌能力（capability）向出牌计划贡献额外支付条款。关键成员: `IEnumerable<SecondaryResourcePlayUse> GetSecondaryResourceUses(CardModel card)`
- **`ICardSecondaryResourceCostContributor`** — 卡牌能力在战斗级费用钩子之前修正本地固定费用。关键成员: `decimal ModifySecondaryResourceCost(SecondaryResourceCardCostContext context, decimal cost)`
- **`SecondaryResourceCardCostColor`**（enum）— 卡牌次级费用显示颜色状态: `Unmodified` / `Increased` / `Decreased` / `InsufficientResources` / `ShortfallPlayable` / `OptionalUnavailable`
- **`SecondaryResourceCardCostHelper`**（static）— 按游戏费用配色规则解析支付条目的颜色状态。关键成员: `static SecondaryResourceCardCostColor GetCostColor(SecondaryResourcePaymentLine line, PileType pileType, CardPreviewMode previewMode, bool pretendCardCanBePlayed = false, bool includeOptionalUnavailable = true)`
- **`SecondaryResourceCardCostUiStyle`**（sealed record）— 卡牌费用槽外观配置。关键成员: `static SecondaryResourceCardCostUiStyle Default`; `bool ReserveVanillaStarCostSlot`
- **`NSecondaryResourceCardCostUi`**（partial Control）— 可复用的卡牌次级费用显示节点（图标+数量+配色），可绑定资源或具体支付条款并自动订阅所有者状态。关键成员: `bool AutoRefresh`; `static NSecondaryResourceCardCostUi Create(string resourceId, SecondaryResourceCardCostUiStyle? style = null)`; `static NSecondaryResourceCardCostUi CreateForUse(string useId, string resourceId, SecondaryResourceCardCostUiStyle? style = null)`; `void Configure(...)`; `void Bind(string resourceId)` / `void BindUse(string useId, string resourceId)`; 多个 `Refresh(...)` 重载
- **`SecondaryResourceCardUiLayout`**（static）— 协调费用显示与 `NCard` 布局（预留辉星费用槽偏移）。关键成员: `static void ReserveVanillaStarCostSlot(NCard card)`
- **`SecondaryResourceCmd`**（static）— mod 修改资源数量的命令入口（唯一官方变更通道），自动应用检查/修正/钳制并触发历史与钩子。关键成员: `static int Get(Player player, string resourceId)`; `static int? GetMax(Player player, string resourceId)`; `static Task<int> Gain(Player player, string resourceId, int amount, AbstractModel? source = null)`; `static Task<int> Lose(...)`; `static Task<int> Set(...)`; `static Task<bool> Spend(Player player, string resourceId, int amount, CardModel? card = null, AbstractModel? source = null)`; `static Task<int> Reset(Player player, string resourceId, bool toMax = false, AbstractModel? source = null)`; `static Task ApplyTurnStartPolicies(Player player, AbstractModel? source = null)`
- **`SecondaryResourceConsoleCmd`**（AbstractConsoleCmd）— 开发者控制台命令 `sresource`（get/gain/lose/set/reset/resetmax/list）。关键成员: `override string CmdName => "sresource"`
- **上下文记录**（均 readonly record struct）: `SecondaryResourceContext (CombatState, Player, Definition, AbstractModel? Source)`; `SecondaryResourceMaxContext (CombatState, Player, Definition)`; `SecondaryResourceChangeContext (..., int OldAmount, int NewAmount, int Delta, SecondaryResourceChangeReason Reason, Source)`; `SecondaryResourceSpendContext (..., CardModel? Card, int Amount, Source)`; `SecondaryResourceInsufficientPaymentContext (..., string UseId, SecondaryResourceUseKind Kind, int Cost, int AmountAvailable, int AmountToSpend, int Shortfall, Source)`; `SecondaryResourceShortfallResolutionContext`（同 InsufficientPaymentContext，无副作用规划用）; `SecondaryResourceShortfallContext (..., int AmountSpent, int OriginalShortfall, int CoveredShortfall, int Shortfall, Source, Ledger)`; `SecondaryResourceCostContext (CombatState, Player, Card, Definition, decimal OriginalCost)`; `SecondaryResourceCardCostContext (Card, Definition, Use, decimal OriginalCost)`; `SecondaryResourceXContext (CombatState, Player, Card, Definition, int OriginalValue)`
- **`SecondaryResourceCostDuration`**（enum）— 附加费用层有效期: `Permanent` / `UntilPlayed` / `ThisTurn` / `ThisCombat`
- **`SecondaryResourceCost`**（sealed record）— 固定或 X 费用描述: `(int Amount, bool CostsX = false, int XMultiplier = 1)`; `static SecondaryResourceCost Free`; `static SecondaryResourceCost X(int multiplier = 1)`
- **`SecondaryResourceCostSet`**（sealed class）— 一张卡牌上分层存储的附加费用。关键成员: `bool HasCosts`; `IReadOnlyList<string> ResourceIds`; `event Action? Changed`; `Set(string resourceId, int amount)`; `Set(string, SecondaryResourceCost[, SecondaryResourceCostDuration[, SecondaryResourceInsufficientPayment?]])`; `SetAllowingShortfall(string resourceId, int amount, SecondaryResourceShortfallPaymentHandler? onShortfall = null, bool spendAvailable = true, SecondaryResourceShortfallResolver? resolveShortfall = null)`; `bool Clear(string resourceId)`; `bool ClearDuration(SecondaryResourceCostDuration)`; `SecondaryResourceCost Get(string resourceId)`; `IReadOnlyDictionary<string, SecondaryResourceCost> Snapshot()`
- **`SecondaryResourceCardExtensions`**（static partial 扩展方法）— 卡牌附加费用/支付条款访问入口。关键成员: `SecondaryResourceCostSet SecondaryCosts(this CardModel card)`; `bool TryGetSecondaryCosts(this CardModel, out SecondaryResourceCostSet)`; `bool ClearSecondaryCostsUntilPlayed(this CardModel)`; `bool ClearSecondaryCostsThisTurn(this CardModel)`; `SecondaryResourcePlayUseSet SecondaryResourceUses(this CardModel)`; `bool TryGetSecondaryResourceUses(this CardModel, out SecondaryResourcePlayUseSet)`
- **`SecondaryResourcePaymentLine`**（sealed record）— 出牌计划中一条已解析支付条目。关键成员: `bool IsAffordable`; `bool CanPlay`; `bool CanSpend`; `bool IsShortfallPlayable`; `bool IsShortfallCovered`; `bool IsOptional`; `bool IsExtraSpend`; `int Shortfall` / `OriginalShortfall` / `CoveredShortfall` / `ExtraStacks` / `ExtraAmountToSpend` / `BaseCost`; `SecondaryResourceInsufficientPayment InsufficientPayment`; `SecondaryResourceShortfallResolution ShortfallResolution`; `string UseId`; `SecondaryResourceUseKind Kind`; `bool BlocksPlay` / `Activated` / `IsPreview` / `HasRuntimeCostModifier`
- **`SecondaryResourcePaymentPlan`**（sealed record）— 一次出牌的已解析支付计划: `(CardModel Card, Player? Player, bool IsFree, IReadOnlyList<SecondaryResourcePaymentLine> Lines)`; `bool IsAffordable`; `bool HasLines`; `bool IsPreview`; `static SecondaryResourcePaymentPlan Empty(CardModel card, Player? player, bool isFree = false)`
- **`SecondaryResourcePaymentResolver`**（static）— 构建并提交出牌支付计划的核心管线。关键成员: `static SecondaryResourcePaymentPlan Plan(CardModel card, bool isFree = false, AbstractModel? source = null)`; `static bool CanPay(CardModel card)`; `static Task<SecondaryResourcePlayLedger> Commit(SecondaryResourcePaymentPlan plan, AbstractModel? source = null)`; `static SecondaryResourcePlayLedger CommitFree(SecondaryResourcePaymentPlan plan)`
- **`SecondaryResourceUseKind`**（enum）— 支付条款用途: `RequiredCost`（必需支付，不足时阻止出牌）/ `OptionalSpend`（可选，可付才激活）/ `ExtraSpend`（可重复额外支付，按完整单位购买）
- **`SecondaryResourcePlayUse`**（sealed record）— 附加到卡牌的一条支付条款: `(string Id, string ResourceId, SecondaryResourceCost Cost, SecondaryResourceUseKind Kind)`; init: `Duration` / `BaseCost` / `InsufficientPayment` / `MaxExtraStacks`
- **`SecondaryResourcePlayUseSet`**（sealed class）— 卡牌分层存储的支付条款集合。关键成员: `bool HasUses`; `event Action? Changed`; `Require(string useId, string resourceId, int amount)`; `Require(string, string, SecondaryResourceCost[, SecondaryResourceInsufficientPayment][, SecondaryResourceCostDuration])`; `RequireAllowingShortfall(...)`; `SpendIfAvailable(string, string, int)`; `SpendExtra(string useId, string resourceId, int perStackAmount, int? maxStacks = null, SecondaryResourceCostDuration duration = Permanent)`; `Set(string, string, SecondaryResourceCost, SecondaryResourceUseKind[, ...])`; `bool Clear(string useId)`; `bool ClearDuration(SecondaryResourceCostDuration)`; `IReadOnlyList<SecondaryResourcePlayUse> Snapshot()`
- **`SecondaryResourceInsufficientPaymentMode`**（enum）— 必需费用不足行为: `BlockPlay = 0`（阻止出牌）/ `AllowPlay = 1`（允许出牌并报告缺口）
- **`SecondaryResourceShortfallPaymentHandler`**（delegate）— 处理已提交且仍有缺口的支付: `Task (SecondaryResourceShortfallContext context)`
- **`SecondaryResourceShortfallResolver`**（delegate）— 无副作用地规划替代支付补足量: `SecondaryResourceShortfallResolution (SecondaryResourceShortfallResolutionContext context)`
- **`SecondaryResourceShortfallResolution`**（sealed record）— 替代支付方案: `static None`; `int CoveredAmount`（init）; `SecondaryResourceShortfallPaymentHandler? OnCommit`（init）; `static Cover(int amount, SecondaryResourceShortfallPaymentHandler? onCommit = null)`
- **`SecondaryResourceInsufficientPayment`**（sealed record）— 必需支付资源不足策略对象: `static BlockPlay`; `Mode` / `SpendAvailable` / `OnShortfall` / `ResolveShortfall`（init）; `bool AllowsPlay`; `static AllowPlay(SecondaryResourceShortfallPaymentHandler? onShortfall = null, bool spendAvailable = true, SecondaryResourceShortfallResolver? resolveShortfall = null)`; `static AllowPlayWithReplacement(SecondaryResourceShortfallResolver resolveShortfall, ...)`
- **`SecondaryResourcePersistencePolicy`**（enum）— 存档持久化范围: `None = 0`（不写入）/ `Combat = 1`（仅显式战斗快照）/ `Run = 2`（跨战斗持久化）
- **`SecondaryResourceTurnStartPolicy`**（enum）— 内置回合开始行为: `None` / `ResetToMax` / `AddMaxToCurrent` / `Clear`（设为硬下限）
- **`SecondaryResourceChangeReason`**（enum）— 数量变化原因: `Unknown` / `Gain` / `Lose` / `Set` / `Spend` / `Reset` / `TurnStart`
- **`SecondaryResourceDefinition`**（sealed record）— 已注册战斗资源的完整定义（注册时由 `Bind` 填充 Id/ModId/LocalId）。关键成员: `SecondaryResourceDefinition(int defaultAmount = 0, int? baseMaxAmount = null, int minAmount = 0, int hardMaxAmount = 999_999_999, SecondaryResourceTurnStartPolicy turnStartPolicy = None, SecondaryResourcePersistencePolicy persistencePolicy = None, string? locTable = null, string? titleKey = null, string? descriptionKey = null, string? smallIconPath = null, string? largeIconPath = null)`; init: `string Id` / `ModId` / `LocalId`; `int DefaultAmount`; `int? BaseMaxAmount`; `int MinAmount`; `int HardMaxAmount`; `TurnStartPolicy`; `PersistencePolicy`; `SecondaryResourceInsufficientPayment DefaultInsufficientPayment`; `string? LocTable` / `TitleKey` / `DescriptionKey` / `SmallIconPath` / `LargeIconPath`; `bool IsVisibleInCombatUi(Player player)`; `bool IsVisibleOnCard(CardModel card, SecondaryResourcePaymentLine? paymentLine = null)`
- **`SecondaryResourceState`**（sealed class）— 单名玩家一场战斗内的可变资源数量。关键成员: `bool HasValues`; `event Action<SecondaryResourceChangedEvent>? Changed`; `int Get(string resourceId)`; `IReadOnlyDictionary<string, int> Snapshot()`
- **`SecondaryResourceChangedEvent`**（sealed record）— 数量变化通知: `(Player, Definition, OldAmount, NewAmount, Reason, Source)`; `int Delta`
- **`SecondaryResourceStateStore`**（static）— 按玩家（PlayerCombatState）附加状态的存取。关键成员: `static SecondaryResourceState Get(Player player)`; `static bool TryGet(Player player, out SecondaryResourceState state)`; `static int GetAmount(Player player, string resourceId)`; `static int? GetMaxAmount(Player player, string resourceId)`
- **`SecondaryResourceHook`**（static）— 钩子总调度：依次聚合全局监听器、战斗中实现接口的模型、以及 `SecondaryResourceModelHookRegistry` 注册的费用钩子。关键成员: `static void RegisterGlobalListener(ISecondaryResourceHookListener listener)`; `ModifyMaxAmount(...)`; `ModifyGain(...)`; `ModifyCost(...)`（常规+Late 两遍）; `ModifyXValue(...)`; `ShouldGain(...)`; `ShouldSpend(...)`; `ModifyInsufficientPayment(...)`; `ResolveShortfall(...)`; `ShouldReset(...)`; `Task AfterChanged(...)`; `Task AfterSpent(...)`; `Task AfterShortfallPayment(...)`; `Task AfterReset(...)`
- **`SecondaryResourceModelHookRegistry`**（static）— 为无法实现钩子接口的模型类型按精确类型注册/替换费用钩子。关键成员: `static void RegisterCostHooks<TModel>(Func<TModel, SecondaryResourceCostContext, decimal, decimal>? modifyCost = null, Func<TModel, SecondaryResourceCostContext, decimal, decimal>? modifyCostLate = null)`; `static void RegisterCostHooks(Type modelType, ...)`; `static bool UnregisterCostHooks<TModel>()`; `static bool UnregisterCostHooks(Type modelType)`
- **`SecondaryResourceHistory`**（static）— 把资源事件附加到游戏战斗历史。关键成员: `static IReadOnlyList<SecondaryResourceHistoryEntry> Entries(CombatHistory history)`; `Changes(CombatHistory)`; `Spends(CombatHistory)`; `Resets(CombatHistory)`
- **`SecondaryResourceHistoryEntry`**（abstract class）— 历史条目的共享元数据。关键成员: `Player Player`; `SecondaryResourceDefinition Definition`; `AbstractModel? Source`; `int RoundNumber`; `CombatSide CurrentSide`; `abstract string Description`; `bool HappenedThisTurn(CombatStateLike? state)`; `bool HappenedLastPlayerTurn(Player player)`（子类：`SecondaryResourceChangedEntry`/`SpentEntry`/`ResetEntry`）
- **`SecondaryResourcePersistence`**（static）— 存档保存/恢复。关键成员: `static void Initialize()`; `static SecondaryResourceRunSaveState CreateSnapshot(CombatStateLike combatState, bool includeCombatScoped)`; `static void RestoreSnapshot(CombatStateLike combatState, SecondaryResourceRunSaveState snapshot)`
- **`SecondaryResourceRunSaveState`**（sealed class）— 可序列化快照（先按玩家 NetId 再按资源 ID）。关键成员: `Dictionary<ulong, Dictionary<string, int>> PlayerAmounts { get; set; }`; `bool IsEmpty`
- **`SecondaryResourcePlayLedger`**（sealed record）— 一次出牌实际产生的支付记录。关键成员: `(CardModel Card, Player? Player, bool IsFree, IReadOnlyDictionary<string, SecondaryResourcePlayLedgerLine> Lines)`; `int Spent(string resourceId)`; `int SpentByUse(string useId)`; `int ExtraSpentByUse(...)`; `int ExtraStacksByUse(...)`; `int Value(string resourceId)`; `int Shortfall(string resourceId)`; `int OriginalShortfall(...)`; `int CoveredShortfall(...)`; `bool CostsX(string resourceId)`; `bool Activated(string useId)`; 各 `...ByUse` 便捷方法
- **`SecondaryResourcePlayLedgerLine`**（sealed record）— 一条支付明细或资源汇总: `(string ResourceId, int AmountSpent, int Value, bool CostsX, bool IsFree)`; init: `UseId` / `Kind` / `Activated` / `Shortfall` / `OriginalShortfall` / `CoveredShortfall` / `BaseAmountSpent` / `ExtraAmountSpent` / `ExtraStacks`
- **`SecondaryResourcePlayExtensions`**（static 扩展方法）— 从 `CardPlay` 读取出牌支付记录。关键成员: `static SecondaryResourcePlayLedger SecondaryResources(this CardPlay play)`; `static bool TryGetSecondaryResources(this CardPlay play, out SecondaryResourcePlayLedger ledger)`
- **`SecondaryResourcePlayLedgerRuntime`**（static）— 运行时排队/附加支付记录到 CardPlay。关键成员: `static SecondaryResourcePlayLedger Get(CardPlay play)`; `static void Attach(CardPlay play, SecondaryResourcePlayLedger ledger)`; `static void SetPending(CardModel card, SecondaryResourcePlayLedger ledger)`; `static IDisposable? BeginPendingScope(CardModel card)`; `static bool TryBindPending(CardPlay play)`
- **UI 上下文/风格/节点**（`SecondaryResourceCombatUiContext<TParent,TNode>`、`SecondaryResourceCombatUiChangeContext<TParent,TNode>`、`SecondaryResourceCardUiContext<TParent,TNode>`、`SecondaryResourceMultiplayerPlayerStateUiContext<TNode>`、`SecondaryResourceCombatUiChangedHandler<TParent,TNode>` delegate、`SecondaryResourceUiRuntime`（UpdateCombatUi/NotifyCombatUiChanged/HideCombatUi/UpdateCardUi/UpdateMultiplayerPlayerStateUi）、`SecondaryResourceVisibility`（`GetCombatUiDefinitions(Player?)`/`GetCardUiDefinitions(CardModel, plan)`）、`SecondaryResourceCombatVisibilityContext`、`SecondaryResourceCardVisibilityContext`、`SecondaryResourceCombatUiVisibilityPredicate` delegate）
- **计数器/图标节点**: `NSecondaryResourceCounter`（`Create(definition, style)`/`Bind(Player?, bool autoRefresh)`/`SetAmount(int, int?)`）; `NSecondaryResourceIcon`; `NSecondaryResourceCounterRow`; `SecondaryResourceCounterStyle`（`static Default`）; `SecondaryResourceIconStyle`; `SecondaryResourceCounterGainFeedback`（`static None/StarCounterLike/EnergyCounterLike`）+ `SecondaryResourceCounterGainEffect` 系列（`IconBrightnessFlashEffect`/`StarCounterLikeBurstEffect`/`EnergyCounterLikeParticlesEffect`/`SceneBurstEffect`）; `SecondaryResourceCounterGainEffects` 静态工厂
- **悬停提示**: `SecondaryResourceHoverTipRequest`、`SecondaryResourceHoverTipPlacementContext`、`SecondaryResourceHoverTipStyle`（`static Default`）、`SecondaryResourceHoverTipBinder`（`static Bind(Control owner, Func<SecondaryResourceHoverTipRequest?> requestFactory, style)`）、`SecondaryResourceHoverTipFactory`（`Create`/`Show`）
- **本地化**: `SecondaryResourceVar`（DynamicVar，带 ResourceId）; `SecondaryResourceVars`（`For`/`ForLocal`）; `SecondaryResourceLocStringSource`（SmartFormat 选择器 `{secondaryResource:...}`）; `SecondaryResourceIconsFormatter`（`secondaryResourceIcons` 格式化器）; `SecondaryResourceText`（`GetIconTag`/`GetTitle`/`GetTitleText`/`GetDescription`/`GetDescriptionText`）
- **用法要点**:
  - **注册**：`ModSecondaryResourceRegistry.For(modId).Register(localId, new SecondaryResourceDefinition(defaultAmount, baseMaxAmount, ..., turnStartPolicy, persistencePolicy, locTable/titleKey/descriptionKey, smallIconPath, largeIconPath))`；完整 ID = `ModContentRegistry.GetCompoundId(modId, "SECONDARY_RESOURCE", localId)`（`GetResourceId` 获取）。重复注册同 ID 返回首次定义，跨 mod 抢占抛异常。
  - **数量变更**：一律经 `SecondaryResourceCmd`（`Gain/Lose/Set/Spend/Reset`），内部跑 `Should*` 检查 → `Modify*` 修正 → 钳制到 `[MinAmount, HardMaxAmount]` → 写入 `SecondaryResourceState`（触发 `Changed`）→ 记录历史 → 更新 UI → await `After*` 钩子。回合开始由 `ApplyTurnStartPolicies` 按 `TurnStartPolicy` 批量处理。
  - **钩子时机**：模型效果直接实现 `ISecondaryResourceHookListener`（或 `SecondaryResourceModelHookRegistry.RegisterCostHooks<TModel>`），战斗中实例由 `SecondaryResourceHook` 统一调度；进程级用 `RegisterGlobalListener`。费用管线：卡牌本地 `ICardSecondaryResourceCostContributor` → 战斗级 `ModifyCost`（常规+Late）→ X 值（`ModifyXValue`）→ `ShouldSpend` → 不足时 `ModifyInsufficientPayment` + `ResolveShortfall`，提交后执行 `OnCommit`/`OnShortfall` 并触发 `AfterShortfallPayment`。
  - **卡牌支付**：`card.SecondaryCosts()`（费用层）与 `card.SecondaryResourceUses()`（条款层，`Require/SpendIfAvailable/SpendExtra`）附加；或实现 `ICardSecondaryResourceUseContributor`。出牌时 `SecondaryResourcePaymentResolver.Plan(card)` → `CanPay` → `Commit(plan)` 返回 `SecondaryResourcePlayLedger`（效果在 `CardPlay` 上用 `play.SecondaryResources()` 读取）。
  - **UI 显示**：`registry.RegisterCombatUi<TParent,TNode>(...)` / `RegisterCardUi(...)` 挂节点，或用现成 `NSecondaryResourceCardCostUi` / `NSecondaryResourceCounter(Row)`；可见性默认"数量>默认值即显示"，可用 `RegisterCombatUiAlwaysVisibleWhen` / `AlwaysShowInCombatUiForCharacter` / `AlwaysShowInCombatUi` 扩展。
  - **存档**：`PersistencePolicy.Run` 资源跨战斗保存（`SecondaryResourcePersistence.Initialize()` 注册生命周期）；`Combat` 范围仅在显式 `CreateSnapshot(..., includeCombatScoped: true)` 时纳入。

### 1.10 战斗 UI 图标角标附加数字标签（`STS2RitsuLib.Combat.Ui.ExtraCornerAmountLabels`）

- **`ExtraIconAmountLabelCorner`** — 额外角标位置枚举。取值: `TopLeft`; `TopRight`; `BottomLeft`; `BottomRight`; `Custom`
- **`ExtraIconAmountLabelTextMode`** — 角标渲染方式枚举。取值: `Plain`（`MegaLabel` 字面渲染）; `RichText`（Godot/Mega 富文本解析）
- **`ExtraIconAmountLabelSlot`** — 纯文本角标描述（readonly record struct）。关键成员: 构造 `(string text, ExtraIconAmountLabelCorner corner)`; 静态 `At(corner, text[, fontColor[, fontOutlineColor]])`; `WithCustom(text, Rect2[, ...])`
- **`ExtraIconRichTextLabelSlot`** — 富文本角标描述（readonly record struct）。关键成员同 `ExtraIconAmountLabelSlot` 的构造与 `At`/`WithCustom` 静态集
- **`ExtraIconAmountLabelSpec`** — 统一提供接口中的角标描述（readonly record struct，`TextMode = Plain` 默认）。关键成员: 多种构造; `static implicit operator ExtraIconAmountLabelSpec(...)`; 静态 `Plain(...)` / `RichText(...)` / `PlainCustom(...)` / `RichTextCustom(...)`
- **`IPowerExtraIconAmountLabelsProvider`** — 在 `PowerModel` 子类上实现，给其 `NPower` 节点添加纯文本角标。关键成员: `IReadOnlyList<ExtraIconAmountLabelSlot> GetPowerExtraIconAmountLabelSlots()`
- **`IPowerExtraIconAmountLabelSpecsProvider`** — 提供纯文本+富文本力量角标（与 Provider 同时实现时优先）。关键成员: `IReadOnlyList<ExtraIconAmountLabelSpec> GetPowerExtraIconAmountLabelSpecs()`
- **`IPowerExtraIconAmountLabelsChangeSource`** — 可选主动刷新通知（主线程触发）。关键成员: `event Action? PowerExtraIconAmountLabelsInvalidated`
- **`IRelicExtraIconAmountLabelsProvider`** — 在 `RelicModel` 子类上实现，给 `NRelicInventoryHolder` 添加纯文本角标。关键成员: `IReadOnlyList<ExtraIconAmountLabelSlot> GetRelicExtraIconAmountLabelSlots()`
- **`IRelicExtraIconAmountLabelSpecsProvider`** — 遗物富文本/统一接口（优先于 Provider）。关键成员: `IReadOnlyList<ExtraIconAmountLabelSpec> GetRelicExtraIconAmountLabelSpecs()`
- **`IRelicExtraIconAmountLabelsChangeSource`** — 遗物角标主动刷新通知。关键成员: `event Action? RelicExtraIconAmountLabelsInvalidated`
- **`IIntentExtraCornerAmountLabelsProvider`** — 在 `AbstractIntent` 子类上实现，给 `NIntent` 添加纯文本角标。关键成员: `IReadOnlyList<ExtraIconAmountLabelSlot> GetIntentExtraCornerAmountLabelSlots()`
- **`IIntentExtraCornerAmountLabelSpecsProvider`** — 意图富文本/统一接口（优先于 Provider）。关键成员: `IReadOnlyList<ExtraIconAmountLabelSpec> GetIntentExtraCornerAmountLabelSpecs()`
- **`IIntentExtraCornerAmountLabelsChangeSource`** — 意图角标主动刷新通知（主线程触发）。关键成员: `event Action? IntentExtraCornerAmountLabelsInvalidated`
- **用法要点**: 给自定义 `PowerModel` / `RelicModel` / `AbstractIntent` 子类实现对应 `I*ExtraIconAmountLabelsProvider`（纯文本）或 `I*...SpecsProvider`（含富文本，二者同实现时 Specs 优先）即可；无需任何显式注册。运行时反射读取宿主原版数量/数值标签作排版参照，按 `RitsuExtra{Power,Relic,Intent}CornerSlot_` 前缀创建/复用 `MegaLabel` 或 `MegaRichTextLabel` 子节点；内置角落每角只取首个条目，`Custom` 条目可重叠后绘置顶。实现 `I*...ChangeSource` 可即时刷新（须在主线程触发事件）。

---

## 2. Interactions（交互）

### 2.1 模型右键（`STS2RitsuLib.Interactions.RightClick`）

- **`IModRightClickableModel`** — mod 模型实现此接口即可接收同步右键操作。关键成员: `bool CanHandleRightClickLocal(ModRightClickContext context)`（默认 true，仅本地快速筛选，只查稳定 UI 事实）; `bool CanExecuteRightClick(ModRightClickExecutionContext context)`（默认 true，各端解析出同步模型后的执行守卫）; `Task OnRightClick(ModRightClickExecutionContext context)`（同步操作入队后执行，唯一必须实现）
- **`IModRightClickableCard` / `IModRightClickableRelic` / `IModRightClickablePower` / `IModRightClickablePotion` / `IModRightClickableOrb`** — 按模型族分的空标记接口，继承 `IModRightClickableModel`，无额外成员
- **`IModRightClickHandler`** — 在内置模型接口处理器之前拦截本地右键请求的自定义处理器。关键成员: `int Priority => 0`（越高越先运行）; `bool TryHandle(ModRightClickContext context)`（返回 true 表示接受并消耗本次输入）
- **`ModRightClickBindingId`** — 标识一个已注册右键绑定的字符串包装（readonly record struct）。关键成员: `ModRightClickBindingId(string Id)`
- **`ModRightClickContext`** — 描述一个本地分发的右键请求。关键成员: `ModRightClickContext(Player Player, AbstractModel Model, ModRightClickTrigger Trigger)`
- **`ModRightClickExecutionContext`** — 描述同步右键操作进入行动队列时的执行状态。关键成员: `ModRightClickExecutionContext(Player, AbstractModel, ModRightClickTrigger, GameActionPlayerChoiceContext?, GameAction?)`
- **`ModRightClickModelKind`** — 内置右键补丁支持的模型类别枚举: `Card = 0`; `Relic = 1`; `Power = 2`; `Potion = 3`; `Orb = 4`
- **`ModRightClickRegistry`** — 注册并分发模型同步右键交互的静态注册表。关键成员: `public static void Register(IModRightClickHandler handler)`; `public static IDisposable Register<TModel>(string modId, string localStem, Func<ModRightClickContext, bool> canHandle, Func<ModRightClickExecutionContext, Task> execute, int priority = 0) where TModel : AbstractModel`; `public static IDisposable Register<TModel>(string modId, string localStem, Func<ModRightClickExecutionContext, Task> execute, int priority = 0, Func<ModRightClickContext, bool>? canHandleLocal = null, Func<ModRightClickExecutionContext, bool>? canExecute = null) where TModel : AbstractModel`; `public static bool TryDispatch(ModRightClickContext context)`
- **`ModRightClickSource`** — 右键请求来源界面枚举: `Unknown = 0`; `HandCard = 1`; `CombatPileCard = 2`; `Relic = 3`; `Power = 4`; `Potion = 5`; `Orb = 6`
- **`ModRightClickTrigger`** — 右键请求的输入元数据。关键成员: `ModRightClickTrigger(bool IsController = false, string? Metadata = null)`; `ModRightClickSource Source { get; init; }`; `PileType? ExpectedCardPile { get; init; }`
- **用法要点**:
  - 补丁已启用右键的模型（全部 internal）：手牌卡牌、战斗牌堆界面卡牌（Draw/Discard/Exhaust）、遗物、能力、药水、本地玩家充能球。
  - 方式一（接口）：模型实现 `IModRightClickableCard` 等对应接口 → `CanHandleRightClickLocal` 本地快速筛选 → `CanExecuteRightClick` 各端执行守卫 → `OnRightClick` 写同步行为；后两者有默认 true，通常只实现 `OnRightClick`。
  - 方式二（注册绑定）：`Register<TModel>(modId, localStem, execute, priority, canHandleLocal, canExecute)`；返回的 `IDisposable` 句柄 `Dispose()` 即注销绑定。同一 id 重复注册抛 `InvalidOperationException`。
  - 分发流程：本地 `TryDispatch` 按 Priority 降序跑自定义处理器（内置处理器垫底），首个返回 true 即消费输入；内置处理器收集候选绑定 → 序列化 payload → 经 `RitsuLibManagedNetActions.Request` 作为同步行动发往发起玩家（战斗中仅 PlayPhase 可用）→ 执行端按绑定 id 顺序解析模型、跑 `canExecute` 守卫后执行；任一绑定成功且模型仍在状态则调 `InvokeExecutionFinished()`。
  - 异常语义：`canHandle`/`canExecute` 返回 false 跳过该绑定；绑定执行与守卫异常一律捕获记日志、不传播、不中断后续绑定。

---

## 3. Ui（界面）

### 3.1 富文本特效（`STS2RitsuLib.Ui.RichTextEffects`）

- **`ModRichTextEffectRegistration`** — 记录已注册模组富文本特效的元数据与特效实例（sealed record）。关键成员: `record ModRichTextEffectRegistration(string ModId, string Bbcode, RichTextEffect Effect)`
- **`ModRichTextEffectRegistry`** — 为 `MegaRichTextLabel` 注册/查询自定义 `RichTextEffect` 的全局注册表（static）。关键成员: `GetQualifiedBbcode(string modId, string localTagStem): string`; `RegisterOwned<TEffect>(string modId, string localTagStem)` / `RegisterOwned(string modId, string localTagStem, RichTextEffect effect)`; `Register<TEffect>(string modId)` / `Register<TEffect>(string modId, string bbcode)` / `Register(string modId, RichTextEffect effect)` / `Register(string modId, string bbcode, RichTextEffect effect)`; `TryGet(string bbcode, out ModRichTextEffectRegistration): bool`; `GetRegistrationsSnapshot(): ModRichTextEffectRegistration[]`; `Wrap(string bbcode, string text, params ModRichTextTagParameter[]): string`; `WrapOwned(string modId, string localTagStem, string text, params ModRichTextTagParameter[]): string`; `Wrap(ModRichTextEffectRegistration registration, string text, params ModRichTextTagParameter[]): string`
- **`ModRichTextTag`** — BBCode 标签构建器（static）。关键成员: `Param(string name, object? value): ModRichTextTagParameter`; `Wrap(string bbcode, string text, params ModRichTextTagParameter[]): string`
- **`ModRichTextTagParameter`** — BBCode 参数名/值对（readonly record struct）。关键成员: `readonly record struct ModRichTextTagParameter(string Name, object? Value)`
- **用法要点**: 从 `RichTextEffect` 派生特效类（须暴露可写 string `bbcode` 成员，注册时自动写入标签名），用 `RegisterOwned(modId, localTagStem)` 注册，标签名自动派生为小写复合 ID `{modid}_richtext_{stem}`（`GetQualifiedBbcode` 可预览），避免全局标签冲突。BBCode 标签全局唯一：仅"同 mod + 同一实例"的重复注册返回既有项，其余冲突抛 `InvalidOperationException`。注册后 RitsuLib 自动把特效装入所有启用 BBCode 的 MegaRichTextLabel（就绪、文本更新、编辑器恢复场景数据时）。拼文本用 `Wrap`/`WrapOwned` + `Param`：参数支持 bool/数字/Color/字符串，null 省略，字符串自动加引号转义。

### 3.2 Toast 通知（`STS2RitsuLib.Ui.Toast`）

- **`RitsuToastService`** — 显示与管理浮动通知的全局入口（static）。关键成员: `Show(RitsuToastRequest request)`; `ShowTracked(RitsuToastRequest): RitsuToastHandle`; `ShowInfo(string body, string? title = null, Action? onClick = null)`; `ShowWarning(...)`; `ShowError(...)`; `ShowInfoTracked/ShowWarningTracked/ShowErrorTracked(...): RitsuToastHandle`; `IsAlive(RitsuToastHandle): bool`; `Close(RitsuToastHandle, bool immediate = false): bool`; `CloseAll(bool immediate = false): int`; `Update(RitsuToastHandle, RitsuToastRequest, bool resetDuration = true): bool`; `UpdateBody/UpdateText/UpdateTitle(...)`; `ResetDuration(RitsuToastHandle, double? durationSeconds = null): bool`
- **`RitsuToastHandle`** — 已跟踪 toast 的稳定句柄（sealed class，由 `ShowTracked` 返回）。关键成员: `Guid Id`; `IsAlive(): bool`; `Close(bool immediate = false)`; `Dismiss(bool immediate = false)`; `Update/UpdateBody/UpdateText/UpdateTitle/ResetDuration(...)`
- **`RitsuToastLevel`** — 语义级别（enum），决定默认配色: `Info`; `Warning`; `Error`
- **`RitsuToastAnimationPreset`** — 进出场内置动画（enum）: `Fade`; `FadeSlide`; `FadeScale`
- **`RitsuToastAnchor`** — 堆栈在视口 3×3 网格上的锚点（enum）: `TopLeft/TopCenter/TopRight/MiddleLeft/MiddleCenter/MiddleRight/BottomLeft/BottomCenter/BottomRight`
- **`RitsuToastRequest`** — 描述 toast 内容/生命周期/交互/呈现的不可变请求（sealed record）。关键成员: `RitsuToastRequest(string body, string? title = null, Texture2D? image = null, RitsuToastLevel level = Info, double? durationSeconds = null, Action? onClick = null, RitsuToastAnimationPreset? animationOverride = null)`; init 属性 `Body/Title/Image/Level/DurationSeconds/OnClick/AnimationOverride/IsPersistent/ProgressFraction/DismissOnClick`; 静态工厂 `Info/Warning/Error(string body, string? title = null)`; 复制方法 `WithBody/WithTitle/WithText/WithImage/WithLevel/WithDuration/WithClick/WithAnimation/Persistent/WithProgress/WithDismissOnClick`
- **用法要点**: 最快用法 `RitsuToastService.ShowInfo("正文", "标题", onClick)`；定制时构造 `RitsuToastRequest`（配图、级别、单条时长、动画预设、`IsPersistent` 常驻、`ProgressFraction` 显式进度、`DismissOnClick` 点击关闭）。需后续管理时用 `ShowTracked`/`*Tracked` 拿句柄做 `Update*`/`Close`/`ResetDuration`。宿主未就绪时请求排队，全局禁用时静默丢弃；默认右上角堆栈、最多 3 条、6 秒、FadeSlide。视觉样式由主题 `components.toast.*` 令牌解析（内部实现 `RitsuToastHost`/`RitsuToastEntry`，仅服务与模型公开）。

### 3.3 浮动窗口（`STS2RitsuLib.Ui.Windows`）

- **`RitsuFloatingWindow`** — 带主题的浮动内容窗口，支持固定/拖动、八方向缩放、内容替换与几何保存恢复（sealed partial : PanelContainer，主线程使用）。关键成员: `RitsuFloatingWindow()`; `RitsuFloatingWindow(RitsuFloatingWindowOptions options)`; `RitsuFloatingWindowOptions Options`; `InteractionLocked { get; set; }`; `event EventHandler? Closed`; `event EventHandler? GeometryChanged`; `Configure(RitsuFloatingWindowOptions options)`（入树前）; `SetContent(Control content): Control?`; `TakeContent(): Control?`; `CaptureGeometry(): RitsuFloatingWindowGeometry`; `ApplyGeometry(RitsuFloatingWindowGeometry)`（须在树内）; `Close()`
- **`RitsuFloatingWindowOptions`** — 标题、尺寸限制、初始位置与交互能力配置（sealed class，init 属性，构造/应用时校验）。关键成员: `Title`; `InitialSize`; `FitInitialSizeToContent`; `MinimumSize`; `MaximumSize`（零分量=对应视口尺寸）; `Movable`; `Resizable`; `Closable`; `StartCentered`; `ConstrainToViewport`
- **`RitsuFloatingWindowGeometry`** — 窗口未缩放的位置与尺寸快照（readonly record struct）。关键成员: `readonly record struct RitsuFloatingWindowGeometry(Vector2 Position, Vector2 Size)`
- **用法要点**: `new RitsuFloatingWindow(new RitsuFloatingWindowOptions { Title = ..., InitialSize = ..., ... })` → `SetContent(control)` 注入内容（须未挂载，返回旧内容）→ 加入场景树。`CaptureGeometry`/`ApplyGeometry` 做持久化；`Close()` 隐藏并同步触发 `Closed`。仅限主线程；入树后禁止 `Configure` 重配；替换出的旧内容不释放，所有权归调用方。

### 3.4 Shell / 主题（`STS2RitsuLib.Ui.Shell` 与 `STS2RitsuLib.Ui.Shell.Theme`）

- **`RitsuShellTheme`** — 已解析主题的不可变快照，暴露类型化令牌、路径访问与模组扩展数据（sealed class）。关键成员: `static RitsuShellTheme Current`; `string Id`; `ColorTokens Color`; `TextTokens Text`; `SurfaceTokens Surface`; `ComponentTokens Component`; `MetricTokens Metric`; `FontTokens Font`; `GetColor(string path): Color`; `TryGetColor(...)`; `GetDimension/GetDimensionDouble/GetDimensionInt(string path)`; `TryGetNumber(string path, out double)`; `GetBool(string path)`; `GetFontFamily(string path): Font`; `TryGetExtension(string modId, out JsonElement)`; `ListExtensionModIds()`
- **`RitsuShellThemeRuntime`** — 管理当前主题快照、应用/重载、变更通知与模组令牌注册（static）。关键成员: `static string ActiveThemeId`; `static RitsuShellTheme Current`; `static event Action? ThemeChanged`; `EnsureBaseline()`; `ApplyThemeId(string? themeId)`; `ReapplyActiveTheme(bool forceReloadCatalog)`; `RegisterModTokens(string modId, JsonElement? defaults, Action<RitsuShellTheme>? onApply = null)`; `UnregisterModTokens(string modId)`
- **`RitsuShellThemeModRegistration`** — 模组默认令牌贡献与可选新快照回调（sealed record）。关键成员: `record RitsuShellThemeModRegistration(string ModId, JsonElement? Defaults, Action<RitsuShellTheme>? OnApply)`
- **`RitsuShellThemeDocument`** — W3C 设计令牌格式主题文档（.theme.json）模型（sealed class）。关键成员: `SchemaReference`; `ThemeFormatVersion`; `ThemeVersion`; `Id`; `DisplayName`; `Inherits`; `Core`; `Semantic`; `Components`; `Scopes`; `Extensions`; `static RitsuShellThemeDocument? Deserialize(Stream stream)`
- **`RitsuShellThemeCatalog`** — 加载内嵌/磁盘主题文档，构建合并 + 引用解析后的快照（static）。关键成员: `RegisteredThemeIds`; `InvalidateCache()`; `EnsureLoaded()`; `TryBuildSnapshot(string themeId, IReadOnlyList<RitsuShellThemeModRegistration>, out string resolvedId, out RitsuShellTheme? theme): bool`; `TryRestoreDiskThemeFromEmbedded(...)`; `TryRestoreAllExistingDiskThemesFromEmbedded(out int): bool`
- **`RitsuShellThemePaths`** — Shell 主题目录路径（static）。关键成员: `GetShellThemesDirectoryVirtual(): string`; `TryEnsureShellThemesDirectory(out string): bool`
- **`RitsuShellChromeStyles`** — 紧凑编辑器/列表/工具栏共享 StyleBoxFlat 工厂（static，结果只读可缓存）。关键成员: `CreateSurfaceStyle()`; `CreateEntryFieldFrameStyle(bool emphasized)`; `CreateColorPickerSwatchFrameStyle()`; `CreateInsetSurfaceStyle()`; `CreateChromeActionsMenuStyle(bool highlighted)`; `CreatePageToolbarTrayStyle()`; `CreateListShellStyle()`; `CreateListItemCardStyle(bool accent = false)`; `CreateListEditorSurfaceStyle()`; `CreatePillStyle(bool highlighted = false)`; `CreateTooltipPanelStyle()`
- **`RitsuShellPanelStyles`** — 带框面板与侧边栏卡片 StyleBoxFlat 工厂（static）。关键成员: `CreateFramedSurface(Color background, int cornerRadius)`; `CreateSidebarModCard(int cornerRadius, bool selected)`; `CreateSidebarModCardCompact(int cornerRadius, bool selected, int innerMargin = 6)`
- **`RitsuShellTooltipTheme`** — 将主题令牌映射到 Godot 原生 `TooltipPanel`/`TooltipLabel`（static）。关键成员: `ApplyToTreeRoot(Control root)`
- **令牌类型（Tokens 目录，全部 public sealed record）**: `ColorTokens`（`White/Transparent/Divider/UnsetPreview/ModalBackdrop/Shadow`）及 `ShadowTokens`/`TextTokens`（12 色）/`SurfaceTokens`（含 `EntrySurfaceTokens/InsetSurfaceTokens/FramedSurfaceTokens`）; `ComponentTokens`（19 组件组：`SidebarCard/ChromeMenu/PageToolbarTray/ListShell/ListItem/ListEditor/Pill/Toggle/Slider/Dropdown/Stepper/DragHandle/Collapsible/SidebarBtn/SidebarRail/TextButton/StringValidation/OverlayPanel/ChoiceCenter`）; `FontTokens(Body/BodyBold/Button)`; `MetricTokens(Radius/BorderWidth/Entry/Slider/Choice/Color/StringEntry/Keybinding/Overlay/Sidebar/FontSize)` 及对应子记录
- **用法要点**: 模组用 `RitsuShellThemeRuntime.RegisterModTokens(modId, defaultsJson, onApply)` 注册默认令牌：`defaults` 为设计令牌格式 JSON（`core`/`semantic`/`components` 分组、`scopes.mod:<modId>` 覆盖、`extensions.<modId>` 扩展数据），在所选主题继承链之前深合并；`onApply` 在新快照发布后回调。主题文件 `.theme.json` 存放于 `user://` Shell 主题目录（`RitsuShellThemePaths`），内嵌版本较新时自动替换磁盘副本并备份。消费侧直接读 `RitsuShellTheme.Current` 强类型令牌（如 `Current.Text.LabelPrimary`、`Current.Metric.FontSize.Secondary`）或用 `GetColor("components.toggle.on.bg")` 路径查询；主题切换经 `ThemeChanged` 广播。构建/解析/合并部分（Builder/LayoutResolver/Merger/ReferenceResolver/ValueCoerce、StyleCache）为 internal，非 mod 作者 API。

### 3.5 Overlay（`STS2RitsuLib.Ui.Overlay`）

- 该子系统**全部为 internal**，不对 mod 作者开放：`RitsuOverlayHostService`（internal static）、`RitsuOverlayHost`（internal Node）、`RitsuOverlaySubmenuStack`（internal）、`RitsuDebugToolsDock`/`RitsuDebugToolsIcons` 等。
- **用法要点**: 无公开 API。mod 作者如需类似"浮层 + 子菜单 + 热键"能力，应改用公开的 `RitsuToastService`、`RitsuFloatingWindow` 与 `RitsuCatalogBrowser`。

### 3.6 目录浏览器（`STS2RitsuLib.Ui.Catalog`）

- **`RitsuCatalogBrowser`** — 带主题、可搜索、单选筛选、虚拟滚动、可选详情面板的目录浏览器（sealed partial : Control，仅主线程创建/修改）。关键成员: `const int MaximumItemCount = 16384`; `const int MaximumSearchTextLength = 512`; `RitsuCatalogBrowser()`; `RitsuCatalogBrowser(RitsuCatalogBrowserOptions options, IReadOnlyList<RitsuCatalogFilter>? filters = null)`; `IReadOnlyList<RitsuCatalogItem> Items`; `RitsuCatalogItem? SelectedItem`; `event EventHandler<RitsuCatalogSelectionChangedEventArgs>? SelectionChanged`; `SetItems(IReadOnlyList<RitsuCatalogItem> items)`; `SetFilters(...)`（UI 构建后禁止）; `SelectItem(string? itemId): bool`; `Refresh()`
- **`RitsuCatalogPresentation`** — 布局（enum）: `List`（每行一项）; `Grid`（自适应多列图标卡片）
- **`RitsuCatalogDetailPresentation`** — 详情呈现（enum）: `Inline`（常驻详情面板）; `Drawer`（右侧滑入抽屉）
- **`RitsuCatalogItemActionTone`** — 快捷操作色调（enum）: `Normal`; `Danger`
- **`RitsuCatalogItemAction`** — 目录项上的图标快捷操作，与选择独立（sealed class）。关键成员: `RitsuCatalogItemAction(Texture2D icon, string tooltip, Action action, RitsuCatalogItemActionTone tone = Normal)`
- **`RitsuCatalogItem`** — 不可变目录项（sealed class）。关键成员: `const int MaximumIdLength = 256`; `const int MaximumTextLength = 2048`; 构造 `RitsuCatalogItem(string id, string title, string? subtitle = null, string? searchText = null, Texture2D? icon = null, string? badge = null, Func<Texture2D?>? iconFactory = null, string? tooltip = null, RitsuCatalogItemAction? quickAction = null, Color? accentColor = null)`
- **`RitsuCatalogFilterOption`** — 筛选组内一个可选项（sealed class）。关键成员: `RitsuCatalogFilterOption(string id, string label, Func<RitsuCatalogItem, bool> matches)`
- **`RitsuCatalogFilter`** — 单选筛选组，浏览器自动添加"全部"首选项（sealed class）。关键成员: `RitsuCatalogFilter(string id, string label, string allLabel, IReadOnlyList<RitsuCatalogFilterOption> options)`
- **`RitsuCatalogBrowserOptions`** — 呈现文本/尺寸/详情工厂配置（sealed class，init 属性，构造与 `_Ready` 时校验）。关键成员: `Presentation`; `DetailPresentation`; `SearchPlaceholder`; `EmptyText`; `DetailPlaceholderText`; `DetailUnavailableText`; `MinimumHeight`; `CatalogWidth`; `DetailMinimumWidth`; `DetailMaximumWidth`; `DetailPreferredWidthFraction`; `MinimumVisibleCatalogWidth`; `RowHeight`; `GridTileMinimumWidth`; `GridTileHeight`; `DetailFactory`（`Func<RitsuCatalogItem, Control>`）
- **`RitsuCatalogSelectionChangedEventArgs`** — 选择变更事件数据（sealed class）。关键成员: `RitsuCatalogItem? Item`
- **用法要点**: 完全对 mod 作者开放：`new RitsuCatalogBrowser(new RitsuCatalogBrowserOptions { Presentation = Grid, DetailPresentation = Drawer, DetailFactory = item => ... })` 后 `SetItems` 填入 `RitsuCatalogItem`（ID 唯一，支持惰性图标工厂/快捷操作/强调色），最多 8 组单选筛选；`SelectItem` 按稳定 ID 选择，`SelectionChanged` 订阅变化。搜索 0.16s 防抖，行/瓦片虚拟化支撑 16384 项；详情工厂返回的控件须未挂载，浏览器接管所有权并在替换时释放。

### 3.7 Ui 基础（`STS2RitsuLib.Ui`；EmbeddedPng 两文件在 `STS2RitsuLib`）

- **`RitsuUiLayer`** — CanvasLayer 层级常量表（**internal static class**，非公开 API）。常量: `CombatOverlay = 20`; `Workspace = 100`; `Modal = 120`; `BlockingProgress = 128`; `Dialog = 132`; `Toast = 160`。mod 作者无法直接引用（internal），自建浮层需自行定义层级数值并注意避让这些值。
- **`RitsuLibEmbeddedPngAssets` / `RitsuLibEmbeddedPngAsset`** — **internal**（`STS2RitsuLib`）：内嵌 PNG 资源表（`card_art_placeholder.png`、`mod_image.png` 等），仅供库内部引用。
- **`RitsuLibEmbeddedPngResourceLoader`** — **internal**（`STS2RitsuLib`）：`ResourceFormatLoader` 子类，使虚拟路径可被 Godot 以 `Texture2D` 正常加载。

---

## 4. 特性（Attribute）清单

> 全部位于 `STS2RitsuLib.Interop.AutoRegistration`。除 `RitsuLibOwnedBy` 外所有特性均为 `AutoRegistrationAttribute` 派生；`AttributeUsage` 均为 `Class`、`AllowMultiple=true`、`Inherited=false`；全部在**类型发现管线**（`LocManager.Initialize` 前缀，模组加载后一次性）被消费。基类 `AutoRegistrationAttribute` 提供通用属性：`Order`（同阶段局部排序，越小越先）、`Inherit`（基类型声明时设为 true 可应用到具体派生类型；同一逻辑"继承槽位"取最近声明，直接声明可替换继承的池/数量/路径/阈值，不同目标 ID/作用域的注册仍累加，同一类型同一槽位重复声明会抛错）。

### 4.1 通用（基类/标记）

| 特性 | 参数 | 触发时机/作用 |
|---|---|---|
| `AutoRegistrationAttribute`（抽象基类） | `Order`、`Inherit` | 管线识别所有注册特性的统一基类 |
| `ContentRegistrationAttribute`（抽象基类） | 无 | 标记：经 `ModContentRegistry` 分发的内容注册基类 |

### 4.2 内容注册（Content，除注明外 Phase=ContentPrimary）

| 特性 | 参数 | 触发时机/作用 |
|---|---|---|
| `RegisterCharacter` | 无 | 标注角色模型类（`CharacterModel` 子类）；注册进 `ModContentRegistry`，提供依赖键 `RegisterCharacter:{类型}`（起始/Act 注册依赖它）。用法：`[RegisterCharacter] class MyChar : CharacterModel` |
| `RegisterAct` | 无 | 标注 `ActModel` 子类；提供键 `RegisterAct:{类型}` |
| `RegisterMonster` | 无 | 标注怪物模型类 |
| `RegisterPower` | 无 | 标注能力模型类。用法：`[RegisterPower] class MyPower : PowerModel` |
| `RegisterOrb` | 无 | 标注充能球模型类 |
| `RegisterEnchantment` | 无 | 标注附魔模型类 |
| `RegisterAffliction` | 无 | 标注侵蚀模型类 |
| `RegisterAchievement` | 无 | 标注成就模型类 |
| `RegisterSingleton` | 无 | 标注单例模型类 |
| `RegisterModelCapability` | `StableEntryStem?`、`FullPublicEntry?`（settable） | 标注 `IModelCapability` 实现；注册能力并提供 `RegisterType:{类型}` 键。`StableEntryStem` 与 `FullPublicEntry` 不能同时设置，默认由类型名派生公共条目 |
| `RegisterDefaultModelCapability` | `Type targetModelType`；`ModifierId?`（settable） | Phase=ContentSecondary；把标注的能力类型加入目标模型实例的默认能力集合；`ModifierId` 缺省由 `目标类型名_能力类型名` 派生 |
| `RegisterGoodModifier` | `ModifierListSortOrder`（负值插到正面列表区段前，非负插后） | 注册为正面每日特效 |
| `RegisterBadModifier` | `ModifierListSortOrder`（同上，负面列表） | 注册为负面每日特效 |
| `RegisterMutuallyExclusiveModifierGroup` | `params Type[] memberTypes` | 若标注类型本身是具体 `ModifierModel` 则并入组；组内须 ≥2 个类型；注册互斥特效组 |
| `RegisterTrashHeapCard` | 无 | Phase=ContentSecondary；标注 `CardModel` 子类，作为"垃圾堆"事件 Grab 选项候选 |
| `RegisterTrashHeapRelic` | 无 | Phase=ContentSecondary；标注 `RelicModel` 子类，作为"垃圾堆"事件 Dive In 选项候选 |

### 4.3 卡池（Pool）

| 特性 | 参数 | 触发时机/作用 |
|---|---|---|
| `RegisterSharedCardPool` | 无 | 标注共享卡牌池模型；提供键 `RegisterSharedCardPool:{类型}` |
| `RegisterSharedRelicPool` | 无 | 标注共享遗物池模型；提供键 `RegisterSharedRelicPool:{类型}` |
| `RegisterSharedPotionPool` | 无 | 标注共享药水池模型 |
| `RegisterSharedEvent` | 无 | 标注共享事件模型 |
| `RegisterSharedAncient` | 无 | 标注共享先古之民事件模型 |
| `RegisterGlobalEncounter` | 无 | 标注全局遭遇模型 |
| `RegisterCard` | `Type poolType`；`StableEntryStem?`、`FullPublicEntry?` | 标注 `CardModel` 子类并注册进指定卡牌池；提供 `RegisterType:{类型}` 键。用法：`[RegisterCard(typeof(MyCardPool))] class Strike : CardModel` |
| `RegisterRelic` | `Type poolType`；同上两个命名选项 | 标注 `RelicModel` 子类注册进指定遗物池。用法：`[RegisterRelic(typeof(MyRelicPool))] class Relic : RelicModel` |
| `RegisterPotion` | `Type poolType`；同上两个命名选项 | 标注药水模型注册进指定药水池 |

### 4.4 角色起始（Phase=ContentSecondary）

| 特性 | 参数 | 触发时机/作用 |
|---|---|---|
| `RegisterCharacterStarterCard` | `Type characterType, int count = 1` | 标注卡牌作为角色初始卡牌（count 份）；依赖 `RegisterCharacter:{角色}` 与卡牌注册键 |
| `RegisterCharacterStarterRelic` | `Type characterType, int count = 1` | 标注遗物作为角色初始遗物 |
| `RegisterCharacterStarterPotion` | `Type characterType, int count = 1` | 标注药水作为角色初始药水 |

### 4.5 Act 限定（Phase=ContentSecondary）

| 特性 | 参数 | 触发时机/作用 |
|---|---|---|
| `RegisterActEncounter` | `Type actType`（须具体 `ActModel`） | 遭遇注册进指定 Act；依赖 `RegisterAct:{act}` 与类型键 |
| `RegisterActEvent` | `Type actType` | 事件注册进指定 Act |
| `RegisterActAncient` | `Type actType` | 先古之民事件注册进指定 Act |

### 4.6 关键词/标签

| 特性 | 参数 | 触发时机/作用 |
|---|---|---|
| `RegisterOwnedKeyword` | `string localKeywordStem`；可设 `TitleTable`(默认"card_keywords")、`TitleKey?`、`DescriptionTable?`、`DescriptionKey?`、`IconPath?`、`CardDescriptionPlacement`(默认 None)、`IncludeInCardHoverTip`(默认 true) | Phase=Keywords；经 `ModKeywordRegistry.RegisterOwned` 注册模组归属关键词 |
| `RegisterOwnedCardKeyword` | `string localKeywordStem`；`IconPath?`、`CardDescriptionPlacement`、`IncludeInCardHoverTip` | Phase=Keywords；按游戏卡牌关键词本地化约定注册 |
| `RegisterOwnedCardTag` | `string localCardTagStem` | Phase=CardTags；经 `GetQualifiedCardTagId` 组合 modId 注册自定义 `CardTag` ID |

### 4.7 纪元/剧情与时间线布局

| 特性 | 参数 | 触发时机/作用 |
|---|---|---|
| `RegisterEpoch` | 无 | Phase=Timeline；标注 `EpochModel` 注册时间线纪元，提供键 `RegisterEpoch:{类型}` |
| `RegisterStory` | 无 | Phase=Timeline；标注 `StoryModel` 注册故事，提供键 `RegisterStory:{类型}` |
| `RegisterStoryEpoch` | `Type storyType` | Phase=Timeline；把标注纪元加入指定故事 |
| `AutoTimelineSlot` | `EpochEra era` | Phase=TimelineLayout；标注 `ModEpochTemplate`，放入该 era 时间线列第一个空位 |
| `AutoTimelineSlotBeforeColumn` | `EpochEra anchorEra` | 放入锚点 era 之前最近的空闲列 |
| `AutoTimelineSlotBeforeEpochColumn` | `Type referenceEpochType`（须具体 `EpochModel`） | 放入参考纪元所在列之前最近空闲列 |
| `AutoTimelineSlotAfterColumn` | `EpochEra anchorEra` | 锚点 era 之后最近空闲列 |
| `AutoTimelineSlotAfterEpochColumn` | `Type referenceEpochType` | 参考纪元列之后 |
| `AutoTimelineSlotInColumn` | `EpochEra anchorEra` | 放入锚点 era 所在列 |
| `AutoTimelineSlotInEpochColumn` | `Type referenceEpochType` | 与参考纪元共享列 |

（6 个 `AutoTimelineSlot*` 共用一个排他继承槽位，同一类型只能声明其一。）

### 4.8 纪元解锁内容/门控（Phase=TimelineLayout）

| 特性 | 参数 | 触发时机/作用 |
|---|---|---|
| `RegisterEpochCards` | `params Type[] cardTypes`（须具体 `CardModel`） | 标注 `EpochModel`；为纪元注册显式卡牌解锁内容并以此纪元为门控 |
| `RequireAllCardsInPool` | `Type poolType`（须 `CardPoolModel`） | 标注纪元；要求该池每张已注册卡牌都以此纪元为解锁前提 |
| `RegisterEpochRelicsFromPool` | `Type poolType`（须 `RelicPoolModel`） | 标注纪元；把池中全部遗物注册为纪元解锁内容并门控 |

### 4.9 Ancient 映射（Phase=AncientMappings）

| 特性 | 参数 | 触发时机/作用 |
|---|---|---|
| `RegisterArchaicToothTranscendence` | `Type ancientCardType` | 标注起始卡牌（`CardModel`）→ "古老牙齿"超越成先古卡牌 |
| `RegisterDustyTomeCard` | `Type characterType` | 标注先古卡牌作为指定角色的"尘封魔典"优先候选 |
| `RegisterTouchOfOrobasRefinement` | `Type upgradedRelicType` | 标注起始遗物（`RelicModel`）→ "欧洛巴斯之触"精炼成的升级遗物 |

### 4.10 解锁（Phase=Unlocks；均标注在角色类上，依赖 `RegisterType:{角色}` 与 `RegisterEpoch:{纪元}`）

| 特性 | 参数 | 触发时机/作用 |
|---|---|---|
| `UnlockEpochAfterRunAs` | `Type epochType` | 用该角色完成任意一局后揭示纪元 |
| `UnlockEpochAfterWinAs` | `Type epochType` | 用该角色通关后揭示纪元 |
| `UnlockEpochAfterAscensionWin` | `Type epochType, int ascensionLevel` | 该角色达到 ≥ 指定进阶通关后揭示 |
| `UnlockEpochAfterEliteVictories` | `Type epochType, int requiredEliteWins = 15` | 该角色累计击败指定数量精英后揭示 |
| `UnlockEpochAfterBossVictories` | `Type epochType, int requiredBossWins = 15` | 该角色累计击败指定数量 Boss 后揭示 |
| `UnlockEpochAfterAscensionOneWin` | `Type epochType` | 该角色通关进阶 1 后揭示 |
| `RevealAscensionAfterEpoch` | `Type epochType` | 纪元揭示后显示该角色进阶界面 |
| `UnlockCharacterAfterRunAs` | `Type epochType` | 通过局后角色解锁检查授予纪元 |

### 4.11 TopBar（Phase=TopBarButtons）

| 特性 | 参数 | 触发时机/作用 |
|---|---|---|
| `RegisterOwnedTopBarButton` | `string localButtonStem`；`IconPath?`、`ButtonOrder`、`OffsetX/OffsetY` | 标注类**必须实现 `IModTopBarButtonHandler` 且有公共无参构造**；RitsuLib 反射建实例并把 `OnClick/IsVisible/IsOpen/GetCount` 接入 `ModTopBarButtonSpec`；悬停提示用 `static_hover_tips` 的 `"{id}.title/.description"` |

### 4.12 CardPiles（Phase=CardPiles）

| 特性 | 参数 | 触发时机/作用 |
|---|---|---|
| `RegisterOwnedCardPile` | `string localPileStem`；`Scope`(默认 CombatOnly)、`Style`(默认 Headless)、`AnchorKind`(默认 StyleDefault)、`AnchorOffsetX/Y`、`AnchorCustomX/Y`、`AnchorCustomPivotX/Y`、`IconPath?`、`Hotkeys?`、`CardShouldBeVisible`、`ExtraHandDirection`(默认 VanillaHand)、`ExtraHandSpacing`(110)、`ExtraHandCardScale`(0.65)、`ExtraHandHoverScale`(1)、`ExtraHandShowPlayableGlow`(true)、`ExtraHandAllowCardPlay`(true)、`HoverTipOffsetX/Y`、`HoverTipPlacement` | 标注类**可实现 `IModCardPileHandler`**（无参构造实例的 `OnOpen` 接入 `ModCardPileSpec`）；本地化键 `"{id}.title/.description/.empty"`（`static_hover_tips.json`） |

### 4.13 NodeAttachments（Phase=NodeAttachments；三个特性共用按 父类型+localId 的作用域槽位）

| 特性 | 参数 | 触发时机/作用 |
|---|---|---|
| `RegisterNodeAttachment` | `Type parentType, string localId`；`NodeType?`（缺省=标注类型须为子节点类型）；基类属性：`NodeName?`、`UniqueNameInOwner`、`IncludeDerivedParentTypes`(true)、`DuplicatePolicy`(AllowDuplicateName)、`AddMode`(AddChildSafely)、`SetupTiming`(BeforeAdd)、`ChildIndex`(-1)、`InsertBeforeName?`、`InsertAfterName?`、`QueueFreeReplacedNode`(true) | 在父节点 `_Ready` 生命周期声明式挂载子节点；标注类型可为 `INodeAttachmentFactory`/`INodeAttachmentSetup`/节点本身 |
| `RegisterNodeAttachmentFromScene` | `Type parentType, string localId, string scenePath`；`NodeType?` | 直接从 `PackedScene` 实例化（`ResourceLoader.Load`）后挂载 |
| `RegisterNodeAttachmentFromConvertedScene` | `Type parentType, string localId, string scenePath`；`NodeType?` | 经 `RitsuGodotNodeFactories.CreateFromScenePath<T>` 加载并转换场景后挂载 |

### 4.14 SmartFormat（Phase=Localization）

| 特性 | 参数 | 触发时机/作用 |
|---|---|---|
| `RegisterSmartFormatter` | 无 | 标注类须为具体 `IFormatter`；经 `ModSmartFormatExtensionRegistry.RegisterFormatterType` 注册到游戏本地化 |
| `RegisterSmartFormatSource` | 无 | 标注类须为具体 `ISource`；`RegisterSourceType` 注册选择器源 |

### 4.15 所有权标记

| 特性 | 参数 | 触发时机/作用 |
|---|---|---|
| `RitsuLibOwnedBy` | `string modId`（空白抛异常） | **非** `AutoRegistrationAttribute`（普通 `Attribute`，Inherited=false）；覆盖标注类型上声明的自动注册特性的归属 modId（含经 `Inherit` 继承的注册）；由 `ResolveOwnerModId` 消费，优先级高于游戏/集线器解析 |

---

## 5. 关键用法模式

### 5.1 Entry 初始化

- **RitsuLib 自身加载**（`components/loader/Bootstrap.cs`，非框架本体）：`Bootstrap` 标注 `[ModInitializer(nameof(Initialize))]`，`Initialize()` 按主机版本从 `lib/<compat>/` 选取并装载匹配的 `STS2-RitsuLib.dll` 变体（manifest + SHA256 校验），装入默认 ALC 并关联到 mod，最后反射调用真正的 `RitsuLibFramework.Initialize()`。
- **mod 作者入口**：静态类标注 `[ModInitializer(nameof(Initialize))]`，在 `Initialize()` 中调用 `RitsuLibFramework.Initialize()`（幂等，`IsInitialized` 去重）。框架会安装 Harmony 补丁、初始化设置/搜索/遥测等服务、注册内置类型发现贡献器（`ModInteropTypeDiscoveryContributor`、`SavedAttachedStateTypeDiscoveryContributor`、`AttributeAutoRegistrationTypeDiscoveryContributor`）。
- 若宿主未提供程序集关联或需显式声明，可调用 `ModTypeDiscoveryHub.RegisterModAssembly(modId, assembly)`（须在 `LocManager.Initialize` 之前）；自定义发现贡献器用 `ModTypeDiscoveryHub.RegisterContributor(...)` 注册。

### 5.2 注册流程（特性自动注册）

- **触发点**：`ModTypeDiscoveryPatch`（Harmony Prefix 打在 `LocManager.Initialize` 上，与 BaseLib 同时机，加锁只跑一次）→ `ModTypeDiscoveryHub.RunOnce(harmony)` → `RitsuLibFramework.FlushDeferredContentPacks`。
- **发现**：快照 `RegisteredAssembliesByModId` → 对每个程序集的每个可加载类型（按 FullName 排序）依次调用每个贡献器的 `Contribute`。
- **特性扫描执行**（`AttributeAutoRegistrationTypeDiscoveryContributor`）：沿基类链收集 `AutoRegistrationAttribute`（仅 `Inherit=true` 生效，按继承槽位取最近声明）；`ResolveOwnerModId` 解析归属（`[RitsuLibOwnedBy]` 覆盖 → 游戏已加载 modId → `ModTypeDiscoveryHub` 注册 modId → manifest）；生成 `AutoRegistrationOperation(OwnerModId, SourceAssembly, SourceType, Phase, Order, Signature, AttributeName, Execute, Dependencies, ProvidedKeys)`；`Execute` 最终调用显式注册表 API（`ModContentRegistry`/`ModKeywordRegistry`/`ModCardTagRegistry`/`ModCardPileRegistry`/`ModTopBarButtonRegistry`/`ModNodeAttachmentRegistry`/`ModTimelineLayoutRegistry`/`ModSmartFormatExtensionRegistry`/Timeline/Unlock 注册表）。
- **排序与依赖**：先按 (modId, 程序集, Phase, Order, 类型, Signature) 稳定排序；再按依赖键（`RegisterType:{AssemblyQualifiedName}`、`RegisterCharacter:{类型}`、`RegisterEpoch:{类型}`、`RegisterShared*Pool:{类型}` 等 ProvidedKeys）拓扑排序（成环回退稳定排序并告警）；逐个 `Execute()`，单操作失败计入诊断并继续。
- **阶段顺序**（`AutoRegistrationPhase`）：`ContentPrimary(0) → ContentSecondary(1) → AncientMappings(2) → Keywords(3) → CardTags(4) → CardPiles(5) → TopBarButtons(6) → NodeAttachments(7) → TimelineLayout(8) → Timeline(9) → Unlocks(10) → Localization(11)`。
- **结论**：mod 作者**无需手动调用任何注册 API**——`[RegisterCard(typeof(MyPool))]`、`[RegisterPower]` 等标注会在 mod 加载后自动扫描执行；`[RitsuLibOwnedBy]` 仅在 manifestId 与运行 id 不一致时用于钉住归属。

### 5.3 生命周期订阅

- **`RitsuLibFramework.SubscribeLifecycle(ILifecycleObserver observer, bool replayCurrentState = true)`** — 订阅框架生命周期事件观察者（经 `OnEvent` 接收），返回 `IDisposable`，释放即退订；`replayCurrentState=true` 时按发生时间补发已发生的可回放事件。
- **`RitsuLibFramework.SubscribeLifecycle<TEvent>(Action<TEvent> handler, bool replayCurrentState = true)`** — 类型化回调订阅（另有带谓词/缓冲的重载）。
- 事件为 `STS2RitsuLib.Lifecycle` 命名空间的 record struct，例如：战斗 `CombatStartingEvent`/`CombatEndedEvent`/`CombatVictoryEvent`/`SideTurnStartingEvent`/`SideTurnEndedEvent`/`CardPlayingEvent`/`CardPlayedEvent`/`CardDrawnEvent`/`CreatureDyingEvent`/`CreatureDiedEvent`；房间/Act `RoomEnteringEvent`/`RoomEnteredEvent`/`RoomExitedEvent`/`ActEnteringEvent`/`ActEnteredEvent`；奖励 `GoldGainedEvent`/`RelicObtainedEvent`/`RewardTakenEvent`；存档 `ProfileIdInitializedEvent`/`ProfileSwitchedEvent`/`RunSavingEvent`/`RunSavedEvent`；以及 `AttackStartingEvent`/`BlockGainingEvent`/`EnergySpentEvent`/`PlayerTurnStartedEvent` 等附加钩子事件。

### 5.4 战斗钩子通用模式（本手册 Combat 各节）

- **「接口 + 自动发现」型钩子**（攻击命中、治疗、玩家资源、生命条预测/视觉扩展、手牌上限、次级资源）：模型/能力实现对应 `I*HookListener`/`I*Source`/`I*Modifier` 接口即被 `ModelHookListenerDispatcher.FromCombat`（或 `Creature.Powers`）自动收集，**无需注册**；进程级全局监听用各 `RegisterGlobalListener(...)`（仅用于非模型效果）。
- **「注册表」型 API**（右键、自定义奖励、生命条全局来源、次级资源定义）：用 `Register<TModel>(modId, localStem, ...)` / `For(modId).RegisterOwned(...)` / `Register(modId, sourceId, source)` 显式注册；多数返回 `IDisposable` 句柄，`Dispose()` 即注销；重复注册同 ID 一般抛异常或仅替换 factory。
- **ID 约定**：模组限定 ID 统一经 `ModContentRegistry` 复合 ID 生成（如 `mymod_richtext_glitch`、`MODID_REWARD_LOCAL`、`SECONDARY_RESOURCE` 段），保证跨 mod 确定性且不冲突。
- **同步/多人**：右键等交互通过 `RitsuLibManagedNetActions.Request` 作为同步行动分发，执行端重新解析模型并跑守卫，异常不传播（记日志继续）。

---

*本文档由 RitsuLib r7 源码（src/Combat、src/Interactions、src/Ui + Interop.AutoRegistration 特性）自动整理生成。*
