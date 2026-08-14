# STS2 Mod 开发单页速查（Jaina）

> 快速参考。详细笔记见 `docs/modding-memory/00-INDEX.md`（教程站全站 / RitsuLib 源码 / MinionLib 源码精华）。
> 游戏版本 0.111.0，RitsuLib 0.5.11，MinionLib 0.6.2（工坊）。API 变化以反编译 `data_sts2_windows_x86_64\sts2.dll` 为准。

## 项目与构建

- 入口：`[ModInitializer]` 类 `Entry`，Init 里必须：
  ```csharp
  RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);
  ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);  // 没有它 [RegisterXxx] 全部失效
  ```
- 构建：`dotnet build -c Debug`（csproj 已配置自动部署 dll/pck 到游戏 mods 目录）；改素材后需重新导出 pck（自动）。
- 调试：游戏内 `~` 控制台（`card <ID>`、`power <ID> 1 0` 仅战斗）；日志 `open logs`；本地联机 `--fastmp=host` / `--fastmp=join --clientId=1001`。
- 本地化：`jaina/localization/{zhs,eng}/*.json`，键 = `JAINA_CARD_<类名大写>` 等；**用 `tools/validate_json.gd` 验证**（Godot 跑），PowerShell ConvertFrom-Json 会误报含 `{IfUpgraded:show:...}` 的文件。

## 注册特性（RitsuLib 自动注册）

| 特性 | 标注于 | 说明 |
|---|---|---|
| `[RegisterCard(typeof(池))]` | 卡类 | 池：JainaCardPool / JainaNeutralCardPool（衍生） |
| `[RegisterCharacterStarterCard(typeof(Jaina), N)]` | 卡类 | 初始卡组第 N 张 |
| `[RegisterRelic(typeof(池))]` / `[RegisterCharacterStarterRelic(typeof(Jaina))]` | 遗物类 | 初始遗物 |
| `[RegisterPower]` / `[RegisterMonster]` / `[RegisterPotion]` | 对应类 | 能力/随从/药水 |
| `[RegisterOwnedCardKeyword("名", IconPath=...)]` | 关键词类 | 自定义关键词（悬停解释） |
| `[RegisterTouchOfOrobasRefinement(typeof(升级遗物))]` | 初始遗物 | 欧洛巴斯之触升级映射 |
| `[RegisterArchaicToothTranscendence(typeof(先古卡))]` | 初始卡 | 古老牙齿升级映射 |
| `[RegisterDustyTomeCard(typeof(角色))]` | 先古卡 | 尘封魔典候选 |
| `[RegisterCharacter]` | 人物类 | 新人物 |
| `[HarmonyPatch(...)]` | patch 类 | 游戏会扫描应用（MerchantPowerSlotPatch 先例） |

## 模板基类（继承这些，不是游戏 Model）

- `ModCardTemplate`（法术/技能）→ `JainaSpellCardTemplate`；`JainaMinionCardTemplate`（随从卡）
  - 构造：`(费用, CardType, CardRarity, TargetType, showInCardLibrary)`
  - 效果：`protected override async Task OnPlay(PlayerChoiceContext, CardPlay)`；升级：`OnUpgrade()`
  - `CanonicalVars`（DynamicVar：`DamageVar/BlockVar/CardsVar`）、`CanonicalKeywords`（关键词悬停）、`CustomPortraitPath`
  - 衍生卡：`CardRarity.Token` + 中立池
- `ModRelicTemplate`：`BeforeCombatStart()` 等生命周期；`CustomIconPath/Outline/Big`
- `ModPowerTemplate`/`PowerModel`：`BeforeDamageReceived`/`AfterCardPlayed` 等钩子；`Amount`、`PowerStackType.Counter`
- 随从：`JainaMinionBase : MinionModel`（见下）

## 常用命令 API

```csharp
await DamageCmd.Attack(n).FromCard(this, cardPlay).Targeting(cardPlay.Target!).WithHitFx("vfx/vfx_attack_blunt").Execute(ctx);
await CreatureCmd.Damage(ctx, [target], n, ValueProp.Unpowered, actor);   // 直接伤害
await CreatureCmd.GainBlock(creature, n, ValueProp.Move, null);
await CardPileCmd.Draw(ctx, n, player);
await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, owner);   // 衍生牌入局（先 MarkGenerated）
await PlayerCmd.GainEnergy(1m, owner);
await PowerCmd.Apply<T>(ctx, creature, amount, applier, source);
await PowerCmd.Remove(power);
var card = combatState.CreateCard(canonicalModel, owner);                 // 生成带 Owner 的实例（MutableClone 会 NRE）
```

