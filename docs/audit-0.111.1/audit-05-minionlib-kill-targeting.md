# MinionLib 审计（0.111.1）

审计对象：`E:\MOD\sts2\MinionLib\MinionLib`（补丁与核心交互点）对照 `E:\MOD\sts2\sts2\0.111.1\src`（3516 个 cs 文件）。
方法：对每个 Harmony patch 目标与运行期调用的游戏 API，在 0.111.1 反编译源码中核对存在性、签名、语义。
结论：**全部 40 处 `[HarmonyPatch]` 目标方法在 0.111.1 均存在**，无「缺失」级兼容性问题；发现 1 处中等严重度的新硬失败行为（`StartTargeting` 校验抛异常）、若干签名/可见性变化的脆弱点与行为差异，详见下表。

---

## Harmony Patch 目标核对结果表

| # | 补丁（mod 文件） | 目标方法 | 0.111.1 状态 | 0.111.1 证据 |
|---|---|---|---|---|
| 1 | MinionKillPatch.cs:15 | `CreatureCmd.KillWithoutCheckingWinCondition` | **签名变化**（`(Creature)` → `(Creature, bool force, int recursion=0)`，且变 private；按名绑定仍可命中，补丁可继续应用） | Core\Commands\CreatureCmd.cs:515 |
| 2 | ActionClickPatch.cs:15 | `NCreature._Ready` | OK | Core\Nodes\Combat\NCreature.cs:129 |
| 3 | ActionPowerIconClickPatch.cs:9 | `NPower._Ready` | OK | Core\Nodes\Combat\NPower.cs:64 |
| 4 | PersonalHivePowerPatch.cs:11 | `PersonalHivePower.AfterDamageReceived` | OK（参数名 `dealer` 仍匹配；0.111.1 官方仍只处理 Osty，补丁扩展逻辑仍必要） | Core\Models\Powers\PersonalHivePower.cs:23 |
| 5 | MinionInteractablePatch.cs:11 | `NCreature.ToggleIsInteractable(bool)` | OK | NCreature.cs:559 |
| 6 | MinionInteractablePatch.cs:23 | `NCombatRoom.AddCreature(Creature)` | OK | Core\Nodes\Rooms\NCombatRoom.cs:534 |
| 7 | CustomGlowColorPatch.cs:10 | `NHandCardHolder.UpdateCard()` | OK | Core\Nodes\Cards\Holders\NHandCardHolder.cs:360 |
| 8 | CustomGlowColorPatch.cs:24 | `NHandCardHolder.Flash()` | OK（场景节点名 `"Flash"` 仍在） | NHandCardHolder.cs:417、:141 |
| 9 | RelicRightClickPatch.cs:10 | `NRelic._Ready` | OK | Core\Nodes\Relics\NRelic.cs:70 |
| 10 | DescriptionPostProcessPatch.cs:13-22 | `CardModel.GetDescriptionForPile(PileType, DescriptionPreviewType, Creature)` | OK（private 3 参重载仍在；`DescriptionPreviewType` 为 private 内嵌枚举，AccessTools 可解析） | Core\Models\CardModel.cs:1372、:37 |
| 11 | CustomTargetTypePotionPatch.cs:33 | `NPotionHolder.UsePotion()` | OK（public async Task） | Core\Nodes\Potions\NPotionHolder.cs:285 |
| 12 | CustomTargetTypePotionPatch.cs:54 | `NPotionHolder.TargetNode(TargetType)` | OK（private async Task，反射可调） | NPotionHolder.cs:308 |
| 13 | CustomTargetTypePotionPatch.cs:66 | `NPotionPopup._Ready` | OK（`Potion` 属性变 private，反射仍可读） | Core\Nodes\Potions\NPotionPopup.cs:74、:44 |
| 14 | PowerRightClickPatch.cs:9 | `NPower._Ready` | OK | NPower.cs:64 |
| 15 | CustomTargetTypeCardPatch.cs:41 | `ActionTargetExtensions.IsSingleTarget(TargetType)` | OK | Core\Entities\Cards\ActionTargetExtensions.cs:5 |
| 16 | CustomTargetTypeCardPatch.cs:51 | `NTargetManager.AllowedToTargetCreature` | OK（变 private，Harmony 可 patch；`_validTargetsType` 字段名不变） | Core\Nodes\Combat\NTargetManager.cs:275、:52 |
| 17 | CustomTargetTypeCardPatch.cs:63 | `CardModel.IsValidTarget(Creature?)` | OK | Core\Models\CardModel.cs:1762 |
| 18 | CustomTargetTypeCardPatch.cs:78 | `NCardPlay.TryPlayCard(Creature?)` | OK（protected void；`CancelPlayCard`/`Cleanup(bool)`/`Finished` 信号齐备） | Core\Nodes\Combat\NCardPlay.cs:55、:125、:139、:28 |
| 19 | CustomTargetTypeCardPatch.cs:119 | `NCardPlay.ShowMultiCreatureTargetingVisuals()` | OK（protected；`Holder.CardNode`、`NCard.SetPreviewTarget/UpdateVisuals`、`CardPreviewMode.MultiCreatureTargeting` 均在） | NCardPlay.cs:186；Holders\NCardHolder.cs:49；NCard.cs:392/413；CardPreviewMode.cs:28 |
| 20 | CustomTargetTypeCardPatch.cs:146 | `NMouseCardPlay.MultiCreatureTargeting(TargetMode)` | OK（private async Task） | Core\Nodes\Combat\NMouseCardPlay.cs:269 |
| 21 | CustomTargetTypeCardPatch.cs:170 | `NControllerCardPlay.MultiCreatureTargeting()` | OK（private void） | Core\Nodes\Combat\NControllerCardPlay.cs:183 |
| 22 | CustomTargetTypeCardPatch.cs:192 | `NControllerCardPlay.SingleCreatureTargeting(TargetType)` | OK（private async Task） | NControllerCardPlay.cs:100 |
| 23 | PotionRightClickPatch.cs:10 | `NPotion._Ready` | OK | Core\Nodes\Potions\NPotion.cs:73 |
| 24 | CardRightClickPatch.cs:10 | `NPlayerHand.AddCardHolder(NHandCardHolder, int)` | OK（变 private，Harmony 可 patch；`Instance`/`InCardPlay`/`NCardHolder.Hitbox/CardModel` 均在） | Core\Nodes\Combat\NPlayerHand.cs:320、:132、:138；NCardHolder.cs:47/55 |
| 25 | NetFullCombatStateComponentsLogPatch.cs:9 | `NetFullCombatState.ToString()` | OK（transpiler 依赖的 `List<CardState>` foreach IL 结构仍在；若消失会硬抛异常） | Core\Entities\Multiplayer\NetFullCombatState.cs:537、:648 |
| 26 | StringIdPoolCollectorPatch.cs:8 | `AbstractModel.InitId(ModelId)` | OK | Core\Models\AbstractModel.cs:89 |
| 27 | BetterExtraArgsPatch.cs:14-23 | `CardModel.GetDescriptionForPile(PileType, DescriptionPreviewType, Creature)` + `AddExtraArgsToDescription(LocString)` | OK（transpiler 注入点 `AddExtraArgsToDescription` 调用在 0.111.1 仍位于目标方法内） | CardModel.cs:1372、:1376、:1545 |
| 28 | MinionGuardianOwnerDamageSuppressPatch.cs:7 | `Creature.LoseHpInternal(decimal, ValueProp)` | OK（返回 DamageResult） | Core\Entities\Creatures\Creature.cs:446 |
| 29 | MinionGuardianOverkillPatch.cs:13-15 | `CreatureCmd.Damage(PlayerChoiceContext, IEnumerable<Creature>, decimal, ValueProp, Creature, CardModel, CardPlay)` | OK（命中 0.111.1 的 `IEnumerable<Creature>?` 重载） | Core\Commands\CreatureCmd.cs:258 |
| 30 | ComponentDescriptionRawCachePatch.cs:16 | `CardModel.Description`（getter） | OK | CardModel.cs:127 |
| 31 | ComponentDescriptionRawCachePatch.cs:31 | `LocString.GetRawText()` | OK（`LocTable`/`LocEntryKey`/`Exists()` 均在） | Core\Localization\LocString.cs:83、:19、:22、:88 |
| 32 | ComponentDescriptionRawCachePatch.cs:45 | `LocManager.SetLanguage(string)` | OK | Core\Localization\LocManager.cs:335 |
| 33 | CardComponentDyanamicVarsUpdatePatch.cs:10 | `CardModel.UpdateDynamicVarPreview(CardPreviewMode, Creature?, DynamicVarSet)` | OK（补丁以 `object` 形参规避枚举类型绑定，按名匹配） | CardModel.cs:1452 |
| 34 | CardComponentDyanamicVarsUpdatePatch.cs:25 | `CardModel.FinalizeUpgradeInternal()` | OK | CardModel.cs:2142 |
| 35 | FrickYanoPatch.cs:7 | `CardModel.FromSerializable(SerializableCard)` | OK | CardModel.cs:2246 |
| 36 | MinionGuardianBlockToHpPatch.cs:11-12 | `CreatureCmd.GainBlock(Creature, decimal, ValueProp, CardPlay, bool)` | OK（命中 0.111.1 的 `CardPlay?` 重载） | CreatureCmd.cs:668 |

