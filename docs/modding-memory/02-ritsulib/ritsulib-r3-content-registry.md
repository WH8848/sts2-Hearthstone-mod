# RitsuLib Content 内容注册系统（src/Content）参考

> 源码：`STS2-RitsuLib/src/Content/`，命名空间 **`STS2RitsuLib.Content`**（补丁类在 `STS2RitsuLib.Content.Patches`）。
> 本目录不包含任何自定义 Attribute；`[RegisterCard]` 等声明式特性定义在 **`STS2RitsuLib.Interop.AutoRegistration`**，通过同一套 `ModContentRegistry` API 派发，一并收录。

## 一、公共 API 清单

### 1. 核心注册表 `ModContentRegistry`（`public sealed partial class`）

按 modId 隔离的注册中心；所有注册方法都是实例方法，通过 `RitsuLibFramework.GetContentRegistry(modId)`（或 `ModContentRegistry.For(modId)`）获取。

**获取 / 状态：**

| 成员 | 签名 | 用途 |
|---|---|---|
| 获取注册表 | `public static ModContentRegistry For(string modId)` | 按 mod ID 获取（首次创建） |
| 注册状态 | `public static bool IsFrozen` / `public static ContentRegistrationState State` | 全局是否已冻结（`Open`/`Frozen`） |
| 所属查询 | `public static bool TryGetOwnerModId(Type modelType, out string modId)` | 查某模型类型由哪个 mod 注册 |
| 固定公共条目 | `public static bool TryGetFixedPublicEntry(Type modelType, out string entry)` | 显式覆盖或自动生成的 `MOD_CATEGORY_TYPENAME` 条目 |
| 快照 | `public static ModContentRegisteredTypeSnapshot[] GetRegisteredTypeSnapshots()` | 诊断用注册快照（modId/类型/ModelDbId/条目） |
| 池查询 | `public static IReadOnlyList<Type> GetRegisteredModelsInPool(string modId, Type poolType)` | 某 mod 注册进某池的全部模型类型 |

**ID 构造工具（static，mod 制作常用）：**

- `GetFixedPublicEntry(string modId, Type modelType)` → 默认 `MOD_CATEGORY_TYPENAME`（规范化大写）。
- `GetCompoundId(string modId, string typeStem, string nameStem)` → `{MOD}_{TYPE}_{NAME}`（type 段仅大写化，其余规范化）。
- `GetQualifiedKeywordId / GetQualifiedCardPileId / GetQualifiedCardTagId / GetQualifiedRewardId / GetQualifiedTargetTypeId / GetQualifiedModelCapabilityId / GetQualifiedTopBarButtonId / GetQualifiedRightClickId` → 各按 `MODID_{TYPE}_{STEM}` 约定生成稳定 ID（卡牌池 ID 会用作 `static_hover_tips.json` 的 `.title/.description/.empty` 键词干）。
- `NormalizePublicStem(string value)` → 大写、非字母数字转 `_`、拆分缩写/驼峰边界。

**池类模型注册（卡牌/遗物/药水）：** 每个都有 `<TPool,TModel>()`、`(Type,Type)`、`(…, ModelPublicEntryOptions)` 三种重载。

```csharp
public void RegisterCard<TPool, TCard>() where TPool : CardPoolModel where TCard : CardModel;
public void RegisterCard(Type poolType, Type cardType, ModelPublicEntryOptions publicEntry);
public void RegisterRelic<TPool, TRelic>(...)   // RelicPoolModel / RelicModel
public void RegisterPotion<TPool, TPotion>(...) // PotionPoolModel / PotionModel
```

**独立模型注册（纳入 ModelDb 各列表）：**

