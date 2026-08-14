# 制作 STS2 Mods 精华（Baselib 篇：卡牌/遗物/能力/药水/先古/充能球/配置）

> 来源：tutorials.sts2modding.com「Baselib」章节，共 8 篇教程。已压缩为可复用干货。
> 通用约定：C# 项目、`{modId}.json` 里的 modId 是一个**新文件夹**（非根目录）；资源放 `res://{modId}/...`；本地化文件在 `{modId}/localization/{Language}/...`（`zhs`=简体中文）；id 规则 = `{命名空间第一段大写}-{类名大写SNAKE_CASE}`（如 `Test.Scripts` 命名空间 + `TestCard` → `TEST-TEST_CARD`）。

---

## 1. docs_03-baselib_03-01-add-card.txt — 添加卡牌

核心概念：
- 建 `Cards` 文件夹 + `TestCard.cs`；类继承 **`CustomCardModel`**（不是 `CardModel`）。
- 用 **`[Pool(typeof(ColorlessCardPool))]`** attribute 指定加入哪个颜色卡池，自动注册；自定义池写法见「添加人物」章节。
- 关键 using：`BaseLib.Abstracts`、`BaseLib.Utils`、`MegaCrit.Sts2.Core.Commands`、`.Entities.Cards`、`.GameActions.Multiplayer`、`.Localization.DynamicVars`、`.Models.CardPools`、`.ValueProps`。

关键代码模式：
```csharp
namespace Test.Scripts;

[Pool(typeof(ColorlessCardPool))]
public class TestCard : CustomCardModel
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;          // 卡牌类型
    private const CardRarity rarity = CardRarity.Common;    // 稀有度
    private const TargetType targetType = TargetType.AnyEnemy; // 目标类型
    private const bool shouldShowInCardLibrary = true;      // 是否进图鉴

    // 卡牌基础数值（"规范值"）
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(12, ValueProp.Move)];

    public TestCard() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary) { }

    // 打出效果（async/await 顺序执行，替代塔1的 action 体系）
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)          // .FromCard(this) 是正式版写法
            .Targeting(cardPlay.Target)        // 目标是玩家所选
            .Execute(choiceContext);
    }

    // 升级效果
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4);  // 升级 +4 伤害
    }
}
```

- **ValueProp**（bitflag 枚举，可组合如 `Unblockable | Unpowered`）：`Move`=卡牌造成的伤害/格挡；`Unpowered`=不受力量等修正；`Unblockable`=不可格挡；`SkipHurtAnim`=跳过受伤动画。
- 卡图：重载 `public override string PortraitPath => $"res://{modid}/images/cards/{GetType().Name}.png";`，任意尺寸无需裁剪（官方普通卡 250x190，先古卡 250x351）。可用一个 abstract 中间类统一写路径，避免每张卡重复。
- 本地化 `{modId}/localization/zhs/cards.json`：
  ```json
  {
    "TEST-TEST_CARD.title": "测试卡牌",
    "TEST-TEST_CARD.description": "造成{Damage:diff()}点伤害。"
  }
  ```
  `{Damage:diff()}` 对应 DamageVar；`:diff()` 表示数值与基础值不同时变红/绿（如升级预览）。
- 验证：编译出 dll+pck 进游戏，战斗内按 `~` 控制台输入 `card TEST-TEST_CARD` 获取；图鉴里显示 `???` 是正常的（未遇到过）。若池里一张卡都没有（或只有左上角一张）说明出问题。
- 最终项目结构：
  ```
  Test/
  ├── Scripts/ (TestCard.cs, Entry.cs)
  └── Test/    (modid 文件夹)
      ├── images/cards/TestCard.png
      └── localization/zhs/cards.json
  ```

注意事项/坑：
- 基础攻击/防御牌要加 tag：`public override IEnumerable<CardTag> Tags => [CardTag.Defend];`。
- 防御牌必须设 `public override bool GainsBlock => true;`，否则相关机制不识别。

---

## 2. docs_03-baselib_03-02-mod-config.txt — 自定义模组配置

前置条件（坑）：
- 必须先放一张图片到 **`{modId}\mod_image.png`** 作 mod 图标（尺寸任意），否则报错、配置界面不显示。

核心概念：
- 类继承 **`SimpleModConfig`**（更复杂用 `ModConfig`），成员为 **`public static` 属性**；支持类型：`bool`、`double`、`enum`、`string`。
- 初始化时调用 **`ModConfigRegistry.Register("<modId>")`**。