## 随从要点（MinionLib）

- `JainaMinionBase : MinionModel`：`BaseAttackValue`、`BehaviorMode`（Manual 行动点 / Auto 回合末自动攻击）、`ActionsPerTurn`、`GenerateMoveStateMachine()`（意图状态机）
- 召唤：`MinionCmd.AddMinion<T>(ctx, player, options)`；战吼 `OnSummon`；死亡清理在 `AfterDamageReceivedLate`（亡语→移除 Powers→场面移除）
- 行动点：`JainaAttackAction : ActionModel`（`TargetType.AnyEnemy`、`DecrementAfterAct`、`OnAct` 攻击）
- 动态意图：`JainaConditionalAttackIntent`（包装 SingleAttackIntent + `Func<bool>` 条件）；状态变化后调 `RefreshIntentDisplay()`（`NCombatRoom.Instance.GetCreatureNode(creature).RefreshIntents()`）
- 目标类型：随从是 `Side=Player, IsPet` → 原生 `AnyEnemy` 选不中！用 `JainaTargetTypes.AnyTargetable`（任意活物含自己）或组合 `UnionTargetType(AnyEnemy, AnyMinion)`
- 布局：MinionLib 自动；`PetOwner` 指向主人 Player

## 常见坑

- **MutableClone 的卡无 Owner**：入牌堆/生成前用 `combatState.CreateCard(canonical, owner)`
- **商店黑屏**：攻击/能力槽候选为空 → 保证池里有对应稀有度的卡；能力槽缺卡用 `MerchantPowerSlotPatch` 转随从槽
- **`TryModifyEnergyCostInCombat`** 只改战斗内费用；图鉴/牌组显示 canonical 值
- **意图刷新**：UI 只在 `UpdateIntent`/`CombatStateChanged` 时重绘，逻辑改完要主动 `RefreshIntents()`
- **游戏 0.111 官方 bug**：`NetHostGameService.get_ConnectedPeers` MissingMethodException（与本 mod 无关，别排查）
- **存档/改名**：遗物/卡 Id 由类名生成，改名后旧存档条目失效（开发期无碍）
- **多人同步**：本地确定性逻辑（掷骰）用 `CombatState.RunState.Rng` 系，别用 `Random`
- 效果函数普遍需要 `PlayerChoiceContext`（无上下文传 `new ThrowingPlayerChoiceContext()`）

## 本项目文件地图

- `Scripts/Entry.cs`（入口；含 [JainaDiag] 临时诊断）
- `Scripts/Character/`：`Jaina.cs`（人物）、`JainaCardPool.cs`/`JainaNeutralCardPool.cs`/`JainaRelicPool.cs`、`JainaCastTracker.cs`（施放记录，罗曼斯/倒带用）
- `Scripts/Character/Cards/`：法术卡（Fireball/Frostbolt/Fireblast/ApexisBlast/FreezingPotion/FlameWard/...）、随从卡、`JainaTargetTypes.cs`、`JainaCardTypes.cs`（动态 Minion 类型）、`MerchantPowerSlotPatch.cs`
- `Scripts/Character/Minions/`：`JainaMinionBase.cs`、`JainaAttackAction.cs`、`JainaConditionalAttackIntent.cs`、各随从（Imp/Zealot/VolatileSkeleton/SpiritCollector/Rommath/Luna/Kalecgos/Antonidas/Mozaki/Varden/Aegwynn/SorcererApprentice/ArcaneArtificer/Renathal）、`JainaMinionPool.cs`（随机召唤）
- `Scripts/Character/Powers/`：FlameWardPower/FreezePower/EmpowerPower/AntonidasPower/KalecgosPower/SorcererApprenticePower/ArcaneArtificerPower/...
- `Scripts/Character/Relics/`：`EvenMatch.cs`（初始遗物）+ `EvenMatchAncient.cs`（先古升级版）
- `Scripts/Character/Keywords/JainaKeywords.cs`（13+1 自定义关键词）、`JainaCardTypes.cs`
