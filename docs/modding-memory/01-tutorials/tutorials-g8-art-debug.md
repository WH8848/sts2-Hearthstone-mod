# STS2 Mod 教程精华：卡图/Spine、变量与描述、美术风格、控制台、调试热重载、先古对话

> 来源文件（可回查）：
> - `docs_05-card-art-and-skin-replacement.txt`（卡图&Spine）
> - `docs_05-variable-and-description.txt`（变量与描述）
> - `docs_06-style-art-drawing.txt`（风格原画绘制）
> - `docs_09-console-commands.txt`（控制台指令）
> - `docs_07-quick-debug-and-hot-reload.txt`（快速调试&热重载）
> - `docs_08-ancient-dialogue.txt`（先古对话）

---

## 1. 卡图替换（docs_05-card-art-and-skin-replacement）

**核心概念**：用 Harmony patch `CardModel.PortraitPath`（Getter），按"类名 → res://路径"字典替换原版卡图。**只能替换原版卡图**，不能加新卡。

**关键代码模式**：

```csharp
[HarmonyPatch(typeof(CardModel), nameof(CardModel.PortraitPath), MethodType.Getter)]
public static class CardModel_GetPortrait_Patch
{
    // 按照类名和资源路径配对即可
    private static readonly Dictionary<string, string> CustomPortraits = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(StrikeIronclad)] = "res://test/images/image.png",
        [nameof(DefendIronclad)] = "res://test/images/image.png",
    };

    static void Postfix(CardModel __instance, ref string __result)
    {
        var className = __instance?.GetType().Name;
        if (string.IsNullOrEmpty(className)) return;
        if (!CustomPortraits.TryGetValue(className, out var path)) return;
        if (!ResourceLoader.Exists(path)) return;
        __result = path;
    }
}
```

**注意事项/坑**：
- 资源路径必须是 `res://` 开头，且先用 `ResourceLoader.Exists()` 校验，避免资源缺失时报错。
- 字典用 `StringComparer.OrdinalIgnoreCase` 防大小写问题。

---

## 2. Spine 动画导入（docs_05-card-art-and-skin-replacement）

