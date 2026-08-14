# md2docx.ps1 — 把 DeepSeek_Harness_安装教程.md 直接转成 Word(.docx)
# 纯 OOXML 生成（zip + document.xml），不依赖 Word，无弹窗、无残留进程
# 用法:  pwsh -File tools\md2docx.ps1   （或在 PowerShell 中运行本脚本）
# 输出: 项目根目录 DeepSeek_Harness_安装教程.docx

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$mdPath   = Join-Path $root 'DeepSeek_Harness_安装教程.md'
$docxPath = Join-Path $root 'DeepSeek_Harness_安装教程.docx'

if (-not (Test-Path $mdPath)) { throw "找不到 md 文件: $mdPath" }

$md = [System.IO.File]::ReadAllText($mdPath).Replace("`r`n", "`n")
$lines = $md -split "`n"
$sb = New-Object System.Text.StringBuilder

# ---------- 行内格式: md -> OOXML runs ----------
function Xml-Escape([string]$s) {
    return $s.Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;').Replace('"', '&quot;')
}

function New-Run([string]$text, [bool]$bold, [bool]$code) {
    if ($text -eq '') { return '' }
    $rpr = ''
    if ($bold) { $rpr += '<w:b/>' }
    if ($code) { $rpr += '<w:rFonts w:ascii="Consolas" w:hAnsi="Consolas"/><w:shd w:val="clear" w:color="auto" w:fill="F2F2F2"/>' }
    if ($rpr -ne '') { $rpr = '<w:rPr>' + $rpr + '</w:rPr>' }
    return '<w:r>' + $rpr + '<w:t xml:space="preserve">' + (Xml-Escape $text) + '</w:t></w:r>'
}

function Format-Inline([string]$s, [bool]$bold = $false) {
    $pattern = '`[^`\r\n]+`|\*\*[^*\r\n]+\*\*|\[[^\]\r\n]+\]\([^)\r\n]+\)'
    $ms = [regex]::Matches($s, $pattern)
    if ($ms.Count -eq 0) { return (New-Run $s $bold $false) }
    $out = ''; $pos = 0
    foreach ($m in $ms) {
        $out += New-Run $s.Substring($pos, $m.Index - $pos) $bold $false
        $tok = $m.Value
        if ($tok.StartsWith('`')) {
            $out += New-Run $tok.Substring(1, $tok.Length - 2) $bold $true
        } elseif ($tok.StartsWith('**')) {
            $out += Format-Inline $tok.Substring(2, $tok.Length - 4) $true
        } else {
            $mt = [regex]::Match($tok, '\[([^\]]+)\]\(([^)]+)\)')
            $txt = $mt.Groups[1].Value
            $url = $mt.Groups[2].Value
            if ($url.StartsWith('#')) {
                $out += Format-Inline $txt $bold   # 目录锚点 -> 纯文本
            } else {
                $out += '<w:r><w:rPr><w:color w:val="0563C1"/><w:u w:val="single"/></w:rPr><w:t xml:space="preserve">' + (Xml-Escape $txt) + '</w:t></w:r>'
            }
        }
        $pos = $m.Index + $m.Length
    }
    $out += New-Run $s.Substring($pos) $bold $false
    return $out
}

# ---------- 块级结构 ----------
function New-Para([string]$runs, [string]$style = '', [string]$extraPPr = '') {
    $ppr = ''
    if ($style -ne '') { $ppr += '<w:pStyle w:val="' + $style + '"/>' }
    $ppr += $extraPPr
    if ($ppr -ne '') { $ppr = '<w:pPr>' + $ppr + '</w:pPr>' }
    return '<w:p>' + $ppr + $runs + '</w:p>'
}

function Split-Cells([string]$line) {
    $s = $line.Trim()
    if ($s.StartsWith('|')) { $s = $s.Substring(1) }
    if ($s.EndsWith('|'))   { $s = $s.Substring(0, $s.Length - 1) }
    $res = @()
    foreach ($p in ($s -split '\|')) { $res += $p.Trim() }
    return $res
}

