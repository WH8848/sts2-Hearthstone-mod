# RitsuLib 系统教程精华（G7）

> 来源抓取文件（均可回查）：
> `docs_04-ritsulib_04-23-lifecycle-events.txt`（生命周期事件）
> `docs_04-ritsulib_04-24-patch-system.txt`（补丁系统）
> `docs_04-ritsulib_04-25-update-checker.txt`（更新检查）
> `docs_04-ritsulib_04-26-runtime-hotkey.txt`（运行时热键）
> `docs_04-ritsulib_04-27-content-registry.txt`（内容注册）
> `docs_04-ritsulib_04-28-node-attachment.txt`（节点附加）
> `docs_04-ritsulib_04-29-telemetry.txt`（数据遥测）
> `docs_04-ritsulib_04-30-mod-integration.txt`（模组联动）

---

## 1. 生命周期事件（04-23）

**核心概念**：`RitsuLibFramework.SubscribeLifecycle<T>(handler)`（lambda 方式）或实现 `ILifecycleObserver`（`OnEvent(IFrameworkLifecycleEvent evt)`），在 `Entry.Init` 中订阅。

```csharp
// lambda 订阅（返回句柄，可 Dispose 取消）
var sub = RitsuLibFramework.SubscribeLifecycle<GameReadyEvent>(evt =>
    Logger.Info($"游戏已就绪：{evt.Game}"));

// 接口订阅（一个类型处理多种事件）
public sealed class MyLifecycleObserver : ILifecycleObserver
{
    public void OnEvent(IFrameworkLifecycleEvent evt)
    {
        if (evt is CombatStartingEvent) Logger.Info("战斗即将开始");
        else if (evt is RunEndedEvent run) Logger.Info($"胜利={run.IsVictory}, 放弃={run.IsAbandoned}");
    }
}
RitsuLibFramework.SubscribeLifecycle(new MyLifecycleObserver());
```

**常用事件分类**（每个事件带 `OccurredAtUtc`；事件源码在 `STS2RitsuLib` 命名空间下 `*LifecycleContracts.cs`）：

- **框架**：`FrameworkInitializedEvent`、`ProfileServicesInitializing/InitializedEvent`
- **游戏引导**：`EssentialInitializationStarting/CompletedEvent`、`DeferredInitializationStarting/CompletedEvent`、`ContentRegistrationClosedEvent`（⚠️ `ModelDb.Init` 开始时冻结 mod 注册，之后不能再注册卡牌/人物）、`ModelRegistryInitializing/InitializedEvent`、`ModelIdsInitializedEvent`（此后可用 `ModelDb.GetId<T>()`）、`GameTreeEnteredEvent`、`GameReadyEvent`
- **局内**：`RunStartedEvent`、`RunLoadedEvent`、`RunEndedEvent`（`Run/IsVictory/IsAbandoned`）
- **房间/章节**：`RoomEntering/Entered/ExitedEvent`、`ActEntering/EnteredEvent`、`RewardsScreenContinuingEvent`
- **战斗**：`CombatStartingEvent`、`CombatVictoryEvent`、`CombatEndedEvent`、`SideTurnStarting/StartedEvent`、`CardPlaying/PlayedEvent`、`CardDrawn/Discarded/ExhaustedEvent`、`CardMovedBetweenPilesEvent`、`BeforeFlushEvent`（回合末即将结算）、`CardsFlushedEvent`、`CreatureDying/DiedEvent`（Died 的 `WasRemovalPrevented=true` 可能没真死）
- **奖励**：`GoldGained/LostEvent`、`RelicObtained/RemovedEvent`、`PotionProcured/DiscardedEvent`、`RewardTakenEvent`
- **解锁**：`EpochObtainedEvent`（获得非解锁）、`EpochRevealedEvent`（解锁）、`UnlockIncrementedEvent`
- **存档**：`ProfileIdInitializedEvent`、`ProfileSwitching/SwitchedEvent`、`RunSaving/SavedEvent`、`ProgressSaving/SavedEvent`、`ProfileDeleting/DeletedEvent`、`ProfileDataReadyEvent`（可读写 `ModDataStore`）、`ProfileDataChangedEvent`、`ProfileDataInvalidatedEvent`

