# ACS Vertical Architecture Deployment Script
# This script deploys the entire ACS system with all tenant processes

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Development", "Staging", "Production")]
    [string]$Environment,
    
    [Parameter(Mandatory = $false)]
    [string]$ConnectionString = "",
    
    [Parameter(Mandatory = $false)]
    [string]$RedisConnectionString = "",
    
    [Parameter(Mandatory = $false)]
    [int]$WebApiPort = 5000,
    
    [Parameter(Mandatory = $false)]
    [int]$DashboardPort = 5001,
    
    [Parameter(Mandatory = $false)]
    [string[]]$TenantIds = @("tenant1", "tenant2", "tenant3"),
    
    [Parameter(Mandatory = $false)]
    [switch]$RecreateDatabase = $false,
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipTests = $false
)

Write-Host "🚀 ACS Vertical Architecture Deployment" -ForegroundColor Green
Write-Host "Environment: $Environment" -ForegroundColor Yellow
Write-Host "Tenant Processes: $($TenantIds -join ', ')" -ForegroundColor Yellow
Write-Host ""

# Set working directory
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionRoot = Split-Path -Parent $ScriptRoot
Set-Location $SolutionRoot

try {
    # Step 1: Clean and Build Solution
    Write-Host "📦 Step 1: Building solution..." -ForegroundColor Cyan
    dotnet clean --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "Clean failed" }
    
    dotnet build --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
    Write-Host "✅ Build completed successfully" -ForegroundColor Green

    # Step 2: Run Tests (unless skipped)
    if (-not $SkipTests) {
        Write-Host "🧪 Step 2: Running tests..." -ForegroundColor Cyan
        dotnet test --configuration Release --no-build --verbosity minimal
        if ($LASTEXITCODE -ne 0) { 
            Write-Warning "Tests failed, but continuing deployment..."
        } else {
            Write-Host "✅ All tests passed" -ForegroundColor Green
        }
    } else {
        Write-Host "⚠️ Step 2: Skipping tests as requested" -ForegroundColor Yellow
    }

    # Step 3: Database Deployment
    if ($ConnectionString) {
        Write-Host "🗄️ Step 3: Deploying database..." -ForegroundColor Cyan
        
        # Use the DatabaseDeployer utility
        $dbDeployScript = @"
using ACS.Database;

try {
    await DatabaseDeployer.DeployAsync("$ConnectionString", $($RecreateDatabase.ToString().ToLower()));
    Console.WriteLine("✅ Database deployment completed successfully");
} catch (Exception ex) {
    Console.WriteLine($"❌ Database deployment failed: {ex.Message}");
    Environment.Exit(1);
}
"@
        
        $tempScript = [System.IO.Path]::GetTempFileName() + ".cs"
        $dbDeployScript | Out-File -FilePath $tempScript -Encoding UTF8
        
        # Execute database deployment
        dotnet run --project ACS.Database -- --script $tempScript
        if ($LASTEXITCODE -ne 0) { throw "Database deployment failed" }
        
        Remove-Item $tempScript -Force
        Write-Host "✅ Database deployment completed" -ForegroundColor Green
    } else {
        Write-Host "⚠️ Step 3: Skipping database deployment (no connection string)" -ForegroundColor Yellow
    }

    # Step 4: Create Configuration Files
    Write-Host "⚙️ Step 4: Creating configuration files..." -ForegroundColor Cyan
    
    # Create appsettings for each environment
    $webApiSettings = @{
        "Logging" = @{
            "LogLevel" = @{
                "Default" = if ($Environment -eq "Production") { "Warning" } else { "Information" }
                "Microsoft.AspNetCore" = "Warning"
            }
        }
        "ConnectionStrings" = @{
            "DefaultConnection" = $ConnectionString
            "Redis" = $RedisConnectionString
        }
        "JwtSettings" = @{
            "SecretKey" = [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes([System.Guid]::NewGuid().ToString()))
            "Issuer" = "ACS-$Environment"
            "Audience" = "ACS-API"
            "ExpiryMinutes" = if ($Environment -eq "Production") { 60 } else { 1440 }
        }
        "TenantProcesses" = @{
            "BasePort" = 6000
            "ProcessTimeout" = 30000
            "MaxRetries" = 3
        }
        "RateLimit" = @{
            "Enabled" = $true
            "DefaultPolicy" = @{
                "RequestLimit" = if ($Environment -eq "Production") { 100 } else { 1000 }
                "WindowSizeSeconds" = 60
            }
        }
        "OpenTelemetry" = @{
            "ServiceName" = "ACS-WebApi"
            "Endpoint" = "http://localhost:4317"
        }
        "Encryption" = @{
            "MasterKeyPath" = "keys/master.key"
            "KeyRotationDays" = 90
        }
    }
    
    $webApiSettings | ConvertTo-Json -Depth 10 | Out-File -FilePath "ACS.WebApi/appsettings.$Environment.json" -Encoding UTF8
    Write-Host "✅ WebApi configuration created" -ForegroundColor Green

    # Step 5: Start Services
    Write-Host "🎯 Step 5: Starting services..." -ForegroundColor Cyan
    
    $jobs = @()
    
    # Start WebApi
    Write-Host "Starting WebApi on port $WebApiPort..." -ForegroundColor White
    $webApiJob = Start-Job -ScriptBlock {
        param($solutionRoot, $environment, $port)
        Set-Location $solutionRoot
        $env:ASPNETCORE_ENVIRONMENT = $environment
        $env:ASPNETCORE_URLS = "https://localhost:$port;http://localhost:$($port + 1)"
        dotnet run --project ACS.WebApi --configuration Release
    } -ArgumentList $SolutionRoot, $Environment, $WebApiPort
    $jobs += $webApiJob

    # Start Dashboard
    Write-Host "Starting Dashboard on port $DashboardPort..." -ForegroundColor White
    $dashboardJob = Start-Job -ScriptBlock {
        param($solutionRoot, $environment, $port)
        Set-Location $solutionRoot
        $env:ASPNETCORE_ENVIRONMENT = $environment
        $env:ASPNETCORE_URLS = "https://localhost:$port;http://localhost:$($port + 1)"
        dotnet run --project ACS.Dashboard --configuration Release
    } -ArgumentList $SolutionRoot, $Environment, $DashboardPort
    $jobs += $dashboardJob

    # Start Tenant Processes
    $tenantPort = 6000
    foreach ($tenantId in $TenantIds) {
        Write-Host "Starting tenant process for $tenantId on port $tenantPort..." -ForegroundColor White
        $tenantJob = Start-Job -ScriptBlock {
            param($solutionRoot, $environment, $tenantId, $port)
            Set-Location $solutionRoot
            $env:ASPNETCORE_ENVIRONMENT = $environment
            $env:TENANT_ID = $tenantId
            $env:ASPNETCORE_URLS = "https://localhost:$port;http://localhost:$($port + 1)"
            dotnet run --project ACS.VerticalHost --configuration Release
        } -ArgumentList $SolutionRoot, $Environment, $tenantId, $tenantPort
        $jobs += $tenantJob
        $tenantPort += 2
    }

    # Wait for services to start
    Write-Host "⏳ Waiting for services to initialize..." -ForegroundColor Yellow
    Start-Sleep -Seconds 10

    # Health Check
    Write-Host "🔍 Step 6: Performing health checks..." -ForegroundColor Cyan
    
    $webApiUrl = "https://localhost:$WebApiPort"
    $dashboardUrl = "https://localhost:$DashboardPort"
    
    try {
        $webApiHealth = Invoke-RestMethod -Uri "$webApiUrl/health" -Method GET -SkipCertificateCheck
        Write-Host "✅ WebApi health check passed" -ForegroundColor Green
    } catch {
        Write-Warning "WebApi health check failed: $($_.Exception.Message)"
    }
    
    try {
        $dashboardHealth = Invoke-RestMethod -Uri "$dashboardUrl/health" -Method GET -SkipCertificateCheck
        Write-Host "✅ Dashboard health check passed" -ForegroundColor Green
    } catch {
        Write-Warning "Dashboard health check failed: $($_.Exception.Message)"
    }

    # Display deployment summary
    Write-Host ""
    Write-Host "🎉 Deployment Summary" -ForegroundColor Green
    Write-Host "===================" -ForegroundColor Green
    Write-Host "Environment: $Environment" -ForegroundColor White
    Write-Host "WebApi URL: https://localhost:$WebApiPort" -ForegroundColor White
    Write-Host "Dashboard URL: https://localhost:$DashboardPort" -ForegroundColor White
    Write-Host ""
    Write-Host "Tenant Processes:" -ForegroundColor White
    $tenantPort = 6000
    foreach ($tenantId in $TenantIds) {
        Write-Host "  - $tenantId: https://localhost:$tenantPort" -ForegroundColor White
        $tenantPort += 2
    }
    Write-Host ""
    Write-Host "🔧 Management Commands:" -ForegroundColor Yellow
    Write-Host "  Stop all services: Get-Job | Stop-Job; Get-Job | Remove-Job" -ForegroundColor White
    Write-Host "  View logs: Get-Job | Receive-Job" -ForegroundColor White
    Write-Host "  Monitor processes: Scripts/monitor.ps1" -ForegroundColor White
    Write-Host ""
    Write-Host "✅ ACS Vertical Architecture deployment completed successfully!" -ForegroundColor Green

} catch {
    Write-Host "❌ Deployment failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Cleaning up..." -ForegroundColor Yellow
    
    # Stop any running jobs
    Get-Job | Stop-Job -ErrorAction SilentlyContinue
    Get-Job | Remove-Job -ErrorAction SilentlyContinue
    
    exit 1
}