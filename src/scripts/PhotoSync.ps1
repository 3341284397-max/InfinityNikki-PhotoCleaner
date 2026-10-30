<#
  无限暖暖 本地照片清理助手 — 核心逻辑
  Infinity Nikki photo cleanup helper — core logic

  作用：找出「游戏内相册已删除、但本地 ScreenShot 目录仍保留」的照片。
  边界：本脚本【绝不删除任何照片】，只做判定、报告、以及把待删文件
        移入 ScreenShot\无限暖暖清理助手_待清理照片\ 由用户自行处理。

  用法：
    powershell -ExecutionPolicy Bypass -File PhotoSync.ps1 -Mode Report
    powershell -ExecutionPolicy Bypass -File PhotoSync.ps1 -Mode Move
    powershell -ExecutionPolicy Bypass -File PhotoSync.ps1 -Mode Report -Lang en

  语言：-Lang zh|en；也可用环境变量 NIKKI_LANG，或数据目录里 设置.txt 的 Language= 行。
        界面语言只影响文字，不影响任何目录名（详见 Get-PendingDir 与 $dataIsEn）。
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('Report', 'Move', 'Clean')]
    [string]$Mode = 'Report',
    [string]$GameDir,
    [int]$MinAgeDays = 3,
    [switch]$IncludeSuspects,
    [switch]$NoOpen,
    [switch]$NoPause,
    [switch]$Yes,
    [switch]$AllRecords,
    [ValidateSet('zh', 'en')]
    [string]$Lang
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch { }

# ============================ 语言 ============================
$ScriptDir = $PSScriptRoot
$ToolRoot  = Split-Path -Parent $ScriptDir

$DataFolderZh    = '无限暖暖清理助手_使用数据'
$DataFolderEn    = 'NikkiCleaner_Data'
$PendingFolderZh = '无限暖暖清理助手_待清理照片'
$PendingFolderEn = 'NikkiCleaner_ToDelete'

$sysLang = 'zh'
try { $sysLang = [Globalization.CultureInfo]::CurrentUICulture.TwoLetterISOLanguageName } catch { }

$requested = $Lang
if (-not $requested) { $requested = $env:NIKKI_LANG }

# 数据目录：已经存在的那个优先，绝不因切换语言而另建一个（与 exe 规则完全一致）
if (Test-Path -LiteralPath (Join-Path $ToolRoot $DataFolderZh)) {
    $dataIsEn = $false
} elseif (Test-Path -LiteralPath (Join-Path $ToolRoot $DataFolderEn)) {
    $dataIsEn = $true
} elseif ($requested) {
    $dataIsEn = ($requested -eq 'en')
} else {
    $dataIsEn = ($sysLang -ne 'zh')
}

$DataDir = Join-Path $ToolRoot $(if ($dataIsEn) { $DataFolderEn } else { $DataFolderZh })
if ($dataIsEn) {
    $ThumbDir     = Join-Path $DataDir 'thumbnails'
    $ReportDir    = Join-Path $DataDir 'reports'
    $LedgerPath   = Join-Path $DataDir 'album-ledger.tsv'
    $ConfigPath   = Join-Path $DataDir 'settings.txt'
    $LastMovePath = Join-Path $DataDir 'last-move.txt'
} else {
    $ThumbDir     = Join-Path $DataDir '缩略图缓存'
    $ReportDir    = Join-Path $DataDir '网页报告'
    $LedgerPath   = Join-Path $DataDir '相册台账.tsv'
    $ConfigPath   = Join-Path $DataDir '设置.txt'
    $LastMovePath = Join-Path $DataDir '最近一次移动记录.txt'
}

# 界面语言：-Lang / NIKKI_LANG > 设置文件 Language= > 系统语言
$uiLang = $requested
if (-not $uiLang -and (Test-Path -LiteralPath $ConfigPath)) {
    foreach ($line in (Get-Content -LiteralPath $ConfigPath -Encoding UTF8)) {
        if ($line -match '^\s*Language\s*=\s*(\S+)\s*$') { $uiLang = $Matches[1] }
    }
}
if (-not $uiLang) { $uiLang = $(if ($sysLang -eq 'zh') { 'zh' } else { 'en' }) }
$script:UiLang = $(if ($uiLang -like 'en*') { 'en' } else { 'zh' })

$script:LangTable = $null
if ($script:UiLang -eq 'en') {
    $langFile = Join-Path $ScriptDir 'lang\PhotoSync.en.ps1'
    if (Test-Path -LiteralPath $langFile) { $script:LangTable = & $langFile }
}

# 取当前语言文案；中文原文就是 key，查不到就退回中文，绝不报错
function T {
    param([string]$zh)
    if ($script:LangTable -and $script:LangTable.ContainsKey($zh)) { return $script:LangTable[$zh] }
    return $zh
}
function TF {
    param([string]$zh, [object[]]$fmt)
    try { return [string]::Format((T $zh), $fmt) } catch { return (T $zh) }
}

# 状态用语言无关的内部码，显示时才翻译（与 C# 版一致）
$S_INALBUM = 'InAlbum'
$S_DELETED = 'Deleted'
$S_SUSPECT = 'Suspect'
$S_TOONEW  = 'TooNew'
$script:StatusLabel = @{
    InAlbum = '在册'
    Deleted = '已删除'
    Suspect = '疑似'
    TooNew  = '太新'
}
function Get-StatusText { param([string]$code) T $script:StatusLabel[$code] }

