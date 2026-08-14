# RitsuLib 核心教程（G3）—— 制作 STS2 Mods 精华笔记

> 来源：tutorials.sts2modding.com 第4章（RitsuLib）教程 04-01 ~ 04-08 提炼。
> 原文文件：`docs_04-ritsulib_04-01-add-card.txt` ~ `docs_04-ritsulib_04-08-add-orb.txt`

## 0. 所有教程的共同前提（必读）

`Entry.Init()` 中必须启用自动注册，否则 `[RegisterCard]` 等 attribute 全部不生效：

```csharp
var assembly = Assembly.GetExecutingAssembly();
RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);
ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);
```

- **ID 命名规则**：通过 RitsuLib 添加的内容，最终 id = `{modid}_{类别}_{原id大写SnakeCase}`。类别如 `CARD`/`POWER`/`RELIC`/`POTION`/`EVENT`/`ORB`。例：类 `TestCard` → 原 id `TEST_CARD` → 最终 `TEST_CARD_TEST_CARD`。
- **本地化语言**：`{Language}` 写 `zhs` 为简体中文。
- **资源路径**：`res://{modid}/...` 是 Godot 资源路径，`{modid}` 对应项目里新建的 modid 文件夹（不是根目录）。
- 图片只要是 Godot 能读的格式即可（png/jpg/svg…）。

---

## 1. 添加卡牌（04-01-add-card.txt）

**核心**：继承 `ModCardTemplate`（不要继承 `CardModel`）；`[RegisterCard(typeof(ColorlessCardPool))]` 注册进指定池；`[RegisterCharacterStarterCard(typeof(TestCharacter), 5)]` 注册为角色起始卡（数量5）。

```csharp
[RegisterCard(typeof(ColorlessCardPool))]
public class TestCard : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://Test/images/cards/{GetType().Name}.png"
        // FramePath: "",            // 卡牌背景
        // PortraitBorderPath: "",   // 边框（状态牌"感染"用的）
        // BannerTexturePath: ""     // 横幅（不同类型）
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(12, ValueProp.Move)
    ];

    public TestCard() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .Targeting(cardPlay.Target!)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);
}
```

- `CanonicalVars`（规范值）= 卡牌基础数值；`ValueProp` 是 bitflag 枚举：`Move`（卡牌造成伤害/格挡）、`Unpowered`（不受力量等修正）、`Unblockable`（不可格挡）、`SkipHurtAnim`（跳过受伤动画），可组合如 `ValueProp.Unblockable | ValueProp.Unpowered`。
- **STS2 用 `async/await` 控制效果顺序执行**（选牌时 await 阻塞后续代码，相当于塔1 action 的生态位）；想做什么效果就参考原版同类卡代码。
- 统一卡图：写抽象基类 `TestCardModel : ModCardTemplate`，加 `[RegisterCard(typeof(TestCardPool), Inherit = true)]` 自动注册所有子类；基类里可按 `Type switch` 给 Attack/Skill/Power 设不同 `FramePath`。
- 卡图任意尺寸、无需裁剪；官方尺寸：普通卡 250x190，先古卡 250x351。
- 本地化 `{modId}/localization/zhs/cards.json`：key 为 `{CardId}.title` / `{CardId}.description`；`{Damage:diff()}` 引用 DamageVar。
- 调试：按 `~` 打开控制台输入 `card TEST_CARD_TEST_CARD` 获得卡（**只能在战斗中**）；图鉴里看到 `???` 是正常的（只是没遇到过）。
- 项目结构：`Scripts/` 放代码，**同级新建 `{modid}/` 文件夹**放 images 与 localization（容易漏掉这一层）。

---

## 2. 自定义配置（04-02-custom-config.txt）

RitsuLib 三种方式：**代码流式构建（推荐）/ 反射属性注册 / Schema 声明注册**。持久化由 `ModDataStore` 承担。

### 方式一：代码流式（推荐）

先注册 DataStore，再绑定控件：

