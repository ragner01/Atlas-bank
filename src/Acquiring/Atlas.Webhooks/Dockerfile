# Multi-stage build for Atlas.Webhooks
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY Atlas.Webhooks.csproj .

# Restore dependencies
RUN dotnet restore Atlas.Webhooks.csproj

# Copy source code
COPY . .

# Build application
RUN dotnet build Atlas.Webhooks.csproj -c Release -o /app/build

# Publish application
FROM build AS publish
RUN dotnet publish Atlas.Webhooks.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user
RUN groupadd -r webhookuser && useradd -r -g webhookuser webhookuser

# Copy published application
COPY --from=publish /app/publish .

# Set ownership and permissions
RUN chown -R webhookuser:webhookuser /app
USER webhookuser

# Expose port
EXPOSE 5703

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5703
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5703/health || exit 1

# Start application
ENTRYPOINT ["dotnet", "Atlas.Webhooks.dll"]
