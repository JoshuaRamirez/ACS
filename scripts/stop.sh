#!/bin/bash
# ACS Vertical Architecture Stop Script
# Gracefully stops all ACS services

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

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

print_warning "🛑 Stopping ACS Vertical Architecture services..."
echo ""

# Stop services gracefully using PID files
PIDS_DIR="$SOLUTION_ROOT/pids"

if [[ -d "$PIDS_DIR" ]]; then
    for pid_file in "$PIDS_DIR"/*.pid; do
        if [[ -f "$pid_file" ]]; then
            service_name=$(basename "$pid_file" .pid)
            pid=$(cat "$pid_file")
            
            if kill -0 "$pid" 2>/dev/null; then
                echo "Stopping $service_name (PID: $pid)..."
                kill "$pid"
                
                # Wait for graceful shutdown
                timeout=10
                while kill -0 "$pid" 2>/dev/null && [[ $timeout -gt 0 ]]; do
                    sleep 1
                    timeout=$((timeout - 1))
                done
                
                # Force kill if still running
                if kill -0 "$pid" 2>/dev/null; then
                    print_warning "Force killing $service_name..."
                    kill -9 "$pid" 2>/dev/null || true
                fi
                
                print_success "✅ Stopped $service_name"
            else
                print_warning "⚠️ $service_name was not running"
            fi
            
            rm -f "$pid_file"
        fi
    done
    
    rmdir "$PIDS_DIR" 2>/dev/null || true
else
    print_warning "No PID files found. Trying alternative methods..."
    
    # Fallback: kill all dotnet processes with ACS in the command
    if pgrep -f "dotnet.*ACS" > /dev/null; then
        print_warning "Killing all ACS dotnet processes..."
        pkill -f "dotnet.*ACS"
        sleep 2
        
        # Force kill if still running
        if pgrep -f "dotnet.*ACS" > /dev/null; then
            pkill -9 -f "dotnet.*ACS"
        fi
        
        print_success "✅ Stopped ACS processes"
    else
        print_warning "⚠️ No ACS processes were running"
    fi
fi

# Clean up any remaining processes on known ports
ports=(5000 5001 5002 6000 6001 6002 6003 6004 6005)
for port in "${ports[@]}"; do
    pid=$(lsof -ti:$port 2>/dev/null || true)
    if [[ -n "$pid" ]]; then
        print_warning "Killing process on port $port (PID: $pid)..."
        kill "$pid" 2>/dev/null || true
        sleep 1
        # Force kill if needed
        kill -9 "$pid" 2>/dev/null || true
    fi
done

print_success "✅ All ACS services have been stopped"

# Display status
echo ""
print_warning "🔍 Verifying shutdown..."

if pgrep -f "dotnet.*ACS" > /dev/null; then
    print_error "❌ Some ACS processes may still be running:"
    pgrep -f -l "dotnet.*ACS"
else
    print_success "✅ No ACS processes detected"
fi

echo ""
print_success "🎉 ACS Vertical Architecture shutdown completed"