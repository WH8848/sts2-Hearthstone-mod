# RitsuLib 进阶教程精华（G4）

来源：tutorials.sts2modding.com 抓取文本（docs_04-ritsulib_04-09 ~ 04-15-2）。所有内容基于 RitsuLib 框架；`[RegisterXxx]` 特性需在 `Entry.Init()` 启用自动注册才生效：

```csharp
var assembly = Assembly.GetExecutingAssembly();
RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);
ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);
```

通用 ID 规则：RitsuLib 添加的内容 id = `{modid}_{类别}_{原id}`，如 `TEST_EVENT_TEST_EVENT`、`TEST_CHARACTER_TEST_CHARACTER`。

---

## 1. 添加时间线（Epoch/Story）
> 文件：`docs_04-ritsulib_04-09-add-timeline.txt`

- **用途**：2 代用"时期（Epoch）"兼顾人物解锁与故事讲述；人物由多个 Epoch 组成时间线。
- **Epoch 解锁条件 → 特性对照表**：

| 解锁条件 | 解锁内容 | 用的特性（写在角色类上） |
|---|---|---|
| 拥有角色 | 角色 | `[RegisterEpoch]`、`[RegisterStoryEpoch]`、角色上 `[RequireEpoch]` |
| 打一局 | 卡牌/遗物/药水 | `[UnlockEpochAfterRunAs]` |
| 赢一局 | 卡牌/遗物/药水 | `[UnlockEpochAfterWinAs]` |
| 击败第一/二/三幕 Boss | 卡牌/遗物/药水/卡牌 | 指定 ID（按幕检索） |
| 累计击杀 15 精英 | 卡牌/遗物/药水 | `[UnlockEpochAfterEliteVictories]` |
| 累计击杀 15 Boss | 卡牌/遗物/药水 | `[UnlockEpochAfterBossVictories]` |
| 进阶 1 胜利 | 卡牌/遗物/药水 | `[UnlockEpochAfterAscensionOneWin]` |

- **核心类**（故事与时期可放同一文件）：
  - `ModStoryTemplate`（配 `[RegisterStory]`）— 覆写 `StoryKey`（唯一标识防撞车）
  - `CharacterUnlockEpochTemplate<TCharacter>` — 角色本体时期
  - `PackDeclaredCardUnlockEpochTemplate` / `PackDeclaredRelicUnlockEpochTemplate` — 解锁卡牌/遗物

- **时期类骨架**：

```csharp
[RegisterEpoch]
[RegisterStoryEpoch(typeof(TestStory), Order = 0)]
[AutoTimelineSlotBeforeColumn(EpochEra.Seeds0)]   // 自动分配时间线位置
[RequireAllCardsInPool(typeof(TestCardPool))]      // 该池卡牌依赖此时期
public class TestEpoch : CharacterUnlockEpochTemplate<TestCharacter>
{
    public override string Id => "TEST_CHARACTER_EPOCH";
    public override EpochAssetProfile AssetProfile => new(
        PackedPortraitPath: "res://icon.svg", BigPortraitPath: "res://icon.svg");
    // 解锁本时期后顺带解锁的所有后续时期
    protected override IEnumerable<Type> ExpansionEpochTypes => [typeof(TestCardEpoch), ...];
}
```

- **其他时期特性**：
  - `[AutoTimelineSlot(EpochEra.Seeds0)]` — 固定时间线位置（枚举：Seeds0/Blight1/Peace0/Seeds2/Blight2/Prehistoria2/Flourish0/Invitation5 等）
  - `[RegisterEpochCards(typeof(TestCard), ...)]` — 时期解锁的卡牌
  - `[RegisterEpochRelicsFromPool(typeof(TestRelicPool))]` — 时期解锁遗物池
- **按幕检索的时期**：`Id => TestStory.ActEpochKey(1)`，辅助函数：

```csharp
internal static string ActEpochKey(int actNum)
    => ModContentRegistry.GetFixedPublicEntry(Entry.ModId, typeof(TestCharacter)) + $"_{actNum + 1}_EPOCH";
```

- **角色类上挂解锁特性**：

