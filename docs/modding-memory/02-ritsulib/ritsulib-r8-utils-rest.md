# RitsuLib 源码文档整理（r8）：Utils / RunData / Saves / RuntimeInput / TopBar / Timeline / Updates / Unlocks / Networking

> 来源：`E:\MOD\sts2\STS2-RitsuLib\src\` 对应目录（已排除 `*.g.cs`、`*MethodName*`、`*PropertyName*`、`*SignalName*`、`<>` 生成类与内部 Harmony 补丁细节）。
> 所有命名空间均为 `STS2RitsuLib.*`（游戏 API 为 `MegaCrit.Sts2.*`）。注册表类普遍采用"**初始化期注册 → 模型初始化前冻结**"模式：冻结后（`IsFrozen == true`）再注册抛异常。

---

## 1. Utils（`STS2RitsuLib.Utils` 等）

子命名空间：`Utils`（U）、`Utils.Json`（J）、`Utils.Persistence`（P）、`Utils.Persistence.Context`（PC）、`Utils.Persistence.Migration`（PM）、`Utils.HarmonyIl`（HI）、`Utils.Speculation`（S）。

### 公共 API 清单

- **`AttachedState<TKey,TValue>`**（U）— 在任意引用对象上挂附加状态（`ConditionalWeakTable`，不阻止 GC）。`this[TKey]`、`TryAdd/Add/GetOrAdd/GetOrCreate/GetValueOrDefault/TryGetValue/Set/Update/Remove/TryRemove/Clear`
- **`SavedAttachedState<TKey,TValue>`**（U）— 持久化版附加状态，经原版 `SavedProperties` 序列化。构造 `(string name, Func<TValue>? defaultValueFactory = null, int order = 0)`；支持的 TValue：`int/bool/string/ModelId/int[]/SerializableCard([])/List<SerializableCard>` 及枚举（数组）。**须在类型发现/初始化期注册，名称全局唯一**
- **`DynamicEnumValueRegistry<TEnum>`**（U）— 进程级动态枚举注册中心：确定性分配 + 归属校验 + 反向查找。`For(string modId)`、`RegisterOwned(modId, localStem)`、`Register(modId, id)`、`GetOwnedId`、`TryGet/Get`、`Get(TEnum value)`、`GetValue(id)`、`TryResolve(idOrEnumName)`、`GetDefinitionsSnapshot()`
- **`ModDynamicEnumValueRegistry<TEnum>`**（U）— 逐模组门面（隐藏 ID 类别段）。`ModId`、`RegisterOwned(string localStem)`、`GetOwnedId`、`GetOwnedValue(…)`
- **`DynamicEnumValueDefinition<TEnum>`**（U）— `record (string ModId, string Id, TEnum Value)`
- **`DynamicEnumValueMinter<TEnum>`**（U）— `ReservedFloor(默认 0x4000_0000) + XxHash32(utf8(id)) % range` 确定性铸值（仅 32 位底层枚举）。`Mint(id)`、`ComputeValue(id)`、`TryGetId/TryGetValue/IsDynamic`
- **`I18N`**（U）— 多来源 JSON 翻译加载器（FS / 嵌入资源 / PCK 合并），自动跟随语言切换。构造 `(instanceName?, fsFolders?, resourceFolders?, pckFolders?, resourceAssembly?, fallbackLanguage?)`；`Get(key, fallback)`、`TryGet`、`ContainsKey`、`ContainsLocalKey`、`Snapshot()`、`EnumerateKeys(prefix?, orderByKey?)`、`GetAllKeys`、`EnumerateAvailableLanguages`、`ForceReload()`、`event Action? Changed`；静态 `ResolveCurrentLanguageCode()`、`NormalizeLanguageCode(string?)`（`zh-CN→zhs`、`en-US→eng`、`ja-JP→jpn`…）
- **`GodotResourcePath`**（U）— `res://` / `user://` / `uid://` 路径解析与存在性检查。`EnumerateCandidatePaths(rawPath)`、`TryEnsurePath(pathOrUid, out path)`、`ResourceExists(rawPath)`、`TryLoad<T>(rawPath, out T? resource)`
- **`FileOperations`**（U）— Godot `FileAccess` 封装（备份 + 原子替换语义）。`ReadText`、`WriteText(path, content, logContext?, atomic = true)`、`RenameFile`、`ReadTextWithBackupFallback`、`ReadJson<T>`、`WriteJson<T>`、`FileExists`、`DeleteFile`、`DeleteDirectoryRecursive`；结果类型 `ReadResult / WriteResult / JsonResult<T>`
- **`WeightedList<T> : IList<T>`**（U）— 加权随机容器，可抽后不放回。`TotalWeight`、`Add(item[, weight])`、`AddRange`、`GetRandom(Rng rng, bool remove = false)`、`TryGetRandom(...)`；配套接口 `IWeightedValue { int Weight { get; } }`
- **`RitsuAnsiText` / `RitsuTextSegment`**（U）— ANSI 终端文本解析。`StripControlSequences(text)`、`ParseSegments(text)`；片段 record `(Text, Color, Bold, Dim, Kind)`
- **`HoverTipHelper`**（根命名空间 `STS2RitsuLib`）— 向活动 UI 追加悬停提示。`AddTipToOwner(Control owner, string title, string description)`、`AddCardTipsToOwner(Control owner, IEnumerable<CardModel> cards)`
- **`CreatureHpDisplayExtensions`**（U）— `IsInfiniteHpDisplayed(this Creature creature)`（跨版本稳定无限 HP 检测）
- **`MaterialUtils`**（U）— 着色器材质工厂：`CreateReplaceHueShaderMaterial(r,g,b,brightness)`、`CreateRgb/Hsv/UnmodulatedHsvShaderMaterial`、`CreateDoomBarShaderMaterial(GradientTexture1D)`、`CreateVanillaDoomBarGradientTexture()/NoiseTexture()`
- **`JsonPointer`**（J）— RFC 6901。`Normalize`、`Get(JsonNode root, pointer)`、`Set(JsonObject root, pointer, JsonNode?)`、`EnumerateSegments/DecodeSegment`
- **`JsonPatch`**（J）— RFC 6902。`Apply(target, patchDocument)`、`Apply(target, IEnumerable<JsonPatchOperation>)`；`record JsonPatchOperation(string Op, string Path, string? From = null, JsonNode? Value = null)`
- **`JsonMergePatch`**（J）— RFC 7386。`Apply(target, patch)`、`ApplyInPlace(JsonObject, JsonObject)`
- **`JsonCanonicalizer`**（J）— RFC 8785 JCS 规范化。`Canonicalize(JsonNode?)`
- **`JsonIJsonValidator`**（J）— I-JSON 校验。`TryValidate(JsonNode?, out string? error)`
- **`SaveScope`**（P）— `enum Global / Profile / InMemory`
- **`ProfileManager`**（P）— 档案追踪与路径解析单例。`Instance`、`CurrentProfileId`、`event Action<int,int>? ProfileChanged`、`event Action<int>? ProfileDeleted`、`Initialize()`、静态 `GetAccountBasePath(modId)`、`GetProfileDirectory([profileId])`、`GetBasePath(scope, …)`、`GetFilePath(fileName, scope, …)`、`DeleteProfileData(profileId, modId)`
- **`PersistentDataEntry<T> where T : class, new()`**（P）— 强类型 JSON 持久化封装（迁移 / 备份恢复 / 变更通知）。构造 `(modId, fileName, SaveScope, defaultValues, jsonOptions, migrationManager, autoCreateIfMissing = false, contextProvider?)`；`Data`、`FilePath`、`Scope`、`event Action? Changed`、`Load()`、`Save()`、`SaveTo(path)`、`Modify(Action<T>)`
- **`DataReadyLifecycle`**（P）— 档案数据就绪协调器。`IsReady`、`ReadyProfileId`、`State`、`NotifyPotentialReady(string source)`、`NotifyProfileInvalidated(int profileId, string reason)`
- **`DataLifecycleContracts`**（P）— `enum DataLifecycleState (WaitingForProfile/Ready)`；事件 `ProfileDataReadyEvent(int ProfileId, …, bool IsInitialReady, bool IsProfileSwitch, bool DataReloaded, …)`、`ProfileDataChangedEvent`、`ProfileDataInvalidatedEvent`
- **`StorageContext`**（PC）— 持久化扩展寻址上下文。`Empty`、`TryGet<TValue>(StorageContextKey<TValue>, out TValue)`、`With/Without`；`StorageContextKey<TValue>(string id)`；内置键 `StorageContextKeys.ProfileId`
- **`IMigration`**（PM）— `int FromVersion; int ToVersion; bool Migrate(JsonObject data);`
- **`MigrationManager`**（PM）— `RegisterConfig<T>(currentVersion, minimumSupportedVersion, schemaVersionProperty)`、`RegisterMigration<T>(IMigration)`、`Migrate<T>(string jsonContent, JsonSerializerOptions?)`；`MigrationResult<T>`（`Success/Data/ErrorMessage/WasMigrated/FinalVersion/RequiresRecovery`）；`ModDataMigrationConfig`（`required CurrentDataVersion`、`MinimumSupportedDataVersion`、`SchemaVersionProperty`）
- **`ModDataRuntimeInterop`**（Persistence\Interop，命名空间根）— 运行时模组数据互操作。`RegisterProviderType(...)`、`TryRegisterAll()`、`SyncAllFromProviders()`、`PushLoadedDataToAllProviders()`、`EnsureProfileSwitchSyncHook()`；`ModDataInteropJsonDocument`、`InteropMigrationAdapter : IMigration`
- **`SpeculativeExecution`**（S）— 推测执行隔离会话（预算内模拟执行）。`SpeculativeExecutionSession(budget?)`、`Enter()`、`TryEnterFrame(MethodBase?, out IDisposable)`、`GetState<T>/SetState/HasState`、`Budget/Effects/Diagnostics/Operations/IsComplete`
- **HarmonyIl**（HI，内部实现，一笔带过）— `HarmonyIlRewriter`（`From(instructions[, originalMethod])` → `TryFind/FindAll/InsertBefore/InsertAfter/Replace/ReplaceCall/RedirectCalls/InstructionsChecked()`）、`HarmonyIlPattern`（`Sequence(...)`、`RequireSingle()`）、`HarmonyIlInspectionExtensions`（`GetOriginalIl`、`FindOriginalIlCallPath`）、`HarmonyIlControlFlow`、`HarmonyIlEffectAnalyzer`、`HarmonyIlPayloadTranspiler`、`HarmonyAsyncIl/HarmonyAsyncTaskBridge`（await 点查找与 Task 续体重写）——供写 Transpiler 时替代手写原生 IL

