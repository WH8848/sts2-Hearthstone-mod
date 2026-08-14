# STS2 Mod 教程精华速查（环境 / 迁移 / 发布）

> 来源文件（回查用）：
> - docs_01-env-setup.txt（环境配置）
> - docs_02-install-view-source-and-patch.txt（安装、看源码、修改）
> - docs_03-choose-base-library.txt（选择基础库）
> - docs_04-add-new-character.txt（添加新人物）
> - docs_08-migration-baselib-to-ritsulib.txt（BaseLib→RitsuLib 迁移）
> - docs_07-migration-99-100.txt（正式版→测试版 API 迁移）
> - docs_11-upload-workshop.txt（上传工坊）
> - docs_08-launch-arguments.txt（启动项参数）

---

## 1. 环境配置（docs_01-env-setup.txt）

### 核心概念
- 游戏用 **Godot 4.5.1 Mono** 开发 → 装官方 4.5.1 Mono（.NET 版）；MegaDot 是制作组魔改版，不推荐
- **.NET SDK 9+**；IDE 新手推荐 **Rider**（VS Code 装 C# Dev Kit，开自动保存）
- 模板：RitsuLib → `github.com/alkaid616/RitsuLibModTemplate`；BaseLib → `github.com/Alchyr/ModTemplate-StS2`
- 项目渲染器尽量选 **Mobile**（与游戏一致）

### 关键文件与代码模式

**`{modid}.json`（mod 配置，必须；`{modid}` 换成项目名）：**
```json
{
  "id": "MyMod",              // 必填，唯一 ID，建议和项目名一致
  "name": "我的 Mod",
  "author": "作者名",
  "description": "Mod 描述",
  "version": "0.1.0",         // 必须是 X.X.X 三段语义版本
  "min_game_version": "0.107.1", // 兼容的最小游戏版本
  "has_pck": true,            // 是否有 .pck 资源包
  "has_dll": true,            // 是否有 .dll 代码
  "dependencies": [ { "id": "STS2-RitsuLib", "min_version": "0.2.27" } ],
  "affects_gameplay": true    // 多人模式是否影响内容；纯模型/优化可 false，默认 true
}
```

**`.csproj` 要点（Sdk=Godot.NET.Sdk/4.5.1）：**
```xml
<TargetFramework>net9.0</TargetFramework>
<LangVersion>13.0</LangVersion>
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
<Sts2Dir>D:\...\Steam\steamapps\common\Slay the Spire 2</Sts2Dir>
<Sts2DataDir>$(Sts2Dir)\data_sts2_windows_x86_64</Sts2DataDir>
<!-- 引用游戏 DLL（Private=false 不拷进输出） -->
<Reference Include="sts2"><HintPath>$(Sts2DataDir)\sts2.dll</HintPath><Private>false</Private></Reference>
<Reference Include="0Harmony"><HintPath>$(Sts2DataDir)\0Harmony.dll</HintPath><Private>false</Private></Reference>
<!-- Target "Copy Mod" AfterTargets="PostBuildEvent"：把 $(TargetPath) 和 {modid}.json 拷到 $(Sts2Dir)/mods/$(MSBuildProjectName)/ -->
```

**`Scripts/Entry.cs`（mod 入口）：**
```csharp
using Godot.Bridge;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace Test.Scripts;

[ModInitializer(nameof(Init))]   // 必须，字符串与 Init 同名
public class Entry
{
    public static void Init()
    {
        var harmony = new Harmony("sts2.reme.testmod"); // 随意，别和别人撞
        harmony.PatchAll();
        ScriptManagerBridge.LookupScriptsInAssembly(typeof(Entry).Assembly); // 让 tscn 能加载自定义脚本
        Log.Info("Mod initialized!");
    }
}
```

