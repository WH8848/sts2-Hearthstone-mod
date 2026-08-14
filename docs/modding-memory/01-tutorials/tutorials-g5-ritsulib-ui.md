# STS2 Modding 教程精华：RitsuLib UI 与状态（G5）

> 来源：tutorials.sts2modding.com，作者 Reme / alkaid616
> 涉及文件：
> - `docs_04-ritsulib_04-16-health-bar-overlay.txt`（血条覆盖）
> - `docs_04-ritsulib_04-17-data-save.txt`（数据保存）
> - `docs_04-ritsulib_04-18-add-capability.txt`（添加组件 ModelCapability）
> - `docs_04-ritsulib_04-18-max-hand-size.txt`（手牌上限）
> - `docs_04-ritsulib_04-19-hand-outline.txt`（手牌泛光）
> - `docs_04-ritsulib_04-20-custom-card-pile.txt`（自定义卡牌堆）
> - `docs_04-ritsulib_04-21-top-bar-button.txt`（顶栏按钮）

---

## 1. 血条覆盖（Health Bar Overlay）

**核心概念**：`IHealthBarForecastSource` 接口 + `GetHealthBarForecastSegments` 重写；可做类似中毒、灾厄的血条覆盖层。

- 命名空间：`STS2RitsuLib.Combat.HealthBars`
- 在能力类（继承 `ModPowerTemplate`）上实现接口：

```csharp
[RegisterPower]
public class TestPower2 : ModPowerTemplate, IHealthBarForecastSource
{
    // 常规 Power 实现（Type/StackType/AssetProfile/CanonicalVars/ModifyDamageMultiplicative）...

    public IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(HealthBarForecastContext context)
    {
        return HealthBarForecasts.Single(
            context.Creature.GetPowerAmount<TestPower2>(),   // 展示的数量（倍率效果可乘 2）
            new Color(0.4f, 0.1f, 0.1f),                    // 颜色
            HealthBarForecastGrowthDirection.FromLeft,      // 从左边还是右边延伸
            // 0,  // （可选）顺序：越大越远离血条边缘，默认 0
            // PreloadManager.Cache.GetMaterial("res://xxx.tres") // （可选）自定义材质
        );
    }
}
```

**注意事项/坑**：
- `GetPowerAmount<T>()` 取能力层数；演示里的能力 `ModifyDamageMultiplicative` 里用 `props.IsPoweredAttack()` 判断是否为攻击、`Owner.CurrentHp > Amount` 判断阈值。
- 配套文本写在 `powers.json`，键 `TEST_POWER_TEST_POWER2.title/description/smartDescription`，可用 `{Amount}`、`{Weakness:percentMore()}` 动态变量。

---

## 2. 数据保存（Data Save）

### 2.1 挂载对象的局内保存：`SavedAttachedState<TOwner, TValue>`

给卡牌、遗物等任意对象附加**随存档保存**的状态，免写序列化逻辑。

```csharp
[RegisterRelic(typeof(SharedRelicPool))]
public class TestRelic : ModRelicTemplate
{
    // 静态字段：状态名 + 默认值构造（读档无此值时用 0）
    public static readonly SavedAttachedState<TestRelic, int> GameTurns = new("GameTurns", _ => 0);

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new CardsVar(1),
        new DynamicVar("GameTurns", GameTurns[this]),   // 要显示在描述里必须加 DynamicVar
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        GameTurns[this]++;                              // 用 GameTurns[this] 读写当前实例状态
        DynamicVars["GameTurns"].BaseValue = GameTurns[this];
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, player);
    }
}
```

**坑**：状态名（第一个参数）在同一对象类型内不可重复。本地化描述里用 `{GameTurns}` 引用。

### 2.2 全局局内保存：`RunSavedData`

- 用于一局游戏的全局数据，联机模式下每个玩家单独一份。详见 RitsuLib 文档「02 - 玩法基底 / 09 - 局内数据」。
- **战斗内数据用 `SavedAttachedState` / `SavedProperty` 更合适**；`RunSavedData` 面向整局配置。

### 2.3 全游戏持久化：`ModDataStore`

适合解锁进度、击杀统计、Mod 设置面板参数。自动读写文件、按存档槽隔离或全局通用。

