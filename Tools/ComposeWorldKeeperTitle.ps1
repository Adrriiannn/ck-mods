$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$srcPath = 'E:\Tools\CoreKeeperRippedAssets\ExportedProject\Assets\Texture2D\title_text_test.png'
$outPath = 'E:\ck mods\CoreKeeperModSDK\Assets\ExpandNullforge\Editor\UI\Art\CKTitleWorldKeeper.png'
$preview = 'C:\Users\NSIAdmin\AppData\Local\Temp\claude\E--ck-mods\89d0d1a3-663e-4768-b574-d60dafd9af90\scratchpad\wk-preview.png'

$src = [System.Drawing.Bitmap]::FromFile($srcPath)

# ---- global vertical gradient, sampled from the giant C ----
$lutR = @(); $lutG = @(); $lutB = @()
for ($y=0; $y -lt 48; $y++) {
    $r=0;$g=0;$b=0;$n=0
    for ($x=2; $x -lt 20; $x++) {
        $p = $src.GetPixel($x,$y)
        if ($p.A -gt 200) { $r+=$p.R;$g+=$p.G;$b+=$p.B;$n++ }
    }
    if ($n -gt 0) { $lutR += [int]($r/$n); $lutG += [int]($g/$n); $lutB += [int]($b/$n) }
    else { $lutR += ($lutR.Count -gt 0 ? $lutR[-1] : 45); $lutG += ($lutG.Count -gt 0 ? $lutG[-1] : 226); $lutB += ($lutB.Count -gt 0 ? $lutB[-1] : 253) }
}
function LutColor([int]$y, [int]$alpha) {
    $i = [Math]::Max(0, [Math]::Min(47, $y))
    return [System.Drawing.Color]::FromArgb($alpha, $lutR[$i], $lutG[$i], $lutB[$i])
}

