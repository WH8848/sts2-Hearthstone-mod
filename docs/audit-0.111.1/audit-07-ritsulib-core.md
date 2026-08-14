# RitsuLib 核心注册与模板体系 审计（0.111.1）

审计对象：`E:\MOD\sts2\STS2-RitsuLib\src`（RitsuLib **0.5.11**，`Const.cs:25`）
对照源码：`E:\MOD\sts2\sts2\0.111.1\src`（7012 个 cs 文件，含 addons/mega_text）+ `sts2.csproj`（Godot.NET.Sdk/**4.5.1**）
核对方式：全量提取 566 个去重 Harmony 补丁目标（`new(typeof(...))`），逐一在 0.111.1 源码中做存在性核对；另对约 40 个关键目标做参数签名核对；对全部 `IsCritical => true` 补丁做目标存在性核对（结果：**零缺失**）。

关键构建事实：RitsuLib 0.5.11 的 `Sts2ApiCompat=0.110.0`（`STS2-RitsuLib.csproj:3-6`），兼容目标清单只到 0.110.0，**0.111.1 不在支持清单内**，`build\RitsuLib.CompatDefines.targets:20-35` 没有 `STS2_AT_LEAST_0_111_0` 定义。本报告全部核对均按 0.110.0 编译分支（`STS2_AT_LEAST_0_103_2 … _0_110_0` 生效）进行。

---

## 兼容性问题（高/中/低 × 5）

### 高 × 0

未发现任何会使 0.5.11（0.110.0 分支）在 0.111.1 上无法启动的关键 API 缺失：566 个目标中，17 个初筛"疑似缺失"全部为 `#if` 死分支或误报（见下文证据表）；`IsCritical=true` 的补丁目标（`SavedPropertiesTypeCacheInjectionPatch`、`CardModelCapabilityPatches.*` 等）在 0.111.1 全部可解析，`RitsuLibFramework.Initialize` 的 `PatchAllRequired` 不会因关键补丁失败而返回 false。

| 疑似目标（死分支/误报） | 结论 |
| --- | --- |
| `Hook.BeforeTurnEnd/AfterTurnEnd`（AdditionalHookLifecycleAfterPatches.cs:497/541） | 0.110 编译走 `#elif STS2_AT_LEAST_0_108_0` 分支 → 实际目标 `Hook.BeforeSideTurnEnd/AfterSideTurnEnd`（Hook.cs:1244/1279）✓ |
| `SaveManager.IncrementUnlock`（UnlockLifecyclePatches.cs:74） | `#else` 分支；0.110 走 `SaveManager.GrantNextUnlock`（SaveManager.cs:1105）✓ |
| `EpochModel.BigPortraitPath/IsArtPlaceholder`（ContentAssetModelVisualOverridePatches.cs:55/83） | 分别被 `#if !STS2_AT_LEAST_0_106_0` / `#if STS2_AT_LEAST_0_106_0 && !STS2_AT_LEAST_0_108_0` 排除；0.110 走 `ResolvedPortraitPath`（EpochModel.cs:271）✓ |
| `CardModel.GetResultPileType*`（CardModelCapabilityPatches.cs:436-440） | 0.110 走 `GetResultLocationForCardPlay`（CardModel.cs:2084）✓ |
| `SavedPropertyCache`、`MegaRichTextLabel`、`HarmonyInitSetterCompat` | 分别是对 `ModelIdSerializationCache` 的类型别名（SavedPropertiesTypeCacheInjectionPatch.cs:20）、addons 目录类型（addons\mega_text\MegaRichTextLabel.cs:12）、RitsuLib 自补丁类型 ✓ |

### 中 × 2

**中** | STS2-RitsuLib.csproj:3-5；build\RitsuLib.CompatDefines.targets:20-35 | RitsuLib 0.5.11 官方兼容目标上限为 **0.110.0**，0.111.1 不在 `RitsuLibCompatTargets`/`Sts2ApiCompat` 清单内，且不存在 `STS2_AT_LEAST_0_111_0` 编译符号 | 0.111.1 证据：sts2.csproj:1-6（Godot.NET.Sdk/4.5.1，net90）；本次静态核对未发现缺失目标，但 0.5.11 二进制是面向 0.110 API 编译的，**0.111.x 后续补丁版若改动任一被反射解析的目标，将表现为运行期补丁失败（可选补丁仅记日志、关键补丁回滚并停用框架）** | 建议：将 0.111.0/0.111.1 追加进 `_RitsuLibSupportedApiVersions` 并新增 `STS2_AT_LEAST_0_111_0` 定义，发布 0.111.x compat 构建（与既有 0.110/0.109/0.108/0.107.1 变体机制一致）；同时更新 `mod_manifest.json` 的 `min_game_version` 生成逻辑（RitsuLib.ModManifest.targets:82 默认取 `Sts2ApiCompat`）。

**中** | Content\ModContentRegistry.cs:1900-1931（RegisterPoolModel）；Scaffolding\Content\TypeListCardPoolModel.cs:78-89 | `RegisterCard` 走 `ModHelper.AddModelToPool(poolType, modelType)`，0.111.1 中该 API 在池首次调用 `ConcatModelsFromMods` 后置 `isFrozen=true`，**冻结后再调用直接抛 `InvalidOperationException("…too late!")`**，RitsuLib 无任何保护/延迟注入兜底 | 0.111.1 证据：Core\Modding\ModHelper.cs:64-69（`if (value.isFrozen) throw …`）、:78-93（`ConcatModelsFromMods` 冻结）。当前时序（ModTypeDiscoveryPatch 在 `LocManager.Initialize` 触发、早于 `ModelDb.Init` 与池首次枚举）是安全的，但任何模组在 `ModelDb.Init` 之后（如 `GameReadyEvent` 回调里）调用 `RegisterCard` 会抛异常而不是优雅降级 | 建议：在 `EnsureMutable` 之外增加"池已冻结"检测，将超时注册改为记录 `RegistrationFreezeDiagnostics` 警告并跳过，而非让 `ModHelper.AddModelToPool` 抛异常中断模组初始化。

### 低 × 3

**低** | mod_manifest.json:10（`"affects_gameplay": false`） | 清单键名与游戏 JSON 属性名不一致：游戏字段为 `affectsGameplay`（`[JsonPropertyName("affectsGameplay")]`），反序列化时 `affects_gameplay` 不会被识别，回退默认值 `true` → 游戏把 RitsuLib 视为影响玩法（`affectsGameplay`）的模组，影响玩法相关评估/度量路径 | 0.111.1 证据：Core\Modding\ModManifest.cs:43（JsonPropertyName）与 :85（`jsonNode.Deserialize(GetTypeInfo<ModManifest>())`）；ModManager.cs:985-1006（GetGameplayRelevantModNameList 依赖该字段）。注意 RitsuLib 自身 `Sts2ModManagerCompat.cs:49-50` 也读 `affectsGameplay`，同样读不到该键 | 建议：把清单键改为 `"affectsGameplay": false`（或让生成 targets 重写该键），与游戏反序列化对齐。

**低** | RitsuLibFramework.cs:1960-2008（AreGodotScriptPathsAlreadyRegistered） | `EnsureGodotScriptsRegistered`（:1894-1958）反射 Godot 内部类型 `Godot.Bridge.ScriptManagerBridge` 的私有字段 `_pathTypeBiMap`/`_scriptTypeBiMap` 及 `ReadWriteLock` 做去重预检；字段名属 Godot 内部实现，改动时仅会跳过该优化（走正常 `LookupScriptsInAssembly` 调用），已做 null guard 降级 | 0.111.1 证据：双方 SDK 一致（sts2.csproj:1 与 STS2-RitsuLib.csproj:1 同为 Godot.NET.Sdk/4.5.1），`LookupScriptsInAssembly(Assembly)` 静态方法（RitsuLibFramework.cs:1910-1915 反射签名）在 Godot 4.5.x 存在；游戏源码不包含 Godot 运行时源码，无法在 src 内直接验证，属外部运行时依赖 | 建议：保持 guard；若未来游戏升级 Godot SDK，优先改用 Godot 官方公开注册入口（如 `ScriptManagerBridge` 公开方法）替代私有字段探测。

**低** | Lifecycle\Patches\CoreLifecyclePatches.cs:147-151, 176-181（ModelDb.Preload 事件） | 0.111.1 中 `ModelDb.Preload()` 仅在 `!OS.HasFeature("editor")` 时由 `ExecuteDeferred` 调用（OneTimeInitialization.cs:101-103），编辑器/测试模式下 `ModelPreloadingStartingEvent/CompletedEvent` 不发布，订阅这些事件的模组在编辑器环境行为与发行版不一致（RitsuLib 本身仅用于内部审计，无直接影响） | 0.111.1 证据：Core\Helpers\OneTimeInitialization.cs:100-104 | 建议：文档注明 Preload 生命周期事件仅发行版触发；或在编辑器模式用 `ModelRegistryInitializedEvent` 兜底补发。

---

## 潜在 Bug / 行为差异（mod 逻辑 vs 0.111.1 语义）

1. **`RunManager.InitializeNewRun/InitializeSavedRun` 为 private，且 0.111.1 中调用点语义有变**：`RunLifecyclePatch`（CoreLifecyclePatches.cs:309-315）按名称补丁这两个 private 方法（RunManager.cs:520/541）发布 `RunStartedEvent/RunLoadedEvent`。0.111.1 中 `InitializeNewRun` 的调用点已扩到 3 处（RunManager.cs:310/342/461：StartRun、ContinueRun、Daily 等），而 `InitializeSavedRun` 由 4 处调用（:370/399/429）。Harmony 对 private 方法按名+参数解析仍有效（本审计确认签名 `(SerializableRun)` 一致），但事件"何时算 run 开始/加载"的语义完全跟随这些私有方法的内联边界——若 0.111.x 后续把新档/读档逻辑从这两个方法中拆出（如新增 `InitializeRunLobby`、`SetStartedWithNeowFlag` 之类步骤），事件触发时机/次数可能偏移（例如 `RunStartedEvent` 在 0.111.1 中于 `InitializeNewRun` 返回后、玩家实际开局逻辑完成前发布）。建议 RitsuLib 在 0.111.x 分支改用更稳定的公共边界（如 `RunManager` 对外状态机方法）或在事件文档中明确语义。

2. **`ModTypeDiscoveryPatch` 依赖 `LocManager.Initialize` 与 `ModelDb.Init` 的调用顺序**：0.111.1 中顺序仍为 `LocManager.Initialize()` → `ModelDb.Init()`（OneTimeInitialization.cs:79-81），与 RitsuLib 设计前提一致；发现管线/延迟内容包刷出（ModTypeDiscoveryPatch.cs:34-47）严格先于 `ModelDb.Init`，`ModHelper.AddModelToPool` 冻结前完成注册。此顺序是 RitsuLib 正确性的隐形前提，0.111.x 未破坏，但属"约定耦合"，建议在 `ExecuteEssential` 顺序变更时由 RitsuLib 自检（例如在 `ModelDb.Init` Prefix 中检测发现管线是否已运行）。

3. **`DustyTomeSetupForPlayerPatch`（Relics\Patches\DustyTomeSetupForPlayerPatch.cs:32-70）与 0.111.1 原生实现的候选集差异**：0.111.1 的 `DustyTome.SetupForPlayer` 仅从 `player.PlayerRng.Rewards.NextItem(items)` 的 `items`（Ancient 卡牌，DustyTome.cs:50-55）随机；RitsuLib Prefix 会先尝试 mod 注册候选、再尝试"已解锁且非 Transcendence 的 Ancient"，最后回退"锁定 Ancient"并打警告。若 `ArchaicTooth.TranscendenceCards` 被 RitsuLib 的 ArchaicToothTranscendenceCardsPatch 追加了 mod 卡（ArchaicTooth.cs:50 的静态字典 + Patch Postfix），原生 `DustyTome` 的候选池会包含 mod 卡——两套逻辑叠加后候选去重依赖 `card.Id` 相等比较，行为一致但逻辑重复，建议在 0.111.x 分支评估直接复用原生 `DustyTome.SetupForPlayer` 的候选构造。

4. **`Hook` 侧事件补丁的参数类型已按版本严格分支，但 0.111.1 中 `Before/AfterSideTurnStart` 用 `IReadOnlyList<Creature>`、`Before/AfterSideTurnEnd` 用 `IEnumerable<Creature>`**（Hook.cs:1156/1175 vs :1244/1279）：RitsuLib 0.110 分支分别用 `IReadOnlyList<Creature>`（CombatHookLifecyclePatches.cs:119/147）与 `IEnumerable<Creature>`（AdditionalHookLifecycleAfterPatches.cs:499-503/542-548），`Type.GetMethod` 精确参数匹配全部命中 ✓。这是最容易在 0.111.x 翻车的一类（任一方法把集合类型换成 `IReadOnlyList`/`IEnumerable` 即静默失效），审计已确认当前版本安全。

5. **`CardModel.HoverTips`/`Keywords` 语义**：0.111.1 中 `Keywords` 为 `IReadOnlySet<CardKeyword>`（CardModel.cs:526），`HoverTips` getter 对每个 keyword 调 `HoverTipFactory.FromKeyword`（:979-986）；RitsuLib 的 `HoverTipFactoryFromKeywordPatch`（Prefix 短路）与 `CardModelHoverTipsModKeywordPatch`（Postfix 按 `IncludeInCardHoverTip` 过滤）在该 getter 上叠加生效，minted mod 关键词照常被路由。行为差异点：`HoverTips` 结果会 `Distinct()`（:987），若 mod 关键词与原生关键词解析出引用相等的 tip 会被去重，属既有行为，非 0.111 引入。

6. **`ModCharacterTemplate` 覆盖点与 0.111.1 抽象/虚方法完全匹配**（CharacterModel.cs:63 `protected abstract UnlocksAfterRunAs`、:93-95 `abstract StartingHp/StartingGold`、:105-113 `abstract CardPool/RelicPool/PotionPool/StartingDeck/StartingRelics`、:115 `virtual StartingPotions`）：模组派生类若按 RitsuLib 0.110 文档只实现部分抽象成员，0.111.1 下依旧编译失败（抽象成员未变）；`StartingPotions` 0.111.1 为 virtual（默认空），RitsuLib `sealed override` 后模组通过 `LocalStartingPotions` 扩展 ✓。

---

## 改进机会（0.111.1 新 API 可替代/增强现有实现）

1. **`AncientEventModel.RelicOption` 原生助手**（AncientEventModel.cs:268-286：`RelicOption<T>(pageName, customDonePage)` / `RelicOption(RelicModel, pageName, customDonePage)`）：与 RitsuLib `ModAncientEventTemplate.CreateModRelicOption`（ModAncientEventTemplate.cs:119-169）功能重叠，原生版额外支持 `customDonePage` 且已内置 `Done()`；RitsuLib 版优势是自定义选项本地化键。建议 0.111.x 分支提供"优先原生 `RelicOption` + 键覆盖"的桥接，减少模板与原生行为分叉。

2. **`ModelIdSerializationCache` 公开 API 已稳定**（ModelIdSerializationCache.cs:290-313 `GetNetIdForPropertyName`/`GetPropertyNameForNetId`、:50-66 `*IdBitSize`）：`SavedPropertiesTypeCacheInjectionPatch`（IsCritical=true）目前直接反射私有字段 `_propertyNameToNetIdMap`/`_netIdToPropertyNameMap`（SavedPropertiesTypeCacheInjectionPatch.cs:267-277）注入合成属性名——0.111.1 下字段名仍匹配（ModelIdSerializationCache.cs:44-46），但可评估把"名称→NetId 注入"改为调用原生 `GetNetIdForPropertyName` 校验 + 保持私有 map 写入，或建议游戏开放注入 API，降低字段名变更风险。

3. **`CardPoolModel.AllCardIds`**（CardPoolModel.cs:60，`IEnumerable<ModelId>` 缓存）：RitsuLib `TypeListCardPoolModel`/`ModContentRegistry` 多处重复 `AllCards.Select(c => c.Id)`（如 ModContentRegistry 的池查询），可换用 `AllCardIds` 减少重复枚举与哈希集分配。

4. **`Hook.ModifyStarCost`/`ModifyEnergyCostInCombat`**（Hook.cs:2024/1583）等 0.111.1 原生修改钩子与 RitsuLib `CardModelCapabilityPatches`（CurrentStarCost/EnergyCost 补丁）并存：对"能量/星标消耗修改"类能力，新模组优先用原生 Hook（多人同步、预览模式免费获得），RitsuLib 补丁作为兜底；建议在 RitsuLib 文档中标注该优先级。

5. **`ModelDb.ActsByIndex`**（ModelDb.cs:323）已存在：RitsuLib `ActsByIndexPatch`（PatcherSetup.cs:728-730 在 `STS2_AT_LEAST_0_107_1` 下启用）直接消费它，0.111.1 兼容 ✓，无需改动；可顺带在 `ModActTemplate` 的章节注册中利用 `ActsByIndex` 做跨 Act 去重。

6. **Godot 4.5.1 SDK 对齐**：游戏与 RitsuLib 均为 `Godot.NET.Sdk/4.5.1`（sts2.csproj:1、STS2-RitsuLib.csproj:1），`EnsureGodotScriptsRegistered` 的反射注册（RitsuLibFramework.cs:1909-1935）与 `AssemblyHasScriptsAttribute`/`ScriptPathAttribute` 枚举（:2010-2031）在 0.111.1 运行时可直接工作，无需改动。

---

## 关键对接点核对结果（任务清单 1-7）

| # | 对接点 | RitsuLib 位置 | 0.111.1 证据 | 结论 |
| --- | --- | --- | --- | --- |
| 1 | ModTypeDiscoveryHub.RegisterModAssembly / RitsuLibFramework.EnsureGodotScriptsRegistered 对接的游戏注册流程（ScriptManagerBridge、Godot script 注册） | Interop\ModTypeDiscoveryHub.cs:64-83（RegisterModAssembly）、:129-161（RunOnce 在 LocManager.Initialize 触发）；RitsuLibFramework.cs:1894-2031（ScriptManagerBridge.LookupScriptsInAssembly 反射注册）；Interop\Patches\ModTypeDiscoveryPatch.cs:31（目标 LocManager.Initialize） | LocManager.cs:166 `static void Initialize()`；OneTimeInitialization.cs:79（Initialize 先于 ModelDb.Init）| ✓ 存在、签名一致、时序正确 |
| 2 | 模板基类：ModCardTemplate / ModRelicTemplate / ModPowerTemplate / ModPotionTemplate / ModCharacterTemplate（AssetProfile/CharacterAssetProfiles.Merge、StartingHp/StartingGold）/ ModEnchantmentTemplate / ModEncounterTemplate / ModAncientEventTemplate | Scaffolding\Content\*.cs；Scaffolding\Characters\ModCharacterTemplate.cs:340-407；CharacterAssetProfiles.cs:97（Merge）、:21（DefaultPlaceholderCharacterId） | CardModel.cs:1087/951；RelicModel.cs:376；PowerModel.cs:348；PotionModel.cs:89(OrbModel 同型)；CharacterModel.cs:63/93/95/105-115；EnchantmentModel.cs:238；EncounterModel（HasScene/HasCustomBackground）；AncientEventModel.cs:159/193/247 | ✓ 全部存在且签名一致；AssetProfile 体系为 RitsuLib 自有类型（ContentAssetProfiles.cs:356-811），不依赖游戏 |
| 3 | RegisterCard → 卡池注册（TypeListCardPoolModel）；RegisterCharacterStarterCard/Relic；RegisterTouchOfOrobasRefinement/RegisterArchaicToothTranscendence/RegisterDustyTomeCard（OrobasAncientUpgradeRegistry 补丁） | ModContentRegistry.cs:378-433（RegisterCard→RegisterPoolModel:1900-1931→ModHelper.AddModelToPool）；TypeListCardPoolModel.cs:78-89（sealed override GenerateAllCards）；RitsuLibFramework.OrobasAncientUpgrades.cs:35-159；RitsuLibFramework.DustyTome.cs:20-47；Relics\Patches\*.cs | CardPoolModel.cs:75（protected abstract GenerateAllCards）；ModHelper.cs:54-69；ArchaicTooth.cs:50（TranscendenceCards）/144（GetTranscendenceStarterCard）；TouchOfOrobas.cs:120（GetUpgradedStarterRelic）；DustyTome.cs:50（SetupForPlayer）；CardPoolModel.cs:101（GetUnlockedCards） | ✓ 存在；ModHelper 冻结后抛异常为唯一行为风险（见中 × 2） |
| 4 | HoverTipFactory.FromKeyword（含 mod 关键词补丁）、ModKeywordRegistry.CreateHoverTip | Keywords\Patches\HoverTipFactoryFromKeywordPatch.cs:37；CardModelHoverTipsModKeywordPatch.cs:37；ModKeywordRegistry.cs:518-543（CreateHoverTip 用 HoverTip(LocString,LocString,Texture2D?)） | HoverTipFactory.cs:36（FromKeyword）；CardModel.cs:953-989（HoverTips 调 FromKeyword）；HoverTip.cs:46（构造匹配） | ✓ 一致 |
| 5 | 生命周期事件订阅（SubscribeLifecycle/GameReadyEvent 等）触发链 | RitsuLibFramework.cs:166-398（SubscribeLifecycle）；CoreLifecyclePatches.cs:47-49/108-112/264-291/309-341；NMainMenuReadyLifecyclePatch.cs:22 | OneTimeInitialization.cs:68/92；ModelDb.cs:423/473/484；NGame.cs:145/182；RunManager.cs:520/541/1623；NMainMenu.cs:124 | ✓ 触发链完整（唯一注意：Preload 事件编辑器下不触发，见低 × 3） |
| 6 | ModContentRegistry / ModContentPackBuilder 依赖的 ModelDb/内容注册 API | ModContentRegistry.cs（RegisterPoolModel、InjectDynamicRegisteredModels:1858-1898、ModelDb 各 All* 补丁：Content\Patches\ModelDbContentPatches.cs）；Scaffolding\Content\ModContentPackBuilder.cs | ModelDb.cs:68-411（All* 属性齐全）、:438（Inject）、:510-549（GetId/GetById/GetByIdOrNull）、:535（GetEntry）；ModHelper.cs:49-68 | ✓ 一致 |
| 7 | 0.5.11 对 0.111.x 的已知不兼容（Harmony 补丁目标全量核对） | 566 个去重目标（见"兼容性问题"） | 全部目标（含 40 个抽查签名）在 0.111.1 存在；17 个初筛疑似项均为死分支/误报；IsCritical=true 目标零缺失 | ✓ 0.110 分支无目标缺失；唯一结构性缺口是兼容目标清单未含 0.111.x（中 × 1） |

---

## 结论摘要

1. **RitsuLib 0.5.11（0.110.0 编译分支）在 0.111.1 上没有发现任何缺失/签名变化的 Harmony 补丁目标**：566 个目标全量核对 + 全部关键补丁 + 约 40 个签名抽查均通过，`IsCritical=true` 补丁（含 `SavedPropertiesTypeCacheInjectionPatch`、`CardModelCapabilityPatches`）全部可解析，框架初始化不会被关键补丁失败阻断。
2. 结构性风险是**发布侧**：0.5.11 兼容目标上限为 0.110.0，0.111.1 不在支持清单且无 `STS2_AT_LEAST_0_111_0` 符号，属"未验证的新版本"，建议尽快发布 0.111.x compat 变体（项目已有成熟的多版本变体机制）。
3. 行为风险集中在 `ModHelper.AddModelToPool` 冻结后抛异常（无兜底）、`RunManager` 私有初始化方法作为生命周期事件边界、`LocManager.Initialize`→`ModelDb.Init` 顺序耦合三处，均不构成 0.111.1 下的即时故障。
4. 低危项：清单 `affects_gameplay` 键名与游戏 `affectsGameplay` 不匹配（游戏默认视为玩法模组）；`ScriptManagerBridge` 私有字段反射依赖 Godot 4.5.1 内部实现（双方 SDK 一致，当前可用且有降级）。
5. 总体评级：**0.5.11 在 0.111.1 上可直接运行，无高/关键兼容性问题**；建议发布侧补上 0.111.x 兼容构建并修正清单键名，即可把风险收敛到零。
