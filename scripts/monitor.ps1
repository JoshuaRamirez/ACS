# ACS Vertical Architecture Monitoring Script
# This script monitors all running ACS services and provides real-time status

param(
    [Parameter(Mandatory = $false)]
    [int]$RefreshInterval = 5,
    
    [Parameter(Mandatory = $false)]
    [switch]$ShowLogs = $false,
    
    [Parameter(Mandatory = $false)]
    [string]$LogLevel = "Information"
)

Write-Host "🔍 ACS System Monitor" -ForegroundColor Green
Write-Host "Refresh Interval: $RefreshInterval seconds" -ForegroundColor Yellow
Write-Host "Press Ctrl+C to exit" -ForegroundColor Yellow
Write-Host ""

# Service endpoints to monitor
$services = @(
    @{ Name = "WebApi"; Url = "https://localhost:5000/health"; Port = 5000 },
    @{ Name = "Dashboard"; Url = "https://localhost:5001/health"; Port = 5001 },
    @{ Name = "Tenant-1"; Url = "https://localhost:6000/health"; Port = 6000 },
    @{ Name = "Tenant-2"; Url = "https://localhost:6002/health"; Port = 6002 },
    @{ Name = "Tenant-3"; Url = "https://localhost:6004/health"; Port = 6004 }
)

function Get-ServiceStatus {
    param($service)
    
    try {
        $response = Invoke-RestMethod -Uri $service.Url -Method GET -TimeoutSec 5 -SkipCertificateCheck
        
        return @{
            Name = $service.Name
            Status = "✅ Healthy"
            ResponseTime = (Measure-Command { Invoke-RestMethod -Uri $service.Url -Method GET -TimeoutSec 5 -SkipCertificateCheck }).TotalMilliseconds
            Details = $response
        }
    } catch {
        return @{
            Name = $service.Name
            Status = "❌ Unhealthy"
            ResponseTime = "N/A"
            Error = $_.Exception.Message
        }
    }
}

function Get-ProcessStatus {
    param($port)
    
    $process = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    if ($process) {
        $proc = Get-Process -Id $process.OwningProcess -ErrorAction SilentlyContinue
        if ($proc) {
            return @{
                ProcessName = $proc.ProcessName
                PID = $proc.Id
                Memory = [math]::Round($proc.WorkingSet / 1MB, 2)
                CPU = $proc.CPU
                StartTime = $proc.StartTime
            }
        }
    }
    return $null
}

function Show-SystemStatus {
    Clear-Host
    Write-Host "🔍 ACS System Status - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Green
    Write-Host "=" * 80 -ForegroundColor Gray
    Write-Host ""
    
    $healthyServices = 0
    $totalServices = $services.Count
    
    foreach ($service in $services) {
        $status = Get-ServiceStatus -service $service
        $processInfo = Get-ProcessStatus -port $service.Port
        
        Write-Host "$($status.Name.PadRight(15))" -NoNewline -ForegroundColor White
        Write-Host "$($status.Status)" -NoNewline
        
        if ($status.Status -like "*Healthy*") {
            $healthyServices++
            Write-Host " (${status.ResponseTime}ms)" -ForegroundColor Green
            
            if ($processInfo) {
                Write-Host "  Process: $($processInfo.ProcessName) (PID: $($processInfo.PID))" -ForegroundColor Gray
                Write-Host "  Memory: $($processInfo.Memory) MB, CPU: $([math]::Round($processInfo.CPU, 2))s" -ForegroundColor Gray
                Write-Host "  Started: $($processInfo.StartTime)" -ForegroundColor Gray
            }
        } else {
            Write-Host " - Error: $($status.Error)" -ForegroundColor Red
        }
        Write-Host ""
    }
    
    # Overall system health
    Write-Host "=" * 80 -ForegroundColor Gray
    $healthPercentage = [math]::Round(($healthyServices / $totalServices) * 100, 1)
    
    if ($healthPercentage -eq 100) {
        Write-Host "🎉 System Health: $healthPercentage% ($healthyServices/$totalServices services healthy)" -ForegroundColor Green
    } elseif ($healthPercentage -ge 80) {
        Write-Host "⚠️ System Health: $healthPercentage% ($healthyServices/$totalServices services healthy)" -ForegroundColor Yellow
    } else {
        Write-Host "❌ System Health: $healthPercentage% ($healthyServices/$totalServices services healthy)" -ForegroundColor Red
    }
    
    # Resource usage summary
    $dotnetProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
    if ($dotnetProcesses) {
        $totalMemory = ($dotnetProcesses | Measure-Object WorkingSet -Sum).Sum / 1MB
        Write-Host "💾 Total Memory Usage: $([math]::Round($totalMemory, 2)) MB" -ForegroundColor Cyan
        Write-Host "🔢 Active .NET Processes: $($dotnetProcesses.Count)" -ForegroundColor Cyan
    }
    
    Write-Host ""
    Write-Host "Press Ctrl+C to exit monitoring..." -ForegroundColor Yellow
}

function Show-JobLogs {
    Write-Host "📋 Recent Job Logs:" -ForegroundColor Cyan
    Write-Host "-" * 50 -ForegroundColor Gray
    
    $jobs = Get-Job -State Running -ErrorAction SilentlyContinue
    foreach ($job in $jobs) {
        Write-Host "Job: $($job.Name) (ID: $($job.Id))" -ForegroundColor White
        $jobOutput = Receive-Job -Job $job -Keep | Select-Object -Last 5
        foreach ($line in $jobOutput) {
            if ($line -match "error|exception|fail") {
                Write-Host "  $line" -ForegroundColor Red
            } elseif ($line -match "warn") {
                Write-Host "  $line" -ForegroundColor Yellow
            } else {
                Write-Host "  $line" -ForegroundColor Gray
            }
        }
        Write-Host ""
    }
}

# Main monitoring loop
try {
    while ($true) {
        Show-SystemStatus
        
        if ($ShowLogs) {
            Show-JobLogs
        }
        
        Start-Sleep -Seconds $RefreshInterval
    }
} catch {
    Write-Host ""
    Write-Host "👋 Monitoring stopped." -ForegroundColor Yellow
    exit 0
}