$canvas = New-Object System.Drawing.Bitmap(168, 72, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

function CopyRegion([int]$sx0,[int]$sy0,[int]$sx1,[int]$sy1,[int]$dx,[int]$dy) {
    for ($y=$sy0; $y -le $sy1; $y++) { for ($x=$sx0; $x -le $sx1; $x++) {
        $p = $src.GetPixel($x,$y)
        if ($p.A -gt 16) { $canvas.SetPixel($dx + ($x-$sx0), $dy + ($y-$sy0), $p) }
    } }
}

# ================= the W: one rounded stroke, drawn like the game draws letters =================
# The logo's letters are soft capsule strokes with round shoulders and feet - the C is
# one thick bent stroke. So this W is one thick polyline: down, up to the peak, down,
# up - rasterized as capsules, which rounds every cap, join and foot by construction.
# Shorter than before (40 tall), C-proportioned, with ONE carved gap at the left
# valley; the right valley's break is played by the O nesting through the stroke.
$WW = 36; $WH = 40
$W = New-Object 'bool[,]' $WW, $WH

# the path, in mask coordinates; the right tip a hair higher than the left - the bounce
$P = @(
    @(2.5, 3.0),     # top-left
    @(8.0, 33.5),    # left foot
    @(16.0, 12.0),   # the peak
    @(24.5, 33.5),   # right foot
    @(30.5, 2.5)     # top-right
)
$T = 3.5             # stroke radius: 7px strokes, the top line's own weight

function DistToSeg([double]$px,[double]$py,[double]$ax,[double]$ay,[double]$bx,[double]$by) {
    $dx=$bx-$ax; $dy=$by-$ay; $len2=$dx*$dx+$dy*$dy
    $t = (($px-$ax)*$dx + ($py-$ay)*$dy) / $len2
    if ($t -lt 0) {$t=0}; if ($t -gt 1) {$t=1}
    $qx = $ax+$t*$dx; $qy=$ay+$t*$dy
    return [Math]::Sqrt(($px-$qx)*($px-$qx)+($py-$qy)*($py-$qy))
}
function AlongSeg([double]$px,[double]$py,[double]$ax,[double]$ay,[double]$bx,[double]$by) {
    $dx=$bx-$ax; $dy=$by-$ay; $len=[Math]::Sqrt($dx*$dx+$dy*$dy)
    return (($px-$ax)*$dx + ($py-$ay)*$dy) / $len
}

for ($y=0; $y -lt $WH; $y++) { for ($x=0; $x -lt $WW; $x++) {
    for ($s=0; $s -lt 4; $s++) {
        $a = $P[$s]; $b = $P[$s+1]
        if ((DistToSeg $x $y $a[0] $a[1] $b[0] $b[1]) -le $T) { $W[$x,$y] = $true; break }
    }
} }

# the carved gap: a narrow channel straight through the ^'s apex, splitting the
# rising / from the falling \ — each keeps its own rounded tip, with air between
# them. Slightly wider at the mouth than at the bottom, the way a split opens, and
# leaning a hair off vertical so it reads cut, not drafted.
# down to y26, not 21: the two capsules stay fused until ~y22, so a shorter channel
# leaves a solid bridge under the cut and the peak reads whole again. The centre
# staggers a pixel side to side on the way down — a break wanders, a saw doesn't —
# with each band's shift small enough that the channel never pinches shut.
# the centre staggers a pixel side to side on the way down — a break wanders, a saw
# doesn't — with each band's shift small enough that the channel never pinches shut.
for ($y=0; $y -le 26; $y++) { for ($x=0; $x -lt $WW; $x++) {
    if (-not $W[$x,$y]) { continue }
    $wander = 0.0
    if ($y -le 16) { $wander = 0.7 }
    elseif ($y -le 20) { $wander = -0.6 }
    elseif ($y -le 24) { $wander = 0.5 }
    else { $wander = -0.4 }
    $centre = 16.0 + $wander
    $half = 1.0 + 0.6*([Math]::Max(0,(21-$y)))/14.0
    if ([Math]::Abs($x - $centre) -le $half) { $W[$x,$y] = $false }
} }

# the W sits at this canvas offset: top a head above the word, chin past the baseline
$WOY = 4

# ---- the intertwine: the O nests INTO the W the way ORE nests in the C's mouth ----
# The O sits overlapping the right slash's path. Every W pixel within 2px of the O is
# left unpainted, so the slash vanishes behind the O's shoulder and re-emerges below
# it on the way to its foot — while its peak still clears the O's top, keeping the W
# silhouette whole. The 2px channel is the same air the C keeps around its letters.
$ODX = 30; $ODY = 13
$oBlock = New-Object 'bool[,]' $WW, $WH
for ($ly=0; $ly -lt 22; $ly++) { for ($lx=0; $lx -lt 25; $lx++) {
    $p = $src.GetPixel(19+$lx, 13+$ly)
    if ($p.A -le 16) { continue }
    for ($dy=-2; $dy -le 2; $dy++) { for ($dx=-2; $dx -le 2; $dx++) {
        # mask coordinates: the W is painted at a vertical canvas offset
        $cx = $ODX+$lx+$dx; $cy = $ODY+$ly+$dy - $WOY
        if ($cx -ge 0 -and $cy -ge 0 -and $cx -lt $WW -and $cy -lt $WH) { $oBlock[$cx,$cy] = $true }
    } }
} }

# paint through the gradient; step corners soften; the O's nest is left as air
for ($y=0; $y -lt $WH; $y++) { for ($x=0; $x -lt $WW; $x++) {
    if (-not $W[$x,$y] -or $oBlock[$x,$y]) { continue }
    $edgeSteps = 0
    foreach ($d in @(@(1,0),@(-1,0),@(0,1),@(0,-1))) {
        $nx=$x+$d[0]; $ny=$y+$d[1]
        $solid = ($nx -ge 0 -and $ny -ge 0 -and $nx -lt $WW -and $ny -lt $WH) -and
                 $W[$nx,$ny] -and (-not $oBlock[$nx,$ny])
        if (-not $solid) { $edgeSteps++ }
    }
    $alpha = ($edgeSteps -ge 2) ? 150 : 255
    $canvas.SetPixel($x, ($WOY + $y), (LutColor ($WOY + $y) $alpha))
} }

# ================= top line: O R L D, the O half-inside the W =================
# The O and D thin by ONE pixel from the inside — their walls came from the CORE line,
# which is set heavier than KEEPER, and the two words sit stacked here. Growing the
# counter keeps the outer silhouette, the spacing and the W-nest untouched; only the
# stroke lightens. (The W, R and L stay exactly as they are.)

# the O's ring, eroded from within: flood the counter, peel every wall pixel touching it
$oMask = New-Object 'bool[,]' 25, 22
for ($ly=0; $ly -lt 22; $ly++) { for ($lx=0; $lx -lt 25; $lx++) {
    if ($src.GetPixel(19+$lx, 13+$ly).A -gt 16) { $oMask[$lx,$ly] = $true }
} }
function FloodHole([bool[,]]$mask,[int]$w,[int]$h,[int]$sx,[int]$sy) {
    $hole = New-Object 'bool[,]' $w, $h
    $stack = New-Object System.Collections.Stack
    $stack.Push(@($sx,$sy))
    while ($stack.Count -gt 0) {
        $q = $stack.Pop(); $qx=$q[0]; $qy=$q[1]
        if ($qx -lt 0 -or $qy -lt 0 -or $qx -ge $w -or $qy -ge $h) { continue }
        if ($mask[$qx,$qy] -or $hole[$qx,$qy]) { continue }
        $hole[$qx,$qy] = $true
        $stack.Push(@(($qx+1),$qy)); $stack.Push(@(($qx-1),$qy))
        $stack.Push(@($qx,($qy+1))); $stack.Push(@($qx,($qy-1)))
    }
    return ,$hole
}
function ErodeAgainstHole([bool[,]]$mask,[bool[,]]$hole,[int]$w,[int]$h) {
    $peel = @()
    for ($ly=0; $ly -lt $h; $ly++) { for ($lx=0; $lx -lt $w; $lx++) {
        if (-not $mask[$lx,$ly]) { continue }
        foreach ($d in @(@(1,0),@(-1,0),@(0,1),@(0,-1))) {
            $nx=$lx+$d[0]; $ny=$ly+$d[1]
            if ($nx -ge 0 -and $ny -ge 0 -and $nx -lt $w -and $ny -lt $h -and $hole[$nx,$ny]) {
                $peel += ,@($lx,$ly); break
            }
        }
    } }
    foreach ($p in $peel) { $mask[$p[0],$p[1]] = $false }
}
$oHole = FloodHole $oMask 25 22 12 11
ErodeAgainstHole $oMask $oHole 25 22
for ($ly=0; $ly -lt 22; $ly++) { for ($lx=0; $lx -lt 25; $lx++) {
    if ($oMask[$lx,$ly]) { $canvas.SetPixel(30+$lx, 13+$ly, $src.GetPixel(19+$lx, 13+$ly)) }
} }
CopyRegion 48  9 68 35  58  9      # R -> 58..78, untouched

# the O's crack: a wedge through the right wall, wider at the outer surface
for ($y=15; $y -le 24; $y++) { for ($x=47; $x -le 54; $x++) {
    $s = $x + $y
    $lo = 70; $hi = 71
    if ($x -ge 52) { $lo = 69; $hi = 72 }
    if ($s -ge $lo -and $s -le $hi) {
        $canvas.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0,0,0,0))
    }
} }

