# ACS Operations Guide

## Overview

This guide covers deployment, monitoring, and operational procedures for the ACS (Access Control System) in production environments. The system supports Docker, Kubernetes, and traditional server deployments with comprehensive monitoring and multi-tenant capabilities.

## Deployment Architecture

### Production Topology

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Load Balancer │    │   HTTP API      │    │  VerticalHost   │
│   (nginx/ALB)   │───▶│   Cluster       │───▶│   Cluster       │
│   Port 80/443   │    │   Ports 5000+   │    │   Ports 50051+  │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         ▼                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Monitoring    │    │   Logging       │    │   Database      │
│   Prometheus    │    │   ELK Stack     │    │   SQL Server    │
│   Grafana       │    │   OpenTelemetry │    │   AlwaysOn AG   │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

### Multi-Tenant Isolation

Each tenant runs in its own VerticalHost process with isolated resources:

```
Tenant A: HTTP API (5000) → VerticalHost A (50051) → Database A
Tenant B: HTTP API (5001) → VerticalHost B (50052) → Database B  
Tenant C: HTTP API (5002) → VerticalHost C (50053) → Database C
```

## Docker Deployment

### Docker Images

Build production-ready Docker images:

```dockerfile
# ACS.WebApi Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Health check endpoint
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

EXPOSE 5000
ENTRYPOINT ["dotnet", "ACS.WebApi.dll"]

# ACS.VerticalHost Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Health check for gRPC service
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD grpc_health_probe -addr=localhost:50051 || exit 1

EXPOSE 50051
ENTRYPOINT ["dotnet", "ACS.VerticalHost.dll"]
```

### Docker Compose Production Setup

```yaml
version: '3.8'
services:
  # SQL Server Database
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: Y
      SA_PASSWORD: ${SQL_SA_PASSWORD}
      MSSQL_PID: Developer
    volumes:
      - sqlserver_data:/var/opt/mssql
    ports:
      - "1433:1433"
    deploy:
      resources:
        limits:
          memory: 4G
        reservations:
          memory: 2G

  # Redis Cache (optional)
  redis:
    image: redis:7-alpine
    command: redis-server --appendonly yes --maxmemory 512mb --maxmemory-policy allkeys-lru
    volumes:
      - redis_data:/data
    ports:
      - "6379:6379"

  # Tenant A Services
  webapi-tenant-a:
    image: acs-webapi:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - TENANT_ID=tenant-a
      - BASE_CONNECTION_STRING=Server=sqlserver;Database=ACS_TenantA;User Id=sa;Password=${SQL_SA_PASSWORD};TrustServerCertificate=true
      - VERTICAL_HOST_ENDPOINT=verticalhost-tenant-a:50051
      - REDIS_CONNECTION_STRING=redis:6379
      - JWT_SECRET_KEY=${JWT_SECRET_KEY}
    depends_on:
      - sqlserver
      - redis
      - verticalhost-tenant-a
    ports:
      - "5000:5000"
    deploy:
      replicas: 2
      resources:
        limits:
          memory: 1G
        reservations:
          memory: 512M

  verticalhost-tenant-a:
    image: acs-verticalhost:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - TENANT_ID=tenant-a
      - GRPC_PORT=50051
      - BASE_CONNECTION_STRING=Server=sqlserver;Database=ACS_TenantA;User Id=sa;Password=${SQL_SA_PASSWORD};TrustServerCertificate=true
      - OTEL_EXPORTER_OTLP_ENDPOINT=http://jaeger:4317
    depends_on:
      - sqlserver
    ports:
      - "50051:50051"
    deploy:
      resources:
        limits:
          memory: 2G
        reservations:
          memory: 1G

  # Tenant B Services (similar structure)
  webapi-tenant-b:
    image: acs-webapi:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - TENANT_ID=tenant-b
      - BASE_CONNECTION_STRING=Server=sqlserver;Database=ACS_TenantB;User Id=sa;Password=${SQL_SA_PASSWORD};TrustServerCertificate=true
      - VERTICAL_HOST_ENDPOINT=verticalhost-tenant-b:50052
    ports:
      - "5001:5000"

  verticalhost-tenant-b:
    image: acs-verticalhost:latest
    environment:
      - TENANT_ID=tenant-b
      - GRPC_PORT=50052
    ports:
      - "50052:50051"

  # Monitoring Stack
  prometheus:
    image: prom/prometheus:latest
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
      - '--web.console.libraries=/etc/prometheus/console_libraries'
      - '--web.console.templates=/etc/prometheus/consoles'
    volumes:
      - ./monitoring/prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus_data:/prometheus
    ports:
      - "9090:9090"

  grafana:
    image: grafana/grafana:latest
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=${GRAFANA_ADMIN_PASSWORD}
    volumes:
      - grafana_data:/var/lib/grafana
      - ./monitoring/grafana/dashboards:/etc/grafana/provisioning/dashboards
    ports:
      - "3000:3000"

  jaeger:
    image: jaegertracing/all-in-one:latest
    environment:
      - COLLECTOR_OTLP_ENABLED=true
    ports:
      - "16686:16686"
      - "4317:4317"

volumes:
  sqlserver_data:
  redis_data:
  prometheus_data:
  grafana_data:
```