**核心概念**：
- 尖塔使用 **Spine 4.2.43** 版本，低于此版本不能直接使用（转换工具：[SpineSkeletonDataConverter](https://github.com/wang606/SpineSkeletonDataConverter)）。
- 需要安装 **Spine Godot Extension**（官方参考 <https://zh.esotericsoftware.com/spine-godot>），文件放到项目根目录，**可能需要重启 Godot**。

**关键流程**：
1. 把 spine 导出的 `atlas / skel / png` 放入项目指定位置（Godot 文件系统能看到即成功）。
2. 右键 Godot 文件系统 → 创建资源 → **`SpineSkeletonDataResource`**，把 `Atlas Res` 和 `SkeletonFile Res` 分别设为 atlas 与 skel 文件。
3. 战斗人物模型必须有这些动画名：`idle_loop`（待机循环）、`attack`（攻击）、`cast`（能力卡）、`hurt`（受伤）、`die`（死亡）。

**任意模型替换思路**：
- patch `CharacterModel.CreateVisuals`，返回继承 **`NCreatureVisuals`** 的自制节点，即可用任意场景替换人物。
- 场景需要有**唯一化命名（%）**的节点：`Visuals(Node2D)`、`Bounds(Control)`、`IntentPos(Marker2D)`、`CenterPos(Marker2D)`。
- 用 3D 模型：新建 `SubViewportContainer → SubViewport` 层级，SubViewport 里放 `Camera3D` + 任意 3D 模型，在 3D 视图中调整视角到 2D 正常显示，最后把 SubViewport 的 `transparent` 设为 `true`。

**注意事项/坑**：
- 遇到问题：项目→项目设置，把 **"将文本资源转换为二进制"** 禁用。

---

## 3. 描述文本：BBCode 与自定义 tag（docs_05-variable-and-description）

**核心概念**：描述是 `RichTextLabel`，Godot 原生 BBCode 全部可用（参考 Godot 4.x bbcode_in_richtextlabel 文档）。

**Godot 原生 BBCode 速览**：`[b]` 粗体、`[i]` 斜体、`[u]` 下划线、`[color=red]` 颜色、`[font=Arial]` 字体、`[font_size=24]` 字号。

**游戏自定义 tag**：

| 标签 | 作用 |
|---|---|
| `[ancient_banner]...[/ancient_banner]` | 先古之民横幅风格 |
| `[aqua]/[blue]/[gold]/[green]/[orange]/[pink]/[purple]/[red]` | 对应颜色文字 |
| `[fade_in]` / `[fly_in]` / `[jitter]` / `[sine]` / `[thinky_dots]` | 渐显 / 飞入 / 抖动 / 正弦波动 / 思考点点 动画 |
| `[rainbow freq=0.3 sat=0.8 val=1]` | 彩虹文字 |

---

## 4. 占位变量（DynamicVars）与 formatter（docs_05-variable-and-description）

**核心概念**：描述中的 `{X}` 占位符会被 model 的 `DynamicVars` 中对应数值替换；formatter 用 **SmartFormat** 库格式化表现。

**常用占位变量**（名称 → 对应 Var 类）：

| 占位符 | Var 类 | 说明 |
|---|---|---|
| `{Damage}` | `DamageVar` | 伤害 |
| `{Block}` | `BlockVar` | 格挡 |
| `{Cards}` | `CardsVar` | 卡牌数量 |
| `{Energy}` | `EnergyVar` | 能量（动态值） |
| `{energyPrefix}` | — | 能量（固定数值） |
| `{Repeat}` | `RepeatVar` | 重复次数 |
| `{Heal}` | `HealVar` | 治疗 |
| `{HpLoss}` | `HpLossVar` | 失去生命 |
| `{MaxHp}` | `MaxHpVar` | 最大生命 |
| `{Gold}` | `GoldVar` | 金币 |
| `{Summon}` | `SummonVar` | 召唤 |
| `{Forge}` | `ForgeVar` | 铸造 |
| `{Stars}` | `StarsVar` | 辉星 |
| `{StrengthPower}` | `PowerVar<StrengthPower>` | 力量 |
| `{DexterityPower}` | `PowerVar<DexterityPower>` | 敏捷 |
| `{WeakPower}` | `PowerVar<WeakPower>` | 虚弱 |
| `{VulnerablePower}` | `PowerVar<VulnerablePower>` | 易伤 |
| `{PoisonPower}` | `PowerVar<PoisonPower>` | 中毒 |
| `{DoomPower}` | `PowerVar<DoomPower>` | 灾厄 |
| `{CalculatedDamage}` | `CalculatedDamageVar` | 计算出的伤害量 |
| `{CalculatedBlock}` | `CalculatedBlockVar` | 计算出的格挡值 |

**游戏自定义 formatter**：
- `diff()` — 高于基础变绿、低于变红（战斗/升级预览）。例：`造成{Damage:diff()}点伤害。`
- `inverseDiff()` — 与 diff 相反。例：`失去{HpLoss:inverseDiff()}点生命。`
- `energyIcons()` — 数值渲染成能量图标。例：`获得{Energy:energyIcons()}。`
- `starIcons()` — 数值渲染为辉星。例：`获得{Stars:starIcons()}。`
- `IfUpgraded:show` — 按升级情况显示不同文本：`{IfUpgraded:show:升级文本|未升级文本}`
- `abs` — 绝对值：`{Damage:abs()}`
- `percentMore()/percentLess()` — 百分比：`PercentMore` 把 1.25 变 25%，`PercentLess` 把 0.75 变 25%。例：`额外造成{Boost:percentMore()}%伤害。`

**SmartFormat 内置 formatter**（参考 <https://github.com/axuno/SmartFormat/wiki>）：
- `cond` — 条件分支：`{X:cond:>0?生效|不生效}`。例：`{FanOfKnivesAmount:cond:>0? 对所有敌人|}造成{Damage:diff()}点伤害。`
- `choose` — 按索引/值选择分支：`{X:choose(1|2|3):one|two|three|other}`
- `plural` — 复数（英语环境）：`Draw {Cards:diff()} {Cards:plural:card|cards}.`
- `list` — 拼接（参考 wiki v2-Lists）。

**卡牌独有上下文变量**：

| 变量 | 含义 | 典型写法 |
|---|---|---|
| `singleStarIcon` | 星星图标 | `每当你获得{singleStarIcon}时` |
| `InCombat` | 是否处于战斗 | `{InCombat:\n（命中{CalculatedHits:diff()}次）\|}` |
| `IsTargeting` | 当前是否有目标 | `{IsTargeting:\n（造成{CalculatedDamage:diff()}点伤害）\|}` |
| `OnTable` | 牌是否在手牌/出牌区 | `{OnTable:在场上|不在场上}` |
| `IfUpgraded` | 是否升级 | `[gold]升级[/gold]你[gold]手牌[/gold]中的{IfUpgraded:show:所有牌|一张牌}。` |

**能力（Power）独有**：本地化通常写三条：
- `description` — 静态描述（能力非可变时用，无独有变量）。
- `smartDescription` — 动态描述（能力可变时用，注入运行时变量 + 叠加 DynamicVars）。
- `remoteDescription` — 联机专用（能力由他人施加，`Applier` 存在且非本地玩家时替换 smartDescription）。

**smartDescription/remoteDescription 运行时变量**：`Amount`（当前层数/数值）、`OnPlayer`（持有者是否玩家，`{OnPlayer:你|该敌人}`）、`IsMultiplayer`、`PlayerCount`、`OwnerName`、`ApplierName`、`TargetName`。

**注意事项/坑**：
- 在 hovertip 等写描述时，能用的占位变量都是游戏预先注入好的，需看源码了解原理。

---

## 5. DynamicVar / CalculatedVar / LocString（docs_05-variable-and-description）

**DynamicVar 定义**（model 上记录的指定值，用 `CanonicalVars` 指定初始值）：

```csharp
protected override IEnumerable<DynamicVar> CanonicalVars => [
    new DamageVar(12, ValueProp.Move)
];
```

- 之后用 `DynamicVars["Damage"].BaseValue` 读写（`DamageVar` 的 ID 是 `"Damage"`）。
- 可用第一个参数自定义 ID：`new DamageVar("TestDamage", 12, ValueProp.Move)`。
- 便捷访问原版属性：`DynamicVars.Damage`。

**CalculatedVar**（公式 `base + extra * calculated`，例：全身撞击）：

```csharp
protected override IEnumerable<DynamicVar> CanonicalVars => new ::_003C_003Ez__ReadOnlyArray<DynamicVar>(new DynamicVar[3]
{
    new CalculationBaseVar(0m),
    new ExtraDamageVar(1m),
    new CalculatedDamageVar(ValueProp.Move).WithMultiplier((CardModel card, Creature? _) => card.Owner.Creature.Block)
});
```

- 即：基础值 0，额外增加 1 倍自身格挡值的伤害。
- **坑**：使用 CalculatedVar 时，`base` 和 `extra` 两个 var 必须同时写；设计繁琐且问题不少，**不推荐使用**。
- 前置库为 **ritsulib** 时用 `ComputedDynamicVar`（RitsuLib 第19章：计算动态变量，支持按目标/升级状态/预览模式动态计算）。

**LocString 本地化用法**：

```csharp
LocString description = new LocString("powers", base.Id.Entry + ".description"); // 从 powers.json 获取
description.Add("Amount", amountOverride ?? Amount);
description.Add("singleStarIcon", "[img]res://images/packed/sprite_fonts/star_icon.png[/img]");
description.Add("energyPrefix", EnergyIconHelper.GetPrefix(this));
stringBuilder.Append(description.GetFormattedText()); // 最终格式化文本
```

---

## 6. 风格原画绘制（docs_06-style-art-drawing）

**工具**：
- 硬件：数位板（百元级即可、坐姿省腰颈）、数位屏（更好控但贵、便宜的有色差）、iPad + Apple Pencil（最适合新手）。
- 软件：PS（88元/月，盗版会强制退出）、CSP（一次性429元）、**krita**（免费开源，稍有小 bug）、Procreate（仅 iPad，88元）。
- 其他制作途径：解包资产、AI 生成。

**绘制流程**：
1. 游戏内截屏作参考（保证画风统一）；用 **PureRef 2.0** 收集角色美术资源参考图。
2. 截屏透明度调低，勾勒草稿，先确定角色与敌人的比例。
3. 分析并画出人物特征：**强化服装/标志性元素**（例：夸张帽子、删去蕾丝边吊坠等装饰）。
4. **发型比五官重要**（识别角色靠高矮胖瘦、衣着，不是脸）。
5. 不要画太多明显线条：塔2是美式卡通风，**以色块为主**；透视和比例可适度变形以适配漫画风格。
6. 用色块切分阴影和亮部，区分体积感后稍添细节。

**注意事项/坑**：
- 角色衣服不要穿太多（字面意思）：后续要拆解绑定做动画，衣服/头发用物理模拟，过多毛发和过长衣物会消耗大量调试精力 → 适合紧身衣/机甲风格。
- 不要专注光影：塔2 游戏内很少有强烈光影，明暗对比强烈反而违和。

---

## 7. 控制台指令（docs_09-console-commands）

**使用方式**：
- 加载 mod 状态下按 `~` 开启内置控制台。
- `tab` 补全候补；`↑`/`↓` 选取 + `enter` 确认；`↑` 调出上一条指令；选中最新指令时 `↓` 可删除当前内容。
- 语法：`<X>` 必填参数，`[X]` 可选参数。
- **目标索引**：从 0（你扮演的玩家）开始；单机 1 往后是敌人（按生成顺序）；联机 1 往后先是其他玩家。

**战斗相关**：

| 指令 | 说明 |
|---|---|
| `damage <数值> [目标索引]` | 对所有敌人造成伤害；指定索引可单一目标 |
| `block <数值> [目标索引]` | 为玩家添加格挡；可指定生物 |
| `kill [目标索引\|all]` | 杀死指定目标；all 杀全部敌人，默认第一个敌人 |
| `die` | 直接死亡 |
| `win` | 直接胜利 |
| `heal <数值> [索引]` | 恢复生命 |
| `godmode` | 切换无敌（强力 buff），再输一次关闭 |
| `power <ID> <层数> <目标索引>` | 对指定目标施加能力 |
| `energy <数值>` / `gold <数值>` / `stars <数值>` | 加能量/金币/辉星（可正可负） |
| `relic [add\|remove] <遗物ID>` | 添加或移除遗物，默认添加 |
| `potion <ID>` | 添加一瓶指定 ID 药水 |

**卡牌相关**：

| 指令 | 说明 |
|---|---|
| `card <卡牌ID> [牌库名]` | 生成卡牌：`hand`（默认）、`draw`、`discard`、`exhaust`、`master_deck` |
| `remove_card <ID> [牌库名]` | 从指定牌库移除卡牌 |
| `upgrade <手牌位置>` | 升级手牌指定位置卡牌（0=最左侧） |
| `enchant <ID> [层数] [手牌位置]` | 附加附魔，层数默认1，默认最左侧手牌 |
| `afflict <ID> [层数] [手牌位置]` | 附加诅咒 |
| `draw <数量>` | 抽指定数量牌 |

**地图相关**：`act <幕数|名称>`（跳章节）、`room <ID>`（跳房间节点，如 BOSS/SHOP，看 tab 补全）、`event <ID>`、`fight <ID>`（跳怪物遭遇战）、`travel`（地图传送模式，点击房间直达）、`ancient <ID> [遗物ID]`（跳先古之民，可必出指定遗物选项）。

**其他**：
- `help [命令名]` — 列命令或查看用法（如 `help card`）。
- `open <logs|saves|root|build-logs|loc-override>` — 打开常用目录（日志/存档/游戏根目录/构建日志/本地化覆盖）。
- `unlock <类型>` — 标记已发现：`cards`、`potions`、`relics`、`monsters`、`events`、`epochs`、`ascensions`；`all` 全解锁。
- `achievement <unlock|revoke> [ID]` — 解锁/撤销成就，无 ID 则操作全部。
- `leaderboard [选项] [名称] <分数> [数量]` — 提交排行榜分数；选项 `upload` 上传、`random` 随机分数测试。
- `getlogs [test-feedback] <名称>` — 收集日志打包 zip 并打开目录；`test-feedback` 只打包关键文件（给开发团队而非 mod 开发者）。
- `dump` — 把所有 model 的 ID 输出到控制台和日志文件。
- `log [类型] <级别>` — 设置日志级别：`verydebug`、`debug`、`info`、`warn`、`error`。
- `art <类型>` — 列出缺少美术资源的内容：`card`、`relic`、`potion`、`enchant` 等。
- `instant` — 加速模式，跳过所有动画延迟。
- `bestiary` — 打开怪物图鉴。
- `sentry <test|message|exception|crash|status> [文本]` — 测试 Sentry 错误上报。
- `trailer` — 预告模式，数字键 0-9 切换 UI 显隐，用于录预告片/截图。
- `cloud delete` — 删除 Steam 云存档（仅 Steam 平台）。
- `multiplayer [test]` — 打开多人菜单，`test` 参数打开测试场景。

---

## 8. 快速调试 & 热重载（docs_07-quick-debug-and-hot-reload）

**csproj 必需配置**（VSCode 与 Rider 通用）：
- 属性 `Sts2DataDir` = `$(Sts2Dir)/data_sts2_windows_x86_64`。
- Debug 配置：`Optimize=false`、`DebugType=portable`；Release 配置：`Optimize=true`、`DebugType=none`、`PathMap=$(AppOutputBase)=.\`。
- `Reference Include="sts2"`，`HintPath=$(Sts2DataDir)/sts2.dll`，`Private=false`。
- 自定义 Target `"Copy Mod"`（`AfterTargets="PostBuildEvent"`）：MakeDir `$(Sts2Dir)/mods/`，Copy `$(TargetPath)`、`$(TargetDir)$(TargetName).pdb`（带 Exists 条件）、`$(MSBuildProjectName).json` 到 `$(Sts2Dir)/mods/$(MSBuildProjectName)/`。

**VSCode 配置**（项目根 `.vscode/` 三个文件）：
- `launch.json`：`type: coreclr`、`request: launch`、`preLaunchTask: build`、`program: ${config:sts2.installDir}/${config:sts2.gameExeName}`、`cwd: ${config:sts2.installDir}`、`console: internalConsole`、`sourceFileMap: { ".\\": "${workspaceFolder}/" }`、`stopAtEntry: false`。
- `tasks.json`：task `"build"`（type process，`dotnet build ${workspaceFolder}/${config:sts2.modId}.csproj -c Debug --nologo`，problemMatcher `$msCompile`）。
- `settings.json`：`sts2.installDir`（游戏路径）、`sts2.gameExeName`（`SlayTheSpire2.exe`）、`sts2.modId`。
- VSCode 设置（ctrl+,）启用 **Csharp › Experimental › Debug: Hot Reload**。

**使用**：F5 启动 → 改代码后点测试表盘中的**火焰图标（🔥）**应用热重载 → 点击代码左侧红点可断点调试。

**Rider 配置**：Add Configuration → Edit Configuration → 创建 **.NET Executable** 配置，按 **Debug** 模式启动（不要点绿三角直接 run）；热重载点 🔥 或旧版本上方 `Apply Changes`。

**注意事项/坑**：
- 热重载功能有限：**不能有增删函数等过大改动**。
- **资源 PCK 不能通过这种方式热重载**。
- 进不了游戏提示不通过 steam：在游戏根目录创建 `steam_appid.txt`，内容写 `2868840`。

---

## 9. 先古对话（docs_08-ancient-dialogue）

**核心概念**：
- 文件路径：`{modId}/localization/{Language}/ancients.json`。
- 对话 ID 格式：`{先古之民ID}.talk.{角色ID}.{对话序号}-{行号}[可选 r].{ancient|char|next}`。
  - `ancient` = 先古之民说的话；`char` = 角色说的话；`next` = "继续"按钮文本。
  - `x-y` = 第 x 套对话的第 y 句。
  - 末尾 `r` = 该套对话**可重复**。
- 游戏按**遇见次数**选取第几套对话：第一次第 0 套、第二次第 1 套、第五次第 2 套（对应 `VisitIndex` 0/1/4…）。
- `firstVisitEver`：全游戏第一次遇见该先古时触发，如 `DARV.talk.firstVisitEver.0-0.ancient`。
- **ANY**：先古不认识的角色触发，如 `DARV.talk.ANY.0-0r.ancient`（ANY 通常标 r）。
- 名称/绰号：`{ID}.title`（先古名字）、`{ID}.epithet`（先古绰号）。

**示例**（达弗 DARV 对话铁甲战士 IRONCLAD）：

```json
"DARV.talk.IRONCLAD.0-0.ancient": "啊，冒火的战士回来啦！\n我这儿有东西正适合你！",
"DARV.talk.IRONCLAD.1-0r.ancient": "看到你还活着四处砸人脑壳，感觉真不错啊！",
"DARV.talk.IRONCLAD.2-0.ancient": "我还留着你的那把重刃呢……但没能找到可以修好它的人。",
"DARV.talk.IRONCLAD.2-0.next": "继续",
"DARV.talk.IRONCLAD.2-1.char": "[i][font_size=22]铁甲战士盯着达弗。[/font_size][/i]",
"DARV.talk.IRONCLAD.2-2.ancient": "或许建筑师知道些什么呢。",
"DARV.talk.firstVisitEver.0-0.ancient": "……我把那东西放哪儿了……喔！\n来看我的收藏的吗！？那边那堆里随便挑一个吧，得好好用上哦！",
"DARV.title": "达弗",
"DARV.epithet": "囤积者",
"DARV.talk.ANY.0-0r.ancient": "来来，这里有些被遗忘的宝石，拿一块去吧！",
"DARV.talk.ANY.1-0r.ancient": "今天我还挺忙的！\n那堆东西里有啥想要的你就自己拿了吧！！"
```

**选取逻辑**（关键）：
- 每个 `x-y` 的 y 可以有一条对话 + 一个 `next`（指定按钮文字）。
- 若当前遇见次数没有对应套（如代码中没有 `VisitIndex=2`），则找标了 `r` 的可重复套；多个可重复套之间**随机选择**。

**扩展（基础库特性）**：
- **攻击建筑师**：`{ID}.talk.{角色}.0-attack` 值为 `Both`/`Architect`/`Player`/`None` 指定谁攻击；`-startattack` / `-endattack` 后缀在对话开始前/后攻击。
  ```json
  "THE_ARCHITECT.talk.TEST_CHARACTER.0-attack": "Both"
  ```
- **指定触发次数**：`{ID}.talk.{角色}.{x}-visit` 值为数字，指定第几次遇到先古时触发本套对话：
  ```json
  "TEST_ANCIENT.talk.TEST_CHARACTER.1-visit": "3"
  ```
- **RitsuLib 专属**：`.sfx` 后缀播放 fmod 音效：
  ```json
  "TEST_ANCIENT.talk.ANY.0-0r.ancient.sfx": "event:/sfx/ui/enchant_simple"
  ```
