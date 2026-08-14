# RitsuLib R5 开发文档：Lifecycle / Patching / Keywords / Localization

> 来源：`E:\MOD\sts2\STS2-RitsuLib\src\`（STS2 官方 mod 库 RitsuLib 源码精读）
> 命名空间：框架入口 `STS2RitsuLib`；补丁 `STS2RitsuLib.Patching.*`；关键词 `STS2RitsuLib.Keywords`；本地化 `STS2RitsuLib.Localization(.SmartFormat)`；自动注册特性 `STS2RitsuLib.Interop.AutoRegistration`。
> 适用：mod 制作实用 API 清单，内部实现（Harmony 补丁类、Patches 子目录等）已跳过。

---

## 0. 全局架构速览

- 框架入口是静态类 **`RitsuLibFramework`**（`[ModInitializer(nameof(Initialize))]`），持有全部注册 API 的门面方法：`GetKeywordRegistry`、`GetSmartFormatRegistry`、`CreatePatcher`、`CreateModLocalization`、`SubscribeLifecycle`、`GetContentRegistry` 等，均以 `modId` 为第一参数。
- 所有**内容/关键词/时间线注册**在模型初始化前完成并**冻结**（冻结后注册抛 `InvalidOperationException`）。
- **生命周期事件**：强类型 `record struct`，通过 `SubscribeLifecycle` 订阅；可回放事件（实现 `IReplayableFrameworkLifecycleEvent`）会在订阅时向新订阅者重放最近一次状态。

---

## 1. Lifecycle（生命周期）

### 1.1 公共 API 清单（命名空间 `STS2RitsuLib`）

**接口 / 基类型**

| 类型 | 用途 |
|---|---|
| `interface IFrameworkLifecycleEvent` | 所有生命周期事件的基接口；唯一成员 `DateTimeOffset OccurredAtUtc { get; }` |
| `interface IReplayableFrameworkLifecycleEvent : IFrameworkLifecycleEvent` | 标记"可回放"事件：新订阅者订阅时会收到最近一次该事件 |
| `interface ILifecycleObserver` | 观察者接口，`void OnEvent(IFrameworkLifecycleEvent evt)`，实现通常按具体类型 switch |

**框架生命周期事件**（`FrameworkLifecycleContracts.cs` / `GameLifecycleContracts.cs`，均在根命名空间）：

| 事件 | 触发时机 | 可回放 |
|---|---|---|
| `FrameworkInitializingEvent(string FrameworkModId, string FrameworkVersion, ...)` | RitsuLib 初始化开始、mod 完成设置前 | |
| `FrameworkInitializedEvent(string FrameworkModId, bool IsActive, ...)` | 框架初始化完成 | ✔ |
| `ProfileServicesInitializingEvent` / `ProfileServicesInitializedEvent(int ProfileId, ...)` | 档案作用域服务初始化前后 | ✔(后者) |
| `EssentialInitializationStartingEvent` / `EssentialInitializationCompletedEvent` | 必要阻塞初始化前后 | ✔(后者) |
| `DeferredInitializationStartingEvent` / `DeferredInitializationCompletedEvent` | 延迟初始化前后 | ✔(后者) |
| `ContentRegistrationClosedEvent(string Reason, ...)` | 内容注册关闭（不再接受注册） | ✔ |
| `ModelRegistryInitializingEvent` / `ModelRegistryInitializedEvent(int RegisteredModelTypeCount, ...)` | 模型注册表填充前后 | ✔(后者) |
| `ModelIdsInitializingEvent` / `ModelIdsInitializedEvent` | 模型 ID 分配阶段前后 | ✔(后者) |
| `ModelPreloadingStartingEvent` / `ModelPreloadingCompletedEvent` | 模型预加载前后 | ✔(后者) |
| `GameTreeEnteredEvent(NGame Game, ...)` | 根游戏节点进入场景树 | ✔ |
| `GameReadyEvent(NGame Game, ...)` | 游戏玩法逻辑就绪 | ✔ |
| `MainMenuReadyEvent` | 主菜单 ready 回调完成 | ✔ |
| `TelemetryStartupSnapshotReadyEvent(DateTimeOffset SnapshotAtUtc, ...)` | 启动遥测快照采样完成 | ✔ |
| `RunStartedEvent(RunState RunState, bool IsMultiplayer, bool IsDaily, ...)` | 新局开始 | |
| `RunLoadedEvent(RunState RunState, bool IsMultiplayer, bool IsDaily, ...)` | 从存档加载一局 | |
| `RunEndedEvent(SerializableRun Run, bool IsVictory, bool IsAbandoned, ...)` | 一局结束（胜负/放弃） | |

**战斗事件**（`CombatLifecycleContracts.cs`）：
`CombatStartingEvent(IRunState, CombatState?, ...)`、`CombatEndedEvent(IRunState, CombatState?, CombatRoom, ...)`、`CombatVictoryEvent(...)`、`SideTurnStartingEvent(CombatState, CombatSide, ...)`、`SideTurnStartedEvent(...)`、`CardPlayingEvent(CombatState, CardPlay, ...)`、`CardPlayedEvent(...)`、`CardMovedBetweenPilesEvent(IRunState, CombatState?, CardModel, PileType PreviousPile, AbstractModel? Source, ...)`、`CardDrawnEvent(CombatState, CardModel, bool FromHandDraw, ...)`、`CardDiscardedEvent(...)`、`CardExhaustedEvent(..., bool CausedByEthereal, ...)`、`BeforeFlushEvent(CombatState, Player, ...)`、`CardsFlushedEvent(CombatState, Player, IReadOnlyCollection<CardModel> FlushedCards, IReadOnlyCollection<CardModel> RetainedCards, ...)`（≥0.105 才触发）、`CreatureDyingEvent(IRunState, CombatState?, Creature, ...)`、`CreatureDiedEvent(IRunState, CombatState?, Creature, bool WasRemovalPrevented, float DeathAnimationDurationSeconds, ...)`

**附加战斗/地图事件**（`AdditionalHookLifecycleContracts.cs`）：
攻击 `AttackStartingEvent(CombatState, AttackCommand, ...)` / `AttackEndedEvent(CombatState, PlayerChoiceContext?, AttackCommand, ...)`；
格挡 `BlockGainingEvent(CombatState, Creature, decimal Amount, ValueProp Props, CardModel? CardSource, ...)` / `BlockGainedEvent(...)` / `BlockBrokenEvent(CombatState, Creature, ...)` / `BlockClearedEvent(...)`；
卡牌 `CardAutoPlayingEvent(CombatState, CardModel, Creature? Target, AutoPlayType, ...)`、`CardEnteredCombatEvent(...)`、`CardGeneratedForCombatEvent(CombatState, CardModel, Player? Creator, bool? AddedByPlayer, ...)`、`CardRemovingEvent(IRunState, CardModel, ...)`；
生物/HP `CreatureAddedToCombatEvent(...)`、`CurrentHpChangedEvent(IRunState, CombatState?, Creature, decimal Delta, ...)`；
能量 `EnergyGainedEvent(CombatState, int Amount, Player Gainer, ...)`、`EnergyResetEvent(...)`、`EnergySpentEvent(CombatState, CardModel, int Amount, ...)`；
手牌/回合 `HandDrawingEvent(CombatState, Player, PlayerChoiceContext, ...)`、`HandEmptiedEvent(...)`、`PlayerTurnStartedEvent(...)`、`ShuffledEvent(CombatState, PlayerChoiceContext, Player Shuffler, ...)`、`ExtraTurnTakenEvent(...)`、`SideTurnEndingEvent(CombatState, CombatSide, IReadOnlyCollection<Creature>? Participants, ...)`、`SideTurnEndedEvent(...)`；
药水 `PotionUsingEvent(IRunState, CombatState?, PotionModel, Creature? Target, ...)` / `PotionUsedEvent(...)`；
星星/召唤 `StarsGainedEvent(CombatState, int Amount, Player Gainer, ...)`、`StarsSpentEvent(...)`、`SummonedEvent(CombatState, PlayerChoiceContext, Player Summoner, decimal Amount, ...)`；
局外 `ItemPurchasedEvent(IRunState, Player, MerchantEntry ItemPurchased, int GoldSpent, ...)`、`MapGeneratedEvent(IRunState, ActMap, int ActIndex, ...)`、`RestSiteHealedEvent(IRunState, Player, bool IsMimicked, ...)`、`RestSiteSmithedEvent(IRunState, Player, ...)`

**奖励事件**（`RewardLifecycleContracts.cs`）：`GoldGainedEvent(IRunState, Player, int GoldTotal, ...)`、`GoldLostEvent(Player, decimal Amount, GoldLossType LossType, int GoldTotal, ...)`、`PotionProcuredEvent(IRunState, CombatState?, PotionModel, ...)`、`PotionDiscardedEvent(...)`、`RelicObtainedEvent(Player, RelicModel, ...)`、`RelicRemovedEvent(...)`、`RewardTakenEvent(IRunState, Player, Reward, ...)`

**房间事件**（`RoomLifecycleContracts.cs`）：`RoomEnteringEvent(IRunState, AbstractRoom, ...)`、`RoomEnteredEvent(...)`、`RoomExitedEvent(RunManager, AbstractRoom, ...)`、`ActEnteringEvent(RunManager, int TargetActIndex, bool DoTransition, ...)`、`ActEnteredEvent(IRunState, int CurrentActIndex, ...)`、`RewardsScreenContinuingEvent(RunManager, ...)`

**存档事件**（`SaveLifecycleContracts.cs`）：`ProfileIdInitializedEvent(SaveManager, int ProfileId, ...)`（✔可回放）、`ProfileSwitchingEvent(SaveManager, int? PreviousProfileId, int NextProfileId, ...)`、`ProfileSwitchedEvent(...)`（✔）、`RunSavingEvent(SaveManager, AbstractRoom? PreFinishedRoom, bool SaveProgress, ...)`、`RunSavedEvent(...)`、`ProgressSavingEvent(SaveManager, int? ProfileId, ...)`、`ProgressSavedEvent(...)`、`ProfileDeletingEvent(SaveManager, int ProfileId, ...)`、`ProfileDeletedEvent(...)`

**解锁事件**（`UnlockLifecycleContracts.cs`）：`EpochObtainedEvent(SaveManager, string EpochId, ...)`、`EpochRevealedEvent(SaveManager, string EpochId, bool IsDebug, ...)`、`UnlockIncrementedEvent(SaveManager, int TotalUnlocks, string? PendingEpochId, ...)`

**游戏结束事件**（`GameOverLifecycleContracts.cs`）：`GameOverScreenCreatedEvent(RunState, SerializableRun, NGameOverScreen Screen, ...)`

### 1.2 订阅 API（`RitsuLibFramework`）

```csharp
IDisposable SubscribeLifecycle(ILifecycleObserver observer, bool replayCurrentState = true)
IDisposable SubscribeLifecycle<TEvent>(Action<TEvent> handler, bool replayCurrentState = true) where TEvent : IFrameworkLifecycleEvent
IDisposable SubscribeLifecycle<TEvent>(Action<TEvent, IDisposable> handler, bool replayCurrentState = true) // 回调内可自注销
```

- 返回 `IDisposable`，Dispose 即退订。
- `replayCurrentState = true`（默认）：订阅时同步重放已发生的可回放事件（按 `OccurredAtUtc` 排序；类型化订阅只重放最近一次 TEvent）。

### 1.3 关键用法模式

```csharp
// 战斗开始后打印敌人数量
RitsuLibFramework.SubscribeLifecycle<CombatStartingEvent>(e =>
{
    GD.Print($"[MyMod] combat start, state={e.CombatState}");
});
// 只在主菜单就绪后执行一次（可回放，迟订阅也不会漏）
RitsuLibFramework.SubscribeLifecycle<MainMenuReadyEvent>(_ => MyMod.InitUi());
```

---

## 2. Patching（Harmony 补丁封装）

命名空间：`STS2RitsuLib.Patching.Core` / `.Models` / `.Builders` / `.Rules`。
入口：`RitsuLibFramework.CreatePatcher(ownerModId, patcherName, patcherLabel = null, LogType logType = Generic)` → 返回带专属 logger 的 `ModPatcher`。

### 2.1 公共 API 清单

| 类型 | 用途 / 关键签名 |
|---|---|
| `class ModPatcher(string patcherId, Logger logger, string patcherName = "")` | 持有一个 Harmony 实例。属性：`PatcherId`、`PatcherName`、`Logger`、`RegisteredPatchCount`、`RegisteredDynamicPatchCount`、`AppliedPatchCount`、`RegisteredPatches`、`IsApplied`。方法：`RegisterPatch(ModPatchInfo)`、`RegisterPatches(params ReadOnlySpan<ModPatchInfo>)`、`RegisterDynamicPatch(DynamicPatchInfo)`、`RegisterDynamicPatches(...)`、`bool ApplyDynamicPatches(IEnumerable<DynamicPatchInfo>, bool rollbackOnCriticalFailure = false)`、`bool PatchAll()`、`ApplyLateStaticPatches(ReadOnlySpan<ModPatchInfo>)`（PatchAll 之后补装）、`int UnpatchExternalPatches(MethodBase/ModPatchTarget, string owner, ...)`（移除别的 Harmony ID 的补丁）、`UnpatchAll()` |
| `class ModPatchInfo(id, Type targetType, string methodName, Type patchType, bool isCritical = true, string description = "", Type[]? parameterTypes = null, bool ignoreIfTargetMissing = false, MethodType harmonyMethodType = Normal)` | 静态补丁描述：定位原版方法 + 补丁类型（含 `Prefix`/`Postfix`/`Transpiler`/`Finalizer` 静态方法）。属性均为 get-only |
| `record ModPatchTarget(Type TargetType, string MethodName, Type[]? ParameterTypes, bool IgnoreIfMissing, MethodType HarmonyMethodType)` | 目标描述，多个便捷构造重载 |
| `static class PatchTarget` | 目标工厂：`Method<TTarget>(name[, params Type[]])`、`OptionalMethod`、`Getter`/`OptionalGetter`、`Setter`/`OptionalSetter`、`Constructor<TTarget>(params Type[])`、`OptionalConstructor`、`AsyncMethod`（编译器生成 MoveNext）、`EnumeratorMethod` |
| `sealed class DynamicPatchInfo(id, MethodBase originalMethod, HarmonyMethod? prefix = null, HarmonyMethod? postfix = null, HarmonyMethod? transpiler = null, HarmonyMethod? finalizer = null, bool isCritical = true, string? description = null)` | 运行时解析目标的动态补丁；`static FromModPatchTarget(id, ModPatchTarget, ...)` 按解析语义创建 |
| `sealed class DynamicPatchBuilder(string idPrefix)` | 流式构建动态补丁：`Add(MethodBase/ModPatchTarget, ...)`、`AddPropertyGetter(Type, string propertyName, ...)`、`AddMethod(Type, string methodName, Type[]? parameterTypes, ...)`、`bool TryAddMethod(...)`、`bool TryAddMethodByName(string targetTypeName, ...)`（按字符串类型名，兼容旧版本）、`static HarmonyMethod FromMethod(Type patchType, string methodName)`；属性 `Patches`（不自动应用，需 `patcher.ApplyDynamic(builder)`） |
| `class ModPatchResult` | 补丁应用结果：`Success`、`Ignored`、`ErrorMessage`、`Exception`；静态工厂 `CreateSuccess/CreateFailure/CreateIgnored` |
| `interface IPatchMethod` | 声明式补丁接口：`static abstract string PatchId`、`static virtual bool IsCritical => true`、`static virtual string Description`、`static abstract ModPatchTarget[] GetTargets()`；默认实现 `static ModPatchInfo[] CreatePatchInfos<TPatch>()` 自动生成补丁元数据（同类型同名多目标自动加 `__1`/`__2` 后缀防重） |
| `interface IModPatches` | 补丁组接口：`static abstract void AddTo(ModPatcher patcher)`，用于把一组补丁注册进 patcher |
| `static class ModPatcherExtensions` | `RegisterPatch<TPatch>() where TPatch : IPatchMethod`、`RegisterPatches<T>() where T : IModPatches`、`RegisterFromRule(ModPatchRule, params ReadOnlySpan<Assembly>)`、`bool ApplyDynamic(DynamicPatchBuilder, bool rollbackOnCriticalFailure = false)` |
| `class ModPatchRule` + `class PatchRuleBuilder` | 程序集扫描规则：`PatchRuleBuilder.Create(id).ForTypes(pred).ForMethods(pred).WithPatch(patchType).Critical(bool).WithDescription(desc).Build()`；`GeneratePatches(params ReadOnlySpan<Assembly>)` 按谓词批量生成 `ModPatchInfo` |
| `static class PatchTargetMethodResolver` | 目标解析器：`Resolve(ModPatchInfo/ModPatchTarget)`、`ResolveRequired(...)`（失败抛 `MissingMethodException`）；Normal 用反射（含继承成员），Async/Getter/Setter/Constructor/Enumerator 用 Harmony `AccessTools` |
| `static class PatchLog` | 补丁日志注册表：`Bind(Type patchType, Logger)`、`For<TPatch>()` / `For(Type)` |
| `static class PrivateAccess` | 私有成员解析助手：`Field<TTarget>(name)`、`DeclaredField`、`FieldRef<TTarget,TField>(name)`、`Method<TTarget>(name[, params Type[]])`、`DeclaredMethod`、`MethodDelegate<TDelegate>(MethodInfo)` 等（含 Getter/Setter 委托），供补丁体内反射使用 |

### 2.2 关键用法模式

**方式 A：静态补丁 + 声明式接口（推荐，类型安全）**

```csharp
public sealed class MyCardDrawPatch : IPatchMethod
{
    public static string PatchId => "MyMod.DrawPatch";
    public static string Description => "patch draw count";
    // 可覆盖: public static bool IsCritical => true;
    public static ModPatchTarget[] GetTargets() => [PatchTarget.Method<Player>(nameof(Player.DrawCards))];

