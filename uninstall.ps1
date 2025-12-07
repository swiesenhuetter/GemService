#Requires -RunAsAdministrator

param(
    [string]$ServiceName = "GemService"
)

Write-Host "Uninstalling $ServiceName..." -ForegroundColor Yellow

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -eq 'Running') {
        Write-Host "Stopping service..." -ForegroundColor Cyan
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 2
    }
    
    Write-Host "Removing service..." -ForegroundColor Cyan
    sc.exe delete $ServiceName
    Write-Host "✓ Service uninstalled successfully!" -ForegroundColor Green
} else {
    Write-Host "Service not found." -ForegroundColor Gray
}