# 判定理由由状态派生，切换语言只是重新取一次文本
function Get-ReasonText {
    param([string]$code, [int]$minAge)
    switch ($code) {
        'InAlbum' { return (T '游戏相册中仍存在') }
        'Deleted' { return (T '曾出现在相册中，现已从相册缓存消失') }
        'TooNew'  { return (TF '拍摄不足 {0} 天，可能还没同步到相册；要清理请手动勾选' $minAge) }
        default   { return (T '相册缓存中从未见过（可能删过，也可能尚未同步）') }
    }
}

# 待清理文件夹：已存在哪个用哪个，切换语言后不会「看不见」之前归拢的照片
function Get-PendingDir {
    param([string]$ScreenShot)
    if ([string]::IsNullOrWhiteSpace($ScreenShot)) {
        return $(if ($script:UiLang -eq 'en') { $PendingFolderEn } else { $PendingFolderZh })
    }
    $zh = Join-Path $ScreenShot $PendingFolderZh
    $en = Join-Path $ScreenShot $PendingFolderEn
    if (Test-Path -LiteralPath $zh) { return $zh }
    if (Test-Path -LiteralPath $en) { return $en }
    return $(if ($script:UiLang -eq 'en') { $en } else { $zh })
}

# 旧版本 _数据 自动搬家，保住已有台账
$LegacyDir = Join-Path $ToolRoot '_数据'
if ((Test-Path -LiteralPath $LegacyDir) -and -not (Test-Path -LiteralPath $DataDir)) {
    try {
        New-Item -ItemType Directory -Path $DataDir -Force | Out-Null
        $legacyFiles = @(
            @('台账.tsv',         $LedgerPath),
            @('配置.txt',         $ConfigPath),
            @('最近一次移动.txt',  $LastMovePath)
        )
        foreach ($pair in $legacyFiles) {
            $from = Join-Path $LegacyDir $pair[0]
            if ((Test-Path -LiteralPath $from) -and -not (Test-Path -LiteralPath $pair[1])) {
                Copy-Item -LiteralPath $from -Destination $pair[1] -Force -ErrorAction SilentlyContinue
            }
        }
        $legacyDirs = @(
            @('报告',   $ReportDir),
            @('缩略图', $ThumbDir)
        )
        foreach ($pair in $legacyDirs) {
            $from = Join-Path $LegacyDir $pair[0]
            if (Test-Path -LiteralPath $from) {
                New-Item -ItemType Directory -Path $pair[1] -Force | Out-Null
                Copy-Item -Path (Join-Path $from '*') -Destination $pair[1] -Force -Recurse -ErrorAction SilentlyContinue
            }
        }
        Move-Item -LiteralPath $LegacyDir -Destination (Join-Path $ToolRoot '_旧数据_可删除') -Force -ErrorAction SilentlyContinue
    } catch { }
}