```csharp
public void RegisterCharacter<TCharacter>() where TCharacter : CharacterModel; // ModelDb.AllCharacters
public void RegisterAct<TAct>() where TAct : ActModel;                          // ModelDb.Acts（不进随机章节列表，需 IModActRandomListPolicy 自选入）
public void RegisterMonster<TMonster>() where TMonster : MonsterModel;          // ModelDb.Monsters
public void RegisterPower<TPower>() where TPower : PowerModel;                  // ModelDb.AllPowers
public void RegisterOrb<TOrb>() where TOrb : OrbModel;                          // ModelDb.Orbs
public void RegisterEnchantment<TEnchantment>() where TEnchantment : EnchantmentModel; // ModelDb.DebugEnchantments
public void RegisterAffliction<TAffliction>() where TAffliction : AfflictionModel;     // ModelDb.DebugAfflictions
public void RegisterAchievement<TAchievement>() where TAchievement : AchievementModel; // ModelDb.Achievements
public void RegisterSingleton<TSingleton>() where TSingleton : SingletonModel;         // ModelDb.Singleton<T> 注入
public void RegisterBadge<TBadge>() where TBadge : ModBadgeTemplate;                    // 自定义徽章模板
```

**角色初始内容（starter）：** 匹配按运行时 CLR 类型 + 祖先注册（仅以 `CharacterModel` 为键的除外）；`order` 控制排序，`count` 为份数。

```csharp
public void RegisterCharacterStarterCard<TCharacter, TCard>(int count = 1);
public void RegisterCharacterStarterCard<TCharacter, TCard>(int count, int order);
public void RegisterCharacterStarterRelic<TCharacter, TRelic>(int count = 1);  // (int count, int order)
public void RegisterCharacterStarterPotion<TCharacter, TPotion>(int count = 1); // (int count, int order)
```

**章节作用域 / 全局内容：**

```csharp
public void RegisterActEncounter<TAct, TEncounter>(); // 某章节专属遭遇（EncounterModel）
public void RegisterGlobalEncounter<TEncounter>();    // 追加到每个章节 GenerateAllEncounters 末尾
public void RegisterActEvent<TAct, TEvent>();         // 某章节专属事件（EventModel）
public void RegisterSharedEvent<TEvent>();            // ModelDb.AllSharedEvents
public void RegisterSharedAncient<TAncient>();        // AncientEventModel 共享先古事件
public void RegisterActAncient<TAct, TAncient>();     // 章节作用域先古事件
public void RegisterAncientOption<TAncient>(ModAncientOptionRule rule); // 先古初始选项规则
```

**每日修饰符（Daily Modifier）：**

```csharp
public void RegisterGoodModifier<TModifier>(int modifierListSortOrder = 0); // 负值插列表前，非负插后
public void RegisterBadModifier<TModifier>(int modifierListSortOrder = 0);
public void RegisterMutuallyExclusiveModifierGroup(params Type[] modifierTypes); // 至少 2 个不同类型
```

**模型能力（Model Capability）：**

```csharp
public void RegisterModelCapability<TCapability>(ModelPublicEntryOptions publicEntry = default) where TCapability : ModelCapability;
public void ConfigureDefaultModelCapabilities<TModel>(string modifierId, Action<TModel, ModelCapabilityList> modifier, int order = 0) where TModel : AbstractModel;
public void ConfigureDefaultModelCapabilities(Type modelType, string modifierId, Action<AbstractModel, ModelCapabilityList> modifier, int order = 0);
```

**章节进入解析（替换一局中的章节槽位）：**

```csharp
public static bool HasAnyActEnterRegistration { get; }
public void RegisterActEnterForce<TAct>(int slotIndex, int priority, Func<ActEnterResolveContext, bool> eligibility); // 高优先级胜出，同优先级先注册胜出
public void RegisterActEnterUniformPool(int slotIndex);                 // 先声明池，再加候选
public void RegisterActEnterUniformPoolCandidate<TAct>(int slotIndex, Func<ActEnterResolveContext, bool> eligibility);
public void RegisterActEnterWeightedPool(int slotIndex);
public void RegisterActEnterWeightedPoolCandidate<TAct>(int slotIndex, Func<ActEnterResolveContext, bool> eligibility, Func<ActEnterResolveContext, double> weight);
public void RegisterActEnterWeightedPoolBaseline(int slotIndex, Func<ActEnterResolveContext, double> weight); // 加权池无隐式基线，须显式注册
```