```csharp
// 数据模型
public sealed class TestSettings
{
    public bool Enabled { get; set; } = true;
    public int Volume { get; set; } = 80;
    public string Layout { get; set; } = "compact";
}

public static class TestSettingsPage
{
    private const string DataKey = "settings";

    // 绑定：getter/setter lambda，自动走 DataStore 查询保存
    private static readonly ModSettingsValueBinding<TestSettings, bool> EnabledBinding =
        new(Entry.ModId, DataKey, SaveScope.Profile,
            static s => s.Enabled, static (s, v) => s.Enabled = v);

    public static void Register()
    {
        ModDataStore.For(Entry.ModId).Register<TestSettings>(
            key: DataKey, fileName: "settings.json",
            scope: SaveScope.Profile,          // Profile=每存档独立；Global=全存档共享
            defaultFactory: () => new TestSettings(),
            autoCreateIfMissing: true);

        RitsuLibFramework.RegisterModSettings(Entry.ModId, page => page
            .WithTitle(ModSettingsText.Literal("Test"))
            .WithModDisplayName(ModSettingsText.Literal("Test Mod"))
            .WithVisibleOnHostSurfaces(ModSettingsHostSurface.MainMenu | ModSettingsHostSurface.RunPause)
            .AddSection("general", section => section
                .WithTitle(ModSettingsText.Literal("通用"))
                .AddToggle("enabled", ModSettingsText.Literal("启用"), EnabledBinding)
                .AddIntSlider("volume", ModSettingsText.Literal("音量"), VolumeBinding,
                    minValue: 0, maxValue: 100, step: 5,
                    valueFormatter: static v => $"{v}%")
                .AddButton("reset", ModSettingsText.Literal("音量"), ModSettingsText.Literal("重置"),
                    host => { VolumeBinding.Write(80); host.MarkDirty(VolumeBinding); host.RequestRefresh(); },
                    ModSettingsButtonTone.Accent)
                .AddChoice("layout", ModSettingsText.Literal("布局"),
                    new ModSettingsValueBinding<TestSettings, string>(Entry.ModId, DataKey, SaveScope.Profile,
                        static s => s.Layout, static (s, v) => s.Layout = v),
                    [new("compact", ModSettingsText.Literal("紧凑")), new("comfortable", ModSettingsText.Literal("舒展"))],
                    presentation: ModSettingsChoicePresentation.Dropdown)));
    }
}
```

- 读写值：`EnabledBinding.Read()` / `Write(...)`；**修改后必须调 `EnabledBinding.Save()`** 保存。
- 临时（不持久化）绑定：`new InMemoryModSettingsValueBinding<bool>(Entry.ModId, "preview.enabled", initialValue: true)`。
- 投影绑定：多个控件编辑同一大对象时，用根绑定 + `ProjectedModSettingsValueBinding<TestSettings,int>(root, "volume", getter, setter)` 统一保存刷新。

### 方式二：反射注册（简单/字段少的场合）

```csharp
[ModSettingsPage(Entry.ModId)]
[ModSettingsSection("general", Title = "通用")]
public static class TestReflectedSettings
{
    [ModSettingsToggle("enabled", "general")]
    [ModSettingsBinding(Source = ModSettingsReflectionBindingSource.Global)]
    public static bool Enabled { get; set; } = true;

    [ModSettingsIntSlider("volume", "general", 0, 100, 5)]
    [ModSettingsBinding(Source = ModSettingsReflectionBindingSource.Global)]
    public static int Volume { get; set; } = 80;

    [ModSettingsButton("reset", "general", ButtonText = "重置音量")]
    public static void ResetVolume() => Volume = 80;
}
// Init 中：RitsuLibFramework.RegisterModSettingsReflectionProvider<TestReflectedSettings>();
```

- `[ModSettingsBinding]` 的 `Source`：`Global`（全局 DataStore）/ `Profile`（分存档）/ `InMemory`（只存内存）。
- 按钮必须绑 **static 方法**。
- 常用控件 Attribute：`ModSettingsToggle` 开关、`ModSettingsSlider` 浮点滑条、`ModSettingsIntSlider` 整数滑条、`ModSettingsChoice` 选项、`ModSettingsKeyBinding` 快捷键、`ModSettingsButton` 按钮、`ModSettingsString` 单行文本、`ModSettingsMultilineString` 多行文本、`ModSettingsColor` 颜色。

