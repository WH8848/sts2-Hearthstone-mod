# ============================================================
# Jaina MOD 力量图标生成工具
# 用法: powershell -ExecutionPolicy Bypass -File gen-power-icons.ps1
# 作用: 为可见力量(Power)生成 128x128 程序绘制图标
#       输出到 assets/power_icons/ (会被 Godot 打进 pck)
# ============================================================
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = 'E:\MOD\sts2\godot_project\jaina'
$outDir = Join-Path $root 'assets\power_icons'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$iceBlue = [System.Drawing.Color]::FromArgb(255, 120, 200, 255)
$white   = [System.Drawing.Color]::FromArgb(255, 240, 245, 255)
$gold    = [System.Drawing.Color]::FromArgb(255, 255, 210, 110)
$red     = [System.Drawing.Color]::FromArgb(255, 255, 110, 110)
$flame   = [System.Drawing.Color]::FromArgb(255, 255, 160, 80)

function New-IconBase {
    param($g)
    $bg = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 24, 32, 48))
    $g.FillEllipse($bg, 4, 4, 120, 120)
    $bg.Dispose()
}

function Draw-IceBarrier {
    param($g)
    New-IconBase $g
    $pen = [System.Drawing.Pen]::new($iceBlue, 7)
    $pts = @()
    $pts += New-Object System.Drawing.PointF(64, 22)
    $pts += New-Object System.Drawing.PointF(100, 38)
    $pts += New-Object System.Drawing.PointF(100, 70)
    $pts += New-Object System.Drawing.PointF(64, 106)
    $pts += New-Object System.Drawing.PointF(28, 70)
    $pts += New-Object System.Drawing.PointF(28, 38)
    $g.DrawPolygon($pen, $pts)
    $pen2 = [System.Drawing.Pen]::new($white, 4)
    $g.DrawLine($pen2, 64, 46, 64, 82)
    $g.DrawLine($pen2, 48, 60, 80, 60)
    $g.DrawLine($pen2, 50, 92, 78, 92)
    $pen.Dispose(); $pen2.Dispose()
}

function Draw-MinionSquad {
    param($g)
    New-IconBase $g
    $brush = [System.Drawing.SolidBrush]::new($white)
    $dark = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 140, 160, 190))
    $g.FillEllipse($dark, 22, 36, 26, 26)
    $g.FillRectangle($dark, 28, 62, 14, 34)
    $g.FillEllipse($brush, 52, 30, 28, 28)
    $g.FillRectangle($brush, 58, 58, 16, 38)
    $g.FillEllipse($brush, 82, 40, 24, 24)
    $g.FillRectangle($brush, 87, 64, 14, 32)
    $dark.Dispose(); $brush.Dispose()
}

function Draw-Freeze {
    param($g)
    New-IconBase $g
    $pen = [System.Drawing.Pen]::new($iceBlue, 5)
    $cx = 64; $cy = 64; $r = 34
    for ($i = 0; $i -lt 6; $i++) {
        $ang = $i * 60 * [Math]::PI / 180
        $x2 = $cx + [Math]::Cos($ang) * $r
        $y2 = $cy + [Math]::Sin($ang) * $r
        $g.DrawLine($pen, $cx, $cy, $x2, $y2)
        $bx = $cx + [Math]::Cos($ang) * ($r * 0.55)
        $by = $cy + [Math]::Sin($ang) * ($r * 0.55)
        $pa = $ang + 25 * [Math]::PI / 180
        $pb = $ang - 25 * [Math]::PI / 180
        $g.DrawLine($pen, $bx, $by, $bx + [Math]::Cos($pa) * 12, $by + [Math]::Sin($pa) * 12)
        $g.DrawLine($pen, $bx, $by, $bx + [Math]::Cos($pb) * 12, $by + [Math]::Sin($pb) * 12)
    }
    $pen.Dispose()
}

function Draw-AttackAction {
    param($g)
    New-IconBase $g
    $pen = [System.Drawing.Pen]::new($white, 6)
    $g.DrawLine($pen, 40, 30, 96, 88)
    $g.DrawLine($pen, 96, 30, 40, 88)
    $g.DrawLine($pen, 30, 40, 34, 66)
    $g.DrawLine($pen, 94, 66, 98, 40)
    $pen2 = [System.Drawing.Pen]::new($gold, 5)
    $g.DrawEllipse($pen2, 50, 50, 28, 28)
    $pen.Dispose(); $pen2.Dispose()
}

function Draw-Objection {
    param($g)
    New-IconBase $g
    $pen = [System.Drawing.Pen]::new($red, 10)
    $g.DrawLine($pen, 64, 36, 64, 78)
    $g.DrawLine($pen, 64, 90, 64, 94)
    $pen2 = [System.Drawing.Pen]::new($white, 3)
    $g.DrawArc($pen2, 30, 26, 68, 68, 200, 140)
    $pen.Dispose(); $pen2.Dispose()
}

function Draw-Counterspell {
    param($g)
    New-IconBase $g
    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 190, 140, 255), 6)
    $g.DrawArc($pen, 30, 30, 68, 68, 0, 300)
    $pen2 = [System.Drawing.Pen]::new($white, 4)
    $g.DrawLine($pen2, 84, 30, 44, 94)
    $g.DrawLine($pen2, 96, 22, 106, 12)
    $pen.Dispose(); $pen2.Dispose()
}