**资源替换（Asset Replacement，运行时生效）：**

```csharp
public void RegisterCardPoolAssetReplacement(string cardPoolEntry, CardPoolAssetProfile assetProfile);
public void RegisterCardPoolAssetReplacement<TPool>(CardPoolAssetProfile assetProfile);
public bool RemoveCardPoolAssetReplacement(string cardPoolEntry);  // 含 <TPool> 重载
public void RegisterGlobalCharacterAssetReplacement(CharacterAssetProfile assetProfile); // 所有角色
public void RegisterCharacterAssetReplacement(string characterEntry, CharacterAssetProfile assetProfile);
public bool ClearGlobalCharacterAssetReplacement();
public bool RemoveCharacterAssetReplacement(string characterEntry);
// “角色拥有某物”时替换视觉（编程式 owned-visual 覆盖；注册表替换优先于它）：
public void RegisterCharacterOwnedRelicVisualOverride(string characterEntry, string relicModelIdEntry, RelicAssetProfile assets); // + <TCharacter,TRelic> 重载
public void RegisterCharacterOwnedPotionVisualOverride(...); // + <TCharacter,TPotion>
public void RegisterCharacterOwnedCardVisualOverride(...);   // + <TCharacter,TCard>
public static string NormalizeCharacterAssetEntryKey(string characterEntry);
public static string NormalizeOwnedModelIdEntry(string modelIdEntry);
```
> 分层合并：每 mod 一层按写入序合并，非空字段后注册优先；角色专用替换 > 全局替换。

**卡牌库图鉴筛选器（Compendium Filter）：**

```csharp
public void RegisterCardLibraryCompendiumSharedPoolFilter<TPool>(string stableId, string iconTexturePath);
public void RegisterCardLibraryCompendiumSharedPoolFilter<TPool>(string stableId, string iconTexturePath, IReadOnlyList<CardLibraryCompendiumPlacementRule>? placementRules);
// stableId 仅允许 ASCII 字母/数字/下划线（Godot 节点名安全），全局唯一
```

**手牌发光 / 描边：**

```csharp
public void RegisterCardHandGlow<TCard>(ModCardHandGlowRules rules) where TCard : CardModel; // 金/红光规则，多次注册按 OR 合并
public void RegisterCardHandOutline<TCard>(ModCardHandOutlineRules<TCard> rules) where TCard : CardModel;
public void RegisterCardHandOutline<TCard>(ModCardHandOutlineSwitchRule<TCard> rule);
public void RegisterCardHandOutline<TCard>(params ModCardHandOutlineSwitchRule<TCard>[] rules);
public void RegisterCardHandOutline<TCard>(Func<TCard, Color?> colorWhen, int priority = 0, bool visibleWhenUnplayable = false, bool refreshEveryFrame = true); // 最高优先级规则生效
```

**占位符内容（无需手写模型类，Reflection.Emit 生成子类）：**

```csharp
public void RegisterPlaceholderCard<TPool>(string stableEntryStem, PlaceholderCardDescriptor descriptor = default) where TPool : CardPoolModel;
public void RegisterPlaceholderCard<TPool>(ModelPublicEntryOptions publicEntry, PlaceholderCardDescriptor descriptor);
public void RegisterPlaceholderRelic<TPool>(string stableEntryStem, PlaceholderRelicDescriptor descriptor = default); // + options 重载
public void RegisterPlaceholderPotion<TPool>(string stableEntryStem, PlaceholderPotionDescriptor descriptor = default); // + options 重载
```

**Trash Heap（垃圾堆）事件：**

```csharp
public void RegisterTrashHeapCard<TCard>() where TCard : CardModel;   // Grab 选项候选
public void RegisterTrashHeapRelic<TRelic>() where TRelic : RelicModel; // Dive In 选项候选
```

### 2. 辅助类型（`STS2RitsuLib.Content`）