### 方式三：Schema 注册（跨框架、不强制依赖 RitsuLib）

提供 `CreateRitsuLibSettingsSchema()`（返回 JSON 路径或 Dictionary）+ 读写回调，csproj 加 AssemblyMetadata：

```xml
<ItemGroup>
  <AssemblyMetadata Include="RitsuLib.ModSettingsMirror.Mod.Test.DisableSources" Value="baselib,modconfig" />
  <AssemblyMetadata Include="RitsuLib.ModSettingsInterop.ProviderType" Value="Test.Scripts.TestSchemaSettings" />
</ItemGroup>
```

```csharp
public static object CreateRitsuLibSettingsSchema() => "res://Test/settings_schema.json";
public static object? GetRitsuLibSettingValue(string key) => key switch { "enabled" => TestConfig.Enabled, _ => null };
public static void SetRitsuLibSettingValue(string key, object? value) { if (key == "enabled") TestConfig.Enabled = (bool)value!; }
public static void SaveRitsuLibSettings() => TestConfig.Save();
public static void InvokeRitsuLibSettingAction(string key) { if (key == "reset") TestConfig.Volume = 80; }
```

- `settings_schema.json` 结构：`$schema` / `modId` / `modDisplayName` / `pages[]`（`pageId`、`title`、`sections[]`（`id`、`title`、`entries[]`））。
- entry 字段：`id`、`type`、`key`、`label`、`description`、`defaultValue`、`scope`；type 有 `toggle`、`slider`、`int-slider`、`choice`、`string`、`multiline-string`、`color`、`key-binding`、`button`、`header`、`paragraph`、`info-card`、`subpage` 等。
- Schema 文本四种写法：纯字符串、`locString`（游戏内置文本表，需 `{modId}/localization/zhs/settings_ui.json` 本地化）、`i18n`（多语言）、`langMap`（内联语言映射）。

### 通用：ModSettingsText 四种形式

```csharp
ModSettingsText.Literal("Test Mod");                       // 固定字符串（开发期最快）
ModSettingsText.LocString("static_hover_tips", "TEST_HEAT.title", "热量"); // 原版文本表
ModSettingsText.I18N(TestUiText.Text, "settings.title", "Test Mod");       // ritsulib 多语言
ModSettingsText.Dynamic(() => $"已导出 {TestExportState.Count} 张图片");    // 运行时动态
```

高级功能：子页面、可见性、复杂数据结构等，看 RitsuLib 文档与游戏内设置示例。

---

## 3. 添加遗物（04-03-add-relic.txt）

```csharp
[RegisterRelic(typeof(SharedRelicPool))]
// [RegisterCharacterStarterRelic(typeof(TestCharacter))] // 注册起始遗物
public class TestRelic : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"res://Test/images/relics/{GetType().Name}.png",        // 小图标（原版85x85）
        IconOutlinePath: $"res://Test/images/relics/{GetType().Name}.png", // 轮廓图标（原版85x85）
        BigIconPath: $"res://Test/images/relics/{GetType().Name}.png");    // 大图标（原版256x256）

    // 每回合开始时抽一张牌
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
        => await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, player);
}
```

- 继承 `ModRelicTemplate`；`[RegisterRelic(typeof(TestRelicPool))]` 自定义池；换池子就改类型。
- 图片统一在 `AssetProfile` 配置；三张图可偷懒用同一张。
- 本地化 `{modId}/localization/zhs/relics.json`：`{id}.title` / `{id}.description` / `{id}.flavor`；`{Cards}` 对应 CardsVar；描述可染色 `[blue]{Cards}[/blue]`。

---

## 4. 卡牌属性（04-04-card-properties.txt）

### 自定义关键词（消耗/虚无类）

```csharp
[RegisterOwnedCardKeyword(nameof(Unique), IconPath = "res://icon.svg",
    CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
// 注意：不能用 static 静态类！
public class MyKeywords
{
    public static readonly CardKeyword Unique =
        ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Unique)).GetModCardKeyword();
}
```

