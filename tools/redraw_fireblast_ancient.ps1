# 重绘二级火焰冲击卡面原画（250x190：暗色背景 + 强化火焰球 + 冰蓝能量环 + 火花）
Add-Type -AssemblyName System.Drawing

$w = 250
$h = 190
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::FromArgb(255, 10, 14, 30))

$cx = 125
$cy = 95

# 背景径向光晕
for ($r = 120; $r -ge 10; $r -= 4) {
  $t = ($r - 10) / 110.0
  $alpha = [int](20 + (1 - $t) * 45)
  $col = [System.Drawing.Color]::FromArgb($alpha, 30, 60, 140)
  $br = New-Object System.Drawing.SolidBrush($col)
  $x0 = $cx - $r
  $y0 = $cy - $r
  $d = $r * 2
  $g.FillEllipse($br, $x0, $y0, $d, $d)
  $br.Dispose()
}

# 能量环（冰蓝双层）
for ($ring = 0; $ring -lt 2; $ring++) {
  $rr = 72 + $ring * 6
  $penW = 3 - $ring
  $penCol = [System.Drawing.Color]::FromArgb(180, 100, 190, 255)
  $pen = New-Object System.Drawing.Pen($penCol, $penW)
  $x0 = $cx - $rr
  $y0 = $cy - $rr
  $d = $rr * 2
  $g.DrawEllipse($pen, $x0, $y0, $d, $d)
  $pen.Dispose()
}

# 火焰球（橙黄红径向）
$fcx = 125
$fcy = 95
for ($r = 46; $r -ge 6; $r -= 2) {
  $t = ($r - 6) / 40.0
  $R = [int](255 - $t * 120)
  $G = [int](120 + (1 - $t) * 90)
  $B = [int](20 + (1 - $t) * 30)
  $col = [System.Drawing.Color]::FromArgb(255, $R, $G, $B)
  $br = New-Object System.Drawing.SolidBrush($col)
  $x0 = $fcx - $r
  $y0 = $fcy - $r
  $d = $r * 2
  $g.FillEllipse($br, $x0, $y0, $d, $d)
  $br.Dispose()
}

# 火焰球高光（左上）
$hlCol = [System.Drawing.Color]::FromArgb(120, 255, 240, 200)
$hl = New-Object System.Drawing.SolidBrush($hlCol)
$g.FillEllipse($hl, 107, 75, 22, 22)
$hl.Dispose()

# 火花粒子
$rand = New-Object System.Random(42)
for ($i = 0; $i -lt 40; $i++) {
  $ox = $rand.NextDouble() * 2 - 1
  $oy = $rand.NextDouble() * 2 - 1
  $px = $fcx + $ox * 90
  $py = $fcy + $oy * 70
  $size = 2 + $rand.Next(4)
  $warm = $rand.NextDouble()
  if ($warm -lt 0.6) {
    $col = [System.Drawing.Color]::FromArgb(200, 255, 160 + $rand.Next(80), 40 + $rand.Next(60))
  } else {
    $col = [System.Drawing.Color]::FromArgb(200, 120 + $rand.Next(80), 190, 255)
  }
  $br = New-Object System.Drawing.SolidBrush($col)
  $g.FillEllipse($br, $px, $py, $size, $size)
  $br.Dispose()
}

$g.Dispose()
$file = "E:\MOD\sts2\godot_project\jaina\assets\card_art\fireblast_ancient.png"
$bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "fireblast_ancient redrawn"
