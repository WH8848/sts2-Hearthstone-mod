# STS2 Mod 制作工具教程精华（tutorials.sts2modding.com / tools 系列）

> 原文抓取文件（可回查）：`tools_card-frame-preview.txt`、`tools_dialogue-preview.txt`、`tools_id-generator.txt`、`tools_text-preview.txt`
> 注：原始抓取为页面 UI 文本转储，以下按工具功能整理为干货。

---

## 1. 卡框预览器（tools_card-frame-preview.txt）

**用途**：为自定义卡牌预览/导出卡框资源，支持导出 PNG 与生成配置代码。

- **卡牌类型**：`Skill`、`Attack`、`Power`、`Quest`、`Ancient`；Ancient 可与基础类型组合：`Ancient Attack`、`Ancient Skill`、`Ancient Power`
- **卡框参数**（三轴取值）：
  - `H`：0–1
  - `S`：0–5
  - `V`：0–3
  - 示例配置串：`SKILL — h:0 s:0 v:1.2`
- **原版材质**：可切换使用游戏原始贴图做基准
- **输出**：
  - 导出 PNG（贴图文件）
  - 生成 `.tres`（Godot 资源描述）
  - 生成 **C# 代码**（卡牌定义代码骨架）

**坑/注意**：
- 卡框由 H/S/V 三参数决定，改卡牌外观时先在这里调参再导出，避免手写材质坐标。

---

## 2. 对话预览器（tools_dialogue-preview.txt）

**用途**：编辑并预览自定义角色的对话（dialogue）数据，输出 JSON 供 mod 使用。

- **角色（Character）字段**：
  - `名字 title`（显示名）
  - `绰号 epithet`（称号/副标题，如「特兹卡塔拉 / 饲火者」）
- **对话集（Dialogue Set）**：可 `＋ 新增` / `✕ 删除` 多个对话集
- **对话行（Dialogue Line）**：`＋ 添加一行`，逐行编辑
- **查看/导出**：
  - 按角色过滤：`仅展示当前角色`
  - 输出 **JSON**，一键 `复制`
- **预览区控件**：角色名+绰号展示、`▼ 点击继续`（模拟玩家点按推进）、`重置对话`、`访问次数`、行号指示 `第 X / Y 行`

**坑/注意**：
- 对话数据结构以角色为顶层（title/epithet），对话集为中间层，行为叶子；导出的 JSON 直接是 mod 对话资源格式。

---

## 3. ID 生成器（tools_id-generator.txt）

**用途**：批量生成 mod 内容 ID（含前置库前缀），输出 JSON。

- **输入项**：
  - `前置库`：`BaseLib` / `RitsuLib`（决定 ID 前缀约定）
  - `命名空间`（namespace）
  - `Mod ID`
  - `内容类别`（content category）
  - `类名`：手动输入，**每行一个**（如 `MyCoolCard`）
- **导入文件夹**：`递归读取文件夹及其所有子文件夹下的 .cs 文件名并追加为类名`，**自动忽略 `.uid` / `.import` 等元数据文件**
- **隐私**：文件仅在本地浏览器读取，**不会上传**
- **输出**：生成 `JSON`，一键 `复制`

**坑/注意**：
- 类名即 ID 来源：一个 .cs 文件 = 一个内容类名，命名要与 Godot 工程中的脚本文件名一致；
- 前置库选择影响最终 ID 格式，发布 mod 前务必选对依赖。

---

## 4. 文本预览器（tools_text-preview.txt）

**用途**：编辑卡牌/能力描述文本（富文本+变量+条件），实时预览渲染效果。

### 颜色标签（`[tag]…[/tag]`）
`[gold]` `[red]` `[blue]` `[green]` `[purple]` `[orange]` `[pink]` `[aqua]` `[color]` `[font]` `[font_size]`

### 文字效果
`[b]` 粗体、`[i]` 斜体、`[u]` 下划线、`[jitter]`、`[sine]`、`[fade_in]`、`[fly_in]`、`[thinky_dots]`、`[rainbow]`

### 占位变量（`{Var}`）
- 数值类：`{Damage}` `{Block}` `{Energy}` `{Cards}` `{Repeat}` `{Heal}` `{HpLoss}` `{MaxHp}` `{Gold}` `{CalculatedDamage}` `{CalculatedBlock}` `{Summon}` `{Forge}` `{Stars}` `{energyPrefix}`
- 力量/状态类：`{StrengthPower}` `{DexterityPower}` `{WeakPower}` `{VulnerablePower}` `{PoisonPower}` `{DoomPower}`

### 卡牌上下文（条件变量）
`{singleStarIcon}` `{InCombat}` `{IsTargeting}` `{OnTable}` `{IfUpgraded}`

### 能力上下文（Power 类描述用）
`{Amount}` `{OnPlayer}` `{IsMultiplayer}` `{PlayerCount}` `{OwnerName}` `{ApplierName}` `{TargetName}`

### 格式化器（`{Var:format()}` 后缀）
- `diff()`：差值显示（正负号）
- `inverseDiff()`：反向差值
- `energyIcons()` / `starIcons()`：能量/星级图标化
- `abs`：绝对值
- 条件三元：`cond`，形如 `{IfUpgraded:0?生效|不生效}`（条件成立显示左侧，否则右侧）
- `choose`、`percentMore()`、`percentLess()`、`plural`（复数）、`list`（列表连接）
- `变量注入`（variable injection）

### 关键代码模式（示例原文）
```
获得等量于与你当前[gold]弃牌堆[/gold]中牌数{IfUpgraded:show:+{CalculationBase}|}的[gold]格挡[/gold]值。{InCombat:\n（获得{CalculatedBlock:diff()}点[gold]格挡[/gold]）|}
```
拆解：
- `[gold]弃牌堆[/gold]`：颜色标签包词
- `{IfUpgraded:show:+{CalculationBase}|}`：升级后追加显示 `+{CalculationBase}`
- `{InCombat:\n（…）|}`：战斗内追加一行括号说明，`|` 为分支分隔
- `{CalculatedBlock:diff()}`：用 `diff()` 显示计算后格挡的差值

**坑/注意**：
- 条件分支语法为 `{条件:成立内容|不成立内容}`（无内容可留空），分隔符是 `|`；
- 格式化器作为 `:` 后的第二段，如 `{CalculatedBlock:diff()}`；
- 实时预览：`输入内容后实时预览`，改描述时在此验证再复制进代码。

---

## 速查总结（制作 mod 常用链）

1. 用 **ID 生成器**（选好 BaseLib/RitsuLib 前置）批量产出内容 ID JSON；
2. 用 **文本预览器** 写卡牌/能力描述文本（变量 + 条件 + 格式化器）并复制；
3. 用 **卡框预览器** 调 H/S/V 参数、选类型（含 Ancient 系），导出 PNG/.tres/C# 代码；
4. 用 **对话预览器** 编辑角色 title/epithet 与对话集，导出 JSON。