```csharp
// ① 数据载体：普通 sealed 类
public sealed class ModProgressData
{
    public int GlobalMonstersKilled { get; set; } = 0;
    public bool HasUnlockedSecret { get; set; } = false;
}

// ② 初始化阶段注册
using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId))
{
    var store = RitsuLibFramework.GetDataStore(Entry.ModId);
    store.Register<ModProgressData>(
        key: "mod_progress",              // ID 键值：一旦确定不要轻易改动
        fileName: "test_mod_progress.json",
        scope: SaveScope.Global,          // Global=全存档通用 / Profile=当前存档槽位独立
        defaultFactory: () => new ModProgressData(),
        autoCreateIfMissing: true
    );
}

// ③ 读取
var progress = RitsuLibFramework.GetDataStore(Entry.ModId).Get<ModProgressData>("mod_progress");

// ④ 修改（Modify 闭包保证线程一致性）+ 必须显式保存
store.Modify<ModProgressData>("mod_progress", data => { data.GlobalMonstersKilled += 1; });
store.Save("mod_progress");               // ⚠️ 不调用 .Save() 不会落盘，可多次修改后一次保存

// ⑤ 查询是否存在
if (!store.HasExistingData("mod_progress")) { /* 首次运行初始化逻辑 */ }
```

**作用域选择**：
- `SaveScope.Global`：所有存档槽互通——Mod 设置、快捷键、通用成就。
- `SaveScope.Profile`：当前存档位独立——角色经验进度、某存档专属解锁状态。

---

## 3. 添加组件（ModelCapability）

**核心概念**：RitsuLib 的通用附加行为系统，可挂到任意 `AbstractModel`（卡牌、遗物、药水、能力、怪物、角色）。类似塔 1 的 cardmodifier / 塔 2 的附魔，但可挂多个且不限卡牌。

> 前提：`Entry.Init()` 里已调用 `ModTypeDiscoveryHub.RegisterModAssembly(...)`，否则 `[RegisterModelCapability]` 自动注册不生效。

### 3.1 定义与挂载

```csharp
[RegisterModelCapability]   // 自动注册，组件 id = {MODID}_MODEL_CAPABILITY_{类名SNAKE_CASE}
public class DrawPowerCapability : CardCapability
{
    protected override void OnAttach(CardModel model)  { /* 挂载回调 */ }
    protected override void OnDetach(CardModel model)  { /* 卸载回调 */ }

    public override async Task AfterCardDrawn(PlayerChoiceContext ctx, CardModel card, bool fromHandDraw)
    {
        if (Owner != null && card == Owner)   // Owner 指向挂载的模型实例
            await PowerCmd.Apply<StrengthPower>(ctx, Owner.Owner.Creature, 1, Owner.Owner.Creature, null);
    }
}

// 卡牌上挂载：GetOrCreate 自动触发 OnAttach
this.GetOrCreateCapability<DrawPowerCapability>();
```

### 3.2 基类一览（按 owner 类型选）

| 基类 | owner | 说明 |
|---|---|---|
| `CardCapability` | CardModel | 卡牌组件，额外暴露 `OnOwnerCardUpgraded` / `OnOwnerCardDowngraded` 等 |
| `CardPlayCapability` | CardModel | 打出组件，自动比对 `cardPlay.Card == Owner`，只处理自己的打出 |
| `OneShotCardPlayCapability` | CardModel | 打出一次后自动移除自身 |
| `OrbCapability` | OrbModel | 充能球组件，含 `OnOwnerOrbPassiveTriggered` / `OnOwnerOrbEvoked` |
| `RelicCapability` / `PotionCapability` 等 | - | 遗物、药水、能力、怪物各有对应组件 |
| `CharacterCapability` | CharacterModel | 角色组件（不接收原版 hook） |
| `OwnerHookCapability<TModel>` | 任意 | 通用 hook 基类，需手动指定 owner 类型 |
| `UntilCombatEndCapability<TModel>` | 任意 | 战斗结束自动移除 |
| `TurnLimitedCapability<TModel>` | 任意 | 计数回合后自动移除，剩余回合数自动持久化 |

自定义：继承 `ModelCapability` 或 `ModelCapability<TModel>`。

### 3.3 注册方式

```csharp
// 自动：标记 [RegisterModelCapability]
// 手动（Entry.Init 中）：
var content = RitsuLibFramework.GetContentRegistry(ModId);
content.RegisterModelCapability<MyCardCapability>();
// 或静态方法
RitsuLibFramework.RegisterModelCapability<MyCardCapability>(ModId);
```

### 3.4 贡献者接口（向 owner 注入内容）

**通用模型接口（任意 owner）**：

