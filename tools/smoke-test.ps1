param(
    [string]$BaseUrl = "http://127.0.0.1:5000",
    [string]$FrontendUrl = "http://127.0.0.1:4200",
    [string]$Email = "admin@khanshop.local",
    [Parameter(Mandatory = $true)]
    [string]$Password
)

$ErrorActionPreference = "Stop"

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

Write-Host "Checking API health..."
$health = Invoke-RestMethod -Uri "$BaseUrl/api/health"
Assert-True ($health.succeeded -and $health.data.status -eq "Healthy") "API health check failed."

Write-Host "Checking login..."
$loginBody = @{ email = $Email; password = $Password } | ConvertTo-Json
$login = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/auth/login" -ContentType "application/json" -Body $loginBody
Assert-True $login.succeeded "Login failed."
$headers = @{ Authorization = "Bearer $($login.data.accessToken)" }

Write-Host "Checking protected endpoints..."
$me = Invoke-RestMethod -Uri "$BaseUrl/api/auth/me" -Headers $headers
Assert-True $me.succeeded "Current user endpoint failed."

$dashboard = Invoke-RestMethod -Uri "$BaseUrl/api/dashboard/summary" -Headers $headers
Assert-True $dashboard.succeeded "Dashboard endpoint failed."

$reports = Invoke-RestMethod -Uri "$BaseUrl/api/reports/operations" -Headers $headers
Assert-True $reports.succeeded "Reports endpoint failed."

$notifications = Invoke-RestMethod -Uri "$BaseUrl/api/notifications/jobs?pageSize=1" -Headers $headers
Assert-True $notifications.succeeded "Notification jobs endpoint failed."

Write-Host "Checking public online store..."
$store = Invoke-RestMethod -Uri "$BaseUrl/api/online-store/products?pageSize=1"
Assert-True $store.succeeded "Online store endpoint failed."

Write-Host "Checking frontend..."
$front = Invoke-WebRequest -UseBasicParsing -Uri $FrontendUrl
Assert-True ($front.StatusCode -eq 200) "Frontend did not return 200."

Write-Host "Smoke test passed."