**注意事项/坑**：
- 事件已发生后订阅默认只收新事件；`SubscribeLifecycle` 第二个参数 `replayCurrentState: true` 可让部分事件（如 `GameReadyEvent`）立刻重放一次当前状态。
- 每个事件的参数见上表（如 `RunState`、`CombatState`、`Card`、`Side` 等），写 handler 前先查参数名。

---

## 2. 补丁系统（04-24）

**核心概念**：RitsuLib 在 Harmony 之上封装，统一声明/注册/失败处理。中大型项目推荐；原始 Harmony 仍可用。

**补丁类实现 `IPatchMethod`**：

```csharp
public class LogReleaseGamePatch : IPatchMethod
{
    public static string PatchId => "test_log_release_game";   // 唯一，防撞车
    public static string Description => "Print IsReleaseGame";  // 用途说明
    public static bool IsCritical => false;                     // false=失败不崩游戏
    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(NGame), nameof(NGame.IsReleaseGame))];      // 要改的原版方法

    public static void Postfix(ref bool __result)               // 可用 Prefix/Postfix/Transpiler
        => Entry.Logger.Info($"NGame.IsReleaseGame = {__result}");
}
```

**注册（Entry.Init 中）**：

```csharp
var patcher = RitsuLibFramework.CreatePatcher(ModId, "core-patches"); // 每个逻辑区域一个 patcher
patcher.RegisterPatch<LogReleaseGamePatch>();
// patcher.RegisterPatches<MyPatchSet>();  // 批量登记
if (!patcher.PatchAll()) throw new InvalidOperationException("Critical patches failed.");
// ↑ 先全部注册完，最后统一 PatchAll() 一次
```

**分组注册**：`MyPatchSet : IModPatches`，在 `static void AddTo(ModPatcher patcher)` 里逐个 `RegisterPatch<T>()`，然后 `patcher.RegisterPatches<MyPatchSet>()`（需 `using STS2RitsuLib.Patching.Core;`）。

**高级用法**：
- 忽略缺失目标（版本差异）：`new(typeof(NGame), "SomeOptionalMethod", ignoreIfMissing: true)`
- 一个补丁作用多目标：`GetTargets()` 返回多个 `ModPatchTarget`
- 动态补丁（运行时发现目标）：

```csharp
using STS2RitsuLib.Patching.Builders;
var builder = new DynamicPatchBuilder("my_dynamic")
    .AddMethod(targetType: typeof(NGame), methodName: nameof(NGame.IsReleaseGame),
        postfix: DynamicPatchBuilder.FromMethod(typeof(MyRuntimePatch), nameof(MyRuntimePatch.Postfix)),
        isCritical: false, description: "Dynamic Patch");
patcher.ApplyDynamic(builder, rollbackOnCriticalFailure: false);
```

**注意事项/坑**：`RegisterPatch<T>` 等扩展方法在 `STS2RitsuLib.Patching.Core`；模型类在 `STS2RitsuLib.Patching.Models`。

---

## 3. 更新检查（04-25）

**核心概念**：只负责"检测到新版本→主菜单弹 toast→带玩家去发布页"，**不下载不安装**。资源站点（manifest JSON）需自备。

**注册**（Entry 初始化中）：

```csharp
using STS2RitsuLib;
using STS2RitsuLib.Updates;
RitsuLibFramework.RegisterModUpdateCheck(new()
{
    ModId = Entry.ModId,
    DisplayName = "Test Mod",
    CurrentVersion = "1.2.0",
    ManifestUri = new Uri("https://cdn.example.com/test-mod/update.json"), // 必须 https 绝对 URL
    ReleasePageUri = new Uri("https://example.com/test-mod/releases"),    // manifest 无 release_page_url 时的备用
});
```

**manifest JSON 格式**（schema `"ritsulib.update.v1"`）：

