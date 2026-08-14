# RitsuLib r2 — Interop 目录与自动注册特性（Interop / AutoRegistration）

> 源码：`E:\MOD\sts2\STS2-RitsuLib\src\Interop\`（含 `Utils\Persistence\Interop\`）
> 命名空间：`STS2RitsuLib.Interop`、`STS2RitsuLib.Interop.AutoRegistration`、`STS2RitsuLib.Interop.Patches`（内部）、`STS2RitsuLib.Utils.Persistence.Interop`
> 本页只列对 mod 制作有实用价值的公开 API，内部 Patch/发射器细节从略。

---

## 1. 公共 API 清单

### 1.1 `STS2RitsuLib.Interop`（跨模组互操作）

| 类型 | 用途 | 关键成员签名 |
|---|---|---|
| `ModInteropAttribute`（sealed Attribute） | 标注互操作存根类：编译期不引用，运行时重写其成员转发到 **另一个 mod** 的成员 | ctor `(string modId, string? type = null)`；属性 `string ModId`、`string? Type`（默认远端 CLR 类型名） |
| `AssemblyInteropAttribute`（sealed Attribute） | 同上前者，但目标按 **程序集限定名** `Namespace.Type, AssemblyName` 解析 | ctor `(string? type = null)`；属性 `string? Type` |
| `InteropTargetAttribute`（sealed Attribute） | 为嵌套包装类/方法/属性覆盖目标类型或远端成员名 | ctor `(string type, string? name)` 或 `(string? name = null)`；属性 `string? Type`、`string? Name` |
| `InteropAnyParamAttribute`（sealed Attribute） | 标注方法参数为通配符：解析远端重载时匹配任意参数类型（不校验可赋值性） | 无成员；可标注 `Parameter` |
| `InteropClassWrapper`（abstract class） | 实例型互操作存根基类：实例成员转发到被包装的运行时对象 | 公有字段 `object Value` |
| `IModTypeDiscoveryContributor`（interface） | 自定义类型发现贡献器接口：每个已发现 mod 类型回调一次 | `void Contribute(Harmony harmony, IReadOnlyDictionary<string, Assembly> modAssembliesByManifestId, Type modType)` |
| `ModTypeDiscoveryHub`（static class） | 模组加载后类型发现管线（早期本地化初始化时运行一次）；注册贡献器与 mod→程序集关联 | `RegisterContributor(IModTypeDiscoveryContributor)`；`RegisterModAssembly(string modId, Assembly)`；`LogDiagnostics()`；`TryResolveRegisteredModId(Assembly, out string)`（internal 其余） |
| `ModInteropTypeDiscoveryContributor`（sealed class） | 内置贡献器：处理带 `[ModInterop]`/`[AssemblyInterop]` 的存根（交给内部 `ModInteropEmitter` 生成 Harmony 转译） | 实现 `Contribute(...)` |
| `ReflectionInteropConvention`（sealed class） | 反射式键控数据通道的静态方法名约定（如 ModData、设置运行时） | 必填 `ObjectGetMethodName`、`ObjectSetMethodName`；可选 MergePatch/JsonPatch/Pointer/全文/根对象等方法名；静态预置 `ModData`、`SettingsRuntimeInterop` |
| `ReflectionStaticChannel`（sealed class） | 反射绑定的静态键控数据访问器（持久化/设置文档/网络载荷） | `Type ProviderType`；`Func<string, object?> GetObject`；`Action<string, object?> SetObject`；`JsonDomChannelDelegates Json` |
| `ReflectionStaticChannelBinder`（static class） | 按命名约定从静态方法提供方类型构建 `ReflectionStaticChannel` | `static ReflectionStaticChannel Bind(Type providerType, ReflectionInteropConvention convention)` |
| `JsonDomChannelDelegates`（sealed record） | 键控 JSON 文档的可选反射绑定操作集合 | 11 个委托字段：`GetMergePatch`、`GetRootObject`、`GetNode`、`ApplyMergePatch`、`GetJsonPatch`、`SetRootObject`、`SetNode`、`MergeObjectAt`、`GetJson`、`SetJson`、`ApplyJsonPatch`（均可为 null） |
| `KeyedJsonDomTransport`（static class） | 在 `ReflectionStaticChannel` 与内存 JSON 树之间同步键控文档（ModData/RPC/副本用） | `PullFromProviderIntoRoot(string key, ReflectionStaticChannel, JsonNode? root, KeyedJsonPathRouting?, JsonSerializerOptions? = null)`；`PushRootToProvider(...)` 同参；`DefaultJsonSerializerOptions` |
| `KeyedJsonPathRouting`（sealed record） | 子树同步用的可选 JSON Pointer 列表 | `(string[]? PullPaths, string[]? PushPaths, string[]? MergePushPaths)` |

### 1.2 `STS2RitsuLib.Interop.AutoRegistration`（自动注册管线）

| 类型 | 用途 | 关键成员签名 |
|---|---|---|
| `AutoRegistrationAttribute`（abstract Attribute） | 所有自动注册特性的基类：声明式注册元数据 | `int Order { get; set; }`（同阶段局部排序，小者先跑）；`bool Inherit { get; set; }`（基类声明可作用于具体派生类型，最近声明覆盖继承配置，不同目标 ID/作用域累加） |
| `ContentRegistrationAttribute`（abstract） | 经 `ModContentRegistry` 分发的内容注册基类 | — |
| `RitsuLibOwnedByAttribute`（sealed Attribute） | 覆盖该类型上声明的自动注册特性的归属 mod ID（含经 Inherit 继承的注册） | ctor `(string modId)`；属性 `string ModId` |

**ModInteropEmitter（`STS2RitsuLib.Interop.Internal`，internal）**：生成 Harmony 转译补丁，把带标记的存根成员转发到目标程序集；仅作机制了解，mod 作者不需要直接使用。

### 1.3 `STS2RitsuLib.Utils.Persistence.Interop`（ModData 运行时互操作）

| 类型 | 用途 | 关键成员签名 |
|---|---|---|
| `ModDataRuntimeInterop`（static class，命名空间 `STS2RitsuLib`） | 注册运行时 ModData 互操作提供方（提供方暴露 `CreateRitsuLibModDataSchema` 及值同步器，无需编译期引用） | `RegisterProviderType(string fullName, string? assemblyName = null)` / `(Type)` / `<TProvider>()`；`RegisterProviderTypeAndTryRegister<TProvider>()`（返回注册数）；`TryRegisterAll()`；`SyncAllFromProviders()`；`PushLoadedDataToAllProviders()`；`EnsureProfileSwitchSyncHook()` |
| `ModDataInteropJsonDocument`（sealed class） | 包装经 `ModDataStore` 持久化的 JSON DOM（含架构版本元数据） | `JsonNode? Root { get; set; }`（默认空 `JsonObject`） |
| `InteropMigrationAdapter`（sealed class : IMigration） | 把定义了 `FromVersion`/`ToVersion`/`Migrate(JsonObject)` 的既有迁移对象适配为 `IMigration` | `(object instance)`；`int FromVersion`、`int ToVersion`、`bool Migrate(JsonObject)`；`static bool TryCreateFromType(Type, out InteropMigrationAdapter?)` |
| `ModDataJsonInteropPrimitives` / `RuntimeModDataInteropSource` / `RuntimeModDataStubPatcher` | 内部实现（原始值互操作、运行时发现源、存根补丁），无需直接使用 | — |

---

## 2. 特性（Attribute）清单

> 除特别说明外均 `AttributeTargets.Class`、`AllowMultiple = true`、`Inherited = false`。基类继承链：`AutoRegistrationAttribute` →（部分）`ContentRegistrationAttribute`。所有特性都可设 `Order`（int）与 `Inherit`（bool）。

### 2.1 内容注册（经 ModContentRegistry，ContentRegistrationAttribute）

| 特性 | 参数 | 附加属性 | 触发时机/行为 |
|---|---|---|---|
| `[RegisterCharacter]` | — | — | 注册为角色模型（Phase ContentPrimary） |
| `[RegisterAct]` | — | — | 注册为阶段模型 |
| `[RegisterMonster]` | — | — | 注册为怪物模型 |
| `[RegisterPower]` | — | — | 注册为能力模型 |
| `[RegisterOrb]` | — | — | 注册为充能球模型 |
| `[RegisterEnchantment]` | — | — | 注册为附魔模型 |
| `[RegisterAffliction]` | — | — | 注册为侵蚀模型 |
| `[RegisterAchievement]` | — | — | 注册为成就模型 |
| `[RegisterSingleton]` | — | — | 注册为单例模型 |
| `[RegisterModelCapability]` | — | `string? StableEntryStem`、`string? FullPublicEntry` | 注册为模型能力（两属性互斥，同时指定报错） |
| `[RegisterDefaultModelCapability(Type targetModelType)]` | 目标模型类型 | `string? ModifierId` | 把标注的能力类型加入目标模型实例的默认能力集；默认派生 ID 为 `{目标类型}_{能力类型}` 的模组作用域 ID |
| `[RegisterGoodModifier]` | — | `int ModifierListSortOrder` | 注册为正面每日特效；负值插到当前正面列表段之前，非负值之后 |
| `[RegisterBadModifier]` | — | `int ModifierListSortOrder` | 注册为负面每日特效；插入语义同上 |
| `[RegisterMutuallyExclusiveModifierGroup(params Type[] memberTypes)]` | 组内其他特效类型 | `Type[] MemberTypes` | 为自定义/每日挑战注册互斥特效组（标注类型若为具体特效会并入组，组内至少 2 个类型） |
| `[RegisterSharedCardPool]` | — | — | 注册为共享卡牌池 |
| `[RegisterSharedRelicPool]` | — | — | 注册为共享遗物池 |
| `[RegisterSharedPotionPool]` | — | — | 注册为共享药水池 |
| `[RegisterSharedEvent]` | — | — | 注册为共享事件 |
| `[RegisterSharedAncient]` | — | — | 注册为共享先古之民事件 |
| `[RegisterGlobalEncounter]` | — | — | 注册为全局遭遇 |

### 2.2 池类条目注册（ModelPublicEntryRegistrationAttributeBase : ContentRegistrationAttribute）

| 特性 | 参数 | 附加属性（基类） | 触发时机/行为 |
|---|---|---|---|
| `[RegisterCard(Type poolType)]` | 目标卡牌池类型 | `Type PoolType`；`string? StableEntryStem`、`string? FullPublicEntry` | 把标注类型注册进指定卡牌池；`PoolType` 必须是 `AbstractModel` 具体子类；Stem 与 Full 互斥，都不填则用类型名 |
| `[RegisterRelic(Type poolType)]` | 目标遗物池类型 | 同上 | 注册进指定遗物池 |
| `[RegisterPotion(Type poolType)]` | 目标药水池类型 | 同上 | 注册进指定药水池 |
| `[RegisterTrashHeapCard]` | — | — | 注册为“垃圾堆”事件 Grab 选项候选（要求 `CardModel` 具体子类，Phase ContentSecondary） |
| `[RegisterTrashHeapRelic]` | — | — | 注册为“垃圾堆”事件 Dive In 选项候选（要求 `RelicModel` 具体子类） |

### 2.3 角色初始内容（CharacterStarterRegistrationAttributeBase : ContentRegistrationAttribute）

| 特性 | 参数 | 触发时机/行为 |
|---|---|---|
| `[RegisterCharacterStarterCard(Type characterType, int count = 1)]` | 目标角色类型、份数 | 把标注卡牌注册为角色初始卡牌（依赖该角色已注册 + 卡牌已注册） |
| `[RegisterCharacterStarterRelic(Type characterType, int count = 1)]` | 同上 | 初始遗物 |
| `[RegisterCharacterStarterPotion(Type characterType, int count = 1)]` | 同上 | 初始药水 |

### 2.4 阶段限定内容（ActScopedRegistrationAttributeBase : ContentRegistrationAttribute）

| 特性 | 参数 | 触发时机/行为 |
|---|---|---|
| `[RegisterActEncounter(Type actType)]` | 目标阶段类型（`ActModel` 子类） | 把标注遭遇注册进指定阶段（依赖该 Act 已注册） |
| `[RegisterActEvent(Type actType)]` | 同上 | 把标注事件注册进指定阶段 |
| `[RegisterActAncient(Type actType)]` | 同上 | 把标注先古事件注册进指定阶段 |

### 2.5 关键词 / 卡牌标签（AutoRegistrationAttribute）

| 特性 | 参数 | 附加属性 | 触发时机/行为 |
|---|---|---|---|
| `[RegisterOwnedKeyword(string localKeywordStem)]` | 本地关键词词干 | `TitleTable`（默认 `"card_keywords"`）、`string? TitleKey`、`string? DescriptionTable`、`string? DescriptionKey`、`string? IconPath`、`ModKeywordCardDescriptionPlacement CardDescriptionPlacement`（默认 None）、`bool IncludeInCardHoverTip`（默认 true） | 注册归属模组的关键词（Phase Keywords） |
| `[RegisterOwnedCardKeyword(string localKeywordStem)]` | 同上 | `string? IconPath`、`CardDescriptionPlacement`、`IncludeInCardHoverTip` | 按游戏卡牌关键词本地化约定注册归属关键词 |
| `[RegisterOwnedCardTag(string localCardTagStem)]` | 本地 CardTag 词干 | — | 注册归属模组的自定义 `CardTag` ID（经 `GetQualifiedCardTagId` 与 mod ID 组合，Phase CardTags） |

### 2.6 时间线（Timeline / TimelineLayout，AutoRegistrationAttribute）

| 特性 | 参数 | 触发时机/行为 |
|---|---|---|
| `[RegisterEpoch]` | — | 注册为时间线历史节点（Phase Timeline） |
| `[RegisterStory]` | — | 注册为时间线故事 |
| `[RegisterStoryEpoch(Type storyType)]` | 目标故事类型 | 把标注历史节点加入指定故事 |
| `[AutoTimelineSlot(EpochEra era)]` | 目标历史时期 | 放入该时期时间线列的第一个空位（要求 `ModEpochTemplate` 子类，Phase TimelineLayout） |
| `[AutoTimelineSlotBeforeColumn(EpochEra anchorEra)]` | 锚点时期 | 放到锚点时期列之前最近的空闲列 |
| `[AutoTimelineSlotBeforeEpochColumn(Type referenceEpochType)]` | 参考历史节点类型（`EpochModel` 子类） | 放到参考节点列之前最近的空闲列 |
| `[AutoTimelineSlotAfterColumn(EpochEra anchorEra)]` | 锚点时期 | 放到锚点时期列之后最近的空闲列 |
| `[AutoTimelineSlotAfterEpochColumn(Type referenceEpochType)]` | 参考历史节点类型 | 放到参考节点列之后最近的空闲列 |
| `[AutoTimelineSlotInColumn(EpochEra anchorEra)]` | 锚点时期 | 放入锚点时期所在列 |
| `[AutoTimelineSlotInEpochColumn(Type referenceEpochType)]` | 参考历史节点类型 | 与参考节点共享列 |

### 2.7 先古映射（AncientMappings，AutoRegistrationAttribute）

| 特性 | 参数 | 触发时机/行为 |
|---|---|---|
| `[RegisterArchaicToothTranscendence(Type ancientCardType)]` | 先古卡牌类型（`CardModel` 子类） | “古老牙齿”把标注初始卡牌转化为先古卡牌的超越映射（标注类型也须为 `CardModel` 子类） |
| `[RegisterDustyTomeCard(Type characterType)]` | 角色类型（`CharacterModel` 子类） | 把标注先古卡牌注册为指定角色“尘封魔典”优先候选 |
| `[RegisterTouchOfOrobasRefinement(Type upgradedRelicType)]` | 升级遗物类型（`RelicModel` 子类） | “欧洛巴斯之触”把标注初始遗物精炼为升级遗物的映射 |

### 2.8 历史节点解锁 / 内容门控（Unlocks / TimelineLayout，AutoRegistrationAttribute）

| 特性 | 参数 | 触发时机/行为 |
|---|---|---|
| `[RegisterEpochCards(params Type[] cardTypes)]` | 卡牌类型数组（`CardModel` 子类） | 为标注历史节点注册显式解锁卡牌，并要求先揭示该节点（标注类型须为 `EpochModel` 子类） |
| `[RequireAllCardsInPool(Type poolType)]` | 卡牌池类型（`CardPoolModel` 子类） | 要求先揭示标注历史节点，才能解锁该池中全部已注册卡牌 |
| `[RegisterEpochRelicsFromPool(Type poolType)]` | 遗物池类型（`RelicPoolModel` 子类） | 把该遗物池所有遗物注册为标注历史节点的解锁内容并门控 |
| `[RequireEpoch(Type epochType)]` | 所需历史节点类型 | 要求先揭示指定节点才能解锁标注内容类型（标注内容类型须已注册） |
| `[UnlockEpochAfterRunAs(Type epochType)]` | 目标历史节点 | 用标注角色完成任意一局后揭示节点 |
| `[UnlockEpochAfterWinAs(Type epochType)]` | 目标历史节点 | 用标注角色通关后揭示节点 |
| `[UnlockEpochAfterAscensionWin(Type epochType, int ascensionLevel)]` | 目标节点、最低进阶等级 | 在 ≥ 该进阶等级通关后揭示 |
| `[UnlockEpochAfterEliteVictories(Type epochType, int requiredEliteWins = 15)]` | 目标节点、精英数（默认 15） | 击败足够精英后揭示 |
| `[UnlockEpochAfterBossVictories(Type epochType, int requiredBossWins = 15)]` | 目标节点、Boss 数（默认 15） | 击败足够 Boss 后揭示 |
| `[UnlockEpochAfterAscensionOneWin(Type epochType)]` | 目标历史节点 | 进阶 1 通关后揭示 |
| `[RevealAscensionAfterEpoch(Type epochType)]` | 目标历史节点 | 节点揭示后显示标注角色的进阶界面 |
| `[UnlockCharacterAfterRunAs(Type epochType)]` | 目标历史节点 | 通过标注角色的局后角色解锁检查授予该节点 |

### 2.9 模组牌堆（CardPiles，AutoRegistrationAttribute）

| 特性 | 参数 | 附加属性 | 触发时机/行为 |
|---|---|---|---|
| `[RegisterOwnedCardPile(string localPileStem)]` | 本地牌堆词干 | `ModCardPileScope Scope`（默认 CombatOnly）、`ModCardPileUiStyle Style`（默认 Headless）、`ModCardPileAnchorKind AnchorKind`（默认 StyleDefault）、`float AnchorOffsetX/Y`、`float AnchorCustomX/Y`、`float AnchorCustomPivotX/Y`、`string? IconPath`（`res://...`）、`string[]? Hotkeys`、`bool CardShouldBeVisible`、`ModExtraHandLayoutDirection ExtraHandDirection`、`float ExtraHandSpacing`（默认 110）、`float ExtraHandCardScale`（默认 0.65）、`float ExtraHandHoverScale`（默认 1）、`bool ExtraHandShowPlayableGlow`（默认 true）、`bool ExtraHandAllowCardPlay`（默认 true）、`float HoverTipOffsetX/Y`、`ModCardPileHoverTipPlacement HoverTipPlacement` | 经 `ModCardPileRegistry` 注册归属牌堆（Phase CardPiles）。标注类型实现 `IModCardPileHandler` 时经公共无参构造创建实例并接入 `OnOpen`。悬停提示本地化键：`static_hover_tips.json` 中 `{id}.title` / `{id}.description` / `{id}.empty` |

