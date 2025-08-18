# ACS VerticalHost Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 6000
EXPOSE 6001

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution file
COPY ["ACS.sln", "."]

# Copy project files
COPY ["ACS.VerticalHost/ACS.VerticalHost.csproj", "ACS.VerticalHost/"]
COPY ["ACS.Core/ACS.Core.csproj", "ACS.Core/"]
COPY ["ACS.Service/ACS.Service.csproj", "ACS.Service/"]
COPY ["ACS.Infrastructure/ACS.Infrastructure.csproj", "ACS.Infrastructure/"]
COPY ["ACS.Database/ACS.Database.csproj", "ACS.Database/"]

# Restore dependencies
RUN dotnet restore "ACS.VerticalHost/ACS.VerticalHost.csproj"

# Copy all source code
COPY . .

# Build the application
WORKDIR "/src/ACS.VerticalHost"
RUN dotnet build "ACS.VerticalHost.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ACS.VerticalHost.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Create directories for keys and logs
RUN mkdir -p /app/keys /app/logs

# Set permissions
RUN chown -R app:app /app
USER app

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:6000/health || exit 1

ENTRYPOINT ["dotnet", "ACS.VerticalHost.dll"]