关键代码模式：
```csharp
public enum FjordMosaicMode { Alpha, Beta, Gamma }

[ConfigHoverTipsByDefault]
public sealed class TestModConfig : SimpleModConfig
{
    [ConfigSection("NimbusWard")]
    public static bool WobbleVexFlag { get; set; } = true;

    public static double PlinthKiteVolume { get; set; } = 2.5;

    [ConfigSlider(-12.5, 88, 0.25, Format = "{0:0.##}%")]
    [ConfigHoverTip]
    public static double MothBanjoBias { get; set; } = 14;

    [ConfigSection("HarborTokens")]
    [ConfigTextInput(TextInputPreset.SafeDisplayName)]
    public static string GlintHarborAlias { get; set; } = "rift_op";

    [ConfigTextInput("[A-Z0-9_]+")]              // 正则校验输入
    public static string KiteVaultCode { get; set; } = "X9";

    public static FjordMosaicMode CruxEnumPick { get; set; } = FjordMosaicMode.Beta;

    [ConfigHoverTip(false)]
    public static bool SilentSporeGate { get; set; }

    [ConfigIgnore]                               // 不显示
    public static double OrphanLedgerAmt { get; set; } = -1;

    [ConfigHideInUI]                             // 隐藏但保留
    public static string NimbusVaultToken { get; set; } = "";

    [ConfigButton("QrkvVaultPing")]              // 按钮：静态方法签名 (ModConfig cfg, NConfigOptionRow row)
    public static void OnVaultPing(ModConfig cfg, NConfigOptionRow row) { _ = cfg; _ = row; }

    [ConfigButton("QrkvRowClear")]               // 按钮：实例方法签名 (NConfigButton btn)
    public void OnRowClear(NConfigButton btn) { _ = btn; }
}
```

注意事项：更多可参考 `BaseLib.Config` 命名空间下的类。

---

## 3. docs_03-baselib_03-03-add-relic.txt — 添加新遗物

核心概念：与卡牌类似，类继承 **`CustomRelicModel`**，`[Pool(typeof(SharedRelicPool))]` 注册。

关键代码模式：
```csharp
[Pool(typeof(SharedRelicPool))]
public class TestRelic : CustomRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    // 遗物数值，替换本地化中的 {Cards}
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    // 三种图标（小/轮廓 85x85，大 256x256）；三张可以指向同一张图
    public override string PackedIconPath => $"res://test/images/relics/{Id.Entry.ToLowerInvariant()}.png";
    protected override string PackedIconOutlinePath => $"res://test/images/relics/{Id.Entry.ToLowerInvariant()}.png";
    protected override string BigIconPath => $"res://test/images/relics/{Id.Entry.ToLowerInvariant()}.png";

    // 每回合开始抽牌：DynamicVars.Cards.IntValue = CardsVar 的数值
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, player);
    }

    // 初始遗物升级写法：
    // public override RelicModel? GetUpgradeReplacement() => ModelDb.Relic<Circlet>();
}
```

- 本地化 `{modId}/localization/zhs/relics.json`：key 为 `TEST-TEST_RELIC.title / .description / .flavor`；描述可用染色标签如 `"每回合开始时，抽[blue]{Cards}[/blue]张牌。"`。

---

## 4. docs_03-baselib_03-04-card-properties.txt — 添加卡牌属性（关键词/动态变量/提示/标签）

### 4.1 新卡牌关键词（消耗、虚无之类）
- 塔2 无需把关键词写进描述，在 `CanonicalKeywords` 添加即可；用 **`CustomEnum`** 给枚举加新值：
```csharp
public class MyKeywords
{
    // 最终 id：{前缀}-{枚举值大写}，如 TEST-UNIQUE
    [CustomEnum("UNIQUE")]
    [KeywordProperties(AutoKeywordPosition.Before)]  // 显示位置：描述前面
    public static CardKeyword Unique;
}
```
- 本地化 `card_keywords.json`：`"TEST-UNIQUE.title"` / `"TEST-UNIQUE.description"`。
- 卡牌里：`public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Unique];`
- 判断：`Keywords.Contains(MyKeywords.Unique)`；可配合 **`SingletonModel`**（单例）实现逻辑。

### 4.2 新动态变量
- 简单数值可直接写；要 tooltip 就加 `.WithTooltip`：
```csharp
protected override IEnumerable<DynamicVar> CanonicalVars =>
[
    new DamageVar(12, ValueProp.Move),
    new DynamicVar("Leech", 1m)          // .WithTooltip("TEST-LEECH") // 要本地化才加
];
```
- 本地化 `static_hover_tips.json`：`"TEST-LEECH.title"` / `"TEST-LEECH.description"`。
- 描述中使用：`"[gold]汲取[/gold]{Leech:diff()}。\n造成{Damage:diff()}点伤害。"`（`:diff()` 变化时红/绿高亮）。
- 效果写法（`DynamicVars["Leech"].BaseValue` 取数值）：
```csharp
await CreatureCmd.Damage(choiceContext, [cardPlay.Target!], DynamicVars["Leech"].BaseValue,
    ValueProp.Unblockable | ValueProp.Unpowered, cardPlay.Card.Owner.Creature);
await CreatureCmd.Heal(cardPlay.Card.Owner.Creature, DynamicVars["Leech"].BaseValue);
```

