# BaseLib 进阶教程精华（中文整理）

> 来源：tutorials.sts2modding.com 抓取文本（2026-05 系列，作者 Reme）
> 原文文件：`docs_03-baselib_03-09-run-save.txt` / `03-10-mod-integration.txt` / `03-11-add-monster.txt` / `03-12-add-event.txt` / `03-13-add-enchantment.txt` / `03-15-add-singleton.txt` / `03-16-max-hand-size.txt`

---

## 1. 局内保存（03-09-run-save）

### SavedProperty（在 Model 上声明可保存属性）
- 在**卡牌、遗物、附魔、Modifier（每日挑战效果）的 Model 属性**上加 `[SavedProperty]` 即可随局内存档保存。
- **必须是属性（Property），不是字段**。
- 属性名建议加前缀 id 防撞车（如 `Test_GameTurns`）。
- 可用 `SerializationCondition` 控制保存条件（默认 `AlwaysSave` 无论值如何都保存）。

```csharp
[Pool(typeof(SharedRelicPool))]
public class TestRelic : CustomRelicModel
{
    [SavedProperty]
    public int Test_GameTurns { get; set; } = 0;

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new CardsVar(1),
        new DynamicVar("GameTurns", Test_GameTurns)];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        Test_GameTurns++;
        DynamicVars["GameTurns"].BaseValue = Test_GameTurns;
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, player);
    }
}
```

- 描述文本里用 `{Cards}`、`{GameTurns}` 引用动态变量：
  ```json
  "TEST-TEST_RELIC.description": "每回合开始时，抽[blue]{Cards}[/blue]张牌。\n已经历过[blue]{GameTurns}[/blue]回合了。"
  ```

### SavedSpireField<TType, TVal>（不改类也能加可保存变量）
- 用**静态变量**给一个类附加可保存的新变量，无需编辑该类（例如给所有遗物加变量）。
- `TType` 只能是：卡牌、遗物、附魔、Modifier。
- 构造参数：`(默认值工厂, 保存id)`，id 尽量独特防撞车。

```csharp
public static SavedSpireField<TestRelic, int> GameTurnsField = new(() => 0, "Test_GameTurns");

// 使用：Set/Get，或 GameTurnsField[this]
GameTurnsField.Set(this, GameTurnsField.Get(this) + 1);
```

- 支持类型：`int`、`bool`、`string`、`ModelId`、`int[]`、`SerializableCard`、`SerializableCard[]`、`List<SerializableCard>`，以及**枚举**、**枚举数组**。
- `SpireField<TType, TVal>` 与上面用法相同，但**无法保存**。

---

## 2. mod 联动 / 可选依赖（03-10-mod-integration）

- 不引用对方 dll 也能调用对方 API（可选依赖）。
- 对方 mod 侧只需普通 Entry：

```csharp
[ModInitializer(nameof(Init))]
public class Entry
{
    public static void Init() {}
    public static List<string> TestIds = ["test1", "test2", "test3"];
    public static void Register(string id) { TestIds.Add(id); }
}
```

- 你的 mod 侧建一个空壳声明类：

```csharp
using BaseLib.Utils.ModInterop;

// 第一个参数：对方 modid；第二个参数：对方命名空间.类名
[ModInterop("test", "Test.Scripts.Entry")]
public static class TestInterop
{
    // 空实现即可"获得"对方函数定义
    public static void Register(string id) { }

    // InteropTarget 指定对方字段名，空实现即可
    [InteropTarget("TestIds")]
    public static List<string> Ids { get; set; }
}
```

- 调用前检查对方是否已加载：

```csharp
if (ModManager.GetLoadedMods().Any(m => string.Equals(m.manifest?.id, "test")))
{
    TestInterop.Register("JustAnotherModId");
}
```

---

## 3. 添加新怪物（03-11-add-monster）

### 怪物类（`CustomMonsterModel`）