```json
{
  "schema": "ritsulib.update.v1",
  "latest_version": "1.2.3",
  "release_page_url": "https://example.com/test-mod/releases/tag/v1.2.3",
  "localized": {
    "eng": { "title": "Test Mod update available",
             "message": "Test Mod {latest_version} is available. Current: {current_version}." },
    "zhs": { "title": "Test Mod 有更新",
             "message": "Test Mod {latest_version} 已发布，当前版本：{current_version}。点击打开发布页。" }
  }
}
```

Toast 文案占位符：`{display_name}`、`{current_version}`、`{latest_version}`。

**自定义检查（不弹 UI，自定反馈）**：

```csharp
var result = await RitsuLibFramework.CheckForModUpdateAsync(Entry.ModId, "Test Mod", "1.2.0",
    "https://example.com/test-mod/update.json", "https://example.com/test-mod/releases");
switch (result.Status)
{
    case ModUpdateCheckStatus.UpdateAvailable:
        RitsuToastService.ShowInfo(result.Message ?? $"发现新版本 {result.LatestVersion}。",
            result.Title ?? "Test Mod 有更新",
            result.ReleasePageUri == null ? null : () => OS.ShellOpen(result.ReleasePageUri.ToString()));
        break;
    case ModUpdateCheckStatus.UpToDate: /* ShowInfo("已是最新") */ break;
    case ModUpdateCheckStatus.InvalidData:
    case ModUpdateCheckStatus.RequestFailed: /* ShowWarning */ break;
}
```

**GitHub Pages 免费托管流程**（国内访问可能有问题，可加 Cloudflare 转发）：
1. 仓库 public；根目录放 `update.template.json`（含 `$schema: https://sts2-ritsulib.ritsukage.com/ritsulib-update.schema.json`）。
2. Settings → Pages → `Deploy from a branch`，分支 main、目录 `/ (root)`。发布后地址：`https://<用户名小写>.github.io/<仓库名>/update.json`。
3. `tools/generate-update-manifest.mjs` 读取 `{modId}.json`（游戏读的 mod 清单）的 `version` 写入模板的 `latest_version`，输出到 `public/update.json`；配 `.github/workflows/deploy.yml`（actions/checkout@v6 → `node tools/generate-update-manifest.mjs` → configure-pages/upload-pages-artifact/deploy-pages）。
4. 每次发布只需改 `{modId}.json` 的 version 和 `CurrentVersion` 再推送。

**注意事项/坑**：新版本但 manifest 和注册参数都没有发布页 → 结果是 `InvalidData`，不显示 toast；ReleasePageUri 暂时没有可填仓库主页。

---

## 4. 运行时热键（04-26）

**核心概念**：支持多绑定、重绑定、修饰键；输入框聚焦或开发者控制台打开时自动不触发。

```csharp
using STS2RitsuLib.RuntimeInput;
private static IRuntimeHotkeyHandle? _reloadHotkey; // 存句柄供未来解绑

_reloadHotkey = RuntimeHotkeyService.Register(
    "Ctrl+Shift+R",                     // 组合修饰键字符串；或数组 ["F5","Ctrl+Shift+R"]，任一键触发、自动去重
    () => Logger.Info("热键已触发！"),
    new RuntimeHotkeyOptions
    {
        Id = "my_mod_reload",           // 稳定 id，便于查找/保存配置
        DisplayName = "重新加载配置",
        Description = "重新加载 Mod 配置文件。",
        Category = "My Mod",            // 热键分组名
        // MarkInputHandled = true,                 // 触发后标记输入已处理，默认 false
        // SuppressWhenTextInputFocused = true,     // 输入框聚焦不触发，默认 true
        // SuppressWhenDevConsoleVisible = true,    // 控制台打开不触发，默认 true
    });
// _reloadHotkey?.Unregister();  // 注销
```

**运行时改键**：

```csharp
if (_reloadHotkey?.TryRebind("Ctrl+Alt+R", out var normalized) == true)
    Logger.Info($"已重绑定为 {normalized}");   // normalized 可写入配置
```

**查询已注册热键**：`RuntimeHotkeyService.GetRegisteredHotkeyDetails()` → 遍历取 `info.Id`、`info.CurrentBindings`（Join(" / ")）。

