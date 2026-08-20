# Grayscale outline cutout via bilinear background model (corners are dark gray gradient,
# subject is bright): erase pixels close to modeled background, keep largest component, feather.
param([int]$Tolerance = 50)

Add-Type -AssemblyName System.Drawing

$Path = 'assets\relic_icons\even_match_outline.png'
$bmp = New-Object System.Drawing.Bitmap((Join-Path (Get-Location) $Path))
$w = $bmp.Width; $h = $bmp.Height

$tl = $bmp.GetPixel(2, 2); $tr = $bmp.GetPixel($w - 3, 2)
$bl = $bmp.GetPixel(2, $h - 3); $br = $bmp.GetPixel($w - 3, $h - 3)

function Lerp([int]$a, [int]$b, [double]$t) { return [int]([double]$a + ($b - $a) * $t) }

$keep = New-Object 'bool[,]' $w, $h
for ($y = 0; $y -lt $h; $y++) {
    $ty = [double]$y / ($h - 1)
    for ($x = 0; $x -lt $w; $x++) {
        $tx = [double]$x / ($w - 1)
        $top = Lerp $tl.R $tr.R $tx
        $bot = Lerp $bl.R $br.R $tx
        $mR = Lerp $top $bot $ty
        $p = $bmp.GetPixel($x, $y)
        if ($p.A -lt 16) { continue }
        $dr = $p.R - $mR; $dg = $p.G - $mR; $db = $p.B - $mR
        $dist = ($dr * $dr) + ($dg * $dg) + ($db * $db)
        if ($dist -gt ($Tolerance * $Tolerance)) { $keep[$x, $y] = $true }
    }
}

# largest connected component
$compId = New-Object 'int[,]' $w, $h
$compSizes = New-Object 'System.Collections.Generic.List[int]'
$nextId = 1
for ($y = 0; $y -lt $h; $y++) { for ($x = 0; $x -lt $w; $x++) {
    if (-not $keep[$x, $y] -or $compId[$x, $y] -ne 0) { continue }
    $stack = New-Object 'System.Collections.Generic.Stack[object]'
    $stack.Push(@($x, $y)); $compId[$x, $y] = $nextId; $size = 0
    while ($stack.Count -gt 0) {
        $pt = $stack.Pop(); $px = [int]$pt[0]; $py = [int]$pt[1]; $size++
        for ($dy = -1; $dy -le 1; $dy++) { for ($dx = -1; $dx -le 1; $dx++) {
            $nx = $px + $dx; $ny = $py + $dy
            if ($nx -lt 0 -or $ny -lt 0 -or $nx -ge $w -or $ny -ge $h) { continue }
            if ($keep[$nx, $ny] -and $compId[$nx, $ny] -eq 0) { $compId[$nx, $ny] = $nextId; $stack.Push(@($nx, $ny)) }
        } }
    }
    $compSizes.Add($size); $nextId++
} }
$largest = 0
for ($i = 0; $i -lt $compSizes.Count; $i++) { if ($compSizes[$i] -gt $compSizes[$largest]) { $largest = $i } }
$keepId = $largest + 1
Write-Output ("components={0} largest={1}" -f $compSizes.Count, $compSizes[$largest])

# fill holes: background pixels not reachable from border become keep
$bgVisited = New-Object 'bool[,]' $w, $h
$stack = New-Object 'System.Collections.Generic.Stack[object]'
$w1 = $w - 1; $h1 = $h - 1
for ($x = 0; $x -lt $w; $x++) {
    if ($compId[$x, 0] -ne $keepId -and -not $bgVisited[$x, 0]) { $stack.Push(@($x, 0)) }
    if ($compId[$x, $h1] -ne $keepId -and -not $bgVisited[$x, $h1]) { $stack.Push(@($x, $h1)) }
}
for ($y = 0; $y -lt $h; $y++) {
    if ($compId[0, $y] -ne $keepId -and -not $bgVisited[0, $y]) { $stack.Push(@(0, $y)) }
    if ($compId[$w1, $y] -ne $keepId -and -not $bgVisited[$w1, $y]) { $stack.Push(@($w1, $y)) }
}
while ($stack.Count -gt 0) {
    $pt = $stack.Pop(); $px = [int]$pt[0]; $py = [int]$pt[1]
    if ($bgVisited[$px, $py]) { continue }
    $bgVisited[$px, $py] = $true
    for ($dy = -1; $dy -le 1; $dy++) { for ($dx = -1; $dx -le 1; $dx++) {
        $nx = $px + $dx; $ny = $py + $dy
        if ($nx -lt 0 -or $ny -lt 0 -or $nx -ge $w -or $ny -ge $h) { continue }
        if ($compId[$nx, $ny] -ne $keepId -and -not $bgVisited[$nx, $ny]) { $stack.Push(@($nx, $ny)) }
    } }
}

# alpha + feather
$alpha = New-Object 'int[,]' $w, $h
for ($y = 0; $y -lt $h; $y++) { for ($x = 0; $x -lt $w; $x++) {
    if ($compId[$x, $y] -eq $keepId -or -not $bgVisited[$x, $y]) { $alpha[$x, $y] = 255 }
} }
for ($y = 0; $y -lt $h; $y++) { for ($x = 0; $x -lt $w; $x++) {
    if ($alpha[$x, $y] -eq 255) {
        $nb = 0
        for ($dy = -1; $dy -le 1; $dy++) { for ($dx = -1; $dx -le 1; $dx++) {
            $nx = $x + $dx; $ny = $y + $dy
            if ($nx -lt 0 -or $ny -lt 0 -or $nx -ge $w -or $ny -ge $h) { continue }
            if ($alpha[$nx, $ny] -eq 255) { $nb++ }
        } }
        if ($nb -lt 9) { $alpha[$x, $y] = [int](255 * ($nb / 9.0)) }
    }
} }

for ($y = 0; $y -lt $h; $y++) { for ($x = 0; $x -lt $w; $x++) {
    $p = $bmp.GetPixel($x, $y); $a = $alpha[$x, $y]
    if ($a -ne $p.A) { $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($a, $p.R, $p.G, $p.B)) }
} }

$cnt = 0
for ($y = 0; $y -lt $h; $y++) { for ($x = 0; $x -lt $w; $x++) { if ($alpha[$x, $y] -gt 0) { $cnt++ } } }
Write-Output ("keep {0:p0}" -f ($cnt / ($w * $h)))
$cols = 40; $rows = 20
$cs = [Math]::Max(1, [int]($w / $cols)); $rs = [Math]::Max(1, [int]($h / $rows))
for ($ry = 0; $ry -lt $rows; $ry++) {
    $line = ""
    for ($cx = 0; $cx -lt $cols; $cx++) {
        $x = [Math]::Min($w - 1, $cx * $cs + [int]($cs / 2))
        $y = [Math]::Min($h - 1, $ry * $rs + [int]($rs / 2))
        if ($alpha[$x, $y] -ge 200) { $line += '#' } elseif ($alpha[$x, $y] -gt 0) { $line += '+' } else { $line += '.' }
    }
    Write-Output $line
}

$tmp = $Path + ".tmp.png"
$bmp.Save($tmp, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Move-Item -Force $tmp $Path
Write-Output "DONE"
