# Jaina Mod 交接文档（给 ChatGPT / 其他协作者）

> 用途：让新的协作者（ChatGPT 或其他 AI/人）快速理解这个项目是什么、现在做到哪一步、接下来要做什么、以及必须遵守的工程约定。
> 建议把本文档整份发给 ChatGPT，再把「第 4 节 你的任务」按实际情况改一改。

---

## 1. 这个项目是什么

**Jaina** 是《Slay the Spire 2》（杀戮尖塔 2）的一个大型角色扩展 Mod，把《炉石传说》的法师吉安娜做成一个可玩角色。

- **引擎**：Godot 4.5.1 mono（C#）
- **依赖框架**：RitsuLib（内容注册/工具）、MinionLib（随从系统）
- **游戏版本**：当前适配 **0.113.0**（之前是 0.111.1）
- **仓库**：`git@github.com:WH8848/sts2-Hearthstone-mod.git`（分支 `main`）
- **本地路径**：`E:\MOD\sts2\godot_project\jaina`
- **游戏安装**：`E:\SteamLibrary\steamapps\common\Slay the Spire 2`（mod 输出到 `mods\Jaina\`）

### 内容规模（当前）

- 卡牌 **95 张**（攻击 18 / 技能 29 / 能力 10 / **随从 37** / 状态 1）
- 稀有度：初始 4、普通 21、罕见 32、稀有 25、先古 2、衍生物 11
- 派系：火焰 / 冰霜 / 奥术 / 暗影（四派系 + 关键词体系）
- 机制关键词：法术牌、冻结、双生法术、战吼、亡语、灌注、交易、微缩、任务、地标、武器等等（41 个自定义关键词）
- 支持**多人联机**（这是最容易出问题的地方，见第 5 节）

### 设计语言

卡面描述大量使用游戏内置格式符，本地化文件里是**原始格式**：

```
造成{Damage:diff()}点伤害。            // diff() = 数值预览（与基础值不同时染色）
{IfUpgraded:show:升级后文本|基础文本}  // 升级前后不同文本
{energyPrefix:energyIcons(2)}          // 能量图标
[gold]关键词[/gold] / [blue]数值[/blue] // 颜色标签
```

---

## 2. 已经完成的工作（近期）

### 2.1 0.113 版本适配（最近一次大工程）

0.113 改了一批接口，导致 Mod 一度**完全无法启动**（Harmony patch 找不到目标 → `Entry.Init()` 抛异常 → 整个 mod 不加载）。已全部修复：

| 问题 | 修复 |
|---|---|
| `AbstractModel.AfterDeath` 新增 `CardModel? source` 参数 | 5 处重写补参数并透传 |
| `DynamicVar.GetBaseValueForIConvertible` 被移除 | 2 处删除重写，改用 `UpdateCardPreview` + `ShownValue` |
| `MonsterModel.IsHealthBarVisible` 被移除 | 2 处删除重写 |
| `NCreature.ToggleIsInteractable` 被移除 | 改为 `SetIsInteractable(on, true)` |
| `MerchantCardEntry` 构造 4→3 参数、`Populate` 需要 inventory | 同步修改 |
| `CardFactory.CreateForMerchant` 新增 `blacklist` 参数 | HarmonyPatch 签名同步 |
| `MerchantInventory.PopulateCharacterCardEntries` 被删除 | 商店稀有度分槽改为 patch 新工厂 `GetDefaultCharacterCardEntries` |
| `NHoverTipSet.SetAlignment` 新增 `Rect2` 重载 → Harmony AmbiguousMatch | 显式指定参数类型 `(Control, HoverTipAlignment)` |

### 2.2 多人联机断联（StateDivergence）修复

联机断联是这个项目最顽固的问题，已定位并修复多类根因：

1. **随机目标池顺序不确定** → `PickRandomTarget` 按 `CombatId` 确定性排序；
2. **目标池重复包含宠物**（`combatState.Creatures` 已含宠物却又 `Concat(Pets)`）→ 去掉重复拼接；
3. **英雄技能卡不占手牌位**导致两端「满手判定」不同 → 取消该特性，统一按手牌总数判定；
4. **冻结减伤上限/层数**调整（50%→25%，回合结束 -4→-2 层）；
5. **寒冰屏障被预测模拟污染**：RandomForeseer 预览时调用 `ModifyDamageMultiplicative` 会设置 `_pendingTrigger`，导致真实结算误触发 → 加 `IsPredictionCall` 防御（注意：栈检测必须用 `CombatPredictionSimulator`，不能用 `RandomForeseer`，否则会误判真实镜像调用）；
6. **随从军势**同类预测副作用 → 同样加预测检测。

### 2.3 其他修复

- **卡死**：`ClearBlock` 是 `async Task`，Harmony Prefix 返回 false 时必须给 `__result` 赋 `Task.CompletedTask`，否则 `await null` 抛 NRE 打死 turn loop；
- **死神之躯**：伤害顺序改为先敌人 7 次、再随从 2 点；「对所有随从」只打随从（`IsPet` 或 `Monster is MinionModel`），不打敌人本体；
- **火焰结界**：触发伤害从 `CreatureCmd.Damage(+ValueProp.Move)` 改为 `DamageCmd.Attack(...).FromCard(施加卡)`，因为前者在钩子链内不叠力量；
- **制造法力饼干**：曾改过又已 revert（当前是「基础版消耗、升级不消耗、饼干固定回 2 费」）；
- **文本/卡图**：一批本地化文本对齐、深度冻结/水元素卡图重下。
- **交易交互**：交易卡拖到弃牌堆并松手立即交易，普通点击弃牌堆仍打开查看；网络动作使用 `CardCmd.DiscardAndDraw` 保留原版弃牌/抽牌钩子与满手牌规则。

### 2.4 附带产出

- `Jaina卡牌数据.xlsx`（项目根目录）：95 张卡的完整数据表（名称/种类/稀有度/费用/描述/关键词/升级后名称）；
- `CODE_REVIEW.md`：271 文件 / 28,057 行的静态审查报告（86 个 Harmony patch、44 处反射、2 个 async void、5 个空 catch、118 条日志、0 个 new Random）；
- 修复了第三方 mod **Monster Predictor Lite** 的闪退：用 Mono.Cecil 直接给它的 dll 打 IL 补丁（`PatchNCreatureReady.Postfix` 开头插入 `Entity == null → return`），不再是额外 mod。原版备份在 `MonsterPredictorLite.dll.bak`。

---

## 3. 工程约定（必须遵守）

### 3.1 构建与打包

```powershell
cd E:\MOD\sts2\godot_project\jaina
dotnet build -c Debug --nologo
```

- 构建会**自动导出 pck** 并复制到 `mods\Jaina\`；
- 验证：`Jaina.pck` 的 `LastWriteTime` 是刚刚。

### 3.2 本地化文件

- 路径：`jaina/localization/{zhs,eng}/{cards,powers,card_keywords,relics,...}.json`
- 格式：`"key":  "value"`（4 空格缩进 + **冒号后两个空格**）——保持这个格式，diff 才小；
- **编辑器会写 UTF-8 BOM**：打包前必须 strip，否则可能出问题；
- 常见手误：标签闭合写成 `}` 而非 `]`（`[/gold}` 应为 `[/gold]`）。

### 3.3 提交习惯

每次改动：**构建 → 中文 commit message → `git push origin main`**，并报告 commit hash。

### 3.4 代码组织

- `Scripts/Character/Cards/{Minions,Tokens,Arcane,Fire,Frost,HeroPowers,Landmarks,Neutral}`：按**基础形态**的派系分类；基础类/模板/patch 类留在 `Cards/` 根目录；
- `Scripts/Character/Powers/`：能力；
- `Scripts/Character/Minions/`：随从（`JainaMinionBase` 是基类）。

---

## 4. 你的任务（ChatGPT 请从这里开始）

> 这一节由项目所有者填写。建议写成具体、可验收的任务，例如：

**示例任务（按需替换）：**

1. **代码审查**：审查 `Scripts/` 下所有 Harmony patch，找出「目标方法签名可能随版本变化」的脆弱点，输出清单 + 加固建议；
2. **新卡设计**：为吉安娜设计 N 张新卡（给出费用/类型/稀有度/派系/关键词/描述/升级形态），要求与现有 95 张卡不重复、符合四派系设计语言；
3. **联机问题排查**：基于 `E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\Jaina\` 与日志，分析下一次 StateDivergence 报告；
4. **文本校对**：检查中英文本一致性（`zhs/` vs `eng/`），输出差异表。

**请明确：**
- 你要处理的是**代码**、**设计**、**文本**、还是**分析**？
- 输出格式（直接改代码 / 出补丁 / 出文档 / 出表格）？
- 是否需要遵守第 3 节的构建与提交约定？

---

## 5. 已知坑与注意事项

1. **Harmony patch 是脆弱的**：0.113 一次更新就导致 8 处 patch 失效，其中 3 处直接让 mod 无法加载。新增 patch 时尽量用 `nameof` + 显式参数类型，并在 `Entry.Init()` 里做 try/catch 隔离（当前是 PatchAll 一崩全崩）。
2. **联机确定性**：任何随机（`rng.NextItem`）都必须保证两端**顺序一致**——列表先排序（`CombatId` / 类型名），不要依赖 `Dictionary`/`HashSet` 的枚举顺序。
3. **预测类 mod 会污染状态**：RandomForeseer 等会在预览时调用伤害/能力钩子，任何在钩子里写实例字段的逻辑都要加 `IsPredictionCall` 防御。
4. **`Creature.IsPet` 只覆盖玩家随从**（`PetOwner != null`），敌方随从要靠 `Monster is MinionLib.Minion.MinionModel` 判断。
5. **日志**：`C:\Users\<用户>\AppData\Roaming\SlayTheSpire2\logs\godot.log`（当前会话）+ `ritsu…state_divergence_*.zip`（断联诊断包，含两端日志和差异报告，最有价值）。
6. **不要**提交 `bin/`、`obj/`、`.tmp_*` 等临时产物（`.gitignore` 已覆盖大部分）。

---

## 6. 关键文件速查

| 文件 | 作用 |
|---|---|
| `Scripts/Entry.cs` | Mod 入口，`PatchAll` + 内容注册 |
| `Scripts/Character/JainaCardPool.cs` | 角色卡池 |
| `Scripts/Character/Keywords/JainaKeywords.cs` | 41 个自定义关键词 |
| `Scripts/Character/JainaHandHelper.cs` | 手牌容量判定 |
| `Scripts/Character/JainaRandomPoolHelper.cs` | 随机释放/目标选择（联机确定性关键） |
| `Scripts/Character/Minions/JainaMinionBase.cs` | 随从基类 |
| `Scripts/Character/Powers/` | 所有能力（含联机敏感逻辑） |
| `Jaina.csproj` | 构建 + 自动导出 pck |
| `MODDING-QUICKREF.md` / `RitsuLib-*.md` / `MinionLib_*.md` | 框架 API 参考 |
| `Jaina卡牌数据.xlsx` | 卡牌数据总表 |

---

## 7. 一句话总结

**这是一个把炉石法师做进杀戮尖塔 2 的大型角色 Mod，已适配 0.113、修复了多类联机断联与卡死问题；协作者需要遵守「构建 → 中文提交 → push」的流程，并对 Harmony patch 的版本脆弱性和联机确定性保持警惕。**
