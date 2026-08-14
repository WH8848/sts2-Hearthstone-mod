# 支持系统（JainaCastTracker / 罗曼斯重放 / Powers 钩子 / 关键词 / 动态 CardType / 手牌机制）审计（0.111.1）

审计范围：`JainaCastTracker.cs`、`JainaDiscoverHelper.cs`、`RommathMinion.cs`、全部 `Powers/*.cs`、`JainaKeywords.cs`、`JainaCardTypes.cs`、`JainaCardTypePatches.cs`、`JainaMinionBase.cs`、`LunaMinion.cs`、`JainaMinionPool.cs`、`JainaMinionTooltip.cs`、`MerchantPowerSlotPatch.cs`、`EvenMatch.cs`、`EvenMatchAncient.cs`、`Fireblast.cs` 等；对照反编译源码 `E:\MOD\sts2\sts2\0.111.1\src`（3545 个 cs）。

结论先行：**所有被调用的游戏 API 在 0.111.1 均存在且签名一致**（含全部 Power 钩子、CardCmd.AutoPlay、ICombatState.CreateCard/Creatures、CardPileCmd.AddGeneratedCardToCombat、CardSelectCmd.FromChooseACardScreen、HoverTipFactory.FromKeyword、ModInitializerAttribute、CardFactory.CreateForMerchant、MoveState/MonsterMoveStateMachine/AbstractIntent 等），无预期的 MissingMethodException。主要风险集中在**手牌上限 10 的静默弃牌交互**与**ObjectionPower 预览消费竞态**两处行为问题，以及动态 CardType 对 RitsuLib 版本的依赖。

---

## 兼容性问题（高 × 1，中 × 3，低 × 3）

**高** | `Scripts/Character/Cards/Fireblast.cs:113-123`、`Scripts/Character/Cards/JainaDiscoverHelper.cs:63-72`、`Scripts/Character/Powers/AntonidasPower.cs:37-42`、`Scripts/Character/Relics/EvenMatch.cs:51-60`、`EvenMatchAncient.cs:50-61` | 手牌上限 10：**满手时 `CardPileCmd.Add` 将牌静默改道弃牌堆**（不再入手），且玩家无感知（只有 "HAND_FULL" 气泡）。火焰冲击"每回合开始自动入手"在满手回合失效并弹提示；发现三选一选中的牌、安东尼达斯火球、幸运币在满手时直接进弃牌堆，玩家会"看到选中的牌消失"。 | 0.111.1 明确存在手牌上限 `CardPile.MaxCardsInHand => 10`（`src/Core/Entities/Cards/CardPile.cs:21`）；满手重定向逻辑 `bool isFullHandAdd = cardPile.Type == PileType.Hand && cardPile.Cards.Count >= CardPile.MaxCardsInHand; if (isFullHandAdd) cardPile = CardPile.Get(PileType.Discard, card.Owner);`（`src/Core/Commands/CardPileCmd.cs:484-488`，气泡在 517-520）；回合开始抽牌同样被上限截断 `Math.Min(handDraw, CardPile.MaxCardsInHand)`（`src/Core/Combat/CombatManager.cs:922`）。 | 加牌前检查手牌空间：`if (PileType.Hand.GetPile(player).Cards.Count >= CardPile.MaxCardsInHand) return;`（Fireblast）/ 满手时发现改为"改为不入手并提示"或让玩家知道会进弃牌堆；至少在所有 `AddGeneratedCardToCombat(...PileType.Hand...)` 前做容量检查。

**中** | `Scripts/Character/Cards/JainaCardTypes.cs:22-29` + `JainaCardTypePatches.cs` 全部 | 动态 `CardType.Minion` 的兼容性取决于 RitsuLib `DynamicEnumValueRegistry` 与 0.111.1 的配合：0.111.1 的 `CardType` 为封闭枚举且已含 7 个成员（`None,Attack,Skill,Power,Status,Curse,Quest`），动态值编号、存档/联机序列化稳定性完全依赖 RitsuLib 实现，游戏源码无法验证。游戏侧 6 处 `switch (CardType)` 中仅 4 处被补丁覆盖（ToLocString / FramePath / PortraitBorderPath / AncientTextBgPath），其余 2 处（NTinyCard 有 default 分支、MadScience 自包含类型）对动态值安全。 | `src/Core/Entities/Cards/CardType.cs:3-12`（枚举成员）；6 处 switch：`CardTypeExtensions.cs:9`、`CardModel.cs:164/195/226`、`src/Core/Nodes/Cards/NTinyCard.cs:53`（含 default，安全）、`src/Core/Models/Cards/MadScience.cs:179-207`（仅处理自身 `TinkerTimeType`，安全）。 | 升级时核对 RitsuLib 版本对 0.111.1 的 `CardType` 动态注册支持；建议在 `Entry.Init` 中加运行时自检（注册后断言 `JainaCardTypes.Minion != CardType.None` 且 `!Enum.IsDefined(typeof(CardType), Minion)`），并保留 4 个 Harmony 补丁。