- `CardDescriptionPlacement`：描述加在卡牌的位置（BeforeCardDescription=描述前）；**默认不显示**。`IconPath` 与 `CardDescriptionPlacement` 均可选。
- 本地化 `{modId}/localization/zhs/card_keywords.json`：key = `TEST_KEYWORD_{id大写}.title/.description`。
- 卡牌类里 `override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Unique, CardKeyword.Exhaust]`；判断用 `Keywords.Contains(MyKeywords.Unique)`；可配合单例 `SingletonModel` 实现逻辑。

### 自定义动态变量

```csharp
protected override IEnumerable<DynamicVar> CanonicalVars => [
    new DamageVar(12, ValueProp.Move),
    ModCardVars.Int("Leech", 3)
        //.WithSharedTooltip("TEST_LEECH") // 需要本地化提示时加这行
];
```

- 简单数值用 `ModCardVars.Int("Leech", 3)` 即可；需要运行时计算（依赖目标/升级/预览）看"RitsuLib 第19章：计算动态变量"。
- 本地化 `static_hover_tips.json`：`TEST_LEECH.title/.description`；卡牌描述里用 `{Leech:diff()}`。
- `:diff()`：值一旦与基础值不同就变红/绿（如升级增伤、预览变绿）。
- 效果写法（不可格挡不受能力影响的伤害 + 回复）：

```csharp
await CreatureCmd.Damage(choiceContext, [cardPlay.Target!], DynamicVars["Leech"].BaseValue,
    ValueProp.Unblockable | ValueProp.Unpowered, cardPlay.Card.Owner.Creature);
await CreatureCmd.Heal(cardPlay.Card.Owner.Creature, DynamicVars["Leech"].BaseValue);
```

### 卡牌提示文本（悬停框）

- **和塔1不同**：STS2 的关键词提示 = 描述染色（`[gold]易伤[/gold]`）+ 卡牌提示文本，两者搭配。
- 加 `消耗` 关键词会自动带提示；若卡牌没有该关键词但描述写了"消耗一张牌"，需手动加：

```csharp
protected override IEnumerable<IHoverTip> AdditionalHoverTips => [
    HoverTipFactory.FromCard<Shiv>(),       // 预览卡牌
    HoverTipFactory.FromPower<TestPower>(), // 能力
    HoverTipFactory.FromKeyword(CardKeyword.Exhaust), // 关键词
];
```

### 自定义 tag（打击/防御类）

```csharp
[RegisterOwnedCardTag(nameof(Heavy))]
public class MyTags
{
    public static readonly CardTag Heavy =
        ModContentRegistry.GetQualifiedCardTagId(Entry.ModId, nameof(Heavy)).GetModCardTag();
}

// 卡牌类里：
protected override HashSet<CardTag> CanonicalTags => [MyTags.Heavy, CardTag.Strike];

// 判断（Card 需为 CardModel）：
if (Card.Tags.Any(t => t == MyTags.Heavy)) { /* Do something */ }
```

- **注意：别忘了给打击/防御类卡加 `strike`/`defend` tag**（会被"打击木偶"类效果增伤）。

---

## 5. 添加能力（04-05-add-power.txt）

```csharp
[RegisterPower]
public class TestPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;              // Buff 或 Debuff
    public override PowerStackType StackType => PowerStackType.Counter; // Counter可叠加 / Single不可叠加
    // public override PowerInstanceType InstanceType => PowerInstanceType.Instanced; // 每次新建实例（像炸弹）；默认在已有能力上堆叠

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://Test/images/powers/test_power.png",    // 小图（原版64x64）
        BigIconPath: "res://Test/images/powers/test_power.png"); // 大图（原版256x256），1:1即可

    // 钩子示例：抽牌后给玩家力量
    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
        => await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, Amount, Owner, null);
}
```

- 钩子机制：想监听别的时机就重写对应方法（如 `AfterCardDrawn`）。给能力用 `PowerCmd.Apply<TestPower>(...)`；调试控制台：`power TEST_POWER_TEST_POWER 1 0`。
- 本地化 `{modId}/localization/zhs/powers.json`：`{id}.description` / `{id}.smartDescription` / `{id}.title`；**`smartDescription` 用 `{Amount}` 显示当前层数**。

### 临时能力（回合结束自动消失）

