# Final cutout: gold-core region grow -> keep largest connected component -> edge feather ->
# apply to even_match_icon/outline/big. ASCII preview for each.
param([int]$GrowN = 10)

Add-Type -AssemblyName System.Drawing

$Targets = @(
    'assets\relic_icons\even_match_icon.png',
    'assets\relic_icons\even_match_outline.png',
    'assets\relic_icons\even_match_big.png'
)

function Get-Hue($p) {
    $mx = [double][Math]::Max($p.R, [Math]::Max($p.G, $p.B))
    $mn = [double][Math]::Min($p.R, [Math]::Min($p.G, $p.B))
    $d = $mx - $mn
    if ($d -lt 0.001) { return -1.0 }
    $hue = 0.0
    if ($mx -eq [double]$p.R) { $hue = 60 * ((($p.G - $p.B) / $d) % 6) }
    elseif ($mx -eq [double]$p.G) { $hue = 60 * (2 + (($p.B - $p.R) / $d)) }
    else { $hue = 60 * (4 + (($p.R - $p.G) / $d)) }
    if ($hue -lt 0) { $hue += 360 }
    return $hue
}
function Is-Gold($p) {
    $hue = Get-Hue $p
    $mx = [double][Math]::Max($p.R, [Math]::Max($p.G, $p.B))
    $mn = [double][Math]::Min($p.R, [Math]::Min($p.G, $p.B))
    $d = $mx - $mn
    return ($d -ge 40 -and $hue -ge 15 -and $hue -lt 75)
}
function Is-StrongBg($p) {
    $hue = Get-Hue $p
    $mx = [double][Math]::Max($p.R, [Math]::Max($p.G, $p.B))
    $mn = [double][Math]::Min($p.R, [Math]::Min($p.G, $p.B))
    $d = $mx - $mn
    return ($d -ge 40 -and $hue -ge 190 -and $hue -le 345)
}

function Process-Image([string]$Path, [int]$GrowN) {
    $bmp = New-Object System.Drawing.Bitmap($Path)
    $w = $bmp.Width; $h = $bmp.Height

    # gold core
    $core = New-Object 'bool[,]' $w, $h
    for ($y = 0; $y -lt $h; $y++) { for ($x = 0; $x -lt $w; $x++) {
        $p = $bmp.GetPixel($x, $y)
        if ($p.A -ge 16 -and (Is-Gold $p)) { $core[$x, $y] = $true }
    } }

    # region grow
    $keep = New-Object 'bool[,]' $w, $h
    for ($y = 0; $y -lt $h; $y++) { for ($x = 0; $x -lt $w; $x++) { $keep[$x, $y] = $core[$x, $y] } }
    for ($iter = 0; $iter -lt $GrowN; $iter++) {
        $grow = New-Object 'bool[,]' $w, $h
        for ($y = 0; $y -lt $h; $y++) { for ($x = 0; $x -lt $w; $x++) {
            if ($keep[$x, $y]) { $grow[$x, $y] = $true; continue }
            $absorb = $false
            for ($dy = -1; $dy -le 1 -and -not $absorb; $dy++) {
                for ($dx = -1; $dx -le 1 -and -not $absorb; $dx++) {
                    $nx = $x + $dx; $ny = $y + $dy
                    if ($nx -lt 0 -or $ny -lt 0 -or $nx -ge $w -or $ny -ge $h) { continue }
                    if ($keep[$nx, $ny]) {
                        $p = $bmp.GetPixel($x, $y)
                        if (-not (Is-StrongBg $p)) { $absorb = $true }
                    }
                }
            }
            if ($absorb) { $grow[$x, $y] = $true }
        } }
        $keep = $grow
    }

    # largest connected component (8-neighborhood BFS over keep mask)
    $compId = New-Object 'int[,]' $w, $h
    $compSizes = New-Object 'System.Collections.Generic.List[int]'
    $nextId = 1
    for ($y = 0; $y -lt $h; $y++) { for ($x = 0; $x -lt $w; $x++) {
        if (-not $keep[$x, $y] -or $compId[$x, $y] -ne 0) { continue }
        $stack = New-Object 'System.Collections.Generic.Stack[object]'
        $stack.Push(@($x, $y))
        $compId[$x, $y] = $nextId
        $size = 0
        while ($stack.Count -gt 0) {
            $pt = $stack.Pop()
            $px = [int]$pt[0]; $py = [int]$pt[1]
            $size++
            for ($dy = -1; $dy -le 1; $dy++) { for ($dx = -1; $dx -le 1; $dx++) {
                $nx = $px + $dx; $ny = $py + $dy
                if ($nx -lt 0 -or $ny -lt 0 -or $nx -ge $w -or $ny -ge $h) { continue }
                if ($keep[$nx, $ny] -and $compId[$nx, $ny] -eq 0) {
                    $compId[$nx, $ny] = $nextId
                    $stack.Push(@($nx, $ny))
                }
            } }
        }
        $compSizes.Add($size)
        $nextId++
    } }
    $largest = 0
    for ($i = 0; $i -lt $compSizes.Count; $i++) { if ($compSizes[$i] -gt $compSizes[$largest]) { $largest = $i } }
    $keepId = $largest + 1

    # feather: alpha = 255 inside, 0 outside, smooth 1px border
    $alpha = New-Object 'int[,]' $w, $h
    for ($y = 0; $y -lt $h; $y++) { for ($x = 0; $x -lt $w; $x++) {
        if ($compId[$x, $y] -eq $keepId) { $alpha[$x, $y] = 255 } else { $alpha[$x, $y] = 0 }
    } }
    # border pixels get partial alpha based on neighbor count
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

    # apply
    for ($y = 0; $y -lt $h; $y++) { for ($x = 0; $x -lt $w; $x++) {
        $p = $bmp.GetPixel($x, $y)
        $a = $alpha[$x, $y]
        if ($a -ne $p.A) { $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($a, $p.R, $p.G, $p.B)) }
    } }

    # stats + ASCII
    $cnt = 0
    for ($y = 0; $y -lt $h; $y++) { for ($x = 0; $x -lt $w; $x++) { if ($alpha[$x, $y] -gt 0) { $cnt++ } } }
    Write-Output ("{0}: keep {1:p0} comps={2}" -f $Path, ($cnt / ($w * $h)), $compSizes.Count)
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
}

foreach ($t in $Targets) {
    Process-Image (Join-Path (Get-Location) $t) $GrowN
}
Write-Output "DONE"