| 类型 | 说明 |
|---|---|
| `enum ContentRegistrationState { Open, Frozen }` | 注册是否仍开放 |
| `readonly record struct ModelPublicEntryOptions` | 公共条目规则：`FromTypeName`（默认，`<MOD>_<CATEGORY>_<CLR类型名>`）、`FromStem(string)`（`<MOD>_<CATEGORY>_<STEM>`）、`FromFullPublicEntry(string)`（规范化完整条目） |
| `readonly record struct ActEnterResolveContext(RunManager, RunState, int EnteringActIndex, Rng, UnlockState, bool IsMultiplayer)` | 章节进入解析上下文（eligibility/weight 回调参数） |
| `enum ActEnterPoolModeKind { Uniform, Weighted }` | 章节进入池模式 |
| `readonly record struct ModContentRegisteredTypeSnapshot` | `ModId, ModelType, ModelDbId, ExpectedPublicEntry, HasExplicitPublicEntryOverride, TypeNamePublicEntry` |
| `readonly record struct PlaceholderCardDescriptor(int BaseCost=1, CardType Type=Skill, CardRarity Rarity=Token, TargetType Target=None, bool ShowInCardLibrary=false)` | 占位卡牌属性 |
| `readonly record struct PlaceholderRelicDescriptor(...)` | 占位遗物属性（Rarity/IsUsedUp/HasUponPickupEffect/SpawnsPets/IsStackable/AddsPet/ShowCounter/DisplayAmount/IncludeEnergyHoverTip/MerchantCostOverride/AlwaysAllowedInRun/FlashSfx/ShouldFlashOnPlayer） |
| `readonly record struct PlaceholderPotionDescriptor(Rarity=Common, Usage=AnyTime, Target=None, CanBeGeneratedInCombat=true, PassesCustomUsabilityCheck=true)` | 占位药水属性 |
| `static class VanillaCharacterIds`（`ModContentRegistry` 内嵌） | `Ironclad / Silent / Defect / Regent / Necrobinder` 常量（用于资源替换键） |
| `static class CardLibraryCompendiumVanillaFilterNames` | 原版图鉴筛选器 Godot 唯一节点名：`%IroncladPool %SilentPool %DefectPool %RegentPool %NecrobinderPool %ColorlessPool %AncientsPool %MiscPool` + `AllInStripOrder` |
| `sealed class CardLibraryCompendiumPlacementRule` | 放置偏好：`VanillaFilterAnchorUniqueName / ModCharacterModelIdEntry / ModSharedCompendiumFilterStableId` 三选一 + `Relation`（`enum CardLibraryCompendiumFilterInsertRelation { Before, After }`） |
| `static class CardLibraryCompendiumPlacementDefaults` | `DefaultCharacterRowRules`：依次尝试放在 Colorless、Ancients、Misc 之前 |
| `sealed class CardLibraryCompendiumSharedPoolFilterRegistration` | `OwningModId / StableId / IconTexturePath / CardPoolType / PlacementRules` |

### 3. 内容来源（Content Source）

```csharp
// STS2RitsuLib.Content
public readonly record struct ContentSourceDescriptor(string modId, string? displayName = null); // 原版用 "Vanilla"
public interface IContentSourceSupplier { ContentSourceDescriptor ContentSource { get; } }        // 模型实现以覆盖来源显示
public static class ContentSourceResolver
{
    public static ContentSourceDescriptor Resolve(AbstractModel model);
    public static ContentSourceDescriptor Resolve(Type modelType); // 必须派生自 AbstractModel
}
```
> 用途：让卡牌/遗物/药水等的悬停提示显示来源 mod；模型可实现 `IContentSourceSupplier` 自报来源，否则按程序集/注册归属推断。显示样式由 RitsuLib 设置控制（`MOD_SETTINGS` 表，键 `ritsulib.modSourceHoverTip.title`）。

## 二、特性（Attribute）清单

