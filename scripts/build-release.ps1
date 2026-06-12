[CmdletBinding()]
param(
    [string]$Version,
    [string]$OutputDir,
    [ValidateSet('all', 'net8', 'net6')]
    [string]$Tfm = 'all',
    [switch]$Build
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$root            = Split-Path -Parent $PSScriptRoot
$proj            = Join-Path $root 'VOCALOIDPatcher\VOCALOIDPatcher.csproj'
$patcherCs       = Join-Path $root 'VOCALOIDPatcher\VOCALOIDPatcher\Patcher.cs'
$translationsDir = Join-Path $root 'translations'
$hardcodedMap    = Join-Path $root 'HardcodedPropertyMap.xml'
$srcDir          = Join-Path $root 'VOCALOIDPatcher\VOCALOIDPatcher'
$readmeName      = '请先读我! README.txt'

$readmeDefault = @'
VOCALOIDPatcher
用于为 Yamaha VOCALOID 6 Editor 提供更多功能的补丁
理论上支持所有 V6 编辑器

https://github.com/IzumiiKonata/VOCALOIDPatcher
https://www.vsqx.top/project/vn15117

将压缩包内所有文件解压并替换到 C:\Program Files\VOCALOID6\Editor\ 中即可

Made with <3 by IzumiiKonata
'@

function Resolve-Version {
    if ($Version) { return $Version }
    $text = Get-Content $patcherCs -Raw
    if ($text -match 'Version\s*=>\s*"([^"]+)"') { return $Matches[1] }
    throw "无法从 Patcher.cs 解析版本号，请用 -Version 指定。"
}

function Resolve-ReadmeText {
    $candidates = @(
        (Join-Path $root $readmeName),
        (Join-Path $root "VOCALOIDPatcher\bin\Release\net8.0-windows\out\$readmeName")
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { return (Get-Content $c -Raw -Encoding UTF8) }
    }
    return $readmeDefault
}

function Warn-IfStale([string]$mergedDll) {
    $sources = @()
    $sources += Get-ChildItem $srcDir -Recurse -File -Filter *.cs -ErrorAction SilentlyContinue
    $sources += Get-ChildItem $translationsDir -File -Filter *.xml -ErrorAction SilentlyContinue
    if (Test-Path $hardcodedMap) { $sources += Get-Item $hardcodedMap }
    if (-not $sources) { return }
    $newest = ($sources | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
    $dllTime = (Get-Item $mergedDll).LastWriteTime
    if ($newest -gt $dllTime) {
        Write-Warning "源码 ($($newest.ToString('MM-dd HH:mm'))) 比合并 DLL ($($dllTime.ToString('MM-dd HH:mm'))) 新，建议先重新构建 (加 -Build)。"
    }
}

function New-ReleaseZip([string]$zipPath, $entries) {
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    $fs = [System.IO.File]::Open($zipPath, [System.IO.FileMode]::CreateNew)
    try {
        $archive = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($e in $entries) {
                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                    $archive, $e.Source, $e.Entry,
                    [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
            }
        }
        finally { $archive.Dispose() }
    }
    finally { $fs.Dispose() }
}

$Version = Resolve-Version
$readmeText = Resolve-ReadmeText

if (-not $OutputDir) { $OutputDir = Join-Path $root 'release' }
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir | Out-Null }

$targets = @(
    [pscustomobject]@{ Key = 'net8'; Dir = 'net8.0-windows'; Suffix = '' },
    [pscustomobject]@{ Key = 'net6'; Dir = 'net6.0-windows'; Suffix = '_NET6.0' }
)
if ($Tfm -ne 'all') { $targets = $targets | Where-Object Key -eq $Tfm }

if ($Build) {
    Write-Host "正在以 Release 构建..." -ForegroundColor Cyan
    & dotnet build $proj -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "构建失败。若 VOCALOID6 编辑器正在运行会锁定输出 DLL，请先关闭再试。"
    }
}

Write-Host "版本: $Version" -ForegroundColor Green
Write-Host "输出目录: $OutputDir" -ForegroundColor Green

$readmeTmp = Join-Path ([System.IO.Path]::GetTempPath()) $readmeName
[System.IO.File]::WriteAllText($readmeTmp, $readmeText, (New-Object System.Text.UTF8Encoding($false)))

$results = @()
foreach ($t in $targets) {
    $tfmDir    = Join-Path $root "VOCALOIDPatcher\bin\Release\$($t.Dir)"
    $mergedDll = Join-Path $tfmDir 'out\Microsoft.Xaml.Behaviors.dll'

    if (-not (Test-Path $mergedDll)) {
        Write-Warning "[$($t.Key)] 找不到合并 DLL: $mergedDll —— 跳过 (请先以 Release 构建，或加 -Build)。"
        continue
    }

    Warn-IfStale $mergedDll

    $entries = @(
        @{ Source = $mergedDll;    Entry = 'Microsoft.Xaml.Behaviors.dll' },
        @{ Source = $readmeTmp;    Entry = $readmeName },
        @{ Source = $hardcodedMap; Entry = 'VOCALOIDPatcher/HardcodedPropertyMap.xml' }
    )
    foreach ($xml in Get-ChildItem $translationsDir -File -Filter *.xml) {
        $entries += @{ Source = $xml.FullName; Entry = "VOCALOIDPatcher/translations/$($xml.Name)" }
    }

    $zipName = "VOCALOIDPatcher $Version$($t.Suffix).zip"
    $zipPath = Join-Path $OutputDir $zipName
    New-ReleaseZip $zipPath $entries

    $sizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
    Write-Host "[$($t.Key)] 已生成 $zipName ($sizeMb MB)" -ForegroundColor Green
    $results += $zipPath
}

Remove-Item $readmeTmp -Force -ErrorAction SilentlyContinue

if (-not $results) {
    throw "未生成任何发行包。请确认已构建 Release，或检查 -Tfm 参数。"
}

Write-Host ""
Write-Host "完成，共 $($results.Count) 个发行包:" -ForegroundColor Cyan
$results | ForEach-Object { Write-Host "  $_" }