**绑定字符串格式**：`[修饰键+][修饰键+]主键`，`+` 连接、不区分大小写。修饰键：`Ctrl`、`Alt`、`Shift`、`Meta`（Win/Command）。示例：`F5`、`Ctrl+S`、`Ctrl+Shift+R`、`Alt+F4`。

---

## 5. 内容注册（04-27）

**核心概念**：RitsuLib 至少支持三种注册方式（不止注解式）。

**方式一：注解式**（内容与注册关系天然在一起时）：

```csharp
[RegisterCard(typeof(TestCardPool))]
[RegisterCharacterStarterCard(typeof(TestCharacter), 4)]
public sealed class BlazingStrike : ModCardTemplate(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
```

**方式二：ContentPack**（需要统一显示注册）：

```csharp
RitsuLibFramework.CreateContentPack(ModId)
    .Character<TestCharacter>(c => c
        .AddStartingRelic<TestStarterRelic>(1)
        .AddStartingCard<BlazingStrike>(4)
        .AddStartingCard<TestDefend>(4))
    .Card<TestCardPool, BlazingStrike>()
    .Card<TestCardPool, TestDefend>()
    .Relic<TestRelicPool, TestStarterRelic>()
    .Power<TestPower>()
    .ActEncounter<TestAct, TestEncounter>()
    .Story<TestStory>()
    .Epoch<TestEpoch>()
    .StoryEpoch<TestStory, TestEpoch>()
    .RequireEpoch<TestRareCard, TestEpoch>()
    .UnlockEpochAfterWinAs<TestCharacter, TestEpoch>()
    .Apply();   // ⚠️ 只在一串最后调用一次
```

**方式三：直接使用注册器**（部分功能需手动注册时）：

```csharp
var content = RitsuLibFramework.GetContentRegistry(Entry.ModId);   // 或 ModContentRegistry.For
content.RegisterCard<TestCardPool, BlazingStrike>();

var keywords = RitsuLibFramework.GetKeywordRegistry(Entry.ModId);
keywords.RegisterCardKeywordOwnedByLocNamespace("burning",
    iconPath: "res://Test/images/keywords/burning.png",
    cardDescriptionPlacement: ModKeywordCardDescriptionPlacement.BeforeCardDescription);

var cardTags = RitsuLibFramework.GetCardTagRegistry(Entry.ModId);
cardTags.RegisterOwned("heavy");
```

**注意事项/坑**：ContentPack builder 按添加顺序执行——被其他规则引用的模型要先注册（如 `RequireEpoch` 的 Epoch 需先注册）。

---

## 6. 节点附加（04-28）

**核心概念**：给原版 Godot 节点挂自己的子节点（给已有场景"打补丁"），如战斗 UI 加小面板。

**方式一：显式注册**（Entry.Init 中，代码创建节点）：

```csharp
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;
ModNodeAttachmentRegistry.For(ModId)
    .RegisterReadyChild<NCombatUi, TestCombatUiBadge>("combat_ui_badge",
        static _ => new TestCombatUiBadge(),
        static (parent, node) => node.Bind(parent),   // 绑定/布局回调
        new NodeAttachmentOptions
        {
            Name = "TestCombatUiBadge",
            Order = 10,   // 同父节点多个 attachment 排序，小者先执行
            DuplicatePolicy = NodeAttachmentDuplicatePolicy.ReuseExistingByName,
            SetupTiming = NodeAttachmentSetupTiming.AfterAdd,
        });
```

子节点类 `: Control` 正常写 `_Ready()` 即可。从场景创建：`RegisterReadyChildFromScene<NCombatUi, Control>("combat_status_panel", "res://Test/scenes/ui/combat_status_panel.tscn", setup回调, options)` —— ⚠️ 要求场景根节点本身就是 `TNode`。若根节点需经 RitsuLib 场景转换：`RegisterReadyChildFromConvertedScene<TParent, TNode>(...)` —— ⚠️ `TNode` 需公开无参构造函数。

**方式二：自动注册**（需已调用 `ModTypeDiscoveryHub.RegisterModAssembly(...)`）：