核心交互点（非 patch 目标）核对：

| 交互 API（mod 使用处） | 0.111.1 状态 | 证据 |
|---|---|---|
| `Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(ICombatState, Creature)` 投票机制（MinionKillPatch.cs:32） | **仍在，语义不变** | Core\Hooks\Hook.cs:2223 |
| `CombatManager.Instance.RemoveCreature(Creature)`（MinionKillPatch.cs:36） | OK | Core\Combat\CombatManager.cs:1365 |
| `ICombatState.RemoveCreature(Creature, bool unattach=true)`（MinionKillPatch.cs:37 单参调用） | OK | Core\Combat\ICombatState.cs:151 |
| `Creature.IsPet/PetOwner/Side/Monster/CombatState/CombatId(uint?)/Pets/GetPower<T>` | OK | Creature.cs:200/181/118/102/125/100/205/571 |
| `NCreature.Hitbox`（`Control`）`GuiInput` 信号、`NPower.GuiInput`、`NCardHolder.Hitbox` | OK | NCreature.cs:84；NPower.cs；NCardHolder.cs:47 |
| `NTargetManager.Instance/IsInSelection/LastTargetingFinishedFrame/SelectionFinished()/StartTargeting(×2)/CreatureHovered+CreatureUnhovered 信号` | OK（见兼容性问题 C1） | NTargetManager.cs:54/56/63/211/216/237/25-40 |
| `CardModel.GetDescriptionForPile`（public 2 参重载新增）、`CanPlayTargeting/TryManualPlay/TargetType/CombatState/Pile` | OK | CardModel.cs:1362/1696/1787/497/1018/356 |
| `CombatManager` 事件 `TurnStarted/TurnEnded/CombatSetUp/CombatEnded`（MinionHookInitializer.cs:16-19） | OK（签名 `Action<CombatState>`/`Action<CombatRoom>` 与订阅委托一致） | CombatManager.cs:273/278/245/255 |
| `GameAction` 基类：`OwnerId/ActionType/ToNetAction/ExecuteAction/Cancel`；`GameActionType.CombatPlayPhaseOnly`；`GameActionPlayerChoiceContext(GameAction)`；`INetAction.ToGameAction` | OK | Core\GameActions\GameAction.cs:48/54/215/201/203；GameActionType.cs:17；GameActionPlayerChoiceContext.cs:29；INetAction.cs:10 |
| `CombatState.GetCreature(uint?)/GetCreatureAsync(uint?, double)/Creatures/CurrentSide` | OK | Core\Combat\CombatState.cs:324/340/71/111 |
| `Player.PlayerCombatState.Pets`（PetOrderSnapshotManager.cs:16/32） | OK | Core\Entities\Players\PlayerCombatState.cs:27 |
| `PotionModel.EnqueueManualUse/CanThrowAtAlly/TargetType/Owner` | OK | Core\Models\PotionModel.cs:242/393/107/116 |
| `NMultiplayerPlayerStateContainer.LockNavigation/UnlockNavigation`、`NMultiplayerPlayerState.Player/Hitbox` | OK | NMultiplayerPlayerStateContainer.cs:96/108；NMultiplayerPlayerState.cs:104/102 |
| `RunManager.Instance.HoveredModelTracker.OnLocalPotionSelected/Deselected`、`ActionQueueSynchronizer.CombatState/RequestEnqueue`、`ActionSynchronizerCombatState.PlayPhase` | OK | RunManager.cs:198/204；HoveredModelTracker.cs:102/108；ActionQueueSynchronizer.cs:46/146 |
| `PacketWriter.WriteUInt/WriteBool/WriteModelEntry`、`PacketReader.ReadUInt/ReadBool/ReadModelIdAssumingType<T>` | OK | PacketWriter.cs:87/29；PacketWriterExtensions.cs:20；PacketReader.cs:82/25；PacketReaderExtensions.cs:19 |
| `PlayerCmd.AddPet<T>`（MinionCmd.cs:17） | OK | Core\Commands\PlayerCmd.cs:237 |
| `ModInitializerAttribute`（MainFile.cs:11） | OK | Core\Modding\ModInitializerAttribute.cs:11 |
| `PowerModel.Owner/Amount/StartPulsing/StopPulsing`；`Flash()`（protected，ActionModel 用 `new` 重暴露） | OK | Core\Models\PowerModel.cs:264/187/440/445/450 |

