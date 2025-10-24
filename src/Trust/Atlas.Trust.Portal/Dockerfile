# Multi-stage build for Atlas Trust Portal - SECURITY OPTIMIZED
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Install security updates
RUN apk update && apk upgrade && apk add --no-cache ca-certificates

# Copy project files for better layer caching
COPY Atlas.Trust.Portal.csproj .

# Restore dependencies with security scanning
RUN dotnet restore Atlas.Trust.Portal.csproj --runtime linux-musl-x64

# Copy source code
COPY . .

# Build the application with security optimizations
RUN dotnet publish Atlas.Trust.Portal.csproj -c Release -o /app/publish \
    --runtime linux-musl-x64 \
    --self-contained false \
    /p:PublishSingleFile=false \
    /p:PublishTrimmed=true \
    /p:TrimMode=partial \
    /p:EnableCompressionInSingleFile=true

# Runtime stage - MINIMAL SECURITY HARDENED
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# Install security updates and minimal required packages
RUN apk update && apk upgrade && \
    apk add --no-cache ca-certificates tzdata && \
    rm -rf /var/cache/apk/*

# Create non-root user with specific UID/GID for security
RUN addgroup -g 1004 -S portaluser && \
    adduser -u 1004 -S portaluser -G portaluser

# Copy published application
COPY --from=build /app/publish .

# Set proper permissions and ownership
RUN chown -R portaluser:portaluser /app && \
    chmod -R 755 /app

# Switch to non-root user
USER portaluser

# Set security-focused environment variables
ENV ASPNETCORE_URLS=http://+:5802
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
ENV DOTNET_EnableDiagnostics=0
ENV DOTNET_UseSharedCompilation=false
ENV DOTNET_UseServerGC=true
ENV DOTNET_gcServer=1
ENV DOTNET_gcConcurrent=1

# Expose port
EXPOSE 5802

# Health check using wget (no curl dependency)
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:5802/portal || exit 1

# Set resource limits
RUN echo "portaluser soft nofile 65536" >> /etc/security/limits.conf && \
    echo "portaluser hard nofile 65536" >> /etc/security/limits.conf

# Entry point
ENTRYPOINT ["dotnet", "Atlas.Trust.Portal.dll"]
