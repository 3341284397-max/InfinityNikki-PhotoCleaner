<#
  无限暖暖 本地照片清理助手 — 移回原目录
  Infinity Nikki photo cleanup helper — move photos back

  把移进「无限暖暖清理助手_待清理照片」的照片全部移回 ScreenShot。
  不删除任何文件。

  语言：-Lang zh|en，也认环境变量 NIKKI_LANG 与 设置.txt 里的 Language= 行。
        界面语言不影响数据目录名（已存在哪个用哪个，与 PhotoSync.ps1 同一套规则）。
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [switch]$NoPause,
    [switch]$Yes,
    [ValidateSet('zh', 'en')]
    [string]$Lang
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch { }

# ============================ 语言与路径 ============================
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
    $LastMovePath = Join-Path $DataDir 'last-move.txt'
    $ConfigPath   = Join-Path $DataDir 'settings.txt'
} else {
    $LastMovePath = Join-Path $DataDir '最近一次移动记录.txt'
    $ConfigPath   = Join-Path $DataDir '设置.txt'
}

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

function T {
    param([string]$zh)
    if ($script:LangTable -and $script:LangTable.ContainsKey($zh)) { return $script:LangTable[$zh] }
    return $zh
}
function TF {
    param([string]$zh, [object[]]$fmt)
    try { return [string]::Format((T $zh), $fmt) } catch { return (T $zh) }
}

# 兼容旧版本的记录位置
if (-not (Test-Path -LiteralPath $LastMovePath)) {
    $legacy = Join-Path $ToolRoot '_数据\最近一次移动.txt'
    if (Test-Path -LiteralPath $legacy) { $LastMovePath = $legacy }
}

function Write-Title($t) { Write-Host ""; Write-Host "  $t" -ForegroundColor Cyan }
function Write-Info($t)  { Write-Host "    $t" -ForegroundColor Gray }
function Write-Ok($t)    { Write-Host "    $t" -ForegroundColor Green }
function Write-Warn($t)  { Write-Host "    $t" -ForegroundColor Yellow }
function Write-Bad($t)   { Write-Host "    $t" -ForegroundColor Red }
function Wait-Close { if (-not $NoPause) { Write-Host ""; [void](Read-Host (T '按回车键关闭窗口')) } }

Write-Host ""
Write-Host "  ============================================" -ForegroundColor Cyan
Write-Host ("   " + (T '无限暖暖 · 照片移回原目录')) -ForegroundColor Cyan
Write-Host "  ============================================" -ForegroundColor Cyan

if (-not (Test-Path -LiteralPath $LastMovePath)) {
    Write-Title (T '没有可还原的记录')
    Write-Info (T '尚未执行过移动，或者记录文件已被清理。')
    Write-Info ((T '记录文件位置：') + $LastMovePath)
    Wait-Close
    exit 0
}

$srcDir  = ''
$destDir = ''
$pairs   = New-Object System.Collections.Generic.List[object]

foreach ($line in (Get-Content -LiteralPath $LastMovePath -Encoding UTF8)) {
    if ($line -match '^#\s*(?:源目录|Source)\s*\t(.+)$') {
        $srcDir = $Matches[1].Trim()
    }
    elseif ($line -match '^#\s*(?:目标目录|Target)\s*\t(.+)$') {
        $destDir = $Matches[1].Trim()
    }
    elseif ($line -match '^MOVE\t([^\t]+)(?:\t(.+))?$') {
        $actual = $Matches[1].Trim()
        $orig   = if ($Matches[2]) { $Matches[2].Trim() } else { $actual }
        $pairs.Add([pscustomobject]@{ Actual = $actual; Original = $orig })
    }
}

Write-Title (T '移动记录')
Write-Info ((T '来源：') + $srcDir)
Write-Info ((T '暂存：') + $destDir)
Write-Info ((TF '数量：{0} 张' $pairs.Count))

if (-not $srcDir -or -not $destDir) {
    Write-Bad (T '记录缺少源目录或目标目录，无法自动还原。')
    Write-Info (T '可以手动把照片从待清理文件夹拖回去。')
    Wait-Close
    exit 1
}

if (-not (Test-Path -LiteralPath $destDir)) {
    Write-Bad (T '待清理文件夹已经不存在了（可能你已经把它删掉了），无法还原。')
    Wait-Close
    exit 1
}

$exist = @($pairs | Where-Object { Test-Path -LiteralPath (Join-Path $destDir $_.Actual) })
if ($exist.Count -eq 0) {
    Write-Warn (T '待清理文件夹里已经没有这些照片了，无需还原。')
    Wait-Close
    exit 0
}

Write-Title (T '将执行还原')
foreach ($p in $exist) { Write-Host "      · $($p.Actual)" -ForegroundColor DarkGray }
Write-Host ""
if (-not $Yes) {
    $ans = Read-Host (TF '确认把这 {0} 张移回原目录？输入 y 回车继续，其他任意键取消' $exist.Count)
    if ($ans -notmatch '^[yY]') {
        Write-Info (T '已取消，未做任何改动。')
        Wait-Close
        exit 0
    }
}

$ok = 0
$failed = 0
$restoredSuffix = $(if ($script:UiLang -eq 'en') { '_restored' } else { '_还原' })

foreach ($p in $exist) {
    $from = Join-Path $destDir $p.Actual
    $to   = Join-Path $srcDir $p.Original
    if (Test-Path -LiteralPath $to) {
        $base = [IO.Path]::GetFileNameWithoutExtension($p.Original)
        $ext  = [IO.Path]::GetExtension($p.Original)
        $to   = Join-Path $srcDir ("{0}{1}{2}{3}" -f $base, $restoredSuffix, (Get-Date -Format 'HHmmss'), $ext)
    }
    try {
        Move-Item -LiteralPath $from -Destination $to -Force
        $ok++
    } catch {
        $failed++
        Write-Bad ((TF '还原失败：{0} — {1}' -fmt @($p.Actual, $_.Exception.Message)))
    }
}

Write-Host ""
Write-Ok (TF '已还原 {0} 张' $ok)

# 全部还完就没必要留着记录了，避免下次误点
if ($ok -gt 0 -and $failed -eq 0) {
    Remove-Item -LiteralPath $LastMovePath -Force -ErrorAction SilentlyContinue
    Write-Info (T '记录已全部还原，记录文件已删除。')
} else {
    Write-Info ((T '还原记录保留：') + $LastMovePath)
    if ($failed -gt 0) { Write-Warn (TF '失败 {0} 张（可能被占用）。' $failed) }
}

if (Test-Path -LiteralPath $destDir) {
    if ((Get-ChildItem -LiteralPath $destDir -Force -ErrorAction SilentlyContinue | Measure-Object).Count -eq 0) {
        Remove-Item -LiteralPath $destDir -Force -ErrorAction SilentlyContinue
        Write-Info (T '待清理文件夹已空，已自动删除。')
    }
}

Wait-Close
