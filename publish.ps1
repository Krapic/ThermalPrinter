param([switch]$Test)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$localSdk = Join-Path $PSScriptRoot '.tools/dotnet/dotnet.exe'
$dotnetCommand = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { 'dotnet' }
if ($Test) { & $dotnetCommand test BT583Printer.sln -c Release; if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' } }
& $dotnetCommand publish src/BT583Printer/BT583Printer.csproj -p:PublishProfile=WindowsX64 -o publish/win-x64
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item -LiteralPath README.md,THIRD-PARTY-NOTICES.md -Destination publish/win-x64
$dotnetRoot = Split-Path -Parent (Get-Command $dotnetCommand).Source
$runtimeLicense = Join-Path $dotnetRoot 'LICENSE.txt'
$runtimeNotices = Join-Path $dotnetRoot 'ThirdPartyNotices.txt'
if (Test-Path -LiteralPath $runtimeLicense) { Copy-Item -LiteralPath $runtimeLicense -Destination publish/win-x64/DOTNET-LICENSE.txt }
if (Test-Path -LiteralPath $runtimeNotices) { Copy-Item -LiteralPath $runtimeNotices -Destination publish/win-x64/DOTNET-THIRD-PARTY-NOTICES.txt }
Write-Host 'Ready: publish/win-x64/BT583Printer.exe'
