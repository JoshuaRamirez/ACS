# ACS - Enterprise Access Control System

ACS is an enterprise-grade .NET 8 access control system implementing **Vertical Slice Architecture** with **CQRS patterns**, **Command Buffer processing**, and **Multi-tenant capabilities**. Built for high-performance, scalable access control and authorization management.

## 🏗️ Architecture Overview

```
┌─────────────────┐    gRPC     ┌─────────────────┐    Sequential  ┌─────────────────┐
│   HTTP API      │ ────────→   │ VerticalHost    │ ────────────→  │ Command Buffer  │
│ (ACS.WebApi)    │             │ (CQRS Handlers)│               │ (LMAX Pattern)  │
└─────────────────┘             └─────────────────┘               └─────────────────┘
                                                                           │
┌─────────────────┐    EF Core  ┌─────────────────┐    Service    ┌───────▼─────────┐
│   SQL Server    │ ◄────────   │ Business Logic  │ ◄──────────   │ Auto-Registered │
│ (Multi-tenant)  │             │ (ACS.Service)   │               │ Handlers (67+)  │
└─────────────────┘             └─────────────────┘               └─────────────────┘
```

### Key Features

- **🎯 Vertical Slice Architecture**: Feature-complete slices from API to database
- **⚡ CQRS with Command Buffer**: Sequential command processing with high throughput
- **🔄 Auto-Handler Registration**: 67+ handlers automatically discovered and registered
- **🏢 Multi-Tenant**: Process-level isolation with tenant-specific databases
- **📊 In-Memory Entity Graph**: High-performance permission evaluation and caching
- **🔐 Enterprise Security**: JWT authentication, field-level encryption, GDPR compliance
- **📈 Full Observability**: OpenTelemetry integration with distributed tracing

## 🚀 Quick Start

### Prerequisites

- .NET 8.0 SDK
- SQL Server (LocalDB included)
- Docker (optional, for containerized development)

### Development Setup

```bash
# Clone and build
git clone <repository-url>
cd ACS
dotnet restore
dotnet build

# Start VerticalHost (business logic layer)
cd ACS.VerticalHost
dotnet run --tenant development --port 50051

# Start HTTP API (new terminal)  
cd ACS.WebApi
dotnet run

# Verify everything works
curl http://localhost:5000/health
curl http://localhost:50051/health
```

### Docker Development

```bash
# Start full development environment
docker-compose up -d

# View logs
docker-compose logs -f

# Scale for load testing
docker-compose up -d --scale verticalhost=3
```

## 📚 Comprehensive Documentation

| Document | Description |
|----------|-------------|
| **[ARCHITECTURE.md](ARCHITECTURE.md)** | Complete architectural guide with vertical slice patterns, CQRS implementation, and performance characteristics |
| **[HANDLERS.md](HANDLERS.md)** | Handler development guide with auto-registration system and CQRS patterns |
| **[API.md](API.md)** | Service layer API documentation with caching strategies and performance patterns |
| **[OPERATIONS.md](OPERATIONS.md)** | Deployment and operations guide for Docker, Kubernetes, and production environments |
| **[GETTING_STARTED.md](GETTING_STARTED.md)** | Developer onboarding with step-by-step feature creation tutorial |

## 🏢 Enterprise Features

### Multi-Tenant Architecture
- **Process Isolation**: Each tenant runs in separate VerticalHost process
- **Database Isolation**: Tenant-specific connection strings and schemas
- **Resource Isolation**: Independent scaling and fault tolerance
- **Configuration Management**: Tenant-specific settings and policies

### Performance & Scalability
- **Command Buffer**: LMAX Disruptor-inspired sequential processing
- **Connection Pooling**: 128 connections per tenant with advanced optimization
- **In-Memory Caching**: Entity graph with sub-millisecond lookups
- **Batch Processing**: Optimized bulk operations for enterprise workloads

### Security & Compliance
- **JWT Authentication**: Industry-standard token-based security
- **Field-Level Encryption**: Automatic encryption of sensitive data
- **Audit Logging**: Complete audit trail for compliance requirements
- **GDPR Support**: Data privacy and right-to-be-forgotten capabilities

## 🔧 Development Commands

```bash
# Build and test
dotnet build
dotnet test
dotnet test --verbosity normal

# Run specific projects
dotnet run --project ACS.WebApi
dotnet run --project ACS.VerticalHost --tenant tenant1 --port 50051

# Database operations
dotnet ef database update --project ACS.Service
dotnet ef migrations add NewMigration --project ACS.Service

# Performance testing
dotnet run --project TestRunner -- --load-test --users 1000
```

## 🐳 Container Support

### Development
```bash
# Quick development environment
docker-compose up -d webapi verticalhost sqlserver

# With monitoring stack  
docker-compose --profile monitoring up -d
```

### Production
```bash
# Multi-tenant production deployment
docker-compose -f docker-compose.prod.yml up -d

# Kubernetes deployment
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/
```

## 📊 Monitoring & Health Checks

### Health Endpoints
```bash
# Overall system health
curl http://localhost:5000/health

# Detailed health with metrics
curl http://localhost:50051/health | jq

# Command buffer statistics
curl http://localhost:50051/health | jq '.entries.command_buffer'
```

### Key Metrics
- **Command Processing Rate**: Commands/queries per second
- **Buffer Utilization**: Channel capacity usage
- **Database Connection Pool**: Active/idle connection ratios
- **Entity Graph Memory**: In-memory cache usage
- **Response Times**: P50, P95, P99 latencies

## 🧪 Testing Strategy

### Test Categories
- **Unit Tests**: Handler and service layer testing
- **Integration Tests**: Full request pipeline testing
- **Performance Tests**: Load testing with realistic scenarios
- **Chaos Tests**: Failure mode and recovery testing

```bash
# Run all tests
dotnet test

# Run specific test categories
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
dotnet test --filter Category=Performance
```

## 📈 Performance Characteristics

| Metric | Development | Production |
|--------|-------------|------------|
| **Command Throughput** | 1,000-2,000/sec | 5,000-10,000/sec |
| **Query Response Time** | <10ms (P95) | <5ms (P95) |
| **Memory Usage** | 500MB-1GB | 1GB-4GB |
| **Database Connections** | 32 per tenant | 128 per tenant |
| **Concurrent Users** | 100-500 | 1,000-10,000 |

## 🔄 Contributing

1. **Read the Documentation**: Start with [GETTING_STARTED.md](GETTING_STARTED.md)
2. **Follow Patterns**: Use existing handler and service patterns
3. **Write Tests**: Unit tests for handlers, integration tests for features
4. **Auto-Registration**: Place handlers in `ACS.VerticalHost.Handlers` namespace
5. **Performance**: Consider command buffer impact and caching strategies

## 🚦 Project Status

- ✅ **Architecture**: Vertical slice with CQRS patterns established
- ✅ **Auto-Registration**: 67+ handlers automatically discovered
- ✅ **Multi-Tenant**: Process and database isolation implemented  
- ✅ **Performance**: Command buffer and entity graph optimized
- ✅ **Security**: JWT, encryption, and audit logging complete
- ✅ **Monitoring**: OpenTelemetry and health checks integrated
- ✅ **Documentation**: Comprehensive enterprise documentation complete

## 📞 Support

For development setup issues, run the setup script:
```bash
scripts/setup_dotnet.sh
```

For architecture questions, see [ARCHITECTURE.md](ARCHITECTURE.md).
For deployment guidance, see [OPERATIONS.md](OPERATIONS.md).

---

**ACS** - Built for enterprise-grade access control with modern .NET 8 patterns and performance optimization.