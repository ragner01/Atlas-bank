# Multi-stage build for Atlas.Chaos.Manager
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY Atlas.Chaos.Manager.csproj .
RUN dotnet restore

# Copy source code and build
COPY . .
RUN dotnet build -c Release --no-restore

# Publish
RUN dotnet publish -c Release --no-build -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install wget for health checks
RUN apt-get update && apt-get install -y wget && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r chaos && useradd -r -g chaos chaos
RUN chown -R chaos:chaos /app
USER chaos

# Copy published app
COPY --from=build /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
  CMD wget --quiet --tries=1 --spider http://localhost:5951/health || exit 1

EXPOSE 5951
ENTRYPOINT ["dotnet", "Atlas.Chaos.Manager.dll"]