```csharp
[RegisterCharacter]
[RequireEpoch(typeof(TestEpoch))]
[UnlockEpochAfterRunAs(typeof(TestCardEpoch))]
[UnlockEpochAfterWinAs(typeof(TestVictoryEpoch))]
[UnlockEpochAfterEliteVictories(typeof(TestEliteEpoch))]
[UnlockEpochAfterBossVictories(typeof(TestBossEpoch))]
[UnlockEpochAfterAscensionOneWin(typeof(TestAscensionOneEpoch))]
[RevealAscensionAfterEpoch(typeof(TestVictoryEpoch))]   // 胜利后揭示进阶
public class TestCharacter : ModCharacterTemplate<TestCardPool, TestRelicPool, TestPotionPool>
{
    protected override Type? UnlocksAfterRunAsType => typeof(Ironclad); // 仅显示"通过谁解锁"
    public override bool RequiresEpochAndTimeline => true;               // 需要时间线
}
```

- **借别的角色解锁**（初始化函数中）：`ModUnlockRegistry.For(ModId).UnlockEpochAfterRunAs<Silent, TestEpoch>();`
- **本地化** `{modId}/localization/{Language}/epochs.json`：键 = `{EPOCH_ID}.title / .description / .unlock / .unlockInfo / .unlockText`
  - `unlockInfo` 支持条件语法：`{IsRevealed:已经用|用}[green]静默猎手[/green]进行一局游戏{IsRevealed:|来揭示这个历史节点}。`
  - 富文本标签：`[blue]`、`[gold]`、`[red]`、`[green]`、`[purple]`、`[sine]`、`[jitter]` 等

---

## 2. 添加音频
> 文件：`docs_04-ritsulib_04-10-add-audio.txt`

### 方法一：fmod 加载 bank（推荐用于 event 类音频，如角色选择音，无需改代码）
- 工具：fmod studio 2.03.06（官网下载）；参考工程 `https://github.com/BAKAOLC/STS2_FModProject_Minimal`（含原版音频 GUID 对应关系）
- 流程：
  1. 拖入音频到 Assets；Banks 栏新建 bank（如 `Test`）——**不要动原来的 Master**
  2. Events 栏建文件夹防撞车，新建 event → 右键 `Assign To Bank` 选自己的 bank
  3. `Window - Mixer Routing` 建原版一致的 routing：`master/sfx`、`master/music`——决定音频受音响/音乐音量控制
  4. event 内新建 sheet：timeline（拼接/延迟）、action（多音频随机触发，右键 add multi instrument）、parameter（调参数）
  5. `File → Build → Export GUIDs`；从 Build 文件夹复制 `GUIDs.txt` + `Desktop/Test.bank` 到 mod 项目（如 `Test/audios/`）；可 `Edit - Preference - Build` 设自动构建路径
- **坑**：Godot 默认不导入 `.bank`/`GUIDs.txt`，打包 .pck 会缺失导致运行时无声——必须在导出设置的"资源"选项卡"筛选导出非资源文件或文件夹"中包含它们
- 初始化加载：

```csharp
using STS2RitsuLib.Audio;
[ModInitializer(nameof(Init))]
public class Entry
{
    public static void Init()
    {
        FmodStudioDeferredBankRegistration.RegisterBank("res://Test/audios/Test.bank");
        FmodStudioDeferredBankRegistration.RegisterStudioGuidMappings("res://Test/audios/GUIDs.txt");
    }
}
```

- 使用：
  - 人物音频：`Audio: new(CharacterSelectSfx: "event:/sfx/kokodayo", ...)`（还有 AttackSfx/CastSfx/DeathSfx/CharacterTransitionSfx）
  - 卡牌伤害音：`DamageCmd.Attack(...).WithHitFx(sfx: "event:/sfx/sword_slash")`
  - 任意播放：`SfxCmd.Play("event:/sfx/block_gain");`

### 方法二：fmod 直接加载音频文件（wav/ogg/mp3）
- **坑**：fmod 只能加载 Godot 未处理过的音频，三种规避方式（选一）：
  1. 装 fmod 插件 `6.1.0-4.5.0`（addons.zip，解压到项目并启用）
  2. 对该音频禁用 Godot 导入，原样导出
  3. 把音频复制到与 mod 同级目录再加载