---

## 兼容性问题（中 × 2，低 × 6）

**中 C1** | `Targeting\Patches\CustomTargetTypeCardPatch.cs:222`、`CustomTargetTypePotionPatch.cs:114`、`Action\Patches\ActionClickPatch.cs:176/184` | 0.111.1 的 `NTargetManager.StartTargeting` 新增硬校验：`if (!validTargetsType.IsSingleTarget()) throw new InvalidOperationException(...)`。mod 当前流程对自定义多目标类型都绕开了 StartTargeting（单目标才进入），但任何把自定义多目标类型传入的路径（未来代码、第三方 ICustomTargetType 消费者）都会**运行期抛 InvalidOperationException**。这是 0.111.1 的新行为，旧版本无此校验。 | `Core\Nodes\Combat\NTargetManager.cs:218-221`（Vector2 重载）、`:239-242`（Control 重载）；`Core\Entities\Cards\ActionTargetExtensions.cs:5-18`（未知枚举值 default → false） | 在所有 `StartTargeting` 调用前显式断言/短路：`customType.IsSingleTarget` 为 false 时禁止进入 StartTargeting；并建议对自定义 TargetType 的 `IsSingleTarget()` 结果做防御性再确认（patch 后置钩子依赖加载顺序）。

**中 C2** | `Minion\Patches\MinionKillPatch.cs:15-22` | 目标 `CreatureCmd.KillWithoutCheckingWinCondition` 签名从 `(Creature)` 变为 `(Creature creature, bool force, int recursion = 0)` 且由 public 变 **private**。Harmony 按参数名 `creature` 绑定仍可命中、补丁仍生效（无 MissingMethodException），但该目标是私有异步方法，随版本迭代被改名/改签名的风险高，且 `force/recursion` 语义与 0.111.1 内部重入（CreatureCmd.cs:603 递归调用）叠加时，postfix 链式等待的时序需要回归验证。 | `Core\Commands\CreatureCmd.cs:515`（private static async Task）、`:470`、`:603`（递归） | 保持现状可运行；建议改用 `AccessTools.Method` + 反射兜底（当 string 名找不到时报更友好的错），并在每次升级后回归「友方随从死亡清理」。

