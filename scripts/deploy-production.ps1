# ACS Production Deployment Script
# This script deploys the ACS application to production environment
# Requires PowerShell 7+ and appropriate Azure/production access

param(
    [Parameter(Mandatory=$false)]
    [string]$Environment = "Production",
    
    [Parameter(Mandatory=$false)]
    [string]$Version = "latest",
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipHealthCheck = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipBackup = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$RollbackOnFailure = $true,
    
    [Parameter(Mandatory=$false)]
    [string]$ConfigFile = "./deployment/production.json"
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Configuration
$DeploymentConfig = @{
    ApplicationName = "ACS"
    DockerRegistry = "acsregistry.azurecr.io"
    KubernetesNamespace = "acs-production"
    HealthCheckEndpoint = "/health"
    MaxDeploymentWaitTime = 600  # 10 minutes
    RollbackTimeoutSeconds = 300 # 5 minutes
}

# Logging function
function Write-DeploymentLog {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Write-Host $logMessage
    Add-Content -Path "./logs/deployment-$(Get-Date -Format 'yyyyMMdd').log" -Value $logMessage
}

# Create logs directory if it doesn't exist
if (!(Test-Path "./logs")) {
    New-Item -ItemType Directory -Path "./logs" -Force
}

Write-DeploymentLog "Starting ACS deployment to $Environment environment" "INFO"
Write-DeploymentLog "Version: $Version, Skip Health Check: $SkipHealthCheck, Skip Backup: $SkipBackup" "INFO"

try {
    # Step 1: Pre-deployment validation
    Write-DeploymentLog "Step 1: Pre-deployment validation" "INFO"
    
    # Check prerequisites
    if (!(Get-Command "kubectl" -ErrorAction SilentlyContinue)) {
        throw "kubectl is not installed or not in PATH"
    }
    
    if (!(Get-Command "docker" -ErrorAction SilentlyContinue)) {
        throw "Docker is not installed or not in PATH"
    }
    
    # Validate configuration file
    if (Test-Path $ConfigFile) {
        $config = Get-Content $ConfigFile | ConvertFrom-Json
        Write-DeploymentLog "Configuration loaded from $ConfigFile" "INFO"
    } else {
        Write-DeploymentLog "Configuration file not found: $ConfigFile, using defaults" "WARN"
    }
    
    # Check cluster connectivity
    $clusterInfo = kubectl cluster-info 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to connect to Kubernetes cluster: $clusterInfo"
    }
    Write-DeploymentLog "Kubernetes cluster connectivity verified" "INFO"
    
    # Step 2: Create backup (if not skipped)
    if (!$SkipBackup) {
        Write-DeploymentLog "Step 2: Creating pre-deployment backup" "INFO"
        
        $backupTimestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $backupScript = "./scripts/backup-production.ps1"
        
        if (Test-Path $backupScript) {
            & $backupScript -BackupName "pre-deployment-$backupTimestamp"
            if ($LASTEXITCODE -ne 0) {
                throw "Backup creation failed"
            }
            Write-DeploymentLog "Backup created successfully: pre-deployment-$backupTimestamp" "INFO"
        } else {
            Write-DeploymentLog "Backup script not found, skipping backup" "WARN"
        }
    } else {
        Write-DeploymentLog "Step 2: Skipping backup as requested" "INFO"
    }
    
    # Step 3: Build and push container images
    Write-DeploymentLog "Step 3: Building and pushing container images" "INFO"
    
    $services = @("webapi", "verticalhost", "dashboard")
    foreach ($service in $services) {
        Write-DeploymentLog "Building $service image" "INFO"
        
        $imageName = "$($DeploymentConfig.DockerRegistry)/acs-$service`:$Version"
        
        # Build image
        docker build -t $imageName -f "./ACS.$($service.Substring(0,1).ToUpper() + $service.Substring(1))/Dockerfile" .
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to build $service image"
        }
        
        # Push image
        docker push $imageName
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to push $service image"
        }
        
        Write-DeploymentLog "$service image built and pushed successfully" "INFO"
    }
    
    # Step 4: Update Kubernetes manifests
    Write-DeploymentLog "Step 4: Updating Kubernetes manifests" "INFO"
    
    # Update image tags in deployment manifests
    $k8sManifests = Get-ChildItem "./k8s/*.yaml"
    foreach ($manifest in $k8sManifests) {
        $content = Get-Content $manifest.FullName -Raw
        $updatedContent = $content -replace "image: $($DeploymentConfig.DockerRegistry)/acs-.*:.*", "image: $($DeploymentConfig.DockerRegistry)/acs-`$1:$Version"
        Set-Content $manifest.FullName -Value $updatedContent
    }
    
    Write-DeploymentLog "Kubernetes manifests updated with version $Version" "INFO"
    
    # Step 5: Deploy to Kubernetes
    Write-DeploymentLog "Step 5: Deploying to Kubernetes cluster" "INFO"
    
    # Apply namespace
    kubectl apply -f "./k8s/namespace.yaml"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create/update namespace"
    }
    
    # Apply secrets and configmaps first
    kubectl apply -f "./k8s/secrets.yaml"
    kubectl apply -f "./k8s/configmaps.yaml"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to apply secrets or configmaps"
    }
    
    # Apply persistent volumes
    kubectl apply -f "./k8s/persistent-volumes.yaml"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to apply persistent volumes"
    }
    
    # Apply database components
    kubectl apply -f "./k8s/database.yaml"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to deploy database components"
    }
    
    # Apply Redis
    kubectl apply -f "./k8s/redis.yaml"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to deploy Redis"
    }
    
    # Wait for database to be ready
    Write-DeploymentLog "Waiting for database to be ready..." "INFO"
    kubectl wait --for=condition=ready pod -l app=acs-database --timeout=300s -n $($DeploymentConfig.KubernetesNamespace)
    
    # Apply application components
    kubectl apply -f "./k8s/webapi.yaml"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to deploy WebAPI"
    }
    
    kubectl apply -f "./k8s/tenants.yaml"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to deploy tenant services"
    }
    
    # Apply monitoring
    kubectl apply -f "./k8s/monitoring.yaml"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to deploy monitoring components"
    }
    
    Write-DeploymentLog "All components deployed successfully" "INFO"
    
    # Step 6: Wait for deployment to complete
    Write-DeploymentLog "Step 6: Waiting for deployment to complete" "INFO"
    
    $deployments = @("acs-webapi", "acs-verticalhost", "acs-dashboard")
    foreach ($deployment in $deployments) {
        Write-DeploymentLog "Waiting for $deployment to be ready..." "INFO"
        kubectl wait --for=condition=available deployment/$deployment --timeout=$($DeploymentConfig.MaxDeploymentWaitTime)s -n $($DeploymentConfig.KubernetesNamespace)
        if ($LASTEXITCODE -ne 0) {
            throw "Deployment $deployment failed to become available within timeout"
        }
    }
    
    Write-DeploymentLog "All deployments are ready" "INFO"
    
    # Step 7: Run health checks
    if (!$SkipHealthCheck) {
        Write-DeploymentLog "Step 7: Running health checks" "INFO"
        
        # Get service endpoints
        $webApiService = kubectl get service acs-webapi-service -n $($DeploymentConfig.KubernetesNamespace) -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
        
        if ($webApiService) {
            $healthUrl = "http://$webApiService$($DeploymentConfig.HealthCheckEndpoint)"
            
            # Wait for health endpoint to respond
            $healthCheckAttempts = 0
            $maxHealthCheckAttempts = 30
            $healthCheckPassed = $false
            
            while ($healthCheckAttempts -lt $maxHealthCheckAttempts -and !$healthCheckPassed) {
                try {
                    $response = Invoke-RestMethod -Uri $healthUrl -Method GET -TimeoutSec 10
                    if ($response.Status -eq "Healthy") {
                        $healthCheckPassed = $true
                        Write-DeploymentLog "Health check passed" "INFO"
                    }
                } catch {
                    Write-DeploymentLog "Health check attempt $($healthCheckAttempts + 1) failed: $($_.Exception.Message)" "WARN"
                }
                
                $healthCheckAttempts++
                if (!$healthCheckPassed) {
                    Start-Sleep 10
                }
            }
            
            if (!$healthCheckPassed) {
                throw "Health checks failed after $maxHealthCheckAttempts attempts"
            }
        } else {
            Write-DeploymentLog "Unable to determine WebAPI service endpoint, skipping health check" "WARN"
        }
    } else {
        Write-DeploymentLog "Step 7: Skipping health checks as requested" "INFO"
    }
    
    # Step 8: Run database migrations
    Write-DeploymentLog "Step 8: Running database migrations" "INFO"
    
    $migrationJob = @"
