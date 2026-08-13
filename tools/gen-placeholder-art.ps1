# ============================================================
# Jaina MOD 缺美术自动补图工具
# 用法: powershell -File gen-placeholder-art.ps1
# 作用: 扫描项目内所有 res://xxx.png 引用, 缺失的自动生成
#       250x190 占位卡图(冰蓝底+内容名+雪花图标), 并输出报告
# ============================================================
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = 'E:\MOD\sts2\godot_project\jaina'
$outDir = Join-Path $root 'assets\card_art'
$missing = @()

# 1) 收集所有 res:// 引用(仅 .png/.jpg, 排除游戏本体路径 src/ 和已存在文件)
$refs = Get-ChildItem $root -Recurse -Include '*.cs', '*.tscn' |
    Where-Object { $_.FullName -notmatch '\\(\.git|\.godot|obj|bin)\\' } |
    Select-String -Pattern 'res://[^"'']+\.(png|jpg)' -AllMatches

foreach ($m in $refs) {
    foreach ($mm in $m.Matches) {
        $p = $mm.Value -replace '^res://', ''
        if ($p -match '^(src|\.godot)/') { continue }   # 游戏本体路径跳过
        $full = Join-Path $root ($p -replace '/', '\')
        if (-not (Test-Path $full)) {
            $missing += [PSCustomObject]@{
                路径   = $p
                引用处 = "$($m.Filename):$($m.LineNumber)"
            }
        }
    }
}

# 2) 去重
$missing = $missing | Sort-Object 路径 -Unique

if ($missing.Count -eq 0) {
    Write-Host "✅ 没有缺失的美术资源, 无需补图"
} else {
    Write-Host "发现 $($missing.Count) 个缺失资源, 生成占位图:"
}

foreach ($m in $missing) {
    $rel = $m.路径
    $name = [System.IO.Path]::GetFileNameWithoutExtension($rel)
    $dest = Join-Path $root ($rel -replace '/', '\')
    New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null

    # 250x190 冰蓝渐变底 + 雪花 + 名称文字
    $bmp = New-Object System.Drawing.Bitmap(250, 190)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    # 渐变底
    $rect = New-Object System.Drawing.Rectangle(0, 0, 250, 190)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 30, 58, 95),   # 深冰蓝
        [System.Drawing.Color]::FromArgb(255, 79, 163, 209), # 亮冰蓝
        45)
    $g.FillRectangle($brush, $rect)

    # 雪花图案(六角)
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(120, 255, 255, 255), 3)
    $cx = 125; $cy = 70
    for ($i = 0; $i -lt 6; $i++) {
        $angle = $i * 60 * [Math]::PI / 180
        $x2 = $cx + [Math]::Cos($angle) * 30
        $y2 = $cy + [Math]::Sin($angle) * 30
        $g.DrawLine($pen, $cx, $cy, $x2, $y2)
    }
    $g.DrawEllipse($pen, $cx - 6, $cy - 6, 12, 12)

    # 名称文字
    $font = New-Object System.Drawing.Font('Microsoft YaHei UI', 13, [System.Drawing.FontStyle]::Bold)
    $textBrush = [System.Drawing.Brushes]::White
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $textRect = New-Object System.Drawing.RectangleF(0, 130, 250, 50)
    $g.DrawString($name, $font, $textBrush, $textRect, $format)

    $g.Dispose()
    $bmp.Save($dest, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "  ✔ 已生成: $rel  (引用: $($m.引用处))"
}

Write-Host "完成"