**中** | `Scripts/Character/Minions/RommathMinion.cs:49-53`（同型问题见 `JainaMinionPool.cs:156,171`、`JainaMinionBase.cs:161-166`） | `ModelDb.GetById<CardModel>(...)` 的 `canonical == null` 守卫是**死代码**：0.111.1 中 `GetById<T>` 找不到即抛 `ModelNotFoundException`（`GetByIdOrNull` 才返回 null）。一旦 `GeneratedAttackSkills` 中出现任何未注册进 ModelDb 的类型（如注册时序问题、类型改名），罗曼斯战吼会直接抛异常中断整个战吼链。 | `src/Core/Models/ModelDb.cs:549-553`（`GetById<T>` 抛 `ModelNotFoundException`）；`ModelDb.cs:540-547`（`GetByIdOrNull<T>` 可空）。 | 全部改为 `ModelDb.GetByIdOrNull<CardModel>(...)`，保留 null 守卫（RommathMinion.cs:50、JainaMinionPool.cs:156、JainaMinionBase.cs:161 已写好的守卫立即生效）。

**中** | `Scripts/Character/Powers/ObjectionPower.cs:35-43`（配合 48-55） | 见"潜在 Bug"第 1 条：0.111.1 中 `ModifyDamageAdditive` 在**意图预览与结算两个阶段都会调用**，当前实现无法区分，导致拦截可能被非攻击伤害提前消耗。 | `src/Core/Hooks/Hook.cs:1495`（`Hook.ModifyDamage` 带 `CardPreviewMode`）、`src/Core/Models/AbstractModel.cs:1575-1577`（cardPlay 仅在实际打出时非 null）。 | 见建议修复（用 `cardPlay != null` 区分结算阶段）。

**低** | `Scripts/Character/Cards/JainaDiscoverHelper.cs:49`（同型：`RommathMinion.cs:68`、`JainaMinionBase.cs:406`） | 发现/随机选择使用 `RunState.Rng.CombatTargets` 掷骰，而 0.111.1 官方卡牌随机生成使用独立的 `CombatCardGeneration` 流；混用会污染战斗目标随机流，破坏种子/录像复现（联机同步时双方 RNG 消耗可能不一致）。 | `src/Core/Runs/RunRngSet.cs:72`（`CombatTargets`）、`src/Core/Factories/CardFactory.cs:119-150`（`GetDistinctForCombat` 用 `CombatCardGeneration` 掷骰选卡）。 | 选卡场景改用 `player.RunState.Rng.CombatCardGeneration.NextItem(...)`；随从/敌人目标选择保留 `CombatTargets`（语义正确）。

**低** | `Scripts/Character/Keywords/JainaKeywords.cs:15-30` | 自定义关键词（Deathrattle/Charge/Freeze/...）由 RitsuLib 注册为动态 `CardKeyword` 值；0.111.1 的 `HoverTipFactory.FromKeyword` 按 `keyword.ToString()` 生成 key 查 `card_keywords` 本地化表（`internal` 扩展类 `CardKeywordExtensions`），动态枚举值的 `ToString()` 名称必须与 RitsuLib 注册名、mod 提供的本地化条目一致，否则悬停提示为空（不崩溃）。 | `src/Core/HoverTips/HoverTipFactory.cs:36-43`；`src/Core/Entities/Cards/CardKeywordExtensions.cs:6-23`（internal、`Slugify(keyword.ToString())` 查表）。 | 确认 RitsuLib 对 0.111.1 `CardKeyword`（8 成员：None/Exhaust/Ethereal/Innate/Unplayable/Retain/Sly/Eternal，`CardKeyword.cs:18-28`）动态注册的名称生成规则，并随包提供全部 13 个关键词的 `card_keywords` 本地化条目。

