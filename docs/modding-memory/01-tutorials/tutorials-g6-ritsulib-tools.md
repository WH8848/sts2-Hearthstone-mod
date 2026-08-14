# RitsuLib 工具类教程精华（G6）

> 来源：tutorials.sts2modding.com 第 04 章 RitsuLib 子系列，作者 alkaid616 / Reme
> 命名空间速查：`STS2RitsuLib` / `STS2RitsuLib.Ui.Toast` / `STS2RitsuLib.Utils` / `STS2RitsuLib.RunData` / `STS2RitsuLib.Combat.Rewards` / `STS2RitsuLib.Combat.CardTargeting` / `STS2RitsuLib.Combat.SecondaryResources` / `STS2RitsuLib.Interactions.RightClick` / `STS2RitsuLib.Combat.Ui.ExtraCornerAmountLabels` / `STS2RitsuLib.Interop.AutoRegistration` / `STS2RitsuLib.Scaffolding.Content`

---

## 1. 通知提示（Toast）
来源文件：`docs_04-ritsulib_04-22-notification.txt`

- **核心 API**：`RitsuToastService.ShowInfo / ShowWarning / ShowError`
- Toast 在 `GameReadyEvent` 之后由框架挂到游戏根节点，**须在局内/UI 就绪后再调用**（用 `RitsuLibFramework.SubscribeLifecycle<GameReadyEvent>` 订阅）
- 参数：正文必填；标题可选；`onClick` 点击回调可选（Error 示例）

```csharp
[ModInitializer(nameof(Init))]
public static class Entry {
    public const string ModId = "Test";
    public static readonly Logger Logger = RitsuLibFramework.CreateLogger(ModId);

    public static void Init() {
        RitsuLibFramework.SubscribeLifecycle<GameReadyEvent>(_ => {
            RitsuToastService.ShowInfo("Mod 已加载");                                   // 普通提示
            RitsuToastService.ShowWarning("生命值过低", "警告");                          // 警告，带标题
            RitsuToastService.ShowError("保存失败。", onClick: () => Logger.Info("用户点击了 Toast"));
        });
    }
}
```

- **完全自定义**：构造 `RitsuToastRequest` 交给 `RitsuToastService.Show(...)`：

```csharp
RitsuToastService.Show(new RitsuToastRequest(
    body: "新配方已解锁！",                              // 正文，必填
    title: "配方",                                       // 标题，可空
    image: myTexture,                                    // 左侧图片，可空
    level: RitsuToastLevel.Info,                         // Info / Warning / Error
    durationSeconds: 5.0,                                // null 用默认 3.5 秒
    onClick: () => Logger.Info("打开配方界面"),
    animationOverride: RitsuToastAnimationPreset.FadeScale));  // Fade / FadeSlide(全局默认) / FadeScale
```

---

## 2. 额外角标（能力/遗物/意图）
来源文件：`docs_04-ritsulib_04-22-1-extra-badge.txt`

- 用途：在图标角落显示额外数字/数量（如能力双数字）
- 接口对应表：

| 目标 | 接口 |
|---|---|
| 能力 | `IPowerExtraIconAmountLabelSpecsProvider` |
| 遗物 | `IRelicExtraIconAmountLabelSpecsProvider` |
| 怪物意图 | `IIntentExtraCornerAmountLabelsProvider`（在 `AbstractIntent` 子类上） |

- 角标规格：`ExtraIconAmountLabelSpec.Plain(corner, text)` 纯文本，或 `.RichText(corner, text)` 支持 bbcode 富文本
- 角落枚举 `ExtraIconAmountLabelCorner`：`TopLeft / TopRight / BottomLeft / BottomRight / Custom`（Custom 需自供 `Rect2`；**BottomRight 原版常用于主计数，谨慎占用**）

```csharp
[RegisterPower]
public sealed class TestMeterPower : ModPowerTemplate, IPowerExtraIconAmountLabelSpecsProvider {
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://Test/images/powers/test_meter.png",
        BigIconPath: "res://Test/images/powers/test_meter.png");

    public IReadOnlyList<ExtraIconAmountLabelSpec> GetPowerExtraIconAmountLabelSpecs() => [
        ExtraIconAmountLabelSpec.Plain(ExtraIconAmountLabelCorner.TopLeft, Amount.ToString()),
        ExtraIconAmountLabelSpec.RichText(ExtraIconAmountLabelCorner.BottomLeft, "[color=gold]x2[/color]"),
    ];
}
```

