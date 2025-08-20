#!/bin/bash

# ACS Staging Deployment Script
# This script deploys the ACS application to staging environment

set -euo pipefail

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
ENVIRONMENT="${ENVIRONMENT:-staging}"
VERSION="${VERSION:-latest}"
SKIP_HEALTH_CHECK="${SKIP_HEALTH_CHECK:-false}"
SKIP_TESTS="${SKIP_TESTS:-false}"
NAMESPACE="acs-staging"
REGISTRY="acsregistry.azurecr.io"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging function
log() {
    local level="$1"
    shift
    local message="$*"
    local timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    
    case "$level" in
        INFO)  echo -e "${GREEN}[$timestamp] [INFO] $message${NC}" ;;
        WARN)  echo -e "${YELLOW}[$timestamp] [WARN] $message${NC}" ;;
        ERROR) echo -e "${RED}[$timestamp] [ERROR] $message${NC}" ;;
        DEBUG) echo -e "${BLUE}[$timestamp] [DEBUG] $message${NC}" ;;
    esac
    
    # Also log to file
    mkdir -p "$PROJECT_ROOT/logs"
    echo "[$timestamp] [$level] $message" >> "$PROJECT_ROOT/logs/deploy-staging-$(date +%Y%m%d).log"
}

# Error handler
error_exit() {
    log ERROR "$1"
    exit 1
}

# Check prerequisites
check_prerequisites() {
    log INFO "Checking prerequisites..."
    
    # Check required tools
    local tools=("kubectl" "docker" "dotnet")
    for tool in "${tools[@]}"; do
        if ! command -v "$tool" &> /dev/null; then
            error_exit "$tool is not installed or not in PATH"
        fi
    done
    
    # Check kubectl connectivity
    if ! kubectl cluster-info &> /dev/null; then
        error_exit "Unable to connect to Kubernetes cluster"
    fi
    
    # Check Docker daemon
    if ! docker info &> /dev/null; then
        error_exit "Docker daemon is not running"
    fi
    
    log INFO "Prerequisites check passed"
}

# Build and test application
build_and_test() {
    log INFO "Building and testing application..."
    
    cd "$PROJECT_ROOT"
    
    # Clean previous builds
    log INFO "Cleaning previous builds..."
    dotnet clean
    
    # Restore dependencies
    log INFO "Restoring dependencies..."
    dotnet restore
    
    # Build solution
    log INFO "Building solution..."
    dotnet build --configuration Release --no-restore
    
    # Run tests if not skipped
    if [ "$SKIP_TESTS" = "false" ]; then
        log INFO "Running tests..."
        
        # Unit tests
        dotnet test ACS.Service.Tests.Unit --configuration Release --no-build --logger "trx" --results-directory "./TestResults"
        
        # Integration tests (if they exist and can run in staging)
        if [ -d "ACS.WebApi.Tests.Integration" ]; then
            log INFO "Running integration tests..."
            dotnet test ACS.WebApi.Tests.Integration --configuration Release --no-build --logger "trx" --results-directory "./TestResults"
        fi
        
        log INFO "All tests passed"
    else
        log INFO "Skipping tests as requested"
    fi
}

# Build Docker images
build_images() {
    log INFO "Building Docker images..."
    
    cd "$PROJECT_ROOT"
    
    local services=("WebApi" "VerticalHost" "Dashboard")
    
    for service in "${services[@]}"; do
        local service_lower=$(echo "$service" | tr '[:upper:]' '[:lower:]')
        local image_name="$REGISTRY/acs-$service_lower:$VERSION"
        
        log INFO "Building $service image: $image_name"
        
        # Build image
        docker build -t "$image_name" -f "ACS.$service/Dockerfile" .
        
        # Tag as latest for staging
        docker tag "$image_name" "$REGISTRY/acs-$service_lower:staging-latest"
        
        # Push images
        log INFO "Pushing $service image..."
        docker push "$image_name"
        docker push "$REGISTRY/acs-$service_lower:staging-latest"
        
        log INFO "$service image built and pushed successfully"
    done
}

