# ==============================================================================
# Multi-stage Dockerfile for ACS.WebApi (HTTP Proxy Layer)
# Production-optimized with security best practices
# ==============================================================================

# Stage 1: Base runtime image (Alpine for smaller size and security)
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
RUN apk update && apk upgrade && apk add --no-cache curl

# Create non-root user for security
RUN addgroup -g 1001 -S appgroup && \
    adduser -u 1001 -S appuser -G appgroup

WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Configure ASP.NET Core for containerized deployment
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_HTTPS_PORTS=8081
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

# Stage 2: SDK image for building (Alpine for consistency and security)
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy solution and project files first for optimal Docker layer caching
COPY ["ACS.sln", "."]
COPY ["ACS.WebApi/ACS.WebApi.csproj", "ACS.WebApi/"]
COPY ["ACS.Core/ACS.Core.csproj", "ACS.Core/"]
COPY ["ACS.Service/ACS.Service.csproj", "ACS.Service/"]
COPY ["ACS.Infrastructure/ACS.Infrastructure.csproj", "ACS.Infrastructure/"]
COPY ["ACS.Alerting/ACS.Alerting.csproj", "ACS.Alerting/"]

# Restore dependencies with retry logic for better reliability
RUN dotnet restore "ACS.WebApi/ACS.WebApi.csproj" --disable-parallel

# Copy source code
COPY . .
WORKDIR "/src/ACS.WebApi"

# Build with optimizations
RUN dotnet build "ACS.WebApi.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build \
    --no-restore

# Stage 3: Publish optimized for production
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ACS.WebApi.csproj" \
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

# Create directories for runtime with proper permissions
RUN mkdir -p /app/keys /app/logs /app/temp && \
    chown -R appuser:appgroup /app
USER appuser

# Health check with updated port
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Metadata labels
LABEL maintainer="ACS Team"
LABEL description="ACS WebApi - HTTP Proxy Layer"
LABEL version="1.0.0"
LABEL component="webapi"
LABEL tier="presentation"
LABEL architecture="proxy"

ENTRYPOINT ["dotnet", "ACS.WebApi.dll"]