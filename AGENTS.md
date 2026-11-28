# Jaina Mod · AI 协作契约

> 任何 AI（DeepSeek / ChatGPT / Cursor / Copilot）在改动本仓库前，必须先读完本文档。
> 这是**契约**，不是建议。违反硬规则的输出一律作废，重做。
> 校准：2026-11-20 · 游戏 0.113.0 · 版本号变更时同步第 2 节

---

## 0. 项目一句话

《杀戮尖塔 2》的**吉安娜（Jaina）**角色 Mod：把《炉石传说》法师体系搬进游戏——
随从站场（7 槽）· 火焰/冰霜/奥术/暗影四派系 · 41 个自定义关键词 · 武器位 · 地标 · 任务线 · 英雄卡 · 多人联机。

规模：约 271 个 C# 文件 / 2.8 万行 · 95 张卡 · 86 个 Harmony patch · 44 处反射。
技术栈：Godot 4.5.1 mono + C# · RitsuLib（内容注册/工具）+ MinionLib（随从系统）。

## 1. 本文档怎么用

| 你是谁 | 怎么用 |
|---|---|
| AI 有文件权限（DeepSeek Harness 等） | 开工前读本文档 → 读 `docs/modding-memory/04-jaina/jaina-memory.md` → 等任务卡 |
| AI 无文件权限（ChatGPT 网页版） | 让用户粘贴 `tools\ai.cmd ctx` 生成的上下文包，包内已含第 2、3 节 |
| 人 | 第 2 节路径速查，第 4 节验收命令，第 6 节验收清单 |

## 2. 环境常量（唯一事实源，改这里就改 `tools/ai.ps1`）