- **刷新机制**：
  - 角标刷新通常跟随原版 `DisplayAmountChanged`；遗物内部状态变化时调 `InvokeDisplayAmountChanged()`
  - 若角标不依赖 `DisplayAmount`：实现 `IRelicExtraIconAmountLabelsChangeSource`，状态变化时触发 `RelicExtraIconAmountLabelsInvalidated`
  - 意图角标：随战斗 UI 刷新重新读取；外部状态变化需实现 `IIntentExtraCornerAmountLabelsChangeSource` 并触发 `IntentExtraCornerAmountLabelsInvalidated`
  - 意图角标用 `ExtraIconAmountLabelSlot.At(corner, text)` 返回列表

---

## 3. 常用工具
来源文件：`docs_04-ritsulib_04-22-2-common-tools.txt`

### 3.1 DynamicEnumValueRegistry（动态枚举扩展）
- 统一管理、安全地为已知枚举添加分支；**只加枚举，匹配逻辑和素材需自己做**

```csharp
static CardType Field;   // 之后使用这个值
// 初始化里注册：
var enumRegistry = DynamicEnumValueRegistry<CardType>.For(ModId);
Field = enumRegistry.RegisterOwned("FIELD").Value;
```

### 3.2 WeightedList\<T\>（带权重抽取）
- 可用原版 `Rng` 抽取，支持不放回；适合奖励卡牌池、选项抽取
- 元素实现 `IWeightedValue` 时 `Add(item)` 自动读 `Weight`，否则默认权重 1
- **坑**：权重必须 > 0；空列表 `GetRandom` 抛异常 → 不确定时用 `TryGetRandom`

```csharp
public readonly record struct RewardChoice(string Id, int Weight) : IWeightedValue;

var choices = new WeightedList<RewardChoice> {
    new("gold", 5), new("card", 10), new("rare_relic", 1),
};
return choices.GetRandom(rng).Id;                    // 可放回
choices.GetRandom(rng, remove: true);                // 不放回（抽卡池）
```

### 3.3 AttachedState（附加状态，不改继承）
- `AttachedState<TKey,TValue>` 用 `ConditionalWeakTable` 把数据挂到任意引用对象上；**不阻止 key 被 GC**
- 用 `TryGetValue` 做只读判断（不创建默认值）；索引器 / `GetOrCreate` 会创建默认状态

```csharp
private static readonly AttachedState<Creature, int> Heat = new(() => 0);
var heat = Heat[creature];   // 取值
Heat[creature] = 5;          // 设值
```

### 3.4 SavedAttachedState（附加状态 + 原版存档）
- 目标对象参与原版 `SavedProperties` 序列化时，用 `SavedAttachedState<TKey,TValue>` 把状态写进原版保存属性
- **只支持**：`int`、`bool`、`string`、`ModelId`、枚举、`int[]`、枚举数组、`SerializableCard`、`SerializableCard[]`、`List<SerializableCard>`

```csharp
private static readonly SavedAttachedState<AbstractModel, bool> IsEchoCopy =
    new("test_echo_copy", defaultValueFactory: () => false);

public static void MarkEchoCopy(AbstractModel model) => IsEchoCopy[model] = true;
public static bool IsMarked(AbstractModel model) => IsEchoCopy.GetValueOrDefault(model, false);
```

### 3.5 DynamicEnumValueMinter（稳定扩展枚举高位）
- 只支持底层为 32 位的 enum；**确保 ID 不撞车**（用带 mod 前缀的字符串）

```csharp
private static readonly DynamicEnumValueMinter<CardTag> Tags = new();
public static readonly CardTag EchoCard = Tags.Mint("test:echo_card");
public static bool IsOurDynamicTag(CardTag tag) => Tags.IsDynamic(tag);
```