### Environment Variables

Create `.env` file for production:

```bash
# Database Configuration
SQL_SA_PASSWORD=YourSecurePassword123!

# JWT Configuration
JWT_SECRET_KEY=your-256-bit-secret-key-here-must-be-very-secure-for-production

# Monitoring
GRAFANA_ADMIN_PASSWORD=admin123

# OpenTelemetry
OTEL_SERVICE_NAME=acs-system
OTEL_RESOURCE_ATTRIBUTES=service.version=1.0.0

# Performance Tuning
DATABASE_POOL_SIZE=128
COMMAND_BUFFER_CAPACITY=10000
REDIS_MAX_MEMORY=512mb
```

## Kubernetes Deployment

### Namespace and ConfigMap

```yaml
# namespace.yaml
apiVersion: v1
kind: Namespace
metadata:
  name: acs-system
  labels:
    name: acs-system

---
# configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: acs-config
  namespace: acs-system
data:
  appsettings.json: |
    {
      "Database": {
        "DbContextPoolSize": 128,
        "CommandTimeoutSeconds": 30,
        "MaxRetryCount": 5
      },
      "CommandBuffer": {
        "Capacity": 10000,
        "FullMode": "Wait"
      },
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft.EntityFrameworkCore": "Warning"
        }
      },
      "OpenTelemetry": {
        "ServiceName": "acs-system",
        "TracingEnabled": true,
        "MetricsEnabled": true
      }
    }
```

### WebApi Deployment

```yaml
# webapi-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: acs-webapi
  namespace: acs-system
spec:
  replicas: 3
  selector:
    matchLabels:
      app: acs-webapi
  template:
    metadata:
      labels:
        app: acs-webapi
    spec:
      containers:
      - name: webapi
        image: acs-webapi:1.0.0
        ports:
        - containerPort: 5000
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: BASE_CONNECTION_STRING
          valueFrom:
            secretKeyRef:
              name: database-secret
              key: connection-string
        - name: JWT_SECRET_KEY
          valueFrom:
            secretKeyRef:
              name: jwt-secret
              key: secret-key
        - name: VERTICAL_HOST_ENDPOINTS
          value: "acs-verticalhost:50051"
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "1Gi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 5000
          initialDelaySeconds: 30
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /health
            port: 5000
          initialDelaySeconds: 5
          periodSeconds: 10
        volumeMounts:
        - name: config-volume
          mountPath: /app/appsettings.json
          subPath: appsettings.json
      volumes:
      - name: config-volume
        configMap:
          name: acs-config

---
apiVersion: v1
kind: Service
metadata:
  name: acs-webapi
  namespace: acs-system
spec:
  selector:
    app: acs-webapi
  ports:
  - port: 80
    targetPort: 5000
  type: ClusterIP
```

### VerticalHost Deployment

