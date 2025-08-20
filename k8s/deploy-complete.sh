#!/bin/bash

# ACS Kubernetes Complete Deployment Script
# This script deploys the entire ACS system to Kubernetes with proper orchestration

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
NAMESPACE="acs-system"
DEPLOYMENT_VERSION="${ACS_VERSION:-1.0.0}"
REGISTRY="${ACS_REGISTRY:-localhost:5000}"
WAIT_TIMEOUT=600

# Logging function
log() {
    echo -e "${BLUE}[$(date +'%Y-%m-%d %H:%M:%S')]${NC} $1"
}

success() {
    echo -e "${GREEN}[$(date +'%Y-%m-%d %H:%M:%S')] ✓${NC} $1"
}

warning() {
    echo -e "${YELLOW}[$(date +'%Y-%m-%d %H:%M:%S')] ⚠${NC} $1"
}

error() {
    echo -e "${RED}[$(date +'%Y-%m-%d %H:%M:%S')] ✗${NC} $1"
}

# Check prerequisites
check_prerequisites() {
    log "Checking prerequisites..."
    
    if ! command -v kubectl &> /dev/null; then
        error "kubectl is not installed or not in PATH"
        exit 1
    fi
    
    if ! command -v kustomize &> /dev/null; then
        error "kustomize is not installed or not in PATH"
        exit 1
    fi
    
    # Check cluster connectivity
    if ! kubectl cluster-info &> /dev/null; then
        error "Cannot connect to Kubernetes cluster"
        exit 1
    fi
    
    success "Prerequisites check passed"
}

# Build and push Docker images
build_and_push_images() {
    log "Building and pushing Docker images..."
    
    # Build WebAPI image
    log "Building ACS WebAPI image..."
    docker build -t "${REGISTRY}/acs/webapi:${DEPLOYMENT_VERSION}" -f ../ACS.WebApi/Dockerfile ..
    docker push "${REGISTRY}/acs/webapi:${DEPLOYMENT_VERSION}"
    success "WebAPI image built and pushed"
    
    # Build Dashboard image
    log "Building ACS Dashboard image..."
    docker build -t "${REGISTRY}/acs/dashboard:${DEPLOYMENT_VERSION}" -f ../ACS.Dashboard/Dockerfile ..
    docker push "${REGISTRY}/acs/dashboard:${DEPLOYMENT_VERSION}"
    success "Dashboard image built and pushed"
    
    # Build VerticalHost image
    log "Building ACS VerticalHost image..."
    docker build -t "${REGISTRY}/acs/verticalhost:${DEPLOYMENT_VERSION}" -f ../ACS.VerticalHost/Dockerfile ..
    docker push "${REGISTRY}/acs/verticalhost:${DEPLOYMENT_VERSION}"
    success "VerticalHost image built and pushed"
}

# Wait for deployment to be ready
wait_for_deployment() {
    local deployment_name=$1
    local namespace=$2
    local timeout=${3:-300}
    
    log "Waiting for deployment ${deployment_name} to be ready..."
    
    if kubectl wait --for=condition=available --timeout="${timeout}s" deployment/"${deployment_name}" -n "${namespace}"; then
        success "Deployment ${deployment_name} is ready"
        return 0
    else
        error "Deployment ${deployment_name} failed to become ready within ${timeout} seconds"
        return 1
    fi
}

# Wait for StatefulSet to be ready
wait_for_statefulset() {
    local statefulset_name=$1
    local namespace=$2
    local timeout=${3:-300}
    
    log "Waiting for StatefulSet ${statefulset_name} to be ready..."
    
    if kubectl wait --for=condition=ready --timeout="${timeout}s" pod -l "app.kubernetes.io/name=acs,app.kubernetes.io/component=${statefulset_name}" -n "${namespace}"; then
        success "StatefulSet ${statefulset_name} is ready"
        return 0
    else
        error "StatefulSet ${statefulset_name} failed to become ready within ${timeout} seconds"
        return 1
    fi
}

# Deploy infrastructure components
deploy_infrastructure() {
    log "Deploying infrastructure components..."
    
    # Apply namespace, RBAC, and persistent volumes first
    kubectl apply -f namespace.yaml
    kubectl apply -f persistent-volumes.yaml
    success "Namespace and persistent volumes deployed"
    
    # Apply ConfigMaps and Secrets
    kubectl apply -f configmaps.yaml
    kubectl apply -f secrets.yaml
    success "ConfigMaps and Secrets deployed"
    
    # Deploy database
    log "Deploying SQL Server database..."
    kubectl apply -f database.yaml
    wait_for_deployment "sqlserver" "${NAMESPACE}" 300
    
    # Deploy Redis cache
    log "Deploying Redis cache..."
    kubectl apply -f redis.yaml
    wait_for_deployment "redis" "${NAMESPACE}" 180
    
    success "Infrastructure components deployed successfully"
}

# Deploy monitoring stack
deploy_monitoring() {
    log "Deploying monitoring stack..."
    
    kubectl apply -f monitoring.yaml
    
    # Wait for monitoring components
    wait_for_deployment "otel-collector" "${NAMESPACE}" 180
    wait_for_deployment "prometheus" "${NAMESPACE}" 180
    wait_for_deployment "grafana" "${NAMESPACE}" 180
    
    success "Monitoring stack deployed successfully"
}

