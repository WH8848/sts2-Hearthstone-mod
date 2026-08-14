@echo off
rem ============================================================
rem  Jaina MOD 一键构建：编译 + 自动更新 mods 里的 dll/pck/本地化
rem  双击运行或命令行执行；完成后自动打开游戏 mods 目录
rem ============================================================
cd /d "%~dp0"
echo Building Jaina mod (Debug)...
dotnet build -c Debug
if errorlevel 1 (
  echo.
  echo [BUILD FAILED] See errors above.
  pause
  exit /b 1
)
echo.
echo [OK] Build succeeded. DLL and PCK updated:
explorer "E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\Jaina"
echo Restart the game to load the new mod files.
pause
