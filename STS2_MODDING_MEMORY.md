# Slay the Spire 2 Modding Memory

用于制作《杀戮尖塔2》模组时的持久参考入口。需要具体 API、示例或当前实现时，直接打开下面的在线文档和源码，不凭记忆猜测版本细节。

## 主要资料

- 中文制作指南：https://tutorials.sts2modding.com/
- RitsuLib 源码：https://github.com/BAKAOLC/STS2-RitsuLib
- RitsuLib 文档：https://sts2-ritsulib.ritsukage.com/

## 教程覆盖范围

教程站点面向 `public-beta`，内容覆盖环境配置、常见问题、基础库选择、变量与描述、调试与热重载、启动参数、控制台、Patch、触发顺序、工坊发布，以及 RitsuLib/BaseLib 的内容注册、玩法基底、模组工具、视觉与辅助工具。新增功能时按站点目录进入对应专题页。

## RitsuLib 参考要点

RitsuLib 是 STS2 模组的共享框架层，提供内容注册、生命周期事件、Harmony Patch、持久化、设置 UI、本地化、音频、运行时 UI、诊断和兼容性辅助。它与游戏 API、BaseLib 等并列协作。

常用入口包括：

- `RitsuLibFramework.CreateContentPack(modId)` 注册卡牌、关键词、时间线、卡池、顶栏按钮等内容
- `RitsuLibFramework.CreatePatcher(modId, patcherName)` 创建带诊断的 Patch
- `RitsuLibFramework.SubscribeLifecycle<TEvent>(...)` 订阅生命周期/时序事件
- `RitsuLibFramework.BeginModDataRegistration(modId)` 与 `GetDataStore(modId)` 保存档案或账号数据
- `RitsuLibFramework.GetRunSavedDataStore(modId)` 保存局内数据
- `RitsuLibFramework.RegisterModSettings(modId, configure)` 注册玩家设置页

最小内容包模式：

```csharp
RitsuLibFramework.CreateContentPack("MyMod")
    .Card<MyCardPool, MyStrike>()
    .Relic<MyRelicPool, MyStarterRelic>()
    .Apply();
```

## 依赖和版本

- 编译期通常引用 NuGet 包 `STS2.RitsuLib`
- 运行时在 `mod_manifest.json` 声明 `STS2-RitsuLib`
- 游戏 API 0.105.x 及更新版本使用对象形式依赖：`{ "id": "STS2-RitsuLib" }`
- 较旧 API 分支使用旧式字符串依赖：`"STS2-RitsuLib"`
- 面向稳定或旧 API 分支时使用匹配的 `STS2.RitsuLib.Compat.<api-version>` 包和运行时变体
- RitsuLib 运行时必须安装完整目录，不能只复制根 DLL
- 需要目录引用时参考仓库提供的 `RitsuLib.References.props`

## 选型提示

Minion、召唤物、随从卡或 Guardian 机制优先查看 MinionLib。RitsuLib 负责通用框架，不替代这个专用库。

## 使用规则

制作 STS2 模组时先查本文件，再按任务打开教程专题、RitsuLib 文档或仓库源码。仓库与教程处于持续更新中，涉及版本、公开 API、依赖格式或类名时以在线资料当前内容为准。

记录来源：

- https://tutorials.sts2modding.com/
- https://github.com/BAKAOLC/STS2-RitsuLib