| 接口 | 作用 | 方法 |
|---|---|---|
| `IModelDynamicVarContributor` | 提供动态变量 | `GetDynamicVars(AbstractModel)` |
| `IModelHoverTipContributor` | 添加悬停提示 | `GetHoverTips(AbstractModel)` |
| `IModelAssetPathContributor` | 声明资源路径（阻止打包裁剪） | `GetAssetPaths(ModelAssetPathContext)` |
| `IModelRightClickCapability` | 处理右键交互 | `OnRightClick(ModRightClickExecutionContext)` |

**卡牌专用**：`ICardDescriptionContributor`（描述片段）、`ICardHoverTipContributor`、`ICardGlowContributor`（金/红发光）、`ICardPropertyContributor`（覆盖类型/稀有度/目标/标签）、`ICardPlayStateContributor`（可否打出、回合结束效果）、`ICardPlayResultContributor`（打出后进哪个牌堆）、`ICardTransformCarryOverCapability`（转化时携带到结果牌）。

**充能球专用**：`IOrbValueDisplayContributor`（被动/激发数值标签显示）、`IOrbHoverTipDescriptionContributor`。

描述片段示例（描述底部追加一行）：

```csharp
public class HealOnExhaustCapability : CardCapability, ICardDescriptionContributor
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [ new DynamicVar("HealAmount", 2) ];

    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context) =>
        [ new CardDescriptionFragment(
            new LocString("cards", $"{Id.Entry}.exhaustHealDescription"),   // 可指定本地化表
            CardDescriptionFragmentPlacement.AfterBase) ];
}
// 本地化文件 {modId}/localization/{Language}/cards.json：
// "TEST_MODELCAPABILITY_HEAL_ON_EXHAUST_CAPABILITY.exhaustHealDescription":
//   "被[gold]消耗[/gold]时，回复[blue]{HealAmount}[/blue]点生命。"
```

### 3.5 运行时操作（重要）

> ⚠️ 组件继承自 `AbstractModel`，**禁止 `new` 创建**，必须经注册表/框架 API（`ModelCapabilityRegistry.GetCapabilityId` / `.Create`）。

```csharp
var caps = model.Capabilities();                              // 组件集合 ModelCapabilitySet
var cap  = model.Capability<DrawPowerCapability>();           // 第一个指定类型，无则 null
if (model.TryGetCapability<DrawPowerCapability>(out var existing)) { }
model.GetOrCreateCapability<DrawPowerCapability>();           // 不存在则创建并挂载（最常用）
model.GetOrCreateUpgradeCapability<DrawPowerCapability>();    // 卡牌升级时挂载
var removed = model.RemoveCapability<DrawPowerCapability>();  // 移除
model.ApplyCapability(removed);                               // 应用已有组件（触发合并）
model.AddCapability(cap);                                     // 叠加层数
model.SubtractCapability(cap);                                // 减除层数
caps.InsertBefore<SomeOtherCapability>(myCap);
caps.InsertAfter<SomeOtherCapability>(myCap);
caps.ApplyRange([cap1, cap2, cap3]);                          // 批量应用
```

### 3.6 默认能力、持久化、合并

```csharp
// 默认能力：某 model 天生带组件（Entry 阶段）
content.ConfigureDefaultModelCapabilities<TestRelic>(
    "charge-on-play",                       // modifier id，同 mod 内唯一
    (relic, caps) => caps.Add<ChargingRelicCapability>());

// 持久化：覆写 Save/LoadAdditionalState（也可用 StatefulModelCapability<TState> 自动序列化）
protected override JsonNode? SaveAdditionalState()
    => JsonSerializer.SerializeToNode(new ChargeData { Charge = Charge });
protected override void LoadAdditionalState(JsonNode? state, int schemaVersion)
    => Charge = state?.Deserialize<ChargeData>()?.Charge ?? 0;

// 合并行为：实现 IModelCapabilityMergeHandler
public bool TryMergeWith(IModelCapability incoming, ApplyModelCapabilityOptions options, out IModelCapability? merged)
{
    if (incoming is StackableBuffCapability other) {
        DynamicVars.Cards.BaseValue += other.DynamicVars.Cards.BaseValue;
        merged = this; return true;
    }
    merged = null; return false;
}
// 同类相减：TrySubtractiveMergeWith（归零时 merged = null 移除自身）
```

**坑**：只有 `AddCapability` / `SubtractCapability` / `ApplyCapability` 走合并流程；`GetOrCreateCapability` 只创建一次，**不会叠加**。

---

## 4. 手牌上限（Max Hand Size）

**核心概念**：接口 `IMaxHandSizeModifier`，在能改手牌上限的 `AbstractModel`（如 PowerModel）上实现。