```csharp
public class TestMonster : CustomMonsterModel
{
    // 进阶调整生命值：进阶8+（ToughEnemies）取 120，否则 100
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 120, 100);
    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 140, 120);

    private int BasicDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 12, 10);
    private int BasicBlock => 8;
    private int HeavyDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 24, 20);

    // 场景无自定义脚本时用这个；挂了自定义脚本则改用 CustomVisualPath
    public override NCreatureVisuals? CreateCustomVisuals() =>
        NodeFactory<NCreatureVisuals>.CreateFromScene("res://test/scenes/test_monster.tscn");
    // public override string? CustomVisualPath => "res://test/scenes/test_monster.tscn";

    // 战斗开始时上 buff 等
    public override async Task AfterAddedToRoom()
    {
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, 2m, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        // MoveState(状态ID, 执行函数或lambda, params 意图...（可多个，全部展示）)
        var basicAttack = new MoveState("BASIC_ATTACK", BasicAttackMove,
            new SingleAttackIntent(BasicDamage), new DefendIntent());

        var heavyAttack = new MoveState("HEAVY_ATTACK",
            async targets => await DamageCmd
                .Attack(HeavyDamage)
                .FromMonster(this)
                .WithAttackerFx(null, AttackSfx)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(null),
            new SingleAttackIntent(HeavyDamage));

        // 状态转换：意图1 → 意图2 → 意图1
        basicAttack.FollowUpState = heavyAttack;
        heavyAttack.FollowUpState = basicAttack;

        return new MonsterMoveStateMachine([basicAttack, heavyAttack], basicAttack); // 初始意图
    }

    private async Task BasicAttackMove(IReadOnlyList<Creature> targets)
    {
        TalkCmd.Play(L10NMonsterLookup("TEST-TEST_MONSTER.moves.BASIC_ATTACK.banter"), Creature, VfxColor.Blue);
        await DamageCmd.Attack(BasicDamage).FromMonster(this)
            // .WithAttackerAnim("Attack", 0.5f) // 有攻击动画时启用
            .WithAttackerFx(null, AttackSfx)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
        await CreatureCmd.GainBlock(Creature, BasicBlock, ValueProp.Move, null);
    }
}
```

- 更复杂的状态转换：`RandomBranchState`（随机意图分支）、`ConditionalBranchState`（条件意图分支）。

### 怪物场景 tscn（与角色场景结构一致）
```
TestCharacter (Node2D)
├── Visuals (Node2D) %        ← Sprite2D 等
├── Bounds (Control) %        ← hitbox；血条长度与它相关
├── IntentPos (Marker2D) %
└── CenterPos (Marker2D) %
```
- `Visuals/Bounds/IntentPos/CenterPos` 名字**不能改**，右键勾选"作为唯一名称访问"（出现 `%`）。
- 人物显示在 x 轴上方（Visuals 用负 y 偏移）。

### 本地化：`{modId}/localization/{Language}/monsters.json`
```json
{
  "TEST-TEST_MONSTER.name": "戈多",
  "TEST-TEST_MONSTER.moves.BASIC_ATTACK.title": "基础攻击",
  "TEST-TEST_MONSTER.moves.BASIC_ATTACK.banter": "[jitter]接下这招！[/jitter]",
  "TEST-TEST_MONSTER.moves.HEAVY_ATTACK.title": "重击"
}
```

### 遭遇（`CustomEncounterModel`）

简单单怪物遭遇：

```csharp
public class TestEncounter : CustomEncounterModel
{
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<TestMonster>()];
    public override bool IsValidForAct(ActModel act) => act.ActNumber() == 1; // 只在第一幕
    public override bool IsWeak => false; // 是否弱怪池

    public TestEncounter() : base(RoomType.Monster) { } // 房间类型：普通怪物

    // 生成怪物：必须 ToMutable()（战斗中的可变数据，非标准值）；null = 自动分配槽位
    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        [(ModelDb.Monster<TestMonster>().ToMutable(), null)];
}
```

多怪物遭遇（自定义站位）：

```csharp
public class TestMultiEncounter : CustomEncounterModel
{
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<TestMonster>()];
    public override bool IsValidForAct(ActModel act) => act.ActNumber() == 1;
    public override bool IsWeak => false;
    public override string? CustomScenePath => "res://test/scenes/test_multi_encounter.tscn";
    public override IReadOnlyList<string> Slots => ["first", "second", "third", "fourth", "first2", "second2", "third2", "fourth2"];
    public override float GetCameraScaling() => 0.8f; // 场景太大可调缩放，另有 GetCameraOffset 调摄像机

    public TestMultiEncounter() : base(RoomType.Monster) { }

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        [(ModelDb.Monster<TestMonster>().ToMutable(), "first"), /* ... 每个槽位一个 */];
}
```