### 流程与注意
- 构建：`dotnet build`（dll 自动复制到 mods 文件夹）；导出：Godot 项目→导出→**导出 pck/zip**（文件名 `{项目名}.pck`，放 dll 同名目录，**必须 pck 不是 zip**）
- 导出时可排除 `{modid}.json`（资源→排除文件）；mac 兼容：`export_presets.cfg` 里 `binary_format/architecture="msil"`
- 可选自动化：csproj 加 `<GodotExe>` 属性 + `Target ExportPck`（`--headless --export-pack "Windows Desktop" ...`，环境变量 `IsInnerGodotExport=true;MSBUILDDISABLENODEREUSE=1`），Rider 右键 Publish / `dotnet build -t:ExportPck`
- mod 三件套：**dll=代码**（改代码重 build）、**pck=素材**（改素材才重导出）、**json=配置（必须有）**
- 首次运行提示开 mod 选"是"→游戏关闭→再开一次；右下角"已加载模组"即成功
- 预览图：mod 根目录下与 modid 同名的文件夹里放 `mod_image.png`

---

## 2. 安装、看源码、修改（docs_02-install-view-source-and-patch.txt）

### 核心概念
- 安装：`Slay the Spire 2\mods\` 下放 dll+pck+json（可套文件夹）
- **存档分离**：有/无 mod 的存档独立；复制 `C:\Users\[用户]\AppData\Roaming\SlayTheSpire2\steam\[steamid]\profile1` 等 → `modded` 文件夹
- 反编译看源码（二选一）：
  - **gdsdecomp**（GDRETools）：`gdre_tools.exe` → RE Tools → Recover Project → 选 `SlayTheSpire2.pck` → Extract（网络问题：Export Settings 关 Download Plugins）；用 Godot 导入 `project.godot` 即可，无需能运行
  - **ilspy / dnspy**：打开 `data_sts2_windows_x86_64\sts2.dll`
- 代码命名空间：**`MegaCrit.Sts2.Core.Models`**（`.Cards` = 卡牌）
- 查卡牌类名技巧：`localization\zhs\cards.json` 搜中文名 → 类名 → 全局搜索

### 关键模式
- 修改代码用 **Harmony**（≈尖塔1的 patch），文档：harmony.pardeike.net/articles/basics.html
- 控制台：开 mod 后按 **~**，`help` 看命令（如 `card SURVIVOR` 加卡入手），`help card` 看命令帮助
- 看 log：控制台 `open logs` / `showlog`（后者需 baselib）；或 bat 里加 `--log` 并以命令行方式运行

### 注意
- 本地联机测试：两个 bat 分别加 `--fastmp=host` 与 `--fastmp=join --clientId=1001`（多玩家改 clientId）；**打完一层遇保存问题 → 管理员模式启动 bat**
- 项目改名（三处统一）：`project.godot` 的 `config/name` + `project/assembly_name`；`{modid}.csproj`；`{modid}.json`（含 `id`）；`{modid}.sln`（含 csproj 引用）；重新打包并删旧名 mod
- 上传器：`github.com/megacrit/sts2-mod-uploader`

---

## 3. 选择基础库（docs_03-choose-base-library.txt）

### 核心概念
- 人物 mod 建议选基础库：减 patch 量、提高兼容性；**BaseLib** 与 **RitsuLib** 二选一，另有随从库 **MinionLib**
- 不添加游戏内容则无需基础库

### 功能对比（截至 2026.07）
| 功能 | BaseLib | RitsuLib |
|---|---|---|
| 基础注册/局内数据/手牌上限/Interop/配置/占位素材/关键词/血条覆盖层/场景转换/新牌堆/角标/自定义奖励/节点附加/组件 | ✅ | ✅（实现更完善） |
| 一代本地化符号 | ✅ | ❌ |
| FMOD 音频 | ❌（用 Godot 原生音频） | ✅ |
| 诊断调试、数据持久化、顶栏按钮、通知提示、自定义目标、数据遥测、更新检查、时间线、事件管线、生命周期、手牌发光、快捷键、动画状态机、次级资源（类星辉）、网络 custommessage | ❌ | ✅ |
| 动画状态机 | 接受原版动画名，不能自定义 | ✅ 自定义 |
| patch | 原始 Harmony | 原始 Harmony + 封装 patch 系统 |

### 注意（内容物 ID 规则）
- BaseLib：`{命名空间第一段大写}-{原卡牌id}`，例 `TEST-TEST_CARD`
- RitsuLib：`{ModId}_{类别}_{原卡牌id}`，例 `TEST_CARD_TEST_CARD`

---

## 4. 添加新人物（docs_04-add-new-character.txt，基于 BaseLib）

### 核心概念
- 需三个专属池子：卡牌/遗物/药水；卡牌用 `[Pool(typeof(...))]` 挂到池

**池子类：**
```csharp
public class TestCardPool : CustomCardPoolModel
{
    public override string Title => "test";   // 池 ID，必须唯一防撞车
    public override string? TextEnergyIconPath => "res://test/images/energy_test.png"; // 描述内能量图标 24x24
    public override string? BigEnergyIconPath => "res://test/images/energy_test_big.png"; // tooltip/卡牌左上角 74x74
    public override Color DeckEntryCardColor => new(0.5f, 0.5f, 1f); // 池主题色
    public override Color ShaderColor => new(0.5f, 0.5f, 1f);        // 默认卡框染色
    // public override Texture2D? CustomFrame(CustomCardModel card) => ... 自定义卡框图
    public override bool IsColorless => false; // 事件/状态等无色池为 true
}
// TestRelicPool : CustomRelicPoolModel、TestPotionPool : CustomPotionPoolModel 只需两个能量图标路径
```

**初始卡升级 / 初始遗物升级：**
```csharp
[Pool(typeof(TestCardPool))]
public class TestCard : CustomCardModel, ITranscendenceCard   // 古老牙齿 → 先古升级
{
    public CardModel GetTranscendenceTransformedCard() => ModelDb.Card<TestCard2>();
}