### 3.6 MaterialUtils（着色器材质工厂）
| 方法 | 用途 |
|---|---|
| `CreateReplaceHueShaderMaterial(rgb, brightness)` | 替换色调（保留原亮度/饱和度），适合改原版卡框 |
| `CreateRgbShaderMaterial(...)` | **已过时**，改用上面的 |
| `CreateHsvShaderMaterial(h, s, v)` | 原版 HSV 着色器 |
| `CreateUnmodulatedHsvShaderMaterial()` | 保留原色（h=0, s=1, v=1），用于自定义卡框 |
| `CreateDoomBarShaderMaterial()` | 原版灾厄血条材质（自带正确 `NoiseTexture`） |
| `CreateVanillaDoomBarGradientTexture()` / `CreateVanillaDoomBarNoiseTexture()` | 灾厄渐变纹理 / 匹配的 `NoiseTexture2D` |

### 3.7 HoverTipHelper（悬浮提示扩展）
- 在已有悬浮提示组上追加文字或卡牌预览

```csharp
HoverTipHelper.AddTipToOwner(owner, "Test", "这是一条额外说明。");
HoverTipHelper.AddCardTipsToOwner(owner, cards);
```

- 返回 `false` 表示当前没有绑定活动悬浮提示组，通常可忽略；自定义控件需先按原版方式创建并绑定悬浮提示组

---

## 4. 局内数据（RunSavedData）
来源文件：`docs_04-ritsulib_04-22-3-in-run-data.txt`

- 适用：**一局游戏的全局配置**。战斗内单场数据用 `SavedAttachedState` / `SavedProperty` 更合适
- 两种作用域：
  - `RunSavedData<T>`：全局共享（难度、总击杀精英数）
  - `PlayerRunSavedData<T>`：按玩家独立（联机时各人选的卡牌包）
- 自带**大厅暂存**与**多人联机同步**支持

### 4.1 注册槽位
```csharp
[ModInitializer(nameof(Init))]
public static class Entry {
    public const string ModId = "test";
    public static RunSavedData<ChallengeRunState> Challenge = null!;
    public static PlayerRunSavedData<PlayerRunState> Player = null!;

    public static void Init() {
        using (RitsuLibFramework.BeginModDataRegistration(ModId)) {
            var store = RitsuLibFramework.GetRunSavedDataStore(ModId);
            Challenge = store.Register(key: "challenge",
                defaultFactory: () => new ChallengeRunState(),
                options: new RunSavedDataOptions {
                    WritePolicy = RunSavedDataWritePolicy.WhenNonDefault,
                    SyncLobbyOnChange = true,   // 大厅修改时同步给队友
                });
            Player = store.RegisterPerPlayer(key: "player",
                defaultFactory: () => new PlayerRunState(),
                options: new RunSavedDataOptions {
                    WritePolicy = RunSavedDataWritePolicy.WhenSet,
                    SyncLobbyOnChange = true,
                });
        }
    }
}
```

- **坑**：`key` 是存档中识别数据的唯一标识。发布后**绝对不要修改已注册的 key**，否则老玩家该部分存档丢失；加新内容直接给 C# 类加属性即可
- 数据类可以是普通 sealed class（属性 get/set 即可）

### 4.2 读取/修改（需传 `RunState` 或 `Player`）
```csharp
// 全局：Get / Modify
var challengeData = TestRunData.Challenge.Get(runState);
TestRunData.Challenge.Modify(runState, data => data.ElitesKilled += 1);

// 玩家独立：传 Player 实例，或 RunState + netId
var playerData = TestRunData.Player.Get(player);
TestRunData.Player.Modify(player, data => data.DraftRerolls -= 1);
TestRunData.Player.Modify(runState, teammateNetId, data => data.LoadoutId = "shared_loadout");
```

- **联机注意**：共享槽位只接受主机 net id 的贡献；客户端提交自己的选择应写 `PlayerRunSavedData<T>`，用本机 `lobby.NetService.NetId` 作玩家 key；主机开始跑局时提交权威快照

### 4.3 大厅暂存（Lobby Scope）
- 开局前 `RunState` 未建立，不能 `Get(runState)`；用 `Lobby.Modify` 写暂存区。只要注册时 `SyncLobbyOnChange = true`，改动自动同步给主机和队友