```csharp
[RegisterNodeAttachment(typeof(NCombatUi), "turn_counter",
    NodeName = "TestTurnCounter",
    DuplicatePolicy = NodeAttachmentDuplicatePolicy.ReuseExistingByName)]
public sealed partial class TestTurnCounter : Label, INodeAttachmentSetup
{
    public void Setup(Node parent, Node node) { Text = "Turn"; Position = new Vector2(40f, 84f); }
}
```

另有 `[RegisterNodeAttachmentFromScene]`、`[RegisterNodeAttachmentFromConvertedScene]`。

**取回附加节点**（注册只负责挂载，不负责取回）：

```csharp
if (ModNodeAttachmentRegistry.For(Entry.ModId)
    .TryGetAttached<NCombatUi, TestCombatUiBadge>(combatUi, "combat_ui_badge", out var badge))
    badge.Visible = true;

// 或全局 id
var id = ModNodeAttachmentRegistry.GetQualifiedNodeAttachmentId(Entry.ModId, "combat_ui_badge");
ModNodeAttachmentRegistry.TryGetAttachedById<NCombatUi, TestCombatUiBadge>(combatUi, id, out var badge);
```

**NodeAttachmentOptions 参数表**：

| 选项 | 用途 |
|---|---|
| `Name` / `NodeName` | 子节点名称，也是重复策略查找依据 |
| `Order` | 同父多 attachment 排序，小者先 |
| `DuplicatePolicy` | 同名子节点：复用/跳过/替换/报错/允许重名 |
| `AddMode` | 默认 `AddChildSafely`；必须立刻入树用 `AddChildDirect` |
| `SetupTiming` | setup 在入树前/后执行 |
| `ChildIndex` | 挂载后移到指定下标 |
| `InsertBeforeName` / `InsertAfterName` | 移到某同级节点前/后 |
| `UniqueNameInOwner` | 设 `UniqueNameInOwner` 并把父节点设为 owner |
| `IncludeDerivedParentTypes` | 父类型子类是否也应用，默认 true |

**注意事项/坑**：
- `TryGetAttached` 不会创建节点，只有父节点 ready 时才真正挂载。
- `ChildIndex`、`InsertBeforeName`、`InsertAfterName` 三者只能选一个。
- 只要 `DuplicatePolicy` 不是 `AllowDuplicateName`，就必须设置 `Name`/`NodeName`。

---

## 7. 数据遥测（04-29）

**核心概念**：RitsuLib 只提供发送系统，不提供收集服务/服务器。注册后玩家会收到是否接受数据发送的请求，**只有接受才发送**。一个申请方对应一个固定后端和一组授权请求；`ApplicantId` 通常设为 Mod id。

**注册申请方**：

```csharp
using STS2RitsuLib.Telemetry;
TelemetryRegistry.RegisterApplicant(new TelemetryApplicant
{
    ApplicantId = Entry.ModId,
    OwnerModId = Entry.ModId,
    DisplayName = "Test Mod",
    DisplayNameText = ModSettingsText.Literal("Test Mod"),
    Adapter = new HttpJsonTelemetryAdapter("https://example.invalid/v1/ingest"), // 或 PostHogTelemetryAdapter(host, projectApiKey)
    Requests =
    [
        TelemetryRequest.BasicUsage(ModSettingsText.Literal("发送版本、平台、语言和匿名安装 ID，用来估算兼容性问题范围。")),
        TelemetryRequest.RunHistory(ModSettingsText.Literal("发送已结束跑局的原版 run-history，用来分析平衡性。"),
            sharedContributionSubscriptions: ["other.mod/challenge_context"],
            captureFilter: evt => !evt.IsAbandoned),
        TelemetryRequest.Diagnostics(ModSettingsText.Literal("发送异常和诊断上下文，用来定位崩溃。")),
        TelemetryRequest.Custom("balance_event", ModSettingsText.Literal("发送本 Mod 的平衡性事件。")),
    ],
});
Client = TelemetryApi.GetClient(ApplicantId);
```