- 代码：

```csharp
// 初始化中预载（可选）
FmodStudioStreamingFiles.TryPreloadAsSound("res://Test/audios/waveform.ogg");
// 播放处
FmodStudioStreamingFiles.TryPlaySoundFile("res://Test/audios/waveform.ogg");
```

---

## 3. 添加新怪物
> 文件：`docs_04-ritsulib_04-11-add-monster.txt`

- **类骨架**：`[RegisterMonster]` + `ModMonsterTemplate`
- 进阶数值：`AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 20, 15)`（ToughEnemies=进阶8+取高值；DeadlyEnemies=进阶2+伤害提高）
- 场景与自动转换（复制即可）：

```csharp
public override MonsterAssetProfile AssetProfile => new(VisualsScenePath: "res://Test/scenes/test_monster.tscn");
protected override NCreatureVisuals? TryCreateCreatureVisuals()
    => RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(AssetProfile.VisualsScenePath!);
```

- 战斗开始上 buff：`AfterAddedToRoom()` → `await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, 2m, Creature, null);`
- **意图状态机**：

```csharp
protected override MonsterMoveStateMachine GenerateMoveStateMachine()
{
    var basicAttack = new MoveState("BASIC_ATTACK", BasicAttackMove,
        new SingleAttackIntent(BasicDamage), new DefendIntent());   // 可填任意数量意图，全部展示
    var heavyAttack = new MoveState("HEAVY_ATTACK",
        async targets => await DamageCmd.Attack(HeavyDamage).FromMonster(this)
            .WithAttackerFx(null, AttackSfx).WithHitFx("vfx/vfx_attack_blunt").Execute(null),
        new SingleAttackIntent(HeavyDamage));
    basicAttack.FollowUpState = heavyAttack;   // 意图1 → 意图2
    heavyAttack.FollowUpState = basicAttack;   // 意图2 → 意图1（循环）
    return new MonsterMoveStateMachine([basicAttack, heavyAttack], basicAttack); // 初始意图
}
```

- 复杂分支：`RandomBranchState`（随机意图分支）、`ConditionalBranchState`（条件意图分支）
- 意图执行函数：

```csharp
private async Task BasicAttackMove(IReadOnlyList<Creature> targets)
{
    TalkCmd.Play(L10NMonsterLookup("TEST_MONSTER_TEST_MONSTER.moves.BASIC_ATTACK.banter"), Creature, VfxColor.Blue);
    await DamageCmd.Attack(BasicDamage).FromMonster(this)
        // .WithAttackerAnim("Attack", 0.5f)  // 有攻击动画时取消注释
        .WithAttackerFx(null, AttackSfx)      // 攻击音效
        .WithHitFx("vfx/vfx_attack_blunt")    // 攻击特效
        .Execute(null);
    await CreatureCmd.GainBlock(Creature, BasicBlock, ValueProp.Move, null);
}
```

- **怪物场景结构**（tscn 根为 Node2D，节点名固定，`%`=作为唯一名称访问）：

```
TestCharacter (NCreatureVisuals)
├── Visuals (Node2D) %      # 可换 Sprite2D/SpineSprite 等
├── Bounds (Control) %      # hitbox 大小，决定血条
├── IntentPos (Marker2D) %
├── CenterPos (Marker2D) %
└── TalkPos (Marker2D) %
```

- 本地化 `monsters.json`：`{MOD}_{MONSTER}.name`、`.moves.{MOVE}.title`（意图名）、`.moves.{MOVE}.banter`（对话）

### 遭遇（Encounter）
- 简单遭遇：

```csharp
[RegisterActEncounter(typeof(Glory))]     // 绑定某幕（可换 Overgrowth 等）
public class TestEncounter : ModEncounterTemplate
{
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<TestMonster>()];
    public override RoomType RoomType => RoomType.Monster;
    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
        => [(ModelDb.Monster<TestMonster>().ToMutable(), null)];  // 必须 ToMutable()；null=自动分配槽位
    // public override bool IsValidForAct(ActModel act) => act is Overgrowth; // 可选生成条件
}
```

