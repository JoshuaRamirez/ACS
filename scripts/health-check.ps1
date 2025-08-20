# ACS Health Check Script
# This script performs comprehensive health checks on the ACS application

param(
    [Parameter(Mandatory=$false)]
    [string]$Environment = "Production",
    
    [Parameter(Mandatory=$false)]
    [string]$Namespace = "acs-production",
    
    [Parameter(Mandatory=$false)]
    [switch]$DetailedOutput = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$JsonOutput = $false,
    
    [Parameter(Mandatory=$false)]
    [int]$TimeoutSeconds = 30
)

# Configuration
$HealthCheckConfig = @{
    WebApiEndpoints = @("/health", "/health/ready", "/health/live")
    DatabaseEndpoints = @("/health")
    MetricsEndpoints = @("/metrics/snapshot")
    ExpectedServices = @("acs-webapi", "acs-verticalhost", "acs-database", "acs-redis")
    MaxRetries = 3
    RetryDelaySeconds = 5
}

# Logging function
function Write-HealthLog {
    param([string]$Message, [string]$Level = "INFO")
    if (!$JsonOutput) {
        $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        $color = switch($Level) {
            "ERROR" { "Red" }
            "WARN" { "Yellow" }
            "SUCCESS" { "Green" }
            default { "White" }
        }
        Write-Host "[$timestamp] [$Level] $Message" -ForegroundColor $color
    }
}

# Health check result object
class HealthCheckResult {
    [string]$Component
    [string]$Status
    [string]$Message
    [hashtable]$Details
    [datetime]$Timestamp
    [double]$ResponseTimeMs
    
    HealthCheckResult([string]$component, [string]$status, [string]$message) {
        $this.Component = $component
        $this.Status = $status
        $this.Message = $message
        $this.Details = @{}
        $this.Timestamp = Get-Date
        $this.ResponseTimeMs = 0
    }
}

# Test HTTP endpoint
function Test-HttpEndpoint {
    param(
        [string]$Url,
        [int]$TimeoutSeconds = 30,
        [int]$MaxRetries = 3
    )
    
    $attempts = 0
    while ($attempts -lt $MaxRetries) {
        try {
            $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
            $response = Invoke-RestMethod -Uri $Url -Method GET -TimeoutSec $TimeoutSeconds
            $stopwatch.Stop()
            
            return @{
                Success = $true
                Response = $response
                ResponseTime = $stopwatch.ElapsedMilliseconds
                Error = $null
            }
        }
        catch {
            $attempts++
            if ($attempts -ge $MaxRetries) {
                return @{
                    Success = $false
                    Response = $null
                    ResponseTime = 0
                    Error = $_.Exception.Message
                }
            }
            Start-Sleep $HealthCheckConfig.RetryDelaySeconds
        }
    }
}

# Check Kubernetes cluster connectivity
function Test-KubernetesConnectivity {
    Write-HealthLog "Checking Kubernetes cluster connectivity..." "INFO"
    
    $result = [HealthCheckResult]::new("Kubernetes", "Unknown", "")
    
    try {
        $clusterInfo = kubectl cluster-info 2>&1
        if ($LASTEXITCODE -eq 0) {
            $result.Status = "Healthy"
            $result.Message = "Cluster connectivity successful"
            $result.Details["ClusterInfo"] = $clusterInfo -join "`n"
        }
        else {
            $result.Status = "Unhealthy"
            $result.Message = "Unable to connect to cluster: $clusterInfo"
        }
    }
    catch {
        $result.Status = "Unhealthy"
        $result.Message = "Kubernetes connectivity error: $($_.Exception.Message)"
    }
    
    return $result
}

# Check namespace existence
function Test-Namespace {
    Write-HealthLog "Checking namespace: $Namespace" "INFO"
    
    $result = [HealthCheckResult]::new("Namespace", "Unknown", "")
    
    try {
        $namespaceInfo = kubectl get namespace $Namespace -o json 2>&1
        if ($LASTEXITCODE -eq 0) {
            $nsData = $namespaceInfo | ConvertFrom-Json
            $result.Status = "Healthy"
            $result.Message = "Namespace exists and is active"
            $result.Details["Phase"] = $nsData.status.phase
            $result.Details["CreationTimestamp"] = $nsData.metadata.creationTimestamp
        }
        else {
            $result.Status = "Unhealthy"
            $result.Message = "Namespace not found or inaccessible"
        }
    }
    catch {
        $result.Status = "Unhealthy"
        $result.Message = "Namespace check error: $($_.Exception.Message)"
    }
    
    return $result
}

