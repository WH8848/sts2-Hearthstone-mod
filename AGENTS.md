# 卡牌设计文档同步

修改游戏内卡牌时，同步更新 [腾讯文档「杀戮尖塔炉石mod_卡牌设计」的「吉安娜卡池」](https://docs.qq.com/sheet/DVVR6ZXhqUnFIZnNu?tab=BB08J2) 中对应的卡牌。

游戏中文卡面以 `jaina/localization/zhs/cards.json` 为准。按卡名匹配，更新此次改动涉及的字段，保留设计备注和其他工作表。卡牌表格的同步目标仅为腾讯文档。

# 构建和发布

每次修改 Mod 后，运行 `dotnet build Jaina.csproj -c Release --nologo`。构建会通过 Godot 的 `Windows Desktop` 预设重新导出 `Jaina.pck`，并生成配套的 `Jaina.dll` 和 `Jaina.json`。

用项目资源制作发布包。确认构建成功、PCK 能挂载且所含本地化是本次版本后，将同一构建的三个文件上传到 `WH8848/sts2-Hearthstone-mod` 的 GitHub Release，并核对已上传的资产。PCK 作为 Release 资产发布。

将发布的同一套文件部署到本机游戏实际读取的 `mods/Jaina/`。使用临时目录打包时，发布后也要核对游戏目录里的 DLL、PCK 和清单已更新。

# 腾讯文档同步效率

保持已登录的腾讯文档标签页，不重复打开页面。先按名称框定位目标范围，再把同一列的连续修改合并成一次多行粘贴。筛选只在定位阶段处理，写入时使用明确单元格地址。完成后只回读本次修改的连续范围和自动保存状态，不逐格重复截图或读取。