# L: the E keeping only its stem (capped at 8px) above its bottom bar
for ($y=11; $y -le 35; $y++) {
    if ($y -ge 31) {
        for ($x=72; $x -le 91; $x++) { $p=$src.GetPixel($x,$y); if ($p.A -gt 16) { $canvas.SetPixel(82+($x-72), $y, $p) } }
    }
    else {
        $x = 72; while ($x -le 91 -and $src.GetPixel($x,$y).A -le 16) { $x++ }
        $runStart = $x
        while ($x -le 91 -and $src.GetPixel($x,$y).A -gt 16 -and ($x - $runStart) -lt 8) {
            $canvas.SetPixel(82+($x-72), $y, $src.GetPixel($x,$y)); $x++
        }
    }
}

# D: the O's right side, curved wall replaced by a slim straight stem; top junction cut
# as a wedge that opens as it leaves the stem. Assembled as a mask FIRST so the inner
# thinning can run while the ring is still closed — flooding through an open cut would
# leak to the outside and peel the outer edge instead of the counter.
$dTileX = 105; $oX0=19; $oY0=13
$dMask = New-Object 'bool[,]' 25, 22
$dStem = New-Object 'bool[,]' 25, 22
$dHalf = New-Object 'bool[,]' 25, 22
for ($y=0; $y -lt 22; $y++) {
    for ($x=8; $x -lt 25; $x++) {
        if ($y -ge 6 -and $y -le 15 -and $x -le 10) { continue }
        if ($src.GetPixel($oX0+$x, $oY0+$y).A -gt 16) { $dMask[$x,$y] = $true }
    }
    for ($x=0; $x -le 7; $x++) {
        $keep = $true; $half = $false
        if ($y -lt 2 -and $x -lt 2) {
            if (($y -eq 0 -and $x -le 1) -or ($y -eq 1 -and $x -eq 0)) { $keep = $false }
            elseif ($y -eq 1 -and $x -eq 1) { $half = $true }
        }
        if ($y -gt 19 -and $x -lt 2) {
            if (($y -eq 21 -and $x -le 1) -or ($y -eq 20 -and $x -eq 0)) { $keep = $false }
            elseif ($y -eq 20 -and $x -eq 1) { $half = $true }
        }
        if ($keep) { $dMask[$x,$y] = $true; $dStem[$x,$y] = $true; if ($half) { $dHalf[$x,$y] = $true } }
    }
}
$dHole = FloodHole $dMask 25 22 14 11
ErodeAgainstHole $dMask $dHole 25 22
for ($y=0; $y -lt 22; $y++) {
    $cutEnd = 10 + [Math]::Min($y, 3)
    for ($x=0; $x -lt 25; $x++) {
        if (-not $dMask[$x,$y]) { continue }
        if ($y -le 5 -and $x -ge 8 -and $x -le $cutEnd) { continue }   # the junction cut
        if ($dStem[$x,$y]) {
            $alpha = $dHalf[$x,$y] ? 150 : 255
            $canvas.SetPixel($dTileX+$x, $oY0+$y, (LutColor ($oY0+$y) $alpha))
        }
        else {
            $canvas.SetPixel($dTileX+$x, $oY0+$y, $src.GetPixel($oX0+$x, $oY0+$y))
        }
    }
}

