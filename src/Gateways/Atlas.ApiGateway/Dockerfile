# Dockerfile for Atlas.ApiGateway
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/BuildingBlocks/Atlas.Common/Atlas.Common.csproj", "src/BuildingBlocks/Atlas.Common/"]
COPY ["src/BuildingBlocks/Atlas.Security/Atlas.Security.csproj", "src/BuildingBlocks/Atlas.Security/"]
COPY ["src/BuildingBlocks/Atlas.Observability/Atlas.Observability.csproj", "src/BuildingBlocks/Atlas.Observability/"]
COPY ["src/Gateways/Atlas.ApiGateway/Atlas.ApiGateway.csproj", "src/Gateways/Atlas.ApiGateway/"]

# Restore dependencies
RUN dotnet restore "src/Gateways/Atlas.ApiGateway/Atlas.ApiGateway.csproj"

# Copy source code
COPY . .

# Build the application
WORKDIR "/src/src/Gateways/Atlas.ApiGateway"
RUN dotnet build "Atlas.ApiGateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Atlas.ApiGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create non-root user
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

ENTRYPOINT ["dotnet", "Atlas.ApiGateway.dll"]
