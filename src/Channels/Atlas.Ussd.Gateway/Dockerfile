# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Install ca-certificates for HTTPS
RUN apk update && apk upgrade && apk add --no-cache ca-certificates

# Copy project file and restore dependencies
COPY Atlas.Ussd.Gateway.csproj .
RUN dotnet restore Atlas.Ussd.Gateway.csproj --runtime linux-musl-x64

# Copy source code
COPY . .

# Build the application with security optimizations
RUN dotnet publish Atlas.Ussd.Gateway.csproj -c Release -o /app/publish \
    --self-contained false \
    /p:PublishSingleFile=false \
    /p:PublishTrimmed=false \
    /p:EnableCompressionInSingleFile=false

# Runtime stage - MINIMAL SECURITY HARDENED
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# Install runtime dependencies
RUN apk update && apk upgrade && \
    apk add --no-cache ca-certificates tzdata wget && \
    rm -rf /var/cache/apk/*

# Create a non-root user
RUN addgroup -g 1010 -S ussduser && \
    adduser -u 1010 -S ussduser -G ussduser

# Copy published application
COPY --from=build /app/publish .

# Set file permissions (as root before switching user)
RUN chown -R ussduser:ussduser /app && \
    chmod -R 755 /app

# Switch to non-root user
USER ussduser

# Health check using wget (no curl dependency)
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:${PORT:-5620}/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "Atlas.Ussd.Gateway.dll"]