foreach ($d in @($DataDir, $ThumbDir, $ReportDir)) {
    if (-not (Test-Path -LiteralPath $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
}

# ============================ 输出 ============================
function Write-Title($t) { Write-Host ""; Write-Host "  $t" -ForegroundColor Cyan }
function Write-Info($t)  { Write-Host "    $t" -ForegroundColor Gray }
function Write-Ok($t)    { Write-Host "    $t" -ForegroundColor Green }
function Write-Warn($t)  { Write-Host "    $t" -ForegroundColor Yellow }
function Write-Bad($t)   { Write-Host "    $t" -ForegroundColor Red }

function Wait-Close { if (-not $NoPause) { Write-Host ""; [void](Read-Host (T '按回车键关闭窗口')) } }
function Open-Item  { param([string]$p) if (-not $NoOpen) { try { Start-Process $p } catch { } } }

# ======================= 定位 ScreenShot =======================
function Test-ScreenShotDir {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) { return $false }
    $x6 = Split-Path -Parent $Path.TrimEnd('\')
    if ((Split-Path -Leaf $x6) -ne 'X6Game') { return $false }
    return (Test-Path -LiteralPath (Join-Path $x6 'Saved\GamePlayPhotos') -PathType Container)
}

function Get-SavedGameDir {
    if (Test-Path -LiteralPath $ConfigPath) {
        foreach ($line in (Get-Content -LiteralPath $ConfigPath -Encoding UTF8)) {
            # 兼容中文键（旧版本）和英文键
            if ($line -match '^\s*(?:游戏目录|GameDir)\s*=\s*(.+)$') {
                $p = $Matches[1].Trim().Trim('"')
                if (Test-ScreenShotDir $p) { return $p }
            }
        }
    }
    return $null
}

function Save-GameDir {
    param([string]$Path)
    $key = $(if ($script:UiLang -eq 'en') { 'GameDir' } else { '游戏目录' })

    # 保留其它行（尤其是 Language=），只替换游戏目录这一行
    $kept = @()
    if (Test-Path -LiteralPath $ConfigPath) {
        foreach ($line in (Get-Content -LiteralPath $ConfigPath -Encoding UTF8)) {
            if ($line -match '^\s*(?:游戏目录|GameDir)\s*=') { continue }
            $kept += $line
        }
    }
    if ($kept.Count -eq 0) {
        $kept = @(
            '# ' + (T '无限暖暖本地照片清理助手 - 配置'),
            '# ' + (T '若游戏换盘或重装，删掉本文件重新运行即可。')
        )
    }
    $kept += "$key=$Path"

    [IO.File]::WriteAllText($ConfigPath, ($kept -join "`r`n"), (New-Object Text.UTF8Encoding $false))
}

function Find-ScreenShotDir {
    param([string]$Hint)

    if ($Hint -and (Test-ScreenShotDir $Hint)) { return $Hint }

    $saved = Get-SavedGameDir
    if ($saved) { return $saved }

    # 1) 工具本身被放在 ScreenShot 或游戏目录里
    foreach ($c in @($ToolRoot, (Join-Path $ToolRoot 'ScreenShot'), (Join-Path $ToolRoot 'X6Game\ScreenShot'))) {
        if (Test-ScreenShotDir $c) { return $c }
    }

    # 2) 各固定磁盘浅层探测（限时，避免大盘卡死）
    Write-Info (T '正在自动查找游戏目录…')
    $sw = [Diagnostics.Stopwatch]::StartNew()
    foreach ($drive in ([IO.DriveInfo]::GetDrives() | Where-Object { $_.DriveType -eq 'Fixed' })) {
        if ($sw.Elapsed.TotalSeconds -gt 12) { break }
        $root = $drive.RootDirectory.FullName
        $level1 = @()
        try { $level1 = Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue } catch { }
        foreach ($d1 in $level1) {
            if ($sw.Elapsed.TotalSeconds -gt 12) { break }
            foreach ($suffix in @('X6Game\ScreenShot', 'InfinityNikki\X6Game\ScreenShot')) {
                $cand = Join-Path $d1.FullName $suffix
                if (Test-ScreenShotDir $cand) { return $cand }
            }
            # 再多探一层：X:\xxx\InfinityNikki\X6Game\ScreenShot
            $level2 = @()
            try { $level2 = Get-ChildItem -LiteralPath $d1.FullName -Directory -ErrorAction SilentlyContinue } catch { }
            foreach ($d2 in $level2) {
                if ($sw.Elapsed.TotalSeconds -gt 12) { break }
                foreach ($suffix in @('X6Game\ScreenShot', 'InfinityNikki\X6Game\ScreenShot')) {
                    $cand = Join-Path $d2.FullName $suffix
                    if (Test-ScreenShotDir $cand) { return $cand }
                }
            }
        }
    }

    # 3) 让用户手选
    Write-Warn (T '没能自动找到游戏目录，请在下面对话框里选中游戏里的 ScreenShot 文件夹。')
    Add-Type -AssemblyName System.Windows.Forms
    $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
    $dlg.Description = (T '请选择：无限暖暖\X6Game\ScreenShot 文件夹')
    $dlg.ShowNewFolderButton = $false
    if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        if (Test-ScreenShotDir $dlg.SelectedPath) { return $dlg.SelectedPath }
        Write-Bad (T '所选目录看起来不是游戏的 ScreenShot 目录（其上一级应为 X6Game）。')
    }
    return $null
}


# ======================= 清理工具记录 =======================
function Format-Bytes([long]$b) {
    if ($b -ge 1GB) { return ('{0:N2} GB' -f ($b / 1GB)) }
    if ($b -ge 1MB) { return ('{0:N1} MB' -f ($b / 1MB)) }
    if ($b -ge 1KB) { return ('{0:N0} KB' -f ($b / 1KB)) }
    return "$b B"
}

if ($Mode -eq 'Clean') {

    Write-Title (T '清理工具自己产生的记录')
    Write-Info (T '这些文件只用于保存判定依据和还原信息，清理它们不影响游戏，也不会动到任何照片。')
    Write-Host ''

    # 目标截图目录：优先命令行参数，其次配置；都没有就跳过「空文件夹」这一项
    $ss = $GameDir
    if (-not $ss) { $ss = Get-SavedGameDir }

    $repFiles   = @()
    $thumbFiles = @()
    $emptyDirs  = @()

    if (Test-Path -LiteralPath $ReportDir) {
        $repFiles = @(Get-ChildItem -LiteralPath $ReportDir -File -Filter '*.html' -ErrorAction SilentlyContinue |
                      Sort-Object LastWriteTime -Descending)
    }
    if (Test-Path -LiteralPath $ThumbDir) {
        $thumbFiles = @(Get-ChildItem -LiteralPath $ThumbDir -File -Filter '*.jpg' -ErrorAction SilentlyContinue)
    }
    if ($ss -and (Test-Path -LiteralPath $ss)) {
        $cands = @()
        $pendFixed = Get-PendingDir $ss
        if (Test-Path -LiteralPath $pendFixed) { $cands += @(Get-Item -LiteralPath $pendFixed) }
        $cands += @(Get-ChildItem -LiteralPath $ss -Directory -Filter '_待确认删除_*' -ErrorAction SilentlyContinue)
        $emptyDirs = @($cands | Where-Object { @(Get-ChildItem -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue).Count -eq 0 })
    }

    $hasMove   = Test-Path -LiteralPath $LastMovePath
    $hasLedger = Test-Path -LiteralPath $LedgerPath

    $repBytes    = [long](($repFiles   | Measure-Object -Property Length -Sum).Sum)
    $thumbBytes  = [long](($thumbFiles | Measure-Object -Property Length -Sum).Sum)
    $moveBytes   = 0L; if ($hasMove)   { $moveBytes   = (Get-Item -LiteralPath $LastMovePath).Length }
    $ledgerBytes = 0L; if ($hasLedger) { $ledgerBytes = (Get-Item -LiteralPath $LedgerPath).Length }

    $colW = $(if ($script:UiLang -eq 'en') { 16 } else { 12 })
    $fmt  = "     {0}.    {1,-$colW} {2,8} {3,12}"

    Write-Host (T '     序号  项目                 数量        占用') -ForegroundColor DarkGray
    Write-Host ($fmt -f 1, (T '网页报告'),     (TF '{0} 份' $repFiles.Count),   (Format-Bytes $repBytes))    -ForegroundColor Green
    Write-Host ($fmt -f 2, (T '缩略图缓存'),   (TF '{0} 个' $thumbFiles.Count), (Format-Bytes $thumbBytes))  -ForegroundColor Green
    Write-Host ($fmt -f 3, (T '待清理空文件夹'), (TF '{0} 个' $emptyDirs.Count),  '-')                        -ForegroundColor Green
    Write-Host ($fmt -f 4, (T '移动记录'),     $(if ($hasMove)   { TF '{0} 份' 1 } else { T '无' }), (Format-Bytes $moveBytes))   -ForegroundColor DarkYellow
    Write-Host ($fmt -f 5, (T '判定台账'),     $(if ($hasLedger) { TF '{0} 份' 1 } else { T '无' }), (Format-Bytes $ledgerBytes)) -ForegroundColor DarkYellow

    Write-Host ''
    Write-Host (T '     绿色 = 安全项，删了会自动重建') -ForegroundColor Gray
    Write-Host (T '     黄色 = 危险项，删了会失去还原能力或重置判定') -ForegroundColor Gray
    Write-Host ''
    Write-Info (T '回车 = 只清安全项（1 2 3）；输入 a = 连同台账和移动记录一起清；输入 n = 取消')
    Write-Host ''

    $ans = ''
    if (-not $Yes) {
        $ans = Read-Host (T '     请选择')
        if ($ans -match '^[nN]') {
            Write-Info (T '已取消，未做任何改动。')
            Wait-Close
            exit 0
        }
    }
    $includeRisky = ($AllRecords -or ($ans -match '^[aA]'))

    if ($includeRisky) {
        Write-Host ''
        Write-Warn (T '清理台账后，所有照片的判定会重置为「疑似」；清理移动记录后「还原上次移动」会失效。')
        if (-not $Yes) {
            $c = Read-Host (T '     确认连危险项一起清理？输入 yes 继续')
            if ($c -ne 'yes') {
                Write-Info (T '已取消，未做任何改动。')
                Wait-Close
                exit 0
            }
        }
    }

    Write-Host ''
    $freed  = 0L
    $errors = 0
    $done   = @()

    # 1. 报告：保留最近 3 份
    $killRep = @($repFiles | Select-Object -Skip 3)
    $n = 0
    foreach ($f in $killRep) {
        try { $freed += $f.Length; Remove-Item -LiteralPath $f.FullName -Force -ErrorAction Stop; $n++ }
        catch { $errors++ }
    }
    if ($n -gt 0) { $done += (TF '报告 {0} 份' $n) }

    # 2. 缩略图：全清，下次生成报告自动重建
    $n = 0
    foreach ($f in $thumbFiles) {
        try { $freed += $f.Length; Remove-Item -LiteralPath $f.FullName -Force -ErrorAction Stop; $n++ }
        catch { $errors++ }
    }
    if ($n -gt 0) { $done += (TF '缩略图 {0} 个' $n) }

    # 3. 待确认空文件夹
    $n = 0
    foreach ($d in $emptyDirs) {
        try { Remove-Item -LiteralPath $d.FullName -Recurse -Force -ErrorAction Stop; $n++ }
        catch { $errors++ }
    }
    if ($n -gt 0) { $done += (TF '空文件夹 {0} 个' $n) }

    # 4/5. 危险项
    if ($includeRisky) {
        if ($hasMove) {
            try { $freed += $moveBytes; Remove-Item -LiteralPath $LastMovePath -Force -ErrorAction Stop; $done += (T '移动记录') }
            catch { $errors++ }
        }
        if ($hasLedger) {
            try { $freed += $ledgerBytes; Remove-Item -LiteralPath $LedgerPath -Force -ErrorAction Stop; $done += (T '判定台账') }
            catch { $errors++ }
        }
    }

    if ($done.Count -eq 0) {
        Write-Info (T '没有需要清理的内容。')
    } else {
        Write-Ok ((T '已清理：') + ($done -join (T '、')))
        if ($freed -gt 0) { Write-Info ((T '释放空间：') + (Format-Bytes $freed)) }
        if ($errors -gt 0) { Write-Warn (TF '有 {0} 个文件被占用或权限不足，未能删除。' $errors) }
    }

    Write-Host ''
    Wait-Close
    exit 0
}

# ======================= 扫描相册缓存 =======================
function Get-AlbumInfo {
    param([string]$ScreenShotDir)
    $x6  = Split-Path -Parent $ScreenShotDir.TrimEnd('\')
    $gp  = Join-Path $x6 'Saved\GamePlayPhotos'
    $set = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    $dirs = New-Object System.Collections.Generic.List[string]

    if (Test-Path -LiteralPath $gp) {
        foreach ($uid in (Get-ChildItem -LiteralPath $gp -Directory -ErrorAction SilentlyContinue)) {
            foreach ($sub in (Get-ChildItem -LiteralPath $uid.FullName -Directory -ErrorAction SilentlyContinue)) {
                if ($sub.Name -like 'NikkiPhotos_*') {
                    $dirs.Add($sub.FullName)
                    foreach ($f in (Get-ChildItem -LiteralPath $sub.FullName -File -ErrorAction SilentlyContinue)) {
                        [void]$set.Add($f.Name)
                    }
                }
            }
        }
    }
    return [pscustomobject]@{ Names = $set; Dirs = $dirs; Root = $gp }
}

# ======================= 台账 =======================
function Read-Ledger {
    $h = @{}
    if (-not (Test-Path -LiteralPath $LedgerPath)) { return $h }
    foreach ($line in (Get-Content -LiteralPath $LedgerPath -Encoding UTF8)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        if ($line.StartsWith('#')) { continue }
        $f = $line -split "`t"
        if ($f.Count -ge 4) {
            $h[$f[0]] = [pscustomobject]@{
                FirstSeen   = $f[1]
                LastSeen    = $f[2]
                EverInAlbum = ($f[3] -eq '1')
            }
        }
    }
    return $h
}

function Save-Ledger {
    param($Ledger)
    $sb = New-Object Text.StringBuilder
    [void]$sb.AppendLine("# 文件名`t首次进入相册`t最后见到`t是否在册")
    foreach ($k in ($Ledger.Keys | Sort-Object)) {
        $v = $Ledger[$k]
        $flag = if ($v.EverInAlbum) { '1' } else { '0' }
        [void]$sb.AppendLine(("{0}`t{1}`t{2}`t{3}" -f $k, $v.FirstSeen, $v.LastSeen, $flag))
    }
    [IO.File]::WriteAllText($LedgerPath, $sb.ToString(), (New-Object Text.UTF8Encoding $false))
}

# ======================= 缩略图 =======================
Add-Type -AssemblyName System.Drawing

function New-Thumbnail {
    param([string]$Src, [string]$Dst, [int]$Size = 300)
    try {
        $fs = [IO.File]::Open($Src, 'Open', 'Read', 'ReadWrite')
        $img = [System.Drawing.Image]::FromStream($fs)
        try {
            $ratio = [Math]::Min($Size / $img.Width, $Size / $img.Height)
            $w = [int]($img.Width * $ratio); $h = [int]($img.Height * $ratio)
            $bmp = New-Object System.Drawing.Bitmap $Size, $Size
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            $g.Clear([System.Drawing.Color]::FromArgb(238, 238, 238))
            $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g.DrawImage($img, [int](($Size - $w) / 2), [int](($Size - $h) / 2), $w, $h)
            $g.Dispose()
            $bmp.Save($Dst, [System.Drawing.Imaging.ImageFormat]::Jpeg)
            $bmp.Dispose()
            return $true
        } finally { $img.Dispose(); $fs.Dispose() }
    } catch {
        return $false
    }
}

# ======================= 主流程 =======================
$now = Get-Date

Write-Host ""
Write-Host "  ============================================" -ForegroundColor Cyan
Write-Host ("   " + (T '无限暖暖 · 本地照片清理助手')) -ForegroundColor Cyan
Write-Host ("   " + (T '（只做识别与归拢，绝不自动删除照片）')) -ForegroundColor DarkGray
Write-Host "  ============================================" -ForegroundColor Cyan

Write-Title (T '第 1 步：定位游戏目录')
$screenShot = Find-ScreenShotDir -Hint $GameDir
if (-not $screenShot) {
    Write-Bad (T '未能定位到游戏的 ScreenShot 目录，已退出。')
    Write-Host ""
    Wait-Close
    exit 1
}
Save-GameDir $screenShot
Write-Ok ((T '截图目录：') + $screenShot)

Write-Title (T '第 2 步：读取游戏内相册缓存')
$album = Get-AlbumInfo -ScreenShotDir $screenShot
Write-Info ((T '缓存目录数：') + $album.Dirs.Count)
Write-Info ((TF '相册在册照片：{0} 张' $album.Names.Count))
if ($album.Dirs.Count -eq 0) {
    Write-Warn (T '未找到 NikkiPhotos_* 缓存目录，可能从未在游戏里使用过相册。')
}

Write-Title (T '第 3 步：比对本地照片')
$files = @(Get-ChildItem -LiteralPath $screenShot -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -match '^\.(jpe?g|png)$' })
if ($files.Count -eq 0) {
    Write-Warn (T 'ScreenShot 目录里没有照片，无需处理。')
    Write-Host ""
    Wait-Close
    exit 0
}

$ledger = Read-Ledger
$isFirstRun = (-not (Test-Path -LiteralPath $LedgerPath))

$rows = New-Object System.Collections.Generic.List[object]
foreach ($f in $files) {
    $inAlbum = $album.Names.Contains($f.Name)
    $led = $ledger[$f.Name]
    $everIn = [bool]($led -and $led.EverInAlbum)

    if ($inAlbum) {
        $status = $S_INALBUM
    } elseif ($everIn) {
        $status = $S_DELETED
    } else {
        $status = $S_SUSPECT
    }

    $ageDays = ($now - $f.LastWriteTime).TotalDays
    if ($status -ne $S_INALBUM -and $ageDays -lt $MinAgeDays) {
        $status = $S_TOONEW
    }

    # 更新台账
    if ($inAlbum) {
        if (-not $ledger.ContainsKey($f.Name)) {
            $ledger[$f.Name] = [pscustomobject]@{ FirstSeen = $now.ToString('yyyy-MM-dd HH:mm:ss'); LastSeen = ''; EverInAlbum = $true }
        }
        $ledger[$f.Name].EverInAlbum = $true
        $ledger[$f.Name].LastSeen    = $now.ToString('yyyy-MM-dd HH:mm:ss')
    } elseif ($ledger.ContainsKey($f.Name)) {
        $ledger[$f.Name].LastSeen = $now.ToString('yyyy-MM-dd HH:mm:ss')
    }

    [void]$rows.Add([pscustomobject]@{
        Name     = $f.Name
        Path     = $f.FullName
        Size     = $f.Length
        Time     = $f.LastWriteTime
        AgeDays  = [Math]::Round($ageDays, 1)
        Status   = $status
        Reason   = (Get-ReasonText $status $MinAgeDays)
        Thumb    = ''
    })
}
Save-Ledger $ledger

$order = @{ 'Deleted' = 0; 'Suspect' = 1; 'TooNew' = 2; 'InAlbum' = 3 }
$rows = @($rows | Sort-Object @{ Expression = { $order[$_.Status] } }, @{ Expression = { $_.Time }; Descending = $true })

$cntDeleted = @($rows | Where-Object { $_.Status -eq $S_DELETED }).Count
$cntSuspect = @($rows | Where-Object { $_.Status -eq $S_SUSPECT }).Count
$cntKept    = @($rows | Where-Object { $_.Status -eq $S_INALBUM }).Count
$cntNew     = @($rows | Where-Object { $_.Status -eq $S_TOONEW }).Count

Write-Ok (TF '本地照片共 {0} 张' $rows.Count)
Write-Info (TF '相册仍在册：{0} 张' $cntKept)
Write-Info (TF '判定已删除：{0} 张' $cntDeleted)
Write-Info (TF '疑似残留：{0} 张' $cntSuspect)
Write-Info (TF '太新跳过：{0} 张' $cntNew)

# ======================= 安全闸门 =======================
Write-Title (T '第 4 步：安全检查')
$blockers = New-Object System.Collections.Generic.List[string]

if ($album.Names.Count -eq 0) {
    $blockers.Add((T '游戏内相册缓存为空。可能是你清空过缓存，此时比对结果不可信。'))
}
if ($rows.Count -ge 5 -and (($cntDeleted + $cntSuspect) / $rows.Count) -gt 0.7) {
    $blockers.Add((T '超过 70% 的本地照片被判定为待删，比例异常，建议先人工抽查。'))
}
$proc = Get-Process -Name 'InfinityNikki' -ErrorAction SilentlyContinue
if ($proc) {
    # 只提示不阻断：文件真被占用时移动会失败并如实报告
    Write-Warn (T '游戏正在运行：刚在游戏内删除的照片，本地缓存可能还没刷新，建议稍等几秒再重新扫描。')
}

if ($blockers.Count -eq 0) {
    Write-Ok (T '安全检查通过')
} else {
    foreach ($b in $blockers) { Write-Bad $b }
    Write-Warn (T '本次将强制只生成报告，不会移动任何文件。')
}

# ======================= 生成缩略图 + 报告 =======================
Write-Title (T '第 5 步：生成预览报告')
$needThumb = @($rows | Where-Object { $_.Status -ne $S_INALBUM })
$made = 0
foreach ($r in $needThumb) {
    $thumbName = ($r.Name -replace '[^\w\.\-]', '_') + '.jpg'
    $thumbPath = Join-Path $ThumbDir $thumbName
    if (-not (Test-Path -LiteralPath $thumbPath)) {
        if (New-Thumbnail -Src $r.Path -Dst $thumbPath -Size 300) { $made++ }
    } else { $made++ }
    if (Test-Path -LiteralPath $thumbPath) { $r.Thumb = $thumbPath }
}
# 清理孤儿缩略图
$liveNames = @{}
foreach ($r in $rows) { $liveNames[(($r.Name -replace '[^\w\.\-]', '_') + '.jpg')] = $true }
foreach ($t in (Get-ChildItem -LiteralPath $ThumbDir -File -ErrorAction SilentlyContinue)) {
    if (-not $liveNames.ContainsKey($t.Name)) { Remove-Item -LiteralPath $t.FullName -Force -ErrorAction SilentlyContinue }
}
Write-Ok (TF '已生成 {0} 张缩略图' $made)

function Esc([string]$s) {
    if ($null -eq $s) { return '' }
    return $s.Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;').Replace('"', '&quot;')
}
function Fmt-Size([long]$b) {
    if ($b -ge 1MB) { return ('{0:N1} MB' -f ($b / 1MB)) }
    if ($b -ge 1KB) { return ('{0:N0} KB' -f ($b / 1KB)) }
    return "$b B"
}

$sb = New-Object Text.StringBuilder
[void]$sb.AppendLine("<!DOCTYPE html><html lang=""$(if ($script:UiLang -eq 'en') { 'en' } else { 'zh-CN' })""><head><meta charset=""utf-8"">")
[void]$sb.AppendLine("<title>$(T '无限暖暖 · 相册残留对账报告')</title>")
[void]$sb.AppendLine(@'
<style>
*{box-sizing:border-box}
body{margin:0;padding:28px 24px 60px;background:#f5f6f8;color:#1f2328;
     font-family:"Microsoft YaHei","PingFang SC","Segoe UI",system-ui,sans-serif}
h1{font-size:21px;margin:0 0 6px;font-weight:600}
.sub{color:#6b7280;font-size:13px;margin-bottom:22px;line-height:1.7}
.stats{display:flex;gap:12px;flex-wrap:wrap;margin-bottom:22px}
.stat{background:#fff;border:1px solid #e5e7eb;border-radius:10px;padding:12px 18px;min-width:112px}
.stat .n{font-size:22px;font-weight:600;line-height:1.2}
.stat .l{font-size:12px;color:#6b7280;margin-top:3px}
.n-del{color:#dc2626}.n-sus{color:#d97706}.n-keep{color:#059669}.n-new{color:#6b7280}
h2{font-size:15px;margin:26px 0 12px;padding-left:9px;border-left:3px solid #2563eb;font-weight:600}
.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(232px,1fr));gap:14px}
.card{background:#fff;border:1px solid #e5e7eb;border-radius:10px;overflow:hidden;
      box-shadow:0 1px 2px rgba(16,24,40,.04)}
.card.del{border-color:#fca5a5}
.card.sus{border-color:#fcd34d}
.card img{width:100%;height:150px;object-fit:cover;display:block;background:#eee}
.noimg{width:100%;height:150px;display:flex;align-items:center;justify-content:center;
       background:#f3f4f6;color:#9ca3af;font-size:12px}
.meta{padding:9px 11px 11px}
.name{font-size:11px;color:#374151;word-break:break-all;line-height:1.45;
      font-family:Consolas,monospace}
.row{display:flex;justify-content:space-between;align-items:center;margin-top:6px;
     font-size:11px;color:#9ca3af}
.badge{font-size:11px;padding:1px 7px;border-radius:20px;font-weight:600}
.b-del{background:#fee2e2;color:#b91c1c}
.b-sus{background:#fef3c7;color:#92400e}
.b-new{background:#e5e7eb;color:#4b5563}
.b-keep{background:#d1fae5;color:#047857}
.tip{background:#eff6ff;border:1px solid #bfdbfe;border-radius:9px;padding:12px 15px;
     font-size:12.5px;color:#1e40af;line-height:1.75;margin-bottom:20px}
.empty{color:#9ca3af;font-size:13px;padding:16px 0}
footer{margin-top:36px;font-size:11.5px;color:#9ca3af;border-top:1px solid #e5e7eb;padding-top:14px}
</style></head><body>
'@)

[void]$sb.AppendLine("<h1>$(T '相册残留对账报告')</h1>")
[void]$sb.AppendLine("<div class='sub'>$(TF '生成时间：{0}<br>截图目录：{1}' -fmt @((Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), (Esc $screenShot)))</div>")

[void]$sb.AppendLine('<div class="stats">')
[void]$sb.AppendLine("  <div class='stat'><div class='n n-del'>$cntDeleted</div><div class='l'>$(T '相册已删除')</div></div>")
[void]$sb.AppendLine("  <div class='stat'><div class='n n-sus'>$cntSuspect</div><div class='l'>$(T '疑似残留')</div></div>")
[void]$sb.AppendLine("  <div class='stat'><div class='n n-keep'>$cntKept</div><div class='l'>$(T '仍在册')</div></div>")
[void]$sb.AppendLine("  <div class='stat'><div class='n n-new'>$cntNew</div><div class='l'>$(T '太新跳过')</div></div>")
[void]$sb.AppendLine('</div>')

# 报告里的说明块含大量 HTML，直接按语言给两段，避免把标签塞进语言表
if ($script:UiLang -eq 'en') {
    [void]$sb.AppendLine(@'
<div class="tip">
<b>How to read this report:</b> "Deleted in album" means you removed it in game while the local copy is still here — safe to clean.<br>
"Unsure leftovers" were never seen in the album cache: maybe deleted, maybe just not synced — <b>check the picture first</b>.<br>
"Still in album" is not listed below and will not be touched.
</div>
'@)
} else {
    [void]$sb.AppendLine(@'
<div class="tip">
<b>怎么用这份报告：</b>「相册已删除」= 你在游戏里删掉了，本地还留着，可以清；<br>
「疑似残留」= 相册缓存里没见过它，可能删过也可能只是没同步，<b>请先看图确认</b>；<br>
「仍在册」不会出现在下面，也不会被处理。
</div>
'@)
}

$groups = @(
    @{ Key = $S_DELETED; Title = '一、相册已删除，本地仍在（建议清理）'; Cls = 'del'; Badge = 'b-del' },
    @{ Key = $S_SUSPECT; Title = '二、疑似残留（请人工确认后再决定）';   Cls = 'sus'; Badge = 'b-sus' },
    @{ Key = $S_TOONEW;  Title = '三、太新已跳过（保护新照片）';         Cls = '';    Badge = 'b-new' }
)

foreach ($g in $groups) {
    $list = @($rows | Where-Object { $_.Status -eq $g.Key })
    $h2 = TF '{0} · {1} 张' -fmt @((T $g.Title), $list.Count)
    [void]$sb.AppendLine("<h2>$h2</h2>")
    if ($list.Count -eq 0) {
        [void]$sb.AppendLine("<div class='empty'>$(T '没有这一类。')</div>")
        continue
    }
    [void]$sb.AppendLine('<div class="grid">')
    foreach ($r in $list) {
        $img = if ($r.Thumb) { "<img src='file:///$(Esc ($r.Thumb -replace '\\','/'))' loading='lazy' alt=''>" }
               else { "<div class='noimg'>$(T '预览不可用')</div>" }
        [void]$sb.AppendLine("<div class='card $($g.Cls)'>$img<div class='meta'>")
        [void]$sb.AppendLine("<div class='name'>$(Esc $r.Name)</div>")
        [void]$sb.AppendLine("<div class='row'><span>$($r.Time.ToString('yyyy-MM-dd HH:mm')) · $(Fmt-Size $r.Size)</span><span class='badge $($g.Badge)'>$(Get-StatusText $r.Status)</span></div>")
        [void]$sb.AppendLine('</div></div>')
    }
    [void]$sb.AppendLine('</div>')
}

[void]$sb.AppendLine("<footer>$(TF '本报告由「无限暖暖 · 本地照片清理助手」生成，仅用于识别与核对，不会删除任何文件。<br>判定阈值：拍摄满 {0} 天；相册缓存目录 {1} 个。' -fmt @($MinAgeDays, $album.Dirs.Count))</footer>")
[void]$sb.AppendLine('</body></html>')

$reportPath = Join-Path $ReportDir ((T '对账报告_') + (Get-Date -Format 'yyyyMMdd_HHmmss') + '.html')
[IO.File]::WriteAllText($reportPath, $sb.ToString(), (New-Object Text.UTF8Encoding $false))
Write-Ok ((T '报告已生成：') + $reportPath)

# 只保留最近 20 份报告
Get-ChildItem -LiteralPath $ReportDir -File -Filter '*.html' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -Skip 20 |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue }

# ======================= 首次运行说明 =======================
if ($isFirstRun) {
    Write-Title (T '首次运行提示')
    Write-Warn (T '这是第一次运行，已建立下面这份「相册台账」：')
    Write-Info $LedgerPath
    Write-Info (T '以后游戏里删掉照片，再运行本工具就能准确识别出来（判定为「已删除」）。')
    Write-Info (T '本次只生成报告，不移动任何文件。请先核对报告判断是否准确。')
    Write-Host ""
    Open-Item $reportPath
    Wait-Close
    exit 0
}

# ======================= 移动 =======================
$toMove = @($rows | Where-Object { $_.Status -eq $S_DELETED })
if ($IncludeSuspects) {
    $toMove += @($rows | Where-Object { $_.Status -eq $S_SUSPECT })
}

Write-Title (T '第 6 步：处理')
if ($Mode -eq 'Report' -or $blockers.Count -gt 0) {
    if ($blockers.Count -gt 0) {
        Write-Warn (T '因安全检查未通过，本次不移动文件。')
    } else {
        Write-Info (T '当前为「只出报告」模式，不移动文件。')
    }
    Write-Host ""
    Open-Item $reportPath
    Wait-Close
    exit 0
}

if ($toMove.Count -eq 0) {
    Write-Ok (T '没有需要清理的照片。')
    Write-Host ""
    Open-Item $reportPath
    Wait-Close
    exit 0
}

Write-Host ""
Write-Warn (TF '即将把 {0} 张照片移动到 ScreenShot 下的「待清理照片」文件夹。' $toMove.Count)
Write-Warn (T '这只是移动，不是删除；确认无误后你可以打开该文件夹，Ctrl+A 全选再按 Delete。')
Write-Host ""
Write-Host (T '    待移动清单：') -ForegroundColor Gray
foreach ($r in $toMove) {
    Write-Host (TF '      · {0} [{1}]' -fmt @($r.Name, (Get-StatusText $r.Status))) -ForegroundColor DarkGray
}
Write-Host ""
if (-not $Yes) {
    $ans = Read-Host (T '确认移动？输入 y 回车继续，其他任意键取消')
    if ($ans -notmatch '^[yY]') {
        Write-Info (T '已取消，未做任何改动。')
        Wait-Close
        exit 0
    }
}

$batch   = Get-Date -Format 'yyyyMMdd_HHmmss'
$pending = Get-PendingDir $screenShot
New-Item -ItemType Directory -Path $pending -Force | Out-Null

$pairs = New-Object System.Collections.Generic.List[object]
foreach ($r in $toMove) {
    try {
        $dest = Join-Path $pending $r.Name
        if (Test-Path -LiteralPath $dest) {
            $stem = [IO.Path]::GetFileNameWithoutExtension($r.Name)
            $ext  = [IO.Path]::GetExtension($r.Name)
            $dest = Join-Path $pending ("{0}_{1}{2}" -f $stem, $batch, $ext)
            $i = 2
            while (Test-Path -LiteralPath $dest) {
                $dest = Join-Path $pending ("{0}_{1}_{2}{3}" -f $stem, $batch, $i, $ext)
                $i++
            }
        }
        Move-Item -LiteralPath $r.Path -Destination $dest -Force
        $pairs.Add([pscustomobject]@{ Actual = (Split-Path -Leaf $dest); Original = $r.Name })
    } catch {
        Write-Bad ((TF '移动失败：{0} — {1}' -fmt @($r.Name, $_.Exception.Message)))
    }
}

# 记录必须累积：待清理文件夹是固定的，多次移动会叠在一起
$merged = New-Object System.Collections.Generic.List[string]
$seen = @{}
foreach ($p in $pairs) {
    $seen[$p.Actual] = $true
    $merged.Add("MOVE`t$($p.Actual)`t$($p.Original)")
}
if (Test-Path -LiteralPath $LastMovePath) {
    foreach ($line in (Get-Content -LiteralPath $LastMovePath -Encoding UTF8)) {
        if ($line -notmatch '^MOVE	') { continue }
        $f = $line -split "`t"
        if ($f.Length -lt 2) { continue }
        if ($seen.ContainsKey($f[1])) { continue }
        $seen[$f[1]] = $true
        $merged.Add($line)
    }
}

# 逐行写入：不要写成 @(...) + $merged，PowerShell 会把它拼成一个字符串，
# 结果三条 # 头挤在同一行，还原脚本就再也读不到源目录/目标目录。
$rec = New-Object System.Collections.Generic.List[string]
$rec.Add("# " + (TF '本次移动记录 {0}（共 {1} 条，累积）' -fmt @((Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $merged.Count)))
$rec.Add("# " + (T '源目录') + "`t$screenShot")
$rec.Add("# " + (T '目标目录') + "`t$pending")
foreach ($m in $merged) { $rec.Add($m) }
[IO.File]::WriteAllLines($LastMovePath, $rec.ToArray(), (New-Object Text.UTF8Encoding $true))

Write-Host ""
Write-Ok (TF '已移入 {0} 张：' $pairs.Count)
Write-Info $pending
Write-Host ""
Write-Info (T '接下来：窗口会自动打开这个文件夹，确认无误后 Ctrl+A 全选、按 Delete 删除。')
Write-Info (T '如果发现移错了，双击工具目录里的「还原上次移动」脚本可以全部移回。')
Write-Host ""

Open-Item $pending
Open-Item $reportPath

Wait-Close