**低** | `Scripts/Character/Cards/JainaCardTypePatches.cs:36-82` | 被补丁的 `CardModel.FramePath` / `PortraitBorderPath` / `AncientTextBgPath` 在 0.111.1 中为 **private getter**（`CardModel.cs:159/190/217`）；Harmony `[HarmonyPatch(typeof(CardModel), "FramePath", MethodType.Getter)]` 按名称解析私有属性 getter 仍可命中，当前版本兼容，但依赖私有成员名——未来版本改名/删除会静默失效（补丁不报错、动态随从卡显示异常）。 | `src/Core/Models/CardModel.cs:159-245`（三个 private getter 内 switch 对动态值抛 `ArgumentOutOfRangeException`，正是补丁存在的理由）。 | 维持补丁并在升级时回归测试；可考虑向官方反馈为动态类型提供公开扩展点。

---

## 潜在 Bug / 行为差异（mod 逻辑 vs 0.111.1 语义）

1. **ObjectionPower 预览消费竞态（中）**：`ModifyDamageAdditive` 在 0.111.1 中既被意图预览调用也被实际伤害结算调用（`Hook.cs:1495` 带 `CardPreviewMode`；`AbstractModel.cs:1575-1577` 明确 cardPlay 仅结算时非 null）。当前实现（`ObjectionPower.cs:37-39`）在预览阶段即置 `_consumed = true`，此后**任意**敌人伤害（其它敌人先手攻击、敌人卡牌伤害）结算触发 `AfterDamageReceived`（`ObjectionPower.cs:48-54`）就会把 Power 移除，真正的攻击意图不再被拦截。建议：标记条件加 `cardPlay != null`（预览时 cardPlay 为 null），并在 `AfterDamageReceived` 校验 `result` 的伤害确实来自被拦截的那次攻击（或直接改在 `ModifyDamageAdditive` 命中且 `cardPlay != null` 时异步移除）。

2. **LunaMinion 手牌快照与满手重定向（低）**：满手时生成的牌被 `Add` 重定向到弃牌堆（`CardPileCmd.cs:484-488`），但 `Hook.AfterCardGeneratedForCombat` 仍会触发（`CardPileCmd.cs:311`），`LunaMinion.cs:79-86` 会把**弃牌堆中的卡** append 到共享快照末尾 → 该回合 `IsRightmost` 判定失效（真实最右卡 index != Count-1），露娜当回合不抽牌。建议在 append 前检查 `card.Pile?.Type == PileType.Hand`。

3. **RommathMinion 对 `TargetType.AnyPlayer` 的处理（低）**：0.111.1 中 `AnyPlayer` 在单人模式"不进行目标选择"（`TargetType.cs:14-19`），而 `CardCmd.AutoPlay` 只对 `AnyEnemy`/`AnyAlly` 在 target 为 null 时自动补目标（`CardCmd.cs:73-97`）。当前发现/生成池内无 AnyPlayer 卡，但若未来加入，罗曼斯 `RommathMinion.cs:60-73` 会把 null 目标传给 AutoPlay，卡片可能被当作无目标卡打出而失效。建议对 `AnyPlayer` 显式挑选目标或跳过。

4. **重放与记录的相互影响（低）**：罗曼斯 AutoPlay 重放的卡会再次走 `OnPlay` → `RecordPlayed`（`JainaCastTracker.cs:68`），使重放的生成牌也进入 `PlayedAttackSkills`（倒带会认为"施放过"）；0.111.1 的 Replay 机制（`PlayIndex`/`PlayCount`，`CardPlay.cs:46-66`）下同一卡多次施放会重复调用 OnPlay，因 HashSet 去重无副作用。语义上可接受，但与"仅手打"直觉略有出入，注意文档表述。

5. **FreezePower 减伤覆盖面（低）**：`ModifyDamageMultiplicative`（`FreezePower.cs:35-46`）对冻结者所有 `IsPoweredAttack()` 伤害生效，含敌人卡牌/技能伤害，不仅"攻击"；0.111.1 中 `IsPoweredAttack()`（`ValuePropExtensions.cs:5`）覆盖所有力量类伤害（与 Weak/Strength 同语义），与"攻击造成的伤害"描述有轻微出入，若需严格只减攻击可加 `cardSource == null` 或 `cardPlay == null` 条件。

6. **`ModifyHpLostBeforeOstyLate` 语义核对（无差异）**：0.111.1 中该钩子在"格挡结算后、Osty 转移前"运行（`AbstractModel.cs:1688-1725`）；随从受击时 `PetOwner` 仅重定向**格挡部分**到主人护甲，未格挡伤害直接扣随从 HP（`CreatureCmd.cs:286-307`）。`MinionSquadPower.cs:29-71` 只拦截 `target == Owner` 的本体伤害，与"随从被打不触发挡伤转移"的语义一致，无问题。`IceBarrierPower`/`FlameWardPower` 的 `target.PetOwner?.Creature == Owner` 判定在 0.111.1 的 `BeforeDamageReceived`（`CreatureCmd.cs:285` 传 originalTarget=随从）下正确触发，无差异。

