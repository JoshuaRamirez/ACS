# ACS WebApi Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5000
EXPOSE 5001

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution file
COPY ["ACS.sln", "."]

# Copy project files
COPY ["ACS.WebApi/ACS.WebApi.csproj", "ACS.WebApi/"]
COPY ["ACS.Core/ACS.Core.csproj", "ACS.Core/"]
COPY ["ACS.Service/ACS.Service.csproj", "ACS.Service/"]
COPY ["ACS.Infrastructure/ACS.Infrastructure.csproj", "ACS.Infrastructure/"]
COPY ["ACS.Database/ACS.Database.csproj", "ACS.Database/"]

# Restore dependencies
RUN dotnet restore "ACS.WebApi/ACS.WebApi.csproj"

# Copy all source code
COPY . .

# Build the application
WORKDIR "/src/ACS.WebApi"
RUN dotnet build "ACS.WebApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ACS.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

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
    CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "ACS.WebApi.dll"]