# Check pod health
function Test-Pods {
    Write-HealthLog "Checking pod health..." "INFO"
    
    $result = [HealthCheckResult]::new("Pods", "Unknown", "")
    
    try {
        $podsJson = kubectl get pods -n $Namespace -o json 2>&1
        if ($LASTEXITCODE -eq 0) {
            $pods = ($podsJson | ConvertFrom-Json).items
            
            $runningPods = $pods | Where-Object { $_.status.phase -eq "Running" }
            $totalPods = $pods.Count
            $runningCount = $runningPods.Count
            
            $result.Details["TotalPods"] = $totalPods
            $result.Details["RunningPods"] = $runningCount
            $result.Details["PodStatus"] = @{}
            
            foreach ($pod in $pods) {
                $podName = $pod.metadata.name
                $podStatus = $pod.status.phase
                $result.Details["PodStatus"][$podName] = $podStatus
                
                if ($DetailedOutput) {
                    Write-HealthLog "Pod $podName`: $podStatus" "INFO"
                }
            }
            
            if ($runningCount -eq $totalPods) {
                $result.Status = "Healthy"
                $result.Message = "All pods are running ($runningCount/$totalPods)"
            }
            else {
                $result.Status = "Degraded"
                $result.Message = "Some pods are not running ($runningCount/$totalPods)"
            }
        }
        else {
            $result.Status = "Unhealthy"
            $result.Message = "Unable to get pod information"
        }
    }
    catch {
        $result.Status = "Unhealthy"
        $result.Message = "Pod check error: $($_.Exception.Message)"
    }
    
    return $result
}

# Check service health
function Test-Services {
    Write-HealthLog "Checking service health..." "INFO"
    
    $result = [HealthCheckResult]::new("Services", "Unknown", "")
    
    try {
        $servicesJson = kubectl get services -n $Namespace -o json 2>&1
        if ($LASTEXITCODE -eq 0) {
            $services = ($servicesJson | ConvertFrom-Json).items
            
            $result.Details["Services"] = @{}
            $healthyServices = 0
            
            foreach ($service in $services) {
                $serviceName = $service.metadata.name
                $serviceType = $service.spec.type
                
                $serviceHealth = @{
                    Type = $serviceType
                    ClusterIP = $service.spec.clusterIP
                    Ports = $service.spec.ports
                }
                
                if ($serviceType -eq "LoadBalancer") {
                    $serviceHealth["ExternalIP"] = $service.status.loadBalancer.ingress[0].ip
                }
                
                $result.Details["Services"][$serviceName] = $serviceHealth
                $healthyServices++
                
                if ($DetailedOutput) {
                    Write-HealthLog "Service $serviceName`: $serviceType" "INFO"
                }
            }
            
            $result.Status = "Healthy"
            $result.Message = "Found $healthyServices services"
        }
        else {
            $result.Status = "Unhealthy"
            $result.Message = "Unable to get service information"
        }
    }
    catch {
        $result.Status = "Unhealthy"
        $result.Message = "Service check error: $($_.Exception.Message)"
    }
    
    return $result
}

