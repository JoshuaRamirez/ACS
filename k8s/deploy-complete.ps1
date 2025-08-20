# ACS Kubernetes Complete Deployment Script (PowerShell)
# This script deploys the entire ACS system to Kubernetes with proper orchestration

[CmdletBinding()]
param(
    [string]$Namespace = "acs-system",
    [string]$Version = "1.0.0",
    [string]$Registry = "localhost:5000",
    [switch]$SkipBuild,
    [switch]$VerifyOnly,
    [switch]$Help
)

# Colors for output
$Colors = @{
    Red     = "Red"
    Green   = "Green" 
    Yellow  = "Yellow"
    Blue    = "Blue"
    Cyan    = "Cyan"
}

# Logging functions
function Write-Log {
    param([string]$Message, [string]$Color = "Blue")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] $Message" -ForegroundColor $Color
}

function Write-Success {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] ✓ $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] ⚠ $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] ✗ $Message" -ForegroundColor Red
}

function Show-Help {
    Write-Host @"
ACS Kubernetes Deployment Script (PowerShell)

Usage: .\deploy-complete.ps1 [OPTIONS]

Options:
  -Namespace      Kubernetes namespace (default: acs-system)
  -Version        Deployment version (default: 1.0.0)
  -Registry       Docker registry (default: localhost:5000)
  -SkipBuild      Skip Docker image building
  -VerifyOnly     Only run verification checks
  -Help           Show this help message

Environment Variables:
  ACS_VERSION     Deployment version
  ACS_REGISTRY    Docker registry URL
  SKIP_BUILD      Skip building images (true/false)

Examples:
  .\deploy-complete.ps1                                    # Deploy with defaults
  .\deploy-complete.ps1 -Version 1.1.0 -Registry my-registry.com    # Deploy specific version
  `$env:SKIP_BUILD="true"; .\deploy-complete.ps1          # Deploy without building
"@
}

function Test-Prerequisites {
    Write-Log "Checking prerequisites..."
    
    # Check kubectl
    if (-not (Get-Command kubectl -ErrorAction SilentlyContinue)) {
        Write-Error "kubectl is not installed or not in PATH"
        exit 1
    }
    
    # Check kustomize
    if (-not (Get-Command kustomize -ErrorAction SilentlyContinue)) {
        Write-Error "kustomize is not installed or not in PATH"
        exit 1
    }
    
    # Check Docker
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        Write-Error "Docker is not installed or not in PATH"
        exit 1
    }
    
    # Check cluster connectivity
    try {
        kubectl cluster-info | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "kubectl cluster-info failed"
        }
    }
    catch {
        Write-Error "Cannot connect to Kubernetes cluster"
        exit 1
    }
    
    Write-Success "Prerequisites check passed"
}

function Build-And-Push-Images {
    Write-Log "Building and pushing Docker images..."
    
    $parentDir = Split-Path -Parent $PSScriptRoot
    
    # Build WebAPI image
    Write-Log "Building ACS WebAPI image..."
    Set-Location $parentDir
    docker build -t "$Registry/acs/webapi:$Version" -f ACS.WebApi/Dockerfile .
    if ($LASTEXITCODE -ne 0) { throw "WebAPI image build failed" }
    
    docker push "$Registry/acs/webapi:$Version"
    if ($LASTEXITCODE -ne 0) { throw "WebAPI image push failed" }
    Write-Success "WebAPI image built and pushed"
    
    # Build Dashboard image
    Write-Log "Building ACS Dashboard image..."
    docker build -t "$Registry/acs/dashboard:$Version" -f ACS.Dashboard/Dockerfile .
    if ($LASTEXITCODE -ne 0) { throw "Dashboard image build failed" }
    
    docker push "$Registry/acs/dashboard:$Version"
    if ($LASTEXITCODE -ne 0) { throw "Dashboard image push failed" }
    Write-Success "Dashboard image built and pushed"
    
    # Build VerticalHost image  
    Write-Log "Building ACS VerticalHost image..."
    docker build -t "$Registry/acs/verticalhost:$Version" -f ACS.VerticalHost/Dockerfile .
    if ($LASTEXITCODE -ne 0) { throw "VerticalHost image build failed" }
    
    docker push "$Registry/acs/verticalhost:$Version"
    if ($LASTEXITCODE -ne 0) { throw "VerticalHost image push failed" }
    Write-Success "VerticalHost image built and pushed"
    
    Set-Location $PSScriptRoot
}

function Wait-ForDeployment {
    param(
        [string]$DeploymentName,
        [string]$Namespace,
        [int]$TimeoutSeconds = 300
    )
    
    Write-Log "Waiting for deployment $DeploymentName to be ready..."
    
    $result = kubectl wait --for=condition=available --timeout="${TimeoutSeconds}s" deployment/$DeploymentName -n $Namespace 2>$null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Deployment $DeploymentName is ready"
        return $true
    }
    else {
        Write-Error "Deployment $DeploymentName failed to become ready within $TimeoutSeconds seconds"
        return $false
    }
}

function Deploy-Infrastructure {
    Write-Log "Deploying infrastructure components..."
    
    # Apply namespace and persistent volumes first
    kubectl apply -f namespace.yaml
    kubectl apply -f persistent-volumes.yaml
    Write-Success "Namespace and persistent volumes deployed"
    
    # Apply ConfigMaps and Secrets
    kubectl apply -f configmaps.yaml
    kubectl apply -f secrets.yaml
    Write-Success "ConfigMaps and Secrets deployed"
    
    # Deploy database
    Write-Log "Deploying SQL Server database..."
    kubectl apply -f database.yaml
    Wait-ForDeployment -DeploymentName "sqlserver" -Namespace $Namespace -TimeoutSeconds 300
    
    # Deploy Redis cache
    Write-Log "Deploying Redis cache..."
    kubectl apply -f redis.yaml
    Wait-ForDeployment -DeploymentName "redis" -Namespace $Namespace -TimeoutSeconds 180
    
    Write-Success "Infrastructure components deployed successfully"
}

function Deploy-Monitoring {
    Write-Log "Deploying monitoring stack..."
    
    kubectl apply -f monitoring.yaml
    
    # Wait for monitoring components
    Wait-ForDeployment -DeploymentName "otel-collector" -Namespace $Namespace -TimeoutSeconds 180
    Wait-ForDeployment -DeploymentName "prometheus" -Namespace $Namespace -TimeoutSeconds 180
    Wait-ForDeployment -DeploymentName "grafana" -Namespace $Namespace -TimeoutSeconds 180
    
    Write-Success "Monitoring stack deployed successfully"
}

function Deploy-Applications {
    Write-Log "Deploying application services..."
    
    # Deploy WebAPI
    Write-Log "Deploying ACS WebAPI..."
    kubectl apply -f webapi.yaml
    Wait-ForDeployment -DeploymentName "acs-webapi" -Namespace $Namespace -TimeoutSeconds 300
    
    # Deploy Dashboard
    Write-Log "Deploying ACS Dashboard..."
    kubectl apply -f dashboard.yaml
    Wait-ForDeployment -DeploymentName "acs-dashboard" -Namespace $Namespace -TimeoutSeconds 300
    
    # Deploy tenant services
    Write-Log "Deploying tenant services..."
    kubectl apply -f tenants.yaml
    Wait-ForDeployment -DeploymentName "acs-tenant1" -Namespace $Namespace -TimeoutSeconds 300
    Wait-ForDeployment -DeploymentName "acs-tenant2" -Namespace $Namespace -TimeoutSeconds 300
    Wait-ForDeployment -DeploymentName "acs-tenant3" -Namespace $Namespace -TimeoutSeconds 300
    
    Write-Success "Application services deployed successfully"
}

function Deploy-Security {
    Write-Log "Applying security policies..."
    
    kubectl apply -f network-policies.yaml
    
    Write-Success "Network policies applied successfully"
}

function Test-Deployment {
    Write-Log "Verifying deployment health..."
    
    # Check all pods are running
    Write-Log "Checking pod status..."
    kubectl get pods -n $Namespace -o wide
    
    # Check services
    Write-Log "Checking service status..."
    kubectl get services -n $Namespace
    
    # Check ingresses
    Write-Log "Checking ingress status..."
    kubectl get ingress -n $Namespace
    
    # Run health checks
    Write-Log "Running health checks..."
    
    # WebAPI health check
    try {
        $null = kubectl exec -n $Namespace deployment/acs-webapi -- curl -f http://localhost:5000/health 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Success "WebAPI health check passed"
        } else {
            Write-Warning "WebAPI health check failed"
        }
    }
    catch {
        Write-Warning "WebAPI health check failed: $_"
    }
    
    # Dashboard health check  
    try {
        $null = kubectl exec -n $Namespace deployment/acs-dashboard -- curl -f http://localhost:8080/health 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Dashboard health check passed"
        } else {
            Write-Warning "Dashboard health check failed"
        }
    }
    catch {
        Write-Warning "Dashboard health check failed: $_"
    }
    
    # Redis connectivity check
    try {
        $result = kubectl exec -n $Namespace deployment/redis -- redis-cli ping 2>$null
        if ($result -match "PONG") {
            Write-Success "Redis connectivity check passed"
        } else {
            Write-Warning "Redis connectivity check failed"
        }
    }
    catch {
        Write-Warning "Redis connectivity check failed: $_"
    }
}

function Show-AccessInfo {
    Write-Log "Deployment completed! Access information:"
    Write-Host ""
    Write-Host "🌐 Web Services:" -ForegroundColor Cyan
    Write-Host "  • API Documentation: https://api.acs.example.com/api-docs" -ForegroundColor White
    Write-Host "  • Dashboard:         https://dashboard.acs.example.com" -ForegroundColor White
    Write-Host "  • Grafana:          https://grafana.acs.example.com (admin/admin123)" -ForegroundColor White
    Write-Host "  • Prometheus:       https://prometheus.acs.example.com" -ForegroundColor White
    Write-Host ""
    Write-Host "🔧 Management Commands:" -ForegroundColor Cyan
    Write-Host "  • View pods:        kubectl get pods -n $Namespace" -ForegroundColor White
    Write-Host "  • View logs:        kubectl logs -f deployment/acs-webapi -n $Namespace" -ForegroundColor White
    Write-Host "  • Port forward API: kubectl port-forward svc/acs-webapi 5000:5000 -n $Namespace" -ForegroundColor White
    Write-Host "  • Scale WebAPI:     kubectl scale deployment acs-webapi --replicas=3 -n $Namespace" -ForegroundColor White
    Write-Host ""
    Write-Host "📊 Monitoring:" -ForegroundColor Cyan
    Write-Host "  • Metrics endpoint: http://localhost:5000/metrics (after port-forward)" -ForegroundColor White
    Write-Host "  • Health endpoint:  http://localhost:5000/health (after port-forward)" -ForegroundColor White
    Write-Host ""
}

function Invoke-Cleanup {
    Write-Error "Deployment failed. Cleaning up..."
    kubectl delete namespace $Namespace --ignore-not-found=true | Out-Null
}

# Main execution
try {
    if ($Help) {
        Show-Help
        exit 0
    }
    
    if ($VerifyOnly) {
        Test-Deployment
        exit 0
    }
    
    # Override with environment variables if present
    if ($env:ACS_VERSION) { $Version = $env:ACS_VERSION }
    if ($env:ACS_REGISTRY) { $Registry = $env:ACS_REGISTRY }
    if ($env:SKIP_BUILD -eq "true") { $SkipBuild = $true }
    
    Write-Log "Starting ACS Kubernetes deployment (version: $Version)"
    
    # Set location to k8s directory
    Set-Location $PSScriptRoot
    
    # Run deployment steps
    Test-Prerequisites
    
    if (-not $SkipBuild) {
        Build-And-Push-Images
    } else {
        Write-Warning "Skipping image build (SkipBuild specified)"
    }
    
    Deploy-Infrastructure
    Deploy-Monitoring  
    Deploy-Applications
    Deploy-Security
    Test-Deployment
    Show-AccessInfo
    
    Write-Success "ACS deployment completed successfully! 🎉"
}
catch {
    Write-Error "Deployment failed: $_"
    Invoke-Cleanup
    exit 1
}