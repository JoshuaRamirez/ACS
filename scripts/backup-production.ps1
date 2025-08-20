# ACS Production Backup Script
# This script creates comprehensive backups of the ACS application and database

param(
    [Parameter(Mandatory=$false)]
    [string]$BackupName = "",
    
    [Parameter(Mandatory=$false)]
    [string]$BackupLocation = "./backups",
    
    [Parameter(Mandatory=$false)]
    [switch]$IncludeKubernetesConfig = $true,
    
    [Parameter(Mandatory=$false)]
    [switch]$IncludeDatabase = $true,
    
    [Parameter(Mandatory=$false)]
    [switch]$IncludeSecrets = $false,
    
    [Parameter(Mandatory=$false)]
    [int]$RetentionDays = 30
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Configuration
$BackupConfig = @{
    KubernetesNamespace = "acs-production"
    DatabasePodLabel = "app=acs-database"
    DatabaseName = "ACS_Production"
    BackupDateFormat = "yyyyMMdd-HHmmss"
}

# Generate backup name if not provided
if ([string]::IsNullOrEmpty($BackupName)) {
    $BackupName = "acs-backup-$(Get-Date -Format $BackupConfig.BackupDateFormat)"
}

# Logging function
function Write-BackupLog {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] [BACKUP] $Message"
    Write-Host $logMessage -ForegroundColor $(if($Level -eq "ERROR") {"Red"} elseif($Level -eq "WARN") {"Yellow"} else {"Cyan"})
    Add-Content -Path "./logs/backup-$(Get-Date -Format 'yyyyMMdd').log" -Value $logMessage
}