```csharp
[RegisterPower]
public class TestPower : ModPowerTemplate, IMaxHandSizeModifier
{
    public int ModifyMaxHandSize(Player player, int currentMaxHandSize)
    {
        if (player != Owner.Player) return currentMaxHandSize;  // 健康实现：判断当前玩家
        return currentMaxHandSize + 2;
    }
}
```

**注意事项/坑**：
- 读取上限用 `RitsuLibFramework.GetMaxHandSize(player)`，**不要硬编码 10**。
- `ModifyMaxHandSizeLate` 比 `ModifyMaxHandSize` 后执行；设固定值建议用 Late。注意 hook 顺序（每日特效和单例最后触发，见 `IterateHookListeners`）。
- 结果不会小于 0（有兜底）。
- 卡牌自身改手牌上限时：抽牌导致该卡入手，**那次抽牌结果不会变化**，需自行实现。

---

## 5. 手牌泛光（Hand Outline）

**原版（金/红）**：卡牌类里直接重写属性：

```csharp
protected override bool ShouldGlowGoldInternal => Owner.Creature.GetPowerAmount<TestPower>() > 5;
protected override bool ShouldGlowRedInternal  => !Owner.Creature.HasPower<TestPower>();
```

**任意颜色（RitsuLib）**：`Entry.Init` 中注册：

```csharp
// 固定规则：条件 + 颜色
ModCardHandOutlineRegistry.Register<TestCard>(ModCardHandOutlineRules.Fixed(
    card => card.Owner.Creature.CurrentHp <= 10,   // 发光条件
    Colors.Purple,                                 // 发光颜色
    // 0,     // （可选）优先级：更高的才展示
    // false  // （可选）不可打出时隐藏边框
));

// 动态规则：颜色随条件变化
ModCardHandOutlineRegistry.Register<TestCard>(ModCardHandOutlineRules.Dynamic(
    card => card.Owner.Creature.CurrentHp <= 10,
    card => card.Owner.Creature.CurrentHp <= 5 ? Colors.Red : Colors.Orange
));
```

**技巧**：注册时用卡牌基类（如 `TestCard`）可让所有子类统一发光。

---

## 6. 自定义卡牌堆（Custom Card Pile）

**核心概念**：RitsuLib 自定义卡牌堆系统，可做"虚空堆"等额外牌堆。

### 6.1 注册（Entry.Init 中，PileType 存静态变量）

```csharp
public static PileType VoidPile;

public static void Init()
{
    var registry = ModCardPileRegistry.For(ModId);
    VoidPile = registry.RegisterOwned("void_pile", new ModCardPileSpec
    {
        Scope = ModCardPileScope.CombatOnly,     // CombatOnly=每场战斗创建销毁；RunPersistent=跨战斗保留（仅内存，需自行写存档）
        Style = ModCardPileUiStyle.BottomLeft,   // 按钮位置：Headless(不可见)/TopBarDeck/BottomLeft/BottomRight/ExtraHand
        Anchor = ModCardPileAnchor.Default,
        IconPath = "res://Test/images/void_pile.png",
        OnOpen = ctx => ctx.ShowDefaultPileScreen(),   // 点击打开
        VisibleWhen = ctx => ctx.Player != null,
    }).PileType;   // RegisterOwned 返回 ModCardPileDefinition，.PileType 是运行时标识
}
```

### 6.2 Anchor 锚点

- `Anchor` 与 `Style` 一起决定挂载位置；不写等价 `ModCardPileAnchor.Default`（用 Style 默认规则排版）。
- 预设槽位（`new ModCardPileAnchor(ModCardPileAnchorKind.X, offset)`）：

| Kind | 搭配 Style | 说明 |
|---|---|---|
| `StyleDefault` | 任意 | 用该 Style 默认规则自动排版（= Default） |
| `BottomLeftPrimary` | BottomLeft | 抽牌堆按钮右侧起向右叠 |
| `BottomLeftSecondary` | BottomLeft | 弃牌堆按钮右侧起向右叠 |
| `BottomRightPrimary` | BottomRight | 消耗堆按钮左侧起向左叠 |
| `BottomRightSecondary` | BottomRight | 消耗堆按钮左侧起向右叠 |
| `TopBarAfterDeck` | TopBarDeck | 顶栏原版牌组按钮右侧 |
| `TopBarBeforeModifiers` | TopBarDeck | 顶栏右侧每日效果按钮组左侧 |
| `ExtraHandAbove` / `ExtraHandBelow` | ExtraHand | 手牌区域上方/下方，再加 Offset |
| `Custom` | 任意 | 完全自定像素位置，不参与自动排队 |

