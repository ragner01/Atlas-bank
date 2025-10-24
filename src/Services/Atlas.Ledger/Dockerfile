# Multi-stage build for Atlas Ledger API
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for better layer caching
COPY src/BuildingBlocks/Atlas.Common/Atlas.Common.csproj src/BuildingBlocks/Atlas.Common/
COPY src/BuildingBlocks/Atlas.Messaging/Atlas.Messaging.csproj src/BuildingBlocks/Atlas.Messaging/
COPY src/BuildingBlocks/Atlas.Persistence/Atlas.Persistence.csproj src/BuildingBlocks/Atlas.Persistence/
COPY src/BuildingBlocks/Atlas.Security/Atlas.Security.csproj src/BuildingBlocks/Atlas.Security/
COPY src/BuildingBlocks/Atlas.Observability/Atlas.Observability.csproj src/BuildingBlocks/Atlas.Observability/
COPY src/BuildingBlocks/Atlas.Resilience/Atlas.Resilience.csproj src/BuildingBlocks/Atlas.Resilience/
COPY src/BuildingBlocks/Atlas.Database/Atlas.Database.csproj src/BuildingBlocks/Atlas.Database/
COPY src/Services/Atlas.Ledger/Atlas.Ledger.csproj src/Services/Atlas.Ledger/

# Restore dependencies
RUN dotnet restore src/Services/Atlas.Ledger/Atlas.Ledger.csproj

# Copy source code
COPY . .

# Build the application
WORKDIR /src/src/Services/Atlas.Ledger
RUN dotnet clean && dotnet restore && dotnet build Atlas.Ledger.csproj -c Release -o /app/build

# Publish the application
RUN dotnet publish Atlas.Ledger.csproj -c Release -o /app/publish --no-build /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN groupadd -r ledgeruser && useradd -r -g ledgeruser ledgeruser

# Copy published application
COPY --from=build /app/publish .

# Set proper permissions
RUN chown -R ledgeruser:ledgeruser /app

# Switch to non-root user
USER ledgeruser

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5181
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

# Expose port
EXPOSE 5181

# Health check without curl
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD dotnet /app/Atlas.Ledger.dll --health-check || exit 1

# Entry point
ENTRYPOINT ["dotnet", "Atlas.Ledger.dll"]