# Check deployment status
function Test-Deployments {
    Write-HealthLog "Checking deployment status..." "INFO"
    
    $result = [HealthCheckResult]::new("Deployments", "Unknown", "")
    
    try {
        $deploymentsJson = kubectl get deployments -n $Namespace -o json 2>&1
        if ($LASTEXITCODE -eq 0) {
            $deployments = ($deploymentsJson | ConvertFrom-Json).items
            
            $result.Details["Deployments"] = @{}
            $healthyDeployments = 0
            $totalDeployments = $deployments.Count
            
            foreach ($deployment in $deployments) {
                $deploymentName = $deployment.metadata.name
                $replicas = $deployment.spec.replicas
                $readyReplicas = $deployment.status.readyReplicas ?? 0
                $availableReplicas = $deployment.status.availableReplicas ?? 0
                
                $deploymentHealth = @{
                    Replicas = $replicas
                    ReadyReplicas = $readyReplicas
                    AvailableReplicas = $availableReplicas
                    IsHealthy = ($readyReplicas -eq $replicas)
                }
                
                $result.Details["Deployments"][$deploymentName] = $deploymentHealth
                
                if ($deploymentHealth.IsHealthy) {
                    $healthyDeployments++
                }
                
                if ($DetailedOutput) {
                    Write-HealthLog "Deployment $deploymentName`: $readyReplicas/$replicas ready" "INFO"
                }
            }
            
            if ($healthyDeployments -eq $totalDeployments) {
                $result.Status = "Healthy"
                $result.Message = "All deployments are healthy ($healthyDeployments/$totalDeployments)"
            }
            else {
                $result.Status = "Degraded"
                $result.Message = "Some deployments are unhealthy ($healthyDeployments/$totalDeployments)"
            }
        }
        else {
            $result.Status = "Unhealthy"
            $result.Message = "Unable to get deployment information"
        }
    }
    catch {
        $result.Status = "Unhealthy"
        $result.Message = "Deployment check error: $($_.Exception.Message)"
    }
    
    return $result
}

# Check application endpoints
function Test-ApplicationEndpoints {
    Write-HealthLog "Checking application endpoints..." "INFO"
    
    $result = [HealthCheckResult]::new("ApplicationEndpoints", "Unknown", "")
    
    try {
        # Get WebAPI service endpoint
        $serviceIP = kubectl get service acs-webapi-service -n $Namespace -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null
        
        if ([string]::IsNullOrEmpty($serviceIP)) {
            # Try getting cluster IP instead
            $serviceIP = kubectl get service acs-webapi-service -n $Namespace -o jsonpath='{.spec.clusterIP}' 2>/dev/null
        }
        
        if (![string]::IsNullOrEmpty($serviceIP)) {
            $result.Details["ServiceIP"] = $serviceIP
            $result.Details["Endpoints"] = @{}
            $healthyEndpoints = 0
            $totalEndpoints = $HealthCheckConfig.WebApiEndpoints.Count
            
            foreach ($endpoint in $HealthCheckConfig.WebApiEndpoints) {
                $url = "http://$serviceIP$endpoint"
                
                $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
                $endpointTest = Test-HttpEndpoint -Url $url -TimeoutSeconds $TimeoutSeconds -MaxRetries $HealthCheckConfig.MaxRetries
                $stopwatch.Stop()
                
                $endpointResult = @{
                    Url = $url
                    Success = $endpointTest.Success
                    ResponseTime = $endpointTest.ResponseTime
                    Error = $endpointTest.Error
                    Response = $endpointTest.Response
                }
                
                $result.Details["Endpoints"][$endpoint] = $endpointResult
                
                if ($endpointTest.Success) {
                    $healthyEndpoints++
                    if ($DetailedOutput) {
                        Write-HealthLog "Endpoint $endpoint`: OK ($($endpointTest.ResponseTime)ms)" "SUCCESS"
                    }
                }
                else {
                    if ($DetailedOutput) {
                        Write-HealthLog "Endpoint $endpoint`: FAILED - $($endpointTest.Error)" "ERROR"
                    }
                }
            }
            
            if ($healthyEndpoints -eq $totalEndpoints) {
                $result.Status = "Healthy"
                $result.Message = "All endpoints are responding ($healthyEndpoints/$totalEndpoints)"
            }
            else {
                $result.Status = "Degraded"
                $result.Message = "Some endpoints are not responding ($healthyEndpoints/$totalEndpoints)"
            }
        }
        else {
            $result.Status = "Unhealthy"
            $result.Message = "Unable to determine service endpoint"
        }
    }
    catch {
        $result.Status = "Unhealthy"
        $result.Message = "Application endpoint check error: $($_.Exception.Message)"
    }
    
    return $result
}