# Update Kubernetes manifests
update_manifests() {
    log INFO "Updating Kubernetes manifests..."
    
    cd "$PROJECT_ROOT"
    
    # Create staging-specific manifests directory
    mkdir -p "k8s/staging"
    
    # Copy base manifests and update for staging
    for manifest in k8s/*.yaml; do
        if [[ "$manifest" != *"staging"* ]]; then
            local staging_manifest="k8s/staging/$(basename "$manifest")"
            cp "$manifest" "$staging_manifest"
            
            # Update namespace
            sed -i "s/namespace: acs-production/namespace: $NAMESPACE/g" "$staging_manifest"
            
            # Update image tags
            sed -i "s|$REGISTRY/acs-webapi:.*|$REGISTRY/acs-webapi:$VERSION|g" "$staging_manifest"
            sed -i "s|$REGISTRY/acs-verticalhost:.*|$REGISTRY/acs-verticalhost:$VERSION|g" "$staging_manifest"
            sed -i "s|$REGISTRY/acs-dashboard:.*|$REGISTRY/acs-dashboard:$VERSION|g" "$staging_manifest"
            
            # Update resource limits for staging (smaller than production)
            sed -i 's/memory: "2Gi"/memory: "1Gi"/g' "$staging_manifest"
            sed -i 's/cpu: "1000m"/cpu: "500m"/g' "$staging_manifest"
            
            # Update replica counts for staging
            sed -i 's/replicas: 3/replicas: 2/g' "$staging_manifest"
        fi
    done
    
    log INFO "Kubernetes manifests updated for staging"
}

# Deploy to Kubernetes
deploy_to_kubernetes() {
    log INFO "Deploying to Kubernetes..."
    
    cd "$PROJECT_ROOT"
    
    # Create/update namespace
    kubectl create namespace "$NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -
    
    # Label namespace
    kubectl label namespace "$NAMESPACE" environment=staging --overwrite
    
    # Apply staging-specific manifests in order
    local manifest_order=(
        "secrets.yaml"
        "configmaps.yaml"
        "persistent-volumes.yaml"
        "database.yaml"
        "redis.yaml"
    )
    
    # Apply infrastructure first
    for manifest in "${manifest_order[@]}"; do
        local staging_manifest="k8s/staging/$manifest"
        if [ -f "$staging_manifest" ]; then
            log INFO "Applying $manifest..."
            kubectl apply -f "$staging_manifest"
        fi
    done
    
    # Wait for database to be ready
    log INFO "Waiting for database to be ready..."
    kubectl wait --for=condition=ready pod -l app=acs-database --timeout=300s -n "$NAMESPACE" || {
        log WARN "Database did not become ready within timeout, continuing..."
    }
    
    # Apply application manifests
    local app_manifests=(
        "webapi.yaml"
        "tenants.yaml"
        "monitoring.yaml"
    )
    
    for manifest in "${app_manifests[@]}"; do
        local staging_manifest="k8s/staging/$manifest"
        if [ -f "$staging_manifest" ]; then
            log INFO "Applying $manifest..."
            kubectl apply -f "$staging_manifest"
        fi
    done
    
    log INFO "All manifests applied successfully"
}

# Wait for deployment
wait_for_deployment() {
    log INFO "Waiting for deployment to complete..."
    
    local deployments=("acs-webapi" "acs-verticalhost" "acs-dashboard")
    
    for deployment in "${deployments[@]}"; do
        log INFO "Waiting for $deployment to be ready..."
        kubectl wait --for=condition=available deployment/"$deployment" --timeout=600s -n "$NAMESPACE"
        
        if [ $? -eq 0 ]; then
            log INFO "$deployment is ready"
        else
            error_exit "$deployment failed to become ready within timeout"
        fi
    done
    
    log INFO "All deployments are ready"
}

# Run database migrations
run_migrations() {
    log INFO "Running database migrations..."
    
    # Create migration job
    cat <<EOF | kubectl apply -f -
apiVersion: batch/v1
kind: Job
metadata:
  name: acs-migration-$VERSION
  namespace: $NAMESPACE
spec:
  template:
    spec:
      containers:
      - name: migration
        image: $REGISTRY/acs-webapi:$VERSION
        command: ["dotnet", "ef", "database", "update"]
        env:
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: acs-secrets
              key: database-connection-string
        - name: ASPNETCORE_ENVIRONMENT
          value: "Staging"
      restartPolicy: Never
  backoffLimit: 3
EOF

    # Wait for migration to complete
    log INFO "Waiting for database migration to complete..."
    kubectl wait --for=condition=complete job/acs-migration-$VERSION --timeout=300s -n "$NAMESPACE"
    
    if [ $? -eq 0 ]; then
        log INFO "Database migrations completed successfully"
    else
        log WARN "Database migration failed or timed out"
        # Get logs for troubleshooting
        kubectl logs job/acs-migration-$VERSION -n "$NAMESPACE" || true
    fi
}

# Health checks
run_health_checks() {
    if [ "$SKIP_HEALTH_CHECK" = "true" ]; then
        log INFO "Skipping health checks as requested"
        return 0
    fi
    
    log INFO "Running health checks..."
    
    # Get service endpoint
    local service_ip=""
    local attempts=0
    local max_attempts=30
    
    while [ $attempts -lt $max_attempts ] && [ -z "$service_ip" ]; do
        service_ip=$(kubectl get service acs-webapi-service -n "$NAMESPACE" -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null || echo "")
        
        if [ -z "$service_ip" ]; then
            log INFO "Waiting for service IP... (attempt $((attempts + 1))/$max_attempts)"
            sleep 10
            ((attempts++))
        fi
    done
    
    if [ -z "$service_ip" ]; then
        # Try using port-forward instead
        log INFO "Service IP not available, using port-forward for health check..."
        
        kubectl port-forward service/acs-webapi-service 8080:80 -n "$NAMESPACE" &
        local port_forward_pid=$!
        
        sleep 5
        
        # Health check via localhost
        local health_url="http://localhost:8080/health"
        local health_check_passed=false
        
        for i in {1..10}; do
            if curl -f -s "$health_url" > /dev/null 2>&1; then
                health_check_passed=true
                log INFO "Health check passed (attempt $i)"
                break
            else
                log INFO "Health check failed, retrying... (attempt $i/10)"
                sleep 10
            fi
        done
        
        # Clean up port-forward
        kill $port_forward_pid 2>/dev/null || true
        
    else
        # Health check via service IP
        local health_url="http://$service_ip/health"
        local health_check_passed=false
        
        for i in {1..20}; do
            if curl -f -s "$health_url" > /dev/null 2>&1; then
                health_check_passed=true
                log INFO "Health check passed (attempt $i)"
                break
            else
                log INFO "Health check failed, retrying... (attempt $i/20)"
                sleep 15
            fi
        done
    fi
    
    if [ "$health_check_passed" = "true" ]; then
        log INFO "All health checks passed"
    else
        error_exit "Health checks failed"
    fi
}

# Post-deployment validation
post_deployment_validation() {
    log INFO "Running post-deployment validation..."
    
    # Check pod status
    local unhealthy_pods=$(kubectl get pods -n "$NAMESPACE" --field-selector=status.phase!=Running --no-headers 2>/dev/null | wc -l)
    
    if [ "$unhealthy_pods" -eq 0 ]; then
        log INFO "All pods are running successfully"
    else
        log WARN "Some pods are not in Running state:"
        kubectl get pods -n "$NAMESPACE" --field-selector=status.phase!=Running
    fi
    
    # Check deployment status
    local deployments=("acs-webapi" "acs-verticalhost" "acs-dashboard")
    for deployment in "${deployments[@]}"; do
        local status=$(kubectl get deployment "$deployment" -n "$NAMESPACE" -o jsonpath='{.status.readyReplicas}/{.status.replicas}' 2>/dev/null || echo "0/0")
        log INFO "$deployment status: $status"
    done
    
    # Clean up old jobs
    kubectl delete job -l app=acs-migration --field-selector status.successful=1 -n "$NAMESPACE" 2>/dev/null || true
    
    log INFO "Post-deployment validation completed"
}

# Cleanup function
cleanup() {
    log INFO "Cleaning up temporary files..."
    # Add any cleanup logic here
}

# Main execution
main() {
    log INFO "Starting ACS staging deployment"
    log INFO "Environment: $ENVIRONMENT"
    log INFO "Version: $VERSION"
    log INFO "Namespace: $NAMESPACE"
    
    # Set trap for cleanup
    trap cleanup EXIT
    
    # Execute deployment steps
    check_prerequisites
    build_and_test
    build_images
    update_manifests
    deploy_to_kubernetes
    wait_for_deployment
    run_migrations
    run_health_checks
    post_deployment_validation
    
    log INFO "Staging deployment completed successfully!"
    log INFO "Application version $VERSION is now running in staging environment"
    log INFO "Namespace: $NAMESPACE"
}

# Run main function
main "$@"