# ================= bottom line: KEEPER, untouched, starting under the top word =================
CopyRegion 38 37 164 71  40 37     # -> 40..166

# ---- soften the freshly cut stroke ends ----
function SoftenCuts([int]$x0,[int]$y0,[int]$x1,[int]$y1) {
    $toSoften = @()
    for ($y=$y0; $y -le $y1; $y++) { for ($x=$x0; $x -le $x1; $x++) {
        $p = $canvas.GetPixel($x,$y)
        if ($p.A -lt 250) { continue }
        $bare = 0
        foreach ($d in @(@(1,0),@(-1,0),@(0,1),@(0,-1))) {
            $nx=$x+$d[0]; $ny=$y+$d[1]
            if ($nx -lt 0 -or $ny -lt 0 -or $nx -ge $canvas.Width -or $ny -ge $canvas.Height -or
                $canvas.GetPixel($nx,$ny).A -lt 32) { $bare++ }
        }
        if ($bare -ge 2) { $toSoften += ,@($x,$y,$p) }
    } }
    foreach ($e in $toSoften) {
        $p = $e[2]
        $canvas.SetPixel($e[0], $e[1], [System.Drawing.Color]::FromArgb(150, $p.R, $p.G, $p.B))
    }
}
SoftenCuts 45 13 56 26     # the O's crack mouths
SoftenCuts 111 12 121 20   # the D's junction gap
SoftenCuts 2 18 36 44      # the W's valley wedges