    public static void Postfix(Player __instance, ref int __result) { /* ... */ }
}

var patcher = RitsuLibFramework.CreatePatcher("MyMod", "MainPatcher");
patcher.RegisterPatch<MyCardDrawPatch>();
patcher.PatchAll(); // 严重失败会自动 UnpatchAll 并返回 false
```

**方式 B：运行时动态补丁（目标需运行时解析 / 版本差异容错）**

```csharp
var builder = new DynamicPatchBuilder("MyMod.Dyn");
builder.TryAddMethodByName("MegaCrit.Sts2.Core.Nodes.SomeScreen", "OpenScreen", isCritical: false);
patcher.ApplyDynamic(builder); // 立即注册并应用
patcher.UnpatchAll();          // 需要时整体回滚
```

**方式 C：`IModPatches` 补丁组 + `IPatchMethod` 批量**

```csharp
public sealed class AllMyPatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<PatchA>();
        patcher.RegisterPatch<PatchB>();
    }
}
patcher.RegisterPatches<AllMyPatches>();
```

要点：`PatchAll` 之后不能再 `RegisterPatch`（抛异常）；可选目标缺失时 `ignoreIfTargetMissing` 产生 Ignored 而非失败；`ApplyLateStaticPatches` 用于必须等 `ModelDb.Init` 的补丁（如 Android）。

---

## 3. Keywords（关键词）

命名空间 `STS2RitsuLib.Keywords`。入口：`RitsuLibFramework.GetKeywordRegistry(modId)` 或静态 `ModKeywordRegistry.For(modId)`。

### 3.1 公共 API 清单

| 类型 | 用途 / 关键成员 |
|---|---|
| `sealed record ModKeywordDefinition` | 不可变关键词注册数据。构造：`(string ModId, string Id, string TitleTable, string TitleKey, string DescriptionTable, string DescriptionKey, string? IconPath = null, ModKeywordCardDescriptionPlacement cardDescriptionPlacement = None, bool includeInCardHoverTip = true)`。属性：`CardKeywordValue`（注册时由动态枚举铸造的原版范围以上 `CardKeyword` 值，可直接存入 `CardModel.Keywords`）、`IncludeInCardHoverTip` |
| `sealed class ModKeywordRegistry` | 全局/按 mod 注册表。静态：`IsFrozen`、`State`（`KeywordRegistrationState`）、`For(string modId)`、`TryGetOwnerModId`、`TryGet/Get(string id)`、`Get/TryGetByCardKeyword(CardKeyword)`、`IsModCardKeyword`、`TryGetCardKeyword(string id, out CardKeyword)`（ID 无需注册即可取确定性枚举值）、`TryResolveCardKeyword(string idOrEnumName, out CardKeyword)`（注册 ID → 枚举名/数字 → 确定性动态值）、`GetCardKeyword`、`TryGetId`、`GetDefinitionsSnapshot()`、`CreateHoverTip(string id)`、`GetTitle/GetDescription/GetCardText`。实例方法：`RegisterOwned(localKeywordStem, titleTable = "card_keywords", titleKey = null, descriptionTable = null, descriptionKey = null, iconPath = null)`、`RegisterCardKeywordOwnedByLocNamespace(localKeywordStem, iconPath = null, placement = None, includeInCardHoverTip = true)`（ID 与本地化键都用 `ModContentRegistry.GetQualifiedKeywordId(modId, stem)`，键为 `{id}.title` / `{id}.description`，表 `card_keywords`） |
| `sealed record KeywordRegistrationEntry` | 内容包声明式条目；`Register(ModKeywordRegistry)` 一次性注册；静态工厂 `OwnedCardByLocNamespace(modId, localKeywordStem, iconPath = null, ...)` |
| `enum KeywordRegistrationState` | `Open = 0` / `Frozen = 1`（模型初始化期间随内容注册一起冻结，冻结后注册抛异常） |
| `enum ModKeywordCardDescriptionPlacement` | `None = 0`（默认，不注入）/ `BeforeCardDescription = 1` / `AfterCardDescription = 2`（注入内联 BBCode：金色标题 + 句号） |
| `static class ModKeywordExtensions` | `CardModel.AddModKeyword(CardKeyword|string)`、`RemoveModKeyword`、`HasModKeyword(CardKeyword|string)`、`GetModKeywordIds(this object target)`、`GetModKeywordHoverTips(this object target)`、`IEnumerable<string>.ToHoverTips()`、`string.GetModKeywordCardText()`、`string.GetModCardKeyword()`、`CardKeyword.GetModKeywordTitle/Description/Id`、`TryGetModKeywordId` 等 |

### 3.2 关键用法模式

```csharp
// 1) 编程式注册（mod 初始化时）
var kwReg = RitsuLibFramework.GetKeywordRegistry("MyMod");
var def = kwReg.RegisterCardKeywordOwnedByLocNamespace("stun", iconPath: "res://MyMod/icons/stun.png");
// 本地化（card_keywords 表）: MyMod.stun.title / MyMod.stun.description