```csharp
[RegisterPower]
public class TempPower : ModTemporaryAppliedPowerTemplate<TestCard, StrengthPower> { }
// 两个泛型：<谁给的, 代表哪个能力的临时效果>

// 可重载：
// protected override bool IsPositive => false;              // 正面还是负面
// protected override bool UntilEndOfOtherSideTurn => false; // true=另一方回合结束过期；否则拥有者回合结束过期
// protected override int LastForXExtraTurns => 0;           // 额外持续回合数
```

- 多来源包装：`abstract class TempPower<T> : ModTemporaryAppliedPowerTemplate<T, StrengthPower> where T : AbstractModel`，类上加 `[RegisterPower(Inherit = true)]`，子类 `TempFromTestCardPower : TempPower<TestCard>` 标记不同来源/图标。
- 推荐重载 `Description` 让多个 power 共享一条文本（powers.json 中写 `TEST_POWER_TEMP_POWER.description` 与 `..._DOWN.description` 区分正负面）。

---

## 6. 添加药水（04-06-add-potion.txt）

```csharp
[RegisterPotion(typeof(SharedPotionPool))]
public class TestPotion : ModPotionTemplate
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly; // 只能在战斗中使用
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(3)];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromCard<Soul>()];

    public override PotionAssetProfile AssetProfile => new(
        ImagePath: "res://icon.svg",   // 药水本体
        OutlinePath: "res://icon.svg"); // 轮廓图（不一定要png，能当Texture读即可）

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
        => await Soul.CreateInHand(Owner, DynamicVars.Cards.IntValue, Owner.Creature.CombatState!);
}
```

- `CanonicalVars`、`AdditionalHoverTips` 写法与卡牌相同；本地化 `potions.json`：`{id}.title/.description`。

---

## 7. 添加先古之民（04-07-add-ancient.txt）

```csharp
[RegisterActAncient(typeof(Glory))] // 指定只在"荣耀"这章生成
// [RegisterSharedAncient] // 自定义生成条件则注册通用，再重载 IsValidForAct
public class TestAncient : ModAncientEventTemplate
{
    public override Color ButtonColor => new(0.12f, 0.2f, 0.8f, 0.5f);   // 选项按钮颜色
    public override Color DialogueColor => new(0.12f, 0.2f, 0.8f);         // 对话框颜色

    public override EventAssetProfile AssetProfile => new(
        BackgroundScenePath: "res://Test/scenes/test_ancient.tscn");

    public override AncientEventPresentationAssetProfile AncientPresentationAssetProfile => new(
        MapIconPath: "res://icon.svg", MapIconOutlinePath: "res://icon.svg",      // 地图图标/轮廓
        RunHistoryIconPath: "res://icon.svg", RunHistoryIconOutlinePath: "res://icon.svg"); // 历史图标/轮廓

    // 固定池一/二 + 带权重池三
    private IReadOnlyList<EventOption> Pool1 => [CreateModRelicOption<Akabeko>(), CreateModRelicOption<Anchor>()];
    private IReadOnlyList<EventOption> Pool2 => [CreateModRelicOption<LizardTail>(), CreateModRelicOption<ArcaneScroll>()];
    private WeightedList<EventOption> Pool3 => new() { { CreateModRelicOption<YummyCookie>(), 2 }, { CreateModRelicOption<WingCharm>(), 1 } };

    public override IEnumerable<EventOption> AllPossibleOptions => [.. Pool1, .. Pool2, .. Pool3];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        [Rng.NextItem(Pool1)!, Rng.NextItem(Pool2)!, Pool3.GetRandom(Rng)];

    // public override bool IsValidForAct(ActModel act) => act is Overgrowth; // 出现条件（只能在密林）
}
```

- 本地化 `ancients.json`：id = `{modId}_EVENT_{类名大写SnakeCase}`；对话 key 格式：
  `{id}.title`、`{id}.epithet`、`{id}.talk.{角色}.{会话序号}.{台词序号}.{说话者}`，说话者 `.ancient`（先古）/ `.char`（角色）/ `.next`（继续按钮）；角色名 `IRONCLAD`/`SILENT`/`DEFECT`/`NECROBINDER`/`REGENT`，`ANY` 为通用台词。