---

## 改进机会（0.111.1 新 API 可替代/增强现有实现）

1. **集中化生成追踪**：`Hook.AfterCardGeneratedForCombat(ICombatState, CardModel, Player?)`（`Hook.cs:251`，由 `CardPileCmd.cs:311` 对**所有**生成牌触发）可以替代 `JainaCastTracker.MarkGenerated` 的 12 个散点调用（`JainaDiscoverHelper.cs:68`、`AntonidasPower.cs:40`、`Rewind.cs:82`、`Trick.cs:69-84`、`UnfairGame.cs:88`、`FreezingPotion.cs:93`、`EvenMatch.cs:58`、`EvenMatchAncient.cs:56`、`SpiritCollectorCard.cs:49` 等）——用一个隐藏 `[RegisterPower]` 挂在玩家身上监听该钩子，任何未来新增的生成牌自动进入罗曼斯重放池，不再依赖逐个调用点。
2. **ObjectionPower 用 `cardPlay != null` 区分预览/结算**（`AbstractModel.cs:1575-1577` 文档已明确该约定），消除预览误消耗。
3. **手牌容量 API**：`CardPile.MaxCardsInHand`（`CardPile.cs:21`）+ `PileType.Hand.GetPile(player).Cards.Count` 在生成/自动入手前做容量检查，规避满手静默弃牌。
4. **费用修改替代**：KalecgosPower / SorcererApprenticePower 可改用 `CardEnergyCost.SetThisTurnOrUntilPlayed(int, bool)` / `AddThisTurn(int, bool)`（`CardEnergyCost.cs:197/300`，0.111.1 新增的本地费用修饰系统）替代手写 `TryModifyEnergyCostInCombat`；hook 方式与游戏原生 `Hook.ModifyEnergyCostInCombat`（`Hook.cs:1583`，decimal 签名与 mod 完全一致）也兼容，保留亦可。
5. **掷骰流规范化**：发现选卡改用 `RunState.Rng.CombatCardGeneration`（与 `CardFactory.GetDistinctForCombat` 一致，`CardFactory.cs:119`），提升种子/联机复现性。
6. **存在性检查**：统一用 `ModelDb.GetByIdOrNull<T>`（`ModelDb.cs:540`）替代 `GetById<T>` + null 守卫，避免 `ModelNotFoundException`。
7. **重放目标选择简化**：罗曼斯随机目标可用游戏原生 `Rng.CombatTargets.NextItem`（已用）或 `AttackCommand.TargetingRandomOpponents`（`AttackCommand.cs:322`）重构，减少手写目标池逻辑。

---

## 结论摘要

1. 0.111.1 反编译源码中，mod 使用的全部钩子与命令 API 均存在且签名一致（Power 钩子：AfterCardPlayed/BeforeDamageReceived/AfterDamageReceived/TryModifyEnergyCostInCombat/BeforeSideTurnStart/BeforeSideTurnEnd/BeforeCardPlayed/AfterCardDrawn/AfterCardGeneratedForCombat/ModifyDamage*/ModifyHpLostBeforeOstyLate/TryModifyPowerAmountReceived/ModifyBlockAdditive/BeforeApplied 等全部核对通过），无 MissingMethodException 风险。
2. 唯一的高影响行为问题是 0.111.1 新增的手牌上限（`MaxCardsInHand = 10`）导致满手时生成牌/火焰冲击被静默改道弃牌堆，需在 Fireblast、发现、安东尼达斯、幸运币等加牌点前做容量检查。
3. 动态 `CardType.Minion` 在 0.111.1 的 4 处补丁目标（ToLocString、FramePath、PortraitBorderPath、AncientTextBgPath）均存在（其中 3 处已变为 private，Harmony 仍可命中），其余 switch 点对动态值安全；风险收敛于 RitsuLib 对 0.111.1 的动态枚举支持，需按库版本确认。
4. `ModelDb.GetById` 的 null 守卫为死代码（0.111.1 抛异常），建议全局改用 `GetByIdOrNull` 提升健壮性；ObjectionPower 的预览消费竞态建议按 `cardPlay != null` 区分阶段修复。
5. 综合评级：兼容性良好（无阻断项），建议优先处理手牌上限交互、ObjectionPower 竞态与 GetByIdOrNull 三处，并利用 0.111.1 的 `AfterCardGeneratedForCombat` 全局钩子重构生成追踪。