**请求类别**：`BasicUsage`→`basic_usage`、`ModInventory`→`mod_inventory`、`RunHistory`→`run_history`、`Diagnostics`→`diagnostics`、`Custom(id,...)`→自定义 id。

**发送事件**（未注册/未授权/授权被撤销 → 记日志并丢弃，属正常行为）：

```csharp
// payload = 结构化 JSON，适合保存完整上下文
Client.CapturePayload(eventName: "challenge.selected", requestId: "balance_event",
    payload: new JsonObject { ["challenge_id"] = challengeId, ["hard_mode"] = hardMode },
    properties: new Dictionary<string, object?> { ["challenge_id"] = challengeId, ["hard_mode"] = hardMode });
// properties = 扁平字段，适合后端建索引
Client.Capture(eventName: "draft.rerolled", requestId: "balance_event",
    properties: new Dictionary<string, object?> { ["reroll_index"] = rerollIndex });
// 异常：固定走 diagnostics 请求，未授权则为 no-op，不要绕过授权
catch (Exception ex) { Client.CaptureException(ex, new Dictionary<string, object?> { ["tool"] = "challenge_preview" }); throw; }
```

**隐私红线**：不要把本地路径、玩家昵称、账号标识、完整日志文件或未裁剪的大对象塞进 payload。

**自动上传一局数据**：注册 `RunHistory` 后，RitsuLib 在游戏结束时为已授权申请方采集原版 `SerializableRun` JSON；`captureFilter` 控制哪些跑局进队列。手动上传用 `TelemetryApi.CaptureVanillaRunHistory(ModId, runHistory, applicantPayload, properties)`——只接受原版 run-history JSON，别拿自定义对象冒充。

**Contribution（给事件补上下文的插件点）**：

```csharp
public sealed class TestBalanceContribution : ITelemetryContributionProvider
{
    public string ContributorModId => Entry.ModId;
    public string ContributionId => "balance_context";
    public TelemetryDataCategory Category => TelemetryDataCategory.RunHistory;
    public TelemetryContributionVisibility Visibility => TelemetryContributionVisibility.PrivateToApplicant;
    public JsonNode? Build(TelemetryContributionContext context)
        => new JsonObject { ["ruleset"] = TestBalanceState.CurrentRuleset, ["season"] = TestBalanceState.Season, ["event_name"] = context.EventName };
}
TelemetryRegistry.RegisterContributionProvider(new TestBalanceContribution());
```

- 私有 contribution 只附加到自己申请方；共享 contribution 可被别的申请方订阅，但需玩家对来源单独授权。
- 订阅写法：请求里写 `"test/balance_context"` 或 `"test:balance_context"`。共享数据在 envelope 的 `shared_contributions`，私有在 `private_contributions`。

**后端批量格式**：`HttpJsonTelemetryAdapter` 向固定 endpoint POST：

```json
{ "schema": "ritsulib.telemetry.batch.v1", "applicant_id": "test", "events": [] }
```

事件 envelope 含：`schema`、`applicantId`、`eventName`、`requestId`、`category`、`timestampUtc`、`properties`、`payload`。后端建议先校验 schema、applicant_id、事件数量再存原始 JSON。

**PostHog + Cloudflare Worker 免费方案**（PostHog 100 万事件/月免费、数据留一年；不代理则 API key 会暴露在 Mod 包内）：
1. 注册 PostHog，Settings → General → Project token 复制 key。
2. Worker 代理：`npm install -g wrangler` → `wrangler login` → `wrangler init`（Hello World / Worker only / JavaScript）→ 改 `src/index.js`（校验 POST、Content-Type、`MAX_BODY_BYTES` 5MB、`MAX_BATCH_SIZE` 1000、注入 `$ip` GeoIP、转发到 `https://us.i.posthog.com/batch/`，env 读 `POSTHOG_API_KEY`）→ `wrangler deploy` 得到 `https://telemetry-proxy.yourname.workers.dev`。
3. 密钥：`wrangler secret put POSTHOG_API_KEY`（验证：`wrangler secret list`）。
4. 模组端：`new PostHogTelemetryAdapter(host: "https://你的worker.workers.dev", projectApiKey: "proxy")` —— ⚠️ **不要填真实 PostHog key**，Worker 服务端替换。不在乎泄露才直连 `https://us.i.posthog.com` 填真 key。
5. 分析：PostHog 控制台 Apps → Product analytics → New insight，Series 选事件，Breakdown 加 `Country name`，Bar chart。

