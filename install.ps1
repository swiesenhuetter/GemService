#Requires -RunAsAdministrator

param(
    [string]$ServiceName = "GemService",
    [string]$DisplayName = "Gem Background Service",
    [string]$Description = "A background worker service",
    [string]$InstallPath = "C:\Services\GemService"
)

Write-Host "Installing $DisplayName..." -ForegroundColor Green

# Stop and remove existing service if it exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Stopping existing service..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force
    Write-Host "Removing existing service..." -ForegroundColor Yellow
    sc.exe delete $ServiceName
    Start-Sleep -Seconds 2
}

# Publish the application
Write-Host "Publishing application..." -ForegroundColor Cyan
dotnet publish -c Release -o $InstallPath --self-contained false

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

# Create the service
Write-Host "Creating Windows Service..." -ForegroundColor Cyan
$binaryPath = Join-Path $InstallPath "$ServiceName.exe"
New-Service -Name $ServiceName `
            -BinaryPathName $binaryPath `
            -DisplayName $DisplayName `
            -Description $Description `
            -StartupType Automatic

# Start the service
Write-Host "Starting service..." -ForegroundColor Cyan
Start-Service -Name $ServiceName

# Verify service is running
$service = Get-Service -Name $ServiceName
if ($service.Status -eq 'Running') {
    Write-Host "✓ Service installed and started successfully!" -ForegroundColor Green
    Write-Host "  Name: $ServiceName" -ForegroundColor Gray
    Write-Host "  Path: $InstallPath" -ForegroundColor Gray
    Write-Host "  Status: $($service.Status)" -ForegroundColor Gray
} else {
    Write-Host "✗ Service installed but failed to start" -ForegroundColor Red
}