### 2.10 顶栏按钮（TopBarButtons，AutoRegistrationAttribute）

| 特性 | 参数 | 附加属性 | 触发时机/行为 |
|---|---|---|---|
| `[RegisterOwnedTopBarButton(string localButtonStem)]` | 本地按钮词干 | `string? IconPath`、`int ButtonOrder`（越小越靠近原版牌组按钮）、`float OffsetX/Y` | 经 `ModTopBarButtonRegistry` 注册归属按钮（Phase TopBarButtons）。标注类**必须**实现 `IModTopBarButtonHandler`（无参构造），其 `OnClick`/`IsVisible`/`IsOpen`/`GetCount` 自动接入回调。悬停提示键：`{id}.title` / `{id}.description` |

### 2.11 节点挂载（NodeAttachments，AutoRegistrationAttribute）

| 特性 | 参数 | 附加属性（基类） | 触发时机/行为 |
|---|---|---|---|
| `[RegisterNodeAttachment(Type parentType, string localId)]` | 父节点类型、本地挂载 ID | 基类属性见下；`Type? NodeType`（省略时标注类型本身为子节点类型） | 父节点 `_Ready` 生命周期挂载由工厂或无参构造创建的子节点（Phase NodeAttachments）。标注类型可实现 `INodeAttachmentFactory`/`INodeAttachmentSetup` |
| `[RegisterNodeAttachmentFromScene(Type parentType, string localId, string scenePath)]` | 父类型、ID、Godot 场景路径 | 基类属性；`Type? NodeType`；`string ScenePath` | 直接 `PackedScene` 实例化挂载 |
| `[RegisterNodeAttachmentFromConvertedScene(Type parentType, string localId, string scenePath)]` | 同上 | 同上 | 经 RitsuLib 节点工厂转换场景后挂载 |