> 内部（不可直接调用）：`RitsuMainThread`、`RitsuGodotAwaitSafety`、`FastMethodInvoker`、`AssetPathDiagnostics`、`ModAccountRelativePath`、`StoragePathResolver`、云同步相关（`ModCloudSyncScope/PathRegistry/Host/Mirror`、`StorageSyncPathEnumerator`）、`RitsuLibDataPaths`、Persistence\Patches 补丁。

### 用法要点

- **动态枚举**：`DynamicEnumValueRegistry<TEnum>.For(modId).RegisterOwned(localStem)`，值落在 `[0x4000_0000, 0x7FFF_FFFF]` 保留区间，跨进程/跨版本稳定；重复注册返回既有定义，不同 mod 抢占同一 ID / 哈希碰撞抛 `InvalidOperationException`。内置类别段：`CardKeyword→KEYWORD`、`PileType→CARDPILE`、`CardTag→CARDTAG`、`RewardType→REWARD`、`TargetType→TARGETTYPE`。已有专用注册表（如 `ModCardTagRegistry`）时优先用专用入口。
- **持久化标准流程**：`ProfileManager.Instance.Initialize()` → 数据就绪后 `DataReadyLifecycle.NotifyPotentialReady(source)` → 用 `PersistentDataEntry<T>` 声明条目，路径落在 `user://.../mod_data/{modId}[/{profileDir}]`；`Load()` 带备份回退 + 迁移，损坏文件改名 `.corrupt`；`Modify()` 触发 `Changed`，`Save()` 后自动镜像云同步；需要更多寻址信息用 `StorageContext.With(StorageContextKeys.ProfileId, id)`。
- **文件与路径**：`FileOperations.WriteText(..., atomic: true)` 为"临时文件 + 备份轮换 + 重命名"；`ReadTextWithBackupFallback` 主文件损坏时回退 `.backup`。资源路径优先用 `GodotResourcePath`（识别 `uid://`）。
- **权重抽取**：`WeightedList<T>.GetRandom(rng, remove: true)` 抽后不放回；项实现 `IWeightedValue` 可免传权重（`Rng` 来自 `MegaCrit.Sts2.Core.Random`）。

---

## 2. RunData（`STS2RitsuLib.RunData`）