```yaml
# verticalhost-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: acs-verticalhost
  namespace: acs-system
spec:
  replicas: 2
  selector:
    matchLabels:
      app: acs-verticalhost
  template:
    metadata:
      labels:
        app: acs-verticalhost
    spec:
      containers:
      - name: verticalhost
        image: acs-verticalhost:1.0.0
        ports:
        - containerPort: 50051
        env:
        - name: TENANT_ID
          value: "default"
        - name: GRPC_PORT
          value: "50051"
        - name: BASE_CONNECTION_STRING
          valueFrom:
            secretKeyRef:
              name: database-secret
              key: connection-string
        resources:
          requests:
            memory: "1Gi"
            cpu: "500m"
          limits:
            memory: "2Gi"
            cpu: "1000m"
        livenessProbe:
          exec:
            command: ["/bin/grpc_health_probe", "-addr=:50051"]
          initialDelaySeconds: 30
          periodSeconds: 30
        readinessProbe:
          exec:
            command: ["/bin/grpc_health_probe", "-addr=:50051"]
          initialDelaySeconds: 5
          periodSeconds: 10

---
apiVersion: v1
kind: Service
metadata:
  name: acs-verticalhost
  namespace: acs-system
spec:
  selector:
    app: acs-verticalhost
  ports:
  - port: 50051
    targetPort: 50051
  type: ClusterIP
```

### Secrets Management

```yaml
# secrets.yaml
apiVersion: v1
kind: Secret
metadata:
  name: database-secret
  namespace: acs-system
type: Opaque
data:
  connection-string: <base64-encoded-connection-string>

---
apiVersion: v1
kind: Secret
metadata:
  name: jwt-secret
  namespace: acs-system
type: Opaque
data:
  secret-key: <base64-encoded-jwt-secret>
```

## Multi-Tenant Configuration

### Tenant Isolation Strategies

#### 1. Process-Level Isolation (Recommended)

Each tenant runs in its own VerticalHost process:

```bash
# Start tenant-specific services
docker run -d --name acs-tenant-a \
  -e TENANT_ID=tenant-a \
  -e GRPC_PORT=50051 \
  -e BASE_CONNECTION_STRING="Server=sql;Database=ACS_TenantA;..." \
  acs-verticalhost:latest

docker run -d --name acs-tenant-b \
  -e TENANT_ID=tenant-b \
  -e GRPC_PORT=50052 \
  -e BASE_CONNECTION_STRING="Server=sql;Database=ACS_TenantB;..." \
  acs-verticalhost:latest
```

#### 2. Database-Level Isolation

```csharp
// In VerticalHost startup (Program.cs)
public static string GetTenantConnectionString(string tenantId)
{
    var baseConnectionString = Environment.GetEnvironmentVariable("BASE_CONNECTION_STRING") 
        ?? "Server=(localdb)\\mssqllocaldb;Database=ACS_{TenantId};Trusted_Connection=true;MultipleActiveResultSets=true";
    
    return baseConnectionString.Replace("{TenantId}", tenantId);
}
```

#### 3. Kubernetes Multi-Tenant Deployment

```yaml
# tenant-specific-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: acs-verticalhost-tenant-a
  namespace: acs-system
spec:
  replicas: 1
  selector:
    matchLabels:
      app: acs-verticalhost
      tenant: tenant-a
  template:
    metadata:
      labels:
        app: acs-verticalhost
        tenant: tenant-a
    spec:
      containers:
      - name: verticalhost
        image: acs-verticalhost:1.0.0
        env:
        - name: TENANT_ID
          value: "tenant-a"
        - name: BASE_CONNECTION_STRING
          valueFrom:
            secretKeyRef:
              name: database-secret-tenant-a
              key: connection-string
        resources:
          requests:
            memory: "1Gi"
            cpu: "500m"
          limits:
            memory: "2Gi"
            cpu: "1000m"
```

## Monitoring and Alerting

### Prometheus Configuration

```yaml
# monitoring/prometheus.yml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'acs-webapi'
    static_configs:
      - targets: ['acs-webapi:5000']
    metrics_path: /metrics
    scrape_interval: 10s

  - job_name: 'acs-verticalhost'
    static_configs:
      - targets: ['acs-verticalhost:50051']
    metrics_path: /metrics
    scrape_interval: 10s

  - job_name: 'sqlserver'
    static_configs:
      - targets: ['sqlserver:1433']
    metrics_path: /metrics
    scrape_interval: 30s

rule_files:
  - "alerts.yml"

alerting:
  alertmanagers:
    - static_configs:
        - targets:
          - alertmanager:9093
```

### Alert Rules