```csharp
public static void SelectChallenge(StartRunLobby lobby, string challengeId, bool hardMode) {
    TestRunData.Challenge.Lobby.Modify(lobby, data => {
        data.ChallengeId = challengeId; data.HardMode = hardMode;
    });
}
public static void SelectLocalLoadout(StartRunLobby lobby, string loadoutId) {
    TestRunData.Player.Lobby.Modify(lobby, lobby.NetService.NetId, data => data.LoadoutId = loadoutId);
}
```

### 4.4 监听提交时机
```csharp
// 驱动大厅 UI 预览：
RitsuLibFramework.SubscribeLifecycle<RunSavedDataLobbyStagingEvent>(evt => {
    if (evt.IsHost && evt.Reason == RunSavedDataLobbyStagingReason.ContributionMerged)
        Entry.Logger.Info("大厅跑局数据已合并，可以刷新预览。");
});
// run snapshot 导出前补齐最终值：
RitsuLibFramework.SubscribeLifecycle<RunSavedDataPreparingEvent>(evt => {
    TestRunData.Challenge.Modify(evt.RunState, data => data.ChallengeId ??= "standard");
});
```

- `RunSavedDataLobbyStagingReason`：`ContributionMerged`（主机合并贡献）、`PlayerJoined`（新玩家进大厅补槽）、`Manual`（手动调 `RunSavedDataLobby.NotifyStagingChanged(lobby)`）、`Committing`（主机即将构建开局快照）

### 4.5 写入策略
| 策略 | 说明 |
|---|---|
| `WhenSet` | **默认**。只有 `Set`/`Modify` 显式改过的值才写入 |
| `WhenNonDefault` | 对象可反复读取，仅与默认值不同才进存档。适合挑战开关、计数器 |
| `AlwaysWhenRegistered` | 槽位能解析就写入。适合每局必须带 schema 的控制数据 |

- **坑**：`WhenNonDefault` 会把当前值和 `defaultFactory` 新对象序列化后比较 → 默认工厂必须稳定，**不要放随机数、时间戳、运行时对象引用**

### 4.6 结构迁移
- `RunSavedDataOptions.SchemaVersion` 写进槽位；旧版本读入时按 `IMigration.FromVersion` 逐级升级到当前版本

```csharp
public sealed class ChallengeV1ToV2Migration : IMigration {
    public int FromVersion => 1;
    public int ToVersion => 2;
    public bool Migrate(JsonObject data) {
        if (data["data"] is not JsonObject payload) return false;
        payload["hardMode"] ??= false;
        return true;
    }
}
// 注册时挂上：
options: new RunSavedDataOptions {
    SchemaVersion = 2,
    WritePolicy = RunSavedDataWritePolicy.WhenNonDefault,
    SyncLobbyOnChange = true,
    Migrations = new[] { new ChallengeV1ToV2Migration() },
}
```

---

## 5. 自定义奖励
来源文件：`docs_04-ritsulib_04-22-4-custom-reward.txt`

### 5.1 注册奖励类型（Entry.Init 中）
```csharp
public static RewardType TokenRewardType;

TokenRewardType = ModRewardRegistry.For(ModId)
    .RegisterOwned("token",                                  // 奖励ID → 生成 MYMOD_REWARD_TOKEN
        (save, player, json) => new MyTokenReward(player))   // 读档重建工厂
    .RewardType;
```

### 5.2 奖励类（继承 `ModCustomReward`）
- **必须**：构造函数 `(Player player) : base(player)`；`ModRewardType`；`MarkContentAsSeen()`；`OnSelect()`（`Task<bool>`）
- **可选**：`RewardIconPath`（null 则空白容器）、`DescriptionLocTable`（默认 `gameplay_ui`）、`DescriptionLocKey`（默认用注册 ID）
- `OnSelect` 返回 `true` 领取成功（UI 消除选项），`false` 取消（按钮保留）

```csharp
public class MyTokenReward : ModCustomReward {
    public MyTokenReward(Player player) : base(player) { }
    public override RewardType ModRewardType => Entry.TokenRewardType;
    protected override string? RewardIconPath => "res://MyMod/images/rewards/token.png";
    public override void MarkContentAsSeen() { }
    protected override async Task<bool> OnSelect() {
        await PlayerCmd.GainGold(25, Player);   // 例如给 25 金币
        return true;
    }
}
```

