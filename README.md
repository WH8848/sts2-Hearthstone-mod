# Jaina — 杀戮尖塔2（Slay the Spire 2）吉安娜 MOD

将《炉石传说》吉安娜/法师体系移植进《杀戮尖塔2》的角色 MOD（Godot 4.5 + C#/.NET，基于 RitsuLib / MinionLib）。

## 功能特性

- **随从系统**：炉石式随从站场、攻击意图、随从槽位（上限 7 个）、战吼/亡语/冲锋/冻结/吸血等关键词
- **法术派系**：火焰 / 冰霜 / 奥术 / 暗影（施放记录、派系任务、派系重放）
- **炉石关键词**：双生法术、灌注、压轴、重放、发现、微缩/微型、交易、虚无、保留、固有、消耗、任务、免疫、地标、疲劳
- **任务线**：巫师的计策 → 拖延时间 → 抵达传送大厅 → 奥术师晨拥（升级形态）
- **武器位**：炉石式武器槽（艾露尼斯、金属探测器、魔法智慧之球等，攻击力/耐久度/武器亡语）
- **地标系统**：潮汐之池、小玩物小屋（每两个回合可使用，耐久度机制）
- **英雄卡**：冰霜女巫吉安娜、魔导师晨拥（获得护甲、替换英雄技能）
- **英雄技能**：火焰冲击（可无限升级）、灌注/野火强化、考达拉幼龙（1费任意次数）
- 40+ 张随从、50+ 张法术/能力卡，全部使用炉石官方原画（wiki.gg）

## 安装

1. 依赖：安装 [RitsuLib](https://github.com/WH8848) 与 MinionLib（游戏 mod 目录）
2. 将本仓库的 `jaina/` 资源与构建产物部署到：
   `Slay the Spire 2/mods/Jaina/`
3. 启动游戏，选择角色「吉安娜」

## 构建

```bash
dotnet build -c Debug
```

构建自动部署 dll / pck 到游戏 `mods/Jaina/` 目录。

## 本地化

- `jaina/localization/zhs/` — 简体中文
- `jaina/localization/eng/` — English

术语与杀戮尖塔2官方中文本地化保持一致（费用/格挡/力量/牌组/抽牌堆等）。

## 文档

- `MODDING-QUICKREF.md` — MOD 开发快速参考
- `docs/` — 审计与技术文档

## 致谢

- 卡图来源：[Hearthstone Wiki (wiki.gg)](https://hearthstone.wiki.gg/)（炉石传说官方原画）
- 框架：[RitsuLib](https://www.nuget.org/packages/sts2.ritsulib)、MinionLib

## 许可

[MIT License](LICENSE)