基类 `RegisterNodeAttachmentAttributeBase(Type parentType, string localId)` 附加属性：`string? NodeName`、`bool UniqueNameInOwner`、`bool IncludeDerivedParentTypes`（默认 true）、`NodeAttachmentDuplicatePolicy DuplicatePolicy`（默认 AllowDuplicateName）、`NodeAttachmentAddMode AddMode`（默认 AddChildSafely）、`NodeAttachmentSetupTiming SetupTiming`（默认 BeforeAdd）、`int ChildIndex`（默认 -1 不指定）、`string? InsertBeforeName`、`string? InsertAfterName`、`bool QueueFreeReplacedNode`（默认 true）。

### 2.12 本地化（Localization，AutoRegistrationAttribute）

| 特性 | 参数 | 触发时机/行为 |
|---|---|---|
| `[RegisterSmartFormatter]` | — | 注册为 SmartFormat 格式化器（标注类型须实现 `SmartFormat.Core.Extensions.IFormatter`） |
| `[RegisterSmartFormatSource]` | — | 注册为 SmartFormat 选择器源（标注类型须实现 `ISource`） |

### 2.13 跨模组互操作标记（STS2RitsuLib.Interop，见 1.1）

| 特性 | 可标注目标 | 参数 | 触发时机/行为 |
|---|---|---|---|
| `[ModInterop(string modId, string? type = null)]` | Class（Inherited=false，不允许多个） | 目标 mod 清单 ID、默认目标 CLR 类型名 | 模组加载后（`LocManager.Initialize` 前缀）由内部发射器生成 Harmony 转译，把存根的 public 静态/实例方法、属性、嵌套 `InteropClassWrapper` 转发到目标 mod 程序集 |
| `[AssemblyInterop(string? type = null)]` | Class | 默认程序集限定类型名 | 同上，但按程序集限定名解析目标类型 |
| `[InteropTarget(string type, string? name)]` / `(string? name)` | Class \| Method \| Property | 目标类型、远端成员名（可只覆盖成员名） | 覆盖单成员的目标类型/远端名 |
| `[InteropAnyParam]` | Parameter | — | 方法参数通配：匹配任意目标参数类型 |

