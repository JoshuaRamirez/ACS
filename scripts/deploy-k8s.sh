#!/bin/bash
# ACS Kubernetes Deployment Script
# Deploys the entire ACS system to Kubernetes

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

print_step() {
    echo -e "${CYAN}$1${NC}"
}

print_success() {
    echo -e "${GREEN}$1${NC}"
}

print_warning() {
    echo -e "${YELLOW}$1${NC}"
}

print_error() {
    echo -e "${RED}$1${NC}"
}

# Set script directory and solution root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOLUTION_ROOT="$(dirname "$SCRIPT_DIR")"
cd "$SOLUTION_ROOT"

# Default values
NAMESPACE="acs-system"
DRY_RUN=false
BUILD_IMAGES=true
WAIT_FOR_READY=true
SKIP_SECRETS=false

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -n|--namespace)
            NAMESPACE="$2"
            shift 2
            ;;
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --skip-build)
            BUILD_IMAGES=false
            shift
            ;;
        --no-wait)
            WAIT_FOR_READY=false
            shift
            ;;
        --skip-secrets)
            SKIP_SECRETS=true
            shift
            ;;
        -h|--help)
            echo "ACS Kubernetes Deployment Script"
            echo ""
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -n, --namespace   Kubernetes namespace (default: acs-system)"
            echo "  --dry-run         Show what would be deployed without applying"
            echo "  --skip-build      Skip building Docker images"
            echo "  --no-wait         Don't wait for pods to be ready"
            echo "  --skip-secrets    Skip deploying secrets (assume they exist)"
            echo "  -h, --help        Show this help"
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            exit 1
            ;;
    esac
done

echo -e "${GREEN}☸️ ACS Kubernetes Deployment${NC}"
echo -e "${YELLOW}Namespace: $NAMESPACE${NC}"
echo -e "${YELLOW}Dry Run: $DRY_RUN${NC}"
echo ""

# Check prerequisites
print_step "🔍 Checking prerequisites..."

if ! command -v kubectl &> /dev/null; then
    print_error "❌ kubectl is not installed or not in PATH"
    exit 1
fi

if ! command -v docker &> /dev/null; then
    print_error "❌ Docker is not installed or not in PATH"
    exit 1
fi

# Check if connected to cluster
if ! kubectl cluster-info &> /dev/null; then
    print_error "❌ Not connected to a Kubernetes cluster"
    exit 1
fi

CURRENT_CONTEXT=$(kubectl config current-context)
print_success "✅ Connected to cluster: $CURRENT_CONTEXT"

# Build Docker images if requested
if [[ "$BUILD_IMAGES" == true ]]; then
    print_step "🐳 Building Docker images..."
    
    # Build WebApi image
    print_warning "Building ACS WebApi image..."
    docker build -t acs/webapi:latest -f ACS.WebApi/Dockerfile .
    
    # Build VerticalHost image  
    print_warning "Building ACS VerticalHost image..."
    docker build -t acs/verticalhost:latest -f ACS.VerticalHost/Dockerfile .
    
    # Build Dashboard image
    print_warning "Building ACS Dashboard image..."
    docker build -t acs/dashboard:latest -f ACS.Dashboard/Dockerfile .
    
    print_success "✅ Docker images built successfully"
fi

# Function to apply manifest
apply_manifest() {
    local manifest=$1
    local description=$2
    
    if [[ "$DRY_RUN" == true ]]; then
        echo "Would apply: $manifest ($description)"
        kubectl apply --dry-run=client -f "$manifest"
    else
        echo "Applying: $manifest ($description)"
        kubectl apply -f "$manifest"
    fi
}

# Deploy Kubernetes manifests in order
print_step "📦 Deploying Kubernetes manifests..."

# 1. Namespace and RBAC
apply_manifest "k8s/namespace.yaml" "Namespace and resource limits"

# 2. Persistent Volumes
apply_manifest "k8s/persistent-volumes.yaml" "Persistent Volume Claims"