function New-TableXml($rows) {
    $hdr = Split-Cells $rows[0]
    $colCount = $hdr.Count
    if ($colCount -eq 0) { return '' }
    $colW = [math]::Floor(8500 / $colCount)
    $grid = ''
    for ($c = 0; $c -lt $colCount; $c++) { $grid += '<w:gridCol w:w="' + $colW + '"/>' }
    $t = '<w:tbl><w:tblPr><w:tblW w:w="5000" w:type="pct"/><w:tblBorders>' +
         '<w:top w:val="single" w:sz="4" w:color="7F7F7F"/><w:left w:val="single" w:sz="4" w:color="7F7F7F"/>' +
         '<w:bottom w:val="single" w:sz="4" w:color="7F7F7F"/><w:right w:val="single" w:sz="4" w:color="7F7F7F"/>' +
         '<w:insideH w:val="single" w:sz="4" w:color="7F7F7F"/><w:insideV w:val="single" w:sz="4" w:color="7F7F7F"/>' +
         '</w:tblBorders><w:tblCellMar><w:top w:w="40" w:type="dxa"/><w:left w:w="80" w:type="dxa"/>' +
         '<w:bottom w:w="40" w:type="dxa"/><w:right w:w="80" w:type="dxa"/></w:tblCellMar></w:tblPr>'
    $t += '<w:tblGrid>' + $grid + '</w:tblGrid>'
    # 表头行（加粗 + 浅蓝底）
    $t += '<w:tr>'
    foreach ($c in $hdr) {
        $t += '<w:tc><w:tcPr><w:tcW w:w="' + $colW + '" w:type="dxa"/><w:shd w:val="clear" w:color="auto" w:fill="DEEAF6"/></w:tcPr>' +
              '<w:p><w:pPr><w:spacing w:after="40"/></w:pPr>' + (Format-Inline $c $true) + '</w:p></w:tc>'
    }
    $t += '</w:tr>'
    # 数据行
    for ($r = 1; $r -lt $rows.Count; $r++) {
        $line = $rows[$r]
        if ($line -match '^\|[\s:|-]+\|?$') { continue }
        $cells = Split-Cells $line
        $t += '<w:tr>'
        foreach ($c in $cells) {
            $t += '<w:tc><w:tcPr><w:tcW w:w="' + $colW + '" w:type="dxa"/></w:tcPr>' +
                  '<w:p><w:pPr><w:spacing w:after="40"/></w:pPr>' + (Format-Inline $c) + '</w:p></w:tc>'
        }
        $t += '</w:tr>'
    }
    $t += '</w:tbl>'
    return $t
}