内部实现：`RunSavedDataRegistry/Runtime/Slot/Document`、`RunSavedDataLobbyRuntime/Session/Sync`、`RunSavedDataJson` 与 `Patches\` 均 internal；**公共入口只有 `RunSavedDataStore`**。

### 公共 API 清单

- **`RunSavedDataStore`** — 按 modId 划分的槽位注册表（进程级单例，modId 大小写不敏感）。`public static RunSavedDataStore For(string modId)`；`public RunSavedData<T> Register<T>(string key, Func<T>? defaultFactory = null, RunSavedDataOptions? options = null) where T : class, new()`；`public PlayerRunSavedData<T> RegisterPerPlayer<T>(...)`。同 modId+key 重复注册抛 `InvalidOperationException`
- **`RunSavedData<T>`** — 整局共享数据访问器。`Lobby { get; }`（`RunSavedDataLobbyScope<T>`）；`T Get(RunState)`、`bool TryGet(RunState, out T)`、`Set(RunState, T)`、`Remove(RunState)`、`T Modify(RunState, Action<T>)`
- **`PlayerRunSavedData<T>`** — 按玩家（`ulong netId`）分别存储。`Lobby { get; }`；`Get/Set/TryGet/Remove/Modify(RunState, ulong netId | Player, …)`
- **`RunSavedDataOptions`** — `int SchemaVersion { get; init; } = 1`；`RunSavedDataWritePolicy WritePolicy { get; init; } = WhenSet`；`bool SyncLobbyOnChange { get; init; }`；`IReadOnlyList<IMigration>? Migrations { get; init; }`
- **`RunSavedDataWritePolicy`** — `enum WhenSet`（仅脏值写）/ `WhenNonDefault`（与默认值序列化结果不同才写）/ `AlwaysWhenRegistered`
- **`RunSavedDataLobby`** — 大厅暂存协调（static）。`NotifyStagingChanged(StartRunLobby)`、`TryPushContribution(StartRunLobby)`（本地暂存贡献推给主机；主机/单人本地合并）
- **`RunSavedDataLobbyScope<T>` / `PlayerRunSavedDataLobbyScope<T>`** — 开局前（快照提交前）暂存访问：`GetOrCreate(lobby[, netId])`、`TryGet`、`Set`、`Remove`、`Modify`
- **`RunSavedDataLobbyStagingEvent`** — `record (StartRunLobby Lobby, bool IsMultiplayer, bool IsHost, RunSavedDataLobbyStagingReason Reason, DateTimeOffset OccurredAtUtc)`，实现 `IFrameworkLifecycleEvent`
- **`RunSavedDataLobbyStagingReason`** — `enum ContributionMerged / PlayerJoined / Manual / Committing / PlayerLeft`
- **`RunSavedDataPreparingEvent`** — `record (RunState RunState, bool IsMultiplayer, DateTimeOffset OccurredAtUtc)`，新局初始化前、数据导入后发布

### 用法要点

- **注册**：`var data = RunSavedDataStore.For("my.mod.id").Register<MyData>("myKey")`（建议初始化期一次注册）。数据类由 System.Text.Json 序列化（`IncludeFields = true`），T 需 `class, new()`。
- **局内读写**：所有 API 以 `RunState`（或 `Player`）为上下文；推荐 `Modify` 原地改（少一次反序列化）。
- **保存时机**：`RunManager.ToSave` 后置补丁导出文档，注入存档根对象 `_ritsulib.run_saved_data`（含 `version`）——普通存档、云存档自动携带。
- **加载时机**：`RunSaveManager.LoadRunSave/LoadMultiplayerRunSave` 后置补丁读回；`RunState.FromSerializable` 后置补丁导入 bag（含 schema 迁移，失败只告警不崩局）。
- **新局（大厅暂存）**：开局前用 `data.Lobby` 暂存；新局开始先发 `Committing` 事件 → 打包暂存载荷 → 导入槽位并触发 `RunSavedDataPreparingEvent` → 写入新 `RunState`。`SyncLobbyOnChange = true` 时每次 Lobby 写入自动推送，否则手动 `RunSavedDataLobby.TryPushContribution(lobby)`。
- **事件订阅**：`RitsuLibFramework.SubscribeLifecycle<RunSavedDataPreparingEvent>(h, replayCurrentState: false)`；典型用途是监听 `Reason == Committing` 在快照构建前做最后修改。两事件均非 replayable。

---

## 3. Saves（`STS2RitsuLib.Saves`、`STS2RitsuLib.Saves.RawProgress`）

### 公共 API 清单

`STS2RitsuLib.Saves` 内：
- **`PreservedProgressRecords`** — 解决"进度记录被游戏覆盖"：反序列化前抓取无法解析的进度条目（角色/卡牌/遭遇/敌人/古物统计、发现集、纪元、成就、待解锁角色），保存时按 ID 合并写回并抑制预期校验警告。**类型 public 但成员全部 internal**，由内部 patch 驱动，mod 无需也不可直调
- **`ProgressMirrorStore`**（internal）— 每次保存把完整 `SerializableProgress` 镜像写入 `progress_records_mirror.save`，加载时校验 `UniqueId` 后合并回，防跨进程丢失
- **`RunHistoryMissingModelSupport`**（internal）— 跑局历史引用的模型不在 `ModelDb` 时回落原版弃用占位模型（防历史预览崩溃）
- **`RunResumeMissingCharacterSupport`**（internal）— 恢复跑局时角色缺失 → 弹"无效存档"弹窗但**不删除**局内存档

`STS2RitsuLib.Saves.RawProgress`（**完整公共契约，mod 可直接使用**）：
- **`IRawProgressCommitBridge`** — 桥接主接口：`RawProgressBridgeDescriptor Describe()`；`ValueTask<RawProgressReadResult> CaptureAsync(CancellationToken = default)`；`ValueTask<RawProgressCommitResult> CommitAsync(RawProgressCommitRequest, CancellationToken = default)`；`ValueTask<RawProgressRecoveryReadResult> GetPendingRecoveriesAsync(string ownerId, …)`；`ValueTask<RawProgressCommitResult> RestoreRecoveryAsync(RawProgressRecoveryRequest, …)`；`ValueTask<RawProgressRecoveryDiscardResult> DiscardRecoveryAsync(RawProgressRecoveryRequest, …)`
- **`RawProgressBridge`** — `public static IRawProgressCommitBridge Instance { get; }`（进程级服务入口）
- **`RawProgressBridgeDescriptor`** — `record (string ProviderId, Version ProviderVersion, int ProtocolVersion, IReadOnlySet<int> SupportedSchemas, RawProgressBridgeFeature Features, long MaxDocumentUtf8Bytes, int MaxRetainedRecoveryJournals, int MaxRecoveryOwnerIdUtf8Bytes)`
- **`RawProgressBridgeFeature`** — `[Flags] enum`（18 位）：`RawSchema21Document`、`UnknownJsonPassThrough`、`LiveGameStoreCommit`、`DurableLocalReplacement`、`CloudSaveBatch`、`ConditionalGenerationCheck`、`ExclusiveSaveWindow`、`LocalOnlyRecoveryJournal`、`StructuredRecoveryOutcome`、`StablePublicContract`、`CloudReadBackVerification`、`LiveProgressStateSynchronization`、`SubsequentSaveUnknownJsonPreservation`、`ActiveProgressSnapshot`、`RecoveryJournalManagement/Ownership/Disposition`、`InvalidRecoveryJournalQuarantine`
- **`ProgressGeneration`** — `record (int ProfileId, bool IsModded, string ProgressUniqueId, string LocalSha256, long LocalLength, long LocalLastModifiedUtcTicks, bool CloudAvailable/CloudSyncEnabled/CloudPersisted, string? CloudSha256, long? CloudLength, long? CloudLastModifiedUtcTicks)`
- **`RawProgressSnapshot`** — `record (int SchemaVersion, string RawJson, ProgressGeneration Generation)`
- **`RawProgressReadOutcome`** — `enum Succeeded / ActiveProfileUnavailable / LocalReadUnavailable / ValidationFailed / SchemaUnsupported / CloudReadUnavailable`；`RawProgressReadResult` = `(Outcome, RawProgressSnapshot?)`
- **`RawProgressCommitRequest`** — `record (int ProtocolVersion, int SchemaVersion, string OwnerId, Guid TransactionId, ProgressGeneration ExpectedGeneration, string ProposedRawJson, string ProposedSha256, long ProposedUtf8Length)`
- **`RawProgressCommitOutcome`** — `enum CommittedVerified`（唯一成功）+ 15 种保守失败：`GenerationConflict`、`ActiveProfileChanged`、`SchemaUnsupported`、`ValidationFailed`、`LocalReplacementUnverified`、`CloudReadBackUnverifiedLocalPreserved`、`CloudReadBackMismatchLocalPreserved`、`LiveProgressStateUnverified`、`UnknownJsonContinuationUnverified`、`RecoveryRequired`、`RecoveryJournalNotFound/Invalid/Changed`、`CancelledBeforeCommit`、`ProviderIncompatible`
- **`RawProgressCommitResult`** — `(Outcome, LocalReadBackSha256?, CloudReadBackStatus CloudStatus, CloudReadBackSha256?, LiveKnownProjectionSha256?, bool UnknownJsonContinuationInstalled, PreservedRawSha256?, bool DestinationMayHaveChanged, bool VerifiedBackupAvailable, bool RecoveryJournalRetained)`
- **`RawProgressRecoveryRecord`** — `(string OwnerId, int SchemaVersion, Guid TransactionId, int ProfileId, bool IsModded, string ProgressUniqueId, RawProgressRecoveryStage Stage, string OriginalSha256, string ProposedSha256, string RecoveryToken)`；`RawProgressRecoveryStage = Prepared/LocalUnverified/LocalVerified/VerificationIncomplete`
- **`RawProgressRecoveryRequest`** — `(string OwnerId, Guid TransactionId, string RecoveryToken, ProgressGeneration ExpectedGeneration)`
- **`RawProgressRecoveryReadOutcome`** — `Succeeded/InvalidEntriesIgnored/StorageUnavailable`；`RawProgressRecoveryReadResult` = `(OwnerId, Outcome, IReadOnlyList<RawProgressRecoveryRecord> Records, int InvalidEntryCount)`
- **`RawProgressRecoveryDiscardOutcome`** — `Discarded/ValidationFailed/RecoveryJournalNotFound/Invalid/Changed/ActiveProfileChanged/GenerationConflict/DestinationUnavailable/Cancelled/StorageFailure`；`RawProgressRecoveryDiscardResult` = `(Outcome, bool RecoveryJournalRetained)`

### 用法要点

- mod 制作者**可直接使用的只有 `RawProgress` 契约 + `RawProgressBridge.Instance`**；其余类型由 RitsuLib 自动生效（解决"卸载 mod 后进度被游戏当未知数据覆盖/报错"）。
- **标准提交流程**：`Describe()` → `CaptureAsync()` 拿快照 → 改 `RawJson` → `CommitAsync(request, ExpectedGeneration = 快照.Generation)`。只有 `Outcome == CommittedVerified` 算成功；结合 `DestinationMayHaveChanged` / `VerifiedBackupAvailable` / `RecoveryJournalRetained` 判断下一步。
- **恢复流程**：收到 `RecoveryRequired` 后重新 `CaptureAsync`（勿复用旧代次）→ `GetPendingRecoveriesAsync(ownerId)` → `RestoreRecoveryAsync`（回滚，成功后删日志）或 `DiscardRecoveryAsync`（接受现状）；`RecoveryToken` 必须原样回传。
- **提交纪律**：`OwnerId` 用稳定 manifest mod id；`TransactionId` 每次新提交用新 Guid（仅重放相同内容可复用）；`ExpectedGeneration` 必须来自最近快照。

---

## 4. RuntimeInput（`STS2RitsuLib.RuntimeInput`）

### 公共 API 清单

- **`RuntimeHotkeyService`**（static）— 运行时热键入口。`static IRuntimeHotkeyHandle Register(string bindingText, Action callback, RuntimeHotkeyOptions? options = null)`（非法绑定抛 `FormatException`）；`Register(IEnumerable<string> bindingTexts, …)`；`Initialize()`（订阅 `GameReadyEvent`，首次 Register 自动调用）；`GetRegisteredHotkeys()` / `GetRegisteredHotkeyDetails()`；`TryGetRegisteredHotkey(string id, out info)`；`TryNormalizeBinding(string?, out string normalized)`；`ActionBinding(string actionName)`（→ `action:<名>`）；`NormalizeOrDefault(string?, string fallback)`
- **`IRuntimeHotkeyHandle`**（interface : `IDisposable`）— `string CurrentBinding`、`IReadOnlyList<string> CurrentBindings`、`bool IsRegistered`、`bool TryRebind(string, out string)` / `TryRebind(IEnumerable<string>, out IReadOnlyList<string>)`、`bool TryGetRegistrationInfo(out …)`、`void Unregister()`
- **`RuntimeHotkeyOptions`**（sealed class，全 init）— `Id`、`DisplayName/Description/Category`（`RuntimeHotkeyText?`）、`Purpose`、`bool ExposeToSteamInput`、`bool MarkInputHandled`、`bool SuppressWhenTextInputFocused = true`、`bool SuppressWhenDevConsoleVisible = true`、`DebugName`
- **`RuntimeHotkeyRegistrationInfo` / `RuntimeHotkeyRegistrationDetails`**（sealed record）— 绑定快照：`(CurrentBinding, IsModifierOnly, Id, DisplayName, Description, Purpose, Category, MarkInputHandled, …)`；Details 含多绑定列表
- **`RuntimeHotkeyText`**（abstract）— 固定/动态文本。`abstract string Resolve()`；`static Literal(string)`、`static Dynamic(Func<string>)`；隐式转换 `string`、`ModSettingsText`、`Func<string>`
- **`RitsuSteamInputActionRegistry`**（static）— 把 Godot InputMap 动作注册为可选 Steam Input 数字动作（引用计数）。`static IDisposable RegisterAction(string actionName, RuntimeHotkeyText displayName, RuntimeHotkeyText? description = null, string? registrationId = null)`
- 内部：`RuntimeHotkeyParser`、`RuntimeHotkeyBinding`、`RuntimeHotkeyRouterNode`、`RitsuSteamInput*`（Backend/Interop/ManifestInstaller）、`Patches\` 补丁

### 用法要点

- **注册**：`RuntimeHotkeyService.Register("Ctrl+Shift+K", () => DoThing(), new RuntimeHotkeyOptions { Id = "mymod.open_menu", DisplayName = "打开菜单", MarkInputHandled = true })`；用 `TryNormalizeBinding` 先校验。
- **绑定语法**：`+` 连接键与修饰键（`Ctrl/Alt/Shift/Meta` 及左右变体）；键名接受 Godot `Key` 枚举名或显示名；动作绑定 `action:<InputMap动作名>`。规范形式如 `Ctrl+Shift+K`、`action:accept`。
- **触发**：倒序遍历注册表（后注册者优先），`MarkInputHandled` 时吞事件；键绑定要求 `Pressed && !IsEcho`；支持"仅修饰键"热键。dev 控制台/文本输入聚焦时按选项抑制。
- **Steam Input（可选）**：`ExposeToSteamInput = true` 且绑定含 `action:` 时，把动作注册为 Steam 数字动作并合并进游戏 VDF 清单（`steam_input_manifest.ritsulib.vdf`），手柄轮询注入 `InputEventAction`；失败自动降级为普通输入。

---

## 5. TopBar（`STS2RitsuLib.TopBar`）

### 公共 API 清单

- **`ModTopBarButtonRegistry`**（sealed）— 按 modId 隔离的按钮注册入口。`public static ModTopBarButtonRegistry For(string modId)`；`RegisterOwned(string localStem, ModTopBarButtonSpec spec)`（ID 按 `MODID_TOPBARBUTTON_类型名` 约定派生）；`Register(string id, ModTopBarButtonSpec spec)`（直接指定全局 ID）；`static bool TryGet(string id, out definition)`；`static ModTopBarButtonDefinition[] GetDefinitionsSnapshot()`（按 `Order` 升序）
- **`ModTopBarButtonSpec`**（sealed record）— `const string HoverTipLocTable = "static_hover_tips"`；`string? IconPath`（Godot 资源路径，省略时克隆原版牌组图标）；`int Order`（越小越靠近牌组按钮）；`Vector2 Offset`；`Action<ModTopBarButtonContext>? OnClick`（**必填**，null 抛异常）；`Func<Context,bool>? VisibleWhen`（`_Process` 求值，null 恒可见）；`Func<Context,bool>? IsOpenWhen`；`Func<Context,int>? CountProvider`（负数隐藏计数）
- **`ModTopBarButtonDefinition`**（sealed record）— 注册结果（不可变）：`ModId`、`Id`、`IconPath`、`Order`、`Offset`、`OnClick`、`VisibleWhen/IsOpenWhen/CountProvider`、`LocString Title/Description`（读 `static_hover_tips` 的 `{Id}.title` / `{Id}.description`）
- **`ModTopBarButtonContext`**（sealed）— `Definition`；`Player? Player`（未绑定时 null，含局间）；`NModCardPileButton? Button`；`bool OpenCapstoneScreen(ICapstoneScreen)`、`ToggleCapstoneScreen(...)`、`CloseCapstoneScreen()`
- **`IModTopBarButtonHandler`**（interface）— 声明式自动注册的行为契约（配合 `[RegisterOwnedTopBarButton]`）：`void OnClick(ModTopBarButtonContext)`（必实现）；`bool IsVisible(context)`（默认 true）；`bool IsOpen(context)`（默认 false）；`int GetCount(context)`（默认 -1）
- **`ModTopBarLayout`**（static）— 布局辅助。`GetRightAlignedContainer(NTopBar)`、`GetDeckSlotAnchor(NTopBar)`、`Place(NTopBar, NModCardPileButton, Vector2 offset = default)`、`PlaceAfterDeck(...)`、`PlaceBeforeModifiers(...)`。注意：同一锚点连续放置时**最后放置者最靠近锚点**，应按目标顺序逆序放置
- **`TopBarButtonRegistrationEntry`** — 内容包声明式注册行。`(string id, ModTopBarButtonSpec spec)`、`void Register(ModTopBarButtonRegistry)`、`static Owned(string modId, string localButtonStem, ModTopBarButtonSpec spec)`
- **`ModTopBarButtonHoverTipFactory`**（static）— `static HoverTip Create(ModTopBarButtonDefinition definition)`

### 特性清单

- **`[RegisterOwnedTopBarButton(string localButtonStem)]`**（定义于 `STS2RitsuLib.Interop.AutoRegistration`）— 目标：任意具体类（**须实现 `IModTopBarButtonHandler`**，AllowMultiple）；命名属性：`string? IconPath`、`int ButtonOrder`、`float OffsetX`、`float OffsetY`。触发：自动注册阶段通过公共无参构造创建实例，把 `OnClick/IsVisible/IsOpen/GetCount` 映射为 `ModTopBarButtonSpec` 回调；悬停提示键 `{限定ID}.title` / `{限定ID}.description`

### 用法要点

- **两种注册方式**：(1) 程序化 `ModTopBarButtonRegistry.For("MyMod").RegisterOwned("Recipes", spec)`；(2) 声明式 `[RegisterOwnedTopBarButton("Recipes", IconPath = "res://…", ButtonOrder = 10)]` + 实现 `IModTopBarButtonHandler`。
- **时机**：必须在原版 `NTopBar._Ready` 之前注册（模组初始化阶段）；同 mod 重复注册同一 ID 返回既有定义，不同 mod 抢 ID 抛 `InvalidOperationException`。
- **动态状态**：`VisibleWhen/IsOpenWhen/CountProvider` 每帧求值，回调须轻量；计数负数隐藏。

---

## 6. Timeline（`STS2RitsuLib.Timeline`、`STS2RitsuLib.Timeline.Scaffolding`）

### 公共 API 清单

注册表：
- **`ModTimelineRegistry`**（sealed）— 把自定义 `EpochModel`/`StoryModel` 类型写入时间线字典（按 modId 单例）。`static bool IsFrozen`；`static ModTimelineRegistry For(string modId)`；`RegisterEpoch<TEpoch>()` / `RegisterEpoch(Type)`；`RegisterStory<TStory>()` / `RegisterStory(Type)`；`RegisterStoryEpoch<TStory, TEpoch>()` / `RegisterStoryEpoch(Type, Type)`。要求具体子类带公共无参构造，`Id` 非空无冲突
- **`ModTimelineLayoutRegistry`**（static）— 为每个 `ModEpochTemplate` 子类分配时间线列与列内位置（先占用原版槽位防重叠）。`RegisterTimelineSlot(Type epochType, EpochEra era, int eraPosition, string modId)`、`RegisterAutoTimelineSlot(Type, EpochEra, string modId)`、`…BeforeEraColumn / BeforeEpochColumn / AfterEraColumn / AfterEpochColumn / InEraColumn / InEpochColumn(…)`
- **`ModStoryEpochBindings`**（static）— "故事 → 有序纪元类型"映射。`Append(Type storyType, Type epochType)`、`GetOrderedEpochTypes(Type storyConcreteType)`
- **`ModEpochGatedContentRegistry`**（static）— 纪元 ID → 受门控的卡牌/遗物 CLR 类型。`static bool IsFrozen`；`Register(string modId, string epochId, IReadOnlyList<Type>? cardTypes, IReadOnlyList<Type>? relicTypes)`；`TryGet(string epochId, out EpochGatedContentEntry)`；`ResolveCards(epochId)`、`ResolveRelics(epochId)`；嵌套 `record EpochGatedContentEntry(string ModId, IReadOnlyList<Type> CardTypes, IReadOnlyList<Type> RelicTypes)`
- **`ModTimelineEraIconRegistry`**（static）— 按时代配置坐标轴图标。`Configure(EpochEra era, bool? enabled = null, string? texturePath = null)` / `Configure(long eraValue, …)`、`Clear(EpochEra|long)`

Scaffolding 模板（均 public abstract，命名空间 `STS2RitsuLib.Timeline.Scaffolding`）：
- **`ModEpochTemplate`** : `EpochModel` — 模组纪元基类（实现 `IModEpochAssetOverrides`）。`public sealed override EpochEra Era`、`public sealed override int EraPosition`（由布局注册表解析，不可重写）；`public virtual EpochAssetProfile AssetProfile => Empty`；`public virtual string? CustomPackedPortraitPath`、`CustomBigPortraitPath`；`protected IReadOnlyList<TModel> RequireUnlockPresentationItems<TModel>(items, sourceName)`（要求 ≥3 项，解锁文本读前三项）
- **`ModStoryTemplate`** : `StoryModel` — 故事基类。`protected sealed override string Id => StringHelper.Slugify(StoryKey)`；`public sealed override EpochModel[] Epochs`（来自绑定表）；**必须实现 `protected abstract string StoryKey { get; }`**
- **`CardUnlockEpochTemplate`** — **必须实现 `protected abstract IEnumerable<Type> CardTypes { get; }`**；`public IReadOnlyList<CardModel> Cards`；`public override string UnlockText`；`EnumerateUnlockCardTypes()`；可选 `protected virtual IEnumerable<Type> ExpansionEpochTypes => []`；`GetTimelineExpansion()` / `QueueUnlocks()` 已实现
- **`RelicUnlockEpochTemplate`** — **必须实现 `protected abstract IEnumerable<Type> RelicTypes { get; }`**；`Relics`、`EnumerateUnlockRelicTypes()`
- **`PotionUnlockEpochTemplate`** — **必须实现 `protected abstract IEnumerable<Type> PotionTypes { get; }`**；`Potions`；池可见性门控仍须另行 `Unlocks.ModUnlockRegistry.RequireEpoch`
- **`CharacterUnlockEpochTemplate<TCharacter>`**（`TCharacter : CharacterModel`）— 解锁角色；`QueueUnlocks()` 调用 `NTimelineScreen.Instance.QueueCharacterUnlock<TCharacter>(this)` 并写 `PendingCharacterUnlock`
- **`PackDeclaredCardUnlockEpochTemplate` / `PackDeclaredRelicUnlockEpochTemplate`** — 门控类型由 `TimelineColumnPackEntry`（经 `ModEpochGatedContentRegistry`）声明而非子类重写；`Cards` / `Relics` 按 `Id` 解析，无抽象成员（除 `Id`）

关联类型（`STS2RitsuLib.Scaffolding.Content`）：`IModEpochAssetOverrides`（`EpochAssetProfile AssetProfile`、`CustomPackedPortraitPath`、`CustomBigPortraitPath`，均有默认实现）；`sealed record EpochAssetProfile(string? PackedPortraitPath = null, string? BigPortraitPath = null)` + `static EpochAssetProfile Empty`

### 用法要点

- **注册流程**：初始化阶段 `ModTimelineRegistry.For(modId)` → `RegisterStoryEpoch<TStory, TEpoch>()`（等价 `RegisterEpoch` + `ModStoryEpochBindings.Append`）→ 末尾 `RegisterStory<TStory>()`。冻结后（模型初始化前自动 `FreezeRegistrations`）注册抛异常。
- **推荐高层入口**（`STS2RitsuLib.Scaffolding.Content`）：`TimelineColumnPackEntry<TStory>(Action<TimelineColumnBuilder<TStory>>)`，Builder 提供 `Epoch<TEpoch>(Action<EpochSlotBuilder<TEpoch>>?)`、`RegisterStory()`；`EpochSlotBuilder<TEpoch>` 提供 `TimelineSlot(EpochEra, int)`、`AutoTimelineSlot(EpochEra)`、`AutoTimelineSlotBeforeColumn/AfterColumn/InColumn(…)`、`AutoTimelineSlotBeforeEpochColumn/AfterEpochColumn/InEpochColumn<TReferenceEpoch>()`、`DisableEraAxisIcon()/EnableEraAxisIcon()/EraAxisIcon(string)`、`Cards(…)/Relics(…)/Potions(…)`、`CardsFromPool<TCardPool>()/RelicsFromPool<TRelicPool>()`、`RequireAllCardsInPool/RequireAllRelicsInPool/RequireAllPotionsInPool<TPool>()`。
- **模板抽象方法小结**：`ModEpochTemplate` 本身无强制抽象成员（子类只需提供 `EpochModel.Id`）；解锁模板各有一个抽象成员 —— `CardTypes` / `RelicTypes` / `PotionTypes` / `StoryKey`；`ExpansionEpochTypes` 均为 virtual（返回非空时 `QueueUnlocks` 顺带 `QueueTimelineExpansion`）。
- **布局强制**：继承 `ModEpochTemplate` 的纪元必须已有布局（固定槽或任一 Auto 槽），否则冻结校验抛异常；`Era`/`EraPosition` sealed，只能经布局注册表间接决定。

---

## 7. Updates（`STS2RitsuLib.Updates`）

### 公共 API 清单

- **`ModUpdateChecker`**（static）— 更新检查唯一公共入口。`static IDisposable RegisterOnFirstMainMenu(ModUpdateCheckOptions options)`（同一 `ModId` 每会话只注册一次；返回的 `IDisposable` 释放后停止自动检查）；`RegisterOnFirstMainMenu(string modId, string displayName, string currentVersion, string manifestUrl, string? releasePageUrl = null)`；`static Task<ModUpdateCheckResult> CheckAsync(options | 字符串重载, CancellationToken = default)`（立即检查，不弹 UI）；`static Task<ModUpdateCheckResult> CheckAndToastAsync(…, bool showCompletionToast = false, …)`（有新版本时弹 toast）
- **`ModUpdateCheckOptions`**（sealed record）— `required string ModId`、`required string DisplayName`、`required string CurrentVersion`（如 `1.2.3`、`v1.2.3-beta.1`）、`required Uri ManifestUri`（绝对 http/https）；`Uri? ReleasePageUri`；`IReadOnlyDictionary<string,string>? Headers`；`TimeSpan Timeout = 8s`；`ToastDurationSeconds?/ToastTitle?/ToastBody?`；`bool SkipWhenLoadedFromSteamWorkshop`；`ulong? SteamWorkshopItemId`、`Assembly? InstallSourceAssembly`、`string? InstallSourcePath`；工厂 `static Create(modId, displayName, currentVersion, manifestUrl, releasePageUrl?)`
- **`ModUpdateCheckManifest`**（sealed record）— 清单 JSON 负载：`$schema` / `schema`（出现时须为 `ritsulib.update.v1`）、`latest_version`（必填，语义版本）、`release_page_url`、`title`、`message`、`localized`（按语言代码索引）
- **`ModUpdateCheckLocalizedText`** — `string? Title`、`string? Message`
- **`ModUpdateCheckStatus`** — `enum UpdateAvailable / UpToDate / InvalidData / RequestFailed / Skipped`
- **`ModUpdateCheckResult`** — `record (ModUpdateCheckStatus Status, string CurrentVersion, string? LatestVersion = null, Uri? ReleasePageUri = null, string? Title = null, string? Message = null)`
- 其余（`SimpleSemanticVersion`、`RitsuLibUpdateCheckService`、`AutomaticUpdateCheckScheduler`、`UpdateCheckNotificationQueue/SessionHistory/SessionState`）均为 **internal**，只通过 `ModUpdateChecker` 接入

### 用法要点

- **接入三步**：① 托管一份精简 JSON 清单（自托管或镜像，保证玩家可达）；② `ModUpdateChecker.RegisterOnFirstMainMenu(...)` 注册自动检查，或 `CheckAsync` / `CheckAndToastAsync` 手动触发；③ 可选填 `SteamWorkshopItemId` 使 Workshop 安装自动跳过外部检查。
- **清单 schema**：顶层键 `$schema`、`schema`、`latest_version`、`release_page_url`、`title`、`message`、`localized`（如 `{"eng": {...}, "zhs": {...}}`）。`latest_version` 必填；`schema` 若出现必须严格等于 `ritsulib.update.v1`，否则 `InvalidData`；清单 >512 KB 或请求失败分别得 `InvalidData` / `RequestFailed`。
- **版本比较**：宽松语义版本（接受 `v` 前缀、`-prerelease`、`+build`；数字段缺位按 0 补全；预发布 < 正式版）。`latest.CompareTo(current) <= 0` → `UpToDate`，否则 `UpdateAvailable`（无发布页 URL 时降级 `InvalidData`）。
- **时机**：自动检查在必要初始化前启动、按设置间隔周期运行，战斗中可配置延后；toast 一律延后到主菜单激活显示；每会话同一模组同一新版本只通知一次；点击 toast 用 `OS.ShellOpen` 打开 `manifest.release_page_url`（优先于 `options.ReleasePageUri`）。
- **占位符**：`ToastTitle/ToastBody/清单 title/message` 支持 `{display_name}`、`{current_version}`、`{latest_version}`；`localized` 精确匹配失败回退英文。

---

## 8. Unlocks（`STS2RitsuLib.Unlocks`）

### 公共 API 清单

- **`ModUnlockRegistry`** — 解锁规则注册中心（按 modId 分组，方法式注册）。`public static ModUnlockRegistry For(string modId)`；`public string ModId`；`public static bool IsFrozen`；`public static void SetEpochRequirementsIgnoredForMod(string modId, bool ignored = true)`
  - 前置锁：`RequireEpoch<TModel, TEpoch>()` / `RequireEpoch(Type modelType, Type epochType)` / `RequireEpoch(Type modelType, string epochId)`（获得/揭示纪元前保持模型锁定）
  - 局后解锁：`UnlockEpochAfterRunAs<TCharacter, TEpoch>()`、`UnlockEpochAfterWinAs<…>()`、`UnlockEpochAfterAscensionWin<…>(int ascensionLevel)`、`UnlockEpochAfterRunCount<TEpoch>(int requiredRuns, bool requireVictory = false)`（均有 Type 重载）
  - 自定义局后规则：`RegisterPostRunRule(PostRunEpochUnlockRule rule)`
  - 精英/首领计数：`UnlockEpochAfterEliteVictories<TCharacter, TEpoch>(int requiredEliteWins = 15)`、`RegisterEliteEpochRule(...)`、`UnlockEpochAfterBossVictories<…>(int requiredBossWins = 15)`、`RegisterBossEpochRule(...)`
  - 进阶相关：`UnlockEpochAfterAscensionOneWin<…>()`、`RegisterAscensionOneEpoch(ModelId characterId, string epochId)`、`RevealAscensionAfterEpoch<TCharacter, TEpoch>()`（揭示纪元后才显示进阶界面）、`RegisterAscensionRevealEpoch(ModelId, string)`
  - 角色解锁：`UnlockCharacterAfterRunAs<TCharacter, TEpoch>()`、`RegisterPostRunCharacterUnlockEpoch(ModelId characterId, string epochId)`
- **`PostRunUnlockContext`** — `record (SerializableRun Run, SerializablePlayer LocalPlayer, bool IsVictory, bool IsAbandoned, int TotalRuns, int TotalWins, ModelId CharacterId, int AscensionLevel)`
- **`PostRunEpochUnlockRule`** — `record (string EpochId, string Description, Func<PostRunUnlockContext,bool> ShouldUnlock)`；工厂 `static Create(epochId, description, shouldUnlock)`
- **`EliteEpochUnlockRule`** — `record (ModelId CharacterId, string EpochId, int RequiredEliteWins, string Description)`；`CountedEpochUnlockRule` — `record (ModelId CharacterId, string EpochId, int RequiredWins, string Description)`（首领计数）

### 用法要点

- **纯方法注册**（无特性）：`ModUnlockRegistry.For("yourModId")` → 调用注册方法；**必须**在模组初始化期、模型初始化前完成（冻结后抛异常，`IsFrozen` 可查）。
- **触发时机（框架补丁驱动，mod 无需挂钩）**：局后规则每局非放弃结算时评估（跳过已获得纪元；谓词为 true 且纪元合法时 `SaveManager.Instance.ObtainEpoch(...)` + `NGainEpochVfx` + 写 `DiscoveredEpochs`）；精英/首领计数由战斗补丁累计；进阶一胜/角色解锁在局后检查授予。
- **监听解锁**：本目录不提供事件订阅；mod 侧"监听"的方式是注册规则（谓词内判断），或需要解锁瞬间反应时自行挂钩 `SaveManager.ObtainEpoch` 或查询 `SaveManager.Instance.Progress.IsEpochObtained(...)`。也可用 `RitsuLibFramework.SubscribeLifecycle<EpochObtainedEvent>(…)`（见下节生命周期事件）。

---

## 9. Networking（`STS2RitsuLib.Networking` 及子命名空间）

### 托管动作（`STS2RitsuLib.Networking.ManagedActions`）

- **`RitsuLibManagedNetAction`** — 承载托管动作的原版 `INetAction` 消息基类。`ulong DescriptorOpcode`、`GameActionType ManagedActionType`、`byte[] Payload`、`Serialize/Deserialize`、`ToGameAction(Player)`
- **`RitsuLibManagedNetActionDescriptor<T>`** — `record (string ModuleId, string ActionKey, Func<T,byte[]> Serialize, Func<ReadOnlySpan<byte>,T> Deserialize, Func<RitsuLibManagedNetActionContext<T>,Task> Execute, GameActionType ActionType)`
- **`RitsuLibManagedNetActionContext<T>`** — `record struct (T Message, Player Player, RitsuLibManagedGameAction Action, GameActionPlayerChoiceContext PlayerChoiceContext)`
- **`RitsuLibManagedNetActions`** — 注册与发送入口（static）。`const int MaxPayloadBytes = 64*1024`；`ulong Register<T>(RitsuLibManagedNetActionDescriptor<T>)`（幂等，冲突抛异常）；`bool Request<T>(RunManager?, descriptor, T message, ulong? ownerNetId = null)`（经原版 `ActionQueueSynchronizer.RequestEnqueue` 入队，返回 true 仅表示请求已发出）
- **`RitsuLibManagedGameAction`** — `GameAction` 子类：`Player`、`DescriptorOpcode`、`Payload`、`ActionType`、`override bool RecordableToReplay => true`

### 消息尾部扩展（`STS2RitsuLib.Networking.MessageExtensions`）

- **`RitsuNetMessageTailExtensions`**（static）— 在任意原版网络消息体后追加/读取有界、带版本的多扩展载荷。`RegisterBytes<TMessage>(string extensionId, int version, Func<TMessage,byte[]?> writePayload, Action<int,ReadOnlyMemory<byte>> readPayload)`；`Write<TMessage>(PacketWriter, TMessage)`、`Read<TMessage>(PacketReader)`。约束：每消息类型每 extensionId 唯一、最多 64 条、载荷 ≤ 4 MiB、整尾 ≤ 8 MiB；`Write`/`Read` 由消息序列化补丁持有者在原版体后各调一次，其他 mod 只注册条目。

### Sidecar（`STS2RitsuLib.Networking.Sidecar`）

顶层：
- **`RitsuLibSidecarProtocol`** — `void EnsureDefaultHandlers()`：幂等准备全部内置多人支持（会话引导、内置 handler、握手路由、必需能力注册）；所有发送/注册 API 内部都会调用
- **`RitsuLibSidecar`**（static）— 信封构建。`byte[] CreateEnvelope(ulong opcode, ReadOnlySpan<byte> payload, RitsuLibSidecarWireFlags extraFlags = None, bool gzipPayload = false, ReadOnlySpan<byte> headerExtension = default)` + `…Compressed / WithDelivery(…)`
- **`RitsuLibSidecarSend`**（static）— 原始信封发送：`TrySendToHost / TrySendToPeer / TryBroadcastToReadyPeers / TryBroadcastToAllConnectedClients`；`int RecommendedChannel(NetTransferMode)`（可靠=48，不可靠=49）
- **`RitsuLibSidecarHighLevelSend`**（static）— 按投递语义自动选通道：`TrySendAsClient(RunManager?, ulong opcode, ReadOnlySpan<byte> payload, RitsuLibSidecarDeliverySemantics, ...)`、`TrySendAsHostToPeer(...)`、`TrySendAsHostBroadcast(...)`
- **`RitsuLibSidecarOpcodes`**（static）— `ulong For(string modId, string messageKind)`（xxhash64(`modId\0messageKind`)，≥ `HashDerivedOpcodeMin`）；`const ulong FixedProtocolOpcodeMaxInclusive = 0xFFFF`
- **`RitsuLibSidecarWire`**（static）— `RecommendedReliableChannel = 48`、`RecommendedUnreliableChannel = 49`、`CurrentWireFormatVersion = 2`、`MaxPayloadBytes = 4 MiB`
- **`RitsuLibSidecarDeliverySemantics`** — `enum BestEffort / StableSync / Unspecified`；**`RitsuLibSidecarWireFlags`** — wire 标志（含 gzip）
- **`RitsuLibSidecarGodotMainLoopScheduling`** — `bool TryPostToMainLoop(Action)`；扩展 `Task ContinueOnGodotMainLoopAsync(this Task)` / `<T>(this Task<T>)`

分发与消息：
- **`RitsuLibSidecarBus`**（static）— 按 opcode 分发。`RegisterHandler(ulong, Action<RitsuLibSidecarDispatchContext>)`、`UnregisterHandler(ulong)`、`ClearHandlers()`、`Task<…> WaitForNextAsync(ulong, TimeSpan timeout, Func<…,bool>? predicate = null, bool consumeOnMatch = true, CancellationToken)`、`CancelAllPendingWaits()`
- **`RitsuLibSidecarDispatchContext`**（readonly struct）— `SenderNetId`、`TransferMode`、`Channel`、`IsHostIngest`、`Envelope`、`Opcode`、`ReadOnlyMemory<byte> Payload`、`WithOwnedEnvelopeMemory()`（延后处理前必须先复制）
- **`IRitsuLibSidecarMessageCodec<T>`** — `ulong Opcode { get; }`、`bool TryDecode(ReadOnlySpan<byte>, out T?)`、`void Encode(IBufferWriter<byte>, T)`
- **`IRitsuLibSidecarSyncProcessor<in T>`** — `void Apply(T message, in RitsuLibSidecarDispatchContext context)`
- **`RitsuLibSidecarMessageBinding`** — `Register<T>(codec, processor)`、`RegisterForGodotMainLoop<T>(…)`（解码与处理切到主循环，失败回退接收线程）
- **`RitsuLibSidecarRequestReply`**（static）— 请求/应答。`static readonly TimeSpan DefaultReplyTimeout = 5s`；`SendRequestToHostAndWaitReplyAsync(...)`、`SendCorrelatedRequestToHostAndWaitReplyAsync(...)`（8 字节关联值防串扰）、`Task<TResponse> SendCorrelatedRequestToHostAsync<TRequest,TResponse>(RunManager?, requestCodec, responseCodec, request, ...)` 及 peer 系列
- **`RitsuLibSidecarTypedMessageRegistry`**（static）— `event Action<SidecarTypedMessageReceivedEvent>? TypedMessageReceived`；`ulong Register<T>(RitsuLibSidecarMessageDescriptor<T>)`（幂等，opcode 冲突抛异常）；`IDisposable Subscribe<T>(descriptor, Action<RitsuLibSidecarTypedDispatchContext<T>>)`；`bool SendToHost<T>(INetGameService?/RunManager?, descriptor, T)`、`SendToPeer<T>(netService, ulong peerNetId, descriptor, T)`、`Broadcast<T>(...)`
- **`RitsuLibSidecarMessageDescriptor<T>`** — `record (string ModuleId, string MessageKey, Func<T,byte[]> Serialize, Func<ReadOnlySpan<byte>,T> Deserialize, RitsuLibSidecarDeliverySemantics Delivery = StableSync, bool Required = false)`
- **`RitsuLibSidecarTypedDispatchContext<T>`** — `record struct (T Message, ulong SenderNetId, NetTransferMode TransferMode, int Channel, bool IsHostIngest)`；**`SidecarTypedMessageReceivedEvent`** — `(ulong Opcode, string ModuleId, string MessageKey, ulong SenderNetId)`
- **`RitsuLibSidecarJsonSerializer<T>`** — `byte[] Serialize(T)`、`T Deserialize(ReadOnlySpan<byte>)`；**`RitsuLibSidecarRequestCorrelation`** — 关联值打包/校验工具

同步消息（vanilla 式路由 + 缓冲 + 位置门控）：
- **`RitsuLibSidecarSyncMessages`**（static）— `ulong Register<T>(RitsuLibSidecarSyncMessageDescriptor<T>)`；`Send<T>(INetGameService?/RunManager?, descriptor, T)`（按服务类型自动选本地/主机广播/发主机）、`SendToHost<T>`、`SendToHostAndBroadcast<T>`、`SendToPeer<T>`、`Broadcast<T>`
- **`RitsuLibSidecarSyncMessageDescriptor<T>`** — `record (ModuleId, MessageKey, Serialize, Deserialize, Func<RitsuLibSidecarSyncMessageContext<T>,Task> Handle, bool LocationTargeted = false, bool ShouldBuffer = true, NetTransferMode Mode = Reliable, int? Channel = null, RitsuLibSidecarSyncFailurePolicy FailurePolicy = Required, RitsuLibSidecarSyncBroadcastScope BroadcastScope = ReadyPeers, bool DispatchLocalOnBroadcast = true, LogLevel LogLevel = Debug, bool ShouldBroadcast = false)`
- **`RitsuLibSidecarSyncMessageContext<T>`** — `record struct (T Message, ulong SenderNetId, INetGameService? NetService, bool IsHostIngest, RunLocation? Location)`
- **`RitsuLibSidecarSyncFailurePolicy`** — `enum Required`（游戏流程，目标 peer 必须全可达否则本地处理被抑制）/ `BestEffort`；**`RitsuLibSidecarSyncBroadcastScope`** — `enum ReadyPeers / AllConnectedPeers`

会话与事件：
- **`RitsuLibSidecarSessionManager`**（static）— `long Epoch`；事件 `SessionBound/SessionUnbound/PeerReachabilityChanged/HandshakeCompleted`；`bool CanSendToPeer(ulong)`、`TryGetReachability(ulong, out RitsuLibSidecarPeerReachability)`、`GetSupportedPeersSnapshot()`、`TryGetPeerFeatures(ulong, out RitsuLibSidecarPeerFeatures)`、`ObserveNetService(INetGameService?)`、`RegisterValidationRoute(IRitsuLibSidecarCapabilityValidationRoute)`、`SetPeerReachabilityHint(...)`、`RefreshAllReachabilityFromProviders()`
- **`RitsuLibSidecarEvents`**（static，订阅均返回 `IDisposable`）— `OnSessionBound(Action<SidecarSessionBoundEvent>)`、`OnSessionUnbound`、`OnPeerReachabilityChanged`、`OnHandshakeCompleted`、`OnTypedMessageReceived`、`OnConfigTopicChanged`、`OnRequiredCapabilityCheck`
- 事件载荷：**`SidecarSessionBoundEvent`**`(INetGameService NetService, long Epoch)`、**`SidecarSessionUnboundEvent`**`(long Epoch)`、**`SidecarPeerReachabilityChangedEvent`**`(ulong PeerNetId, RitsuLibSidecarPeerReachability Previous, Current, string Reason, long Epoch)`、**`SidecarHandshakeCompletedEvent`**`(ulong PeerNetId, RitsuLibSidecarPeerFeatures Features, long Epoch)`
- **`RitsuLibSidecarPeerReachability`** — `enum Unknown / Supported / Unsupported`；**`RitsuLibSidecarPeerFeatures`** — `[Flags] enum None / ChunkedStreams / ManagedNetActions / BrotliPayloadCompression / ModelRightClickV2 / DeveloperActionsV1`
- **`IRitsuLibSidecarCapabilityValidationRoute`** — 可插拔对等能力验证路由（按 `Order` 排序、首个非 null 判定生效）；**`RitsuLibSidecarConnectionSession`** — 握手功能缓存（`SetPeerFeatures/TryGetPeerFeatures/Clear`）；**`RitsuLibSidecarNetworkingLifecycle`** — `EnsureHooksInstalled()`

能力与配置同步：
- **`RitsuLibSidecarRequiredCapabilities`**（static）— `RitsuLibSidecarRequiredCapabilityPolicy Policy { get; set; }`（`Warn/Fail`）；`event Action<SidecarRequiredCapabilityCheckCompletedEvent>? CheckCompleted`；`RegisterRequiredCapability(string capabilityKey, Func<ulong,bool> evaluator)`；`bool ValidatePeers(IEnumerable<ulong>, out SidecarRequiredCapabilityMiss[] misses)`；载荷 `SidecarRequiredCapabilityCheckCompletedEvent(bool Passed, Policy, IReadOnlyList<SidecarRequiredCapabilityMiss> MissingByPeer)`、`SidecarRequiredCapabilityMiss(ulong PeerNetId, IReadOnlyList<string> MissingCapabilities)`
- **`RitsuLibSidecarConfigSyncService`**（static，主机权威配置主题同步）— `event Action<SidecarConfigTopicChangedEvent>? TopicChanged`；`RegisterTopic<TState,TDelta>(string topic, TState initialState, Func<ulong,TDelta,bool> canClientRequest, Func<TState,TDelta,TState> applyDelta)`；`bool TryRequestClientChange<TDelta>(INetGameService?/RunManager?, string topic, TDelta delta, string reason = "")`；`bool TryGetTopicState<TState>(string topic, out TState? state, out long revision)`；`PublishHostState(...)`；载荷 `SidecarConfigTopicChangedEvent(string Topic, long Revision, ulong ChangedByPeer, string Reason, string StateJson)`

诊断与资源：
- **`RitsuLibSidecarTrafficCounters`**（static）— `IncomingPackets/IncomingWireBytes/IncomingLogicalPayloadBytes/OutgoingSendOperations/OutgoingWireBytes`、`Reset()`
- **`RitsuLibSidecarNetDiagnosticsOptions`** — `TimeSpan IncompleteChunkStreamRetention { get; set; }`（默认 2 分钟）
- **`RitsuLibSidecarResourcePolicy`** — 上限常量：`MaxBufferedSyncContexts = 256`、`MaxBufferedSyncBytes = 8 MiB`、`MaxChunkReassemblyStreamsGlobal = 64`、`MaxChunkReassemblyStreamsPerSender = 16`

其他（internal，mod 不可直用）：`ContentModInventoryPayloadCodec`、`JoinDiagnostics`（`STS2RitsuLib.Networking.JoinDiagnostics`，mod 清单差异弹窗报告）、`StateDivergence`（`STS2RitsuLib.Networking.StateDivergence`，运行时状态比对诊断）、`ProgressDiagnosticsSnapshot`。Sidecar 的 `Layout/Chunk/Wire/Handshake/Transport` 类型（`RitsuLibSidecarEnvelope`、`RitsuLibSidecarChunk*`、`RitsuLibSidecarHandshake*`、`RitsuLibSidecarControlOpcodes` 等）虽为 public 但属协议实现细节，正常开发用高层 API 即可。

### 用法要点

1. **ManagedActions（可靠动作级同步，进原版动作队列、可回放）**：注册一次 `RitsuLibManagedNetActions.Register(descriptor)`（opcode 由 ModuleId/ActionKey 稳定派生）→ 发送 `Request(RunManager.Instance, descriptor, message)`；执行器 `Execute(ctx)` 中可直接用 `ctx.PlayerChoiceContext` 调原版命令 API；载荷 ≤ 64 KiB；要求对端声明 `ManagedNetActions` 功能，单机也走通。
2. **Sidecar 按 opcode 通用消息**：最低层 `RitsuLibSidecarBus.RegisterHandler(opcode, ctx => …)` + `RitsuLibSidecarHighLevelSend.TrySendAsClient/HostToPeer/HostBroadcast(...)`；注意 `WithOwnedEnvelopeMemory()` 后再延后处理。推荐层 `RitsuLibSidecarTypedMessageRegistry.Register/Subscribe` + `SendToHost/SendToPeer/Broadcast`。
3. **请求应答**：`RitsuLibSidecarRequestReply.SendCorrelatedRequestToHostAsync<Req,Resp>(runManager, requestCodec, responseCodec, req)`（5s 默认超时，8 字节关联值防串扰）；await 后碰 Godot 节点用 `ContinueOnGodotMainLoopAsync()`。
4. **同步消息（玩法逻辑推荐）**：`RitsuLibSidecarSyncMessages.Register` + `Send/Broadcast`；描述符设 `ShouldBuffer`（跟原版 `NetMessageBus` 缓冲对齐）、`LocationTargeted`（等 `RunLocation` 对齐）、`FailurePolicy.Required`（游戏流程强一致，目标不可达则抑制本地处理）。
5. **会话/事件**：统一入口 `RitsuLibSidecarEvents.OnHandshakeCompleted(e => …)`；`SessionManager.ObserveNetService` 由生命周期钩子驱动，握手后只有 `Supported` 的 peer 才会被 `CanSendToPeer` 放行；高层发送在目标不可达时返回 false 不抛异常。
6. **消息尾扩展**：`RitsuNetMessageTailExtensions.RegisterBytes<SomeVanillaMessage>("myMod.ext", 1, write, read)`；若你拥有该消息的序列化补丁，在原版体后补一次 `Write`/`Read`；越界条目自动降级省略，不破坏对端解析。

---

## 10. 特性（Attribute）清单（全部）

### 10.1 自动注册基础（`STS2RitsuLib.Interop.AutoRegistration`）

- **`AutoRegistrationAttribute`**（abstract，: Attribute）— 所有声明式注册的基类。属性：`int Order { get; set; }`（同阶段局部排序，越小越先）；`bool Inherit { get; set; }`（基类声明是否应用到派生类型；每个逻辑槽位取最近声明，直接声明可替换池/数量/路径/位置/阈值，不同目标 ID 累加）
- **`ContentRegistrationAttribute`**（abstract）— 经 `ModContentRegistry` 分发的内容注册基类

> 以下内容注册特性 **目标均为 Class，`AllowMultiple = true, Inherited = false`**，触发时机：模组初始化/类型发现阶段的自动注册管线（需先 `ModTypeDiscoveryHub.RegisterModAssembly`），模型初始化前冻结。

| 特性 | 参数（构造 + 命名属性） | 用途 |
|---|---|---|
| `[RegisterCharacter]` / `[RegisterAct]` / `[RegisterMonster]` / `[RegisterPower]` / `[RegisterOrb]` / `[RegisterEnchantment]` / `[RegisterAffliction]` / `[RegisterAchievement]` / `[RegisterSingleton]` | 无参 | 注册对应模型类型 |
| `[RegisterModelCapability]` | 命名：`string? StableEntryStem`、`string? FullPublicEntry` | 注册为模型能力 |
| `[RegisterDefaultModelCapability(Type targetModelType)]` | 命名：`string? ModifierId` | 把能力加入目标模型类型的默认能力集 |
| `[RegisterGoodModifier]` / `[RegisterBadModifier]` | 命名：`int ModifierListSortOrder`（负值插到列表段前） | 注册每日正面/负面特效 |
| `[RegisterMutuallyExclusiveModifierGroup(params Type[] memberTypes)]` | — | 特效互斥组 |
| `[RegisterSharedCardPool]` / `[RegisterSharedRelicPool]` / `[RegisterSharedPotionPool]` / `[RegisterSharedEvent]` / `[RegisterSharedAncient]` / `[RegisterGlobalEncounter]` | 无参 | 共享池/事件/全局遭遇 |
| `[RegisterCard(Type poolType)]` | 命名：`string? StableEntryStem`、`string? FullPublicEntry` | 注册卡牌到指定池 |
| `[RegisterRelic(Type poolType)]` | 同上 | 注册遗物到指定池 |
| `[RegisterPotion(Type poolType)]` | 同上 | 注册药水到指定池 |
| `[RegisterTrashHeapCard]` / `[RegisterTrashHeapRelic]` | 无参 | "垃圾堆"事件候选 |
| `[RegisterCharacterStarterCard(Type characterType, int count = 1)]` / `…StarterRelic…` / `…StarterPotion…` | — | 角色初始内容 |
| `[RegisterActEncounter(Type actType)]` / `[RegisterActEvent(Type actType)]` / `[RegisterActAncient(Type actType)]` | — | 按阶段注册遭遇/事件/先古事件 |

关键词 / 标签 / 牌堆（同上目标与时机）：
- **`[RegisterOwnedKeyword(string localKeywordStem)]`** — 命名：`string TitleTable = "card_keywords"`、`string? TitleKey`、`string? DescriptionTable`、`string? DescriptionKey`、`string? IconPath`、`ModKeywordCardDescriptionPlacement CardDescriptionPlacement`、`bool IncludeInCardHoverTip = true` — 注册 mod 归属关键词
- **`[RegisterOwnedCardKeyword(string localKeywordStem)]`** — 命名：`IconPath`、`CardDescriptionPlacement`、`IncludeInCardHoverTip` — 按游戏卡牌关键词本地化约定注册
- **`[RegisterOwnedCardTag(string localCardTagStem)]`** — 注册 mod 归属 CardTag ID
- **`[RegisterOwnedCardPile(string localPileStem)]`** — 命名：`ModCardPileScope Scope = CombatOnly`、`ModCardPileUiStyle Style = Headless`、`ModCardPileAnchorKind AnchorKind = StyleDefault`、`float AnchorOffsetX/Y`、`float AnchorCustomX/Y/PivotX/PivotY`、`string? IconPath`、`string[]? Hotkeys`、`bool CardShouldBeVisible`、`ModExtraHandLayoutDirection ExtraHandDirection`、`float ExtraHandSpacing = 110f`、`ExtraHandCardScale = 0.65f`、`ExtraHandHoverScale = 1f`、`bool ExtraHandShowPlayableGlow = true`、`bool ExtraHandAllowCardPlay = true`、`float HoverTipOffsetX/Y`、`ModCardPileHoverTipPlacement HoverTipPlacement` — 经 `ModCardPileRegistry` 注册牌堆（标注类可实现 `IModCardPileHandler`）

时间线 / 解锁（同上目标与时机）：
- **`[RegisterEpoch]`** / **`[RegisterStory]`** — 注册时间线节点/故事
- **`[RegisterStoryEpoch(Type storyType)]`** — 把纪元加入指定故事
- **`[AutoTimelineSlot(EpochEra era)]`**、`[AutoTimelineSlotBeforeColumn(EpochEra)]`、`[AutoTimelineSlotBeforeEpochColumn(Type refEpoch)]`、`[AutoTimelineSlotAfterColumn(EpochEra)]`、`[AutoTimelineSlotAfterEpochColumn(Type)]`、`[AutoTimelineSlotInColumn(EpochEra)]`、`[AutoTimelineSlotInEpochColumn(Type)]` — 自动时间线列放置
- **`[RegisterEpochCards(params Type[] cardTypes)]`** — 纪元揭示卡牌并作为其前置
- **`[RequireAllCardsInPool(Type poolType)]`** — 卡池全部卡牌需该纪元
- **`[RegisterEpochRelicsFromPool(Type poolType)]`** — 遗物池全部遗物作为该纪元解锁内容
- **`[RequireEpoch(Type epochType)]`** — 内容解锁前置纪元
- **`[UnlockEpochAfterRunAs(Type epochType)]`** / **`[UnlockEpochAfterWinAs(Type)]`** / **`[UnlockEpochAfterAscensionWin(Type, int ascensionLevel)]`** / **`[UnlockEpochAfterEliteVictories(Type, int requiredEliteWins = 15)]`** / **`[UnlockEpochAfterBossVictories(Type, int requiredBossWins = 15)]`** / **`[UnlockEpochAfterAscensionOneWin(Type)]`** / **`[RevealAscensionAfterEpoch(Type)]`** / **`[UnlockCharacterAfterRunAs(Type)]`** — 声明式解锁规则（与 `Unlocks.ModUnlockRegistry` 方法注册等价）

特殊内容映射（同上）：
- **`[RegisterArchaicToothTranscendence(Type ancientCardType)]`** — "古老牙齿"超越映射
- **`[RegisterDustyTomeCard(Type characterType)]`** — "尘封魔典"优先候选
- **`[RegisterTouchOfOrobasRefinement(Type upgradedRelicType)]`** — "欧洛巴斯之触"精炼映射

TopBar / 节点挂载 / SmartFormat：
- **`[RegisterOwnedTopBarButton(string localButtonStem)]`** — 命名：`string? IconPath`、`int ButtonOrder`、`float OffsetX`、`float OffsetY`；标注类须实现 `IModTopBarButtonHandler`（详见第 5 节）
- **`[RegisterNodeAttachment(Type parentType, string localId)]`** / **`[RegisterNodeAttachmentFromScene(Type parentType, string localId, string scenePath)]`** / **`[RegisterNodeAttachmentFromConvertedScene(...)]`** — 在父节点 `_Ready` 生命周期声明式挂载子节点；命名属性：`Type? NodeType`、`string? NodeName`、`bool UniqueNameInOwner`、`bool IncludeDerivedParentTypes = true`、`NodeAttachmentDuplicatePolicy DuplicatePolicy`、`NodeAttachmentAddMode AddMode`、`NodeAttachmentSetupTiming SetupTiming`、`int ChildIndex = -1`、`string? InsertBeforeName/InsertAfterName`、`bool QueueFreeReplacedNode = true`
- **`[RegisterSmartFormatter]`** / **`[RegisterSmartFormatSource]`** — 注册游戏本地化的 SmartFormat 扩展

### 10.2 互操作（`STS2RitsuLib.Interop`）

- **`[ModInterop(string modId, string? type = null)]`** — 目标：Class（`Inherited = false`）。标注类在运行时被重写，无需编译期引用即可调用另一 mod 程序集成员（公共方法/属性/嵌套包装类）
- **`[AssemblyInterop(string? type = null)]`** — 目标：Class。转发到程序集限定 CLR 类型（`Namespace.Type, AssemblyName`）
- **`[InteropTarget(string type, string? name)]`** / **`[InteropTarget(string? name = null)]`** — 目标：Property | Class | Method。覆盖远端类型或成员名
- **`[InteropAnyParam]`** — 目标：Parameter。重载解析时该参数匹配任意类型
- **`[RitsuLibOwnedBy(string modId)]`** — 目标：Class。覆盖类型上自动注册特性的归属 modId（`AutoRegistration` 命名空间）

### 10.3 ModSettings（`STS2RitsuLib.Settings` 下，简述）

- **`[ModSettingsPage(string modId, string? pageId = null)]`**（Class）、**`[ModSettingsSection(string id)]`**（Class，AllowMultiple）
- 条目（Property | Field）：**`[ModSettingsBinding]`**、**`[ModSettingsToggle(string id, string sectionId)]`**、**`[ModSettingsSlider(id, sectionId, min, max, step?)]`**、**`[ModSettingsIntSlider(id, sectionId, int min, int max, int step = 1)]`**、**`[ModSettingsString(id, sectionId)]`**、**`[ModSettingsMultilineString]`**、**`[ModSettingsColor]`**、**`[ModSettingsKeyBinding]`**、**`[ModSettingsChoice]`**
- 动作（Method）：**`[ModSettingsButton(id, sectionId)]`**、**`[ModSettingsParagraph]`**、**`[ModSettingsHeader]`**、**`[ModSettingsInfoCard]`**、**`[ModSettingsRuntimeHotkeySummary]`**、**`[ModSettingsImage]`**、**`[ModSettingsSubpage(id, sectionId, string targetPageId)]`**、**`[ModSettingsCustomEntry]`**

---

## 11. 关键用法模式

### 11.1 Entry 初始化（`[ModInitializer]`）

```csharp
using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using STS2RitsuLib.Patching.Core;