---

## 3. 关键用法模式

### 3.1 入口初始化（模组初始化器）

```csharp
// 在 mod 初始化器中：
ModTypeDiscoveryHub.RegisterContributor(myContributor);   // 可选：自定义发现贡献器（须在管线运行前）
ModTypeDiscoveryHub.RegisterModAssembly("my_mod_id", typeof(MyMod).Assembly); // 建立 mod→程序集关联（供 AutoRegister 与互操作解析）
```
- 类型发现管线在 `LocManager.Initialize` 的前缀补丁（`ModTypeDiscoveryPatch`）中**只运行一次**，先于后续游戏系统消费本地化数据；随后刷新延迟内容包。
- 内置贡献器（由 `RitsuLibFramework` 注册）：`ModInteropTypeDiscoveryContributor`（互操作存根重写）、`SavedAttachedStateTypeDiscoveryContributor`（静态 `SavedAttachedState<,>` 字段触发）、`AttributeAutoRegistrationTypeDiscoveryContributor`（本页全部特性）。

### 3.2 自动注册流程（AttributeAutoRegistrationTypeDiscoveryContributor）

1. **扫描**：对每个关联程序集（按 modId、程序集名排序去重）用 `GetLoadableTypes` 取所有可加载类型，跳过 abstract/interface；每个程序集全局只处理一次。
2. **归集**：沿继承链（基类→派生）收集 `AutoRegistrationAttribute`；派生类型上非 `Inherit` 的基类声明被跳过；每个逻辑槽位（特性类型+作用域，如 `PoolType`、`CharacterType`、`LocalStem`）最近声明胜出，同层同槽重复声明直接抛错；不同目标 ID/作用域的注册累加。
3. **解析归属**：`[RitsuLibOwnedBy]`（标注在声明类型上，含继承注册）→ ModManager/mod_manifest.json 程序集归属 → `ModTypeDiscoveryHub.RegisterModAssembly` → 兜底查 `modAssembliesByManifestId`；解析不到则跳过并告警。
4. **排序执行**：确定性排序（modId → 程序集 → Phase → Order → 类型 → 签名）后再做依赖拓扑排序（`Dependencies`/`ProvidedKeys`，如“卡牌先于角色初始卡牌”）；有环则回退稳定排序并告警。单个操作失败不影响其余，失败记入 `RegistrationFreezeDiagnostics`。
5. **阶段顺序**（`AutoRegistrationPhase`，内部枚举，仅示意）：ContentPrimary → ContentSecondary → AncientMappings → Keywords → CardTags → CardPiles → TopBarButtons → NodeAttachments → TimelineLayout → Timeline → Unlocks → Localization。