**低 C3** | `Action\Patches\ActionClickPatch.cs:48` | `targetManager.LastTargetingFinishedFrame == actorNode.GetTree().GetFrame()`：0.111.1 中 `LastTargetingFinishedFrame` 为 `long`（旧版可能为 ulong），而 Godot `SceneTree.GetFrame()` 返回 `ulong`——若用 0.111.1 程序集重新编译，`long == ulong` 无运算符（CS0019）将**编译失败**；预编译二进制运行期不受影响。 | `Core\Nodes\Combat\NTargetManager.cs:63`（`public long LastTargetingFinishedFrame`） | 改为显式比较：`(ulong)targetManager.LastTargetingFinishedFrame == actorNode.GetTree().GetFrame()` 或统一转为 long。

**低 C4** | `Targeting\Patches\CustomTargetTypeCardPatch.cs:51-61` | `NTargetManager.AllowedToTargetCreature` 0.111.1 为 **private**（旧版可能 public）；Harmony 仍可 patch，但属于脆弱的私有成员依赖，且字段 `_validTargetsType` 仍存在（FieldRefAccess 可用）。 | `Core\Nodes\Combat\NTargetManager.cs:275`、`:52` | 维持现状；留意 `AllowedToTargetNode`（:258）为唯一调用方，若其重构为 inline 判断则补丁失效。