$src.Dispose()

# ================= the glow, in the title screen's own colours =================
# Measured from the game's title_text_glow.png: peak RGB(45,226,253) — the logo's own
# top-gradient cyan — falling off toward RGB(37,154,250) deep blue at the fringe. Ours
# is generated from our letters' silhouette (blurred alpha), so the halo hugs WORLD
# KEEPER exactly the way the original hugs CORE KEEPER.
$margin = 8
$fw = $canvas.Width + 2*$margin; $fh = $canvas.Height + 2*$margin

$alpha = New-Object 'double[,]' $fw, $fh
for ($y=0; $y -lt $canvas.Height; $y++) { for ($x=0; $x -lt $canvas.Width; $x++) {
    $alpha[($x+$margin), ($y+$margin)] = $canvas.GetPixel($x,$y).A / 255.0
} }

function BoxBlur([double[,]]$m,[int]$w,[int]$h,[int]$r) {
    $tmp = New-Object 'double[,]' $w, $h
    for ($y=0; $y -lt $h; $y++) { for ($x=0; $x -lt $w; $x++) {
        $sum=0.0; $n=0
        for ($d=-$r; $d -le $r; $d++) { $ix=$x+$d; if ($ix -ge 0 -and $ix -lt $w) { $sum+=$m[$ix,$y]; $n++ } }
        $tmp[$x,$y] = $sum/$n
    } }
    for ($x=0; $x -lt $w; $x++) { for ($y=0; $y -lt $h; $y++) {
        $sum=0.0; $n=0
        for ($d=-$r; $d -le $r; $d++) { $iy=$y+$d; if ($iy -ge 0 -and $iy -lt $h) { $sum+=$tmp[$x,$iy]; $n++ } }
        $m[$x,$y] = $sum/$n
    } }
}
BoxBlur $alpha $fw $fh 3
BoxBlur $alpha $fw $fh 3
BoxBlur $alpha $fw $fh 2

$final = New-Object System.Drawing.Bitmap($fw, $fh, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
for ($y=0; $y -lt $fh; $y++) { for ($x=0; $x -lt $fw; $x++) {
    $a = $alpha[$x,$y]
    if ($a -le 0.004) { continue }
    $t = [Math]::Min(1.0, $a * 1.6)          # near the letters: bright cyan; fringe: deep blue
    $r = [int](37 + (45-37)*$t)
    $g = [int](154 + (226-154)*$t)
    $b = [int](250 + (253-250)*$t)
    $ga = [int][Math]::Min(255, $a * 0.62 * 255)
    $final.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($ga, $r, $g, $b))
} }
$gfxF = [System.Drawing.Graphics]::FromImage($final)
$gfxF.DrawImage($canvas, $margin, $margin)
$gfxF.Dispose()
$canvas.Dispose()
$canvas = $final

$canvas.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)

# preview at 4x on the title screen's dark blue
$big = New-Object System.Drawing.Bitmap(($canvas.Width*4), ($canvas.Height*4))
$g = [System.Drawing.Graphics]::FromImage($big)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$g.Clear([System.Drawing.Color]::FromArgb(255,16,14,30))
$g.DrawImage($canvas, 0, 0, $big.Width, $big.Height)
$g.Dispose()
$big.Save($preview, [System.Drawing.Imaging.ImageFormat]::Png)
$big.Dispose(); $canvas.Dispose()
'composed 168x72'
