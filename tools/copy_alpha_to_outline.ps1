# Copy alpha channel from processed even_match_icon.png to even_match_outline.png
# (same composition, grayscale version). Keeps outline's own RGB.
Add-Type -AssemblyName System.Drawing

$src = 'assets\relic_icons\even_match_icon.png'
$dst = 'assets\relic_icons\even_match_outline.png'

$srcBmp = New-Object System.Drawing.Bitmap((Join-Path (Get-Location) $src))
$dstBmp = New-Object System.Drawing.Bitmap((Join-Path (Get-Location) $dst))
$w = $srcBmp.Width; $h = $srcBmp.Height
Write-Output ("src {0}x{1} dst {2}x{3}" -f $w, $h, $dstBmp.Width, $dstBmp.Height)
if ($w -ne $dstBmp.Width -or $h -ne $dstBmp.Height) { Write-Output "SIZE MISMATCH"; exit 1 }

for ($y = 0; $y -lt $h; $y++) { for ($x = 0; $x -lt $w; $x++) {
    $s = $srcBmp.GetPixel($x, $y)
    $d = $dstBmp.GetPixel($x, $y)
    if ($s.A -ne $d.A) { $dstBmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($s.A, $d.R, $d.G, $d.B)) }
} }

$tmp = $dst + ".tmp.png"
$dstBmp.Save($tmp, [System.Drawing.Imaging.ImageFormat]::Png)
$dstBmp.Dispose(); $srcBmp.Dispose()
Move-Item -Force $tmp $dst
Write-Output "DONE"
