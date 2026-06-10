param([ValidateSet('on', 'off', 'status')][string]$Mode = 'status')

$editor = 'C:\Program Files\VOCALOID6\Editor'
$dll = Join-Path $editor 'Microsoft.Xaml.Behaviors.dll'
$bak = Join-Path $editor 'Microsoft.Xaml.Behaviors.dll.bak'
$target = 'D:\RiderProjects\VOCALOIDPatcher\VOCALOIDPatcher\bin\Release\net8.0-windows\out\Microsoft.Xaml.Behaviors.dll'

if ($Mode -ne 'status' -and (Get-Process VOCALOID6 -ErrorAction SilentlyContinue)) {
    Write-Host 'VOCALOID6 is running, close it first.'
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
