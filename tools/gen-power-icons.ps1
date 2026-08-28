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

function Draw-FrostLichJaina {
    param($g)
    New-IconBase $g
    # Snowflake + blood drop (Frost Lich Jaina: elemental lifesteal aura)
    $pen = [System.Drawing.Pen]::new($iceBlue, 5)
    $g.DrawLine($pen, 64, 26, 64, 66)
    $g.DrawLine($pen, 44, 40, 84, 40)
    $g.DrawLine($pen, 48, 28, 80, 52)
    $g.DrawLine($pen, 80, 28, 48, 52)
    $redPen = [System.Drawing.Pen]::new($red, 6)
    $pts = @()
    $pts += New-Object System.Drawing.PointF(64, 108)
    $pts += New-Object System.Drawing.PointF(86, 82)
    $pts += New-Object System.Drawing.PointF(86, 92)
    $pts += New-Object System.Drawing.PointF(42, 92)
    $pts += New-Object System.Drawing.PointF(42, 82)
    $g.DrawPolygon($redPen, $pts)               # 吸血水滴
    $pen.Dispose(); $redPen.Dispose()
}

function Draw-StargazingReplay {
    param($g)
    New-IconBase $g
    # 双箭头回环（重放：打出后自动重放一次）
    $pen = [System.Drawing.Pen]::new($iceBlue, 5)
    $g.DrawArc($pen, 30, 40, 68, 48, 200, 140)  # 回环上半
    $g.DrawArc($pen, 30, 40, 68, 48, 20, 140)   # 回环下半
    $arrow = [System.Drawing.Pen]::new($white, 5)
    $g.DrawLine($arrow, 86, 40, 96, 40)
    $g.DrawLine($arrow, 96, 40, 88, 32)
    $g.DrawLine($arrow, 96, 40, 88, 48)
    $pen.Dispose(); $arrow.Dispose()
}

function Draw-KhadgarOrb {
    param($g)
    New-IconBase $g
    # 水晶球（魔法智慧之球：回合结束施放法师法术）
    $ball = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(90, 140, 220, 255))
    $g.FillEllipse($ball, 34, 30, 60, 60)
    $ball.Dispose()
    $pen = [System.Drawing.Pen]::new($white, 4)
    $g.DrawEllipse($pen, 34, 30, 60, 60)
    $spark = [System.Drawing.Pen]::new($gold, 4)
    $g.DrawLine($spark, 50, 42, 60, 54)
    $g.DrawLine($spark, 60, 54, 74, 46)
    $star = [System.Drawing.Pen]::new($gold, 4)
    $g.DrawLine($star, 48, 78, 80, 78)
    $g.DrawLine($star, 64, 66, 64, 90)
    $pen.Dispose(); $spark.Dispose(); $star.Dispose()
}

function Draw-Wildfire {
    param($g)
    New-IconBase $g
    # 火焰 + 上箭头（野火：英雄技能伤害永久增加）
    $pen = [System.Drawing.Pen]::new($flame, 6)
    $pts = @()
    $pts += New-Object System.Drawing.PointF(64, 30)
    $pts += New-Object System.Drawing.PointF(92, 76)
    $pts += New-Object System.Drawing.PointF(64, 62)
    $pts += New-Object System.Drawing.PointF(36, 76)
    $g.DrawPolygon($pen, $pts)              # 火焰
    $arrow = [System.Drawing.Pen]::new($gold, 5)
    $g.DrawLine($arrow, 64, 86, 64, 106)
    $g.DrawLine($arrow, 64, 106, 52, 94)
    $g.DrawLine($arrow, 64, 106, 76, 94)
    $pen.Dispose(); $arrow.Dispose()
}

function Draw-HearthstoneForm {
    param($g)
    New-IconBase $g
    # 卡牌 + 能量圆环（炉石形态：保留+消耗+十点能量）
    $pen = [System.Drawing.Pen]::new($white, 4)
    $g.DrawRectangle($pen, 40, 26, 48, 66)  # 卡牌
    $pen2 = [System.Drawing.Pen]::new($gold, 5)
    $g.DrawEllipse($pen2, 78, 34, 40, 40)   # 能量环
    $g.DrawLine($pen2, 98, 42, 98, 56)
    $g.DrawLine($pen2, 90, 49, 106, 49)
    $star = [System.Drawing.Pen]::new($iceBlue, 4)
    $g.DrawLine($star, 58, 62, 58, 74)
    $g.DrawLine($star, 52, 68, 64, 68)
    $pen.Dispose(); $pen2.Dispose(); $star.Dispose()
}

