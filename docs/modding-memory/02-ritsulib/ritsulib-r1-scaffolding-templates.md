# RitsuLib Scaffolding（模板与脚手架）— mod 制作实用参考

> 来源：`E:\MOD\sts2\STS2-RitsuLib\src\Scaffolding\`（165 个 .cs 文件），并交叉参考 `Interop.AutoRegistration`（注册特性）与 `RitsuLibFramework`（入口/生命周期）。
> 范围：只收录对 mod 制作有实用价值的公共 API，跳过内部实现、Patches、诊断工具。
> 命名空间：`STS2RitsuLib.Scaffolding.*`；注册特性位于 `STS2RitsuLib.Interop.AutoRegistration`；框架入口 `STS2RitsuLib.RitsuLibFramework`。

---

## 0. 总览：Scaffolding 能帮你做什么

| 子目录 | 用途 |
|---|---|
| `Content` | **核心**：内容模板基类（卡/遗物/药水/能力/怪物/事件…）+ 注册条目 + 流式注册器 `ModContentPackBuilder` + 资源配置 records |
| `Characters` | 角色模板 `ModCharacterTemplate<T1,T2,T3>`、角色资源配置 `CharacterAssetProfile`、原版角色资源替换 |
| `Cards\HandGlow` / `HandOutline` | 自定义手牌金色/红色发光、任意颜色描边规则 |
| `Godot` | 显式 Godot 节点工厂（场景转换）、`_Ready` 节点挂载（NodeAttachment） |
| `Visuals` | 视觉提示集 `VisualCueSet`/帧序列 + 动画状态机 `ModAnimStateMachine`（Spine / 非 Spine 后端） |
| `MonsterMoves` | 怪物行动状态机常用构造（循环、随机分支、条件分支） |
| `Ancients\Options` | 为先古事件（Ancient）追加初始选项 |
| `Combat` | 回合阶段查询等少量扩展 |

两条并行的注册路径：
1. **特性驱动**：给类贴 `[RegisterCard(typeof(MyPool))]` 等特性，RitsuLib 在类型发现阶段自动注册（详见第 2 节）。
2. **代码驱动**：在入口初始化中调用 `RitsuLibFramework` 各 `GetXxxRegistry(modId)`，或用 `ModContentPackBuilder` 流式收集后 `Apply()`（详见第 3 节）。

---

## 1. 公共 API 清单（按目录）

### 1.1 `Scaffolding.Content` — 内容模板与注册（核心）

**模板基类**（继承游戏自带 Model 类型，直接 `new`/注册即可）：

| 类型 | 说明与关键成员 |
|---|---|
| `abstract ModCardTemplate(int baseCost, CardType type, CardRarity rarity, TargetType target, bool showInCardLibrary = true) : CardModel, IModCardAssetOverrides…` | 卡牌基类。`protected virtual IEnumerable<IHoverTip> AdditionalHoverTips`（追加悬浮提示，已 sealed 接入 `ExtraHoverTips`）；`public virtual CardAssetProfile AssetProfile => CardAssetProfile.Empty`；`CustomPortraitPath` 等资源覆盖属性全部由 `AssetProfile` 派生，未配置时自动用 RitsuLib 内嵌占位图 |
| `abstract ModPowerTemplate : PowerModel, IModPowerAssetOverrides` | 能力基类。`protected virtual IEnumerable<string> RegisteredKeywordIds`（仅显示用关键词，经 `ModKeywordRegistry` 解析为悬浮提示）；`protected virtual IEnumerable<IHoverTip> AdditionalHoverTips`；`protected virtual bool IncludeEnergyHoverTip`；`public virtual PowerAssetProfile AssetProfile`；`CustomIconPath`/`CustomBigIconPath` |
| `abstract ModRelicTemplate : RelicModel, IModRelicAssetOverrides` | 遗物基类（悬浮提示/资源覆盖同能力模式） |
| `abstract ModPotionTemplate : PotionModel, IModPotionAssetOverrides` | 药水基类（`IPotionAssetOverrides` 提供图标等） |
| `abstract ModEnchantmentTemplate : EnchantmentModel` / `ModAfflictionTemplate : AfflictionModel` / `ModModifierTemplate : ModifierModel` | 附魔 / 侵蚀 / 每日特效（Modifier）基类 |
| `abstract ModMonsterTemplate : MonsterModel, IModMonsterAssetOverrides, …` | 怪物基类（资源覆盖 + 战斗场景工厂接口） |
| `abstract ModEncounterTemplate : EncounterModel` / `ModEventTemplate : EventModel` / `ModAncientEventTemplate : AncientEventModel` | 遭遇 / 事件 / 先古事件基类（含 `IModEventLayoutPackedSceneFactory` 等布局工厂接口） |
| `abstract ModActTemplate : ActModel, IModActAssetOverrides, IModActRandomListPolicy` | 章节基类 |
| `abstract ModRestSiteOptionTemplate(Player owner)` | 休息处选项基类；接口 `IModRestSiteOptionAssetOverrides`、`IModRestSiteOptionCustomTitle` |
| `abstract ModBadgeTemplate` | 徽章基类 |
| `abstract ModOrbTemplate : OrbModel` | 充能球基类；相关接口 `IModOrbRandomPoolPolicy`、`IModOrbValueDisplayPolicy`；枚举 `ModOrbValueDisplayMode` |
| `abstract ModPlaceholderCardTemplate(...)` / `ModPlaceholderRelicTemplate(...)` / `ModPlaceholderPotionTemplate(...)` | 无 CLR 类型的生成式占位内容基类；**官方建议直接走 `ModContentRegistry.RegisterPlaceholderXxx`，不继承** |
| `abstract TypeListCardPoolModel : CardPoolModel, IModBigEnergyIconPool, IModTextEnergyIconPool`（另有 `TypeListRelicPoolModel`、`TypeListPotionPoolModel`） | 用类型列表声明池内容的池模型；`protected virtual IEnumerable<Type> CardTypes` 返回池内类型 |

**注册器（推荐入口）— `ModContentPackBuilder`**（`STS2RitsuLib.Scaffolding.Content`）：

- `static ModContentPackBuilder For(string modId)` — 创建构建器。
- 流式方法（均返回 builder，可链式）：`Character<TChar>()`、`Character<TChar>(Action<CharacterRegistrationEntry<TChar>> configure)`、`CharacterStarterCard/Relic/Potion<TChar,TItem>(int count=1, int order=0)`、`CharacterAssetReplacement(string characterEntry, CharacterAssetProfile)`、`Act<TAct>()`、`ActEnterForce/UniformPool/WeightedPool...`、`ActEncounter/ActEvent/ActAncient<TAct,TX>()`、`GlobalEncounter<T>()`、`Monster<T>()`、`Card<TPool,TCard>(ModelPublicEntryOptions? = default)`、`CardHandGlow<TCard>(ModCardHandGlowRules)`、`CardHandOutline<TCard>(…规则…)`、`PlaceholderCard/Relic/Potion<TPool>(string stableEntryStem, descriptor=default)`、`Relic/Potion<TPool,T>(…)`、`Power<T>()`、`Orb<T>()`、`Enchantment<T>()`、`Affliction<T>()`、`Achievement<T>()`、`Singleton<T>()`、`GoodModifier/BadModifier<T>(int? sortOrder)`、`MutuallyExclusiveModifierGroup(params Type[])`、`SharedCardPool/RelicPool/PotionPool<T>()`、`SharedEvent<T>()`、`SharedAncient<T>()`、`AncientOption<TAncient>(ModAncientOptionRule)`、`TrashHeapCard<TCard>()`、`TrashHeapRelic<TRelic>()`、`HealthBarForecast<TSource>(string? sourceId)`、`SmartFormatter<T>()`/`SmartFormatSource<T>()`、`KeywordOwned(...)`、`CardKeywordOwnedByLocNamespace(...)`、`CardTagOwned(string)`、`DynamicEnumValue<TEnum>(string)`、`CardPileOwned(...)`、`TopBarButtonOwned(...)`、`Epoch<T>()`/`Story<T>()`/`StoryEpoch<TStory,TEpoch>()`、`ModEpochTimelineSlot`/`ModEpochAutoTimelineSlot*`、`TimelineColumn<TStory>(Action<TimelineColumnBuilder<TStory>>)`、`RequireEpoch<TModel,TEpoch>()`、`BindCardUnlockEpoch<T>()`/`BindRelicUnlockEpoch<T>()`、`EpochCards/EpochRelics/EpochPotions/…FromPool/RequireAll…InPool`、`UnlockEpochAfter*`、`RevealAscensionAfterEpoch`、`UnlockCharacterAfterRunAs`、`ArchaicToothTranscendence<TS,TA>()`、`DustyTomeCard<TChar,TAncient>()`、`TouchOfOrobasRefinement<TS,TR>()`、`Entry/Entries/Keyword(s)/CardTag(s)/CardPile(s)/TopBarButton(s)/PackEntry/PackEntries/Manifest/Custom(Action<ModContentPackContext>)`。
- `ModContentPackContext BuildContext()` — 立即物化上下文但不执行。
- `ModContentPackContext Apply()` — **把全部步骤排入框架的延迟注册窗口**（`RitsuLibFramework.EnqueueDeferredContentPack`），单步失败只记日志不阻断后续；返回已创建的上下文。
- 上下文 `readonly record struct ModContentPackContext(string ModId, ModContentRegistry Content, ModKeywordRegistry Keywords, ModTimelineRegistry Timeline, ModUnlockRegistry Unlocks)`，附带便捷属性 `CardTags`/`CardPiles`/`SmartFormat`/`TopBarButtons`（均按 ModId 解析单例）、`DynamicEnumValues<TEnum>()`。

**注册条目** — `IContentRegistrationEntry`（`void Register(ModContentRegistry registry)`）与 `IModContentPackEntry`（`void Apply(ModContentPackContext context)`）是两种清单条目接口。`ContentRegistrationEntries.cs` 为每种内容提供一个 sealed 条目类：`CharacterRegistrationEntry<TChar>`（可链式 `.AddStartingCard<TCard>(count,order)` / `.AddStartingRelic` / `.AddStartingPotion`）、`CharacterStarterCard/Relic/PotionRegistrationEntry`、`CharacterAssetReplacementRegistrationEntry`、`ActRegistrationEntry`、`CardRegistrationEntry<TPool,TCard>`、`CardHandGlowRegistrationEntry<TCard>`、`CardHandOutlineRegistrationEntry<TCard>`、`RelicRegistrationEntry`、`PotionRegistrationEntry`、`PowerRegistrationEntry`、`HealthBarForecastRegistrationEntry`、`OrbRegistrationEntry`、`EnchantmentRegistrationEntry`、`AfflictionRegistrationEntry`、`AchievementRegistrationEntry`、`SingletonRegistrationEntry`、`GoodModifier/BadModifierRegistrationEntry`、`SharedCardPool/RelicPool/PotionPoolRegistrationEntry`、`SharedEventRegistrationEntry`、`MonsterRegistrationEntry`、`ActEncounter/ActEvent/ActAncientRegistrationEntry`、`GlobalEncounterRegistrationEntry`、`SharedAncientRegistrationEntry`、`AncientOptionRegistrationEntry`、`TrashHeapCard/RelicRegistrationEntry`、`PlaceholderCard/Relic/Potion(FromOptions)RegistrationEntry`、`ArchaicToothTranscendenceByIdRegistrationEntry`、`DustyTomeCard(ById)RegistrationEntry`、`TouchOfOrobasRefinementByIdRegistrationEntry`。`ModPackRegistrationEntries.cs` 提供时间线/解锁类 pack 条目：`EpochPackEntry<T>`、`StoryPackEntry<T>`、`StoryEpochPackEntry`、`RequireEpochPackEntry`、`BindCardUnlockEpochPackEntry`、`BindRelicUnlockEpochPackEntry`、`UnlockEpochAfterRunAs/WinAs/AscensionWin/RunCount/EliteVictories/BossVictories/AscensionOneWinPackEntry`、`RevealAscensionAfterEpochPackEntry`、`UnlockCharacterAfterRunAsPackEntry`。`TimelineColumnPackEntry<TStory>`（内含 `TimelineColumnBuilder<TStory>`、`EpochSlotBuilder<TEpoch>`）用一个流式块定义列顺序与解锁绑定。

**资源配置 records**（`ContentAssetProfiles.cs`，全部用 `with` 表达式扩展）：`CardAssetProfile`、`RelicAssetProfile`、`PowerAssetProfile`、`OrbAssetProfile`、`PotionAssetProfile`、`AfflictionAssetProfile`、`EnchantmentAssetProfile`、`ModifierAssetProfile`、`ActAssetProfile`、`MonsterAssetProfile(string? VisualsScenePath)`、`EncounterAssetProfile`、`EventAssetProfile`、`AncientEventPresentationAssetProfile`、`RestSiteOptionAssetProfile`、`EpochAssetProfile`、`CardPoolAssetProfile(CardPoolDeckViewStyle?)`、`CardPoolDeckViewStyle`/`CardPoolDeckViewStyleContext`、枚举 `CardVisualStyle`；各 profile 提供 `static XxxAssetProfile Empty` 与 `static XxxAssetProfiles` 构造辅助类（如 `CardAssetProfiles.Create(...)`）。材质/贴图路径字段一般在 profile 上以 `XxxPath`、`XxxMaterialPath`、`XxxMaterial` 暴露。

**内容资源接口**（`Content\Patches\ContentAssetOverridePatches.cs` 中声明，模板类自动实现）：`IModCardAssetOverrides`、`IModCardPortraitMaterialOverride`、`IModCardFrameMaterialOverride`、`IModCardBannerMaterialOverride`、`IModCardPortraitBorderMaterialOverride`、`IModCardEnergyIconMaterialOverride`、`IModCardAncientBorderMaterialOverride`、`IModCardAncientTextBgMaterialOverride`、`IModCardAncientBannerMaterialOverride`、`IModCardPoolAssetOverrides`、`IModCardPoolDeckViewStyle`、`IModRelicAssetOverrides`、`IModPowerAssetOverrides`、`IModOrbAssetOverrides`、`IModActAssetOverrides`、`IModEventAssetOverrides`、`IModAncientEventAssetOverrides : IModEventAssetOverrides`、`IModEpochAssetOverrides`、`IModAfflictionAssetOverrides`、`IModEnchantmentAssetOverrides`、`IModTextEnergyIconPool`（另在 `EncounterAssetOverridePatches.cs`/`MonsterAssetOverridePatches.cs`/`ModifierAssetOverridePatches.cs` 声明 `IModEncounterAssetOverrides`、`IModMonsterAssetOverrides`、`IModModifierAssetOverrides`）。在 `ModModelRuntimeGodotFactoryInterfaces.cs`：`IModCreatureVisualsFactory`、`IModMonsterCreatureVisualsFactory`、`IModCharacterCreatureVisualsFactory`、`IModEncounterCombatSceneFactory`、`IModEventLayoutPackedSceneFactory`、`IModEventBackgroundPackedSceneFactory`、`IModEventVfxFactory`、`IModOrbSpriteFactory`、`IModCreatureAnimatorFactory`、`IModCharacterCreatureAnimatorFactory`、`IModCreatureCombatAnimationStateMachineFactory`、`IModNonSpineAnimationStateMachineFactory`、`IModCharacterMerchantAnimationStateMachineFactory`、`IModCharacterRestSiteAnimationStateMachineFactory` —— 返回 null 表示走默认路径，返回节点/动画器/状态机表示自定义。

**内容有效性过滤**：接口 `IModEncounterActValidity`、`IModAncientActValidity`（`IsValid…` 判定某内容是否允许出现在某 act）；静态类 `ModEncounterActValidityFilter`、`ModAncientActValidityFilter` 提供注册辅助。

### 1.2 `Scaffolding.Characters` — 角色模板

- `abstract ModCharacterTemplate<TCardPool, TRelicPool, TPotionPool> : CharacterModel`（三个泛型是已注册的池类型；`TCardPool : CardPoolModel` 等）：
  - sealed 重写 `CardPool` / `RelicPool` / `PotionPool`（从 ModelDb 按类型解析）、`StartingDeck` / `StartingRelics` / `StartingPotions`（本地内容 + 注册表追加内容合并）。
  - 自定义入口：`protected virtual IEnumerable<CardModel> LocalStartingDeck`、`LocalStartingRelics`、`LocalStartingPotions`；**旧版** `StartingDeckEntries`/`StartingDeckTypes`/`StartingRelicTypes`/`StartingPotionTypes`（标 `[Obsolete]`，推荐改用 `CharacterRegistrationEntry.AddStartingXxx` 或 `RegisterCharacterStarterXxx`）。
  - `protected virtual Type? UnlocksAfterRunAsType` — 前置解锁角色。
  - 资源：`public virtual CharacterAssetProfile AssetProfile => CharacterAssetProfile.Empty`、`public virtual string? PlaceholderCharacterId`（默认 `CharacterAssetProfiles.DefaultPlaceholderCharacterId`，用原版角色兜底缺失字段）；`CustomVisualsPath`/`CustomEnergyCounterPath`/`CustomMerchantAnimPath`/`CustomRestSiteAnimPath`/`CustomIconTexturePath`/`CustomCharacterSelectBgPath`/`CustomMapMarkerPath`/`CustomTrailPath`/`CustomCombatSpineSkeletonDataPath`/`CustomCharacterSelectSfx` 等全部由 `ResolvedAssetProfile`（占位填充后）派生。
  - 动画自定义：`protected virtual NCreatureVisuals? TryCreateCreatureVisuals()`、`protected virtual CreatureAnimator? SetupCustomCreatureAnimator(MegaSprite controller)`、`protected virtual ModAnimStateMachine? SetupCustomCombatAnimationStateMachine(Node visualsRoot, CharacterModel character)`、`SetupCustomMerchantAnimationStateMachine`、`SetupCustomRestSiteAnimationStateMachine`（默认委托给 Merchant 版）；`SetupCustomNonSpineAnimationStateMachine` 已弃用。
  - 原版交互策略：`public virtual bool HideFromVanillaCharacterSelect`、`AllowInVanillaRandomCharacterSelect`、`HideInCardLibraryCompendium`、`RequiresEpochAndTimeline => true`。
- 角色相关接口：`IModCharacterEpochTimelineRequirement`、`IModCharacterUnlockPrerequisite`、`IModCharacterVanillaSelectionPolicy`、`IModCharacterAssetOverrides`、`IModCharacterCardLibraryCompendiumPlacement`、`IModColorfulPhilosophersCardPool`（空接口，标记使用"五彩哲学家"卡池）；`readonly record struct StartingDeckEntry(Type CardType, int Count = 1)` + `static Of<TCard>(int count=1)`。
- 角色资源：`sealed record CharacterAssetProfile(CharacterSceneAssetSet? Scenes, CharacterUiAssetSet? Ui, CharacterVfxAssetSet? Vfx, CharacterSpineAssetSet? Spine, CharacterAudioAssetSet? Audio, CharacterMultiplayerAssetSet? Multiplayer, VisualCueSet? VisualCues, CharacterWorldProceduralVisualSet? WorldProceduralVisuals, CharacterVanillaRelicVisualOverride[]?, CharacterVanillaPotionVisualOverride[]?, CharacterVanillaCardVisualOverride[]?)`（`Empty` 静态实例）。子 records：`CharacterSceneAssetSet`、`CharacterUiAssetSet`、`CharacterVfxAssetSet`、`CharacterTrailStyle`、`CharacterSpineAssetSet`、`CharacterAudioAssetSet`、`CharacterMultiplayerAssetSet`、`CharacterVanillaRelicVisualOverride(string RelicModelIdEntry, RelicAssetProfile Assets)`、`CharacterVanillaPotionVisualOverride`、`CharacterVanillaCardVisualOverride`。静态类 `CharacterAssetProfiles`（`Resolve(profile, placeholderCharacterId)` 等）、`CharacterAssetPathHelper`、`CharacterOwnedVanillaRelicModelId`。世界场景视觉：`record CharacterWorldProceduralVisualSet`（含 `CharacterMerchantWorldDefinition(VisualCueSet)`、`CharacterRestSiteWorldDefinition(VisualCueSet)`）+ `CharacterWorldProceduralVisualSetBuilder`；`ModCharacterWorldSceneVisuals`（静态入口）、`ModWorldSceneVisualNodeFactory`、`ModCreatureVisualPlayback`。

### 1.3 `Scaffolding.Cards`

**HandGlow（金色/红色发光）**：
- `static ModCardHandGlowRegistry`：`Register<TCard>(ModCardHandGlowRules)` / `Register(Type, ModCardHandGlowRules)` —— 冻结后（`ModContentRegistry.IsFrozen`）禁止注册；为基类注册的规则对派生类生效；任一谓词返回 true 即发光。
- `readonly record struct ModCardHandGlowRules { Func<CardModel,bool>? GoldWhenBonusActive; Func<CardModel,bool>? RedWhenHandWarning; }` + 工厂 `Gold(...)` / `Red(...)` / `GoldAndRed(...)` / 合并 `Or(other)`。
- `static ModCardHandGlowPredicates`：现成谓词 `OwnerCompanionOstyMissing`、`AnyOfOwnersCardsExhaustedThisTurn`、`ThisCardNotFinishedPlayThisTurn`。
- 扩展：`CardModelHandGlowExtensions`、`ModCardHandGlowCombine`（组合规则）。

**HandOutline（任意颜色描边）**：
- `static ModCardHandOutlineRegistry`：`Register<TCard>(ModCardHandOutlineRules)` / `Register<TCard>(ModCardHandOutlineSwitchRule<TCard>)` / `Register<TCard>(params …[])` / `Register(Type, …)`；`TryRefreshOutlineForHolder(NHandCardHolder)`、`TryRefreshDynamicOutlineForHolder(NHandCardHolder)`。
- `readonly record struct ModCardHandOutlineRules` / `ModCardHandOutlineRules<TCard>`（`Of(params rules)`）、`readonly record struct ModCardHandOutlineSwitchRule` / `ModCardHandOutlineSwitchRule<TCard>(Func<TCard, Color?> ColorWhen, int Priority = 0, bool VisibleWhenUnplayable = false, bool RefreshEveryFrame = true)`（最高优先级胜出）。`ModCardHandOutlineEvaluation` 为评估结果。

### 1.4 `Scaffolding.Godot`

**节点工厂（显式调用，不打 `PackedScene.Instantiate` 补丁）**：
- `public interface IRitsuGodotNodeFactory<out TNode> where TNode : Node`：`TNode CreateFromNode(Node source, VisualNodeStyle? style)`、`TNode CreateFromResource(object resource, VisualNodeStyle? style)`。
- `static RitsuGodotNodeFactories`：`RegisterFactory<TNode>(IRitsuGodotNodeFactory<TNode>, bool replaceExisting = false)`、`RegisterFactory<TNode>(Func<Node,VisualNodeStyle?,TNode>, Func<object,VisualNodeStyle?,TNode>? = null, bool = false)`、`CreateFromScene<TNode>(PackedScene, [VisualNodeStyle?|GenEditState])`、`CreateFromScenePath<TNode>(string, …)`、`CreateFromResource<TNode>(object, [VisualNodeStyle?])`（资源可为已加载对象或资源路径字符串）。全部要求主线程。
- 内置工厂（`Godot\NodeFactories\`）：`RitsuNCardTrailVfxNodeFactory`、`RitsuNCreatureVisualsNodeFactory`、`RitsuNEnergyCounterNodeFactory`、`RitsuNMerchantCharacterNodeFactory`、`RitsuNode2DSceneRootFactory`、`RitsuNRestSiteCharacterNodeFactory`、`RitsuTextureRectControlNodeFactory`；辅助：`RitsuGodotNodeExtensions`、`RitsuGodotPackedSceneHelper`、`RitsuGodotTreeCompat`（如 `AddChildSafely`）、`RitsuGodotNodeFactoryBootstrap`。

**NodeAttachments（`_Ready` 时挂子节点）**：
- `sealed ModNodeAttachmentRegistry`（每 mod 单例）：`static For(string modId)`、`RegisterReadyChild<TParent,TNode>(string localId, Func<TParent,TNode> factory, [Action<TParent,TNode>? setup,] NodeAttachmentOptions? = null)`、`RegisterReadyChildFromScene<TParent,TNode>(string localId, string scenePath, …)`、`RegisterReadyChildFromConvertedScene<TParent,TNode>(…)`、`TryGetAttached<TParent,TNode>(parent, localId, out node)`、`static TryGetAttachedById<TParent,TNode>(parent, id, out node)`、`static EnsureReadyAttachments(Node parent)`、`static GetDefinitionsSnapshot()`、`static GetQualifiedNodeAttachmentId(modId, localId)`。
- `sealed NodeAttachmentOptions`：`Name`、`Order`、`UniqueNameInOwner`、`IncludeDerivedParentTypes=true`、`DuplicatePolicy`、`AddMode`、`AttachParentSelector(Func<Node,Node?>)`、`SetupTiming`、`ChildIndex`、`InsertBeforeName`/`InsertAfterName`、`QueueFreeReplacedNode=true`（只能指定一种插入方式）。
- 枚举：`NodeAttachmentDuplicatePolicy`（AllowDuplicateName / ReuseExistingByName / SkipIfExistingByName / ReplaceExistingByName / ThrowIfExistingByName）、`NodeAttachmentAddMode`（AddChildSafely / AddChildDirect）、`NodeAttachmentSetupTiming`（BeforeAdd / AfterAdd）。
- 接口：`INodeAttachmentFactory.CreateNode(Node parent)`、`INodeAttachmentSetup.Setup(Node parent, Node node)`（特性自动注册用）。

### 1.5 `Scaffolding.Visuals`

- **定义**：`record VisualCueSet`（`VisualCueSetBuilder`）、`record VisualFrameSequence`（`VisualFrameSequenceBuilder`）、`readonly record struct VisualFrame(string TexturePath, float DurationSeconds)`、`record VisualNodeStyle`。入口 `static ModVisualCues`：`CueSet()` / `FrameSequence()`。
- **动画状态机**（替代 baselib `SetupAnimationState`）：
  - `static ModAnimStateMachines`：`Standard(MegaSprite controller, string idleName, string? deadName…, …)` → 返回原版 `CreatureAnimator`（Spine）；`StandardCue(Node visualsRoot, CharacterModel? character, string idleName, …)` → 返回 `ModAnimStateMachine`（非 Spine，自动发现后端）；`StandardMerchantCue` / `StandardRestSiteCue`（优先使用角色商店/休息处 cue 集）。标准触发器：`Idle` / `Dead` / `Hit` / `Attack` / `Cast` / `Relaxed`。
  - `sealed ModAnimStateMachineBuilder`：`Create()`、`AddState(string id, bool loop=false)` → `StateScope`（`.WithNext(id)` / `.Done()`）、`AddBranch(fromId, trigger, toId, Func<bool>? condition)`、`AddAnyState(trigger, toId, Func<bool>? condition)`、`Build(IAnimationBackend)`、`BuildSpine(MegaSprite)`、`BuildForVisualsRoot(Node, CharacterModel? = null, VisualCueSet? = null)`（经 `CompositeBackendFactory` 自动发现后端）。
  - `sealed ModAnimStateMachine`（状态机本体，`Start`/`AddAnyState`/`SetState` 等）、`sealed ModAnimState`（含 `NextState`、`AddBranch`）、`IAnimationBackend`、`IAnimationTimingProvider`、`CompositeBackendFactory`；后端：`SpineAnimationBackend`、`GodotAnimationPlayerBackend`、`AnimatedSprite2DBackend`、`CueAnimationBackend`、`FormSwitchingAnimationBackend`、`CompositeAnimationBackend`。
- 帧序列播放器 `CueFrameSequencePlayer`。

### 1.6 `Scaffolding.MonsterMoves`

`static ModMonsterMoveStateMachines`（用于 `MonsterModel.GenerateMoveStateMachine` 保持简洁）：
- `SingleMoveLoop(MoveState move)` — 单行动每回合重复。
- `Cycle(params MoveState[] moves)` — 轮转循环（末行动接回首行动）。
- `HeadThenRepeatTail(MoveState head, MoveState tail)` — 先执行一次 head 再循环 tail（Track → Hounds → Hounds 模式）。
- `RandomEntry(string branchId, Action<RandomBranchState> configureBranches, IReadOnlyList<MonsterState> allStatesIncludingMoves)` — 随机分支入口。
- `ConditionalEntry(string branchId, Action<ConditionalBranchState> configureBranches, IReadOnlyList<MonsterState>)` — 条件分支入口。

### 1.7 `Scaffolding.Ancients.Options`

- `sealed ModAncientOptionRule`：构造 `(Func<AncientEventModel, IEnumerable<EventOption>> optionFactory)`；可配置 `Condition`、`Priority`（高者先跑，同优先级按注册序）、`SkipDuplicateTextKeys=true`；静态工厂 `Single(Func<AncientEventModel, EventOption?>, condition=null, priority=0, skipDuplicateTextKeys=true)`。
- `static ModAncientOptionRegistry`：`Register(Type ancientType, ModAncientOptionRule rule)` 等（为特定 Ancient 类型追加初始选项）。

### 1.8 `Scaffolding.Combat` 与 `Scaffolding.Content` 杂项

- `static CombatTurnPhaseExtensions`：`IsOwnerPlayPhase(this CardModel model)` — 拥有者是否处于玩家出牌阶段（多版本兼容）。
- `static ModAncientStageVisuals`、`record AncientEventStageProceduralVisualSet` + builder、`static AncientStageProceduralRootFactory`（先古事件舞台程序化视觉）。
- `static RuntimeAssetRefreshCoordinator` + 枚举 `RuntimeAssetRefreshScope`、`RuntimeAssetReloadExtensions`（编辑器/热重载资源刷新）。
- `static CombatBackgroundAssetsFactory`、`static AncientEventStageProceduralAssetPaths`、`static ExternalAssetOverrideRegistry`、`static ExternalCardMaterialOverrideRegistry`、`static ExternalBadgeIconOverrideRegistry`、`static CardPoolDeckViewStyleRegistry`。

---

## 2. 特性（Attribute）清单

全部位于 `STS2RitsuLib.Interop.AutoRegistration`；基类 `AutoRegistrationAttribute : Attribute` 提供 `int Order`（同阶段内局部排序）与 `bool Inherit`（基类特性是否应用于派生类；继承的注册保留原 owner）。**可标注目标全部为 `AttributeTargets.Class`，均 `AllowMultiple = true`**（除 `RitsuLibOwnedBy`）。触发时机：每个 mod 程序集在**类型发现阶段**被扫描一次（`AttributeAutoRegistrationTypeDiscoveryContributor`），操作按确定性顺序经显式注册表 API 执行；扫描发生在框架初始化与 ModelDb 内容注册冻结之前。

| 特性 | 必填参数 / 可配置属性 | 作用 |
|---|---|---|
| `RegisterCard(Type poolType)` | `PoolType`；`StableEntryStem`、`FullPublicEntry` | 把标注类注册进指定卡池 |
| `RegisterRelic(Type poolType)` / `RegisterPotion(Type poolType)` | 同上 | 注册进指定遗物池 / 药水池 |
| `RegisterCharacter` | — | 注册角色模型 |
| `RegisterAct` / `RegisterMonster` / `RegisterPower` / `RegisterOrb` / `RegisterEnchantment` / `RegisterAffliction` / `RegisterAchievement` / `RegisterSingleton` | — | 注册章节 / 怪物 / 能力 / 充能球 / 附魔 / 侵蚀 / 成就 / 单例模型 |
| `RegisterBadge`（经 ContentRegistrationEntry/BuildContext 使用，无独立特性类） | — | 徽章 |
| `RegisterModelCapability` | `StableEntryStem`、`FullPublicEntry` | 注册模型能力（ModelCapability） |
| `RegisterDefaultModelCapability(Type targetModelType)` | `TargetModelType`；`ModifierId` | 把标注能力加入目标模型类型的默认能力集 |
| `RegisterGoodModifier` / `RegisterBadModifier` | `ModifierListSortOrder`（负值插到区段前，非负插后） | 注册每日正/负面特效 |
| `RegisterMutuallyExclusiveModifierGroup(params Type[] memberTypes)` | `MemberTypes` | 互斥特效组（标注类 + 成员组） |
| `RegisterSharedCardPool` / `RegisterSharedRelicPool` / `RegisterSharedPotionPool` | — | 共享卡池 / 遗物池 / 药水池 |
| `RegisterSharedEvent` / `RegisterSharedAncient` | — | 共享事件 / 共享先古事件 |
| `RegisterGlobalEncounter` | — | 并入每章遭遇池 |
| `RegisterActEncounter(Type actType)` / `RegisterActEvent(Type actType)` / `RegisterActAncient(Type actType)` | `ActType` | 限定某章节的遭遇 / 事件 / 先古事件 |
| `RegisterCharacterStarterCard(Type characterType, int count = 1)` / `RegisterCharacterStarterRelic` / `RegisterCharacterStarterPotion` | `CharacterType`、`Count` | 角色初始内容（带顺序可再配 `Order`） |
| `RegisterTrashHeapCard` / `RegisterTrashHeapRelic` | — | "垃圾堆"事件 Grab / Dive In 候选 |
| `RegisterOwnedKeyword(string localKeywordStem)` | `LocalKeywordStem`、`TitleTable="card_keywords"`、`TitleKey`、`DescriptionTable/Key`、`IconPath`、`CardDescriptionPlacement`、`IncludeInCardHoverTip=true` | 注册归属本 mod 的关键词（限定 ID 由 modId+词干派生） |
| `RegisterOwnedCardKeyword(string localKeywordStem)` | 同上（按游戏卡牌关键词本地化约定） | 注册归属本 mod 的卡牌关键词 |
| `RegisterOwnedCardTag(string localCardTagStem)` | `LocalCardTagStem` | 注册归属本 mod 的 CardTag |
| `RegisterOwnedCardPile(string localPileStem)` | `LocalPileStem`、`Scope`、`Style`、`AnchorKind`、`AnchorOffsetX/Y`、`AnchorCustomX/Y/PivotX/PivotY`、`IconPath`、`Hotkeys`、`ExtraHand*` 系列、`HoverTipOffsetX/Y`、`HoverTipPlacement` | 声明式注册卡牌堆；标注类可实现 `IModCardPileHandler`（无参构造实例，`OnOpen` 自动接入） |
| `RegisterOwnedTopBarButton(string localButtonStem)` | `LocalButtonStem`、`IconPath`、`ButtonOrder`、`OffsetX/Y` | 声明式注册顶栏按钮；标注类必须实现 `IModTopBarButtonHandler` |
| `RegisterNodeAttachment(Type parentType, string localId)` | `ParentType`、`LocalId`、`NodeType`、`NodeName`、`UniqueNameInOwner`、`IncludeDerivedParentTypes=true`、`DuplicatePolicy`、`AddMode`、`SetupTiming`、`ChildIndex`、`InsertBeforeName/AfterName`、`QueueFreeReplacedNode=true` | 在父节点 `_Ready` 时用工厂/标注类创建并挂载子节点 |
| `RegisterNodeAttachmentFromScene(Type parentType, string localId, string scenePath)` | 同基类 + `ScenePath`、`NodeType` | 直接实例化场景挂载 |
| `RegisterNodeAttachmentFromConvertedScene(Type parentType, string localId, string scenePath)` | 同上 | 经 RitsuLib 节点工厂转换场景后挂载 |
| `RegisterSmartFormatter` / `RegisterSmartFormatSource` | —（`Order`） | 注册本地化 SmartFormat 扩展 |
| `RegisterEpoch` / `RegisterStory` / `RegisterStoryEpoch(Type storyType)` | `StoryType` | 时间线历史节点 / 故事 / 故事-节点绑定 |
| `AutoTimelineSlot(EpochEra era)` / `AutoTimelineSlotBeforeColumn(era)` / `AutoTimelineSlotAfterColumn(era)` / `AutoTimelineSlotInColumn(era)` / `AutoTimelineSlotBeforeEpochColumn(Type)` / `AutoTimelineSlotAfterEpochColumn(Type)` / `AutoTimelineSlotInEpochColumn(Type)` | `Era` / `AnchorEra` / `ReferenceEpochType` | 自动放置标注 epoch 到时间线列（首空位 / 锚定列前后 / 同列） |
| `RegisterArchaicToothTranscendence(Type ancientCardType)` | `AncientCardType` | "古老牙齿"：标注初始卡牌 → 指定先古卡牌 |
| `RegisterDustyTomeCard(Type characterType)` | `CharacterType` | 标注先古卡牌加入该角色"尘封魔典"候选 |
| `RegisterTouchOfOrobasRefinement(Type upgradedRelicType)` | `UpgradedRelicType` | "欧洛巴斯之触"：标注初始遗物 → 升级遗物 |
| `RegisterEpochCards(params Type[] cardTypes)` | `CardTypes` | 为标注 epoch 注册卡牌解锁内容并反向要求 |
| `RegisterEpochRelicsFromPool(Type poolType)` / `RequireAllCardsInPool(Type poolType)` | `PoolType` | 池内遗物作解锁内容 / 池内所有卡要求该 epoch |
| `RequireEpoch(Type epochType)` | `EpochType` | 标注内容需要先揭示该 epoch |
| `UnlockEpochAfterRunAs(Type epochType)` / `UnlockEpochAfterWinAs` / `UnlockEpochAfterAscensionOneWin` / `RevealAscensionAfterEpoch` / `UnlockCharacterAfterRunAs` | `EpochType` | 角色完成/通关后解锁 epoch 等 |
| `UnlockEpochAfterAscensionWin(Type epochType, int ascensionLevel)` | `EpochType`、`AscensionLevel` | 指定进阶通关解锁 |
| `UnlockEpochAfterEliteVictories(Type epochType, int requiredEliteWins = 15)` / `UnlockEpochAfterBossVictories(Type epochType, int requiredBossWins = 15)` | `EpochType`、`RequiredEliteWins`/`RequiredBossWins` | 精英 / Boss 击杀数解锁 |
| `UnlockEpochAfterRunCount(Type epochType, int requiredRuns, bool requireVictory = false)`（经 pack entry，无独立类在 RegistrationAttributes.cs 首文件外） | — | 局数解锁 |
| `RitsuLibOwnedBy(string modId)` | `ModId` | 覆盖标注类型上自动注册特性的归属 mod ID（`Inherited=false`，单次标注） |

---

## 3. 关键用法模式

### 3.1 模组入口（Entry）初始化

RitsuLib 自身用游戏引擎的 `[ModInitializer(nameof(Initialize))]`（`MegaCrit.Sts2.Core.Modding`）标记静态类 + `public static void Initialize()`。mod 的入口推荐：

```csharp
[ModInitializer(nameof(Initialize))]
public static class MyModEntry
{
    public const string ModId = "my_mod_id";

