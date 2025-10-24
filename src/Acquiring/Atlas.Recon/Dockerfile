# Multi-stage build for Atlas.Recon
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY Atlas.Recon.csproj .

# Restore dependencies
RUN dotnet restore Atlas.Recon.csproj

# Copy source code
COPY . .

# Build application
RUN dotnet build Atlas.Recon.csproj -c Release -o /app/build

# Publish application
FROM build AS publish
RUN dotnet publish Atlas.Recon.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user
RUN groupadd -r reconuser && useradd -r -g reconuser reconuser

# Copy published application
COPY --from=publish /app/publish .

# Set ownership and permissions
RUN chown -R reconuser:reconuser /app
USER reconuser

# Expose port
EXPOSE 5702

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5702
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5702/health || exit 1

# Start application
ENTRYPOINT ["dotnet", "Atlas.Recon.dll"]