- 多怪物遭遇额外成员：`IsWeak`（弱怪池）、`EncounterAssetProfile(EncounterScenePath)`、`Slots`（槽位名列表，如 "first".."fourth","first2".."fourth2"）、`GetCameraScaling()`（缩放，场景大时用）、`GetCameraOffset()`（调摄像机）
- 遭遇场景：Node2D 根 + 与 Slots 同名的 Marker2D 子节点标站位
- 本地化 `encounters.json`：键 = `{MOD}-{ENCOUNTER}.title` / `.loss`（`{character}`、`{encounter}` 占位符）
- 自定义遭遇背景：TODO（重载 `CustomEncounterBackground`）

---

## 4. 添加新事件
> 文件：`docs_04-ritsulib_04-12-add-event.txt`

- 注册：`[RegisterActEvent(typeof(Glory))]`（指定幕）；或 `[RegisterSharedEvent]` + 重载 `IsAllowed` 自定义条件
- **多阶段事件骨架**（`ModEventTemplate`）：

```csharp
public sealed class TestEvent : ModEventTemplate
{
    public override EventAssetProfile AssetProfile => new(InitialPortraitPath: "res://images/events/battleworn_dummy.png");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(10m, ValueProp.Unblockable | ValueProp.Unpowered), new GoldVar(60)];

    public override bool IsAllowed(IRunState runState)
        => runState.Players.All(p => p.Gold >= DynamicVars.Gold.BaseValue);

    protected override Task BeforeEventStarted(bool isPreFinished) { Owner!.CanRemovePotions = false; return Task.CompletedTask; }
    protected override void OnEventFinished() { Owner!.CanRemovePotions = true; }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        [new EventOption(this, TakeDamage, InitialOptionKey("TAKE_DAMAGE")),
         new EventOption(this, LoseGold, InitialOptionKey("LOSE_GOLD"))];
}
```

- 选项方法：`CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner!.Creature, DynamicVars.Damage, null, null)`；`PlayerCmd.LoseGold(DynamicVars.Gold.BaseValue, Owner!, GoldLossType.Stolen)`
- **切到下一阶段**：

```csharp
private void ChooseRewardTypePage() => SetEventState(
    L10NLookup($"{Id.Entry}.pages.CHOOSE_TYPE.description"),
    [new EventOption(this, ChoosePotions, ModOptionKey("CHOOSE_TYPE", "CHOOSE_POTIONS")),
     new EventOption(this, ChooseCards, ModOptionKey("CHOOSE_TYPE", "CHOOSE_CARDS"))]);
```

- **结束事件**：`SetEventFinished(L10NLookup($"{Id.Entry}.pages.POTIONS_CHOSEN.description"));`
- 奖励：`RewardsCmd.OfferCustom(Owner!, [new PotionReward(Owner!)]);`；卡牌奖励 `new CardReward(CardCreationOptions.ForNonCombatWithDefaultOdds([Owner!.Character.CardPool]), 3, Owner)`
- **本地化 `events.json`**（id = `TEST_EVENT_TEST_EVENT`）：
  - `{ID}.title`
  - `{ID}.pages.INITIAL.description`（INITIAL=初始页）
  - `{ID}.pages.INITIAL.options.{OPTION}.title/.description` —— 选项 key 由方法名 slugify 生成：`TakeDamage` → `TAKE_DAMAGE`；自建页用 `ModOptionKey` 的第二个参数指定
  - `{Damage}`、`{Gold}`、`{Cards}` 为 DynamicVar 占位符
- **战斗事件**：

```csharp
public override EventLayoutType LayoutType => EventLayoutType.Combat;
public override EncounterModel CanonicalEncounter => ModelDb.Encounter<TestEncounter>();
public Task Fight()
{
    EnterCombatWithoutExitingEvent<TestEncounter>(
        [new SpecialCardReward(Owner!.RunState.CreateCard<LanternKey>(Owner), Owner)],
        shouldResumeAfterCombat: false);   // true 则战斗后继续事件
    return Task.CompletedTask;
}
public override async Task Resume(AbstractRoom room) { }   // shouldResumeAfterCombat=true 时战斗后执行
```