```yaml
# monitoring/alerts.yml
groups:
- name: acs-alerts
  rules:
  - alert: HighCommandBufferUsage
    expr: command_buffer_usage_percent > 80
    for: 2m
    labels:
      severity: warning
    annotations:
      summary: "Command buffer usage high on {{ $labels.instance }}"
      description: "Command buffer usage is {{ $value }}% on {{ $labels.instance }}"

  - alert: DatabaseConnectionPoolExhausted  
    expr: database_connection_pool_usage_percent > 90
    for: 1m
    labels:
      severity: critical
    annotations:
      summary: "Database connection pool nearly exhausted on {{ $labels.instance }}"
      description: "Connection pool usage is {{ $value }}% on {{ $labels.instance }}"

  - alert: HighCommandProcessingLatency
    expr: command_processing_duration_seconds_p95 > 5
    for: 5m
    labels:
      severity: warning
    annotations:
      summary: "High command processing latency on {{ $labels.instance }}"
      description: "95th percentile processing time is {{ $value }}s on {{ $labels.instance }}"

  - alert: ServiceDown
    expr: up == 0
    for: 1m
    labels:
      severity: critical
    annotations:
      summary: "Service {{ $labels.job }} down"
      description: "{{ $labels.job }} on {{ $labels.instance }} has been down for more than 1 minute"
```

### Grafana Dashboards

Key metrics to monitor:

```json
{
  "dashboard": {
    "title": "ACS System Overview",
    "panels": [
      {
        "title": "Command Processing Rate",
        "type": "graph",
        "targets": [
          {
            "expr": "rate(commands_processed_total[5m])",
            "legendFormat": "Commands/sec - {{instance}}"
          }
        ]
      },
      {
        "title": "Command Buffer Usage",
        "type": "singlestat", 
        "targets": [
          {
            "expr": "command_buffer_usage_percent",
            "legendFormat": "Buffer Usage %"
          }
        ]
      },
      {
        "title": "Database Connection Pool",
        "type": "graph",
        "targets": [
          {
            "expr": "database_connections_active",
            "legendFormat": "Active Connections"
          },
          {
            "expr": "database_connections_idle", 
            "legendFormat": "Idle Connections"
          }
        ]
      },
      {
        "title": "Entity Graph Memory Usage",
        "type": "graph",
        "targets": [
          {
            "expr": "entity_graph_memory_bytes / 1024 / 1024",
            "legendFormat": "Memory Usage (MB)"
          }
        ]
      }
    ]
  }
}
```

## Backup and Disaster Recovery

### Database Backup Strategy

```sql
-- Full backup daily
BACKUP DATABASE [ACS_TenantA] 
TO DISK = '/backups/ACS_TenantA_Full_{timestamp}.bak'
WITH COMPRESSION, CHECKSUM, INIT;

-- Differential backup every 4 hours
BACKUP DATABASE [ACS_TenantA] 
TO DISK = '/backups/ACS_TenantA_Diff_{timestamp}.bak'
WITH DIFFERENTIAL, COMPRESSION, CHECKSUM, INIT;

-- Transaction log backup every 15 minutes
BACKUP LOG [ACS_TenantA] 
TO DISK = '/backups/ACS_TenantA_Log_{timestamp}.trn'
WITH COMPRESSION, CHECKSUM, INIT;
```

### Automated Backup Scripts

```bash
#!/bin/bash
# backup.sh - Automated backup script

TENANT_ID=$1
BACKUP_DIR="/backups/${TENANT_ID}"
DATABASE="ACS_${TENANT_ID}"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)

# Create backup directory
mkdir -p $BACKUP_DIR

# Full backup
sqlcmd -S localhost -E -Q "
BACKUP DATABASE [$DATABASE] 
TO DISK = '$BACKUP_DIR/${DATABASE}_Full_${TIMESTAMP}.bak'
WITH COMPRESSION, CHECKSUM, INIT;"

# Upload to cloud storage
aws s3 cp $BACKUP_DIR/${DATABASE}_Full_${TIMESTAMP}.bak \
  s3://acs-backups/tenant-$TENANT_ID/ \
  --storage-class GLACIER

# Cleanup local backups older than 7 days
find $BACKUP_DIR -name "*.bak" -mtime +7 -delete

echo "Backup completed for tenant: $TENANT_ID"
```

### Docker Volume Backup

