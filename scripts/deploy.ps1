[CmdletBinding()]
param(
    [string]$Editor = 'C:\Program Files\VOCALOID6\Editor'
)

$ErrorActionPreference = 'Stop'

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole(
        [Security.Principal.WindowsBuiltinRole]::Administrator)
}

if (-not (Test-Admin)) {
    Write-Host 'Requesting administrator privileges...'
    Start-Process powershell -Verb RunAs -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath)
    exit
}

function Fail([string]$message) {
    Write-Host "[ERROR] $message" -ForegroundColor Red
    Read-Host 'Press Enter to exit'
    exit 1
}

function Remove-Link([string]$path) {
    if (-not (Test-Path $path)) { return }
    $item = Get-Item $path -Force
    if ($item.PSIsContainer) {
        [IO.Directory]::Delete($item.FullName)
    }
    else {
        Remove-Item $item.FullName -Force
    }
}

function Is-ReparsePoint([string]$path) {
    ((Get-Item $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
}

$root     = Split-Path -Parent $PSScriptRoot
$dllSrc   = Join-Path $root 'VOCALOIDPatcher\bin\Release\net8.0-windows\out\Microsoft.Xaml.Behaviors.dll'
$mapSrc   = Join-Path $root 'HardcodedPropertyMap.xml'
$transSrc = Join-Path $root 'translations'

$dllDst   = Join-Path $Editor 'Microsoft.Xaml.Behaviors.dll'
$bakDst   = Join-Path $Editor 'Microsoft.Xaml.Behaviors.dll.bak'
$patchDir = Join-Path $Editor 'VOCALOIDPatcher'

if (-not (Test-Path $dllSrc)) {
    Fail "Built dll not found:`n  $dllSrc`nBuild Release first: dotnet build VOCALOIDPatcher\VOCALOIDPatcher.csproj -c Release"
}
if (-not (Test-Path $Editor))   { Fail "Editor directory not found: $Editor" }
if (-not (Test-Path $mapSrc))   { Fail "Not found: $mapSrc" }
if (-not (Test-Path $transSrc)) { Fail "Not found: $transSrc" }

Write-Host "Editor: $Editor"
Write-Host ''

if (Test-Path $bakDst) {
    Write-Host "Backup already exists, keeping it: $bakDst"
    Remove-Link $dllDst
}
elseif (Test-Path $dllDst) {
    if (Is-ReparsePoint $dllDst) {
        Remove-Link $dllDst
    }
    else {
        Rename-Item $dllDst 'Microsoft.Xaml.Behaviors.dll.bak'
        Write-Host 'Renamed original to Microsoft.Xaml.Behaviors.dll.bak'
    }
}

New-Item -ItemType SymbolicLink -Path $dllDst -Target $dllSrc | Out-Null
Write-Host "Linked $dllDst"

if (-not (Test-Path $patchDir)) {
    New-Item -ItemType Directory -Path $patchDir | Out-Null
    Write-Host "Created $patchDir"
}

$mapLink = Join-Path $patchDir 'HardcodedPropertyMap.xml'
Remove-Link $mapLink
New-Item -ItemType SymbolicLink -Path $mapLink -Target $mapSrc | Out-Null
Write-Host "Linked $mapLink"

$transLink = Join-Path $patchDir 'translations'
Remove-Link $transLink
New-Item -ItemType SymbolicLink -Path $transLink -Target $transSrc | Out-Null
Write-Host "Linked $transLink"

Write-Host ''
Write-Host 'Deployment complete.' -ForegroundColor Green
Read-Host 'Press Enter to exit'