    public static void Initialize()
    {
        // 1) 创建流式注册器并 Apply（步骤排入框架延迟窗口，时机安全）
        ModContentPackBuilder.For(ModId)
            .Character<MyCharacter>()
            .Card<MyCharacterCardPool, MyCard>()
            .Relic<MyRelicPool, MyRelic>()
            .Power<MyPower>()
            .KeywordOwned("my_keyword", titleTable: "card_keywords")
            .Apply();

        // 2) 或直接用注册表 API
        RitsuLibFramework.GetContentRegistry(ModId).RegisterCard<MyPool, MyCard>();
        // 3) 或手写注册条目清单
        ModContentPackBuilder.For(ModId)
            .Entries(new IContentRegistrationEntry[] { new CardRegistrationEntry<MyPool, MyCard>() })
            .Apply();
    }
}
```

要点：
- 所有注册必须在**内容注册冻结**（`ModContentRegistry.IsFrozen`，即 ModelDb 初始化）之前完成；特性自动注册与 `Apply()` 的延迟窗口都满足该时机。
- 由 `Apply()` 排队执行的步骤是延迟执行的；失败单步只记日志并跳过，不阻断后续步骤。
- 每 mod 的注册表都是按 `modId` 隔离的单例：`RitsuLibFramework.GetContentRegistry/GetKeywordRegistry/GetTimelineRegistry/GetUnlockRegistry/GetCardTagRegistry/GetCardPileRegistry/GetTopBarButtonRegistry/GetSmartFormatRegistry/GetNodeAttachmentRegistry/GetDataStore/GetRunSavedDataStore/GetModelSavedDataStore/GetModelCloneRegistry(modId)`。

### 3.2 注册流程（两条路径）

**路径 A — 特性驱动**（零样板，适合简单内容）：
```csharp
[RegisterCard(typeof(MyPool))]            // 进池
[RegisterCharacterStarterCard(typeof(MyChar))] // 初始牌组 +1 张
[RegisterOwnedKeyword("burning")]         // 关键词
public sealed class MyCard : ModCardTemplate { ... }
```
基类上的特性需 `Inherit = true` 才作用到派生类；继承注册保留基类 owner（可用 `RitsuLibOwnedBy` 覆盖）。同 mod 内 `RegisterCard` 等目标若重叠（如先注册角色再注册初始卡）会自动建立依赖顺序（`providedKeys` 去重）。

**路径 B — 代码驱动**（推荐复杂内容，构建器见 1.1）：
```csharp
ModContentPackBuilder.For(ModId)
    .Character<MyCharacter>(entry => entry
        .AddStartingCard<StrikeCard>(4)
        .AddStartingRelic<MyStarterRelic>())
    .SharedCardPool<MySharedPool>()
    .Card<MyPool, CardA>().Card<MyPool, CardB>()
    .TimelineColumn<MyStory>(col => col
        .Epoch<MyEpoch>(slot => slot ...))
    .Apply();
