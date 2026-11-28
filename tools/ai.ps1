#Requires -Version 5.1
<#
  ai.ps1 -- Jaina Mod 的 AI 协作工具（工作流的"手"）

  子命令，对应工作流的三道闸：

    doctor            环境体检：版本/路径/构建产物是否一致
    api <名字>         闸1 事实闸：查 sts2.xml + 反编译源码，给出 文件:行号 证据
    verify            闸2+闸3：构建 -> 校验部署产物 -> 扫日志
    ctx -Task "..."   生成给 AI 的上下文包（粘进 DeepSeek/ChatGPT 就能开工）
    log [-Tail 80]    看游戏日志（可 -Pattern 过滤）

  示例：
    .\tools\ai.ps1 api PlayerCombatState
    .\tools\ai.ps1 verify
    .\tools\ai.ps1 ctx -Task "给冰霜新星加冻结" -Files Scripts/Character/Cards/Frost/FrostNovaCard.cs
    .\tools\ai.ps1 log -Tail 200 -Pattern "Exception|Jaina"
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('doctor', 'api', 'verify', 'ctx', 'log')]
    [string]$Command = 'doctor',

    [Parameter(Position = 1)]
    [string]$Pattern = '',

    [string]$Task = '',
    [string[]]$Files = @(),
    [int]$Tail = 80,
    [int]$MaxFileChars = 12000,
    [int]$MaxHits = 40
)

$ErrorActionPreference = 'Stop'

# ---------- 常量（与 AGENTS.md 第 2 节保持一致；改这里就改那里） ----------
$Root       = Split-Path -Parent $PSScriptRoot
$GameDir    = 'E:\SteamLibrary\steamapps\common\Slay the Spire 2'
$DataDir    = Join-Path $GameDir 'data_sts2_windows_x86_64'
$ModDir     = Join-Path $GameDir 'mods\Jaina'
$DecompRoot = 'E:\MOD\sts2\sts2'
$LogPath    = Join-Path $env:APPDATA 'SlayTheSpire2\logs\godot.log'
$CtxDir     = Join-Path $Root 'docs\_ctx'
$Utf8NoBom  = New-Object System.Text.UTF8Encoding($false)
$TICK       = [string][char]96
$FENCE      = $TICK * 3

function Write-Head([string]$Text) {
    Write-Host ''
    Write-Host (('=' * 4) + ' ' + $Text + ' ' + ('=' * 4)) -ForegroundColor Cyan
}
function Write-Ok([string]$Text)   { Write-Host "  [OK]   $Text" -ForegroundColor Green }
function Write-Bad([string]$Text)  { Write-Host "  [!!]   $Text" -ForegroundColor Red }
function Write-Info([string]$Text) { Write-Host "  [ ]    $Text" -ForegroundColor Gray }

function Get-DecompVersion {
    if (-not (Test-Path $DecompRoot)) { return $null }
    Get-ChildItem $DecompRoot -Directory |
        Where-Object { $_.Name -match '^\d+\.\d+(\.\d+)*$' } |
        Sort-Object { try { [version]$_.Name } catch { [version]'0.0.0' } } |
        Select-Object -Last 1
}