```bash
# Backup SQL Server data volume
docker run --rm -v acs_sqlserver_data:/data -v /backup:/backup \
  alpine tar czf /backup/sqlserver-backup-$(date +%Y%m%d).tar.gz -C /data .

# Backup Redis data
docker run --rm -v acs_redis_data:/data -v /backup:/backup \
  alpine tar czf /backup/redis-backup-$(date +%Y%m%d).tar.gz -C /data .
```

### Recovery Procedures

```bash
# 1. Stop all services
docker-compose down

# 2. Restore database backup
sqlcmd -S localhost -E -Q "
RESTORE DATABASE [ACS_TenantA] 
FROM DISK = '/backups/ACS_TenantA_Full_20240101_120000.bak'
WITH REPLACE, CHECKDB;"

# 3. Restore data volumes
docker run --rm -v acs_sqlserver_data:/data -v /backup:/backup \
  alpine tar xzf /backup/sqlserver-backup-20240101.tar.gz -C /data

# 4. Start services
docker-compose up -d

# 5. Verify health
curl http://localhost:5000/health
```

## Troubleshooting Guide

### Common Issues and Solutions

#### 1. Command Buffer Full

**Symptoms**: High latency, timeout errors, "buffer full" messages

**Diagnosis**:
```bash
# Check buffer statistics
curl http://localhost:50051/health | jq '.entries.command_buffer'
```

**Solutions**:
- Increase buffer capacity in configuration
- Scale VerticalHost instances
- Optimize slow handlers
- Implement backpressure

```yaml
# Increase buffer capacity
environment:
  - COMMAND_BUFFER_CAPACITY=20000
```

#### 2. Database Connection Pool Exhausted

**Symptoms**: "Connection pool exhausted" errors, timeouts

**Diagnosis**:
```sql
-- Check active connections
SELECT 
    DB_NAME(dbid) as DatabaseName,
    COUNT(dbid) as NumberOfConnections,
    loginame as LoginName
FROM sys.sysprocesses 
WHERE dbid > 0 
GROUP BY dbid, loginame;
```

**Solutions**:
- Increase pool size
- Fix connection leaks
- Optimize long-running queries

```yaml
environment:
  - DATABASE_POOL_SIZE=256
```

#### 3. Entity Graph Out of Memory

**Symptoms**: OutOfMemoryException, high memory usage

**Diagnosis**:
```bash
# Check memory usage
curl http://localhost:50051/health | jq '.entries.entity_graph.data.memory_usage_mb'
```

**Solutions**:
- Increase container memory limits
- Implement entity graph cleanup
- Partition large tenants

```yaml
resources:
  limits:
    memory: "4Gi"
```

#### 4. gRPC Communication Failures

**Symptoms**: "Service unavailable", connection timeouts

**Diagnosis**:
```bash
# Test gRPC connectivity
grpc_health_probe -addr=localhost:50051 -v
```

**Solutions**:
- Check firewall rules
- Verify TLS certificates
- Increase timeout values

#### 5. Performance Degradation

**Symptoms**: Slow response times, high CPU usage

**Diagnosis**:
- Check Prometheus metrics
- Review application logs
- Profile database queries

**Solutions**:
- Scale horizontally
- Optimize database queries
- Implement caching
- Review command handler performance

### Log Analysis

```bash
# View recent errors across all services
docker-compose logs --tail=100 | grep ERROR

# Check specific service logs
docker logs acs-verticalhost-tenant-a --tail=50 -f

# Search for specific patterns
docker logs acs-webapi 2>&1 | grep -i "timeout\|error\|exception"
```

### Performance Tuning

```yaml
# Production performance configuration
environment:
  # Database optimization
  - DATABASE_POOL_SIZE=256
  - DATABASE_COMMAND_TIMEOUT=60
  - DATABASE_MAX_RETRY_COUNT=5
  
  # Command buffer tuning
  - COMMAND_BUFFER_CAPACITY=20000
  - COMMAND_BUFFER_FULL_MODE=Wait
  
  # Memory management
  - DOTNET_gcServer=1
  - DOTNET_GCHeapCount=4
  - DOTNET_GCConserveMemory=5
  
  # gRPC optimization  
  - GRPC_MAX_RECEIVE_MESSAGE_SIZE=33554432
  - GRPC_MAX_SEND_MESSAGE_SIZE=33554432
  - GRPC_KEEPALIVE_TIME_MS=30000
```

This operations guide provides comprehensive deployment and maintenance procedures for enterprise-grade ACS deployments.