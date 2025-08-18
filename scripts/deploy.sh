#!/bin/bash
# ACS Vertical Architecture Deployment Script (Linux/macOS)
# This script deploys the entire ACS system with all tenant processes

set -e  # Exit on any error

# Default values
ENVIRONMENT=""
CONNECTION_STRING=""
REDIS_CONNECTION_STRING=""
WEBAPI_PORT=5000
DASHBOARD_PORT=5001
TENANT_IDS=("tenant1" "tenant2" "tenant3")
RECREATE_DATABASE=false
SKIP_TESTS=false

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Function to print colored output
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

# Function to show usage
show_usage() {
    cat << EOF
ACS Vertical Architecture Deployment Script

Usage: $0 -e ENVIRONMENT [OPTIONS]

Required Arguments:
  -e, --environment    Environment (Development|Staging|Production)

Optional Arguments:
  -c, --connection     Database connection string
  -r, --redis          Redis connection string
  -w, --webapi-port    WebApi port (default: 5000)
  -d, --dashboard-port Dashboard port (default: 5001)
  -t, --tenants        Comma-separated tenant IDs (default: tenant1,tenant2,tenant3)
  --recreate-db        Recreate database (drops existing)
  --skip-tests         Skip running tests
  -h, --help           Show this help message

Examples:
  $0 -e Development
  $0 -e Production -c "Server=localhost;Database=ACS;Trusted_Connection=true" -r "localhost:6379"
  $0 -e Staging --skip-tests --recreate-db

EOF
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -e|--environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        -c|--connection)
            CONNECTION_STRING="$2"
            shift 2
            ;;
        -r|--redis)
            REDIS_CONNECTION_STRING="$2"
            shift 2
            ;;
        -w|--webapi-port)
            WEBAPI_PORT="$2"
            shift 2
            ;;
        -d|--dashboard-port)
            DASHBOARD_PORT="$2"
            shift 2
            ;;
        -t|--tenants)
            IFS=',' read -ra TENANT_IDS <<< "$2"
            shift 2
            ;;
        --recreate-db)
            RECREATE_DATABASE=true
            shift
            ;;
        --skip-tests)
            SKIP_TESTS=true
            shift
            ;;
        -h|--help)
            show_usage
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            show_usage
            exit 1
            ;;
    esac
done

# Validate required arguments
if [[ -z "$ENVIRONMENT" ]]; then
    print_error "Environment is required. Use -e or --environment"
    show_usage
    exit 1
fi

if [[ ! "$ENVIRONMENT" =~ ^(Development|Staging|Production)$ ]]; then
    print_error "Invalid environment. Must be Development, Staging, or Production"
    exit 1
fi

# Set script directory and solution root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOLUTION_ROOT="$(dirname "$SCRIPT_DIR")"
cd "$SOLUTION_ROOT"

echo -e "${GREEN}🚀 ACS Vertical Architecture Deployment${NC}"
echo -e "${YELLOW}Environment: $ENVIRONMENT${NC}"
echo -e "${YELLOW}Tenant Processes: ${TENANT_IDS[*]}${NC}"
echo ""

# Trap to cleanup on exit
cleanup() {
    echo ""
    print_warning "🧹 Cleaning up background processes..."
    pkill -f "dotnet.*ACS" 2>/dev/null || true
    exit
}
trap cleanup EXIT INT TERM

# Step 1: Clean and Build Solution
print_step "📦 Step 1: Building solution..."
dotnet clean --verbosity quiet
dotnet build --configuration Release --no-restore
print_success "✅ Build completed successfully"

# Step 2: Run Tests (unless skipped)
if [[ "$SKIP_TESTS" != true ]]; then
    print_step "🧪 Step 2: Running tests..."
    if dotnet test --configuration Release --no-build --verbosity minimal; then
        print_success "✅ All tests passed"
    else
        print_warning "⚠️ Some tests failed, but continuing deployment..."
    fi
else
    print_warning "⚠️ Step 2: Skipping tests as requested"
fi

# Step 3: Database Deployment
if [[ -n "$CONNECTION_STRING" ]]; then
    print_step "🗄️ Step 3: Deploying database..."
    
    # Create temporary C# script for database deployment
    TEMP_SCRIPT=$(mktemp).cs
    cat > "$TEMP_SCRIPT" << EOF
using ACS.Database;

try {
    await DatabaseDeployer.DeployAsync("$CONNECTION_STRING", $RECREATE_DATABASE);
    Console.WriteLine("✅ Database deployment completed successfully");
} catch (Exception ex) {
    Console.WriteLine(\$"❌ Database deployment failed: {ex.Message}");
    Environment.Exit(1);
}
EOF
    
    # Execute database deployment
    dotnet run --project ACS.Database -- --script "$TEMP_SCRIPT"
    rm -f "$TEMP_SCRIPT"
    print_success "✅ Database deployment completed"
else
    print_warning "⚠️ Step 3: Skipping database deployment (no connection string)"
fi

# Step 4: Create Configuration Files
print_step "⚙️ Step 4: Creating configuration files..."

