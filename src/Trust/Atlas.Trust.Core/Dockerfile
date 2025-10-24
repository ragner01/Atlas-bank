# Multi-stage build for Atlas.Trust.Core
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY Atlas.Trust.Core.csproj .

# Restore dependencies
RUN dotnet restore Atlas.Trust.Core.csproj

# Copy source code
COPY . .

# Build application
RUN dotnet build Atlas.Trust.Core.csproj -c Release -o /app/build

# Publish application
FROM build AS publish
RUN dotnet publish Atlas.Trust.Core.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user
RUN groupadd -r trustuser && useradd -r -g trustuser trustuser

# Copy published application
COPY --from=publish /app/publish .

# Set ownership and permissions
RUN chown -R trustuser:trustuser /app
USER trustuser

# Expose port
EXPOSE 5801

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5801
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5801/health || exit 1

# Start application
ENTRYPOINT ["dotnet", "Atlas.Trust.Core.dll"]
