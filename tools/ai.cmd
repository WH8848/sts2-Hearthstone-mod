@echo off
rem ============================================================
rem  ai.cmd -- wrapper for tools\ai.ps1 (ASCII only, cmd hates UTF-8)
rem  usage:  tools\ai.cmd api CardModel
rem          tools\ai.cmd verify
rem          tools\ai.cmd ctx -Task "one line task"
rem ============================================================
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0ai.ps1" %*
