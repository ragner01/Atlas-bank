# Multi-stage build for Atlas.Fees
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY Atlas.Fees.csproj .

# Restore dependencies
RUN dotnet restore Atlas.Fees.csproj

# Copy source code
COPY . .

# Build application
RUN dotnet build Atlas.Fees.csproj -c Release -o /app/build

# Publish application
FROM build AS publish
RUN dotnet publish Atlas.Fees.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user
RUN groupadd -r feesuser && useradd -r -g feesuser feesuser

# Copy published application
COPY --from=publish /app/publish .

# Set ownership and permissions
RUN chown -R feesuser:feesuser /app
USER feesuser

# Expose port
EXPOSE 5701

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5701
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5701/health || exit 1

# Start application
ENTRYPOINT ["dotnet", "Atlas.Fees.dll"]