**注意事项/坑**：`Description`/`DescriptionText` 是玩家看到的授权说明，要直接说明数据类别和用途，别写"改进体验"这类空话；RitsuLib 会自动生成设置页和授权入口。

---

## 8. 模组联动（04-30）

**核心概念**：可选依赖/模组联动。不想在 `.csproj` 硬引用对方 dll，又想在其存在时调用 → 用强类型代理。

**`[ModInterop]`（跨 Mod）**：

```csharp
using STS2RitsuLib.Interop;
// 参数：目标 mod id + 完整类名
[ModInterop("target-mod", "TargetMod.Api.PublicApi")]
public static class TargetModApiInterop
{
    public static bool IsReady => false;                                  // 代理默认值
    public static int GetBonusLevel(string playerId) => 0;
    public static void GrantBadge(string badgeId)
        => throw new NotSupportedException("Target mod is not loaded.");  // 目标不存在时抛
}
```

**代理实例类 + 重命名成员**（本地名与对面不同，或需包装实例类）：

```csharp
[ModInterop("target-mod")]
public static class TargetCatalogInterop
{
    [InteropTarget("TargetMod.Api.Catalog", "FindById")]   // 指定原类名/方法名
    public static EntryRef Find(string id) => throw new NotSupportedException();

    [InteropTarget("TargetMod.Api.Entry")]                 // 包装对端实例类
    public sealed class EntryRef : InteropClassWrapper
    {
        public EntryRef(string id) { }
        public string DisplayName => "";
        public int GetScore() => 0;
    }
}
```

**`[AssemblyInterop]`（调用任意 CLR 程序集，更推荐）**：用法相同，目标类型名带程序集名——`[AssemblyInterop("Target.Lib.Api, TargetLib")]`；同样支持 `[InteropTarget]` + `InteropClassWrapper`（类型名如 `"Target.Lib.Catalog, TargetLib"`）。框架自动区分：类型名含 `,` → AssemblyInterop；不含 → ModInterop，两种可共存。

**调用时机**（把目标不存在当正常分支处理）：

```csharp
// 初始化时先注册程序集（必须）
ModTypeDiscoveryHub.RegisterModAssembly(Entry.ModId, Assembly.GetExecutingAssembly());

if (TargetModApiInterop.IsReady)
{
    var level = TargetModApiInterop.GetBonusLevel("test_player");
    if (level >= 3) TargetModApiInterop.GrantBadge("test:veteran");
    var entry = TargetCatalogInterop.Find("some_id");
}
```

---

## 速查：常用 RitsuLib API 一览

| 功能 | 入口 |
|---|---|
| 日志 | `RitsuLibFramework.CreateLogger(ModId)` |
| 生命周期 | `RitsuLibFramework.SubscribeLifecycle<T>()` / `SubscribeLifecycle(observer)` |
| 补丁 | `RitsuLibFramework.CreatePatcher(ModId, group)` + `PatchAll()` |
| 更新检查 | `RitsuLibFramework.RegisterModUpdateCheck(...)` / `CheckForModUpdateAsync(...)` |
| 热键 | `RuntimeHotkeyService.Register(...)`（`STS2RitsuLib.RuntimeInput`） |
| 内容注册 | `CreateContentPack(ModId)` / `GetContentRegistry` / `GetKeywordRegistry` / `GetCardTagRegistry` |
| 节点附加 | `ModNodeAttachmentRegistry.For(ModId)`（`STS2RitsuLib.Scaffolding.Godot.NodeAttachments`） |
| 遥测 | `TelemetryRegistry.RegisterApplicant` / `TelemetryApi.GetClient` |
| 联动 | `[ModInterop]` / `[AssemblyInterop]`（`STS2RitsuLib.Interop`） |
