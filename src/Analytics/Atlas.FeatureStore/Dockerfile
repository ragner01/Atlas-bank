# Multi-stage build for Atlas.FeatureStore
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY Atlas.FeatureStore.csproj .
RUN dotnet restore

# Copy source code and build
COPY . .
RUN dotnet build -c Release -o /app/build

# Publish stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS publish
WORKDIR /src
COPY Atlas.FeatureStore.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install wget for health checks
RUN apt-get update && apt-get install -y wget && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r atlas && useradd -r -g atlas atlas
RUN mkdir -p /app/logs && chown -R atlas:atlas /app

# Copy published application
COPY --from=publish /app/publish .

# Set ownership and permissions
USER atlas

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
  CMD wget --quiet --tries=1 --spider http://localhost:5831/health || exit 1

# Expose port
EXPOSE 5831

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5831
ENV ASPNETCORE_ENVIRONMENT=Production

# Run the application
ENTRYPOINT ["dotnet", "Atlas.FeatureStore.dll"]