### 4.3 卡牌提示文本（ExtraHoverTips）
- 塔2 的机制：描述染色（`[gold]易伤[/gold]`）+ 额外提示框；卡牌没有对应关键词但描述提到时用此方式：
```csharp
protected override IEnumerable<IHoverTip> ExtraHoverTips =>
[
    HoverTipFactory.FromCard<Shiv>(),        // 预览卡牌
    HoverTipFactory.FromPower<BlurPower>(),  // 能力提示
    HoverTipFactory.FromKeyword(MyKeywords.Unique)  // 关键词提示
];
```

### 4.4 卡牌 tag（打击/防御等，会被相关机制识别）
```csharp
public class MyCardTags
{
    [CustomEnum]                             // 不写名字则自动生成
    public static CardTag Test;
}
```
- 卡牌中：`protected override HashSet<CardTag> CanonicalTags => [MyCardTags.Test];`
- 判断：`if (Card.Tags.Contains(MyCardTags.Test))`（`Card` 为 `CardModel` 类型）。

---

## 5. docs_03-baselib_03-05-add-power.txt — 添加新能力

核心概念：类继承 **`CustomPowerModel`**；能力通过 `PowerCmd.Apply<...>` 给予。

关键代码模式：
```csharp
public class TestPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;              // Buff 或 Debuff
    public override PowerStackType StackType => PowerStackType.Counter; // Counter=可叠加, Single=不可叠加

    // 图标：1:1 即可；原版大图 256x256，小图 64x64
    public override string? CustomPackedIconPath => "res://test/powers/test_power.png";
    public override string? CustomBigIconPath => "res://test/powers/test_power.png";

    // 事件钩子示例：抽牌后给玩家力量（Amount 为能力层数）
    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, Amount, Owner, null);
    }
}
```

- 本地化 `powers.json`：key 为 `TEST-TEST_POWER.title / .description / .smartDescription`；`smartDescription` 可用 `{Amount}` 显示当前层数：
  ```json
  "TEST-TEST_POWER.description": "每次抽牌时，获得一点[gold]力量[/gold]。",
  "TEST-TEST_POWER.smartDescription": "每次抽牌时，获得[blue]{Amount}[/blue]点[gold]力量[/gold]。"
  ```
- 测试：`PowerCmd.Apply<TestPower>(...)` 或控制台 `power TEST-TEST_POWER 1 0`。

---

## 6. docs_03-baselib_03-06-add-potion.txt — 添加新药水

核心概念：类继承 **`CustomPotionModel`**，`[Pool(typeof(TestPotionPool))]` 注册；结构类似卡牌。

关键代码模式：
```csharp
[Pool(typeof(TestPotionPool))]
public class TestPotion : CustomPotionModel
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;   // 只能在战斗中使用
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(3)];

    // 预览卡牌提示（也可换成关键词提示）
    public override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromCard<Soul>()];

    // 药水图片：不一定要 svg，能转成 Texture 即可
    public override string? CustomPackedImagePath => "res://icon.svg";
    public override string? CustomPackedOutlinePath => "res://icon.svg";

    // 使用效果：创造 3 张灵魂入手的例子
    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        await Soul.CreateInHand(Owner, DynamicVars.Cards.IntValue, Owner.Creature.CombatState!);
    }
}
```

- 本地化 `potions.json`：`TEST-TEST_POTION.title / .description`，如 `"将[blue]{Cards}[/blue]张[gold]灵魂[/gold]加入你的[gold]手牌[/gold]。"`。

---

## 7. docs_03-baselib_03-07-add-ancient.txt — 添加先古之民

核心概念：类继承 **`CustomAncientModel`**；需要代码 + 本地化对话 + 自定义 Godot 场景（tscn）。