- 多怪物场景：根节点 `Control`（铺满屏幕、mouse_filter=2），子节点是与 `Slots` 同名的 `Marker2D` 标注站位。
- 自定义场景遭遇：TODO——重载 `CustomEncounterBackground`。

### 遭遇本地化：`{modId}/localization/{Language}/encounters.json`
```json
{
  "TEST-TEST_ENCOUNTER.title": "一只戈多",
  "TEST-TEST_ENCOUNTER.loss": "{character}被[gold]{encounter}[/gold]折磨而死。"
}
```

---

## 4. 添加新事件（03-12-add-event）

### 简单多阶段事件（`CustomEventModel`，建议 sealed）

```csharp
public sealed class TestEvent : CustomEventModel
{
    public override string? CustomInitialPortraitPath => "res://images/events/battleworn_dummy.png"; // 背景图

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(10m, ValueProp.Unblockable | ValueProp.Unpowered),
        new GoldVar(60)];

    // 事件出现条件：所有玩家金币 >= 60
    public override bool IsAllowed(IRunState runState) =>
        runState.Players.All(p => p.Gold >= DynamicVars.Gold.BaseValue);

    // 事件开始前 / 结束后逻辑
    protected override Task BeforeEventStarted(bool isPreFinished)
    { Owner!.CanRemovePotions = false; return Task.CompletedTask; }
    protected override void OnEventFinished() { Owner!.CanRemovePotions = true; }

    // 初始选项
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        [Option(TakeDamage), Option(LoseGold)];

    private async Task TakeDamage()
    {
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner!.Creature, DynamicVars.Damage, null, null);
        ChooseRewardTypePage();
    }

    private async Task LoseGold()
    {
        await PlayerCmd.LoseGold(DynamicVars.Gold.BaseValue, Owner!, GoldLossType.Stolen);
        ChooseRewardTypePage();
    }

    // 进入第二阶段：第二个参数代表选项所在页面
    private void ChooseRewardTypePage() =>
        SetEventState(PageDescription("CHOOSE_TYPE"), [
            Option(ChoosePotions, "CHOOSE_TYPE"),
            Option(ChooseCards, "CHOOSE_TYPE")]);

    private async Task ChoosePotions()
    {
        await RewardsCmd.OfferCustom(Owner!, [new PotionReward(Owner!)]);
        SetEventFinished(PageDescription("POTIONS_CHOSEN")); // 结束事件
    }

    private async Task ChooseCards()
    {
        await RewardsCmd.OfferCustom(Owner!, [
            new CardReward(CardCreationOptions.ForNonCombatWithDefaultOdds([Owner!.Character.CardPool]), 3, Owner)]);
        SetEventFinished(PageDescription("CARDS_CHOSEN"));
    }
}
```

### 事件本地化：`{modId}/localization/{Language}/events.json`
- 键结构：
  - `{ID}.title`
  - `{ID}.pages.{PAGE}.description`（`INITIAL` 是初始页面）
  - `{ID}.pages.{PAGE}.options.{OPTION}.title` / `.description`
- **选项 id 从函数名自动生成**：`TakeDamage` → `TAKE_DAMAGE`（大写+下划线）。
- 自定义页面名（如 `CHOOSE_TYPE`、`POTIONS_CHOSEN`）就是代码里 `PageDescription(...)` 传的字符串。

### 战斗事件
```csharp
public override EventLayoutType LayoutType => EventLayoutType.Combat;      // 战斗场景
public override EncounterModel CanonicalEncounter => ModelDb.Encounter<TestEncounter>(); // 即将进行的遭遇

public Task Fight()
{
    EnterCombatWithoutExitingEvent<TestEncounter>(
        [new SpecialCardReward(Owner!.RunState.CreateCard<LanternKey>(Owner), Owner)], // 额外奖励
        shouldResumeAfterCombat: false); // 战斗后是否继续事件
    return Task.CompletedTask;
}

// shouldResumeAfterCombat = true 时，战斗结束后执行
public override async Task Resume(AbstractRoom room) { }
```

---

## 5. 添加新附魔（03-13-add-enchantment）

