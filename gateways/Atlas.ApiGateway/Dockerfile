FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for better layer caching
COPY ["gateways/Atlas.ApiGateway/Atlas.ApiGateway.csproj", "gateways/Atlas.ApiGateway/"]
RUN dotnet restore "gateways/Atlas.ApiGateway/Atlas.ApiGateway.csproj"

# Copy source code
COPY . .

# Build the application
RUN dotnet build "gateways/Atlas.ApiGateway/Atlas.ApiGateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "gateways/Atlas.ApiGateway/Atlas.ApiGateway.csproj" -c Release -o /app/publish

# Use distroless image for better security
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy AS final

# Install wget for health checks
RUN apt-get update && apt-get install -y wget && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd -r appgroup && useradd -r -g appgroup appuser

WORKDIR /app

# Copy published files
COPY --from=publish /app/publish .

# Change ownership to non-root user
RUN chown -R appuser:appgroup /app
USER appuser

# Health check using wget (no curl dependency)
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:${PORT:-5080}/health || exit 1

# Resource limits
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Expose ports (HTTP for internal, HTTPS for external)
EXPOSE 5080 5443

# Use exec form for better signal handling
ENTRYPOINT ["dotnet", "Atlas.ApiGateway.dll"]