function Draw-IceBlock {
    param($g)
    New-IconBase $g
    # 冰晶盾牌（寒冰屏障：致命伤害防护 + 免疫）
    $pen = [System.Drawing.Pen]::new($iceBlue, 6)
    $pts = @()
    $pts += New-Object System.Drawing.PointF(64, 22)
    $pts += New-Object System.Drawing.PointF(102, 44)
    $pts += New-Object System.Drawing.PointF(102, 78)
    $pts += New-Object System.Drawing.PointF(64, 106)
    $pts += New-Object System.Drawing.PointF(26, 78)
    $pts += New-Object System.Drawing.PointF(26, 44)
    $g.DrawPolygon($pen, $pts)              # 冰盾
    $cross = [System.Drawing.Pen]::new($white, 4)
    $g.DrawLine($cross, 64, 42, 64, 88)
    $g.DrawLine($cross, 44, 58, 84, 58)
    $pen.Dispose(); $cross.Dispose()
}

function Draw-HeroPowerReplay {
    param($g)
    New-IconBase $g
    # double-arrow loop (replay 1) + flame (hero power)
    $pen = [System.Drawing.Pen]::new($flame, 5)
    $g.DrawArc($pen, 30, 40, 68, 48, 200, 140)  # loop top
    $g.DrawArc($pen, 30, 40, 68, 48, 20, 140)   # loop bottom
    $arrow = [System.Drawing.Pen]::new($white, 5)
    $g.DrawLine($arrow, 86, 40, 96, 40)
    $g.DrawLine($arrow, 96, 40, 88, 32)
    $g.DrawLine($arrow, 96, 40, 88, 48)
    # small central flame (hero power)
    $flameBrush = [System.Drawing.SolidBrush]::new($gold)
    $pts = @()
    $pts += New-Object System.Drawing.PointF(64, 58)
    $pts += New-Object System.Drawing.PointF(78, 84)
    $pts += New-Object System.Drawing.PointF(64, 74)
    $pts += New-Object System.Drawing.PointF(50, 84)
    $g.FillPolygon($flameBrush, $pts)
    $pen.Dispose(); $arrow.Dispose(); $flameBrush.Dispose()
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

Save-Icon 'jaina_power_minion_squad_power' (Get-Command Draw-MinionSquad)
Save-Icon 'jaina_power_freeze_power'       (Get-Command Draw-Freeze)
Save-Icon 'jaina_power_jaina_attack_action' (Get-Command Draw-AttackAction)
Save-Icon 'jaina_power_objection_power'    (Get-Command Draw-Objection)
Save-Icon 'jaina_power_counterspell_power' (Get-Command Draw-Counterspell)
Save-Icon 'jaina_power_aegwynn_legacy_power' (Get-Command Draw-AegwynnLegacy)
Save-Icon 'jaina_power_empower_power'         (Get-Command Draw-Empower)
Save-Icon 'jaina_power_jaina_weapon_power'        (Get-Command Draw-JainaWeaponPower)
Save-Icon 'jaina_power_jaina_weapon_attack_action' (Get-Command Draw-JainaWeaponAttackAction)
Save-Icon 'jaina_power_frost_lich_jaina_power'     (Get-Command Draw-FrostLichJaina)
Save-Icon 'jaina_power_stargazing_replay_power'    (Get-Command Draw-StargazingReplay)
Save-Icon 'jaina_power_khadgar_orb_power'          (Get-Command Draw-KhadgarOrb)
Save-Icon 'jaina_power_wildfire_power'              (Get-Command Draw-Wildfire)
Save-Icon 'jaina_power_hearthstone_form_power'      (Get-Command Draw-HearthstoneForm)
Save-Icon 'jaina_power_ice_block_power'              (Get-Command Draw-IceBlock)
Save-Icon 'jaina_power_hero_power_replay_power'      (Get-Command Draw-HeroPowerReplay)

Write-Host "完成: 7 个力量图标 -> $outDir"