function Read-TextFile([string]$Path) {
    if (-not (Test-Path $Path)) { return $null }
    [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
}

# ============================== doctor ==============================
function Invoke-Doctor {
    Write-Head 'doctor - 环境体检'

    $verDir = Get-DecompVersion
    Write-Info ("项目根        " + $Root)
    Write-Info ("游戏目录      " + $GameDir + $(if (Test-Path $GameDir) { '' } else { '   <缺失>' }))
    $dll = Join-Path $DataDir 'sts2.dll'
    Write-Info ("主程序集      " + $dll + $(if (Test-Path $dll) { '' } else { '   <缺失>' }))
    $xml = Join-Path $DataDir 'sts2.xml'
    Write-Info ("API 字典      " + $xml + $(if (Test-Path $xml) { '' } else { '   <缺失>' }))
    if ($verDir) { Write-Info ("反编译源码    最新 " + $verDir.Name + "  ->  " + (Join-Path $verDir.FullName 'src')) }
    else { Write-Bad '反编译源码目录不存在，闸1 无法执行' }
    Write-Info ("部署目录      " + $ModDir)

    Write-Head '构建产物'
    foreach ($f in @('Jaina.dll', 'Jaina.json', 'Jaina.pck')) {
        $p = Join-Path $ModDir $f
        if (Test-Path $p) {
            $i = Get-Item $p
            Write-Ok ('{0,-12} {1,12:N0} B   {2}' -f $i.Name, $i.Length, $i.LastWriteTime.ToString('MM-dd HH:mm:ss'))
        }
        else { Write-Bad "$f 不存在（还没构建过？）" }
    }

    Write-Head '工程事实（以 csproj 为准）'
    $csproj = Join-Path $Root 'Jaina.csproj'
    if (Test-Path $csproj) {
        $proj = [System.IO.File]::ReadAllText($csproj, [System.Text.Encoding]::UTF8)
        foreach ($m in [regex]::Matches($proj, '<(TargetFramework|LangVersion)>(.*?)</\1>')) {
            Write-Info ($m.Groups[1].Value.PadRight(30) + $m.Groups[2].Value)
        }
        foreach ($m in [regex]::Matches($proj, 'PackageReference\s+Include="([^"]+)"\s+Version="([^"]+)"')) {
            Write-Info ($m.Groups[1].Value.PadRight(30) + $m.Groups[2].Value)
        }
    }

    Write-Head 'git'
    Push-Location $Root
    try {
        Write-Info ("分支  " + (git rev-parse --abbrev-ref HEAD 2>&1))
        $dirty = @(git -c core.quotepath=false status --porcelain 2>&1)
        if ($dirty.Count -gt 0) { Write-Bad ("工作区有 " + $dirty.Count + " 项未提交改动（大改前先提交或 stash）") }
        else { Write-Ok '工作区干净' }
        git log --oneline -3 2>&1 | ForEach-Object { Write-Info $_ }
    }
    finally { Pop-Location }

    Write-Head '日志'
    if (Test-Path $LogPath) {
        $i = Get-Item $LogPath
        Write-Ok ('godot.log  ' + $i.LastWriteTime.ToString('MM-dd HH:mm:ss') + '  ' + ('{0:N0}' -f $i.Length) + ' B')
    }
    else { Write-Bad "日志不存在：$LogPath" }
}

# ============================== api ==============================
function Invoke-Api {
    if ([string]::IsNullOrWhiteSpace($Pattern)) {
        throw '用法: .\tools\ai.ps1 api <类型名或成员名>'
    }
    Write-Head "闸1 事实闸 - 查证 '$Pattern'"

    $hits = 0

    $xmlPath = Join-Path $DataDir 'sts2.xml'
    if (Test-Path $xmlPath) {
        $xmlHits = @(Select-String -Path $xmlPath -Pattern $Pattern -SimpleMatch -ErrorAction SilentlyContinue | Select-Object -First $MaxHits)
        if ($xmlHits.Count -gt 0) {
            Write-Host "  [sts2.xml] $($xmlHits.Count) 处 -- 符号存在" -ForegroundColor Green
            foreach ($h in $xmlHits) { Write-Host ('    L{0,-7} {1}' -f $h.LineNumber, $h.Line.Trim()) }
            $hits += $xmlHits.Count
        }
        else {
            Write-Host '  [sts2.xml] 0 处 -- 字典里没有这个名字' -ForegroundColor Yellow
        }
    }

    $verDir = Get-DecompVersion
    if ($verDir) {
        $srcDir = Join-Path $verDir.FullName 'src'
        if (Test-Path $srcDir) {
            Write-Host "  [源码 $($verDir.Name)] 搜索中..." -ForegroundColor DarkGray
            $sw = [Diagnostics.Stopwatch]::StartNew()
            $files = Get-ChildItem $srcDir -Recurse -Filter *.cs -File -ErrorAction SilentlyContinue
            $srcHits = @($files | Select-String -Pattern $Pattern -SimpleMatch -ErrorAction SilentlyContinue | Select-Object -First $MaxHits)
            $sw.Stop()
            if ($srcHits.Count -gt 0) {
                Write-Host "  [源码] $($srcHits.Count) 处（$($files.Count) 个文件，$([math]::Round($sw.Elapsed.TotalSeconds,1))s）" -ForegroundColor Green
                foreach ($h in $srcHits) {
                    $rel = $h.Path.Substring($srcDir.Length).TrimStart('\')
                    Write-Host ('    {0}:{1}' -f $rel, $h.LineNumber)
                    Write-Host ('        ' + $h.Line.Trim())
                }
                $hits += $srcHits.Count
            }
            else {
                Write-Host '  [源码] 0 处' -ForegroundColor Yellow
            }
        }
    }

    Write-Host ''
    if ($hits -gt 0) {
        Write-Host '  -> 结论：符号存在。引用时写上 文件:行号，写进 commit 或注释。' -ForegroundColor Green
    }
    else {
        Write-Host '  -> 结论：查无此符号。硬规则 A：不许写。换个名字重查，或先写 3 行反射探针。' -ForegroundColor Red
    }
}

# ============================== verify ==============================
function Invoke-Verify {
    Write-Head '闸2 编译闸 - dotnet build -c Debug'
    $t0 = Get-Date
    Push-Location $Root
    try {
        $buildLog = & dotnet build -c Debug --nologo 2>&1
        $code = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    $errors = @($buildLog | Where-Object { $_ -match ':\s*error\s' })
    $warns  = @($buildLog | Where-Object { $_ -match ':\s*warning\s' })

    if ($code -eq 0) {
        Write-Ok "编译通过（$([math]::Round(((Get-Date) - $t0).TotalSeconds,1))s，warning $($warns.Count) 条）"
        foreach ($w in ($warns | Select-Object -First 10)) { Write-Info ($w.ToString().Trim()) }
    }
    else {
        Write-Bad "编译失败（exit $code），错误 $($errors.Count) 条："
        foreach ($e in ($errors | Select-Object -First 25)) { Write-Host ('    ' + $e.ToString().Trim()) -ForegroundColor Red }
        Write-Host ''
        Write-Host '  -> 闸2 未通过，不要往下走。先修编译。' -ForegroundColor Red
        return
    }

    Write-Head '闸2.5 部署校验'
    foreach ($f in @('Jaina.dll', 'Jaina.pck')) {
        $p = Join-Path $ModDir $f
        if (-not (Test-Path $p)) { Write-Bad "$f 未生成"; continue }
        $i = Get-Item $p
        if ($i.LastWriteTime -lt $t0.AddSeconds(-3)) {
            Write-Bad ("$f 时间戳陈旧（" + $i.LastWriteTime.ToString('HH:mm:ss') + '），构建可能没真正部署')
        }
        else { Write-Ok ("$f 已更新 " + $i.LastWriteTime.ToString('HH:mm:ss')) }
    }

    Write-Head '闸3 运行闸 - 日志扫描'
    if (-not (Test-Path $LogPath)) { Write-Bad "日志不存在：$LogPath（先进一次游戏）"; return }
    $li = Get-Item $LogPath
    Write-Info ('godot.log 最后写入 ' + $li.LastWriteTime.ToString('MM-dd HH:mm:ss'))
    if ($li.LastWriteTime -lt $t0) {
        Write-Bad '日志比这次构建还旧 -- 你还没启动过游戏，闸3 没做'
        Write-Host '  -> 操作：启动游戏 -> 进一局 -> 退出 -> 再跑一次 verify' -ForegroundColor Yellow
        return
    }
    $tailLines = Get-Content $LogPath -Tail 400
    $bad = @($tailLines | Where-Object { $_ -match 'ERROR|Exception|FAILED|StateDivergence|turn loop died' })
    if ($bad.Count -eq 0) { Write-Ok '最近 400 行无 ERROR/Exception/断联' }
    else {
        Write-Bad "最近 400 行里有 $($bad.Count) 条可疑："
        foreach ($b in ($bad | Select-Object -Last 20)) { Write-Host ('    ' + $b.Trim()) -ForegroundColor Yellow }
    }
    Write-Host ''
    Write-Host '  -> 闸3 通过还需要：游戏内功能可见 + 联机两端一致（见 docs/AI-WORKFLOW.md 第 4 节）' -ForegroundColor Gray
}

# ============================== log ==============================
function Invoke-Log {
    if (-not (Test-Path $LogPath)) { throw "日志不存在：$LogPath" }
    $lines = Get-Content $LogPath -Tail $Tail
    if (-not [string]::IsNullOrWhiteSpace($Pattern)) {
        $lines = $lines | Where-Object { $_ -match $Pattern }
    }
    $lines | ForEach-Object { Write-Host $_ }
}

# ============================== ctx ==============================
function Invoke-Ctx {
    if (-not (Test-Path $CtxDir)) { New-Item -ItemType Directory -Path $CtxDir -Force | Out-Null }

    $sb = New-Object System.Text.StringBuilder
    function Add-Line([string]$Text) { [void]$script:SB.AppendLine($Text) }
    $script:SB = $sb

    $verDir = Get-DecompVersion
    $verName = if ($verDir) { $verDir.Name } else { '(未知)' }

    Add-Line '# 上下文包 · Jaina Mod'
    Add-Line ''
    Add-Line ('> 由 ' + $TICK + 'tools/ai.ps1 ctx' + $TICK + ' 生成。粘贴给 AI 即可开工，不需要它猜环境。')
    Add-Line ''

    Add-Line '## 1. 任务'
    Add-Line ''
    if ([string]::IsNullOrWhiteSpace($Task)) {
        Add-Line ('（未填写。先用 ' + $TICK + 'docs/templates/TASK-CARD.md' + $TICK + ' 写一张任务卡，再 -Task "一句话"）')
    }
    else {
        Add-Line $Task
    }
    Add-Line ''

    Add-Line '## 2. 项目与环境（已实测，不要猜、不要再问路径）'
    Add-Line ''
    Add-Line '| 项 | 值 |'
    Add-Line '|---|---|'
    Add-Line ('| 项目根 | ' + $TICK + $Root + $TICK + ' |')
    Add-Line ('| 游戏版本 | ' + $verName + ' |')
    Add-Line ('| 主程序集 | ' + $TICK + $DataDir + '\sts2.dll' + $TICK + ' |')
    Add-Line ('| API 字典 | ' + $TICK + $DataDir + '\sts2.xml' + $TICK + '（grep 即可确认成员是否存在） |')
    Add-Line ('| 反编译源码 | ' + $TICK + (Join-Path $verDir.FullName 'src') + $TICK + ' |')
    Add-Line '| 运行时 | .NET 10（csproj: net10.0）+ Godot 4.5.1 mono |'
    Add-Line '| 框架 | RitsuLib 0.5.12、MinionLib 0.6.2 |'
    Add-Line ('| 部署目录 | ' + $TICK + $ModDir + $TICK + ' |')
    Add-Line ('| 日志 | ' + $TICK + $LogPath + $TICK + ' |')
    Add-Line ''

    $agents = Join-Path $Root 'AGENTS.md'
    if (Test-Path $agents) {
        Add-Line '## 3. 硬规则（摘要，完整版见项目根 AGENTS.md）'
        Add-Line ''
        $text = Read-TextFile $agents
        $lines = $text -split '\r?\n'
        $inRule = $false
        foreach ($l in $lines) {
            if ($l -match '^##\s*3\.') { $inRule = $true; continue }
            if ($inRule -and $l -match '^##\s*4\.') { break }
            if ($inRule -and $l.Trim()) { Add-Line $l }
        }
        Add-Line ''
    }

    Add-Line '## 4. 当前状态'
    Add-Line ''
    Push-Location $Root
    try {
        Add-Line $FENCE
        Add-Line ('分支: ' + (git rev-parse --abbrev-ref HEAD 2>&1))
        Add-Line '最近提交:'
        git log --oneline -5 2>&1 | ForEach-Object { Add-Line ('  ' + $_) }
        $dirty = @(git -c core.quotepath=false status --porcelain 2>&1)
        if ($dirty.Count -gt 0) {
            Add-Line '未提交改动:'
            $dirty | Select-Object -First 20 | ForEach-Object { Add-Line ('  ' + $_) }
        }
        Add-Line $FENCE
    }
    finally { Pop-Location }
    Add-Line ''

    if ($Files.Count -gt 0) {
        Add-Line '## 5. 相关文件'
        Add-Line ''
        foreach ($f in $Files) {
            $p = if ([System.IO.Path]::IsPathRooted($f)) { $f } else { Join-Path $Root $f }
            if (-not (Test-Path $p)) { Add-Line ('### ' + $f + ' （不存在）'); continue }
            $content = Read-TextFile $p
            $truncated = $false
            if ($content.Length -gt $MaxFileChars) {
                $content = $content.Substring(0, $MaxFileChars)
                $truncated = $true
            }
            $ext = [System.IO.Path]::GetExtension($p).TrimStart('.')
            if ($ext -eq 'cs') { $ext = 'csharp' } elseif ($ext -eq 'ps1') { $ext = 'powershell' }
            Add-Line ('### ' + $f)
            Add-Line ''
            Add-Line ($FENCE + $ext)
            Add-Line $content.TrimEnd()
            Add-Line $FENCE
            if ($truncated) { Add-Line ('> （已截断，需要更多请单独要）') }
            Add-Line ''
        }
    }

    Add-Line '## 6. 输出要求'
    Add-Line ''
    Add-Line '1. 先复述任务，列出你要用到但**还没查证**的 API；不要直接写代码。'
    Add-Line ('2. 每个外部符号必须给 ' + $TICK + '文件:行号' + $TICK + ' 证据（可用 ' + $TICK + 'tools/ai.ps1 api <名字>' + $TICK + ' 自证）。')
    Add-Line '3. 改动给完整文件或精确 diff，不要「省略中间部分」。'
    Add-Line '4. 不许说「已测试」——你无法运行游戏；把验证步骤列成清单交给人类执行。'
    Add-Line ''

    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $slug = if ([string]::IsNullOrWhiteSpace($Task)) { 'ctx' } else { ($Task -replace '[\\/:*?"<>|\s]+', '-') }
    if ($slug.Length -gt 24) { $slug = $slug.Substring(0, 24) }
    $outPath = Join-Path $CtxDir ($stamp + '-' + $slug + '.md')
    [System.IO.File]::WriteAllText($outPath, $sb.ToString(), $Utf8NoBom)

    Write-Head '上下文包已生成'
    Write-Ok $outPath
    Write-Info ("大小 " + ('{0:N0}' -f (Get-Item $outPath).Length) + ' B')
    Write-Host '  -> 用记事本/VSCode 打开，整份粘进 DeepSeek 或 ChatGPT。'
}

# ============================== dispatch ==============================
switch ($Command) {
    'doctor' { Invoke-Doctor }
    'api'    { Invoke-Api }
    'verify' { Invoke-Verify }
    'log'    { Invoke-Log }
    'ctx'    { Invoke-Ctx }
}
