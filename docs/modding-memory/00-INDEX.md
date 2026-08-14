# STS2 MOD 开发 Memory 总索引

> **做 mod 前先看本目录**。三份知识源（RitsuLib 源码 / MinionLib 源码 / tutorials.sts2modding.com 全站 82 页）已消化为结构化笔记。
> 原文与源码位置：教程站抓取 `E:\MOD\sts2\tutorials-site\`（text\ 下 82 个 txt）；RitsuLib 源码 `E:\MOD\sts2\STS2-RitsuLib\`；MinionLib 源码 `E:\MOD\sts2\MinionLib\`；游戏反编译 sts2.dll 见 `%TEMP%\sts2-*`。

## 速查表

| 需求 | 看哪份 |
|---|---|
| 环境/项目搭建/mod.json/csproj/Entry/构建导出 | `01-tutorials/g1` |
| 选择 BaseLib vs RitsuLib；迁移表 | `01-tutorials/g1`（choose-base-library、migration-baselib-to-ritsulib） |
| 版本迁移（0.99→0.110 API 变更，**游戏升级前必看**） | `01-tutorials/g1`（migration-99-100） |
| 加卡/遗物/药水/能力/附魔/充能球/先古（RitsuLib 模板基类） | `02-ritsulib/r1` + `01-tutorials/g3` |
| 注册特性大全（[RegisterCard] 等全部 Attribute） | `02-ritsulib/r2` |
| 内容注册流程（ModContentRegistry/包构建） | `02-ritsulib/r3` |
| 遗物/模型/数据（含欧洛巴斯之触、古老牙齿、尘封魔典 API） | `02-ritsulib/r4` |
| 生命周期事件/封装 patch 系统/关键词/本地化 | `02-ritsulib/r5` |
| 牌堆操作（CardPileCmd）/卡牌属性/卡标签 | `02-ritsulib/r6` |
| 战斗/交互/UI 节点 API | `02-ritsulib/r7` |
| 工具/存档/快捷键/顶栏/时间线/网络 | `02-ritsulib/r8` |
| **随从系统**：MinionModel/MinionCmd/召唤/布局/死亡清理 | `03-minionlib/m1` |
| 随从行动点（ActionModel/点击触发/联机队列） | `03-minionlib/m2` |
| 自定义目标类型（AnyCreature/Union 等） | `03-minionlib/m3` |
| 随从组件（卡牌组件/时机）/布局/右键菜单 | `03-minionlib/m4` |
| 加新人物（三个池/资源/tscn 结构/本地化/先古对话） | `01-tutorials/g4`（04-14） |
| 卡图/皮肤替换/描述变量/控制台/热重载/先古对话格式 | `01-tutorials/g8` |
| 动画/VFX/粒子/着色器/原生特效/Harmony patch | `01-tutorials/g9` |
| 工坊上传/启动参数/本地联机 | `01-tutorials/g1`（upload-workshop、launch-arguments） |
| 教程站自带工具（卡框预览/对话预览/ID 生成/文本预览） | `01-tutorials/g10` |

## 文件清单

### 01-tutorials/（教程站精华，11 片）
- `g1-env-migration-publish.md`：环境/安装看源码/选库/新人物(BaseLib)/迁移/工坊/启动参数
- `g2a-baselib-cards.md`：BaseLib 加卡/配置/遗物/卡属性/能力/药水/先古/充能球
- `g2b-baselib-advanced.md`：BaseLib 局内保存/联动/怪物/事件/附魔/单例/手牌上限
- `g3-ritsulib-core.md`：RitsuLib 加卡/配置/遗物/卡属性/能力/药水/先古/充能球
- `g4-ritsulib-advanced.md`：RitsuLib 时间线/音频/怪物/事件/附魔/新人物/卡池/角色动画/单例
- `g5-ritsulib-ui.md`：血条覆盖层/数据保存/能力/手牌上限/手牌发光/自定义牌堆/顶栏按钮
- `g6-ritsulib-tools.md`：通知/徽章/常用工具/局内数据/自定义奖励/自定义目标/右键/次级资源
- `g7-ritsulib-systems.md`：生命周期事件/patch 系统/更新检查/运行时快捷键/内容注册表/节点挂载/遥测/mod 集成
- `g8-art-debug.md`：卡图皮肤/描述变量/美术风格/控制台命令/快速调试热重载/先古对话
- `g9-vfx-patch.md`：帧动画/图集/VFX 实例化/粒子/世界环境/着色器/游戏内建特效/Harmony patch 详解
- `g10-tools.md`：教程站四个工具页

### 02-ritsulib/（RitsuLib 源码 API 笔记，8 片）
- `r1-scaffolding-templates.md`：模板基类（ModCardTemplate/ModRelicTemplate/ModPowerTemplate/ModPotionTemplate/ModCharacterTemplate/ModEnchantmentTemplate/ModEncounterTemplate/ModAncientEventTemplate/ModSingletonTemplate 等）
- `r2-interop-attributes.md`：全部自动注册特性清单（AutoRegistration）
- `r3-content-registry.md`：ModContentRegistry/ContentRegistrationEntries/ModContentPackBuilder
- `r4-relics-models-data.md`：Relics（含 OrobasAncientUpgradeRegistry）/Models/Data
- `r5-lifecycle-patching-keywords.md`：Lifecycle/Patching/Keywords/Localization
- `r6-cardpiles-cards.md`：CardPiles/Cards/CardTags
- `r7-combat-interactions-ui.md`：Combat/Interactions/Ui（80KB，最厚）
- `r8-utils-rest.md`：Utils/RunData/Saves/RuntimeInput/TopBar/Timeline/Updates/Unlocks/Networking

### 03-minionlib/（MinionLib 源码笔记，4 片）
- `m1-core.md`：MinionModel/MinionCmd/MinionAnimCmd/初始化/工具（布局/辉光/描述后处理）
- `m2-action.md`：ActionModel 行动点系统/点击补丁/联机动作队列/Powers
- `m3-targeting.md`：自定义目标类型体系（CustomTargetTypeManager/AnyCreature/Union 等）
- `m4-component-layout.md`：卡牌组件系统（Component 37 文件）/布局/右键菜单

## 本项目（Jaina）已确认的关键事实
- 游戏版本 0.111.0；RitsuLib NuGet 0.5.11（含 RegisterTouchOfOrobasRefinementMapping 等全部先古 API）
- 随从为玩家侧宠物（Side=Player, IsPet=true, Monster: MinionModel）；指向法术需自定义目标类型（见 JainaTargetTypes.cs）
- 商店无 Power 候选黑屏 → MerchantPowerSlotPatch（能力槽改随从槽）
- 游戏 0.111 官方 bug：NetHostGameService.get_ConnectedPeers MissingMethodException（与本 mod 无关）
- 本地化文件用 Godot 校验（tools/validate_json.gd），PowerShell ConvertFrom-Json 解析不了含 {IfUpgraded:show:...} 的值
