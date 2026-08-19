# 批量生成 Jaina 关键词图标（64x64 PNG：冰蓝圆形徽章 + 中文首字）与 .import 文件
Add-Type -AssemblyName System.Drawing

$outDir = "E:\MOD\sts2\godot_project\jaina\assets\keyword_icons"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# 关键词 -> 显示字
$kw = @{
  Deathrattle='亡'; Charge='冲'; Freeze='冻'; Twinspell='双'; Empower='灌';
  Finisher='斩'; Battlecry='吼'; Replay='重'; HeroPower='英'; Spell='法';
  Fire='火'; Frost='冰'; Arcane='奥'; Shadow='暗'; Quest='任';
  Durability='耐'; Weapon='武'; Elemental='元'; Beast='野'; Dragon='龙';
  Undead='尸'; Demon='魔'; Draenei='德'; Naga='娜'; Pirate='海';
  Mech='机'; Lifesteal='吸'; Discover='探'; Immune='免'; Landmark='地';
  Fatigue='疲'; Miniaturize='缩'; Mini='微'; Tradeable='易'; ZeroCostMark='零'
}

$template = Get-Content "E:\MOD\sts2\godot_project\jaina\assets\power_icons\jaina_power_minion_squad_power.png.import" -Raw

function New-Uid {
  $chars = 'abcdefghijklmnopqrstuvwxyz0123456789'
  $s = -join (1..13 | ForEach-Object { $chars[(Get-Random -Maximum $chars.Length)] })
  return "uid://$s"
}

$size = 64
foreach ($name in $kw.Keys) {
  $glyph = $kw[$name]
  $bmp = New-Object System.Drawing.Bitmap($size, $size)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.Clear([System.Drawing.Color]::Transparent)

  # 圆形背景（深蓝渐变近似：径向画多层圆）
  $cx = $size / 2; $cy = $size / 2
  for ($r = 30; $r -ge 8; $r -= 2) {
    $t = ($r - 8) / 22.0
    $blue = [int](10 + (1 - $t) * 30)
    $col = [System.Drawing.Color]::FromArgb(255, 20, 40 + $blue, 90 + $blue)
    $brush = New-Object System.Drawing.SolidBrush($col)
    $g.FillEllipse($brush, $cx - $r, $cy - $r, $r * 2, $r * 2)
    $brush.Dispose()
  }
  # 冰蓝描边
  $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 120, 200, 255), 2.5)
  $g.DrawEllipse($pen, 2, 2, $size - 4, $size - 4)
  $pen.Dispose()

  # 中央字（微软雅黑）
  $font = New-Object System.Drawing.Font("Microsoft YaHei", 28, [System.Drawing.FontStyle]::Bold)
  $fmt = New-Object System.Drawing.StringFormat
  $fmt.Alignment = [System.Drawing.StringAlignment]::Center
  $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
  $brush2 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 225, 240, 255))
  $rect = New-Object System.Drawing.RectangleF(0, 2, $size, $size)
  $g.DrawString($glyph, $font, $brush2, $rect, $fmt)
  $brush2.Dispose(); $font.Dispose(); $fmt.Dispose(); $g.Dispose()

  $file = Join-Path $outDir ('keyword_' + $name + '.png')
  $bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()

  # .import（复制模板改 uid / hash / path）
  $bytes = [System.IO.File]::ReadAllBytes($file)
  $md5 = [System.Security.Cryptography.MD5]::Create()
  $hash = [System.BitConverter]::ToString($md5.ComputeHash($bytes)).Replace('-','').ToLower()
  $uid = New-Uid
  $import = $template -replace 'uid://[a-z0-9]+', $uid
  $ctexName = 'keyword_' + $name + '.png-' + $hash + '.ctex'
  $import = $import -replace 'jaina_power_minion_squad_power\.png-[a-f0-9]+\.ctex', $ctexName
  [System.IO.File]::WriteAllText(($file + '.import'), $import)
  Write-Host ('OK keyword_' + $name + '.png (' + $glyph + ') uid=' + $uid)
}
Write-Host "done"
