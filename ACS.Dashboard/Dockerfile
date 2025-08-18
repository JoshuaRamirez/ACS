# ACS Dashboard Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5100
EXPOSE 5101

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution file
COPY ["ACS.sln", "."]

# Copy project files
COPY ["ACS.Dashboard/ACS.Dashboard.csproj", "ACS.Dashboard/"]
COPY ["ACS.Core/ACS.Core.csproj", "ACS.Core/"]
COPY ["ACS.Service/ACS.Service.csproj", "ACS.Service/"]
COPY ["ACS.Infrastructure/ACS.Infrastructure.csproj", "ACS.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "ACS.Dashboard/ACS.Dashboard.csproj"

# Copy all source code
COPY . .

# Build the application
WORKDIR "/src/ACS.Dashboard"
RUN dotnet build "ACS.Dashboard.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ACS.Dashboard.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Create directories for logs
RUN mkdir -p /app/logs

# Set permissions
RUN chown -R app:app /app
USER app

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:5100/health || exit 1

ENTRYPOINT ["dotnet", "ACS.Dashboard.dll"]