- 本地化：`{modId}/localization/{lang}/gameplay_ui.json` → `{ "MYMOD_REWARD_TOKEN": "获得 25 金币" }`

### 5.3 发放给玩家
```csharp
// 卡牌效果里（找 CombatState 即可，如 Owner.CombatState）：
if (CombatState.RunState.CurrentRoom is CombatRoom combatRoom)
    combatRoom.AddExtraReward(player, new MyTokenReward(player));

// 原版遗物 OnGetRewards（或类似回调）里直接：
rewards.Add(new MyTokenReward(Owner));
```

### 5.4 带 Payload 的存档奖励
- 奖励含**动态状态**（随机金币数、随机卡牌 ID）时必须存档，否则结算界面 ESC 退出再读档会刷新/丢失
- 步骤：
  1. 定义 Payload 与 JSON 上下文：
  ```csharp
  public readonly record struct TokenPayload(int TokenCount);
  [JsonSerializable(typeof(TokenPayload))]
  internal sealed partial class MyJsonContext : JsonSerializerContext;
  ```
  2. 注册时传入 JSON 协定与带 Payload 的工厂：
  ```csharp
  TokenRewardType = ModRewardRegistry.For(ModId)
      .RegisterOwned<TokenPayload>("token", MyJsonContext.Default.TokenPayload,
          (save, player, payload) => new MyTokenReward(player, payload?.TokenCount ?? 0))  // payload 为 null = 旧档无数据
      .RewardType;
  ```
  3. 奖励类里序列化：
  ```csharp
  public override string? ToModRewardJson() =>
      System.Text.Json.JsonSerializer.Serialize(new TokenPayload(_tokenCount), MyJsonContext.Default.TokenPayload);
  ```
- 也可用 `ToSerializable<TPayload>(payload, jsonTypeInfo)` 重载（重写里返回 `base.ToSerializable<TPayload>(...)`），省掉手写 `ToModRewardJson`，二选一
- **坑**：Payload 只能放 JSON 可序列化数据（int/string/record struct 组合），**不要塞 Godot 节点或图片**

### 5.5 联机同步规则
- “选了哪个奖励”由原版自动网络同步；但 `OnSelect()` 会在**所有客户端各执行一次**
- **坑**：`OnSelect()` 里不能有随机数检定或本地独有资源，否则客户端结果不一致 → 断连/状态分裂
- 解法：严格确定（用 `RunState.Rng` 等共享随机序列），或走原版网络指令（`PlayerCmd.GainGold`、`PlayerCmd.GainRelic` 等）

---

## 6. 自定义目标
来源文件：`docs_04-ritsulib_04-22-5-custom-targeting.txt`

### 6.1 预置目标类型（无需注册，直接填 `TargetType`）
- `CustomTargetType.Anyone`：单体，任意存活友方/敌方
- `CustomTargetType.Everyone`：群体，场上所有存活生物
- `AnyAttackingEnemy` / `AllAttackingEnemies`：单体/群体，限“当前拥有攻击意图的存活敌人”
- `AnyBlockingEnemy` / `AllBlockingEnemies`：单体/群体，限“当前护甲 > 0 的存活敌人”
- `AllHighestHpEnemies` / `AllLowestHpEnemies`：群体，血量并列最高/最低的所有存活敌人

### 6.2 注册自定义目标类型（启动阶段调用一次）
```csharp
public static TargetType WoundedEnemy { get; private set; }
public static TargetType AllWoundedEnemies { get; private set; }

public static void Register() {
    WoundedEnemy = CustomTargetType.RegisterSingleTargetType(Entry.ModId, "WOUNDED_ENEMY",
        creature => creature is { IsMonster: true, IsAlive: true } && creature.CurrentHp < creature.MaxHp);
    AllWoundedEnemies = CustomTargetType.RegisterMultiTargetType(Entry.ModId, "ALL_WOUNDED_ENEMIES",
        creature => creature is { IsMonster: true, IsAlive: true } && creature.CurrentHp < creature.MaxHp);
}
```

- **坑**：注册标识字符串（如 `"WOUNDED_ENEMY"`）Mod 内必须唯一，**发布后绝对不要改**——底层按它计算确定性枚举数字写入存档，改名会导致旧存档读档匹配不到目标类型