- 场景 `.tscn`：根节点 `Control`（全屏锚点）+ `ColorRect`（着色器背景，如动态变色 shader）+ `CPUParticles2D`（粒子星空）+ `TextureRect`；文本支持 BBCode（`[i]`、`[font_size=22]` 等）。

---

## 8. 添加充能球（04-08-add-orb.txt）

```csharp
[RegisterOrb]
public class TestOrb : ModOrbTemplate
{
    public override decimal PassiveVal => ModifyOrbValue(1); // 被动数值；ModifyOrbValue=吃集中加成
    public override decimal EvokeVal => ModifyOrbValue(2);   // 激发数值

    // 战斗中数值显示样式：
    // Contextual=普通行为（平时显示被动值，预览激发时显示激发值，大多数球用这个）
    // SinglePassive=只显示被动 / SingleEvoke=只显示激发 / Both=同时显示（原版黑暗球）
    public override ModOrbValueDisplayMode ValueDisplayMode => ModOrbValueDisplayMode.Contextual;

    public override Color DarkenedColor => new(0.1f, 0.2f, 0.5f); // 暗色（用球主体色的暗色调）

    public override OrbAssetProfile AssetProfile => new(
        IconPath: "res://icon.svg",                    // 提示文本小图标
        VisualsScenePath: "res://Test/scenes/test_orb.tscn"); // 充能球场景

    // 免手动挂脚本，复制即可
    protected override Node2D? TryCreateOrbSprite()
        => RitsuGodotNodeFactories.CreateFromScenePath<Node2D>(AssetProfile.VisualsScenePath!);

    public override async Task AfterTurnStartOrbTrigger(PlayerChoiceContext choiceContext)
        => await Passive(choiceContext, null); // 回合开始时触发被动

    public override async Task Passive(PlayerChoiceContext choiceContext, Creature? target)
    { Trigger(); await CardPileCmd.Draw(choiceContext, PassiveVal, Owner); }

    public override async Task<IEnumerable<Creature>> Evoke(PlayerChoiceContext playerChoiceContext)
    {
        PlayEvokeSfx();
        await CardPileCmd.Draw(playerChoiceContext, EvokeVal, Owner);
        return [Owner.Creature]; // 返回受影响角色
    }
}
```

- 生成：`await OrbCmd.Channel<TestOrb>(choiceContext, cardPlay.Card.Owner)`。
- 本地化 `orbs.json`：`{id}.description` / `{id}.smartDescription`（用 `{Passive}`、`{Evoke}`）/ `{id}.title`。
- 场景 `test_orb.tscn` 最小结构：`Node2D` 根 + `Sprite2D` 子节点挂贴图。

---

## 速查：常见命令与 API

| 用途 | 写法 |
|---|---|
| 自动注册前提 | `EnsureGodotScriptsRegistered` + `ModTypeDiscoveryHub.RegisterModAssembly` |
| 卡牌 | `ModCardTemplate` + `[RegisterCard(typeof(池))]` |
| 遗物 | `ModRelicTemplate` + `[RegisterRelic(typeof(池))]` |
| 能力 | `ModPowerTemplate` + `[RegisterPower]` |
| 药水 | `ModPotionTemplate` + `[RegisterPotion(typeof(池))]` |
| 先古事件 | `ModAncientEventTemplate` + `[RegisterActAncient(typeof(章))]` / `[RegisterSharedAncient]` |
| 充能球 | `ModOrbTemplate` + `[RegisterOrb]` |
| 抽牌 | `CardPileCmd.Draw(choiceContext, 数量, 对象)` |
| 伤害 | `DamageCmd.Attack(...)` / `CreatureCmd.Damage(choiceContext, targets, value, props, source)` |
| 治疗 | `CreatureCmd.Heal(creature, value)` |
| 给予能力 | `PowerCmd.Apply<TPower>(choiceContext, target, amount, source, ...)` |
| 生成球 | `OrbCmd.Channel<TOrb>(choiceContext, owner)` |
| 控制台调试 | `card <ID>`（战斗内）、`power <ID> 层数 0` |