```

**模板类用法**：卡牌继承 `ModCardTemplate(2, CardType.Attack, CardRarity.Common, TargetType.Enemy)`，覆盖 `AssetProfile => CardAssetProfiles.Create(...)`（或 `with` 表达式扩展）即可配卡图/边框/材质；`AdditionalHoverTips` 加悬浮提示。角色继承 `ModCharacterTemplate<MyCardPool, MyRelicPool, MyPotionPool>`，覆盖 `AssetProfile`（`CharacterAssetProfile`，含场景/UI/VFX/Spine/音频/多人/原版卡遗物药水视觉覆盖）与动画自定义钩子。

### 3.3 生命周期订阅

`RitsuLibFramework` 提供类型化生命周期事件总线（事件均实现 `IFrameworkLifecycleEvent`，含 `OccurredAtUtc`；`IReplayableFrameworkLifecycleEvent` 会向新订阅者重放）：

```csharp
// 类型化订阅（TEvent 须为 struct 或 sealed class；返回 IDisposable 用于退订）
using var sub = RitsuLibFramework.SubscribeLifecycle<FrameworkInitializedEvent>(
    e => Log(e.IsActive), replayCurrentState: true);

// 一次性订阅（触发后自动退订）
RitsuLibFramework.SubscribeLifecycleOnce<MainMenuReadyEvent>(_ => SetupMainMenuStuff());