[ModInitializer(nameof(Initialize))]
public static class MyModEntry
{
    public const string ModId = "MyMod";
    public static Logger Logger { get; private set; } = null!;

    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();
        Logger = RitsuLibFramework.CreateLogger(ModId);                 // ① 日志
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);        // ② 关键：注册程序集，CLR 注解注册才能被发现
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger); // ③ 仅当 mod 有挂 .tscn 的 C# 脚本
        var patcher = RitsuLibFramework.CreatePatcher(ModId, "main");    // ④ 补丁
        patcher.RegisterPatches<MyModPatches>();
        RitsuLibFramework.ApplyRequiredPatcher(patcher, DisableMod);     // ⑤ 必要补丁无法应用时禁用 mod
    }
}
```

### 11.2 注册流程（三种方式）

1. **属性式（推荐，注册点贴近类定义）**：模型类上加 `[RegisterCard(typeof(MyCardPool))]` 等特性；文本写进对应本地化表（如 `MY_MOD_CARD_MY_STRIKE.title/description`）。由自动注册管线扫描（依赖 11.1 的 `RegisterModAssembly`）。
2. **程序式**：`xxxRegistry.For(modId)` → 注册方法。典型：`RunSavedDataStore.For(id).Register<T>(key)`、`ModTopBarButtonRegistry.For(id).RegisterOwned(...)`、`ModTimelineRegistry.For(id).RegisterStoryEpoch<TStory,TEpoch>()`、`ModUnlockRegistry.For(id).RequireEpoch<TModel,TEpoch>()`、`RuntimeHotkeyService.Register(...)`、`ModUpdateChecker.RegisterOnFirstMainMenu(...)`。
3. **Content Pack（批量）**：`RitsuLibFramework.CreateContentPack("MyMod").Card<TPool,TCard>().Relic<TRelicPool,TRelic>().Apply()`；时间线用 `TimelineColumnPackEntry<TStory>(builder => ...)`。

**共同约束**：所有注册必须在**模型初始化前**完成——各注册表（`ModTimelineRegistry`、`ModUnlockRegistry`、`ModEpochGatedContentRegistry`、`RunSavedDataStore` 槽位等）在模型初始化前 `FreezeRegistrations`，冻结后（`IsFrozen == true`）注册抛 `InvalidOperationException`。

### 11.3 生命周期订阅（`RitsuLibFramework`）

- `public static IDisposable SubscribeLifecycle<TEvent>(Action<TEvent> handler, bool replayCurrentState = true) where TEvent : IFrameworkLifecycleEvent` — 类型化订阅；`replayCurrentState = true` 时立即用最近一次可重放事件调用 handler；释放返回值退订
- `SubscribeLifecycle<TEvent>(Action<TEvent, IDisposable> handler, bool replayCurrentState = true)` — 回调内可拿到订阅自身
- `SubscribeLifecycleOnce<TEvent>(...)` — 一次性订阅
- `SubscribeLifecycle(ILifecycleObserver observer, bool replayCurrentState = true)` — 观察者模式（`ILifecycleObserver.OnEvent(IFrameworkLifecycleEvent)`）
- 事件契约（`readonly record struct`，命名空间 `STS2RitsuLib`；`IReplayableFrameworkLifecycleEvent` 标记可重放）：
  - **框架级**：`FrameworkInitializingEvent`、`FrameworkInitializedEvent`（可重放）、`ProfileServicesInitializingEvent`、`ProfileServicesInitializedEvent`（可重放）；另有 `GameReadyEvent`、`MainMenuReadyEvent`、`DeferredInitializationCompletedEvent`、`ModelRegistryInitializedEvent`、`RunStartedEvent/RunLoadedEvent/RunEndedEvent`、`EssentialInitializationStartingEvent` 等（`Lifecycle\` 与核心补丁发布）
  - **存档（SaveLifecycleContracts）**：`ProfileIdInitializedEvent`（可重放）、`ProfileSwitchingEvent`、`ProfileSwitchedEvent`（可重放）、`RunSavingEvent`、`RunSavedEvent`、`ProgressSavingEvent`、`ProgressSavedEvent`、`ProfileDeletingEvent`、`ProfileDeletedEvent`
  - **解锁（UnlockLifecycleContracts）**：`EpochObtainedEvent(SaveManager, string EpochId, …)`、`EpochRevealedEvent(..., bool IsDebug, …)`、`UnlockIncrementedEvent(SaveManager, int TotalUnlocks, string? PendingEpochId, …)`
  - **战斗（CombatLifecycleContracts）**：`CombatStartingEvent`、`CombatEndedEvent`、`CombatVictoryEvent`、`SideTurnStarting/Started`、`CardPlaying/Played`、`CardMovedBetweenPiles`、`CardDrawn/Discarded/Exhausted`、`BeforeFlush/CardsFlushed`、`CreatureDying/Died` 等
  - **房间/奖励/其他**：`RoomEntering/Entered/Exited`、`ActEntering/Entered`、`GoldGained/Lost`、`PotionProcured/Discarded`、`RelicObtained/Removed`、`RewardTaken`、`GameOverScreenCreated`，以及 `AdditionalHookLifecycleContracts`（`AttackStarting/Ended`、`BlockGaining/Gained/Broken/Cleared`、`EnergyGained/Reset/Spent`、`HandDrawing/Emptied`、`PlayerTurnStarted`、`PotionUsing/Used`、`Shuffled`、`StarsGained/Spent`、`Summoned`、`ExtraTurnTaken`、`SideTurnEnding/Ended`、`ItemPurchased`、`MapGenerated`、`RestSiteHealed/Smithed` 等）
- RunData 专属事件：`RunSavedDataPreparingEvent`、`RunSavedDataLobbyStagingEvent`（均非 replayable，见第 2 节）

### 11.4 目录速查（注册时机一览）

| 能力 | 入口 | 时机约束 |
|---|---|---|
| 动态枚举 | `DynamicEnumValueRegistry<TEnum>.For(modId).RegisterOwned(stem)` | 初始化/类型发现期 |
| 局内/按玩家数据 | `RunSavedDataStore.For(modId).Register<T>(key)` / `RegisterPerPlayer<T>` | 初始化期（冻结前） |
| 存档兼容（原始 JSON） | `RawProgressBridge.Instance`（`IRawProgressCommitBridge`） | 运行时任意时刻 |
| 热键 | `RuntimeHotkeyService.Register(...)` | 任意时刻（首次自动初始化） |
| 顶栏按钮 | `ModTopBarButtonRegistry.For(modId)` 或 `[RegisterOwnedTopBarButton]` | 原版 `NTopBar._Ready` 前 |
| 时间线 | `ModTimelineRegistry.For(modId)` / `TimelineColumnPackEntry` | 模型初始化前 |
| 更新检查 | `ModUpdateChecker.RegisterOnFirstMainMenu(...)` | 任意时刻 |
| 解锁规则 | `ModUnlockRegistry.For(modId)` | 模型初始化前 |
| 网络动作/消息 | `RitsuLibManagedNetActions.Register` / `RitsuLibSidecarTypedMessageRegistry.Register` / `RitsuLibSidecarSyncMessages.Register` | 任意时刻（幂等） |

---

*本文档由源码自动整理，覆盖 `STS2-RitsuLib` 上述 9 个目录的公共 API 面；内部实现（Harmony 补丁、协议内部类型、云同步细节等）已按"对 mod 制作有实用价值"原则省略。*
