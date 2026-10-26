[CmdletBinding()]
param(
    [string]$Workspace = 'D:\Workshop\KhanShop',
    [string]$ApiTarget = 'C:\inetpub\wwwroot\BShopApi',
    [string]$FrontendTarget = 'C:\inetpub\wwwroot\BShop'
)

$ErrorActionPreference = 'Stop'
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an Administrator PowerShell session.'
}

$apiStage = Join-Path $env:TEMP "BShopApi-publish-$([Guid]::NewGuid().ToString('N'))"
$frontendStage = Join-Path $Workspace 'frontend\dist\retail-shop-web\browser'
if (-not (Test-Path (Join-Path $frontendStage 'index.html'))) {
    throw "Angular staging package is missing: $frontendStage"
}

New-Item -ItemType Directory -Path $apiStage -Force | Out-Null
& dotnet publish (Join-Path $Workspace 'src\RetailShop.Api\RetailShop.Api.csproj') `
    --configuration Release --no-restore --disable-build-servers `
    /p:MSBuildEnableWorkloadResolver=false --output $apiStage
if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $apiStage 'RetailShop.Api.dll'))) {
    throw 'The current API source could not be published to the deployment staging folder.'
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$rollbackRoot = Join-Path $Workspace "backups\iis-finance-deploy-$stamp"
$apiRollback = Join-Path $rollbackRoot 'BShopApi'
$frontendRollback = Join-Path $rollbackRoot 'BShop'
New-Item -ItemType Directory -Path $apiRollback, $frontendRollback -Force | Out-Null
Copy-Item (Join-Path $ApiTarget '*') $apiRollback -Recurse -Force
Copy-Item (Join-Path $FrontendTarget '*') $frontendRollback -Recurse -Force

$offline = Join-Path $ApiTarget 'app_offline.htm'
$deploymentComplete = $false
try {
    [IO.File]::WriteAllText($offline, 'BShop API deployment in progress.')

    & robocopy $apiStage $ApiTarget /E /R:2 /W:1 /XF appsettings.json web.config /XD .keys logs media uploads
    $apiExit = $LASTEXITCODE
    if ($apiExit -ge 8) { throw "API deployment failed with Robocopy exit code $apiExit." }

    & robocopy $frontendStage $FrontendTarget /E /R:2 /W:1 /XF web.config
    $frontendExit = $LASTEXITCODE
    if ($frontendExit -ge 8) { throw "Frontend deployment failed with Robocopy exit code $frontendExit." }

    $deploymentComplete = $true
    Write-Host "Deployment completed. API Robocopy=$apiExit; Frontend Robocopy=$frontendExit"
    Write-Host "Rollback backup: $rollbackRoot"
}
catch {
    Write-Error $_
    Remove-Item $offline -Force -ErrorAction SilentlyContinue

    & robocopy $apiRollback $ApiTarget /E /R:2 /W:1
    $apiRollbackExit = $LASTEXITCODE
    & robocopy $frontendRollback $FrontendTarget /E /R:2 /W:1
    $frontendRollbackExit = $LASTEXITCODE
    throw "Deployment failed and rollback was attempted. API rollback=$apiRollbackExit; Frontend rollback=$frontendRollbackExit."
}
finally {
    Remove-Item $offline -Force -ErrorAction SilentlyContinue
    Remove-Item $apiStage -Recurse -Force -ErrorAction SilentlyContinue
}

if (-not $deploymentComplete) { exit 1 }

Get-Item (Join-Path $ApiTarget 'RetailShop.Api.dll'), (Join-Path $FrontendTarget 'index.html') |
    Select-Object FullName, Length, LastWriteTime