// 观察者模式（收到所有事件，自行 switch 具体类型）
RitsuLibFramework.SubscribeLifecycle(new MyObserver());
```

内置事件（`FrameworkLifecycleContracts.cs`）：`FrameworkInitializingEvent`（初始化中）、`FrameworkInitializedEvent`（初始化完成，可重放）、`ProfileServicesInitializingEvent`、`ProfileServicesInitializedEvent`（档案服务就绪，可重放）；另有 `MainMenuReadyEvent` 等由补丁发布的运行时事件（主菜单就绪时触发 `SubscribeLifecycleOnce<MainMenuReadyEvent>` 是常见"开始干活"时机）。

### 3.4 常用拼图（速查）

- **手牌发光**：`ModCardHandGlowRegistry.Register<MyCard>(ModCardHandGlowRules.Gold(c => ModCardHandGlowPredicates.ThisCardNotFinishedPlayThisTurn(c)))`。
- **手牌描边**：`ModCardHandOutlineRegistry.Register<MyCard>(new ModCardHandOutlineSwitchRule<MyCard>(c => someColor, priority: 10))`。
- **怪物行动**：`GenerateMoveStateMachine()` 里 `return ModMonsterMoveStateMachines.Cycle(moveA, moveB);` 或 `RandomEntry(...)`。
- **自定义动画**：非 Spine 角色用 `ModAnimStateMachines.StandardCue(visualsRoot, character, "idle", attackName: "attack", …)`；复杂图用 `ModAnimStateMachineBuilder.Create().AddState(...).AddBranch(...).BuildForVisualsRoot(root, character, cueSet)`。
- **挂子节点**：`RitsuLibFramework.GetNodeAttachmentRegistry(modId).RegisterReadyChildFromScene<NTarget, NMyNode>("id", "res://mod/…tscn")`，或直接贴 `[RegisterNodeAttachmentFromScene(typeof(NTarget), "id", "res://…")]`。
- **先古事件加选项**：`ModAncientOptionRegistry.Register(typeof(MyAncient), ModAncientOptionRule.Single(ancient => new EventOption{...}))`，或经 builder `AncientOption<TAncient>(rule)`。

---

## 4. 备注

- 目录下的 `Patches\`、`Internal\`、诊断/导出工具类未列入；`ContentAssetOverridePatches.cs` 等补丁文件里声明的 `IMod*AssetOverrides` 接口是模板类实现资源覆盖的契约，已在上文接口清单中汇总。
- 占位内容（`RegisterPlaceholderCard/Relic/Potion<TPool>(stableEntryStem, descriptor)`）允许只给稳定 ID 与资源描述就生成内容，无需写 CLR 类。
- 资源路径约定：mod 内容资源走 `res://` Godot 路径；`ModelPublicEntryOptions`（`FromStem`/`Full`/`Hidden` 等）控制公开条目命名与可见性；限定 ID 由 `ModContentRegistry.GetCompoundId / GetQualifiedXxxId(modId, localStem)` 派生。