// 2) 声明式注册（特性，见第 6 节 [RegisterOwnedCardKeyword]）

// 3) 运行时应用
card.AddModKeyword(def.CardKeywordValue);      // 写入原版 CardModel.Keywords
card.HasModKeyword("mymod.stun");              // 按 ID 查询
card.GetModKeywordIds();                        // 列出该卡所有 mod 关键词 ID
```

ID 约定：mod 限定 ID 由 `ModContentRegistry.GetQualifiedKeywordId(modId, stem)` 生成（形如 `MyMod.stun`），全局唯一、去空白、大小写不敏感；重复注册相同 ID 且数据不同会抛异常。

---

## 4. Localization（本地化）

命名空间 `STS2RitsuLib.Localization`（SmartFormat 子命名空间 `STS2RitsuLib.Localization.SmartFormat`）。

### 4.1 公共 API 清单

| 类型 | 用途 / 关键成员 |
|---|---|
| `static class I18NLocTableBridge` | 把 RitsuLib 的 `I18N` 字典注册为游戏原生虚拟 `LocTable`，使 `LocString`/`LocTable` 管线可直接解析。`GetTableId(modId, stem = "DEFAULT")`（标准形如 `MODID_I18N_DEFAULT`）、`TryRegister(modId, I18N, stem, replaceExisting = false)`、`TryUnregister(modId, stem)`。门面：`RitsuLibFramework.RegisterI18NLocTableBridge(modId, i18N, stem)` / `UnregisterI18NLocTableBridge` / `GetI18NLocTableId` |
| `static class AncientDialogueLocalization` | 从 `ancients` 本地化表为模组角色/先古事件加载对话：`BaseLocKey(ancientEntry, characterEntry)`（`{ancient}.talk.{character}.`）、`GetDialoguesForCharacter(ancientEntry, CharacterModel)`、`GetDialoguesForKey(locTable, baseKey)`、`BuildDialogueSetForModAncient(ancientEntry)`（自动读 firstVisitEver/ANY/各原版角色序列，跳过 mod 角色）、`AppendCharacterDialogues(AncientDialogueSet, ancientEntry, IEnumerable<CharacterModel>)` |
| `sealed class ModSmartFormatExtensionRegistry` | SmartFormat 扩展注册（按 mod）：`static For(modId)`；`Register(IFormatter formatter, int order = 0)`、`Register<TFormatter>(int order = 0)`、`RegisterFormatterType(Type, int order)`、`RegisterSource(ISource, int order)`、`RegisterSource<TSource>(int order)`、`RegisterSourceType(Type, int order)`；静态快照 `GetFormattersSnapshot()` / `GetSourcesSnapshot()`、`TryGetFormatterOwnerModId(name, out modId)`。门面：`RitsuLibFramework.GetSmartFormatRegistry(modId)` |
| `sealed record ModSmartFormatExtensionDefinition(string OwnerModId, SmartFormatExtensionKind Kind, Type ImplementationType, int Order, object Instance)` | 已注册扩展的描述 |
| `enum SmartFormatExtensionKind` | `Source`（选择器数据源）/ `Formatter`（格式化器） |

关联类型（`STS2RitsuLib.Utils`，本地化强相关）：`class I18N` —— 从文件系统/嵌入资源/PCK 加载合并 JSON 翻译字典，构造 `(instanceName, fsFolders, resourceFolders, pckFolders, resourceAssembly, fallbackLanguage)`；非英语自动回退 `eng`；API：`Get(key, fallback)`、`TryGet`、`ContainsKey`、`Snapshot()`、`EnumerateKeys(prefix)`、`EnumerateAvailableLanguages()`、`ForceReload()`、`event Action? Changed`、`static ResolveCurrentLanguageCode()` / `NormalizeLanguageCode()`。创建门面：`RitsuLibFramework.CreateLocalization(...)` / `CreateModLocalization(modId, instanceName, ...)`（默认文件根 `.../mod_data/{modId}/localization`）。

### 4.2 关键用法模式

```csharp
// 1) 创建 mod 本地化并桥接为虚拟 LocTable（原生 LocString 也能解析）
var i18n = RitsuLibFramework.CreateModLocalization("MyMod", "MyModLoc");
RitsuLibFramework.RegisterI18NLocTableBridge("MyMod", i18n);
var loc = new LocString(RitsuLibFramework.GetI18NLocTableId("MyMod"), "SomeKey");