---

## 5. 添加新附魔
> 文件：`docs_04-ritsulib_04-13-add-enchantment.txt`

- `[RegisterEnchantment]` + `ModEnchantmentTemplate`
- 常用覆写：`ShowAmount`（卡上显示数值）、`DisplayAmount`、`HasExtraCardText`、`CanonicalVars`（如 `new CardsVar(2)`）、`ExtraHoverTips`（如 `HoverTipFactory.FromKeyword(CardKeyword.Retain)`）；与卡牌/遗物/药水一样支持 DynamicVars 与 ExtraHoverTips
- 图标：`EnchantmentAssetProfile(IconPath: "res://icon.svg")`，1:1，原版 64x64
- 关键逻辑：

```csharp
public override bool CanEnchant(CardModel card)
    => base.CanEnchant(card) && card.GainsBlock;          // 只允许附魔到"获得格挡"的卡

protected override void OnEnchant() => Card.AddKeyword(CardKeyword.Retain);

public override decimal EnchantBlockAdditive(decimal originalBlock) => Amount;  // 增加格挡量=Amount

public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
{
    if (Status == EnchantmentStatus.Normal)               // 一次性：仅首次可用
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Card.Owner);
        Status = EnchantmentStatus.Disabled;
    }
}
```

- 本地化 `enchantments.json`：`{ID}.title`、`.extraCardText`（附加在卡牌上的文本）、`.description`（附魔介绍）
- 测试：控制台 `enchant TEST_ENCHANTMENT_TEST_ENCHANTMENT [数量] [手牌编号]`
- 效果中施加：`CardCmd.Enchant<TestEnchantment>(card, 2m)`（第二参数设置 Amount）

---

## 6. 添加新人物
> 文件：`docs_04-ritsulib_04-14-add-new-character.txt`

### 三个池子
- `TestCardPool : TypeListCardPoolModel`、`TestRelicPool : TypeListRelicPoolModel`、`TestPotionPool : TypeListPotionPoolModel`
- 关键成员：`Title`（唯一防撞车）、`EnergyColorName`、`TextEnergyIconPath`（24x24，描述用）、`BigEnergyIconPath`（74x74，tooltip/卡面左上角）、`DeckEntryCardColor`（主题色）、`EnergyOutlineColor`、`IsColorless`
- 卡框换色：

```csharp
// 原版卡框：直接替换色调（有 CreateReplaceHueShaderMaterial 时优先用）
private static readonly Material? _poolFrameMaterial = MaterialUtils.CreateReplaceHueShaderMaterial(0.5f, 0.5f, 1f);
// 备选：CreateRgbShaderMaterial(0.5f, 0.5f, 1f)；自定义卡框用 CreateUnmodulatedHsvShaderMaterial()
public override Material? PoolFrameMaterial => _poolFrameMaterial;
```

- 卡牌/遗物/药水注册进池：`[RegisterCard(typeof(TestCardPool))]`（遗物/药水同理换特性名）

### 人物独有内容适配原版机制（Entry.Init 中注册映射）
- 古老牙齿（初始卡→先古升级）：`RitsuLibFramework.RegisterArchaicToothTranscendenceMapping<TestCard, Shiv>();`
- 欧洛巴斯之触（初始遗物升级）：`RitsuLibFramework.RegisterTouchOfOrobasRefinementMapping<TestRelic, Akabeko>();`
- 尘封魔典：池里再建一张先古卡即可（自动排除古老牙齿那张）
- 美味饼干：`AssetProfile` 里 `VanillaRelicVisualOverrides: [new(CharacterOwnedVanillaRelicModelId.YummyCookie, new("res://icon.svg"))]`
- 海玻璃：`relics.json` 加 `"SEA_GLASS.{你人物id}.title"`（id 形如 `TEST_CHARACTER_TEST_CHARACTER`）
- 色彩哲学家：卡池实现 `IModColorfulPhilosophersCardPool` 接口；`events.json` 加 `COLORFUL_PHILOSOPHERS.pages.INITIAL.options.{EnergyColorName大写}.title/.description`

