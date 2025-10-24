# Multi-stage build for Atlas Canary Service
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Install security updates
RUN apk update && apk upgrade && apk add --no-cache ca-certificates

# Copy project files for better layer caching
COPY Atlas.Canary.csproj .

# Restore dependencies with security scanning
RUN dotnet restore Atlas.Canary.csproj --runtime linux-musl-x64

# Copy source code
COPY . .

# Build the application with security optimizations
RUN dotnet publish Atlas.Canary.csproj -c Release -o /app/publish \
    --runtime linux-musl-x64 \
    --self-contained true \
    /p:PublishSingleFile=false \
    /p:PublishTrimmed=true \
    /p:TrimMode=partial \
    /p:EnableCompressionInSingleFile=true

# Runtime stage - MINIMAL SECURITY HARDENED
FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine AS runtime
WORKDIR /app

# Install security updates and minimal required packages
RUN apk update && apk upgrade && \
    apk add --no-cache ca-certificates tzdata wget && \
    rm -rf /var/cache/apk/*

# Create non-root user with specific UID/GID for security
RUN addgroup -g 1006 -S canaryuser && \
    adduser -u 1006 -S canaryuser -G canaryuser

# Copy published application
COPY --from=build /app/publish .

# Set proper permissions and ownership
RUN chown -R canaryuser:canaryuser /app && \
    chmod -R 755 /app

# Switch to non-root user
USER canaryuser

# Set security-focused environment variables
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
ENV DOTNET_EnableDiagnostics=0
ENV DOTNET_UseSharedCompilation=false
ENV DOTNET_UseServerGC=true
ENV DOTNET_gcServer=1
ENV DOTNET_gcConcurrent=1

# Health check using wget (no curl dependency)
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "Atlas.Canary.dll"]