# ---------- 主解析循环 ----------
$i = 0
while ($i -lt $lines.Count) {
    $line = $lines[$i]

    # 代码块
    if ($line -match '^```') {
        $code = New-Object System.Text.StringBuilder
        $i++
        while ($i -lt $lines.Count -and $lines[$i] -notmatch '^```') {
            [void]$code.Append($lines[$i] + "`n")
            $i++
        }
        $i++   # 跳过结束 ``` 行
        $codeLines = $code.ToString().TrimEnd("`n") -split "`n"
        $codeXml = ''
        for ($k = 0; $k -lt $codeLines.Count; $k++) {
            if ($k -gt 0) { $codeXml += '<w:r><w:rPr><w:rFonts w:ascii="Consolas" w:hAnsi="Consolas"/><w:sz w:val="20"/></w:rPr><w:br/></w:r>' }
            $codeXml += '<w:r><w:rPr><w:rFonts w:ascii="Consolas" w:hAnsi="Consolas"/><w:sz w:val="20"/></w:rPr><w:t xml:space="preserve">' + (Xml-Escape $codeLines[$k]) + '</w:t></w:r>'
        }
        [void]$sb.Append((New-Para $codeXml 'CodeBlock'))
        continue
    }
    # 空行
    if ($line.Trim() -eq '') { $i++; continue }
    # 分隔线
    if ($line.Trim() -eq '---') {
        [void]$sb.Append('<w:p><w:pPr><w:pBdr><w:bottom w:val="single" w:sz="6" w:space="1" w:color="BFBFBF"/></w:pBdr><w:spacing w:before="60" w:after="120"/></w:pPr></w:p>')
        $i++; continue
    }
    # 表格
    if ($line.Trim().StartsWith('|')) {
        $rows = New-Object System.Collections.ArrayList
        while ($i -lt $lines.Count -and $lines[$i].Trim().StartsWith('|')) {
            [void]$rows.Add($lines[$i].Trim()); $i++
        }
        [void]$sb.Append((New-TableXml $rows))
        continue
    }
    # 标题
    if ($line -match '^(#{1,6})\s+(.*)$') {
        $lv = $matches[1].Length
        [void]$sb.Append((New-Para (Format-Inline $matches[2]) ('Heading' + $lv)))
        $i++; continue
    }
    # 引用块
    if ($line -match '^>\s?(.*)$') {
        while ($i -lt $lines.Count -and $lines[$i] -match '^>\s?(.*)$') {
            if ($matches[1].Trim() -ne '') {
                [void]$sb.Append((New-Para (Format-Inline $matches[1]) 'Quote'))
            }
            $i++
        }
        continue
    }
    # 有序列表项（手动编号，避免 Word 列表编号重排）
    if ($line -match '^\s*(\d+)[.、]?\s+(.*)$') {
        $numRun = '<w:r><w:rPr><w:b/></w:rPr><w:t xml:space="preserve">' + $matches[1] + '. </w:t></w:r>'
        [void]$sb.Append((New-Para ($numRun + (Format-Inline $matches[2])) '' '<w:ind w:left="567"/>'))
        $i++; continue
    }
    # 无序列表项
    if ($line -match '^\s*[-*]\s+(.*)$') {
        $bulletRun = '<w:r><w:t xml:space="preserve">• </w:t></w:r>'
        [void]$sb.Append((New-Para ($bulletRun + (Format-Inline $matches[1])) '' '<w:ind w:left="283"/>'))
        $i++; continue
    }
    # 普通段落（连续行合并）
    $para = New-Object System.Collections.ArrayList
    while ($i -lt $lines.Count) {
        $l = $lines[$i]
        if ($l.Trim() -eq '') { break }
        if ($l -match '^(```|#{1,6}\s|>\s?|---$|\s*\|)') { break }
        if ($l -match '^\s*(\d+)[.、]?\s+|^\s*[-*]\s+') { break }
        [void]$para.Add($l); $i++
    }
    [void]$sb.Append((New-Para (Format-Inline ($para -join '<w:br/>'))))
}

# ---------- 组装 OOXML 各部件 ----------
$wNs = 'xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"'

$documentXml = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
  '<w:document ' + $wNs + '><w:body>' + $sb.ToString() +
  '<w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" w:header="708" w:footer="708" w:gutter="0"/></w:sectPr>' +
  '</w:body></w:document>'