### 人物类
```csharp
[RegisterCharacter]
public class TestCharacter : ModCharacterTemplate<TestCardPool, TestRelicPool, TestPotionPool>
{
    public override Color NameColor => new(0.5f, 0.5f, 1f);
    // EnergyLabelOutlineColor / MapDrawingColor / Gender / StartingHp / StartingGold 同理覆写
    public override int StartingHp => 80;
    public override int StartingGold => 99;
    public override bool RequiresEpochAndTimeline => false;   // 不要时间线时加这句
    public override float AttackAnimDelay => 0f;              // 对齐动画的延迟
    public override float CastAnimDelay => 0f;
    protected override NCreatureVisuals? TryCreateCreatureVisuals()
        => RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(AssetProfile.Scenes!.VisualsPath!);
    public override List<string> GetArchitectAttackVfx() => ["vfx/vfx_attack_blunt", "vfx/vfx_heavy_blunt", ...];
}
```

- **AssetProfile 推荐写法**（缺失资源自动用原版补齐，没有的资源注释掉即可）：

```csharp
public override CharacterAssetProfile AssetProfile => CharacterAssetProfiles.Merge(
    CharacterAssetProfiles.Ironclad(),
    new(Scenes: new(
            VisualsPath: "res://Test/scenes/test_character.tscn",
            EnergyCounterPath: "res://Test/scenes/test_energy_counter.tscn",
            MerchantAnimPath: "res://Test/scenes/test_character_merchant.tscn",
            RestSiteAnimPath: "res://Test/scenes/test_character_rest_site.tscn"),
        Ui: new(
            IconTexturePath: "res://icon.svg",            // 头像图（自适应大小）
            IconPath: "res://Test/scenes/test_icon.tscn", // 左上角/统计页/每日挑战头像（场景！）
            CharacterSelectBgPath: "res://Test/scenes/test_bg.tscn",
            CharacterSelectIconPath: "res://Test/images/char_select_test.png",
            CharacterSelectLockedIconPath: "res://Test/images/char_select_test_locked.png",
            CharacterSelectTransitionPath: "res://materials/transitions/ironclad_transition_mat.tres", // 可选
            MapMarkerPath: "res://icon.svg"),
        Vfx: new(TrailPath: "res://scenes/vfx/card_trail_ironclad.tscn"),   // 卡牌拖尾
        Audio: new(CharacterSelectSfx: "event:/sfx/ui/wipe_ironclad"),      // AttackSfx/CastSfx/DeathSfx...
        Multiplayer: new(ArmPointingTexturePath: null, ArmRockTexturePath: null, ...), // 手指/石头剪刀布
        // Spine: null, VisualCues: null, WorldProceduralVisuals: null,
        // VanillaCardVisualOverrides: [], VanillaRelicVisualOverrides: [...], VanillaPotionVisualOverrides: []
        ));
```

- 初始卡组/遗物二选一：类上 `[RegisterCharacterStarterCard]` / `[RegisterCharacterStarterRelic]`，或覆写 `StartingDeckEntries` / `StartingRelicTypes`

### 各场景结构与坑
- **战斗模型**（Node2D）：`Visuals% / Bounds% / IntentPos% / CenterPos% / TalkPos%`——名字不能改、必须勾"作为唯一名称访问"（出现 %）；`Bounds` 即 hitbox；模型显示在 x 轴上方
  - 3D 模型：`visuals → subviewportcontainer → subviewport`，加 camera3d 与模型，设 `subviewport.transparent = true`，在 3D 视图调视角到 2D 正常显示
  - Spine：`Visuals` 改成 `SpineSprite`（不改名），动画名需有 `idle_loop / attack / cast / hurt / die`（无 SpineSprite 时先装 Spine Godot Extension）
