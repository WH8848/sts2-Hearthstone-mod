# MinionLib 0.6.2 — Targeting（自定义目标选择系统）源码精读

源码根：`E:\MOD\sts2\MinionLib\MinionLib\Targeting\`（19 个 .cs，全部精读）。目标选择的核心思路：**给游戏原生 `TargetType`（枚举）注入"自定义目标判定器"（ICustomTargetType），再用 Harmony 前缀/后缀 patch 接管卡片与药水的整条选择链路**。

---

## 一、公共类型清单（命名空间 MinionLib.Targeting）

### 1. `ICustomTargetType`（接口，`Targeting\ICustomTargetType.cs`）
自定义目标判定器契约。全部判定最终收敛到"某个 Creature 是否可选"。
- `bool IsSingleTarget { get; }` — 单目标（选 1 个生物）还是多目标（不选/全选）
- `bool IsValidTargetPreview(Creature target)` — 预览/悬停高亮用
- `bool IsValidTarget(CardModel card, Creature target)`
- `bool IsValidTarget(PotionModel potion, Creature target)`
- `bool IsValidTarget(ActionModel action, Creature target)`

### 2. `CustomTargetType`（抽象基类，`Targeting\CustomTargetType.cs`）
便捷基类：Card/Potion/Action 三个重载默认**转发到纯 Creature 判定**，实现类只需覆写一个方法。
- `abstract bool IsSingleTarget { get; }`
- `bool IsValidTargetPreview(Creature)` → 转调 `IsValidTarget(target)`
- `virtual bool IsValidTarget(CardModel, Creature)` / `(PotionModel, Creature)` / `(ActionModel, Creature)` → 默认 `IsValidTarget(Creature)`
- `protected abstract bool IsValidTarget(Creature target)`

### 3. `CustomTargetTypeManager`（静态类，`Targeting\CustomTargetTypeManager.cs`）
注册中心。把自定义类型变成一个 `TargetType` 枚举值并登记。
- `TargetType Register(ICustomTargetType customTargetType, string @namespace, string name)` — 用 **32 位 FNV-1a 哈希** `"{namespace}.{name}"` 强转成 `TargetType`，登记后返回
- `TargetType Register(ICustomTargetType customTargetType, [CallerArgumentExpression] string expr = "")` — 自动重载：从调用栈 `StackFrame(1)` 取声明类型命名空间 + 调用表达式去空白做名字（标了 `[MethodImpl(NoInlining)]`）
- `bool IsCustomTargetType(TargetType targetType)` — 是否注册过的自定义类型
- `bool TryGetCustomTargetType(TargetType targetType, out ICustomTargetType customTargetType, bool includeBuiltin = true)` — `includeBuiltin:false` 时只查自定义注册表（patch 均用 false）
- 内部状态：`HashSet<TargetType> RegisteredCustomTypes`；`Dictionary<TargetType, ICustomTargetType> CustomTypeDefinitions`（用 `BuiltInTargetType.All` 预填，即**内置 TargetType 也走同一字典分派**）

### 4. `MinionTargetTypes`（静态类，`Targeting\MinionTargetTypes.cs`）
预注册好的随从目标类型常量（注册命名空间固定为 `"MinionLib"`）：
| 字段 | 类型 | 含义 |
|---|---|---|
| `TargetType AnyMinion` | 单 | 任意随从 |
| `TargetType AllMinions` | 多 | 所有随从 |
| `TargetType Itself` | 单 | 仅 Action 场景可用（target == action.Owner） |
| `TargetType AnyCreature` | 单 | 任意存活生物 |
| `TargetType AllCreatures` | 多 | 所有存活生物 |
| `TargetType AnyMinionOrOwner` | 单 | 随从或其主人 |
| `TargetType Void` | 单 | 恒不可选（占位） |

### 5. Pets 目录 `Targeting\Pets\`（具体判定器实现）
- `AnyMinionTargetType`（`AnyMinionTargetType.cs`）— `IsSingleTarget=true`。有效目标：`IsAlive && Side==CombatSide.Player && IsPet && Monster is MinionModel`，且归属匹配：Card→`target.PetOwner == card.Owner`；Potion→`== potion.Owner`；Action→`== actor.PetOwner || == actor.Player`；Preview→`LocalContext.IsMe(target.PetOwner)`（只认本地玩家）
- `AllMinionsTargetType`（`AllMinionsTargetType.cs`）— 判定与 AnyMinion 完全相同，仅 `IsSingleTarget=false`
- `AnyMinionOrOwnerTargetType`（`AnyMinionOrOwnerTargetType.cs`）— `IsSingleTarget=true`。有效：`IsAlive && (IsPlayer || (Side==Player && IsPet && Monster is MinionModel))`；归属：`target.PetOwner == owner || target.Player == owner`；Action 版本比较 actor 与 target 的 Player/PetOwner 四组排列；Preview 用 `LocalContext.IsMe(target) || LocalContext.IsMe(target.PetOwner)`
- `AnyCreatureTargetType`（`AnyCreatureTargetType.cs`）— `IsSingleTarget=true`，仅 `target.IsAlive`
- `AllCreaturesTargetType`（`AllCreaturesTargetType.cs`）— `IsSingleTarget=false`，仅 `target.IsAlive`
- `ItselfTargetType`（`ItselfTargetType.cs`）— `IsSingleTarget=true`；纯 Creature 判定**恒 false**，仅 Action 重载返回 `target == action.Owner`（即"自己"只在随从行动脚本里可作目标）
- `VoidTargetType`（`VoidTargetType.cs`）— `IsSingleTarget=true`，恒 false 占位

### 6. Utilities 目录 `Targeting\Utilities\`（构造/组合工具）
- `BuiltInTargetType`（静态，`BuiltInTargetType.cs`）— 把游戏原生枚举映射为判定器：`internal static Dictionary<TargetType, ICustomTargetType> All`（含 None、Self、AnyEnemy/AllEnemies/RandomEnemy、AnyPlayer、AnyAlly/AllAllies、TargetedNoCreature、Osty）；`ICustomTargetType From(TargetType)` 查询，不支持则抛 `ArgumentOutOfRangeException`
- `LambdaTargetType(bool isSingleTarget, Func<Creature,bool> generalPredicate, Func<CardModel,Creature,bool>? cardPredicate=null, Func<PotionModel,Creature,bool>? potionPredicate=null, Func<ActionModel,Creature,bool>? actionPredicate=null) : CustomTargetType` — 用 lambda 快速造判定器；各重载有专属谓词就用，否则回落 generalPredicate
- `UnionTargetType(params ICustomTargetType[])` — `IsSingleTarget = 任一子类型为单`；有效 = **任一**子类型通过
- `IntersectionTargetType(params ICustomTargetType[])` — 有效 = **全部**子类型通过；`IsSingleTarget` 同 Union 逻辑（任一单即单）
- `DifferenceTargetType(ICustomTargetType original, ICustomTargetType exclude, bool? overrideIsSingleTarget = null)` — `original && !exclude`；可覆写单目标标志
- `SingleTargetTypesUnionManager`（静态，`SingleTargetTypesUnionManager.cs`）— 把多个**单目标** TargetType 合并成一个 Union 类型并缓存：`Registry: Dictionary<ImmutableHashSet<TargetType>, TargetType>`；`TargetType Get(IEnumerable<TargetType>)`（空集→`MinionTargetTypes.Void`；单元素→直接返回；新组合→`Register(new UnionTargetType(...), "MinionLib-UnionTargetType", hex 组合名)` 并缓存）；`TargetType GetWithBase(IEnumerable<TargetType>, TargetType baseType)`（结果为 Void 时回落 baseType）。**跳过**非注册/非单目标类型并 `Log.Warn`

### 7. Patches 目录 `Targeting\Patches\`（Harmony 接线）
- `CustomTargetTypeCardPatch`（静态，`[HarmonyPatch]`，`CustomTargetTypeCardPatch.cs`）— 卡片目标选择全链路 patch（见下）
- `CustomTargetTypePotionPatch`（静态，`[HarmonyPatch]`，`CustomTargetTypePotionPatch.cs`）— 药水目标选择 patch（见下）

---

## 二、核心机制说明（注册 → 判定 → 选中 → 出牌）

### 1. 注册
游戏原生 `TargetType` 是枚举（`MegaCrit.Sts2.Core.Entities.Cards.TargetType`）。MinionLib 用 FNV-1a 哈希 `"{namespace}.{name}"` → int → 强转 `TargetType` 得到自定义枚举值，同时写入 `RegisteredCustomTypes` 与 `CustomTypeDefinitions`；内置枚举（AnyEnemy 等）通过 `BuiltInTargetType.All` 预填进同一字典，因此**内置与自定义类型共用同一套分派**，区别只在 `TryGetCustomTargetType(..., includeBuiltin:false)` 只认自定义。

### 2. 判定分派
所有 patch 先调 `CustomTargetTypeManager.TryGetCustomTargetType(targetType, out custom, false)`：
- **命中自定义类型** → prefix/postfix 接管，用 `ICustomTargetType.IsValidTarget(实体, target)` 系列决定结果并 `return false`（跳过原方法）
- **未命中** → `return true` 走游戏原逻辑，对非自定义卡零影响

### 3. 单目标 vs 多目标语义（卡片）
- `IsSingleTarget=true`：
  - `CardModel.IsValidTarget` 接管为 `target != null && custom.IsValidTarget(card, target)`
  - `NCardPlay.TryPlayCard` 接管：target 为 null 直接 `CancelPlayCard()`；否则 `CanPlayTargeting(resolvedTarget)` → `TryManualPlay(resolvedTarget)` → `Cleanup(true)` → `EmitSignal(Finished, true)` → `Hand.TryGrabFocus()`
  - 鼠标/手柄的 `MultiCreatureTargeting` 被重定向到各自的 `SingleCreatureTargeting`（反射 Invoke 原方法）
- `IsSingleTarget=false`：
  - `TryPlayCard` 时 `resolvedTarget = null`
  - `ShowMultiCreatureTargetingVisuals` postfix 自行枚举 `card.CombatState.Creatures`（`IsAlive && custom.IsValidTarget`）：恰好 1 个 → `SetPreviewTarget`；多个 → 每个 `ShowMultiselectReticle()`；鼠标多选交给原逻辑

### 4. 手柄（Controller）完整流程
`NControllerCardPlay.SingleCreatureTargeting` 被 prefix 接管为 `ControllerSingleCustomTargeting`（async）：
1. 校验 card/cardNode/room/CombatState/自定义类型，失败则 `CancelPlayCard`
2. 连接 `NTargetManager.CreatureHovered/CreatureUnhovered` 信号（转发到原 `OnCreatureHover/Unhover`）
3. `targetManager.StartTargeting(targetType, cardNode, TargetMode.Controller, shouldCancel, null)`（shouldCancel = 方向导航失效或实例失效）
4. 过滤 validTargets；空 → 断开信号 + `CancelPlayCard`
5. `RestrictControllerNavigation(hitboxes)` + 首个 `TryGrabFocus()`
6. `await SelectionFinished()` → 断开信号 → `TryPlayCard(creatureNode.Entity)` 或 `CancelPlayCard`

### 5. 药水流程（`CustomTargetTypePotionPatch`）
- `NPotionHolder.UsePotion` prefix：非单目标 → `potion.EnqueueManualUse(potion.Owner.Creature)`（对自己喝）直接完成；单目标 → 经 `HoveredModelTracker.OnLocalPotionSelected/Deselected` 包裹调原 `TargetNode(potion.TargetType)`
- `NPotionHolder.TargetNode` prefix 接管 `TargetNodeCustom`：
  - `StartTargeting(targetType, startPosition, 方向导航?Controller:ClickMouseToTarget, shouldCancel, node => IsAllowedPotionTargetNode(...))` — **传入 potion 专属过滤**：NCreature→`Entity`、NMultiplayerPlayerState→`Player.Creature`，注释明言"防止 owner 锁定的目标类型选中其他玩家的随从"
  - 方向导航且战斗进行中 → 过滤 validTargets + `RestrictControllerNavigation` + 首个聚焦；非战斗 → 多人容器 `LockNavigation` + 首个玩家 Hitbox 聚焦
  - `await SelectionFinished()` → `ResolveTargetFromNode` → 再校验 `IsValidTarget(potion, target)` → `EnqueueManualUse(target)`；finally 恢复导航并 `TryGrabFocus`
- `NPotionPopup._Ready` postfix：按 `IsSingleTarget || potion.CanThrowAtAlly()` 设按钮文案 `"POTION_POPUP.throw"` / `"POTION_POPUP.drink"`

### 6. 随从注册/召唤/行动/死亡（不在本目录，交叉引用）
本目录只负责"选谁"。随从本身生命周期在 `Minion\MinionModel.cs`（`abstract class MinionModel : MonsterModel`）、`Minion\Patches\MinionKillPatch.cs`（死亡清理）、`Minion\Patches\MinionInteractablePatch.cs`、`Action\*`（`ActionModel` / `ExecuteCreatureActionGameAction`）、`Layout\*`、`Powers\MinionGuardianPower.cs`。本目录判定依赖的随从特征为 `Creature.IsPet`、`Creature.PetOwner`、`Monster is MinionModel`、`CombatState.Creatures`（均为游戏 API）。

---

## 三、与游戏 API 的交互点（HarmonyPatch 目标）

### 卡片（`CustomTargetTypeCardPatch`）
| 游戏类型/方法 | patch 类型 | 作用 |
|---|---|---|
| `ActionTargetExtensions.IsSingleTarget(TargetType)` | Postfix | 覆盖自定义类型的单目标标志 |
| `NTargetManager.AllowedToTargetCreature(Creature)` | Prefix | 预览/悬停合法性（经 FieldRef 读私有字段 `_validTargetsType`） |
| `CardModel.IsValidTarget(Creature?)` | Prefix | 卡判定（读 `CardModel.TargetType`） |
| `NCardPlay.TryPlayCard(Creature?)` | Prefix | 完整接管打牌（CanPlayTargeting→TryManualPlay→Cleanup→Finished 信号） |
| `NCardPlay.ShowMultiCreatureTargetingVisuals()` | Postfix | 自定义多选高亮 |
| `NMouseCardPlay.MultiCreatureTargeting(TargetMode)` | Prefix | 单目标时重定向到 `SingleCreatureTargeting` |
| `NControllerCardPlay.MultiCreatureTargeting()` | Prefix | 同上（手柄） |
| `NControllerCardPlay.SingleCreatureTargeting(TargetType)` | Prefix | 接管为自定义手柄流程 |

反射引用（方法可能缺失，均判空）：`NMouseCardPlay.SingleCreatureTargeting`、`NControllerCardPlay.SingleCreatureTargeting`、`NCardPlay.OnCreatureHover/OnCreatureUnhover/Cleanup(bool)`、`NCardPlay.Card` 属性。

### 药水（`CustomTargetTypePotionPatch`）
| 游戏类型/方法 | patch 类型 | 作用 |
|---|---|---|
| `NPotionHolder.UsePotion()` | Prefix | 接管使用（返回 Task） |
| `NPotionHolder.TargetNode(TargetType)` | Prefix | 接管目标选择 |
| `NPotionPopup._Ready` | Postfix | 改按钮文案 |

反射引用：`NPotionHolder.TargetNode`、`NPotionHolder.ShouldCancelTargeting`、`NPotionPopup._useButton` 字段。

涉及游戏命名空间：`MegaCrit.Sts2.Core.Entities.Cards`（TargetType）、`Entities.Creatures`（Creature）、`Models`（CardModel/PotionModel/ActionModel/MonsterModel）、`Nodes.Combat`（NCardPlay/NMouseCardPlay/NControllerCardPlay/NTargetManager/NCombatRoom/CombatManager）、`Nodes.CommonUi`（NPotionPopup/NPotionPopupButton）、`Nodes.Potions`（NPotionHolder）、`Nodes.Multiplayer`（NMultiplayerPlayerState(Container)）、`Nodes.Rooms`（NCombatRoom Ui/Hand）、`Context`（LocalContext.IsMe）、`Combat`（CombatSide）、`Runs`（RunManager）。

---

## 四、已知限制 / 陷阱（自代码注释与实现可见）

1. **枚举值是 32 位哈希强转**（`CustomTargetTypeManager.cs` L17-23、L52-65）：不同字符串可能哈希碰撞；同名/同命名空间注册会覆盖字典项（`Dictionary.Add` 重复 key 直接抛异常）。
2. **自动命名空间注册依赖调用栈**：`Register(ICustomTargetType, expr)` 用 `StackFrame(1)` 取命名空间，被内联/包装会抛 `InvalidOperationException`（故有 `NoInlining`）。`MinionTargetTypes` 实际绕过了它：经私有 `Register` 包装、手动传 `"MinionLib"` 命名空间。
3. **`TryPlayCard` 完全绕过原打牌逻辑**：prefix 只做 CanPlayTargeting+TryManualPlay+Cleanup+信号，游戏版本更新改签名会静默失效（反射方法判空后各自降级为取消打牌）。
4. **非单目标卡在 `TryPlayCard` 传 null 目标**（`resolvedTarget = null`），依赖非空 target 的原卡逻辑可能出问题。
5. **`SingleTargetTypesUnionManager` 静默跳过**非注册/非单目标类型（仅 `Log.Warn`）；union 枚举名是 hex 组合，不同组合也可能哈希碰撞；`GetWithBase` 的 Void 回落仅当集合为空。
6. **`ItselfTargetType` / `VoidTargetType` 的纯 Creature 判定恒 false**：卡片/药水/预览永远选不中，只有 Action（随从行动）上下文可用——设计如此，别当 bug。
7. **预览与实判的归属判定不对称**：`AnyMinion`/`AllMinions` 预览用 `LocalContext.IsMe(PetOwner)`（本地玩家），卡/药水用 `owner` 相等——多人下由非本地玩家发起的实体判定走 card/potion 重载，不受预览限制。
8. **药水路径的反射降级**：`TargetNode`/`ShouldCancelTargeting` 缺失时分别取消目标选择或传 null shouldCancel；`LockNavigation` 只在非战斗方向导航时执行。
9. **无 `CombatState` 场景会跳过/取消**：多选高亮、手柄流程、药水手柄导航都依赖 `card.CombatState.Creatures` / `potion.Owner.Creature.CombatState`，非战斗时直接不生效。
10. **调试日志 `[Conditional("DEBUG")]`**（`MainFile.cs` L29-42）：发布构建全部消失，线上排查需看游戏自身日志。

---
*生成：MinionLib 工坊版 0.6.2 源码精读（仅 Targeting 目录，19 文件全读）；供 mod 开发回查。*
