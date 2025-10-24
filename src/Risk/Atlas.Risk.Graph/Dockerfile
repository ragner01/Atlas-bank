# Multi-stage Dockerfile for AtlasBank Risk Graph Service
# Optimized for production with ONNX model support

# =============================================
# STAGE 1: Build Stage
# =============================================
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build

# Install security updates and build dependencies
RUN apk update && apk upgrade && \
    apk add --no-cache \
    ca-certificates \
    tzdata \
    && rm -rf /var/cache/apk/*

# Set working directory
WORKDIR /src

# Copy project files for dependency restoration
COPY src/Risk/Atlas.Risk.Graph/Atlas.Risk.Graph.csproj ./Atlas.Risk.Graph/

# Restore dependencies
RUN dotnet restore Atlas.Risk.Graph/Atlas.Risk.Graph.csproj

# Copy source code
COPY src/Risk/Atlas.Risk.Graph/ ./
COPY contracts/proto/risk/v1/risk.proto ../../../contracts/proto/risk/v1/

# Build the application
RUN dotnet build Atlas.Risk.Graph.csproj \
    --configuration Release \
    --no-restore \
    --verbosity minimal

# Publish the application
RUN dotnet publish Atlas.Risk.Graph.csproj \
    --configuration Release \
    --no-build \
    --output /app/publish \
    --verbosity minimal

# =============================================
# STAGE 2: Runtime Stage
# =============================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime

# Install security updates and runtime dependencies
RUN apk update && apk upgrade && \
    apk add --no-cache \
    ca-certificates \
    tzdata \
    curl \
    && rm -rf /var/cache/apk/*

# Create non-root user for security
RUN addgroup -g 1001 -S atlasbank && \
    adduser -u 1001 -S atlasbank -G atlasbank

# Set working directory
WORKDIR /app

# Copy published application
COPY --from=build /app/publish .

# Create models directory
RUN mkdir -p /app/models && chown -R atlasbank:atlasbank /app/models

# Change ownership to non-root user
RUN chown -R atlasbank:atlasbank /app

# Switch to non-root user
USER atlasbank

# Expose ports
EXPOSE 5401

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5401 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
    DOTNET_RUNNING_IN_CONTAINER=true

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:5401/health || exit 1

# Start the application
ENTRYPOINT ["dotnet", "Atlas.Risk.Graph.dll"]