- **能量表盘**（Control）：`EnergyVfxBack% (Node2D) / Layers% (Control，内含 Layer1 + RotationLayers%(旋转层)) / EnergyVfxFront% (Node2D) / Label (Label，名字固定)`——建议复制原版 tscn 起步
- **商店模型**（Node2D，一个节点即可）：Spine 用 `SpineSprite`，默认动画 `relaxed_loop`
- **火堆模型**（Node2D）：`Node(任意) / ControlRoot(Control) → SelectionReticle% / Hitbox% / ThoughtBubbleRight% / ThoughtBubbleLeft%`
  - 非 Spine 动画的 Node 必须放在 ControlRoot 内，否则联机不会自动翻转 X 轴
  - Spine：代码按当前幕自动播 `overgrowth_loop / hive_loop / glory_loop`（仅光照不同）；自定义动画可写脚本继承 `NRestSiteCharacter`
- **过渡动画**：准备 2560x1200 过渡图（越接近白色越先出现，黑最后）→ 建 ShaderMaterial + 如下 shader，`shader_parameter/transitionTex` 设图：

```glsl
shader_type canvas_item;
uniform sampler2D transitionTex;
uniform float threshold : hint_range(0,1);
void fragment() {
    float falloff = 1.0 - texture(transitionTex, UV).r;
    float remap = mix(-0.1, 1.1, threshold);   // 缓解极值处 artifacts
    falloff = step(falloff, remap);
    COLOR.a = falloff;
}
```

### 本地化
- `characters.json` 键：`title / titleObject / description / unlockText({Prerequisite}占位) / possessiveAdjective / pronounSubject / pronounObject / pronounPossessive / cardsModifierTitle / cardsModifierDescription / aromaPrinciple / goldMonologue / eventDeathPrevention / banter.alive.endTurnPing / banter.dead.endTurnPing`
- `ancients.json`（先古对话）：键 = `{ANCIENT}.talk.{人物ID}.{阶段}-{分支}.{char|ancient|next}`，如 `DARV.talk.TEST_CHARACTER_TEST_CHARACTER.0-0.char`；`THE_ARCHITECT.talk.{ID}.{n}-attack` 值为 "Both"

---

## 7. 添加卡池（补充）
> 文件：`docs_04-ritsulib_04-15-1-add-card-pool.txt`

- 人物卡池写法同第 6 章；**注意** `PoolFrameMaterial` 对池内所有卡生效，除非卡牌自己指定了 `FrameMaterial`
- **通用（多职业共享）卡池**：池类上加 `[RegisterSharedCardPool]`；默认不出现在图鉴
- 想进图鉴（Entry.Init）：

```csharp
ModContentRegistry.For(ModId).RegisterCardLibraryCompendiumSharedPoolFilter<MultiClassSharedPool>(
    "reme_multiclass_shared_pool",   // ID
    "res://icon.svg",                // 图标
    null);                           // 放置顺序（可选）
```

- 图鉴悬浮额外文字：`card_library.json` 键 = `{ModId}_POOLFILTER_{ID大写}`，如 `"REME_MOD_POOLFILTER_REME_MULTICLASS_SHARED_POOL": "多职业共享池。"`

---

## 8. 角色动画
> 文件：`docs_04-ritsulib_04-15-2-character-animation.txt`

### VisualCueSet（静态图/帧动画）
- 保留 `VisualsPath` + `TryCreateCreatureVisuals`；场景至少有一个 Sprite2D 节点
- `AssetProfile` 里加 `VisualCues`：

```csharp
VisualCues: ModVisualCues.CueSet()
    .Single("idle", "res://Test/images/character/idle.png")
    .Single("hit", "res://Test/images/character/hit.png", 0.5f)   // 持续0.5秒；不带时长=永久切换
    .Sequence("attack", seq => seq
        .Frame("res://Test/images/character/attack_01.png", 0.06f)
        .Frame("res://Test/images/character/attack_02.png", 0.06f)
        .Frame("res://Test/images/character/attack_03.png", 0.08f))
    .Single("dead", "res://Test/images/character/dead.png")
    .Build()   // 必须调 Build()
```

- 标准 6 状态一键配：覆写 `SetupCustomCombatAnimationStateMachine` 返回 `ModAnimStateMachines.StandardCue(visualsRoot, character, idleName: "idle", deadName: "dead", hitName: "hit", attackName: "attack", castName: "cast", relaxedName: "relaxed")` —— idle/relaxed 循环，其余播完自动回 idle
- 世界场景（商店/篝火）免做完整场景：