```csharp
public class TestEnchantment : CustomEnchantmentModel
{
    public override bool ShowAmount => true;                    // 卡牌上显示数值
    // public override int DisplayAmount => DynamicVars.Cards.IntValue; // 自定义显示数字
    public override bool HasExtraCardText => true;              // 是否附加卡牌描述文本

    // 与卡牌/遗物/药水一样支持 DynamicVars 和 ExtraHoverTips
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromKeyword(CardKeyword.Retain)];

    // 图标，1:1 即可，原版 64x64
    protected override string? CustomIconPath => "res://icon.svg";

    // 能否附魔：先过基类判断，再限定条件（这里仅限获得格挡的卡）
    public override bool CanEnchant(CardModel card)
    {
        if (base.CanEnchant(card)) return card.GainsBlock;
        return false;
    }

    // 附魔应用时：给卡牌添加保留
    protected override void OnEnchant() => Card.AddKeyword(CardKeyword.Retain);

    // 修改卡牌获得的格挡，返回增加的改变量
    public override decimal EnchantBlockAdditive(decimal originalBlock) => Amount;

    // 卡牌打出时调用；一次性效果用 EnchantmentStatus 控制
    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        if (Status == EnchantmentStatus.Normal)
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Card.Owner);
            Status = EnchantmentStatus.Disabled;
        }
    }
}
```

- 本地化：`{modId}/localization/{Language}/enchantments.json`
  - `{ID}.title` / `{ID}.description`（附魔介绍）/ `{ID}.extraCardText`（附加在卡牌上的文本，可引用 `{Cards}`、`{Amount}`）。
- 使用方式：
  - 控制台：`enchant TEST-TEST_ENCHANTMENT [数量] [给予手牌的编号]`
  - 代码：`CardCmd.Enchant<TestEnchantment>(card, 2m)`（第二个参数用于设置 `Amount`）。

---

## 6. 添加单例（03-15-add-singleton）

- `SingletonModel` 是独立于卡牌/遗物等的 `AbstractModel`；**所有 `AbstractModel` 都能接收游戏事件**。
- 用途：全局影响。例如多人模式用 SingletonModel 判断怪物是否按玩家数提高格挡；也可实现关键词效果（出牌后判断关键词再抽牌）。

```csharp
public class TestSingleton : CustomSingletonModel
{
    public TestSingleton() : base(true, true) { }

    // 重载 AbstractModel 的虚函数即可监听事件，与遗物/药水接口一致
    // public override Task AfterActEntered() { ... }
    // public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw) { ... }
}
```

- 想看全部可用事件接口：反编译原版 `Hook.cs`。

---

## 7. 手牌上限（03-16-max-hand-size）

- 实现 `IMaxHandSizeModifier` 接口即可修改手牌上限（如给 PowerModel 加）：

```csharp
public class TestPower : CustomPowerModel, IMaxHandSizeModifier
{
    // 或实现 ModifyMaxHandSizeLate（比 ModifyMaxHandSize 后执行）
    public int ModifyMaxHandSize(Player player, int currentMaxHandSize)
    {
        if (player != Owner.Player) // 健康实现：判断是否是当前玩家
            return currentMaxHandSize;
        return currentMaxHandSize + 2;
    }
}
```

- 读取玩家手牌上限用 `MaxHandSizePatch.GetMaxHandSize(player)`，**不要硬编码 10**。
- 想设成固定值建议用 `ModifyMaxHandSizeLate`（注意 Hook 顺序：每日特效和单例最后触发，查 `IterateHookListeners`）。
- 结果不会少于 0，最后有兜底。
- 已知坑：卡牌抽牌导致"更改手牌上限的卡牌"入手时，**那次抽牌的结果不会变化**，需自行实现。

---

## 通用速查

- 进阶数值：`AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 高值, 低值)`（生命）、`AscensionLevel.DeadlyEnemies`（伤害）。
- 战斗执行链：`DamageCmd.Attack(x).FromMonster(this).WithAttackerFx(...).WithHitFx("vfx/vfx_attack_blunt").Execute(null)`。
- Model 引用：`ModelDb.Monster<T>()` / `ModelDb.Encounter<T>()`；遭遇生成怪物必须 `.ToMutable()`。
- 玩家上下文：命令类命令多数需要 `new ThrowingPlayerChoiceContext()`。
- 本地化统一路径：`{modId}/localization/{Language}/{monsters|encounters|events|enchantments}.json`，键前缀 `{MODID}-{NAME}`。