### 6.3 结算目标（核心扩展方法）
- `CardModelTargetingExtensions.GetTargets()`（用法 `this.GetTargets(cardPlay.Target)`）自动校验：单体→判断 `cardPlay.Target` 是否合法并转列表；群体→按注册规则找出全场亮圈的目标

```csharp
public sealed class StrikeWounded() : ModCardTemplate(1, CardType.Attack, CardRarity.Common, TestTargets.WoundedEnemy) {
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) {
        foreach (var target in this.GetTargets(cardPlay.Target)) {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this).Targeting(target).Execute(choiceContext);
        }
    }
}
```

---

## 7. 右键交互
来源文件：`docs_04-ritsulib_04-22-6-right-click.txt`

- 支持卡牌/遗物/能力/药水右键；自动处理多人同步、手柄兼容、优先级调度
- **前提**：`Entry.Init()` 中已调用 `ModTypeDiscoveryHub.RegisterModAssembly(...)`，否则自动注册不生效

### 7.1 方式一：模型实现接口
| 模型 | 接口 |
|---|---|
| 卡牌 | `IModRightClickableCard` |
| 遗物 | `IModRightClickableRelic` |
| 能力 | `IModRightClickablePower` |
| 药水 | `IModRightClickablePotion` |

```csharp
[RegisterPower]
public sealed class TestInfoPower : ModPowerTemplate, IModRightClickablePower {
    // ... AssetProfile 等常规定义 ...
    public bool CanHandleRightClickLocal(ModRightClickContext context) => Amount > 0;  // 可选，默认 true
    public async Task OnRightClick(ModRightClickExecutionContext context) {            // 多人下所有客户端同步执行
        RitsuToastService.ShowInfo($"当前层数：{Amount}");
    }
}
```

### 7.2 方式二：注册绑定（不改模型类）
```csharp
private static IDisposable? _examineBinding;
_examineBinding = ModRightClickRegistry.Register<CardModel>(
    ModId, "examine",                                    // ID 防撞
    canHandle: ctx => ctx.Model is CardModel card && card.Tags.Contains(CardTag.Strike),
    execute: async ctx => { /* 执行（多人同步） */ },
    priority: 0);                                        // 越高越先触发
// 取消：_examineBinding?.Dispose();
```

- 同一模型可挂多个绑定，按优先级排序依次执行；`canHandle` 返回 false 则跳过该绑定

### 7.3 方式三：注册接口（自定义类/全局处理）
```csharp
public sealed class TestGlobalHandler : IModRightClickHandler {
    public int Priority => 100;                           // 默认 0，越高越先
    public bool TryHandle(ModRightClickContext context) {
        if (context.Model is RelicModel relic) {
            RitsuToastService.ShowInfo($"遗物：{relic.DisplayName}");
            return true;                                  // 消费事件，不再往后传
        }
        return false;                                     // 不处理，交给下一个
    }
}
// Entry.Init 中：ModRightClickRegistry.Register(new TestGlobalHandler());
```

- 处理器运行在**模型绑定之前**，按 `Priority` 降序执行；返回 true 即消费事件，不再走模型绑定流程

### 7.4 上下文
```csharp
public readonly record struct ModRightClickContext(Player Player, AbstractModel Model, ModRightClickTrigger Trigger);
```
- `Player`：发起右键的本地玩家（经 `LocalContext.GetMe(...)` 解析）
- `Model`：被右键的模型，运行时可能是 `CardModel / RelicModel / PowerModel / PotionModel`（都继承 `AbstractModel`）
- `Trigger`：`IsController`（是否控制器触发）+ `Metadata`（预留自定义数据）
- 同步执行阶段用 `ModRightClickExecutionContext`，多 `PlayerChoiceContext` 和 `Action` 两个字段

---

## 8. 次要资源（SecondaryResource，测试阶段）
来源文件：`docs_04-ritsulib_04-22-7-secondary-resources.txt`

- 类似“星辉”的第二套战斗资源系统，用于额外费用/资源管理的卡牌、遗物、能力

