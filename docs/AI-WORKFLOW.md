# Jaina Mod · AI 协作工作流

> 给一个人 + 两个 AI（DeepSeek、ChatGPT）+ 一个 28k 行 Mod 的流水线。
> 配套：AGENTS.md（契约）、tools/ai.ps1（执行）、docs/templates/TASK-CARD.md（任务卡）。
> 版本：2026-11-20

---

## 1. 这套流程解决什么

不是"让 AI 写更多代码"，而是**让 AI 只在有证据的地方动手**。三个真实事故说明了缺什么：

| 事故 | 根因 | 本流程对应的闸 |
|---|---|---|
| 0.113 更新后 8 处 patch 失效，3 处 mod 直接不加载（AfterDeath 加参数、ToggleIsInteractable 被删、MerchantCardEntry 构造 4→3） | 签名漂移无自检，编译期发现不了 | 闸 1 + 版本迁移 SOP |
| 联机反复 StateDivergence（惊愕卡牌异步乱序、MinionLib 版本差一项 SavedProperty） | 时序不确定 / 环境不一致 | 硬规则 C + 断联 SOP |
| 文档写 net9.0 / RitsuLib 0.5.11，实际 net10.0 / 0.5.12 | 事实散落在多个文档里各写各的 | 环境常量单一事实源 |

一句话：**你缺的不是 AI 的能力，是 AI 的输出可不可信。**

---

## 2. 五阶段流水线

```
  S0 任务卡      S1 侦察        S2 设计       S3 实现        S4 验证       S5 沉淀
  人 · 5分钟  →  AI · 闸1   →   AI + 人   →   AI · 闸2   →   人 · 闸3   →  AI
  写清验收       查证 API        出 2 个方案    小步改动       跑游戏        写文档
  和边界         列证据清单      人拍板         每步编译       日志/截图     提交
```

| 阶段 | 输入 | 谁做 | 产出 | 闸门 |
|---|---|---|---|---|
| **S0 任务卡** | 一句需求 | 人 | docs/tasks/<名>.md（用模板） | 验收标准可执行吗？ |
| **S1 侦察** | 任务卡 | AI | 证据清单：每个 API 的 文件:行号 | **闸1 事实闸** |
| **S2 设计** | 任务卡 + 证据 | 两个 AI 各一版 | 方案 A/B + 风险 + 影响面 | 人选一个，写进任务卡 |
| **S3 实现** | 定稿方案 | AI | 代码 + 编译通过 | **闸2 编译闸** |
| **S4 验证** | 构建产物 | 人 | 日志 + 截图 + 联机结果 | **闸3 运行闸** |
| **S5 沉淀** | 验证结果 | AI | 文档同步 + 中文 commit | AGENTS.md 第 5 节 |

**关键：S0 和 S4 必须是人。** AI 能写、能查、能算，但它看不见你的屏幕、听不见音效、进不了联机房间。凡是"效果对不对"的判断，只有你能做。

---

## 3. 双模型分工

两个 AI 不是"双保险"，是**两种不同的工作条件**：

| 场景 | DeepSeek（有工具权限） | ChatGPT（无文件权限） |
|---|---|---|
| 读反编译、grep API、追调用链 | 主力 | 只能靠粘贴 |
| 写代码 + 构建 + 部署校验 | 主力 | — |
| 解析断联 zip / 日志 | 主力 | 需要你上传 |
| 方案设计与取舍 | 出一版 | 出一版（然后对比） |
| 疑难 bug 第二意见 | 复核 | 主力（无上下文包袱，不容易跟着错） |
| 卡牌设计 / 文案 / 英文资料 | 一般 | 主力 |
| 代码审查找脆弱 patch | 复核 | 主力（盲审） |

### 互审协议（对抗性检查，最有价值的一步）

1. 实现方写一段**变更说明**：改了什么 / 为什么 / 证据（文件:行号）。
2. 审查方只看三样：**任务卡 + diff + AGENTS.md 第 3 节**。**不给**实现方的理由。
3. 审查方输出两类结论：
   - **阻断项**：违反硬规则 / 幻觉 API / 会破坏联机 → 必须改
   - **建议项**：可读性、性能、命名 → 记入待办，不阻塞
4. **分歧不靠说服，靠源码裁定**：两边吵不清时，跑 tools/ai.cmd api <符号>，谁对谁错当场见分晓。

> 经验：让 ChatGPT 盲审 DeepSeek 的 diff，最常抓到的就是**编造的 API** 和**漏掉的 try/catch**。

---

## 4. 三道闸

### 闸1 · 事实闸（写代码之前）

```powershell
tools/ai.cmd api PlayerCombatState          # 查类型
tools/ai.cmd api Hand.Cards                 # 查成员
```

判据：
- sts2.xml 有 → 符号存在
- 反编译源码有 → 拿到 文件:行号
- **两边都没有 → 不许写**。换名字重查，或先写 3 行反射探针。

产出物是**证据清单**，写进任务卡或代码注释：

```csharp
// 证据：Core/Combat/CombatManager.cs:454  PopulateCombatState(state.Players)
```

### 闸2 · 编译闸（写完代码）

```powershell
tools/ai.cmd verify
```

它做三件事：dotnet build -c Debug → 校验 mods/Jaina 里 dll/pck 时间戳 → 扫日志尾部 400 行。

判据：**0 error**。warning 记录但可暂不阻塞。

> ⚠️ 回滚源码后必须重新 build。游戏跑的是已部署的 dll，源码回退 ≠ 行为回退（踩过）。

### 闸3 · 运行闸（人做）

```powershell
tools/ai.cmd log -Tail 300 -Pattern "Exception|ERROR|StateDivergence|Jaina"
```