# Check persistent volumes
function Test-PersistentVolumes {
    Write-HealthLog "Checking persistent volumes..." "INFO"
    
    $result = [HealthCheckResult]::new("PersistentVolumes", "Unknown", "")
    
    try {
        $pvcJson = kubectl get pvc -n $Namespace -o json 2>&1
        if ($LASTEXITCODE -eq 0) {
            $pvcs = ($pvcJson | ConvertFrom-Json).items
            
            $result.Details["PVCs"] = @{}
            $boundPVCs = 0
            $totalPVCs = $pvcs.Count
            
            foreach ($pvc in $pvcs) {
                $pvcName = $pvc.metadata.name
                $pvcStatus = $pvc.status.phase
                
                $result.Details["PVCs"][$pvcName] = @{
                    Status = $pvcStatus
                    Capacity = $pvc.status.capacity.storage
                    StorageClass = $pvc.spec.storageClassName
                }
                
                if ($pvcStatus -eq "Bound") {
                    $boundPVCs++
                }
                
                if ($DetailedOutput) {
                    Write-HealthLog "PVC $pvcName`: $pvcStatus" "INFO"
                }
            }
            
            if ($boundPVCs -eq $totalPVCs) {
                $result.Status = "Healthy"
                $result.Message = "All PVCs are bound ($boundPVCs/$totalPVCs)"
            }
            else {
                $result.Status = "Degraded"
                $result.Message = "Some PVCs are not bound ($boundPVCs/$totalPVCs)"
            }
        }
        else {
            $result.Status = "Healthy"
            $result.Message = "No PVCs found or unable to access"
        }
    }
    catch {
        $result.Status = "Unhealthy"
        $result.Message = "PVC check error: $($_.Exception.Message)"
    }
    
    return $result
}

# Generate health report
function Generate-HealthReport {
    param([System.Collections.ArrayList]$Results)
    
    $overallStatus = "Healthy"
    $healthyComponents = 0
    $totalComponents = $Results.Count
    
    foreach ($result in $Results) {
        if ($result.Status -eq "Healthy") {
            $healthyComponents++
        }
        elseif ($result.Status -eq "Degraded" -and $overallStatus -eq "Healthy") {
            $overallStatus = "Degraded"
        }
        elseif ($result.Status -eq "Unhealthy") {
            $overallStatus = "Unhealthy"
        }
    }
    
    $report = @{
        Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC"
        Environment = $Environment
        Namespace = $Namespace
        OverallStatus = $overallStatus
        HealthyComponents = $healthyComponents
        TotalComponents = $totalComponents
        Components = $Results
    }
    
    return $report
}

# Main execution
try {
    Write-HealthLog "Starting ACS health check for $Environment environment" "INFO"
    Write-HealthLog "Namespace: $Namespace" "INFO"
    
    $results = [System.Collections.ArrayList]::new()
    
    # Run all health checks
    $results.Add((Test-KubernetesConnectivity)) | Out-Null
    $results.Add((Test-Namespace)) | Out-Null
    $results.Add((Test-Pods)) | Out-Null
    $results.Add((Test-Services)) | Out-Null
    $results.Add((Test-Deployments)) | Out-Null
    $results.Add((Test-ApplicationEndpoints)) | Out-Null
    $results.Add((Test-PersistentVolumes)) | Out-Null
    
    # Generate report
    $report = Generate-HealthReport -Results $results
    
    # Output results
    if ($JsonOutput) {
        $report | ConvertTo-Json -Depth 10
    }
    else {
        Write-HealthLog "" "INFO"
        Write-HealthLog "=== HEALTH CHECK SUMMARY ===" "INFO"
        Write-HealthLog "Overall Status: $($report.OverallStatus)" $(if($report.OverallStatus -eq "Healthy") {"SUCCESS"} elseif($report.OverallStatus -eq "Degraded") {"WARN"} else {"ERROR"})
        Write-HealthLog "Healthy Components: $($report.HealthyComponents)/$($report.TotalComponents)" "INFO"
        Write-HealthLog "" "INFO"
        
        foreach ($result in $results) {
            $statusColor = switch($result.Status) {
                "Healthy" { "SUCCESS" }
                "Degraded" { "WARN" }
                default { "ERROR" }
            }
            Write-HealthLog "$($result.Component): $($result.Status) - $($result.Message)" $statusColor
        }
        
        Write-HealthLog "" "INFO"
        Write-HealthLog "Health check completed at $(Get-Date)" "INFO"
    }
    
    # Set exit code based on overall status
    if ($report.OverallStatus -eq "Unhealthy") {
        exit 1
    }
    elseif ($report.OverallStatus -eq "Degraded") {
        exit 2
    }
    else {
        exit 0
    }
}
catch {
    Write-HealthLog "Health check script failed: $($_.Exception.Message)" "ERROR"
    exit 1
}