**低 C5** | `RightClick\Patches\CardRightClickPatch.cs:10-24` | `NPlayerHand.AddCardHolder` 0.111.1 为 **private**（旧版可能非 private）；postfix 形参只声明 `holder`，与 `(NHandCardHolder, int)` 按名绑定成功。 | `Core\Nodes\Combat\NPlayerHand.cs:320` | 维持现状；若未来 AddCardHolder 内联进 `Add()` 需改挂点。

**低 C6** | `Component\Patches\NetFullCombatStateComponentsLogPatch.cs:17-93` | transpiler 对 `NetFullCombatState.ToString()` 的 IL 结构做精确匹配（`List<NetFullCombatState.CardState>.Enumerator` Current→stloc→MoveNext），**匹配失败会硬抛 `Exception("Transpiler 失败: ...")`** 使该 patch 整体失败。0.111.1 的 ToString 仍含 `foreach (CardState card in pile.cards)`（:648），当前可匹配。 | `Core\Entities\Multiplayer\NetFullCombatState.cs:537-782`（尤其 :648-685） | 每次版本升级回归；建议把硬抛改为「匹配失败仅告警跳过」，避免 patch 失败影响整个 mod 加载。

**低 C7** | `Targeting\Patches\CustomTargetTypeCardPatch.cs:111-112` | 补丁在 `TryPlayCardPrefix` 成功路径先调 `Cleanup(true)`（0.111.1 中 `Cleanup` 已 `EmitSignal(SignalName.Finished, true)`，NCardPlay.cs:145），随后又显式 `EmitSignal(Finished, true)` → **Finished 信号双发**，接收方（如 NPlayerHand 的卡片播放清理逻辑）可能执行两次。 | `Core\Nodes\Combat\NCardPlay.cs:139-148` | 删除补丁内显式 `EmitSignal`（Cleanup 已发），或保留其一并确认下游幂等。

**低 C8** | `Minion\Patches\MinionKillPatch.cs:36-37` | 0.111.1 官方对自己（敌方）的移除新增 `monster != null && !monster.IsPerformingMove` 门控（CreatureCmd.cs:559-563），补丁对友方随从的 `CombatState.RemoveCreature` 无此门控——若随从在招式执行中途死亡，可能与移动状态机竞争。概率极低，属于语义差异。 | `Core\Commands\CreatureCmd.cs:556-564` | 移除前追加 `!(creature.Monster?.IsPerformingMove ?? false)` 检查，与官方门控对齐。

---

## 潜在 Bug / 行为差异（mod 逻辑 vs 0.111.1 语义）

