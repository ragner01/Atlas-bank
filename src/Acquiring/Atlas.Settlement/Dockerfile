# Multi-stage build for Atlas.Settlement
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY Atlas.Settlement.csproj .

# Restore dependencies
RUN dotnet restore Atlas.Settlement.csproj

# Copy source code
COPY . .

# Build application
RUN dotnet build Atlas.Settlement.csproj -c Release -o /app/build

# Publish application
FROM build AS publish
RUN dotnet publish Atlas.Settlement.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user
RUN groupadd -r settlementuser && useradd -r -g settlementuser settlementuser

# Copy published application
COPY --from=publish /app/publish .

# Set ownership and permissions
RUN chown -R settlementuser:settlementuser /app
USER settlementuser

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production

# Start application
ENTRYPOINT ["dotnet", "Atlas.Settlement.dll"]