> 命名空间 **`STS2RitsuLib.Interop.AutoRegistration`**，标注目标均为 **Class**（`AllowMultiple = true, Inherited = false`）。
> **触发时机**：由 `AttributeAutoRegistrationTypeDiscoveryContributor`（`IModTypeDiscoveryContributor`）在每个 mod 程序集加载时扫描一次，按确定性顺序（同阶段内按 `Order` 升序）翻译成 `ModContentRegistry.For(modId)` 调用执行；全部必须在 `ModelDb.Init` 冻结注册前完成。
> 公共基类：`AutoRegistrationAttribute`（属性 `int Order`、`bool Inherit`——声明在基类且 `Inherit=true` 时可被派生类型继承，最近的声明覆盖同槽位配置）；内容类特性基类 `ContentRegistrationAttribute`。

**池类内容（池类型由构造函数参数指定）：**

| 特性 | 参数 | 说明 |
|---|---|---|
| `[RegisterCard(Type poolType)]` | `StableEntryStem`、`FullPublicEntry`（可设属性） | 注册为指定卡牌池中的卡牌 |
| `[RegisterRelic(Type poolType)]` | 同上 | 注册进遗物池 |
| `[RegisterPotion(Type poolType)]` | 同上 | 注册进药水池 |

**独立模型（无参数）：** `[RegisterCharacter]`、`[RegisterAct]`、`[RegisterMonster]`、`[RegisterPower]`、`[RegisterOrb]`、`[RegisterEnchantment]`、`[RegisterAffliction]`、`[RegisterAchievement]`、`[RegisterSingleton]`、`[RegisterSharedCardPool]`、`[RegisterSharedRelicPool]`、`[RegisterSharedPotionPool]`、`[RegisterSharedEvent]`、`[RegisterSharedAncient]`、`[RegisterGlobalEncounter]`、`[RegisterTrashHeapCard]`、`[RegisterTrashHeapRelic]`。

**带参数的独立/关联注册：**

| 特性 | 参数 | 说明 |
|---|---|---|
| `[RegisterModelCapability]` | `StableEntryStem?`、`FullPublicEntry?` | 注册模型能力 |
| `[RegisterDefaultModelCapability(Type targetModelType)]` | `ModifierId?` | 把标注能力类型加入目标模型的默认能力集 |
| `[RegisterGoodModifier]` / `[RegisterBadModifier]` | `int ModifierListSortOrder` | 正面/负面每日修饰符；负值插列表前、非负插后 |
| `[RegisterMutuallyExclusiveModifierGroup(params Type[] memberTypes)]` | 构造参数 | 标注类型 + 成员构成互斥组 |
| `[RegisterCharacterStarterCard(Type characterType, int count = 1)]` | `CharacterType`、`Count` | 初始牌组（Relic/Potion 同理：`[RegisterCharacterStarterRelic]`、`[RegisterCharacterStarterPotion]`） |
| `[RegisterActEncounter(Type actType)]` / `[RegisterActEvent(Type actType)]` / `[RegisterActAncient(Type actType)]` | `ActType` | 章节作用域内容 |

**关联但非 Content 目录的注册特性（同命名空间，供参考）：** `[RegisterOwnedKeyword(string localKeywordStem)]`、`[RegisterOwnedCardKeyword]`、`[RegisterOwnedCardTag(string localCardTagStem)]`（`STS2RitsuLib.CardTags`）、`[RegisterOwnedCardPile(string localPileStem)]`、`[RegisterOwnedTopBarButton(string localButtonStem)]`、时间线/历史节点类（`[RegisterEpoch]`、`[RegisterStory]`、`[RegisterStoryEpoch(Type)]`、`[AutoTimelineSlot*(...)]`、`[RequireEpoch(Type)]`、`[RegisterEpochCards(params Type[])]`、`[RequireAllCardsInPool(Type)]`、`[RegisterEpochRelicsFromPool(Type)]`）、解锁类（`[UnlockEpochAfterRunAs(Type)]`、`[UnlockEpochAfterWinAs(Type)]`、`[UnlockEpochAfterAscensionWin(Type,int)]`、`[UnlockEpochAfterEliteVictories(Type,int=15)]`、`[UnlockEpochAfterBossVictories(Type,int=15)]`、`[UnlockCharacterAfterRunAs(Type)]`、`[RevealAscensionAfterEpoch(Type)]`）、内容联动（`[RegisterArchaicToothTranscendence(Type ancientCardType)]`、`[RegisterDustyTomeCard(Type characterType)]`、`[RegisterTouchOfOrobasRefinement(Type upgradedRelicType)]`）、`[RegisterNodeAttachment...]`、`[RegisterSmartFormatter]` 等。

