# ACS Production Rollback Script
# This script rolls back the ACS application to the previous working version

param(
    [Parameter(Mandatory=$false)]
    [string]$Environment = "Production",
    
    [Parameter(Mandatory=$false)]
    [string]$TargetVersion = "",
    
    [Parameter(Mandatory=$false)]
    [int]$TimeoutSeconds = 300,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipDatabaseRollback = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$ForceRollback = $false
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Configuration
$RollbackConfig = @{
    ApplicationName = "ACS"
    KubernetesNamespace = "acs-production"
    HealthCheckEndpoint = "/health"
    MaxRollbackWaitTime = $TimeoutSeconds
}

# Logging function
function Write-RollbackLog {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] [ROLLBACK] $Message"
    Write-Host $logMessage -ForegroundColor $(if($Level -eq "ERROR") {"Red"} elseif($Level -eq "WARN") {"Yellow"} else {"Green"})
    Add-Content -Path "./logs/rollback-$(Get-Date -Format 'yyyyMMdd').log" -Value $logMessage
}

# Create logs directory if it doesn't exist
if (!(Test-Path "./logs")) {
    New-Item -ItemType Directory -Path "./logs" -Force
}

Write-RollbackLog "Starting ACS rollback for $Environment environment" "INFO"

try {
    # Step 1: Pre-rollback validation
    Write-RollbackLog "Step 1: Pre-rollback validation" "INFO"
    
    # Check prerequisites
    if (!(Get-Command "kubectl" -ErrorAction SilentlyContinue)) {
        throw "kubectl is not installed or not in PATH"
    }
    
    # Check cluster connectivity
    $clusterInfo = kubectl cluster-info 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to connect to Kubernetes cluster: $clusterInfo"
    }
    Write-RollbackLog "Kubernetes cluster connectivity verified" "INFO"
    
    # Step 2: Determine rollback target
    Write-RollbackLog "Step 2: Determining rollback target" "INFO"
    
    if ([string]::IsNullOrEmpty($TargetVersion)) {
        # Get the previous deployment from rollout history
        $webApiHistory = kubectl rollout history deployment/acs-webapi -n $($RollbackConfig.KubernetesNamespace) 2>&1
        if ($LASTEXITCODE -eq 0) {
            # Parse rollout history to find previous version
            $revisions = $webApiHistory -split "`n" | Where-Object { $_ -match "^\d+\s+" } | ForEach-Object { ($_ -split "\s+")[0] }
            if ($revisions.Count -ge 2) {
                $previousRevision = $revisions[-2]  # Second to last revision
                Write-RollbackLog "Target rollback revision: $previousRevision" "INFO"
            } else {
                throw "No previous deployment found to rollback to"
            }
        } else {
            throw "Failed to get rollout history: $webApiHistory"
        }
    } else {
        Write-RollbackLog "Using specified target version: $TargetVersion" "INFO"
    }
    
    # Step 3: Check current deployment status
    Write-RollbackLog "Step 3: Checking current deployment status" "INFO"
    
    $deployments = @("acs-webapi", "acs-verticalhost", "acs-dashboard")
    $currentStatuses = @{}
    
    foreach ($deployment in $deployments) {
        $status = kubectl get deployment $deployment -n $($RollbackConfig.KubernetesNamespace) -o jsonpath='{.status.readyReplicas}/{.status.replicas}' 2>&1
        if ($LASTEXITCODE -eq 0) {
            $currentStatuses[$deployment] = $status
            Write-RollbackLog "$deployment current status: $status" "INFO"
        } else {
            Write-RollbackLog "Failed to get status for $deployment" "WARN"
        }
    }
    
    # Step 4: Perform rollback confirmation
    if (!$ForceRollback) {
        Write-RollbackLog "Step 4: Rollback confirmation required" "WARN"
        $confirmation = Read-Host "Are you sure you want to rollback the $Environment environment? (yes/no)"
        if ($confirmation -ne "yes") {
            Write-RollbackLog "Rollback cancelled by user" "INFO"
            exit 0
        }
    } else {
        Write-RollbackLog "Step 4: Forced rollback - skipping confirmation" "INFO"
    }
    
    # Step 5: Create rollback backup
    Write-RollbackLog "Step 5: Creating rollback backup" "INFO"
    
    $rollbackTimestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $backupScript = "./scripts/backup-production.ps1"
    
    if (Test-Path $backupScript) {
        try {
            & $backupScript -BackupName "pre-rollback-$rollbackTimestamp"
            if ($LASTEXITCODE -eq 0) {
                Write-RollbackLog "Rollback backup created: pre-rollback-$rollbackTimestamp" "INFO"
            } else {
                Write-RollbackLog "Backup creation failed, continuing with rollback" "WARN"
            }
        } catch {
            Write-RollbackLog "Backup creation error: $($_.Exception.Message)" "WARN"
        }
    } else {
        Write-RollbackLog "Backup script not found, skipping backup" "WARN"
    }
    
    # Step 6: Stop current traffic
    Write-RollbackLog "Step 6: Stopping incoming traffic" "INFO"
    
    # Scale down load balancer or implement traffic stop
    try {
        # Update service to block traffic (method depends on your load balancer)
        kubectl annotate service acs-webapi-service -n $($RollbackConfig.KubernetesNamespace) maintenance.mode="true" --overwrite
        Write-RollbackLog "Traffic blocking annotation added" "INFO"
        
        # Wait a moment for connections to drain
        Start-Sleep 10
    } catch {
        Write-RollbackLog "Failed to stop traffic: $($_.Exception.Message)" "WARN"
    }
    
    # Step 7: Rollback database (if not skipped)
    if (!$SkipDatabaseRollback) {
        Write-RollbackLog "Step 7: Rolling back database" "INFO"
        
        # Database rollback logic would go here
        # This is highly dependent on your database backup/restore strategy
        Write-RollbackLog "Database rollback not implemented - manual intervention required" "WARN"
    } else {
        Write-RollbackLog "Step 7: Skipping database rollback as requested" "INFO"
    }
    
    # Step 8: Rollback application deployments
    Write-RollbackLog "Step 8: Rolling back application deployments" "INFO"
    
    foreach ($deployment in $deployments) {
        Write-RollbackLog "Rolling back $deployment" "INFO"
        
        if ([string]::IsNullOrEmpty($TargetVersion)) {
            # Use kubectl rollout undo
            kubectl rollout undo deployment/$deployment -n $($RollbackConfig.KubernetesNamespace)
        } else {
            # Rollback to specific revision/version
            kubectl rollout undo deployment/$deployment --to-revision=$TargetVersion -n $($RollbackConfig.KubernetesNamespace)
        }
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to rollback $deployment"
        }
        
        Write-RollbackLog "$deployment rollback initiated" "INFO"
    }
    
    # Step 9: Wait for rollback to complete
    Write-RollbackLog "Step 9: Waiting for rollback to complete" "INFO"
    
    foreach ($deployment in $deployments) {
        Write-RollbackLog "Waiting for $deployment rollback to complete..." "INFO"
        kubectl rollout status deployment/$deployment --timeout=$($RollbackConfig.MaxRollbackWaitTime)s -n $($RollbackConfig.KubernetesNamespace)
        if ($LASTEXITCODE -ne 0) {
            throw "Rollback for $deployment failed or timed out"
        }
    }
    
    Write-RollbackLog "All deployments rolled back successfully" "INFO"
    
    # Step 10: Restore traffic
    Write-RollbackLog "Step 10: Restoring traffic" "INFO"
    
    try {
        # Remove traffic blocking annotation
        kubectl annotate service acs-webapi-service -n $($RollbackConfig.KubernetesNamespace) maintenance.mode-
        Write-RollbackLog "Traffic blocking annotation removed" "INFO"
    } catch {
        Write-RollbackLog "Failed to restore traffic: $($_.Exception.Message)" "WARN"
    }
    
    # Step 11: Verify rollback
    Write-RollbackLog "Step 11: Verifying rollback" "INFO"
    
    # Wait a moment for traffic to resume
    Start-Sleep 30
    
    # Check pod status
    $pods = kubectl get pods -n $($RollbackConfig.KubernetesNamespace) -o json | ConvertFrom-Json
    $unhealthyPods = $pods.items | Where-Object { $_.status.phase -ne "Running" }
    
    if ($unhealthyPods.Count -eq 0) {
        Write-RollbackLog "All pods are running successfully after rollback" "INFO"
    } else {
        Write-RollbackLog "Some pods are not healthy after rollback:" "WARN"
        foreach ($pod in $unhealthyPods) {
            Write-RollbackLog "  $($pod.metadata.name): $($pod.status.phase)" "WARN"
        }
    }
    
    # Basic health check
    try {
        $webApiService = kubectl get service acs-webapi-service -n $($RollbackConfig.KubernetesNamespace) -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
        if ($webApiService) {
            $healthUrl = "http://$webApiService$($RollbackConfig.HealthCheckEndpoint)"
            $response = Invoke-RestMethod -Uri $healthUrl -Method GET -TimeoutSec 30
            
            if ($response.Status -eq "Healthy") {
                Write-RollbackLog "Health check passed after rollback" "INFO"
            } else {
                Write-RollbackLog "Health check failed: $($response.Status)" "WARN"
            }
        }
    } catch {
        Write-RollbackLog "Health check error: $($_.Exception.Message)" "WARN"
    }
    
    # Step 12: Log rollback completion
    Write-RollbackLog "Step 12: Rollback completion" "INFO"
    
    Write-RollbackLog "Rollback completed successfully!" "INFO"
    Write-RollbackLog "Application has been rolled back in $Environment environment" "INFO"
    
    # Display final status
    foreach ($deployment in $deployments) {
        $finalStatus = kubectl get deployment $deployment -n $($RollbackConfig.KubernetesNamespace) -o jsonpath='{.status.readyReplicas}/{.status.replicas}' 2>&1
        Write-RollbackLog "$deployment final status: $finalStatus" "INFO"
    }
    
} catch {
    Write-RollbackLog "Rollback failed: $($_.Exception.Message)" "ERROR"
    Write-RollbackLog "Stack trace: $($_.ScriptStackTrace)" "ERROR"
    
    # Try to restore traffic if it was stopped
    try {
        kubectl annotate service acs-webapi-service -n $($RollbackConfig.KubernetesNamespace) maintenance.mode-
    } catch {
        Write-RollbackLog "Failed to restore traffic after rollback failure" "ERROR"
    }
    
    Write-RollbackLog "Manual intervention required" "ERROR"
    exit 1
}

Write-RollbackLog "Rollback script completed at $(Get-Date)" "INFO"