[Pool(typeof(TestRelicPool))]
public class TestRelic : CustomRelicModel                     // 欧洛巴斯之触 → 升级遗物
{
    public override RelicModel? GetUpgradeReplacement() => ModelDb.Relic<TestRelic2>();
}
// 尘封魔典：自动从你的池子选先古卡（去除古老牙齿那张），只需再建一张先古卡
```

### 人物类（继承 PlaceholderCharacterModel，缺资源就注释掉用原版）
```csharp
public class TestCharacter : PlaceholderCharacterModel
{
    public override Color NameColor => new(0.5f, 0.5f, 1f);          // 名称颜色
    public override CharacterGender Gender => CharacterGender.Masculine;
    public override int StartingHp => 80;
    public override string CustomVisualPath => "res://test/scenes/test_character.tscn";      // 战斗模型
    public override string CustomIconTexturePath => "res://icon.svg";                         // 头像图片
    public override string CustomEnergyCounterPath => "res://test/scenes/test_energy_counter.tscn"; // 能量表盘
    public override string CustomCharacterSelectBg => "res://test/scenes/test_bg.tscn";       // 选择背景
    public override string CustomCharacterSelectIconPath => "res://test/images/char_select_test.png";
    public override string CustomCharacterSelectLockedIconPath => "res://test/images/char_select_test_locked.png";
    public override string CharacterTransitionSfx => "event:/sfx/ui/wipe_ironclad";          // 过渡音效，不能删
    public override CardPoolModel CardPool => ModelDb.CardPool<TestCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<TestRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<TestPotionPool>();
    public override IEnumerable<CardModel> StartingDeck => [ ModelDb.Card<TestCard>(), /* x5 */ ];
    public override IReadOnlyList<RelicModel> StartingRelics => [ ModelDb.Relic<TestRelic>() ];
    public override List<string> GetArchitectAttackVfx() => [ "vfx/vfx_attack_blunt", "vfx/vfx_heavy_blunt", "vfx/vfx_attack_slash", "vfx/vfx_bloody_impact", "vfx/vfx_rock_shatter" ];
    // 其它可选：CustomTrailPath / CustomIconPath(场景) / CustomRestSiteAnimPath / CustomMerchantAnimPath /
    // 多人手指/石头剪刀布贴图 / CustomCharacterSelectTransitionPath / CustomMapMarkerPath /
    // 音效 CustomAttackSfx 等（baselib 3.1.1+ 支持 "res://...wav" 路径）
}
```

### 场景结构约定（节点名不能改，标 % 的需"作为唯一名称访问"）
- **战斗模型** `Node2D`：`Visuals (Node2D) %` / `Bounds (Control) %`（=hitbox，血条长度） / `IntentPos (Marker2D) %` / `CenterPos (Marker2D) %`；人物显示在 x 轴上方；3D 模型用 `visuals → subviewportcontainer → subviewport` + camera3d + 模型，subviewport 设 transparent=true
- **动画**：Visuals 可换成 SpineSprite / Sprite2D / AnimatedSprite2D / AnimationPlayer；Spine 与 AnimatedSprite2D 的动画名必须为 `idle_loop / attack / cast / hurt / die`
- **能量表盘** `Control`：`EnergyVfxBack (Node2D) %` / `Layers (Control) %`（内 `Layer1` 任意 + `RotationLayers (Control) %` 放旋转层）/ `EnergyVfxFront (Node2D) %` / `Label (Label)`；BaseLib 处理后无需挂脚本
- **商店模型** `Node2D` 单节点；Spine 默认动画名 `relaxed_loop`
- **火堆**：`AssetProfile.Scenes: new(RestSiteAnimPath: "res://...tscn")`；结构 `Node2D` + `Node(任意)` + `ControlRoot (Control) %`（内 `SelectionReticle % / Hitbox % / ThoughtBubbleRight % / ThoughtBubbleLeft %`）；Spine 按幕播放 `overgrowth_loop / hive_loop / glory_loop`（仅光照色不同）；自定义动画可继承 `NRestSiteCharacter`
- 附赠资源含 test_bg / test_character / test_energy_counter / test_character_merchant / test_character_rest_site / test_icon 的完整 tscn

### 本地化
- 路径：`{modId}/localization/{Language}/characters.json`
- 键格式 `{ID}-{CHAR}.xxx`：`title`、`titleObject`、`description`、`possessiveAdjective`、`pronounSubject/Object/Possessive`、`unlockText`（`{Prerequisite}` 占位）、`cardsModifierTitle/Description`、`aromaPrinciple`、`goldMonologue`、`eventDeathPrevention`、`banter.alive/dead.endTurnPing`
- 先古对话：`{modId}/localization/{Language}/ancients.json`，键 `{ANCIENT}.talk.{CHAR}.{0-0|1-0r|2-0|2-1|2-2}.{char|ancient|next}`（ANCIENT：DARV/NEOW/NONUPEIPE/OROBAS/PAEL/TANX/TEZCATARA/VAKUU/THE_ARCHITECT）

### 注意
- Init 中必须调用 `ScriptManagerBridge.LookupScriptsInAssembly(typeof(Entry).Assembly)`
- 项目设置里**禁用"将文本资源转换为二进制"**

---

## 5. BaseLib → RitsuLib 迁移（docs_08-migration-baselib-to-ritsulib.txt）

### 核心概念
- 两库都支持自动注册；RitsuLib 需在 `Entry.Init` 调用 `ModTypeDiscoveryHub.RegisterModAssembly`
- ID 规则不同（见第 3 节）

### 注册与基类对照
| 内容 | BaseLib | RitsuLib |
|---|---|---|
| 卡牌 | `[Pool(typeof(TestCardPool))]` + `CustomCardModel` | `[RegisterCard(typeof(TestCardPool))]` + `ModCardTemplate` |
| 遗物 | `[Pool(typeof(TestRelicPool))]` + `CustomRelicModel` | `[RegisterRelic(...)]` + `ModRelicTemplate` |
| 药水 | `[Pool(typeof(TestPotionPool))]` + `CustomPotionModel` | `[RegisterPotion(...)]` + `ModPotionTemplate` |
| 能力 | `CustomPowerModel` | `[RegisterPower]` + `ModPowerTemplate` |
| 附魔 | `CustomEnchantmentModel` | `ModEnchantmentTemplate` |
| 遭遇 | `CustomEncounterModel` | `[RegisterActEncounter(typeof(X))]` + `ModEncounterTemplate` |
| 先古之民 | `CustomAncientModel` | `[RegisterSharedAncient]` / `[RegisterActAncient(typeof(X))]` + `ModAncientEventTemplate` |
| 事件 | 不用 | `[RegisterSharedEvent]` / `[RegisterActEvent(typeof(X))]` |
| 人物 | `PlaceholderCharacterModel` | `ModCharacterTemplate<TestCardPool, TestRelicPool, TestPotionPool>` |

### 成员对照
- 提示文本：`ExtraHoverTips` → `AdditionalHoverTips`
- 关键词：`CanonicalKeywords` → `RegisteredKeywordIds`；`HoverTipFactory.FromKeyword(X)` → `ModKeywordRegistry.CreateHoverTip(X)`；`[CustomEnum("UNIQUE")]` → `[RegisterOwnedCardKeyword("Unique", IconPath = "res://icon.svg")]`；`.WithTooltip` → `.WithSharedTooltip`
- 人物：`CustomVisualPath` → `CustomVisualsPath`；`CustomCharacterSelectBg` → `CustomCharacterSelectBgPath`；`StartingDeck` → `StartingDeckEntries`（或卡上 `[RegisterCharacterStarterCard]`）；`StartingRelics` → `StartingRelicTypes`（或 `[RegisterCharacterStarterRelic]`）；池绑定 → 泛型模板
- 池：`CustomCardPoolModel` → `TypeListCardPoolModel`；药水/遗物池同理 `TypeListXxxPoolModel`
- 药水/能力/充能球：`CustomPackedImagePath` → `CustomImagePath`；`CustomPackedOutlinePath` → `CustomOutlinePath`；`CustomPackedIconPath` → `CustomIconPath`；`CreateCustomSprite()` → `CustomVisualsScenePath`；`CreateCustomVisuals()` → `CustomVisualsPath`
- 遭遇/先古：`IsValidForAct(ActModel)` → `[RegisterActEncounter(typeof(Glory))]`；`base(RoomType.Monster)` → `override RoomType RoomType => RoomType.Monster`；`CustomScenePath` → `CustomEncounterScenePath` / `CustomBackgroundScenePath`；出现条件 `IsValidForAct` → `IsAllowed(IRunState runState)`；`MakeOptionPools` → `AllPossibleOptions + GenerateInitialOptions()`；`AncientOption<T>()` → `CreateModRelicOption<T>()`
- 事件选项：`Option(TakeDamage)` → `new EventOption(this, TakeDamage, InitialOptionKey("TAKE_DAMAGE"))`；`Option(ChoosePotions, "CHOOSE_TYPE")` → `new EventOption(this, ChoosePotions, ModOptionKey("CHOOSE_TYPE", "CHOOSE_POTIONS"))`；`PageDescription("CHOOSE_TYPE")` → `L10NLookup($"{Id.Entry}.pages.CHOOSE_TYPE.description")`

### 先古/升级（RitsuLib 写法）
```csharp
[RegisterCard(typeof(TestCardPool))]
[RegisterArchaicToothTranscendence(typeof(Shiv))]   // 古老牙齿把此牌变成 Shiv
public class TestCard : ModCardTemplate {}