### 3.3 声明式注册示例

```csharp
// 卡牌：注册进自定义卡牌池（也用 [RegisterRelic]/[RegisterPotion] 同构）
[RegisterCard(typeof(MyCardPool), StableEntryStem = "my_card_stem")]
public sealed class MyCard : CardModel { ... }

// 角色初始卡（count 份）+ 关键词
[RegisterCharacterStarterCard(typeof(MyCharacter), count = 5)]
[RegisterOwnedKeyword("my_keyword", CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.Inline)]
public sealed class StarterCard : CardModel { ... }

// 每日特效 + 互斥组
[RegisterGoodModifier(ModifierListSortOrder = -10)]
[RegisterMutuallyExclusiveModifierGroup(typeof(OtherModifier))]
public sealed class MyModifier : ModifierModel { ... }

// 顶栏按钮（必须实现 IModTopBarButtonHandler，无参构造）
[RegisterOwnedTopBarButton("my_button", IconPath = "res://my_mod/icons/btn.png", ButtonOrder = 100)]
public sealed class MyButtonHandler : IModTopBarButtonHandler { public void OnClick() { } ... }

// 时间线历史节点 + 自动占位 + 解锁条件
[RegisterEpoch]
[AutoTimelineSlotBeforeColumn(EpochEra.Act1)]
[UnlockEpochAfterWinAs(typeof(MyCharacter))]
public sealed class MyEpoch : EpochModel { ... }
```