function Draw-AegwynnLegacy {
    param($g)
    New-IconBase $g
    $pen = [System.Drawing.Pen]::new($gold, 6)
    $pts = @()
    $pts += New-Object System.Drawing.PointF(30, 88)
    $pts += New-Object System.Drawing.PointF(30, 50)
    $pts += New-Object System.Drawing.PointF(48, 66)
    $pts += New-Object System.Drawing.PointF(64, 38)
    $pts += New-Object System.Drawing.PointF(80, 66)
    $pts += New-Object System.Drawing.PointF(98, 50)
    $pts += New-Object System.Drawing.PointF(98, 88)
    $g.DrawPolygon($pen, $pts)
    $pen2 = [System.Drawing.Pen]::new($white, 3)
    $g.DrawLine($pen2, 36, 100, 92, 100)
    $pen.Dispose(); $pen2.Dispose()
}

function Draw-Empower {
    param($g)
    New-IconBase $g
    # flame + star rays (empower)
    $pen = [System.Drawing.Pen]::new($flame, 6)
    $g.DrawLine($pen, 64, 86, 64, 44)
    $g.DrawLine($pen, 64, 66, 44, 52)
    $g.DrawLine($pen, 64, 60, 84, 46)
    $pen2 = [System.Drawing.Pen]::new($gold, 4)
    for ($i = 0; $i -lt 8; $i++) {
        $ang = $i * 45 * [Math]::PI / 180
        $x2 = 64 + [Math]::Cos($ang) * 18
        $y2 = 30 + [Math]::Sin($ang) * 12
        $g.DrawLine($pen2, 64, 30, $x2, $y2)
    }
    $pen.Dispose(); $pen2.Dispose()
}

function Draw-UnfairGame {
    param($g)
    New-IconBase $g
    # dice + question mark (unfair game)
    $brush = [System.Drawing.SolidBrush]::new($white)
    $g.FillRectangle($brush, 40, 40, 48, 48)
    $dark = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 60, 70, 95))
    $g.FillEllipse($dark, 48, 48, 8, 8)
    $g.FillEllipse($dark, 72, 48, 8, 8)
    $g.FillEllipse($dark, 60, 60, 8, 8)
    $pen = [System.Drawing.Pen]::new($gold, 5)
    $g.DrawArc($pen, 88, 30, 16, 16, 200, 220)
    $g.DrawLine($pen, 96, 46, 96, 54)
    $g.DrawLine($pen, 96, 62, 96, 66)
    $brush.Dispose(); $dark.Dispose(); $pen.Dispose()
}

function Draw-JainaWeaponPower {
    param($g)
    New-IconBase $g
    # 战斧：斧头 + 斧柄
    $pen = [System.Drawing.Pen]::new($white, 6)
    $g.DrawLine($pen, 56, 40, 56, 98)          # 斧柄
    $pts = @()
    $pts += New-Object System.Drawing.PointF(56, 34)
    $pts += New-Object System.Drawing.PointF(100, 26)
    $pts += New-Object System.Drawing.PointF(104, 52)
    $pts += New-Object System.Drawing.PointF(56, 46)
    $g.DrawPolygon($pen, $pts)                  # 斧刃
    $pen2 = [System.Drawing.Pen]::new($gold, 4)
    $g.DrawEllipse($pen2, 44, 30, 22, 22)       # 耐久指示
    $g.DrawLine($pen2, 55, 34, 55, 48)
    $g.DrawLine($pen2, 49, 41, 61, 41)
    $pen.Dispose(); $pen2.Dispose()
}

function Draw-JainaWeaponAttackAction {
    param($g)
    New-IconBase $g
    # 攻击箭头 + 小斧头（武器攻击行动点）
    $pen = [System.Drawing.Pen]::new($white, 6)
    $g.DrawLine($pen, 36, 34, 96, 34)
    $g.DrawLine($pen, 96, 34, 78, 22)
    $g.DrawLine($pen, 96, 34, 78, 46)
    $pen2 = [System.Drawing.Pen]::new($gold, 4)
    $g.DrawEllipse($pen2, 46, 56, 34, 34)       # 武器攻击标记
    $g.DrawLine($pen2, 63, 62, 63, 84)
    $g.DrawLine($pen2, 63, 70, 46, 64)
    $g.DrawLine($pen2, 63, 66, 80, 60)
    $pen.Dispose(); $pen2.Dispose()
}

function Save-Icon {
    param($name, $drawFunc)
    $bmp = New-Object System.Drawing.Bitmap(128, 128)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    & $drawFunc $g
    $dest = Join-Path $outDir "$name.png"
    $bmp.Save($dest, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Host "  OK  $name.png"
}

Save-Icon 'jaina_power_ice_barrier_power'  (Get-Command Draw-IceBarrier)
Save-Icon 'jaina_power_minion_squad_power' (Get-Command Draw-MinionSquad)
Save-Icon 'jaina_power_freeze_power'       (Get-Command Draw-Freeze)
Save-Icon 'jaina_power_jaina_attack_action' (Get-Command Draw-AttackAction)
Save-Icon 'jaina_power_objection_power'    (Get-Command Draw-Objection)
Save-Icon 'jaina_power_counterspell_power' (Get-Command Draw-Counterspell)
Save-Icon 'jaina_power_aegwynn_legacy_power' (Get-Command Draw-AegwynnLegacy)
Save-Icon 'jaina_power_empower_power'         (Get-Command Draw-Empower)
Save-Icon 'jaina_power_unfair_game_power'     (Get-Command Draw-UnfairGame)
Save-Icon 'jaina_power_jaina_weapon_power'        (Get-Command Draw-JainaWeaponPower)
Save-Icon 'jaina_power_jaina_weapon_attack_action' (Get-Command Draw-JainaWeaponAttackAction)

Write-Host "完成: 7 个力量图标 -> $outDir"