### 8.1 注册资源定义
```csharp
var registry = RitsuLibFramework.GetSecondaryResourceRegistry(Entry.ModId);
ManaDefinition = registry.Register("mana", new SecondaryResourceDefinition(
    defaultAmount: 0,
    baseMaxAmount: 3,
    turnStartPolicy: SecondaryResourceTurnStartPolicy.AddMaxToCurrent,
    persistencePolicy: SecondaryResourcePersistencePolicy.Run,
    smallIconPath: "res://Test/images/resources/mana_small.png",
    largeIconPath: "res://Test/images/resources/mana_large.png"));
ManaId = ManaDefinition.Id;
```

- 返回 ID 格式：`{MODID}_SECONDARY_RESOURCE_{LOCALID}`（如 `TEST_SECONDARY_RESOURCE_MANA`）
- `baseMaxAmount: null` = 无上限
- `turnStartPolicy`：`None`（不动）/ `ResetToMax`（回满）/ `AddMaxToCurrent`（上限加到当前，如蓄能）/ `Clear`（清零）
- `persistencePolicy`：`None`（仅运行时）/ `Combat`（战斗内恢复）/ `Run`（跨战斗持久化）

### 8.2 修改资源（`SecondaryResourceCmd` 静态类）
```csharp
int currentMana = SecondaryResourceCmd.Get(player, ManaId);
int? maxMana = SecondaryResourceCmd.GetMax(player, ManaId);          // 无上限返回 null
await SecondaryResourceCmd.Gain(player, ManaId, 2);                   // 会经过 Gain Hook 修正
await SecondaryResourceCmd.Lose(player, ManaId, 1);
await SecondaryResourceCmd.Set(player, ManaId, 5);
bool success = await SecondaryResourceCmd.Spend(player, ManaId, 3);   // 经 Spend Hook，不足不扣且返回 false
await SecondaryResourceCmd.Reset(player, ManaId, toMax: true);        // 重置；toMax: true 重置到上限
```

### 8.3 卡牌次级资源费用
```csharp
// 构造函数中：
this.SecondaryCosts().Set(ModResources.ManaId, 2);                   // 固定费用 2 法力
this.SecondaryCosts().Set(ModResources.ManaId, SecondaryResourceCost.X());   // 消耗所有法力
this.SecondaryCosts().Set(ModResources.ManaId, SecondaryResourceCost.X(2));  // X 数值 ×2
// 临时费用（duration）：
this.SecondaryCosts().Set(ManaId, 1, SecondaryResourceCostDuration.ThisTurn);     // 仅本回合
this.SecondaryCosts().Set(RageId, 2, SecondaryResourceCostDuration.ThisCombat);   // 仅本场战斗
this.SecondaryCosts().Set(ManaId, 1, SecondaryResourceCostDuration.UntilPlayed);  // 打出后清除
this.SecondaryCosts().Set(ManaId, SecondaryResourceCost.Free, SecondaryResourceCostDuration.UntilPlayed); // 免费打一次
this.SecondaryCosts().Clear(ManaId);                                 // 完全移除该资源费用
// 手动清理（框架自动调用，一般不用写）：
card.ClearSecondaryCostsThisTurn();
card.ClearSecondaryCostsUntilPlayed();
```

- **X 费用取值**（`OnPlay` 中）：
```csharp
int effectValue = cardPlay.SecondaryResources().Value(ModResources.ManaId);  // 当前持有量 × XMultiplier，经 Hook 修正
bool wasX = cardPlay.SecondaryResources().CostsX(ModResources.ManaId);
int spent = cardPlay.SecondaryResources().Spent(ModResources.ManaId);        // 实际消耗量
```

### 8.4 Hook 系统（`ISecondaryResourceHookListener`）
- 遗物/能力/角色实现该接口；所有钩子有默认实现，只需重写需要的；用 `context.Definition.Id` 判断资源
```csharp
[RegisterRelic(typeof(SharedRelicPool))]
public class ManaRelic : ModRelicTemplate, ISecondaryResourceHookListener {
    public decimal ModifyMaxSecondaryResource(SecondaryResourceMaxContext context, decimal amount) =>
        context.Definition.Id == ModResources.ManaId ? amount + 2 : amount;   // 上限 +2
    public decimal ModifySecondaryResourceGain(SecondaryResourceContext context, decimal amount) =>
        context.Definition.Id == ModResources.ManaId ? amount + 1 : amount;   // 获得 +1
    public async Task AfterSecondaryResourceChanged(SecondaryResourceChangeContext context) {
        if (context.Definition.Id != ModResources.ManaId || context.NewAmount > 0) return;
        await context.Player.LoseHp(2, context.Player);                       // 归零时扣血
    }
}
```
- 全部钩子：`ModifySecondaryResourceGain`（修正获得量）、`ModifyMaxSecondaryResource`（修正上限）、`ModifySecondaryResourceCost`（修正固定费用，不含 X）、`ModifySecondaryResourceXValue`（修正 X 值）、`ShouldGainSecondaryResource` / `ShouldSpendSecondaryResource` / `ShouldResetSecondaryResource`（返回 false 阻止）、`AfterSecondaryResourceChanged` / `AfterSecondaryResourceSpent` / `AfterSecondaryResourceReset`（变化后回调）