# Generate JWT secret
JWT_SECRET=$(openssl rand -base64 64 | tr -d "\\n")

# Create appsettings for WebApi
cat > "ACS.WebApi/appsettings.$ENVIRONMENT.json" << EOF
{
  "Logging": {
    "LogLevel": {
      "Default": "$([ "$ENVIRONMENT" = "Production" ] && echo "Warning" || echo "Information")",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "$CONNECTION_STRING",
    "Redis": "$REDIS_CONNECTION_STRING"
  },
  "JwtSettings": {
    "SecretKey": "$JWT_SECRET",
    "Issuer": "ACS-$ENVIRONMENT",
    "Audience": "ACS-API",
    "ExpiryMinutes": $([ "$ENVIRONMENT" = "Production" ] && echo "60" || echo "1440")
  },
  "TenantProcesses": {
    "BasePort": 6000,
    "ProcessTimeout": 30000,
    "MaxRetries": 3
  },
  "RateLimit": {
    "Enabled": true,
    "DefaultPolicy": {
      "RequestLimit": $([ "$ENVIRONMENT" = "Production" ] && echo "100" || echo "1000"),
      "WindowSizeSeconds": 60
    }
  },
  "OpenTelemetry": {
    "ServiceName": "ACS-WebApi",
    "Endpoint": "http://localhost:4317"
  },
  "Encryption": {
    "MasterKeyPath": "keys/master.key",
    "KeyRotationDays": 90
  }
}
EOF

print_success "✅ WebApi configuration created"

# Step 5: Start Services
print_step "🎯 Step 5: Starting services..."

# Create PID file directory
mkdir -p "$SOLUTION_ROOT/pids"

# Start WebApi
echo "Starting WebApi on port $WEBAPI_PORT..."
export ASPNETCORE_ENVIRONMENT="$ENVIRONMENT"
export ASPNETCORE_URLS="https://localhost:$WEBAPI_PORT;http://localhost:$((WEBAPI_PORT + 1))"
nohup dotnet run --project ACS.WebApi --configuration Release > logs/webapi.log 2>&1 &
echo $! > "$SOLUTION_ROOT/pids/webapi.pid"

# Start Dashboard
echo "Starting Dashboard on port $DASHBOARD_PORT..."
export ASPNETCORE_URLS="https://localhost:$DASHBOARD_PORT;http://localhost:$((DASHBOARD_PORT + 1))"
nohup dotnet run --project ACS.Dashboard --configuration Release > logs/dashboard.log 2>&1 &
echo $! > "$SOLUTION_ROOT/pids/dashboard.pid"

# Start Tenant Processes
TENANT_PORT=6000
for TENANT_ID in "${TENANT_IDS[@]}"; do
    echo "Starting tenant process for $TENANT_ID on port $TENANT_PORT..."
    export TENANT_ID="$TENANT_ID"
    export ASPNETCORE_URLS="https://localhost:$TENANT_PORT;http://localhost:$((TENANT_PORT + 1))"
    nohup dotnet run --project ACS.VerticalHost --configuration Release > "logs/tenant-$TENANT_ID.log" 2>&1 &
    echo $! > "$SOLUTION_ROOT/pids/tenant-$TENANT_ID.pid"
    TENANT_PORT=$((TENANT_PORT + 2))
done

# Create logs directory
mkdir -p logs

# Wait for services to start
print_warning "⏳ Waiting for services to initialize..."
sleep 10

# Step 6: Health Check
print_step "🔍 Step 6: Performing health checks..."

WEBAPI_URL="https://localhost:$WEBAPI_PORT"
DASHBOARD_URL="https://localhost:$DASHBOARD_PORT"

# Health check function
check_health() {
    local url=$1
    local service=$2
    if curl -k -f "$url/health" > /dev/null 2>&1; then
        print_success "✅ $service health check passed"
        return 0
    else
        print_warning "⚠️ $service health check failed"
        return 1
    fi
}

check_health "$WEBAPI_URL" "WebApi"
check_health "$DASHBOARD_URL" "Dashboard"

# Display deployment summary
echo ""
print_success "🎉 Deployment Summary"
print_success "==================="
echo -e "${NC}Environment: $ENVIRONMENT"
echo -e "${NC}WebApi URL: https://localhost:$WEBAPI_PORT"
echo -e "${NC}Dashboard URL: https://localhost:$DASHBOARD_PORT"
echo ""
echo -e "${NC}Tenant Processes:"
TENANT_PORT=6000
for TENANT_ID in "${TENANT_IDS[@]}"; do
    echo -e "${NC}  - $TENANT_ID: https://localhost:$TENANT_PORT"
    TENANT_PORT=$((TENANT_PORT + 2))
done

echo ""
print_warning "🔧 Management Commands:"
echo -e "${NC}  Stop all services: ./Scripts/stop.sh"
echo -e "${NC}  View logs: tail -f logs/*.log"
echo -e "${NC}  Monitor processes: ./Scripts/monitor.sh"
echo ""

print_success "✅ ACS Vertical Architecture deployment completed successfully!"

# Don't cleanup automatically for successful deployment
trap - EXIT