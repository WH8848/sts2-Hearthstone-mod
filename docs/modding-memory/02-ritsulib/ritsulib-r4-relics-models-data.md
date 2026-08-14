# RitsuLib (STS2) 公共 API 整理 — Relics / Models / Data

> 来源：`E:\MOD\sts2\STS2-RitsuLib\src\` 下 `Relics`、`Models`、`Data` 三个目录（另含驱动它们的 `Interop\AutoRegistration` 特性定义）。
> 命名空间统一为 `STS2RitsuLib.*`。只列对 mod 制作有实用价值的内容。

---

## 一、Relics（遗物联动与可见性）

### 公共 API 清单

| 类型 | 命名空间 | 用途 | 关键成员 |
|---|---|---|---|
| `IModRelicVisibility` | `STS2RitsuLib.Relics.Visibility` | 遗物模型实现此接口以控制是否生成常规遗物 UI | `bool IsRelicVisible { get; }` |
| `ModRelicVisibilityRegistry` | `STS2RitsuLib.Relics.Visibility` | 运行时遗物可见性规则注册表 | `static IDisposable Register(string modId, Func<RelicModel,bool> isVisible)`（返回 `false` 隐藏该遗物；释放句柄即注销）；`static bool IsVisible(RelicModel relic)` |

- 注册的可见性规则对所有遗物生效；实现 `IModRelicVisibility` 的模型（`IsRelicVisible == false`）也会被隐藏。
- 隐藏效果覆盖：遗物库存 `NRelicInventory`（Add/Remove/动画）、悬停提示、`NRelicBasicHolder`、多人远程获得/移除动画、多人展开检查界面等（内部 Harmony 补丁实现，mod 无需关心）。

### 内部注册器（mod 不用直接调用，由特性驱动）

| 内部类型 | 作用 | 对应特性 |
|---|---|---|
| `DustyTomeCardRegistry` | 尘封之书（Dusty Tome）按角色注册先古卡牌候选，优先级高于原版已解锁候选；仅接受 `CardRarity.Ancient` 卡牌 | `[RegisterDustyTomeCard(characterType)]` |
| `OrobasAncientUpgradeRegistry` | 古老牙齿（Archaic Tooth）超越映射 + 欧洛巴斯之触（Touch of Orobas）精炼映射；同 key 重复注册会替换并告警 | `[RegisterArchaicToothTranscendence(ancientCardType)]`、`[RegisterTouchOfOrobasRefinement(upgradedRelicType)]` |

- 这些注册器以 CLR `Type` 保存目标，运行时才经 `ModelDb.GetByIdOrNull<T>` 解析，因此**可在模组 `Apply()` 中、ModelDb 加入 mod 内容之前注册**。
- 补丁行为要点（mod 可预期）：
  - `DustyTome.SetupForPlayer`：优先用注册候选 → 原版已解锁 Ancient → 锁定池 Ancient（避免设置失败）。
  - `ArchaicTooth.TranscendenceCards`：追加模组注册的先古卡牌模板（去重）。
  - `ArchaicTooth.GetTranscendenceStarterCard`：原版找不到起始卡时，在牌组中查找注册的起始卡。
  - `ArchaicTooth.GetTranscendenceTransformedCard`：按模板生成卡牌，继承原卡的升级次数与附魔（`CardCmd.Upgrade` / `CardCmd.Enchant`）。
  - `TouchOfOrobas.GetUpgradedStarterRelic`：`[HarmonyPriority(First)]` 优先应用模组精炼映射。

---

## 二、Models（模型基类、模型能力、保存数据）

### 2.1 命名空间 `STS2RitsuLib.Models`

| 类型 | 用途 | 关键成员 |
|---|---|---|
| `HookedSingletonModel`（abstract，: `SingletonModel`） | 可自行订阅局内/战斗钩子流的单例基类，免去反射式钩子注册 | 构造 `HookedSingletonModel(HookType)`；`enum HookType { None, Combat, Run }`；`bool ShouldReceiveCombatHooks`（override）；`protected IRunState? CurrentRunState`、`protected CombatState? CurrentCombatState` |
| `ModelCloneContext`（readonly record struct） | 一次完成的模型复制操作描述 | `(AbstractModel Prototype, AbstractModel ClonedModel)` |
| `ModelCloneRegistry`（sealed） | 按 mod 分组的模型复制监听器；每次 `AbstractModel.MutableClone` 完成后通知（经补丁挂接） | `static ModelCloneRegistry For(string modId)`；`void Register(string listenerId, Action<ModelCloneContext>)`；`void Register(string listenerId, Func<ModelCloneContext,bool> predicate, Action<ModelCloneContext>)`；`void Register<TModel>(string listenerId, Action<TModel,TModel>) where TModel : AbstractModel`；`bool Unregister(string listenerId)` |
| `ModelLocStringSource`（sealed record） | 已知模型族的 LocString 表/键映射 | `(Type ModelType, string Table, Func<AbstractModel,string> Key, Func<AbstractModel,LocString> Resolve)`；`bool Matches(AbstractModel)`；`LocString CreateDefault(AbstractModel)` |
| `ModelTitleExtensions`（static） | 显示标题解析：已注册解析器优先，其次按已知模型族取表/键 | `static void RegisterTitleResolver<TModel>(Func<TModel,LocString?>)` / `(Type, Func<AbstractModel,LocString?>)`；`static bool UnregisterTitleResolver(...)`；`static bool TryResolveTitle(this AbstractModel, out LocString?)`；`static LocString ResolveTitleOr(this AbstractModel, LocString fallback)`；`static bool TryGetTitleLocStringSource(...)`；`static LocString CreateDefaultTitleLocString(this AbstractModel)`；`static IReadOnlyList<ModelLocStringSource> KnownTitleSources` |

**Identity（多人同步的运行时模型身份）** — 命名空间 `STS2RitsuLib.Models.Identity`：

| 类型 | 用途 |
|---|---|
| `ModModelIdentity`（readonly record struct） | 可变模型进入原版同步所有关系时分配的运行时身份；`static readonly ModModelIdentity None`；`bool IsValid` |
| `ModModelIdentityToken`（readonly record struct） | 带 ModelId 校验的传输令牌：`(ModModelIdentity Identity, ModelId ModelId)`；`bool IsValid` |

- 内部 `ModModelIdentityRegistry` 通过补丁跟踪生命周期：RunState 创建时清空；卡牌/遗物/药水/力量/充能球/附魔/苦痛/怪物进出所有权时注册或注销；`Player.SyncWithSerializedPlayer` 时快照-恢复身份。mod 一般无需直接操作，主要用于多人引用/互操作。

### 2.2 模型能力（Capabilities）— 命名空间 `STS2RitsuLib.Models.Capabilities`

**入口与扩展**

| 类型 | 用途 | 关键成员 |
|---|---|---|
| `ModelCapabilities`（static） | 能力宿主入口；内置持久化槽（key=`model_capabilities`） | `static void EnsureInitialized()`（**初始化时必须先调用**，之后才能修改能力）；`static ModelCapabilitySet Get(AbstractModel)`；`static bool TryGet(AbstractModel, out ModelCapabilitySet)` |
| `ModelCapabilityExtensions`（static） | 模型便捷扩展 | `Capabilities()`、`Capability<TC>()`、`TryGetCapability<TC>()`、`ApplyCapability<TC>(cap, options)`、`AddCapability<TC>(cap, allowMerge=true, isUpgrade=false)`、`SubtractCapability<TC>(cap, isUpgrade=false)`、`GetOrAddCapability<TC>(factory, options)`、`GetOrCreateCapability<TC>(options)`、`GetOrCreateUpgradeCapability<TC>(allowMerge)`、`AddUpgradeCapability<TC>(allowMerge)`、`RemoveCapability<TC>()` |
| `ModelCapabilitySet`（sealed） | 单个模型实例上的能力集合 | `Owner`、`All/Attached`（执行序）、`Count`；`Apply/Add/AddForUpgrade/Subtract/Insert/InsertBefore<T>/InsertAfter<T>/Before<T>/After<T>`、`Remove<TC>()/Remove(id)/Remove(instance)/RemoveAll...`、`Clear(policy)`、`ReplaceAll(...)`、`Get<T>/TryGet/Get(id)/Contains/GetAll`、`GetOrAdd(factory,options)`、`GetOrCreate<TC>(options)`、`GetOrCreateUpgrade<TC>(allowMerge)`、`MarkDirty()` |
| `ModelCapabilityList`（sealed） | 构建默认能力列表用的可变列表（实现 `IModelCapabilitySource` 时使用） | `All`、`Add(instance)`、`Add<TC>()`、`Add(Type)`、`AddFromRegistry<TC>()`、`Insert<TC>(index)`、`InsertBefore<T>/InsertAfter<T>`、`Remove<TC>/RemoveAll<TC>`、`Replace<TC>(replacement)`、`Get<TC>/GetAll<TC>/Contains<TC>` |
| `ModelCapabilityRegistry`（static） | 能力 ID ↔ 工厂注册表 | `Register(string id, Type, Func<IModelCapability>)`、`Register<TC>(string id, Func<TC>)`、`Register<TC>(string id) where TC : new()`、`TryCreate(id, out ...)`、`TryCreate<TC>(out ...)`、`Create(id)`、`Create<TC>()`、`GetCapabilityId(Type/<TC>)`、`TryGetCapabilityType(id, out Type)` |
| `ModelCapabilityDiagnostics`（static） | 冲突诊断日志 | `ModelCapabilityConflictLogMode ConflictLogs { get; set; }`（`Off/WarnOnce/WarnEveryTime`，默认 Off）；`static void ClearConflictLogCache()` |
| `ModelCapabilitySaveDocument` / `ModelCapabilitySaveEntry`（sealed） | 能力持久化文档模型 | `Capabilities: List<ModelCapabilitySaveEntry>`；`Id/Schema/Data(JsonNode)` |

**契约接口**

| 接口 | 用途 | 关键成员 |
|---|---|---|
| `IModelCapability` | 能力基础契约 | `string CapabilityId`、`AbstractModel? Owner`、`Attach(owner, isInternal=false)`、`Detach(isInternal=false)` |
| `IModelCapability<out TModel>` | 类型化契约 | `new TModel? Owner` |
| `IModelCapabilitySource` | 模型类实现后向默认能力列表填充自身能力 | `void BuildDefaultCapabilities(ModelCapabilityList capabilities)` |
| `IModelCapabilityMergeHandler` | 能力合并/减法合并 | `bool TryMergeWith(incoming, options, out merged)`、`bool TrySubtractiveMergeWith(...)` |
| `IModelCapabilityJsonState` | JSON 持久化 | `int SchemaVersion => 1`、`JsonNode? SaveState()`、`void LoadState(JsonNode?, int schemaVersion)` |
| `IModelCapabilityCloneHandler` | 自定义克隆 | `IModelCapability CloneFor(AbstractModel clonedOwner)` |
| `IModelCapabilityCloneNotification` | 所属模型克隆后回调 | `void AfterOwnerCloned(originalOwner, clonedOwner, originalCapability)` |
| `IModelCapabilityHookListener` | 订阅所属模型的原版钩子流（多人同步安全） | `bool ShouldReceiveOwnerHooks => true`；`int OwnerHookOrder => 0`（负数先于所属模型，0/正数在后） |
| `ApplyModelCapabilityOptions`（readonly record struct） | 应用选项 | `(bool AllowMerge=true, bool UseSubtractiveMerge=false, bool IsUpgrade=false, IReadOnlyDictionary<string,object?>? Extra=null)`；`static Upgrade(allowMerge=true, extra=null)` |
| `UnknownModelCapabilityPolicy` / `MissingModelCapabilityAnchorPolicy`（enum） | 未知条目策略 / 锚点缺失策略 | `Preserve/Remove`；`Append/Prepend/Skip/Throw` |

**基类（写自定义能力时继承）**

| 基类 | 说明 |
|---|---|
| `ModelCapability`（abstract，: `AbstractModel`） | 模型化能力基类（有稳定 ModelId，可持久化）。`DynamicVars`（能力自有动态变量）；`virtual string CapabilityId`；`virtual void Attach/Detach`；`virtual IModelCapability CloneFor(clonedOwner)`；`public void Modify(Action<ModelCapability>)`（回调后自动 MarkDirty）；`RemoveFromOwner()`；保护钩子 `OnAttach/OnDetach/OnLoadedFromSave`；`protected virtual IEnumerable<DynamicVar> CanonicalVars`、`protected virtual JsonNode? SaveAdditionalState()`、`LoadAdditionalState(JsonNode?, int)`、`ResetDynamicVarsToCanonical()` |
| `ModelCapability<TModel>` | 仅可附加到 `TModel` 的类型化基类；`new TModel? Owner`；类型不符抛异常；`OnAttach/OnDetach/OnLoadedFromSave(TModel)` |
| `StatefulModelCapability<TState>` / `StatefulModelCapability<TModel,TState>` | 带类型化 JSON 状态（`TState : class, new()`）；`protected TState State`；`SetState(TState)`、`MutateState(Action<TState>)`、`virtual TState ReadState(JsonNode?, int schemaVersion)`（可做版本迁移） |
| `OwnerHookCapability<TModel>` | 自动订阅所属模型原版钩子；`virtual bool ShouldReceiveOwnerHooks`、`virtual int OwnerHookOrder` |
| `CardCapability` | 卡牌能力基类：`OnOwnerCardUpgraded/OnOwnerCardUpgradeFinalized/OnOwnerCardDowngraded/OnOwnerCardTransformedFrom/OnOwnerCardTransformedTo(CardModel)` |
| `CapabilityCardModel`（abstract，: `CardModel, IModelCapabilitySource`） | 卡牌基类，自带默认能力钩子 `protected virtual void BuildDefaultCapabilities(ModelCapabilityList)` |
| `RelicCapability` / `PotionCapability` / `PowerCapability` / `EnchantmentCapability` / `AfflictionCapability` / `MonsterCapability` / `CharacterCapability` | 按所属模型族分的空基类（`CharacterCapability : ModelCapability<CharacterModel>`，无 OwnerHook） |
| `OrbCapability` | 充能球能力：`OnOwnerOrbPassiveTriggered(OrbPassiveTriggerContext)`、`OnOwnerOrbEvoked(OrbEvokeContext)`、`OnOwnerOrbBeforeTurnEndTriggered(...)`、`OnOwnerOrbAfterTurnStartTriggered(...)`（均可异步） |
| `CardPlayCapability`（: `CardCapability, ICardOnPlayHookListener`） | 卡牌打出能力：`abstract Task OnOwnerCardPlayed(choiceContext, cardPlay)`；`virtual Task<bool> BeforeOwnerCardOnPlay(...)`（返回 true 阻止原 OnPlay）；`virtual bool ShouldHandleCardPlay(cardPlay)` |
| `OneShotCardPlayCapability` | 打出一次后自动 `RemoveFromOwner()`；`abstract Task OnOwnerCardPlayedOnce(...)` |
| `UntilCombatEndCapability<TModel>` | 战斗结束后自动移除；`virtual Task OnCombatEnded(CombatRoom)` |
| `TurnLimitedCapability<TModel>` | 回合计数（持久化 `remainingTurns`），归零自动移除；`int RemainingTurns`、`SetRemainingTurns(int)`、`ShouldTickTurnLimit(...)`、`OnTurnLimitTicked(...)`、`OnTurnLimitExpired(...)` |

**显示/交互贡献接口（能力实现后自动接入对应 UI 面）**

- 通用：`IModelDynamicVarContributor`（能力自有动态变量，`string? LocStringVariableScope`、`DynamicVarSet GetDynamicVars(model)`；文本用 `{Capabilities.Scope.Variable}` 寻址）、`IModelHoverTipContributor` / `IModelHoverTipContributor<in TModel>`、`IModelAssetPathContributor` / `<in TModel>`、`ModelAssetPathScope { General, Run, Combat, Map, CharacterSelect }`、`ModelAssetPathContext`、`ModelCapabilityDynamicVarNames`（`RootName="Capabilities"`、`GetScopeName/GetVariableName`）。
- 卡牌：`ICardDescriptionContributor`（`CardDescriptionContext/Fragment/Placement{BeforeBase,AfterBase}`）、`ICardHoverTipContributor`、`ICardOverlayContributor`（`CardOverlayContext/Contribution`：`FromScenePath/FromScene/FromFactory`，`Order` 越大越上层，`FullRect` 默认 true）、`ICardOverlayAssetPathContributor`、`ICardTitleContributor`（`CardTitleContext/Fragment/Placement{BeforeBase,ReplaceBase,AfterBase,AfterTitle}`，ReplaceBase 首个胜出）、`ICustomTypeTextCard` / `ICardTypeTextModifier`（类型文本，含 `{Type}` 包裹契约，与 BaseLib 兼容）、`ICardGlowContributor`（金/红发光）、`ICardPropertyContributor`（`GetCardType/GetCardRarity/GetTargetType` 首个非空胜出 + `GetTags` 追加去重）、`ICardEnergyCostContributor`（`ModifyEnergyCost(card, currentCost, CostModifiers)`，仅 Local 费用面）、`ICardStarCostContributor`（返回负数隐藏）、`ICardPlayStateContributor`（`CanPlay` 后者覆盖前者；`HasTurnEndInHandEffect` 或合并）、`ICardPlayResultContributor`（打出后目标牌堆）、`ICardTransformCarryOverCapability`（转化时携带能力到新卡）。
- 充能球：`IOrbValueDisplayContributor`（`GetValueDisplayMode/GetPassiveValueDisplayText/GetEvokeValueDisplayText`）、`IOrbHoverTipDescriptionContributor`（`OrbHoverTipDescriptionContext/Fragment/Placement{BeforeBase,AfterBase}`）。
- 右键：`IModelRightClickCapability`（`RightClickPriority`、`RightClickRunMode{Exclusive,Continue}`、`CanHandleRightClickLocal`、`CanExecuteRightClick`、`Task OnRightClick(ctx)`）——同步多人右键交互。

**钩子监听分发**

- `ModelHookListener<TListener>`（readonly record struct：`(TListener Listener, AbstractModel? Model)`）。
- `ModelHookListenerDispatcher`（static）：`FromCombat<TListener>(combatState, params extraModels)`、`FromCombatWithAdapters<TListener>(combatState, adapterResolver, ...)`、`FromRun<TListener>(runState, combatState?, ...)`、`FromModels<TListener>(models, ...)`——从钩子模型 + 已附加能力（+ 全局监听器 + 适配器）解析监听器并去重。

### 2.3 模型保存数据（ModelSavedData）— 命名空间 `STS2RitsuLib.Models.Capabilities`

| 类型 | 用途 | 关键成员 |
|---|---|---|
| `ModelSavedDataStore`（sealed） | 按 mod 的保存槽注册表 | `static ModelSavedDataStore For(string modId)`；`ModelSavedData<TTarget,TPayload> Register<TTarget,TPayload>(string key, Func<TPayload>? defaultFactory=null, ModelSavedDataOptions? options=null)`；`void RegisterComputed<TTarget,TPayload>(key, exporter, importer, defaultFactory=null, options=null)`（值直接从模型导出/导入） |
| `ModelSavedData<TTarget,TPayload>`（sealed） | 类型化槽位句柄（`TTarget : AbstractModel`，`TPayload : class, new()`） | `TPayload Get(model)`、`bool TryGet(model, out value)`、`Set(model, value)`、`MarkDirty(model)`、`bool Remove(model)`、`TPayload Modify(model, Action<TPayload> mutate)` |
| `ModelSavedDataOptions`（sealed） | 槽位配置 | `int SchemaVersion=1`、`ModelSavedDataWritePolicy WritePolicy`、`ModelSavedDataClonePolicy ClonePolicy`、`IReadOnlyList<IMigration>? Migrations` |
| `ModelSavedDataWritePolicy`（enum） | 写入时机 | `WhenSet`（仅显式修改后）、`WhenNonDefault`（不同于默认值）、`AlwaysWhenPresent` |
| `ModelSavedDataClonePolicy`（enum） | 模型克隆时行为 | `Copy`（经序列化深拷贝）、`Drop`（不复制）、`Share`（共享同一内存值） |

- 注册必须在模组初始化阶段完成（注册后冻结，`FinalizeRegistration` 之后再注册会抛异常）。
- 数据随 `AbstractModel.MutableClone` 自动按 `ClonePolicy` 处理；随存档保存/读档自动导入导出（内部文档格式 `model_saved_data`）。

---

## 三、Data（持久化数据）

### 公共 API 清单

| 类型 | 命名空间 | 用途 | 关键成员 |
|---|---|---|---|
| `ModDataStore`（class） | `STS2RitsuLib.Data` | 模组持久化/内存数据的按 key 注册与访问 | `static ModDataStore For(string modId)`；`void Register<T>(string key, string fileName, SaveScope scope, Func<T>? defaultFactory=null, bool autoCreateIfMissing=false, ModDataMigrationConfig? migrationConfig=null, IEnumerable<IMigration>? migrations=null) where T : class, new()`（另有 `bool syncToCloud`、`Func<StorageContext> contextProvider` 重载）；`T Get<T>(key)`；`void Modify<T>(key, Action<T>)`；`void Save(key)` / `void SaveAll()`；`bool HasExistingData(key)`；`ModDataStoreCache<T> CreateCache<T>(key)`；`IDisposable BeginRegistrationScope(bool initializeProfileIfReady=true)`；`bool ReloadIfPathChanged()`；`void InitializeGlobal()` / `void InitializeProfileScoped()`；`string ModId` |
| `ModDataStoreCache<T>`（sealed, `IDisposable`） | 单 key 惰性缓存；条目重载或档案失效时自动失效 | `T Value`、`bool HasValue`、`void Invalidate()`、`T Refresh()`、`void Modify(Action<T>)`、`void Save()`、`string Key` |
| `RitsuLibSettings`（sealed） | RitsuLib 全局 JSON 设置模型（示范如何做全局设置） | `const int CurrentSchemaVersion = 17`；`int SchemaVersion`（`JsonPropertyName(ModDataVersion.SchemaVersionProperty)`）+ 大量 `[JsonPropertyName(...)]` 设置项（调试兼容开关、开发者工具、快捷键、Toast、更新检查、PNG 导出、UI 主题、模型来源悬停提示等） |

- `SaveScope` 来自 `STS2RitsuLib.Utils.Persistence`：`Global` / `Profile` / `InMemory`（`InMemory` 不落盘，也无云同步）。
- 迁移：`IMigration`（`STS2RitsuLib.Utils.Persistence.Migration`）实现 `int FromVersion`、`int ToVersion`、`bool Migrate(JsonObject data)`；`ModDataMigrationConfig` 提供 `CurrentDataVersion` / `MinimumSupportedDataVersion` / `SchemaVersionProperty`。参考 `src\Data\Migrations\RitsuLibSettingsV*ToV*Migration.cs`（V0→V17 共 16 步链式迁移）。
- `ModelDbDeterministicSortMode`（internal enum：`Disabled/Auto/Force`，对应设置字符串 `off/auto/force`）。

---

## 四、特性（Attribute）清单

> 定义于 `STS2RitsuLib.Interop.AutoRegistration`（`RegistrationAttributes*.cs`）。
> 全部为 `[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]`。
> 触发时机：RitsuLib 自动注册管线在**模组初始化阶段**扫描处理；注册在初始化后"冻结"，冻结时校验缺失的模型类型/ID 并告警。
> 公共基类属性：`int Order`（同阶段内局部排序，小者先跑）、`bool Inherit`（声明在基类型上时可被派生类型继承，最近声明覆盖同名槽位配置）。

### 4.1 内容注册（ContentRegistration，经 ModContentRegistry 分发）

| 特性 | 参数 | 作用 |
|---|---|---|
| `[RegisterCharacter]` / `[RegisterAct]` / `[RegisterMonster]` / `[RegisterPower]` / `[RegisterOrb]` / `[RegisterEnchantment]` / `[RegisterAffliction]` / `[RegisterAchievement]` / `[RegisterSingleton]` | 无 | 将标注类型注册为对应模型 |
| `[RegisterModelCapability]` | `string? StableEntryStem`、`string? FullPublicEntry` | 将标注类型注册为模型能力（经 ModelDb 创建，`CapabilityId` 取注册 ID） |
| `[RegisterDefaultModelCapability(Type targetModelType)]` | `string? ModifierId` | 把标注能力类型加入目标模型类型的默认能力集合 |
| `[RegisterCard(Type poolType)]` / `[RegisterRelic(Type poolType)]` / `[RegisterPotion(Type poolType)]` | 池类型 + `string? StableEntryStem` / `string? FullPublicEntry` | 注册进指定池；`StableEntryStem`/`FullPublicEntry` 用于稳定公共条目命名 |
| `[RegisterTrashHeapCard]` / `[RegisterTrashHeapRelic]` | 无 | 注册为"垃圾堆"事件选项候选 |
| `[RegisterCharacterStarterCard(Type characterType, int count=1)]` / `[RegisterCharacterStarterRelic(...)]` / `[RegisterCharacterStarterPotion(...)]` | 角色类型 + 份数 | 角色初始内容 |
| `[RegisterActEncounter(Type actType)]` / `[RegisterActEvent(Type actType)]` / `[RegisterActAncient(Type actType)]` | 阶段类型 | 按阶段注册遭遇/事件/先古事件 |
| `[RegisterGoodModifier]` / `[RegisterBadModifier]` | `int ModifierListSortOrder`（负值插到列表段前，非负插后） | 每日特效注册 |
| `[RegisterMutuallyExclusiveModifierGroup(params Type[] memberTypes)]` | 组内其他特效类型 | 互斥特效组 |
| `[RegisterSharedCardPool]` / `[RegisterSharedRelicPool]` / `[RegisterSharedPotionPool]` / `[RegisterSharedEvent]` / `[RegisterSharedAncient]` / `[RegisterGlobalEncounter]` | 无 | 共享池 / 共享事件 / 全局遭遇 |

### 4.2 遗物联动（驱动 Relics 目录的内部注册器）

| 特性 | 参数 | 触发时机 |
|---|---|---|
| `[RegisterArchaicToothTranscendence(Type ancientCardType)]` | 先古卡牌类型 | 古老牙齿把标注卡牌（作为起始卡）超越为指定先古卡；`TranscendenceCards` 也会追加该模板 |
| `[RegisterDustyTomeCard(Type characterType)]` | 角色类型 | 尘封之书为指定角色选择 Ancient 候选时优先取标注卡牌 |
| `[RegisterTouchOfOrobasRefinement(Type upgradedRelicType)]` | 升级后遗物类型 | 欧洛巴斯之触把标注遗物（作为初始遗物）精炼为指定遗物 |

### 4.3 关键词 / 标签

| 特性 | 参数 | 说明 |
|---|---|---|
| `[RegisterOwnedKeyword(string localKeywordStem)]` | 词干 + `TitleTable`（默认 `card_keywords`）/`TitleKey`/`DescriptionTable`/`DescriptionKey`/`IconPath`/`CardDescriptionPlacement`/`IncludeInCardHoverTip`（默认 true） | 注册 mod 自有关键词 |
| `[RegisterOwnedCardKeyword(string localKeywordStem)]` | 同上（按游戏卡牌关键词本地化约定） | 注册 mod 自有卡牌关键词 |
| `[RegisterOwnedCardTag(string localCardTagStem)]` | 标签词干 | 注册 mod 自有 `CardTag` ID |

### 4.4 时间线 / 纪元解锁

- `[RegisterEpoch]`、`[RegisterStory]`、`[RegisterStoryEpoch(Type storyType)]`：时间线内容。
- 时间线槽位：`[AutoTimelineSlot(EpochEra era)]`、`[AutoTimelineSlotBeforeColumn/AfterColumn/InColumn(EpochEra)]`、`[AutoTimelineSlotBeforeEpochColumn/AfterEpochColumn/InEpochColumn(Type)]`。
- 解锁条件：`[UnlockEpochAfterRunAs(Type)]`、`[UnlockEpochAfterWinAs(Type)]`、`[UnlockEpochAfterAscensionWin(Type, int ascensionLevel)]`、`[UnlockEpochAfterEliteVictories(Type, int requiredEliteWins=15)]`、`[UnlockEpochAfterBossVictories(Type, int requiredBossWins=15)]`、`[UnlockEpochAfterAscensionOneWin(Type)]`、`[RevealAscensionAfterEpoch(Type)]`、`[UnlockCharacterAfterRunAs(Type)]`。
- 解锁内容门控：`[RequireEpoch(Type epochType)]`、`[RegisterEpochCards(params Type[] cardTypes)]`、`[RequireAllCardsInPool(Type poolType)]`、`[RegisterEpochRelicsFromPool(Type poolType)]`。

### 4.5 其他

| 特性 | 参数 | 说明 |
|---|---|---|
| `[RegisterNodeAttachment(Type parentType, string localId)]` / `[RegisterNodeAttachmentFromScene(Type, string, string scenePath)]` / `[RegisterNodeAttachmentFromConvertedScene(Type, string, string scenePath)]` | 父节点类型 + 本地 ID + 可选场景路径 | 在父节点 `_Ready` 生命周期声明式挂载子节点；基类属性含 `NodeName`、`UniqueNameInOwner`、`IncludeDerivedParentTypes`、`DuplicatePolicy`、`AddMode`、`SetupTiming`、`ChildIndex`、`InsertBeforeName/InsertAfterName`、`QueueFreeReplacedNode` |
| `[RitsuLibOwnedBy(string modId)]` | mod ID | 覆盖本类型上所声明自动注册特性的归属 mod（`AttributeTargets.Class, Inherited=false`，非 AllowMultiple） |
| `[RegisterOwnedTopBarButton(string localButtonStem)]`、`[RegisterOwnedCardPile(string localPileStem)]`、`[RegisterSmartFormatter]`、`[RegisterSmartFormatSource]` | — | 顶栏按钮 / 卡牌堆 / 智能格式化（其余 AutoRegistration 类特性，简要列出） |

---

## 五、关键用法模式

### 5.1 Entry 初始化（模组 `Apply()` 阶段）

```csharp
// 数据注册包裹在注册作用域内，作用域结束才统一惰性初始化
using (RitsuLibFramework.BeginModDataRegistration(Const.ModId, /*syncToCloud*/ false))
{
    // 1) 全局/档案/内存 JSON 数据
    ModDataStore.For(Const.ModId).Register<MyData>(
        "my_data", "my_data.json", SaveScope.Profile,
        () => new(), autoCreateIfMissing: true,
        new ModDataMigrationConfig { CurrentDataVersion = MyData.Current, MinimumSupportedDataVersion = 0 },
        [new V0ToV1Migration()]);

    // 2) 模型保存数据（附着在可变模型实例上，随存档/克隆自动处理）
    ModelSavedDataStore.For(Const.ModId).Register<CardModel, MyCardData>(
        "my_card_data", () => new(),
        new ModelSavedDataOptions { WritePolicy = ModelSavedDataWritePolicy.WhenNonDefault });

    // 3) 模型能力持久化槽初始化（修改能力前必须先调用）
    ModelCapabilities.EnsureInitialized();
}
// 能力工厂注册（也可用 [RegisterModelCapability]）
ModelCapabilityRegistry.Register<MyCapability>("my_capability_id");
// 默认能力修饰（也可用 [RegisterDefaultModelCapability] 的 IModelCapabilitySource）
// 复制监听
ModelCloneRegistry.For(Const.ModId).Register<CardModel>("track", (proto, clone) => { /* ... */ });
// 标题解析器
ModelTitleExtensions.RegisterTitleResolver<MyCardModel>(m => new LocString("cards", m.Id.Entry + ".title"));
// 遗物可见性规则（释放句柄注销）
_relicVisibilityHandle = ModRelicVisibilityRegistry.Register(Const.ModId, relic => relic is not HiddenRelic);
```

- 注册时机要点：`ModelSavedDataStore` 槽位、`ModelCapabilities`、自动注册特性均在**初始化期间**注册（之后冻结）；遗物联动注册器（特性驱动）允许在 `ModelDb` 加入 mod 内容前注册，因为目标以 `Type` 保存、运行时才解析。

### 5.2 注册流程（自动注册管线）

1. 在模型类上声明特性，如 `[RegisterCard(typeof(MyCardPool))]`、`[RegisterPower]`、`[RegisterModelCapability]`。
2. 管线在初始化阶段处理：`ContentRegistration*` 走 `ModContentRegistry`；其余走对应注册器（如 `RegisterArchaicToothTranscendence` → `OrobasAncientUpgradeRegistry`）。
3. 需要稳定公共条目时用 `StableEntryStem`/`FullPublicEntry`；跨 mod 继承声明用 `Inherit = true` + `[RitsuLibOwnedBy(...)]` 指定归属。
4. 初始化结束后注册冻结，冻结校验对缺失的模型类型/ID 输出告警（`RegistrationFreezeDiagnostics`）。

### 5.3 模型能力（推荐写法）

```csharp
// 1. 定义能力（模型化，可持久化、可多人同步）
[RegisterModelCapability(StableEntryStem = "burning")]
public sealed class BurningCapability : OwnerHookCapability<CardModel> // 或 CardCapability
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Damage")];
    public override async Task AfterTurnEnd(PlayerChoiceContext ctx, CombatSide side) { /* 原版钩子 */ }
}
// 2. 附加到模型
card.Capabilities().ApplyCapability(new BurningCapability());   // 或 GetOrCreateCapability<BurningCapability>()
card.Capabilities().RemoveCapability<BurningCapability>();
```

- 能力拥有独立 `DynamicVars`，文本里用 `{Capabilities.<Scope>.<Var>}` 寻址（`LocStringVariableScope` 可自定义作用域名）。
- 实现 `IModelCapabilityHookListener`（或继承 `OwnerHookCapability<T>`）后自动进入所属模型的原版钩子流，按 `OwnerHookOrder` 排序；多人逻辑请走此路径（原版钩子会 await）。
- 一次性/限时能力：`OneShotCardPlayCapability`、`UntilCombatEndCapability<T>`、`TurnLimitedCapability<T>`（剩余回合持久化，归零自移除）。

### 5.4 生命周期订阅

| 场景 | 做法 |
|---|---|
| 单例模型收局内/战斗钩子 | 继承 `HookedSingletonModel`，构造传 `HookType.Run` / `HookType.Combat`；`CurrentRunState`/`CurrentCombatState` 自动更新 |
| 模型/能力收原版钩子 | 实现 `IModelCapabilityHookListener` 或继承 `OwnerHookCapability<T>`；重写对应 `After*`/`Before*` 钩子 |
| 自定义监听器流 | `ModelHookListenerDispatcher.FromCombat/FromRun/FromModels<TListener>(...)` 解析（含已附加能力与去重） |
| 模型被 `MutableClone` | `ModelCloneRegistry.For(modId).Register(...)`；能力走 `IModelCapabilityCloneHandler`；保存数据走 `ClonePolicy` |
| 卡牌生命周期 | `CardCapability` 的升级/降级/转化回调；`CardPlayCapability`/`OneShotCardPlayCapability` 的打出回调；`ICardTransformCarryOverCapability` 携带到转化结果 |
| 充能球生命周期 | `OrbCapability.OnOwnerOrbPassiveTriggered/Evoked/BeforeTurnEndTriggered/AfterTurnStartTriggered` |
| 档案切换 | `ModDataStoreCache<T>` 自动监听 `ProfileDataInvalidatedEvent` 失效（`RitsuLibFramework.SubscribeLifecycle<TEvent>`） |
| 多人模型身份 | `ModModelIdentity`/`ModModelIdentityToken` 由内部注册表随所有权生命周期自动维护 |

---

## 六、速查：常用入口一览

| 想做什么 | 用哪个 |
|---|---|
| 注册卡/遗物/药水等模型 | `[RegisterCard(Type pool)]`、`[RegisterRelic(Type pool)]`、`[RegisterPotion(Type pool)]` 等特性 |
| 自定义卡牌/遗物等行为 | 继承 `CardCapability` / `RelicCapability` / `PowerCapability` / `OrbCapability` 等 |
| 给模型挂持久化数据 | `ModelSavedDataStore.For(modId).Register<TTarget,TPayload>(key, ...)` |
| 全局/档案 JSON 存档 | `ModDataStore.For(modId).Register<T>(key, fileName, SaveScope.X, ...)` + `Get/Modify/Save` |
| 遗物不出现在常规 UI | 模型实现 `IModRelicVisibility`，或 `ModRelicVisibilityRegistry.Register(modId, rule)` |
| 古老牙齿/尘封之书/欧洛巴斯之触联动 | `[RegisterArchaicToothTranscendence]` / `[RegisterDustyTomeCard]` / `[RegisterTouchOfOrobasRefinement]` |
| 改卡牌显示标题/描述/费用/发光/覆盖层 | 实现 `ICardTitleContributor` / `ICardDescriptionContributor` / `ICardEnergyCostContributor` / `ICardGlowContributor` / `ICardOverlayContributor` 等能力接口 |
| 模型显示标题解析 | `ModelTitleExtensions.RegisterTitleResolver<TModel>(...)` |