# 3. ConfigMaps
apply_manifest "k8s/configmaps.yaml" "Configuration"

# 4. Secrets (only if not skipping)
if [[ "$SKIP_SECRETS" != true ]]; then
    apply_manifest "k8s/secrets.yaml" "Secrets"
else
    print_warning "⚠️ Skipping secrets deployment"
fi

# 5. Database
apply_manifest "k8s/database.yaml" "SQL Server Database"

# 6. Redis Cache
apply_manifest "k8s/redis.yaml" "Redis Cache"

# 7. WebApi
apply_manifest "k8s/webapi.yaml" "WebApi Service"

# 8. Tenant Processes
apply_manifest "k8s/tenants.yaml" "Tenant Processes"

# 9. Monitoring Stack
apply_manifest "k8s/monitoring.yaml" "Monitoring Stack"

if [[ "$DRY_RUN" == true ]]; then
    print_success "✅ Dry run completed successfully"
    exit 0
fi

print_success "✅ All manifests applied successfully"

# Wait for pods to be ready if requested
if [[ "$WAIT_FOR_READY" == true ]]; then
    print_step "⏳ Waiting for pods to be ready..."
    
    # Wait for database first
    print_warning "Waiting for SQL Server..."
    kubectl wait --for=condition=ready pod -l app.kubernetes.io/component=database -n "$NAMESPACE" --timeout=300s
    
    # Wait for Redis
    print_warning "Waiting for Redis..."
    kubectl wait --for=condition=ready pod -l app.kubernetes.io/component=cache -n "$NAMESPACE" --timeout=120s
    
    # Wait for WebApi
    print_warning "Waiting for WebApi..."
    kubectl wait --for=condition=ready pod -l app.kubernetes.io/component=webapi -n "$NAMESPACE" --timeout=180s
    
    # Wait for tenant processes
    print_warning "Waiting for tenant processes..."
    kubectl wait --for=condition=ready pod -l app.kubernetes.io/component=tenant -n "$NAMESPACE" --timeout=180s
    
    print_success "✅ All pods are ready"
fi

# Get deployment status
print_step "📊 Deployment Status"
echo "=================="

kubectl get pods -n "$NAMESPACE" -o wide
echo ""

# Get service information
print_step "🌐 Service Information"
echo "====================="

kubectl get services -n "$NAMESPACE"
echo ""

# Get ingress information if available
if kubectl get ingress -n "$NAMESPACE" &> /dev/null; then
    print_step "🔗 Ingress Information"
    echo "====================="
    kubectl get ingress -n "$NAMESPACE"
    echo ""
fi

# Show useful commands
print_step "🔧 Useful Commands"
echo "=================="
echo "View pod logs:           kubectl logs -f <pod-name> -n $NAMESPACE"
echo "Get pod details:         kubectl describe pod <pod-name> -n $NAMESPACE"
echo "Port forward WebApi:     kubectl port-forward svc/acs-webapi 5000:5000 -n $NAMESPACE"
echo "Port forward Grafana:    kubectl port-forward svc/grafana 3000:3000 -n $NAMESPACE"
echo "Scale tenant:            kubectl scale deployment acs-tenant1 --replicas=2 -n $NAMESPACE"
echo "Delete deployment:       kubectl delete namespace $NAMESPACE"
echo ""

# Check if services are accessible
print_step "🔍 Health Check"
echo "==============="

# Port forward temporarily for health checks
kubectl port-forward svc/acs-webapi 8080:5000 -n "$NAMESPACE" &
PF_PID=$!
sleep 5

if curl -f http://localhost:8080/health &> /dev/null; then
    print_success "✅ WebApi health check passed"
else
    print_warning "⚠️ WebApi health check failed - service may still be starting"
fi

# Clean up port forward
kill $PF_PID 2>/dev/null || true

print_success "🎉 ACS Kubernetes deployment completed successfully!"
print_warning "📝 Remember to update DNS records for ingress hostnames"
print_warning "🔐 Update secrets with production values before going live"