- 自定义坐标（Kind 必须 `Custom`，四个参数）：`new ModCardPileAnchor(ModCardPileAnchorKind.Custom, Offset: new Vector2(4,4), CustomPosition: new Vector2(200,150), CustomAuthoringPivot: ModCardPileAnchor.PivotCenter)`。控件左上角 = `CustomPosition + Offset - 名义尺寸 * CustomAuthoringPivot`。
- 静态工厂：`ModCardPileAnchor.AtPosition(pos)`（左上角对齐）、`AtCenter(pos)`、`AtPivot(pos, pivot)`（pivot 如 `(1,0)` 为右上）。
- ⚠️ Anchor Kind 须与 Style 搭配，不匹配可能排不到预期位置。

### 6.3 使用与本地化

```csharp
await CardPileCmd.Add(card, Entry.VoidPile);          // 单张移入（原版 CardPileCmd API）
var pile = Entry.VoidPile.GetPile(player);            // 获取牌堆对象
foreach (var c in pile.Cards) Logger.Info($"虚空堆中的卡牌: {c.Id}");
```

- 本地化文件 `{modId}/localization/{lang}/static_hover_tips.json`，ID 格式 `{MODID}_CARDPILE_{LOCALSTEM}`（例 `TEST_CARDPILE_VOID_PILE`），键：`title` / `description`（悬浮提示）/ `empty`（堆为空文本）。

---

## 7. 顶栏按钮（Top Bar Button）

**核心概念**：`IModTopBarButtonHandler` 接口 + `[RegisterOwnedTopBarButton]`，注册自定义顶栏按钮（新类或 singleton 均可）。

> 前提：`Entry.Init()` 已调用 `ModTypeDiscoveryHub.RegisterModAssembly(...)`，否则自动注册不生效。

```csharp
[RegisterOwnedTopBarButton(
    "recipes",                                 // ID → 本地化键 {ModId}_TOPBARBUTTON_{ID}
    IconPath = "res://Test/images/recipe_icon.png",  // （可选）图标
    ButtonOrder = 0,                           // （可选）排序：越小越靠近原版牌组按钮
    OffsetX = 0, OffsetY = 0)                  // （可选）相对自动排布槽位的像素偏移
]
public class RecipeButtonHandler : IModTopBarButtonHandler
{
    public void OnClick(ModTopBarButtonContext ctx)   // 必须实现
    {
        // ctx.OpenCapstoneScreen(myScreen);
        // ctx.ToggleCapstoneScreen(myScreen);
        // ctx.CloseCapstoneScreen();
    }
    public bool IsVisible(ModTopBarButtonContext ctx) => ctx.Player != null;
    public bool IsOpen(ModTopBarButtonContext ctx)   // 界面打开时按钮持续摆动
        => ModScreenService.CurrentCapstoneScreen is MyRecipeScreen;
    public int  GetCount(ModTopBarButtonContext ctx) => -1;   // 角标数字；-1 不显示
}
```

**本地化**：`{modId}/localization/{lang}/static_hover_tips.json`，ID 格式 `{MODID}_TOPBARBUTTON_{LOCALSTEM}`（例 `TEST_TOPBARBUTTON_RECIPES`），键 `title` / `description`。

**显式注册**（需动态注册时，Entry.Init 中）：

```csharp
ModTopBarButtonRegistry.For(ModId).RegisterOwned("recipes", new ModTopBarButtonSpec
{
    IconPath = "res://Test/images/recipe_icon.png",
    OnClick = ctx => RitsuToastService.ShowInfo("配方按钮已点击"),
    VisibleWhen = ctx => ctx.Player != null,
});
```

---

## 快速索引（跨文件复用）

- 自动注册三件套：`[RegisterPower]` / `[RegisterRelic]` / `[RegisterCard]` / `[RegisterModelCapability]` 等 attribute + `Entry.Init()` 中 `ModTypeDiscoveryHub.RegisterModAssembly(...)`。
- UI/悬浮文本统一放 `{modId}/localization/{lang}/static_hover_tips.json`（自定义卡牌堆、顶栏按钮）。
- 动态变量：`CanonicalVars` + `DynamicVar`，描述里用 `{Name}` / `{Name:percentMore()}` 等格式化。
- 持久化三档：战斗/对象级 `SavedAttachedState`/`SavedProperty` → 局内全局 `RunSavedData` → 全游戏 `ModDataStore`（注意显式 `.Save()`）。