[RegisterRelic(typeof(TestRelicPool))]
[RegisterTouchOfOrobasRefinement(typeof(Akabeko))]  // 欧洛巴斯之触把此遗物变成 Akabeko
public class TestRelic : ModRelicTemplate {}

// 或在 Init 中：
RitsuLibFramework.RegisterArchaicToothTranscendenceMapping<TestCard, Shiv>();
RitsuLibFramework.RegisterTouchOfOrobasRefinementMapping<TestRelic, Akabeko>();
```

### 场景
- BaseLib：大部分场景**全自动转换**（无需挂脚本/唯一化命名）
- RitsuLib：**半自动**（角色类重载 `TryCreateCreatureVisuals`）；能量表盘全自动

---

## 6. 版本迁移 0.99 → 0.110（docs_07-migration-99-100.txt，摘录破坏性变更）

### 0.109 → 0.110
- 新增 **`CombatId`**（`readonly record struct CombatId(int Value)`）：标识一次战斗，防已结束战斗的延迟操作泄漏到下一场
- `CombatManager`：`BeginCardOrPotionEffect(Player)` 返回 `CombatId?`；`EndCardOrPotionEffect` / `CheckForEmptyHand` / `HandlePlayerDeath` / `RemoveDeadPlayerCardsFromCombat` 均加 `CombatId?` 首参（见下示例）；`EndPlayerTurnPhaseTwoInternal` / `SwitchFromPlayerToEnemySide` 去掉可选参；新增 `CurrentCombatId` 属性
```csharp
// 0.109                     → 0.110
CombatManager.Instance.BeginCardOrPotionEffect(Owner);
await CombatManager.Instance.EndCardOrPotionEffect(Owner);
await CombatManager.Instance.CheckForEmptyHand(choiceContext, originalOwner);
// 改为：
CombatId? effectCombatId = CombatManager.Instance.BeginCardOrPotionEffect(Owner);
await CombatManager.Instance.EndCardOrPotionEffect(effectCombatId, Owner);
await CombatManager.Instance.CheckForEmptyHand(effectCombatId, choiceContext, originalOwner);
```
- `MegaInput`：`accept` → `confirm`；`releaseCard` 删除；新增 `endTurn = "ui_end_turn"`
- `BranchingPlayerChoiceContext` ctor 加 `GameAction` 参；新增枚举 `InputType { MouseAndKeyboard, KeyboardOnlyMode, Controller }`
- 新增 `PeerVersionInfo`（多人版本校验，含 `gameplayAffectingMods`）
- `LobbyPlayer` 拆分为 `RunLobbyPlayer / LoadRunLobbyPlayer / StartRunLobbyPlayer`；`RunLobby.ConnectedPlayerIds` → `PlayerIds`
- `ProgressState.TotalUnlocks` 字段→计算属性，新增 `GrantNextUnlock()`

### 0.108 → 0.109
- `AbstractModel.AfterBlockBroken` 加 `(PlayerChoiceContext, Creature target, Creature? breaker)`
- 卡牌落点重构：`ModifyCardPlayResultPileTypeAndPosition`（`(PileType, CardPilePosition)` 元组）→ **`ModifyCardPlayResultLocation`**（新 `record struct CardLocation(Player, PileType, CardPilePosition)`）；`AfterModifyingCardPlayResultPileOrPosition` → `AfterModifyingCardPlayResultLocation`；`GetResultPileTypeAndPositionForCardPlay` → `GetResultLocationForCardPlay`
- `CardModel.CreateDupe()` 加 `Player newOwner`；`CreatureCmd.LoseBlock` 加 `(PlayerChoiceContext, Creature? remover)`
- `CardPileCmd.Draw` 去 async（返回 `Task<IEnumerable<CardModel>>`），新增 `DrawWithoutBlockingOnOtherPlayers`；`CardCmd.ApplySingleTurnRetain(CardModel)`；`CardSelectCmd.FromCombatPile` filter 可空
- `CombatManager.EndCardOrPotionEffect` 改 `Task`；新增 `CombatBegan` 事件、`RemoveDeadPlayerCardsFromCombat`；`EndPlayerTurnPhaseTwoInternal(CancellationToken?)`
- **RNG 重构 uint→ulong（种子 12 位）**：`StringHelper.GetDeterministicHashCode` 返回 ulong（旧版用 `GetDeterministicHashCodeOld`）；`Rng.Seed` 改 ulong、ctor 全改、`Counter`/`FastForwardCounter` 删除、新增 `NextUnsignedLong`/`ToSerializable`/`LoadFromSerializable`；`EventSynchronizer`、`MegaRandom`、`PlayerRngSet`、`RunRngSet` 同步改；`ModelIdSerializationCache` 合并 `SavedPropertiesTypeCache`；`MegaCritSerializerContext` 删 `UInt32`、加 `SerializableRng`
- `PlayerChoiceContext.SignalPlayerChoiceBegun` 加 `Player chooser` 参（影响全部子类 override）；新增 `ModelStack`、`OwnerId`；`HookPlayerChoiceContext` ctor 的 `ICombatState` 可空 + 静态 `GetOwner`；新增 `BranchingPlayerChoiceContext`
- `PotionModel` 新增 `LargeImagePath` / `LargeImage`

### 0.107 → 0.108
- `ModifyDamageAdditive/Multiplicative/Cap` 加 `CardPlay?` 参；新增 `BeforeCombatRewardOffered`、`AbstractModel.IsMock`
- `AttackCommand.FromCard/FromOsty` 加 `CardPlay?`；`CreateContextAsync`/`AttackContext.CreateAsync` 参数 `CardModel` → `CardPlay`；新增 `CardModel.CreateCloneForPlayer/GiveToAnotherPlayer`
- `OrbModel`：`Triggered` → `PassiveActivated`；`Trigger()` 拆为 `ActivateEvoke(Creature[])` + `TriggerPassive(PlayerChoiceContext, Creature?)`；新增 `EvokeActivated`
- 事件：`BeginEvent` 加 `EventCombatSynchronizer?`；`GenerateInternalCombatState`/`ResetInternalCombatState` 删除，由新类 `EventCombatSynchronizer`（InitializeForEvent/ReadyToEnterCombat/ResetState/...）替代；`EncounterModel.IsDebugEncounter` 删除
- `EpochModel` 删 `Year/EraName/ModelId/IsArtPlaceholder/PackedPortraitPath`；新增 `HasRealPortrait`、`AllEpochs`
- `CardCreationOptions` 卡池与过滤器拆分：删 `CustomCardPool/ForNonCombatWithDefaultOdds/WithRngOverride`；`WithCardPools(IEnumerable<CardPoolModel>)` 简化；新增 `WithFilter(Func<CardModel,bool>)`
- `Controller`：`dPadNorth/South/East/West` → `dPadUp/Down/Right/Left`；`joystickPress` → `lStickPress`

### 0.106 → 0.107
- **`ActModel` 新增 3 个 abstract 成员**：`Index`、`IsDefault`、`IsUnlocked(UnlockState)`——继承 ActModel 的 mod 必须实现，否则编译失败
- `AbstractModel` 新增：`TryModifyKeywordsInCombat`、`ModifyGoldGained`（替代 `ShouldGainGold`）、`ModifyPowerAmountGivenAdditive/Multiplicative`（替代 `ModifyPowerAmountGiven`）、`AfterModifyingGoldGained`
- `CardModel.Title` 直接属性（不再查 LocString）；`EncounterModel.CalculateGoldProportion`

### 0.105 → 0.106
- 新增枚举 `HpLossHookPhase`：`ModifyHpLostBeforeOsty`/`ModifyHpLostAfterOsty` 合并为 `ModifyHpLost(..., HpLossHookPhase, out IEnumerable<AbstractModel>)`；`ModifyDamageHookType.Cap` 新增
- 回合钩子全部加 `participants` 参数并改名：`BeforeSideTurnStart(choiceContext, side, participants, combatState)`、`BeforeSideTurnEnd(choiceContext, side, participants)` 等
- `AfterCardChangedPiles` 参数 `source` → `clonedBy`；`CardPileCmd.AddCurseToDeck` 返回 `Task<CardModel?>`、`AddCursesToDeck` 返回结果列表、`Add` 参数改 `clonedBy`；`OrbCmd.IncreaseBaseOrbCount` → `AddSlots`

### 0.103 → 0.105
- manifest 新增必填 `min_game_version`；依赖写法见第 1 节
- `ShowsInfiniteHp` → `HpDisplay` 枚举；`IsInstanced` → `PowerInstanceType` 枚举
- 效果执行函数（如 `PowerCmd.Apply`）需传 `PlayerChoiceContext`；找不到就 `new ThrowingPlayerChoiceContext()`
- `CardPileCmd.AddGeneratedCardToCombat` 等：`addedByPlayer(bool)` → `creator(Player?)`（false→null，true→`cardPlay.card.Owner` 或 `Owner`）
- `OnTurnEndInHand` 改 `protected virtual`；`GetResultPileType` → `GetResultPileTypeForCardPlay` + 新增 `GetResultPileTypeForOnTurnEndInHandEffect`；`BeforePlayPhaseStart(/_Late)` → `AfterAutoPrePlayPhaseEntered(/_Early/...Late)` + `AfterAutoPostPlayPhaseEntered`；`CombatState` 参数改 `ICombatState`

### 0.99 → 0.103（能量表盘）
- 结构 `BurstBack/BurstFront (CPUParticles2D)` → **`EnergyVfxBack/EnergyVfxFront (NParticlesContainer)`**；正式版加人物必须建这两个节点（加 % 唯一名）

---

## 7. 上传工坊（docs_11-upload-workshop.txt）

### 核心概念
- 官方上传器：`github.com/megacrit/sts2-mod-uploader`；`ModUploader.exe` 生成工作区 `NewModWorkspace`

### 流程
1. 工作区重命名；mod 文件（json/dll/pck）放进 **`Content`** 目录（**不压缩**）
2. `workspace.json`：**建议只留 `tags` 字段**（tags 在工坊改不了；其它字段填了上传时会覆盖工坊内容）；更新说明填 `changeNotes`
3. 预览图替换为工作区里固定文件名 **`image.jpg`**（**≤1MB**，否则无法上传）
4. 命令行：`ModUploader.exe upload -w <工作区名>`；更新同样命令，mod ID 自动从目录 `mod_id.txt` 读取
5. 建议封装成 bat/cmd/sh 或 CI 脚本自动化

### 注意
- `dependencies` 写对应项目的**工坊 ID，不加引号**（见上传器 README）
- 常用 tags：`Characters`、`QoL`、`Cards`、`Relics`、`schinese`（简体中文）、`English` 等
- 描述/changelog 建议直接删除后在工坊编辑；**不要忘记更改可见性**；json 里所有设置都会覆盖工坊内容（不写/删除则不覆盖）

---

## 8. 启动项参数（docs_08-launch-arguments.txt）

### 参数总览
| 参数 | 示例 | 作用 |
|---|---|---|
| `--autoslay` | `--autoslay` | 自动跑图打牌测试（发行版需 patch 才能用） |
| `--seed` | `--seed=abc123` | 给 autoslay 指定随机种子 |
| `--log-file` | `--log-file=C:\logs\autoslay.log` | autoslay 日志输出文件 |
| `--bootstrap` | `--bootstrap` | 启动后直接进入某场景 |
| `--fastmp` | `--fastmp=join` | 多人本地测试 |
| `--clientId` | `--clientId=2001` | 指定本地测试玩家 ID |
| `+connect_lobby` | `+connect_lobby 12345678901234567` | 按 Steam 大厅 ID 自动加入 |
| `--nomods` | `--nomods` | 不启用 mod 模式 |
| `--force-steam` | `--force-steam` / `=on` / `=off` | 强制开启/关闭 Steam 初始化 |
| `-log` | `-log Net Info` | 设置指定日志类型输出级别 |
| `-wpos` | `-wpos 100 200` | 窗口位置 |

### fastmp 取值
`host`（打开多人菜单）/ `host_standard` / `host_daily` / `host_custom` / `load`（加载本地多人存档）/ `join`（需 clientId）

### 注意
- 根目录建 `steam_appid.txt` 写 `2868840` 可绕过"非 steam 启动"提示；或加 `--force-steam=off`；打完一层保存问题 → 管理员模式运行 bat
- **`--autoslay` 与 `--bootstrap` 发行版不可用**，需 patch：
```csharp
[HarmonyPatch(typeof(NGame), nameof(NGame.IsReleaseGame))]
public static class NGamePatch
{
    public static void Postfix(ref bool __result) => __result = false;
}
// --bootstrap 还需 patch IBootstrapSettingsSubtypes.Get 以添加启动场景
```