# Deploy application services
deploy_applications() {
    log "Deploying application services..."
    
    # Deploy WebAPI
    log "Deploying ACS WebAPI..."
    kubectl apply -f webapi.yaml
    wait_for_deployment "acs-webapi" "${NAMESPACE}" 300
    
    # Deploy Dashboard
    log "Deploying ACS Dashboard..."
    kubectl apply -f dashboard.yaml
    wait_for_deployment "acs-dashboard" "${NAMESPACE}" 300
    
    # Deploy tenant services
    log "Deploying tenant services..."
    kubectl apply -f tenants.yaml
    wait_for_deployment "acs-tenant1" "${NAMESPACE}" 300
    wait_for_deployment "acs-tenant2" "${NAMESPACE}" 300
    wait_for_deployment "acs-tenant3" "${NAMESPACE}" 300
    
    success "Application services deployed successfully"
}

# Apply network policies
deploy_security() {
    log "Applying security policies..."
    
    kubectl apply -f network-policies.yaml
    
    success "Network policies applied successfully"
}

# Verify deployment health
verify_deployment() {
    log "Verifying deployment health..."
    
    # Check all pods are running
    log "Checking pod status..."
    kubectl get pods -n "${NAMESPACE}" -o wide
    
    # Check services
    log "Checking service status..."
    kubectl get services -n "${NAMESPACE}"
    
    # Check ingresses
    log "Checking ingress status..."
    kubectl get ingress -n "${NAMESPACE}"
    
    # Run health checks
    log "Running health checks..."
    
    # WebAPI health check
    if kubectl exec -n "${NAMESPACE}" deployment/acs-webapi -- curl -f http://localhost:5000/health > /dev/null 2>&1; then
        success "WebAPI health check passed"
    else
        warning "WebAPI health check failed"
    fi
    
    # Dashboard health check
    if kubectl exec -n "${NAMESPACE}" deployment/acs-dashboard -- curl -f http://localhost:8080/health > /dev/null 2>&1; then
        success "Dashboard health check passed"
    else
        warning "Dashboard health check failed"
    fi
    
    # Database connectivity check
    if kubectl exec -n "${NAMESPACE}" deployment/sqlserver -- /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$DB_PASSWORD" -C -Q "SELECT 1" > /dev/null 2>&1; then
        success "Database connectivity check passed"
    else
        warning "Database connectivity check failed"
    fi
    
    # Redis connectivity check
    if kubectl exec -n "${NAMESPACE}" deployment/redis -- redis-cli ping | grep -q PONG; then
        success "Redis connectivity check passed"
    else
        warning "Redis connectivity check failed"
    fi
}

# Display access information
display_access_info() {
    log "Deployment completed! Access information:"
    echo ""
    echo "🌐 Web Services:"
    echo "  • API Documentation: https://api.acs.example.com/api-docs"
    echo "  • Dashboard:         https://dashboard.acs.example.com"
    echo "  • Grafana:          https://grafana.acs.example.com (admin/admin123)"
    echo "  • Prometheus:       https://prometheus.acs.example.com"
    echo ""
    echo "🔧 Management Commands:"
    echo "  • View pods:        kubectl get pods -n ${NAMESPACE}"
    echo "  • View logs:        kubectl logs -f deployment/acs-webapi -n ${NAMESPACE}"
    echo "  • Port forward API: kubectl port-forward svc/acs-webapi 5000:5000 -n ${NAMESPACE}"
    echo "  • Scale WebAPI:     kubectl scale deployment acs-webapi --replicas=3 -n ${NAMESPACE}"
    echo ""
    echo "📊 Monitoring:"
    echo "  • Metrics endpoint: http://localhost:5000/metrics (after port-forward)"
    echo "  • Health endpoint:  http://localhost:5000/health (after port-forward)"
    echo ""
}

# Cleanup function
cleanup_on_error() {
    error "Deployment failed. Cleaning up..."
    kubectl delete namespace "${NAMESPACE}" --ignore-not-found=true
}

# Main deployment function
main() {
    log "Starting ACS Kubernetes deployment (version: ${DEPLOYMENT_VERSION})"
    
    # Set error trap
    trap cleanup_on_error ERR
    
    # Run deployment steps
    check_prerequisites
    
    if [ "${SKIP_BUILD:-false}" != "true" ]; then
        build_and_push_images
    else
        warning "Skipping image build (SKIP_BUILD=true)"
    fi
    
    deploy_infrastructure
    deploy_monitoring
    deploy_applications
    deploy_security
    verify_deployment
    display_access_info
    
    success "ACS deployment completed successfully! 🎉"
}

# Help function
show_help() {
    echo "ACS Kubernetes Deployment Script"
    echo ""
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  -h, --help           Show this help message"
    echo "  -n, --namespace      Kubernetes namespace (default: acs-system)"
    echo "  -v, --version        Deployment version (default: 1.0.0)"
    echo "  -r, --registry       Docker registry (default: localhost:5000)"
    echo "  --skip-build         Skip Docker image building"
    echo "  --verify-only        Only run verification checks"
    echo ""
    echo "Environment Variables:"
    echo "  ACS_VERSION          Deployment version"
    echo "  ACS_REGISTRY         Docker registry URL"
    echo "  SKIP_BUILD           Skip building images (true/false)"
    echo ""
    echo "Examples:"
    echo "  $0                                    # Deploy with defaults"
    echo "  $0 -v 1.1.0 -r my-registry.com      # Deploy specific version"
    echo "  SKIP_BUILD=true $0                   # Deploy without building"
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -h|--help)
            show_help
            exit 0
            ;;
        -n|--namespace)
            NAMESPACE="$2"
            shift 2
            ;;
        -v|--version)
            DEPLOYMENT_VERSION="$2"
            shift 2
            ;;
        -r|--registry)
            REGISTRY="$2"
            shift 2
            ;;
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        --verify-only)
            verify_deployment
            exit 0
            ;;
        *)
            error "Unknown option: $1"
            show_help
            exit 1
            ;;
    esac
done

# Run main deployment
main