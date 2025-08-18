#!/bin/bash
# ACS Docker Deployment Script
# Deploys the entire ACS system using Docker Compose

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
ENVIRONMENT="Development"
RECREATE=false
BUILD_ONLY=false
DOWN_FIRST=false

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -e|--environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        --recreate)
            RECREATE=true
            shift
            ;;
        --build-only)
            BUILD_ONLY=true
            shift
            ;;
        --down)
            DOWN_FIRST=true
            shift
            ;;
        -h|--help)
            echo "ACS Docker Deployment Script"
            echo ""
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -e, --environment   Environment (Development|Staging|Production)"
            echo "  --recreate          Recreate containers (docker-compose up --force-recreate)"
            echo "  --build-only        Only build images, don't start services"
            echo "  --down              Stop existing containers first"
            echo "  -h, --help          Show this help"
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            exit 1
            ;;
    esac
done

echo -e "${GREEN}🐳 ACS Docker Deployment${NC}"
echo -e "${YELLOW}Environment: $ENVIRONMENT${NC}"
echo ""

# Check if Docker and Docker Compose are installed
if ! command -v docker &> /dev/null; then
    print_error "❌ Docker is not installed or not in PATH"
    exit 1
fi

if ! command -v docker-compose &> /dev/null && ! docker compose version &> /dev/null; then
    print_error "❌ Docker Compose is not installed or not in PATH"
    exit 1
fi

# Use docker compose or docker-compose based on what's available
COMPOSE_CMD="docker compose"
if ! docker compose version &> /dev/null; then
    COMPOSE_CMD="docker-compose"
fi

print_success "✅ Docker environment verified"

# Stop existing containers if requested
if [[ "$DOWN_FIRST" == true ]]; then
    print_step "🛑 Stopping existing containers..."
    $COMPOSE_CMD down --remove-orphans
    print_success "✅ Existing containers stopped"
fi

# Create required directories
print_step "📁 Creating required directories..."
mkdir -p logs keys

# Generate master encryption key if it doesn't exist
if [[ ! -f "keys/master.key" ]]; then
    print_step "🔐 Generating master encryption key..."
    openssl rand -hex 32 > keys/master.key
    print_success "✅ Master encryption key generated"
fi

# Create OpenTelemetry collector configuration
print_step "📊 Creating monitoring configuration..."
cat > otel-collector-config.yaml << EOF
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
      http:
        endpoint: 0.0.0.0:4318

processors:
  batch:

exporters:
  prometheus:
    endpoint: "0.0.0.0:8888"
  logging:
    loglevel: info

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [batch]
      exporters: [logging]
    metrics:
      receivers: [otlp]
      processors: [batch]
      exporters: [prometheus, logging]
    logs:
      receivers: [otlp]
      processors: [batch]
      exporters: [logging]
EOF

# Create Prometheus configuration
cat > prometheus.yml << EOF
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'acs-webapi'
    static_configs:
      - targets: ['webapi:5000']
    metrics_path: '/metrics'
    scrape_interval: 15s
    
  - job_name: 'acs-tenants'
    static_configs:
      - targets: ['tenant1:6000', 'tenant2:6002', 'tenant3:6004']
    metrics_path: '/metrics'
    scrape_interval: 15s
    
  - job_name: 'otel-collector'
    static_configs:
      - targets: ['otel-collector:8888']
    metrics_path: '/metrics'
    scrape_interval: 15s
EOF

print_success "✅ Monitoring configuration created"

# Build and start services
if [[ "$BUILD_ONLY" == true ]]; then
    print_step "🔨 Building Docker images..."
    $COMPOSE_CMD build --parallel
    print_success "✅ Docker images built successfully"
    exit 0
fi

print_step "🔨 Building and starting services..."

if [[ "$RECREATE" == true ]]; then
    $COMPOSE_CMD up --build --force-recreate -d
else
    $COMPOSE_CMD up --build -d
fi

print_success "✅ Services started successfully"

# Wait for services to be healthy
print_step "⏳ Waiting for services to become healthy..."
sleep 30

# Check service health
print_step "🔍 Checking service health..."

services=("sqlserver" "redis" "webapi" "dashboard" "tenant1" "tenant2" "tenant3")
healthy_services=0

for service in "${services[@]}"; do
    if $COMPOSE_CMD ps --format table | grep -q "$service.*healthy\|$service.*Up"; then
        print_success "✅ $service is healthy"
        healthy_services=$((healthy_services + 1))
    else
        print_warning "⚠️ $service is not healthy yet"
    fi
done

echo ""
print_step "📊 Deployment Summary"
echo "==================="
echo "Environment: $ENVIRONMENT"
echo "Healthy Services: $healthy_services/${#services[@]}"
echo ""
echo "🌐 Service URLs:"
echo "  - WebApi: https://localhost:5001 (http://localhost:5000)"
echo "  - Dashboard: https://localhost:5101 (http://localhost:5100)"
echo "  - Tenant 1: https://localhost:6001 (http://localhost:6000)"
echo "  - Tenant 2: https://localhost:6003 (http://localhost:6002)"
echo "  - Tenant 3: https://localhost:6005 (http://localhost:6004)"
echo ""
echo "📊 Monitoring URLs:"
echo "  - Prometheus: http://localhost:9090"
echo "  - Grafana: http://localhost:3000 (admin/admin123)"
echo "  - OpenTelemetry: http://localhost:8888"
echo ""
echo "🔧 Management Commands:"
echo "  - View logs: $COMPOSE_CMD logs -f [service_name]"
echo "  - Stop services: $COMPOSE_CMD down"
echo "  - Restart service: $COMPOSE_CMD restart [service_name]"
echo "  - Scale tenants: $COMPOSE_CMD up --scale tenant1=2 -d"
echo ""

if [[ $healthy_services -eq ${#services[@]} ]]; then
    print_success "🎉 All services are healthy! Deployment completed successfully."
elif [[ $healthy_services -gt 0 ]]; then
    print_warning "⚠️ Deployment completed with some issues. Check service logs for details."
    echo "Run: $COMPOSE_CMD logs [service_name] to see detailed logs"
else
    print_error "❌ Deployment failed. No services are healthy."
    echo "Run: $COMPOSE_CMD logs to see detailed logs"
    exit 1
fi