关键代码模式：
```csharp
public class TestAncient : CustomAncientModel
{
    public override Color ButtonColor => new(0.12f, 0.2f, 0.8f, 0.5f);   // 选项按钮颜色
    public override Color DialogueColor => new(0.12f, 0.2f, 0.8f);        // 对话框颜色

    public override bool IsValidForAct(ActModel act) => act.ActNumber() == 2; // 出现条件：仅第二幕

    public override string? CustomScenePath => "res://test/scenes/test_ancient.tscn"; // 自定义场景
    public override string? CustomMapIconPath => "res://icon.svg";
    public override string? CustomMapIconOutlinePath => "res://icon.svg";
    public override string? CustomRunHistoryIconPath => "res://icon.svg";
    public override string? CustomRunHistoryIconOutlinePath => "res://icon.svg";

    // 生成选项：每个 OptionPools 一组，组内随机出一个
    protected override OptionPools MakeOptionPools { get; } = new(
        MakePool(AncientOption<Akabeko>(), AncientOption<Anchor>()),
        MakePool(AncientOption<LizardTail>(), AncientOption<ArcaneScroll>()),
        MakePool(AncientOption<YummyCookie>(weight: 2),   // weight 加权，越大越易抽到
            AncientOption<WingCharm>())
    );
}
```

- 本地化 `ancients.json`（已有则追加）；id 同规则 `TEST-TEST_ANCIENT`；key 结构：
  - `TEST-TEST_ANCIENT.title` / `.epithet`
  - 对话：`TEST-TEST_ANCIENT.talk.{场景}.{分支}.{说话人}`，如：
    - `talk.firstVisitEver.0-0.ancient`（首次访问）、`talk.ANY.0-0r.ancient`（通用，`r`=再次访问）
    - 按角色：`IRONCLAD`、`SILENT`、`DEFECT`、`NECROBINDER`、`REGENT`，分支 `0-0`、`1-0r`、`2-0`、`2-1`、`2-2`
    - 说话人后缀：`.ancient`（先古）、`.char`（角色）、`.next`（"继续"按钮文本）
  - 文本支持富文本：`[i][font_size=22]<迟疑的嘀嘀声>[/font_size][/i]`
- 场景 `test_ancient.tscn`：根节点 `Control`（全屏锚点），内含 `ColorRect`（可挂 ShaderMaterial 做动态背景）、`CPUParticles2D`（星尘粒子）、`TextureRect` 等。

---

## 8. docs_03-baselib_03-08-add-orb.txt — 添加充能球

核心概念：类继承 **`CustomOrbModel`**；被动/激发数值吃"集中"等修正需经 `ModifyOrbValue(...)`。

关键代码模式：
```csharp
public class TestOrb : CustomOrbModel
{
    public override decimal PassiveVal => ModifyOrbValue(1);   // 被动数值（ModifyOrbValue=吃集中等修正）
    public override decimal EvokeVal => ModifyOrbValue(2);     // 激发数值

    public override Color DarkenedColor => new(0.1f, 0.2f, 0.5f); // 暗色（球主体色暗调）

    // public override bool IncludeInRandomPool => false;      // 不加入随机球池

    public override string? CustomIconPath => "res://icon.svg";   // 提示图标

    // 球的场景（方案A，限制多：必须有名为 SpineSkeleton 且为 SpineSprite 类型的节点）
    // public override string? CustomSpritePath => "res://test/scenes/test_orb.tscn";

    // 方案B（推荐）：继承自行搭建，父节点 Node2D 即可，无上述限制
    public override Node2D? CreateCustomSprite()
        => PreloadManager.Cache.GetScene("res://test/scenes/test_orb.tscn").Instantiate<Node2D>();

    // 回合开始触发被动
    public override async Task AfterTurnStartOrbTrigger(PlayerChoiceContext choiceContext)
        => await Passive(choiceContext, null);

    // 被动：Trigger() 播放动画后执行效果
    public override async Task Passive(PlayerChoiceContext choiceContext, Creature? target)
    {
        Trigger();
        await CardPileCmd.Draw(choiceContext, PassiveVal, Owner);
    }

    // 激发：返回受影响角色
    public override async Task<IEnumerable<Creature>> Evoke(PlayerChoiceContext playerChoiceContext)
    {
        PlayEvokeSfx();
        await CardPileCmd.Draw(playerChoiceContext, EvokeVal, Owner);
        return [Owner.Creature];
    }
}
```

- 本地化 `orbs.json`：key 为 `TEST-TEST_ORB.title / .description / .smartDescription`；`smartDescription` 用 `{Passive}`、`{Evoke}`：
  ```json
  "TEST-TEST_ORB.smartDescription": "[gold]被动：[/gold]回合开始时，抽[blue]{Passive}[/blue]张牌。\n[gold]激发：[/gold]抽[blue]{Evoke}[/blue]张牌。"
  ```
- 生成球：`await OrbCmd.Channel<TestOrb>(choiceContext, cardPlay.Card.Owner)`。
- `test_orb.tscn` 示例：根节点 `Node2D` + 子节点 `Sprite2D`（texture 指向图标）。