# Create directories
foreach ($dir in @($BackupLocation, "./logs")) {
    if (!(Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force
    }
}

$backupPath = Join-Path $BackupLocation $BackupName
New-Item -ItemType Directory -Path $backupPath -Force

Write-BackupLog "Starting backup: $BackupName" "INFO"
Write-BackupLog "Backup location: $backupPath" "INFO"

try {
    # Step 1: Pre-backup validation
    Write-BackupLog "Step 1: Pre-backup validation" "INFO"
    
    if (!(Get-Command "kubectl" -ErrorAction SilentlyContinue)) {
        throw "kubectl is not installed or not in PATH"
    }
    
    # Check cluster connectivity
    $clusterInfo = kubectl cluster-info 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to connect to Kubernetes cluster: $clusterInfo"
    }
    Write-BackupLog "Kubernetes cluster connectivity verified" "INFO"
    
    # Step 2: Backup Kubernetes configurations
    if ($IncludeKubernetesConfig) {
        Write-BackupLog "Step 2: Backing up Kubernetes configurations" "INFO"
        
        $k8sBackupPath = Join-Path $backupPath "kubernetes"
        New-Item -ItemType Directory -Path $k8sBackupPath -Force
        
        # Backup all resources in the namespace
        $resourceTypes = @(
            "deployments",
            "services", 
            "configmaps",
            "persistentvolumeclaims",
            "ingresses",
            "horizontalpodautoscalers",
            "networkpolicies"
        )
        
        if ($IncludeSecrets) {
            $resourceTypes += "secrets"
        }
        
        foreach ($resourceType in $resourceTypes) {
            try {
                Write-BackupLog "Backing up $resourceType" "INFO"
                $output = kubectl get $resourceType -n $($BackupConfig.KubernetesNamespace) -o yaml 2>&1
                
                if ($LASTEXITCODE -eq 0) {
                    $outputFile = Join-Path $k8sBackupPath "$resourceType.yaml"
                    $output | Out-File -FilePath $outputFile -Encoding utf8
                    Write-BackupLog "$resourceType backed up to $outputFile" "INFO"
                } else {
                    Write-BackupLog "No $resourceType found or error occurred" "WARN"
                }
            } catch {
                Write-BackupLog "Failed to backup $resourceType`: $($_.Exception.Message)" "WARN"
            }
        }
        
        # Backup persistent volume definitions
        try {
            $pvOutput = kubectl get pv -o yaml 2>&1
            if ($LASTEXITCODE -eq 0) {
                $pvFile = Join-Path $k8sBackupPath "persistentvolumes.yaml"
                $pvOutput | Out-File -FilePath $pvFile -Encoding utf8
                Write-BackupLog "Persistent volumes backed up" "INFO"
            }
        } catch {
            Write-BackupLog "Failed to backup persistent volumes: $($_.Exception.Message)" "WARN"
        }
        
        Write-BackupLog "Kubernetes configurations backup completed" "INFO"
    }
    
    # Step 3: Backup database
    if ($IncludeDatabase) {
        Write-BackupLog "Step 3: Backing up database" "INFO"
        
        $dbBackupPath = Join-Path $backupPath "database"
        New-Item -ItemType Directory -Path $dbBackupPath -Force
        
        # Find database pod
        $dbPod = kubectl get pods -n $($BackupConfig.KubernetesNamespace) -l $($BackupConfig.DatabasePodLabel) -o jsonpath='{.items[0].metadata.name}' 2>&1
        
        if ($LASTEXITCODE -eq 0 -and ![string]::IsNullOrEmpty($dbPod)) {
            Write-BackupLog "Found database pod: $dbPod" "INFO"
            
            # Create database dump
            $dumpFileName = "database-dump-$(Get-Date -Format $BackupConfig.BackupDateFormat).sql"
            $dumpFilePath = Join-Path $dbBackupPath $dumpFileName
            
            # SQL Server backup command (adjust for your database type)
            $backupCommand = "sqlcmd -S localhost -d $($BackupConfig.DatabaseName) -Q `"BACKUP DATABASE [$($BackupConfig.DatabaseName)] TO DISK = '/tmp/backup.bak'`""
            
            Write-BackupLog "Creating database backup..." "INFO"
            kubectl exec $dbPod -n $($BackupConfig.KubernetesNamespace) -- powershell -c $backupCommand
            
            if ($LASTEXITCODE -eq 0) {
                # Copy backup file from pod
                kubectl cp "$($BackupConfig.KubernetesNamespace)/$dbPod`:/tmp/backup.bak" $dumpFilePath
                
                if ($LASTEXITCODE -eq 0) {
                    Write-BackupLog "Database backup created: $dumpFilePath" "INFO"
                } else {
                    Write-BackupLog "Failed to copy database backup from pod" "ERROR"
                }
            } else {
                Write-BackupLog "Database backup command failed" "ERROR"
            }
            
        } else {
            Write-BackupLog "Database pod not found with label: $($BackupConfig.DatabasePodLabel)" "WARN"
        }
    }
    
    # Step 4: Backup application configuration
    Write-BackupLog "Step 4: Backing up application configuration" "INFO"
    
    $configBackupPath = Join-Path $backupPath "configuration"
    New-Item -ItemType Directory -Path $configBackupPath -Force
    
    # Copy important configuration files
    $configFiles = @(
        "./k8s/*.yaml",
        "./docker-compose.yml",
        "./ACS.WebApi/appsettings.json",
        "./ACS.WebApi/appsettings.Production.json",
        "./ACS.VerticalHost/appsettings.json",
        "./deployment/*.json"
    )
    
    foreach ($pattern in $configFiles) {
        try {
            $files = Get-ChildItem $pattern -ErrorAction SilentlyContinue
            foreach ($file in $files) {
                $destPath = Join-Path $configBackupPath $file.Name
                Copy-Item $file.FullName $destPath -Force
                Write-BackupLog "Backed up configuration: $($file.Name)" "INFO"
            }
        } catch {
            Write-BackupLog "Failed to backup configuration pattern $pattern`: $($_.Exception.Message)" "WARN"
        }
    }
    
    # Step 5: Create backup metadata
    Write-BackupLog "Step 5: Creating backup metadata" "INFO"
    
    $metadata = @{
        BackupName = $BackupName
        Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC"
        Environment = "Production"
        KubernetesNamespace = $BackupConfig.KubernetesNamespace
        IncludeKubernetesConfig = $IncludeKubernetesConfig
        IncludeDatabase = $IncludeDatabase
        IncludeSecrets = $IncludeSecrets
        CreatedBy = $env:USERNAME
        BackupVersion = "1.0"
    }
    
    # Get current deployment versions
    try {
        $deployments = kubectl get deployments -n $($BackupConfig.KubernetesNamespace) -o json | ConvertFrom-Json
        $metadata.Deployments = @{}
        
        foreach ($deployment in $deployments.items) {
            $name = $deployment.metadata.name
            $image = $deployment.spec.template.spec.containers[0].image
            $metadata.Deployments[$name] = @{
                Image = $image
                Replicas = $deployment.spec.replicas
                ReadyReplicas = $deployment.status.readyReplicas
            }
        }
    } catch {
        Write-BackupLog "Failed to capture deployment information: $($_.Exception.Message)" "WARN"
    }
    
    # Save metadata
    $metadataFile = Join-Path $backupPath "backup-metadata.json"
    $metadata | ConvertTo-Json -Depth 4 | Out-File $metadataFile -Encoding utf8
    Write-BackupLog "Backup metadata saved: $metadataFile" "INFO"
    
    # Step 6: Create backup archive
    Write-BackupLog "Step 6: Creating backup archive" "INFO"
    
    $archivePath = "$backupPath.zip"
    
    if (Get-Command "Compress-Archive" -ErrorAction SilentlyContinue) {
        Compress-Archive -Path "$backupPath\*" -DestinationPath $archivePath -Force
        Write-BackupLog "Backup archive created: $archivePath" "INFO"
        
        # Get archive size
        $archiveSize = (Get-Item $archivePath).Length
        $archiveSizeMB = [math]::Round($archiveSize / 1MB, 2)
        Write-BackupLog "Archive size: $archiveSizeMB MB" "INFO"
    } else {
        Write-BackupLog "Compress-Archive not available, backup remains uncompressed" "WARN"
    }
    
    # Step 7: Cleanup old backups
    Write-BackupLog "Step 7: Cleaning up old backups" "INFO"
    
    try {
        $cutoffDate = (Get-Date).AddDays(-$RetentionDays)
        $oldBackups = Get-ChildItem $BackupLocation -Directory | Where-Object { $_.CreationTime -lt $cutoffDate }
        
        foreach ($oldBackup in $oldBackups) {
            Remove-Item $oldBackup.FullName -Recurse -Force
            Write-BackupLog "Removed old backup: $($oldBackup.Name)" "INFO"
        }
        
        # Also cleanup old archive files
        $oldArchives = Get-ChildItem "$BackupLocation/*.zip" | Where-Object { $_.CreationTime -lt $cutoffDate }
        foreach ($oldArchive in $oldArchives) {
            Remove-Item $oldArchive.FullName -Force
            Write-BackupLog "Removed old archive: $($oldArchive.Name)" "INFO"
        }
        
        if ($oldBackups.Count -eq 0 -and $oldArchives.Count -eq 0) {
            Write-BackupLog "No old backups found for cleanup" "INFO"
        }
    } catch {
        Write-BackupLog "Failed to cleanup old backups: $($_.Exception.Message)" "WARN"
    }
    
    # Step 8: Verify backup integrity
    Write-BackupLog "Step 8: Verifying backup integrity" "INFO"
    
    $verificationResults = @{
        MetadataExists = Test-Path $metadataFile
        KubernetesConfigExists = Test-Path (Join-Path $backupPath "kubernetes")
        DatabaseBackupExists = Test-Path (Join-Path $backupPath "database")
        ConfigurationExists = Test-Path (Join-Path $backupPath "configuration")
        ArchiveExists = Test-Path $archivePath
    }
    
    $allChecksPass = $verificationResults.Values -notcontains $false
    
    if ($allChecksPass) {
        Write-BackupLog "Backup verification passed" "INFO"
    } else {
        Write-BackupLog "Backup verification failed for some components:" "WARN"
        foreach ($check in $verificationResults.GetEnumerator()) {
            if (!$check.Value) {
                Write-BackupLog "  $($check.Key): FAILED" "WARN"
            }
        }
    }
    
    Write-BackupLog "Backup completed successfully!" "INFO"
    Write-BackupLog "Backup name: $BackupName" "INFO"
    Write-BackupLog "Backup path: $backupPath" "INFO"
    if (Test-Path $archivePath) {
        Write-BackupLog "Archive path: $archivePath" "INFO"
    }
    
} catch {
    Write-BackupLog "Backup failed: $($_.Exception.Message)" "ERROR"
    Write-BackupLog "Stack trace: $($_.ScriptStackTrace)" "ERROR"
    exit 1
}

Write-BackupLog "Backup script completed at $(Get-Date)" "INFO"