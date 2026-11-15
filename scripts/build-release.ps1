[CmdletBinding()]
param(
    [string]$Version,
    [string]$OutputDir,
    [ValidateSet('all', 'net10')]
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
$thirdParty      = Join-Path $root 'THIRD-PARTY-NOTICES.txt'
$srcDir          = Join-Path $root 'VOCALOIDPatcher\VOCALOIDPatcher'
$readmeName      = 'README 请先读我.txt'

$readmeDefault = @'
VOCALOIDPatcher

用于为 Yamaha VOCALOID7 Editor 提供更多功能的补丁
理论上支持所有 VOCALOID7 编辑器
将压缩包内所有文件解压并替换到 C:\Program Files\VOCALOID7\Editor\ 中即可

------------------------------------------------------------------

A patch that adds extra features to the Yamaha VOCALOID7 Editor.
Works with all VOCALOID7 editors in theory.
Extract all files from the archive and replace them in C:\Program Files\VOCALOID7\Editor\.

https://github.com/IzumiiKonata/VOCALOIDPatcher
https://www.vsqx.top/project/vn15117

Made with <3 by IzumiiKonata
'@

function Resolve-Version {
    if ($Version) { return $Version }
    $text = Get-Content $patcherCs -Raw
    if ($text -match 'Version\s*=>\s*"([^"]+)"') { return $Matches[1] }
    throw "Failed to parse the version from Patcher.cs. Specify it with -Version."
}

function Resolve-ReadmeText {
    $candidates = @(
        (Join-Path $root $readmeName),
        (Join-Path $root "VOCALOIDPatcher\bin\Release\net10.0-windows\out\$readmeName")
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
        Write-Warning "Sources ($($newest.ToString('MM-dd HH:mm'))) are newer than the merged DLL ($($dllTime.ToString('MM-dd HH:mm'))). Rebuild first (add -Build)."
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
    [pscustomobject]@{ Key = 'net10'; Dir = 'net10.0-windows'; Suffix = '' }
)
if ($Tfm -ne 'all') { $targets = $targets | Where-Object Key -eq $Tfm }

if ($Build) {
    Write-Host "Building in Release..." -ForegroundColor Cyan
    & dotnet build $proj -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed. If the VOCALOID7 editor is running, it locks the output DLL; close it and try again."
    }
}

Write-Host "Version: $Version" -ForegroundColor Green
Write-Host "Output directory: $OutputDir" -ForegroundColor Green

$readmeTmp = Join-Path ([System.IO.Path]::GetTempPath()) $readmeName
[System.IO.File]::WriteAllText($readmeTmp, $readmeText, (New-Object System.Text.UTF8Encoding($false)))

$results = @()
foreach ($t in $targets) {
    $tfmDir    = Join-Path $root "VOCALOIDPatcher\bin\Release\$($t.Dir)"
    $mergedDll = Join-Path $tfmDir 'out\Microsoft.Xaml.Behaviors.dll'

    if (-not (Test-Path $mergedDll)) {
        Write-Warning "[$($t.Key)] Merged DLL not found: $mergedDll - skipping (build in Release first, or add -Build)."
        continue
    }

    Warn-IfStale $mergedDll

    $entries = @(
        @{ Source = $mergedDll;    Entry = 'Microsoft.Xaml.Behaviors.dll' },
        @{ Source = $readmeTmp;    Entry = $readmeName },
        @{ Source = $thirdParty;   Entry = 'THIRD-PARTY-NOTICES.txt' },
        @{ Source = $hardcodedMap; Entry = 'VOCALOIDPatcher/HardcodedPropertyMap.xml' }
    )
    foreach ($xml in Get-ChildItem $translationsDir -File -Filter *.xml) {
        $entries += @{ Source = $xml.FullName; Entry = "VOCALOIDPatcher/translations/$($xml.Name)" }
    }

    $zipName = "VOCALOIDPatcher $Version$($t.Suffix).zip"
    $zipPath = Join-Path $OutputDir $zipName
    New-ReleaseZip $zipPath $entries

    $sizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
    Write-Host "[$($t.Key)] Created $zipName ($sizeMb MB)" -ForegroundColor Green
    $results += $zipPath
}

Remove-Item $readmeTmp -Force -ErrorAction SilentlyContinue

if (-not $results) {
    throw "No release package was generated. Make sure Release is built, or check the -Tfm parameter."
}

Write-Host ""
Write-Host "Done. Generated $($results.Count) release package(s):" -ForegroundColor Cyan
$results | ForEach-Object { Write-Host "  $_" }