三样证据缺一不可：
1. **构建时间戳**（dll/pck 是这次的）
2. **日志**（无异常，或异常与本次改动无关）
3. **截图 / 联机结果**（功能可见；联机打 2~3 回合不断）

判据清单在 AGENTS.md 第 6 节。

---

## 5. 会话协议

### 开场（每次新会话）

**有工具权限的 AI**：读 AGENTS.md → git log -5 + git status → 读 jaina-memory.md → 读任务卡 → **复述任务 + 列待查证 API，等确认**。

**网页版 AI**：

```powershell
tools/ai.cmd ctx -Task "任务一句话" -Files Scripts/Character/Cards/Frost/XxxCard.cs
```

生成的 docs/_ctx/<时间>-<任务>.md 整份粘过去。里面有：环境表 + 硬规则 + git 状态 + 相关文件全文 + 输出要求。

> 别把整个仓库倒进去。**上下文越脏，幻觉越多。**

### 中途

- 一次只问一件事。三个问题混在一段里，AI 只会答最后一个。
- 贴 **diff**，不要贴 2000 行文件。
- AI 说"已修复" → 立刻反问："哪个文件第几行改的？证据是什么？"（它答不上来就是在编）

### 收尾

让 AI 输出两样东西，你照着执行：
1. **变更说明**：改了哪些文件、每处为什么、证据
2. **验收清单**：具体到"进哪一局、打哪张牌、看哪行日志"

---

## 6. SOP

### 6.1 游戏版本升级（0.113 → 0.114）

1. **等框架先动**。RitsuLib / MinionLib 没出新版就别急着升——游戏能跑，mod 跑不了。
2. 反编译新版本到 E:/MOD/sts2/sts2/<新版本>/（保留旧版本目录，方便对照）。
3. 抽查关键目标是否还在：
   ```powershell
   tools/ai.cmd api AfterDeath
   tools/ai.cmd api ToggleIsInteractable
   ```
4. 全量 verify。签名变了**编译期就会报错**，这是最便宜的一轮。
5. 启动游戏看日志：搜 Undefined target method / HarmonyException。**签名变了但编译不报错的，只有运行时才暴露。**
6. 按 CODE_REVIEW.md 的 P1 建议，维护一张「patch 目标 → 存在性自检」清单，启动时打 Error。
7. 单机一局 → 联机一局。

### 6.2 联机断联（StateDivergence）

1. 收集诊断包：%APPDATA%/SlayTheSpire2/logs/ritsulib_state_divergence_*.zip
2. 解压看 state-divergence-report.txt。**头部 savedProperties.netIdMap 直接列出两端差异**（xxx: local=X; remote=Y）。
3. 按症状分三类：

| 症状 | 归类 | 处理 |
|---|---|---|
| SavedProperty 条数两端不等 | **环境不一致** | 对齐 MinionLib / RitsuLib / BaseLib 版本与**内容**（版本号相同、内容不同也会炸） |
| 同一集合两端顺序不同 | **顺序不确定** | 按 CombatId / 类型名排序 |
| 操作顺序相反（Play LOCAL [A,B] vs REMOTE [B,A]） | **异步时序** | 串行队列，禁止 fire-and-forget 中 await |

4. 定位到具体卡/能力后，写一个最小复现：单机用控制台直接触发，看日志里 [Jaina*] 诊断行的顺序。
5. 修复后联机验证，确认 savedProperties.count 两端相等。

### 6.3 发布

```powershell
dotnet build -c Release --nologo
```

三件套（Jaina.dll / Jaina.pck / Jaina.json）上传 GitHub Release → 核对资产 → 部署到本机 mods/Jaina → 进游戏确认版本号变了。

### 6.4 文档同步

改完代码不是结束，按 AGENTS.md 第 5 节的矩阵同步。**最容易漏的两项**：
- xlsx 卡牌表（设计稿）和 cards.json（游戏内）不同步
- CHATGPT_HANDOFF.md 的"已完成工作"越写越长、越来越假 → 定期删旧，只留最近两周 + 已知坑

---

## 7. 反模式（见到就停）

| 反模式 | 后果 | 正确做法 |
|---|---|---|
| "我记得这个 API 是这样" | 编造符号，编译报错或运行时静默失效 | ai.cmd api 查证 |
| 一次让 AI 改 10 个文件 | 出问题无法定位、无法回滚 | 一卡一改，改完就编译 |
| AI 说"已修复"就信了 | 你要的是游戏里能用，不是聊天框里说得通 | 要证据：日志/截图 |
| 把整个 Scripts/ 塞进上下文 | 模型注意力被稀释，反而开始编 | ctx 只带相关文件 |
| 两个 AI 同时改同一份代码 | 互相覆盖 | 一次只有一个"施工方"，另一个只审 |
| 联机问题靠猜 | 断联是最贵的 bug | 拿 zip，看 netIdMap |
| 文档当"记忆"用，不验证 | 会腐化（net9/net10 事故） | 环境常量只在 AGENTS.md 第 2 节，且以 csproj 为准 |

---

## 8. 工具速查

| 命令 | 什么时候用 |
|---|---|
| tools/ai.cmd doctor | 开工前 / 环境怪怪的 |
| tools/ai.cmd api <名字> | 写任何外部符号之前 |
| tools/ai.cmd verify | 每次改完代码 |
| tools/ai.cmd log -Tail 300 -Pattern "Exception" | 游戏崩了 / 行为不对 |
| tools/ai.cmd ctx -Task "..." -Files ... | 要问网页版 AI 之前 |

直接双击 tools/ai.cmd 也行（默认跑 doctor）。

> ai.ps1 是 UTF-8 **带 BOM** 的，因为你的 PowerShell 是 5.1（GBK 默认），不带 BOM 中文会乱。ai.cmd 里已加 -ExecutionPolicy Bypass，不用改系统策略。