### 8.5 战斗 UI（`RegisterCombatUi` / `RegisterCardUi`）
- 内置组件：`NSecondaryResourceCounter`（能量盘）、`NSecondaryResourceCardCostUi`（卡牌费用展示）；也可自建并绑定数值和玩家
```csharp
registry.RegisterCombatUi("mana_combat_counter", parent => {
    var row = NSecondaryResourceCounter.Create(ManaDefinition, new SecondaryResourceCounterStyle {
        FontSize = 32,
        PositiveColor = Colors.Cyan,
        FormatAmount = (amount, max) => amount.ToString(),
        IconStyle = SecondaryResourceIconStyle.Default with { Size = new Vector2(80, 80), HoverTip = SecondaryResourceHoverTipStyle.Default },
    });
    var energyCounter = parent.GetNode<Control>("%EnergyCounterContainer");   // 定位到能量计数器旁
    row.Position = energyCounter.Position + new Vector2(120, -120);
    return row;
}, ctx => ctx.Node.Bind(ctx.Player));

registry.RegisterCardUi("mana_card_ui", parent => {
    var ui = NSecondaryResourceCardCostUi.Create(ManaId, new SecondaryResourceCardCostUiStyle {
        IconSize = new Vector2(48, 48), FontSize = 24,
    });
    var energyIcon = parent.GetNode<TextureRect>("%EnergyIcon");             // 定位到能量图标旁
    ui.Position = energyIcon.Position + new Vector2(0, 80);
    return ui;
}, ctx => ctx.Node.Refresh(ctx));

registry.AlwaysShowInCombatUi(ManaDefinition.LocalId);                        // 永远显示
// registry.AlwaysShowInCombatUiForCharacter<Ironclad>(ManaDefinition.LocalId);  // 仅特定角色显示
```
- `RegisterCombatUi` 基于 `NodeAttachment` 系统自动挂载（详见“节点附加”教程）；style 可自由配置

### 8.6 本地化
- 悬浮提示默认读 `static_hover_tips` 表；`SecondaryResourceDefinition` 的 `locTable` 参数可指定自定义表
- 未提供 `titleKey`/`descriptionKey` 时按 `{resourceId}.title` / `{resourceId}.description` 自动推导 key：
```json
{
  "TEST_SECONDARY_RESOURCE_MANA.title": "法力",
  "TEST_SECONDARY_RESOURCE_MANA.description": "每回合开始时获得数值。跨战斗保留。",
  "TEST_SECONDARY_RESOURCE_RAGE.title": "怒气",
  "TEST_SECONDARY_RESOURCE_RAGE.description": "每回合开始时清零。打出攻击牌可获得怒气。"
}
```

### 8.7 卡牌文本中显示图标
```csharp
protected override IEnumerable<DynamicVar> CanonicalVars => [
    SecondaryResourceVars.For("Mana", ModResources.ManaId, 2)
];
// 本地化文本： "消耗 {Mana:secondaryResourceIcons()} 点法力。"   （或用 {Mana} 显示数字）
```

---

## 通用要点速记
- 多个注册体系（奖励/目标/资源/右键）都要求**启动阶段注册一次**，返回值存静态字段供全局引用
- 凡是**写进存档的标识符**（RunSavedData key、目标类型字符串、奖励 ID、右键绑定 ID）发布后一律不可修改
- 联机环境下 `OnSelect`、`OnRightClick` 等会在所有客户端执行，逻辑必须确定性或走原版网络指令