## 三、关键用法模式

### 1. 程序化注册（Entry / Mod 初始化时）

```csharp
// 在 mod 初始化阶段（ModInitializer 或 BaseLib 式初始化钩子中）：
var registry = RitsuLibFramework.GetContentRegistry("com.example.mymod");

registry.RegisterCard<MyCharacterPool, MyCard>();
registry.RegisterRelic<MyCharacterPool, MyRelic>();
registry.RegisterPower<MyPower>();
registry.RegisterOrb<MyOrb>();
registry.RegisterCharacter<MyCharacter>();
registry.RegisterCharacterStarterCard<MyCharacter, MyStarterCard>(5);   // 5 张
registry.RegisterCharacterStarterRelic<MyCharacter, MyStarterRelic>(1, 0);
registry.RegisterActEncounter<MyAct, MyEncounter>();
registry.RegisterGlobalEncounter<MyEncounter>();
registry.RegisterSharedEvent<MyEvent>();
```

### 2. 声明式注册（特性）

```csharp
[RegisterCard(typeof(MyCharacterPool), StableEntryStem = "fireball")]
[RegisterCharacterStarterCard(typeof(MyCharacter), count = 1)]
public sealed class Fireball : CardModel { /* ... */ }
```

### 3. 注册流程与生命周期（关键时序）

1. **开放期**：mod 程序集加载 → 类型发现（自动注册特性被扫描执行）→ 各 mod 在初始器中调用注册 API。
2. **冻结**：`ModelDb.Init` 的 `Prefix` 中调用 `ModContentRegistry.FreezeRegistrations` → `IsFrozen = true`，此后任何注册调用抛出 `InvalidOperationException`（提示需在 ModelDb 初始化前注册）。同时发布可回放生命周期事件 **`ContentRegistrationClosedEvent(Reason, OccurredAtUtc)`**（`STS2RitsuLib` 命名空间，`IReplayableFrameworkLifecycleEvent`），可用 `RitsuLibFramework.SubscribeLifecycle(observer)` 订阅。
3. **注入**：`ModelDb.Init` 前调用 `InjectDynamicRegisteredModels()`，把动态程序集（Reflection.Emit 占位类型）通过 `ModelDb.Inject(type)` 注入——游戏的子类型扫描看不到动态类型，必须显式注入。
4. **解析/预热**：`ModelDb.Init` 完成后 `WarmResolvedModelCaches()`，按目录（Characters/Acts/Monsters/Powers/Orbs/Shared*/ActScoped* 等）把注册类型解析为模型实例并缓存。
5. **合并**：各 `ModelDb` getter 由 `ModelDbContentPatches`（`STS2RitsuLib.Content.Patches`，PatchId `modeldb_*`）以 `AppendDistinctById`/`MergeDistinctById` 策略并入原版序列（怪物用 MergeDistinctById，其余默认 AppendDistinctById，均按 `AbstractModel.Id` 去重）。

### 4. 公共条目（Public Entry）约定