$stylesXml = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
'<w:styles ' + $wNs + '>' +
'<w:docDefaults><w:rPrDefault><w:rPr>' +
'<w:rFonts w:ascii="Microsoft YaHei" w:hAnsi="Microsoft YaHei" w:eastAsia="微软雅黑" w:cs="Microsoft YaHei"/>' +
'<w:sz w:val="22"/><w:szCs w:val="22"/><w:lang w:val="en-US" w:eastAsia="zh-CN"/>' +
'</w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:after="120" w:line="300" w:lineRule="auto"/></w:pPr></w:pPrDefault></w:docDefaults>' +
'<w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/><w:qFormat/></w:style>' +
'<w:style w:type="paragraph" w:styleId="Heading1"><w:name w:val="heading 1"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/>' +
'<w:pPr><w:keepNext/><w:spacing w:before="280" w:after="160"/><w:outlineLvl w:val="0"/></w:pPr>' +
'<w:rPr><w:b/><w:sz w:val="40"/><w:color w:val="1F3864"/></w:rPr></w:style>' +
'<w:style w:type="paragraph" w:styleId="Heading2"><w:name w:val="heading 2"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/>' +
'<w:pPr><w:keepNext/><w:spacing w:before="240" w:after="120"/><w:pBdr><w:bottom w:val="single" w:sz="6" w:space="2" w:color="8EAADB"/></w:pBdr><w:outlineLvl w:val="1"/></w:pPr>' +
'<w:rPr><w:b/><w:sz w:val="32"/><w:color w:val="1F3864"/></w:rPr></w:style>' +
'<w:style w:type="paragraph" w:styleId="Heading3"><w:name w:val="heading 3"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/>' +
'<w:pPr><w:keepNext/><w:spacing w:before="200" w:after="100"/><w:outlineLvl w:val="2"/></w:pPr>' +
'<w:rPr><w:b/><w:sz w:val="27"/><w:color w:val="2E5395"/></w:rPr></w:style>' +
'<w:style w:type="paragraph" w:styleId="Heading4"><w:name w:val="heading 4"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/>' +
'<w:pPr><w:keepNext/><w:spacing w:before="160" w:after="80"/><w:outlineLvl w:val="3"/></w:pPr>' +
'<w:rPr><w:b/><w:sz w:val="24"/><w:color w:val="2E5395"/></w:rPr></w:style>' +
'<w:style w:type="paragraph" w:styleId="CodeBlock"><w:name w:val="CodeBlock"/><w:basedOn w:val="Normal"/><w:qFormat/>' +
'<w:pPr><w:spacing w:after="80" w:line="240" w:lineRule="auto"/><w:ind w:left="113" w:right="113"/>' +
'<w:shd w:val="clear" w:color="auto" w:fill="F2F2F2"/>' +
'<w:pBdr><w:top w:val="single" w:sz="4" w:space="4" w:color="CCCCCC"/><w:left w:val="single" w:sz="4" w:space="4" w:color="CCCCCC"/>' +
'<w:bottom w:val="single" w:sz="4" w:space="4" w:color="CCCCCC"/><w:right w:val="single" w:sz="4" w:space="4" w:color="CCCCCC"/></w:pBdr></w:pPr>' +
'<w:rPr><w:rFonts w:ascii="Consolas" w:hAnsi="Consolas"/><w:sz w:val="20"/></w:rPr></w:style>' +
'<w:style w:type="paragraph" w:styleId="Quote"><w:name w:val="Quote"/><w:basedOn w:val="Normal"/><w:qFormat/>' +
'<w:pPr><w:spacing w:after="80"/><w:ind w:left="284" w:right="284"/>' +
'<w:shd w:val="clear" w:color="auto" w:fill="FFF7E6"/>' +
'<w:pBdr><w:left w:val="single" w:sz="18" w:space="4" w:color="FFC000"/></w:pBdr></w:pPr></w:style>' +
'</w:styles>'

$contentTypesXml = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
'<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">' +
'<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>' +
'<Default Extension="xml" ContentType="application/xml"/>' +
'<Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>' +
'<Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>' +
'</Types>'

$relsXml = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
'<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' +
'<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>' +
'</Relationships>'

# ---------- 打包为 .docx ----------
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $docxPath) { Remove-Item $docxPath -Force }
$zip = [System.IO.Compression.ZipFile]::Open($docxPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $entries = @{
        '[Content_Types].xml' = $contentTypesXml
        '_rels/.rels'         = $relsXml
        'word/document.xml'   = $documentXml
        'word/styles.xml'     = $stylesXml
    }
    foreach ($name in $entries.Keys) {
        $entry = $zip.CreateEntry($name, [System.IO.Compression.CompressionLevel]::Optimal)
        $stream = $entry.Open()
        try {
            $bytes = [System.Text.Encoding]::UTF8.GetBytes($entries[$name])
            $stream.Write($bytes, 0, $bytes.Length)
        } finally {
            $stream.Dispose()
        }
    }
} finally {
    $zip.Dispose()
}

$sizeKB = [math]::Round((Get-Item $docxPath).Length / 1KB)
Write-Host "DOCX 已生成: $docxPath  ($sizeKB KB)"
