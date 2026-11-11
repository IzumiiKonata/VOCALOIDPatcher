param([ValidateSet('on', 'off', 'status')][string]$Mode = 'status')

$root = Split-Path -Parent $PSScriptRoot
$editor = 'C:\Program Files\VOCALOID7\Editor'
$dll = Join-Path $editor 'Microsoft.Xaml.Behaviors.dll'
$bak = Join-Path $editor 'Microsoft.Xaml.Behaviors.dll.bak'
$target = Join-Path $root 'VOCALOIDPatcher\bin\Release\net10.0-windows\out\Microsoft.Xaml.Behaviors.dll'

if ($Mode -ne 'status' -and (Get-Process VOCALOID7 -ErrorAction SilentlyContinue)) {
    Write-Host 'VOCALOID7 is running, close it first.'
    exit 1
}

switch ($Mode) {
    'off' {
        Remove-Item $dll -Force
        Copy-Item $bak $dll
        Write-Host 'Patcher OFF: original Microsoft.Xaml.Behaviors.dll restored.'
    }
    'on' {
        Remove-Item $dll -Force
        New-Item -ItemType SymbolicLink -Path $dll -Target $target | Out-Null
        Write-Host 'Patcher ON: symlink restored.'
    }
    'status' {
        $item = Get-Item $dll
        if ($item.LinkType -eq 'SymbolicLink') { Write-Host 'Patcher ON (symlink)' }
        else { Write-Host 'Patcher OFF (regular file)' }
    }
}