- 默认规则 `MOD_CATEGORY_TYPENAME`（全大写、`_` 分隔、非字母数字折叠为 `_`、驼峰/缩写拆分）。例：mod `com.example.my-mod`、类型 `MyCard` → `MYMOD_CARD_MY_CARD`。
- 想保持稳定 ID 用 `ModelPublicEntryOptions.FromStem("my_card")`（→ `MYMOD_CARD_MY_CARD`）或 `FromFullPublicEntry("EXACT_ENTRY")`；**一经注册不可变更**（重复注册同类型不同条目抛异常）。
- 跨 mod 引用：`GetQualifiedKeywordId(提供方modId, 本地词干)` 等静态方法生成同一 ID 供对方引用。
- 卡牌池 ID（`GetQualifiedCardPileId`）是 `static_hover_tips.json` 键词干（`.title/.description/.empty`）。

### 5. 占位符（不想写模型类时）

```csharp
registry.RegisterPlaceholderCard<MyPool>("proto_dagger",
    new PlaceholderCardDescriptor(BaseCost: 0, Type: CardType.Attack, Rarity: CardRarity.Common));
// 运行时由 PlaceholderModelTypeEmitter 用 Reflection.Emit 生成 ModPlaceholderCardTemplate 子类并注册
```

### 6. 章节进入替换（Act Enter）

```csharp
// 强制替换：槽位 1 必为 MyAct（优先级高者胜）
registry.RegisterActEnterForce<MyAct>(slotIndex: 1, priority: 100,
    ctx => ctx.RunState.Players.Count == 1);
// 加权池：声明 → 候选（带权重）→ 基线权重
registry.RegisterActEnterWeightedPool(2);
registry.RegisterActEnterWeightedPoolCandidate<MyAct>(2,
    _ => true, ctx => 3.0);
registry.RegisterActEnterWeightedPoolBaseline(2, _ => 1.0);
```

### 7. 资源替换（对角色/卡牌池换皮）

```csharp
registry.RegisterCharacterAssetReplacement(VanillaCharacterIds.Ironclad,
    new CharacterAssetProfile(Ui: new CharacterUiAssetProfile(IconTexturePath: "res://mods/mymod/icons/ironclad.png")));
registry.RegisterCardPoolAssetReplacement<MyPool>(new CardPoolAssetProfile(/* 卡背等 */));
// 注册即生效（RuntimeAssetRefreshCoordinator.Request 触发刷新），可移除
```

## 四、内部实现要点（供理解，非 mod API）

- `ModContentRegistry` 是 `sealed partial`，按 modId 静态字典缓存；全部注册以 `Lock SyncRoot` 保护，重复注册静默跳过；模型类型必须为封闭具体子类（`EnsureModelType`），一类型仅一所有者（`RememberOwner` 跨 mod 抢占会抛异常）。
- 目录定义在 `ModContentRegistry.ContentCatalogs.cs`：`GlobalEntry`/`ScopedEntry` 描述“注册来源 → 解析器 → 合并模式”；`ResolvedModelCache` 按 `Open → Frozen → Resolved` 阶段缓存解析结果。
- `ModelDbGetterMerge` 用 `[ThreadStatic]` 深度计数防嵌套 `ModelDb` 查找导致无限递归。
- `PlaceholderModelTypeEmitter` 按 modId 缓存 `ModuleBuilder`，动态程序集名 `STS2RitsuLib.Placeholders.*`。
- `ModelIdSerializationCacheDynamicContentPatch`：按设置（`ModelDbDeterministicSortMode`）在 `ModelIdSerializationCache.Init` 后以确定性顺序重建网络 ID 映射与哈希，保证多人/存档一致性；非对局模型获得 local-only sort ID（`LocalOnlyModelSortIds`），`LocalOnlyModelIdSortingPatch` 在 `AbstractModel.InitId` 的 Finalizer 中兜底。
- 章节动态补丁（`DynamicActContentPatcher` / `DynamicCharacterStarterContentPatcher`）由 `ModelDb.Init` 的 bootstrap Prefix 触发，对每个已加载 Act/Character 类型生成 Harmony 动态补丁，合并 AllEvents/AllAncients/GenerateAllEncounters/BossDiscoveryOrder/GetUnlockedAncients 与 StartingDeck/StartingRelics/StartingPotions。