### 3.4 生命周期订阅

- 自动注册内容在**本地化初始化早期**（`LocManager.Initialize`）生效——不要在主菜单已构建后依赖它。
- 动态内容（牌堆打开、顶栏点击、节点挂载）通过实现对应接口由 RitsuLib 在注册时实例化并接线：`IModCardPileHandler.OnOpen`、`IModTopBarButtonHandler`（`OnClick`/`IsVisible`/`IsOpen`/`GetCount`，`GetCount` 返回 -1 隐藏角标）、`INodeAttachmentFactory`/`INodeAttachmentSetup`。
- ModData 互操作：`ModDataRuntimeInterop.RegisterProviderType<TProvider>()`（或 `RegisterProviderTypeAndTryRegister<TProvider>()` 立即注册架构）；运行时通过 `ReflectionInteropConvention.ModData` 约定的 `GetRitsuLibModDataValue(key)` / `SetRitsuLibModDataValue(key, value)` 等静态方法双向同步；`KeyedJsonDomTransport` 负责在通道与 JSON 树间拉/推（支持 MergePatch → JsonPatch → 根对象 → Pointer 子树 → 全文 → 对象回退的优先级链）。

### 3.5 跨模组互操作存根（[ModInterop]）

```csharp
[ModInterop("other_mod_id", type = "OtherMod.SomeApi")]
public static class MyInterop
{
    public static bool IsAvailable() => false;              // 存根方法，运行时转发
    public static string GetName([InteropAnyParam] object arg) => ""; // 参数通配匹配远端重载

    [InteropTarget("OtherMod.InnerApi")]                    // 覆盖目标类型
    public static int GetCount() => 0;

    public sealed class Wrapper : InteropClassWrapper       // 实例包装：构造/成员转发到远端实例
    {
        public Wrapper(int seed) { }
        public int Next() => 0;
    }
}
```
要点：存根成员与目标成员按名称+参数匹配（可 `[InteropTarget(name: "...")]` 改名）；参数 `[InteropAnyParam]` 可匹配任意远端参数类型；`[ModInterop]` 要求目标 mod 已加载（未加载则该类型静默跳过），`[AssemblyInterop]` 不依赖 mod 加载、用程序集限定名；两者不能同时标注。

---

## 4. 备注

- 命名空间汇总：互操作标记与通道在 `STS2RitsuLib.Interop`；全部注册特性在 `STS2RitsuLib.Interop.AutoRegistration`；ModData 互操作在 `STS2RitsuLib`（`ModDataRuntimeInterop`）与 `STS2RitsuLib.Utils.Persistence.Interop`。
- 内部实现（`ModInteropEmitter`、`AutoRegistrationOperation(Comparer)`、`AssemblyTypeScanHelper`、各 `Patches/*`、`RuntimeModDataStubPatcher` 等）仅作机制理解，mod 作者不应直接引用。
- 本页依据 `src\Interop\` 下全部 `.cs`（排除生成类）的 public 签名与核心实现整理。
