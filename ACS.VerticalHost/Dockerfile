# ==============================================================================
# Multi-stage Dockerfile for ACS.VerticalHost (Business Logic Layer)
# Production-optimized for tenant-specific gRPC services
# ==============================================================================

# Stage 1: Base runtime image (Alpine for security and performance)
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
RUN apk update && apk upgrade && apk add --no-cache curl

# Create non-root user for security
RUN addgroup -g 1001 -S appgroup && \
    adduser -u 1001 -S appuser -G appgroup

WORKDIR /app
EXPOSE 50051

# Configure for gRPC service
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP2_LIMITS__MAX_REQUEST_HEADER_SIZE=32768
ENV ASPNETCORE_KESTREL__LIMITS__MAX_REQUEST_HEADER_SIZE=32768
ENV GRPC_PORT=50051

# Stage 2: SDK image for building (Alpine for consistency)
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy solution and project files for optimal layer caching
COPY ["ACS.sln", "."]
COPY ["ACS.VerticalHost/ACS.VerticalHost.csproj", "ACS.VerticalHost/"]
COPY ["ACS.Core/ACS.Core.csproj", "ACS.Core/"]
COPY ["ACS.Service/ACS.Service.csproj", "ACS.Service/"]
COPY ["ACS.Infrastructure/ACS.Infrastructure.csproj", "ACS.Infrastructure/"]

# Restore dependencies with optimizations for VerticalHost
RUN dotnet restore "ACS.VerticalHost/ACS.VerticalHost.csproj" --disable-parallel

# Copy source code
COPY . .
WORKDIR "/src/ACS.VerticalHost"

# Build with optimizations for gRPC performance
RUN dotnet build "ACS.VerticalHost.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build \
    --no-restore

# Stage 3: Publish with production optimizations
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ACS.VerticalHost.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:PublishTrimmed=false \
    /p:PublishReadyToRun=true \
    --no-restore

# Stage 4: Final runtime image
FROM base AS final
WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Create runtime directories with proper structure for tenant isolation
RUN mkdir -p /app/keys /app/logs /app/data /app/temp /app/tenant-data && \
    chown -R appuser:appgroup /app
USER appuser

# Health check for gRPC service
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:50051/health || exit 1

# Metadata labels for VerticalHost
LABEL maintainer="ACS Team"
LABEL description="ACS VerticalHost - Business Logic Layer with Command Buffer"
LABEL version="1.0.0"
LABEL component="verticalhost"
LABEL tier="business"
LABEL architecture="tenant-specific"
LABEL protocol="grpc"

# Support for tenant-specific configuration via environment variables
ENV TENANT_ID=""
ENV BASE_CONNECTION_STRING=""

ENTRYPOINT ["dotnet", "ACS.VerticalHost.dll"]