```csharp
WorldProceduralVisuals = CharacterWorldProceduralVisualSetBuilder.Create()
    .Merchant(cues => cues.Single("idle", "res://.../merchant_idle.png")
        .Sequence("talk", seq => seq.Frame(".../merchant_talk_01.png", 0.08f).Frame(".../merchant_talk_02.png", 0.08f).Loop()))
    .RestSite(cues => cues.Single("relaxed", "res://.../rest_idle.png"))
    .Build(),
```

### 场景自动转换（Spine/自定义动画）
- 只需 `TryCreateCreatureVisuals() => RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(...)`，场景结构与原版一致即可（教程方式）

### CreatureAnimator（Spine 专用，额外动画名）
```csharp
protected override CreatureAnimator? SetupCustomCreatureAnimator(MegaSprite controller)
{
    var idle = new AnimState("idle", isLooping: true);
    var shiv = new AnimState("shiv");          // 新动画
    var attack = new AnimState("attack");
    attack.NextState = idle;                   // 播完自动回 idle
    var animator = new CreatureAnimator(idle, controller);
    animator.AddAnyState("Idle", idle);
    animator.AddAnyState("Attack", attack);
    animator.AddAnyState("Shiv", shiv);
    // 还有 Dead / Hit / Cast / Relaxed
    return animator;
}
```
- 播放：`await CreatureCmd.TriggerAnim(Owner.Creature, "Shiv", Owner.Character.CastAnimDelay);` 或攻击时 `DamageCmd.Attack(...).WithAttackerAnim("Shiv", 0.5f)`

### AnimationStateMachine（同时支持 spine/静态图/帧动画）
- 覆写 `SetupCustomCombatAnimationStateMachine`，用 builder 建状态 + `AddAnyState(状态名, cue名)` 映射，最后：
  - 帧动画：`return builder.BuildForVisualsRoot(visualsRoot, character);`
  - Spine：`builder.BuildSpine(spineBody)`（注释建议：spine 还是优先用 CreatureAnimator）

---

## 9. 添加单例
> 文件：`docs_04-ritsulib_04-15-add-singleton.txt`

- `SingletonModel`：独立于卡牌/遗物等的 `AbstractModel`；**所有 AbstractModel 都能接收游戏事件**——用于全局影响（如多人模式按玩家数调怪物格挡）或关键词效果（打出抽牌等）
- 骨架：

```csharp
[RegisterSingleton]
public class TestSingleton : HookedSingletonModel
{
    public TestSingleton() : base(HookType.Combat) { }   // HookType: Combat | Run | None
    // 重载 AbstractModel 虚函数，与遗物/药水接口一致：
    // public override Task AfterActEntered() { Log.Info("AfterActEntered"); return Task.CompletedTask; }
    // public override async Task AfterCardDrawn(PlayerChoiceContext ctx, CardModel card, bool fromHandDraw) { ... }
}
```

- 参考：反编译原版 `Hook.cs` 查看全部可监听接口

---

## 跨章节速查（常见坑汇总）
- 所有 `[RegisterXxx]` 依赖自动注册初始化代码（见文首），漏掉则内容静默不生效
- 怪物/人物场景节点名固定且需"唯一名称访问（%）"；`Bounds` 是 hitbox
- `GenerateMonsters()` 里的模型必须 `.ToMutable()`（战斗可变数据，非标准值）
- Godot 打包前导出设置必须包含 `.bank`/`GUIDs.txt`（或其他非资源文件），否则运行时缺失
- 本地化 JSON 统一放 `{modId}/localization/{Language}/`，文件名按类别：epochs/events/monsters/encounters/enchantments/characters/ancients/card_library 等
- 本地化文本支持富文本标签 `[blue][gold][red][green][purple][sine][jitter][b][i][font_size=22]...[/font_size]` 与 DynamicVar 占位符 `{Damage}{Gold}{Cards}{Amount}{Potion1}{IsRevealed:已|未}`