apiVersion: batch/v1
kind: Job
metadata:
  name: acs-migration-$Version
  namespace: $($DeploymentConfig.KubernetesNamespace)
spec:
  template:
    spec:
      containers:
      - name: migration
        image: $($DeploymentConfig.DockerRegistry)/acs-webapi:$Version
        command: ["dotnet", "ef", "database", "update"]
        env:
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: acs-secrets
              key: database-connection-string
      restartPolicy: Never
  backoffLimit: 3
"@
    
    $migrationJob | kubectl apply -f -
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create migration job"
    }
    
    # Wait for migration to complete
    kubectl wait --for=condition=complete job/acs-migration-$Version --timeout=300s -n $($DeploymentConfig.KubernetesNamespace)
    if ($LASTEXITCODE -ne 0) {
        Write-DeploymentLog "Database migration job failed or timed out" "ERROR"
        # Don't fail the deployment for migration issues, but log them
    } else {
        Write-DeploymentLog "Database migrations completed successfully" "INFO"
    }
    
    # Step 9: Post-deployment validation
    Write-DeploymentLog "Step 9: Post-deployment validation" "INFO"
    
    # Validate all pods are running
    $pods = kubectl get pods -n $($DeploymentConfig.KubernetesNamespace) -o json | ConvertFrom-Json
    $failedPods = $pods.items | Where-Object { $_.status.phase -ne "Running" }
    
    if ($failedPods.Count -gt 0) {
        Write-DeploymentLog "Some pods are not in Running state:" "WARN"
        foreach ($pod in $failedPods) {
            Write-DeploymentLog "  $($pod.metadata.name): $($pod.status.phase)" "WARN"
        }
    } else {
        Write-DeploymentLog "All pods are running successfully" "INFO"
    }
    
    # Clean up old migration jobs
    kubectl delete job -l app=acs-migration --field-selector status.successful=1 -n $($DeploymentConfig.KubernetesNamespace)
    
    Write-DeploymentLog "Deployment completed successfully!" "INFO"
    Write-DeploymentLog "Application version $Version is now running in $Environment environment" "INFO"
    
} catch {
    Write-DeploymentLog "Deployment failed: $($_.Exception.Message)" "ERROR"
    Write-DeploymentLog "Stack trace: $($_.ScriptStackTrace)" "ERROR"
    
    if ($RollbackOnFailure) {
        Write-DeploymentLog "Initiating rollback procedure..." "INFO"
        
        try {
            # Run rollback script
            $rollbackScript = "./scripts/rollback-production.ps1"
            if (Test-Path $rollbackScript) {
                & $rollbackScript -TimeoutSeconds $($DeploymentConfig.RollbackTimeoutSeconds)
                Write-DeploymentLog "Rollback completed" "INFO"
            } else {
                Write-DeploymentLog "Rollback script not found, manual rollback required" "ERROR"
            }
        } catch {
            Write-DeploymentLog "Rollback failed: $($_.Exception.Message)" "ERROR"
        }
    }
    
    exit 1
}

Write-DeploymentLog "Deployment script completed at $(Get-Date)" "INFO"