// 2) SmartFormat 扩展
var sf = RitsuLibFramework.GetSmartFormatRegistry("MyMod");
sf.Register<MyFormatter>();      // 实现 SmartFormat.Core.Extensions.IFormatter
sf.RegisterSource<MySource>();   // 实现 ISource
```

---

## 5. 关键用法模式汇总（Entry / 注册流程 / 生命周期）

### 5.1 Mod 入口（Entry）初始化

STS2 标准 mod 入口：程序集内一个 `[ModInitializer(nameof(Initialize))]` 静态类 + `public static void Initialize()`（`ModInitializerAttribute` 来自游戏 `MegaCrit.Sts2.Core.Modding`）。RitsuLib 自身即如此（`RitsuLibFramework` 带 `[ModInitializer]`；`STS2-RitsuLib` 包另有一个 loader 入口 `Bootstrap` 负责按游戏版本选加载变体）。

```csharp
[ModInitializer(nameof(Initialize))]
public static class MyMod
{
    public static void Initialize()
    {
        // 1) 注册内容（卡/遗物/能力…）——经 ModContentRegistry
        // 2) 注册关键词 / 卡牌标签 / 牌堆 / 顶部栏按钮 / 时间线 / 解锁
        // 3) 创建并 PatchAll 补丁器（严重失败可用 RitsuLibFramework.ApplyRequiredPatcher(patcher, disableMod) 兜底）
        // 4) 创建本地化并注册 I18N 桥
        // 5) SubscribeLifecycle 订阅事件
    }
}
```

### 5.2 注册流程与冻结时序

1. 游戏加载所有 mod → 各 mod `[ModInitializer]` 的 `Initialize()` 依次执行（**所有编程式注册应在这里完成**）。
2. 框架启动阶段发布：`FrameworkInitializing → FrameworkInitialized → EssentialInitialization… → DeferredInitialization…`。
3. 自动注册管线（`ModTypeDiscoveryHub` + `AttributeAutoRegistrationTypeDiscoveryContributor`）扫描所有 mod 程序集类型，处理 `AutoRegistrationAttribute` 系列特性（含 `Order` 排序、`Inherit` 继承语义、`RitsuLibOwnedBy` 归属覆盖）。
4. 模型初始化：`ModelRegistryInitializing → ModelRegistryInitialized → ModelIdsInitialized → ModelPreloadingCompleted`；期间发布 `ContentRegistrationClosedEvent`，**关键词/内容/时间线注册表同时冻结**（`KeywordRegistrationState.Frozen`，再注册抛异常）。
5. 之后进入游戏循环：`GameTreeEntered → GameReady → MainMenuReady`（三者均可回放，迟订阅不会漏）。

### 5.3 生命周期订阅

- 强类型订阅 `SubscribeLifecycle<TEvent>(handler)`；handler 内可自行 switch 或按类型过滤。
- 需要"只跑一次"的场景用可回放事件（如 `MainMenuReadyEvent`、`GameReadyEvent`）。
- 返回的 `IDisposable` 用于退订；带 `(evt, sub)` 的重载可在回调内自注销。
- 观察者回调异常只记日志不会中断框架。

### 5.4 常用门面速查（`RitsuLibFramework`）

`CreatePatcher` / `CreateLogger(modId)` / `CreateModLocalization(WithFallback)` / `GetI18NLocTableId` / `RegisterI18NLocTableBridge` / `GetKeywordRegistry` / `GetSmartFormatRegistry` / `GetContentRegistry` / `GetCardPileRegistry` / `GetTopBarButtonRegistry` / `GetTimelineRegistry` / `GetUnlockRegistry` / `GetDataStore` / `GetRunSavedDataStore` / `GetModelCloneRegistry` / `BeginModDataRegistration(modId)` / `EnsureGodotScriptsRegistered(assembly)` / `ApplyRequiredPatcher`。

---

## 6. 特性（Attribute）清单

所有自动注册特性命名空间：`STS2RitsuLib.Interop.AutoRegistration`。共同点：

- 基类 `AutoRegistrationAttribute`：`int Order`（同阶段内排序，小者先执行）、`bool Inherit`（基类声明、派生类继承时生效；最近声明覆盖继承配置）。
- `ContentRegistrationAttribute : AutoRegistrationAttribute`：内容注册（经 `ModContentRegistry` 分发）。
- 标注目标：绝大多数为 **`AttributeTargets.Class`，`AllowMultiple = true`，`Inherited = false`**（下文不再重复，仅列例外）。
- 触发时机：框架启动的**类型发现阶段自动执行**（`Initialize()` 之后、模型注册表初始化/冻结之前）；`[ModInitializer]` 是唯一由游戏加载器直接调用的"特性"。
- `[RitsuLibOwnedBy(modId)]`：`Inherited = false`，标注在类上，覆盖该类声明的自动注册特性所属 modId（多 mod 共享程序集时用）。

### 6.1 内容模型注册（class 目标）

| 特性 | 参数 | 作用 |
|---|---|---|
| `[RegisterCharacter]` | — | 注册为角色模型 |
| `[RegisterAct]` | — | 注册为阶段模型 |
| `[RegisterMonster]` | — | 注册为怪物模型 |
| `[RegisterPower]` | — | 注册为能力模型 |
| `[RegisterOrb]` | — | 注册为充能球模型 |
| `[RegisterEnchantment]` | — | 注册为附魔模型 |
| `[RegisterAffliction]` | — | 注册为侵蚀模型 |
| `[RegisterAchievement]` | — | 注册为成就模型 |
| `[RegisterSingleton]` | — | 注册为单例模型 |
| `[RegisterModelCapability]` | 属性：`StableEntryStem?`、`FullPublicEntry?` | 注册为模型能力 |
| `[RegisterDefaultModelCapability(Type targetModelType)]` | 属性：`ModifierId?` | 把标注的能力类型加入目标模型类型的默认能力集 |
| `[RegisterGoodModifier]` | 属性：`ModifierListSortOrder`（负值插前/非负插后） | 注册为正面每日特效 |
| `[RegisterBadModifier]` | 属性：`ModifierListSortOrder` | 注册为负面每日特效 |
| `[RegisterMutuallyExclusiveModifierGroup(params Type[] memberTypes)]` | `MemberTypes` | 注册互斥特效组（含标注类型） |
| `[RegisterSharedCardPool]` / `[RegisterSharedRelicPool]` / `[RegisterSharedPotionPool]` / `[RegisterSharedEvent]` / `[RegisterSharedAncient]` | — | 注册共享卡池/遗物池/药水池/事件/先古事件 |
| `[RegisterGlobalEncounter]` | — | 注册为全局遭遇 |
| `[RegisterCard(Type poolType)]` | 属性：`StableEntryStem?`、`FullPublicEntry?`（基类 `ModelPublicEntryRegistrationAttributeBase`） | 注册为指定卡池中的卡牌 |
| `[RegisterRelic(Type poolType)]` | 同上 | 注册为指定遗物池中的遗物 |
| `[RegisterPotion(Type poolType)]` | 同上 | 注册为指定药水池中的药水 |
| `[RegisterTrashHeapCard]` / `[RegisterTrashHeapRelic]` | — | 注册为"垃圾堆"事件 Grab/Dive 候选 |
| `[RegisterCharacterStarterCard(Type characterType, int count = 1)]` | `CharacterType`、`Count` | 注册为角色初始卡牌（份数） |
| `[RegisterCharacterStarterRelic(Type characterType, int count = 1)]` | 同上 | 角色初始遗物 |
| `[RegisterCharacterStarterPotion(Type characterType, int count = 1)]` | 同上 | 角色初始药水 |
| `[RegisterActEncounter(Type actType)]` / `[RegisterActEvent(Type actType)]` / `[RegisterActAncient(Type actType)]` | `ActType` | 注册到指定阶段的遭遇/事件/先古事件 |

### 6.2 关键词 / 标签

| 特性 | 参数 | 作用 |
|---|---|---|
| `[RegisterOwnedKeyword(string localKeywordStem)]` | 属性：`TitleTable`（默认 `card_keywords`）、`TitleKey?`、`DescriptionTable?`、`DescriptionKey?`、`IconPath?`、`CardDescriptionPlacement`（默认 None）、`IncludeInCardHoverTip`（默认 true） | 注册模组限定 ID 的关键词（键默认 `{id}.title`/`{id}.description`） |
| `[RegisterOwnedCardKeyword(string localKeywordStem)]` | 属性：`IconPath?`、`CardDescriptionPlacement`、`IncludeInCardHoverTip` | 按游戏 card_keywords 约定注册关键词 |
| `[RegisterOwnedCardTag(string localCardTagStem)]` | `LocalCardTagStem` | 注册模组自定义 `CardTag` ID |

### 6.3 时间线 / 纪元 / 解锁

| 特性 | 参数 | 作用 |
|---|---|---|
| `[RegisterEpoch]` | — | 注册为时间线纪元节点 |
| `[RegisterStory]` | — | 注册为时间线故事 |
| `[RegisterStoryEpoch(Type storyType)]` | `StoryType` | 把标注纪元加入指定故事 |
| `[AutoTimelineSlot(EpochEra era)]` | `Era` | 纪元放入指定时期列第一个空位 |
| `[AutoTimelineSlotBeforeColumn(EpochEra anchorEra)]` / `[AutoTimelineSlotAfterColumn(...)]` | `AnchorEra` | 放入锚点时期之前/之后最近的空闲列 |
| `[AutoTimelineSlotBeforeEpochColumn(Type refEpoch)]` / `[AutoTimelineSlotAfterEpochColumn(...)]` | `ReferenceEpochType` | 放入参考纪元列之前/之后最近的空闲列 |
| `[AutoTimelineSlotInColumn(EpochEra anchorEra)]` / `[AutoTimelineSlotInEpochColumn(Type refEpoch)]` | — | 放入锚点时期/参考纪元同一列 |
| `[RegisterArchaicToothTranscendence(Type ancientCardType)]` | `AncientCardType` | 古老牙齿：标注初始卡 → 指定先古卡 |
| `[RegisterDustyTomeCard(Type characterType)]` | `CharacterType` | 尘封魔典优先候选（标注先古卡，指定角色） |
| `[RegisterTouchOfOrobasRefinement(Type upgradedRelicType)]` | `UpgradedRelicType` | 欧洛巴斯之触：标注初始遗物 → 升级遗物 |
| `[RegisterEpochCards(params Type[] cardTypes)]` | `CardTypes` | 为标注纪元注册显式卡牌解锁内容 |
| `[RequireAllCardsInPool(Type poolType)]` | `PoolType` | 池中所有已注册卡牌要求先揭示该纪元 |
| `[RegisterEpochRelicsFromPool(Type poolType)]` | `PoolType` | 池中所有遗物作为该纪元解锁内容并受其门控 |
| `[RequireEpoch(Type epochType)]` | `EpochType` | 标注内容要求先揭示指定纪元 |
| `[UnlockEpochAfterRunAs(Type epochType)]` | `EpochType` | 用标注角色完成任意一局后解锁纪元 |
| `[UnlockEpochAfterWinAs(Type epochType)]` | `EpochType` | 用标注角色通关后解锁 |
| `[UnlockEpochAfterAscensionWin(Type epochType, int ascensionLevel)]` | `AscensionLevel` | 指定进阶及以上通关后解锁 |
| `[UnlockEpochAfterEliteVictories(Type epochType, int requiredEliteWins = 15)]` | `RequiredEliteWins` | 击败 N 精英后解锁 |
| `[UnlockEpochAfterBossVictories(Type epochType, int requiredBossWins = 15)]` | `RequiredBossWins` | 击败 N Boss 后解锁 |
| `[UnlockEpochAfterAscensionOneWin(Type epochType)]` | `EpochType` | 进阶 1 通关后解锁 |
| `[RevealAscensionAfterEpoch(Type epochType)]` | `EpochType` | 纪元揭示后显示标注角色的进阶界面 |
| `[UnlockCharacterAfterRunAs(Type epochType)]` | `EpochType` | 通过标注角色的局后解锁流程授予纪元 |

### 6.4 UI / 附加注册

| 特性 | 参数 | 作用 |
|---|---|---|
| `[RegisterOwnedTopBarButton(string localButtonStem)]` | 属性：`IconPath?`、`ButtonOrder`、`OffsetX/Y`；类须实现 `IModTopBarButtonHandler`（无参构造） | 注册模组顶部栏按钮；悬停提示用 `static_hover_tips` 的 `{id}.title`/`{id}.description` |
| `[RegisterOwnedCardPile(string localPileStem)]` | 属性：`Scope`（默认 CombatOnly）、`Style`（默认 Headless）、`AnchorKind`、`AnchorOffsetX/Y`、`AnchorCustomX/Y/PivotX/PivotY`、`IconPath?`、`Hotkeys?`、`CardShouldBeVisible`、`ExtraHandDirection/Spacing/CardScale/HoverScale/ShowPlayableGlow/AllowCardPlay`、`HoverTipOffsetX/Y`、`HoverTipPlacement`；类可实现 `IModCardPileHandler` | 注册模组牌堆；悬停提示键 `{id}.title`/`{id}.description`/`{id}.empty` |
| `[RegisterNodeAttachment(Type parentType, string localId)]` | 属性：`NodeType?`、`QueueFreeReplacedNode`（基类属性） | 注册节点挂载项（标注类型为子节点类型或工厂创建） |
| `[RegisterNodeAttachmentFromScene(Type parentType, string localId, string scenePath)]` | 另有 `ScenePath`、`NodeType?` | 从 Godot 场景实例化节点挂载 |
| `[RegisterNodeAttachmentFromConvertedScene(Type parentType, string localId, string scenePath)]` | 另有 `ScenePath`、`NodeType?` | 从 RitsuLib 节点工厂转换场景后挂载 |

### 6.5 本地化 / 其他

| 特性 | 参数 | 作用 |
|---|---|---|
| `[RegisterSmartFormatter]` | — | 标注类型注册为游戏本地化 SmartFormat 格式化器（实现 `SmartFormat.Core.Extensions.IFormatter`，需无参构造） |
| `[RegisterSmartFormatSource]` | — | 标注类型注册为 SmartFormat 选择器数据源（实现 `ISource`） |
| `[RitsuLibOwnedBy(string modId)]` | `ModId` | 覆盖标注类型上自动注册特性的归属 modId |
| `[ModInitializer(string methodName)]`（游戏 `MegaCrit.Sts2.Core.Modding` 提供） | `initializerMethod` | mod 入口：标注静态类，指定静态初始化方法名，由游戏加载器调用 |