| 项 | 值 |
|---|---|
| 项目根 | `E:\MOD\sts2\godot_project\jaina` |
| 游戏目录 | `E:\SteamLibrary\steamapps\common\Slay the Spire 2` |
| 游戏版本 | **0.113.0** |
| 主程序集 / API 字典 | `...\data_sts2_windows_x86_64\sts2.dll` / `sts2.xml` |
| 反编译源码 | `E:\MOD\sts2\sts2\0.113.0\src`（3867 个 .cs） |
| 部署目录 | `...\mods\Jaina\`（Jaina.dll / Jaina.json / Jaina.pck） |
| 运行时 | .NET 10（csproj `net10.0`）+ Godot 4.5.1 mono |
| 框架 | RitsuLib **0.5.12**、MinionLib **0.6.2** |
| 日志 | `%APPDATA%\SlayTheSpire2\logs\godot.log` |
| 断联包 | `%APPDATA%\SlayTheSpire2\logs\ritsulib_state_divergence_*.zip` |
| 仓库 | `WH8848/sts2-Hearthstone-mod`（main） |

> ⚠️ **历史事故**：旧文档写 `net9.0` / RitsuLib `0.5.11`，实际是 `net10.0` / `0.5.12`。
> **以 `Jaina.csproj` 为准**；发现不一致，先修本文档，再干活。

## 3. 硬规则

### A · 不许猜 API（违反即作废）

1. 任何外部符号（类型 / 方法 / 属性 / 字段 / 事件）写进代码前，必须能指向证据：`sts2.xml` 里有它，或反编译 `src/<文件>.cs:<行号>`。
2. 查不到 = 不许写。用 `tools\ai.cmd api <名字>` 查；把证据（文件:行号）写进代码注释或 commit message。
3. 参数顺序 / 重载不确定时，先写 3 行反射探针打印真实签名，再写业务代码。禁止"我记得是这样"。

### B · 不许假称验证（违反即作废）

4. 只有三样东西算证据：**编译输出**、**godot.log**、**截图**。没有它们，不许说"已修复 / 已测试 / 应该没问题"。
5. 每次改动后必须 `dotnet build -c Debug` 通过才能进下一步。
6. **回滚源码后也必须重新 build**——游戏跑的是已部署的 dll，源码回退 ≠ 行为回退。
7. 声称"已测试"必须同时给出：构建时间、日志时间戳、复现步骤。

### C · 联机确定性（这个 Mod 最顽固的问题）

8. 禁止 `new Random()`。随机一律走 `RunState.Rng`。
9. 任何参与随机 / 遍历的集合，必须先按 `CombatId` 或类型名**确定性排序**；禁止依赖 `Dictionary` / `HashSet` 的枚举顺序。
10. 任何 fire-and-forget（`_ = Xxx()`）里含 `await`，且会打牌 / 用 RNG / 改手牌顺序的，必须串行排队（`JainaSerialExecutor` 模式），否则两端时序不同 → StateDivergence。
11. 在伤害 / 能力钩子里写实例字段的逻辑，必须加 `IsPredictionCall` 防御——预测模拟会污染真实结算。栈检测用 `CombatPredictionSimulator`，**不是** `RandomForeseer`。
12. 全局施加钩子（`ModifyPowerAmountReceived` 等）必须校验 `target`，否则会把全场所有 Power 拦下来（地标免疫清空力量的经典事故）。

### D · 兼容与稳定

13. Harmony patch：显式参数类型 + `nameof` + 外层 try/catch + 失败降级不崩局。重载有歧义时用 `new[] { typeof(...) }` 显式指定。
14. 禁止 patch 一个未声明的基类方法（如 `Tainted.CanAfflict` 声明在 `AfflictionModel`）——`PatchAll` 会整体抛异常，整个 mod 不加载。
15. 反射集中到 `GameReflection`，启动时校验 + 缺失打 `Error`。
16. 新增 / 修改 patch 后必须跑 `tools\ai.cmd verify`，确认日志里没有 `Undefined target method` 或 `HarmonyException`。

### E · 本地化与内容

17. 本地化只在 `jaina/localization/{zhs,eng}/*.json`。格式：4 空格缩进 + **冒号后两个空格**（保持既有格式，diff 才小）。
18. 打包前必须 strip UTF-8 BOM（编辑器会自动加 BOM）。
19. 标签闭合是 `[/gold]`，不是 `}[/gold}`。
20. 卡面格式符：`{Damage:diff()}`、`{IfUpgraded:show:升级后|基础}`、`{energyPrefix:energyIcons(2)}`。中文卡面以 `zhs/cards.json` 为准。

### F · 交付

21. 一次只改一个可独立回滚的单元；改完立即编译。
22. 中文 commit，格式：`<动词>：<做了什么>——<为什么>（<证据>）`。
23. 流程：构建 → 提交 → `git push origin main` → 报告 commit hash。

## 4. 构建 / 部署 / 发布

```powershell
dotnet build -c Debug --nologo     # 编译 + 自动复制 dll/pdb/json 到 mods\Jaina + Godot headless 导出 pck
tools\ai.cmd verify                # 上面这条 + 部署校验 + 日志扫描（推荐直接用这个）
```

验收：`mods\Jaina\Jaina.pck` 的 `LastWriteTime` 是刚才。

发布：`-c Release` 构建，三件套（dll / pck / json）上传 `WH8848/sts2-Hearthstone-mod` 的 GitHub Release，并核对资产。

## 5. 改什么 → 必须同步什么

| 改动 | 必须同步 |
|---|---|
| 卡牌数值 / 文本 | `杀戮尖塔炉石mod_卡牌设计.xlsx` + `zhs/cards.json` + `eng/cards.json` |
| 新增关键词 | `card_keywords.json`（中英）+ 关键词图标 |
| 新增卡 | 卡池文件 + 两张卡面语言 + xlsx + 卡图 |
| 新机制 / 新坑 | `docs/modding-memory` 对应章节 + `CHATGPT_HANDOFF.md` 第 5 节 |
| 依赖 / 环境 | 本文档第 2 节 + `tools/ai.ps1` 常量 |

## 6. 一局验收清单

**单机**
- [ ] 进游戏 → 选吉安娜 → 进战斗（mod 加载成功）
- [ ] 本次改动的卡牌能打出、效果对、描述文字对
- [ ] `godot.log` 无 `Exception` / `ERROR` / `turn loop died`

**联机**（任何涉及随机、出牌顺序、钩子的改动都必须过）
- [ ] 双端同一版本（MinionLib / RitsuLib 版本必须一致，否则 SavedProperty 表差一项就断联）
- [ ] 打 2~3 回合，涉及本次改动的卡至少用一次
- [ ] 无 `StateDivergence`；有则保留 `ritsulib_state_divergence_*.zip` 走 `docs/AI-WORKFLOW.md` 断联 SOP

## 7. 会话开场与收尾

**开场**（AI 自己执行）：读本文档 → `git log --oneline -5` + `git status --short` → 读 `jaina-memory.md` → 读任务卡 → **复述任务并列出待查证的 API，等确认再动手**。

**收尾**：构建 → 按第 5 节同步文档 → 中文 commit → push → 回报 hash + 证据（日志时间戳 / 截图）。

## 8. 禁止事项

- 猜 API、编方法名、编参数顺序
- 声称测试过却没有日志或截图
- 一次改 20 个文件然后说"应该好了"
- 把反编译源码写进项目目录（`_decomp*` 会被 csproj 排除，但仍会污染编译）
- 直接改游戏原文件；改第三方 mod 的 dll 必须先备份 `.bak` 并记录
- 提交 `bin/`、`obj/`、`.temp/`、`temp/`、`*.zip`