1. **MinionKillPatch 仍然必要，且语义符合预期**：0.111.1 的 `KillWithoutCheckingWinCondition` 死亡移除仍仅限 `Side == Enemy`（CreatureCmd.cs:556），`PlayerCombatState.OnPetDied` 只把宠物移出 `_pets` 注册表、**不移除 CombatManager/CombatState**（PlayerCombatState.cs:281-292）。因此友方随从残留问题在 0.111.1 依然存在，补丁的清理职责未被官方替代——这是「行为差异确认」，不是新问题。
2. **自定义 TargetType 依赖 patch 后的 `IsSingleTarget`**：游戏 `IsSingleTarget`（ActionTargetExtensions.cs:5-18）对未知枚举 default 返回 false；mod 的 hash 型自定义 TargetType（`CustomTargetTypeManager.cs:19`，`Calculate32BitHash`）必须依赖 IsSingleTargetPostfix 生效。若该 patch 应用失败（Harmony 顺序、方法改名），自定义目标卡片/药水会全部走多目标分支。建议初始化时自检。
3. **`ShowMultiCreatureTargetingVisuals` 语义**：0.111.1 原版对 `AllEnemies/AllAllies/Self/Osty` 分支做预览（NCardPlay.cs:186-237），自定义类型落到 default 无操作；补丁 postfix 为自定义类型补 reticle——与官方行为互补，无冲突。
4. **`DescriptionPostProcessPatch` 的 `int previewType` 形参**：目标方法第 2 参是私有枚举 `DescriptionPreviewType`（CardModel.cs:37，None=0/Upgrade=1），Harmony 按名绑定后经 `unbox.any int32` 传入——枚举底层为 int 时运行期合法；mod 侧自己的枚举值与之逐一对齐（None/Upgrade）。若官方枚举未来增加值或改底层类型，需同步。
5. **`BetterExtraArgsPatch` transpiler 注入点在 `AddExtraArgsToDescription` 调用之后**：0.111.1 该方法在 `DynamicVars.AddTo(description)` 之后调用（CardModel.cs:1375-1376），注入的 helper 读取的 `description` 是已含动态变量的 LocString——语义与旧版一致，无差异。

## 改进机会（0.111.1 新 API 可替代/增强现有实现）

1. **官方原生支持 mod 的 INetAction 子类型**：`ActionTypes.Initialize()` 通过 `ReflectionHelper.GetSubtypesInMods<INetAction>()` 自动收集 mod 程序集内的 `INetAction` 实现（ActionTypes.cs:14-17、ReflectionHelper.cs:60），mod 的 `NetExecuteCreatureActionGameAction`（public struct）会被自动注册，无需任何额外接入——这是 0.111.1 的新机制，可去掉任何隐式假设。
2. **`CardModel.GetDescriptionForPile(PileType, Creature?)` 公开 2 参重载**（CardModel.cs:1362）：对不需要预览枚举的普通描述后处理（如仅按目标重写），可改挂公开重载，降低对 private 3 参方法 + 私有枚举的反射依赖。
3. **`StartTargeting` 新校验可作自检**：利用 0.111.1 的抛异常行为，在 mod 初始化/调试模式下断言所有已注册自定义 TargetType 的 `IsSingleTarget` 值符合预期，提前暴露注册错误。
4. **`PlayerCombatState.OnPetDied`（PlayerCombatState.cs:281-292）**：官方已把「死亡宠物从 `_pets` 移除」纳入宠物生命周期；MinionKillPatch 可考虑同步订阅 `pet.Died`（或复用该移除点）使 CombatManager/CombatState 清理与官方生命周期钩子统一，避免两套移除逻辑时序竞态。

## 结论摘要

1. 全部 40 处 Harmony patch 目标在 0.111.1 均存在且按名绑定可用，**无缺失级问题**，mod 可加载运行；核心投票机制 `Hook.ShouldCreatureBeRemovedFromCombatAfterDeath`、`CombatManager.RemoveCreature`、`ICombatState.RemoveCreature` 均原样保留。
2. 最值得关注的是 0.111.1 新增的 `StartTargeting` 单目标校验（C1）：当前 mod 流程恰好绕开，但属于新硬失败路径，需防未来踩雷。
3. `KillWithoutCheckingWinCondition` 签名/可见性变化（C2）及 `LastTargetingFinishedFrame` 类型（C3）是重新编译时的两个具体风险点，运行期不受影响。
4. 0.111.1 官方仍未处理友方随从死亡后的 CombatManager/CombatState 清理，MinionKillPatch 的修复职责依然成立。
5. 建议：修复 C7 的 Finished 双发、给 C6 的 transpiler 加软失败、按 C8 对齐 `IsPerformingMove` 门控，并在下次升级时回归验证本表。
