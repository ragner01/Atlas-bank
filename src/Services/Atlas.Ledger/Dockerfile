# Dockerfile for Atlas.Ledger service
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443
EXPOSE 5181
EXPOSE 7001

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/BuildingBlocks/Atlas.Common/Atlas.Common.csproj", "src/BuildingBlocks/Atlas.Common/"]
COPY ["src/BuildingBlocks/Atlas.Messaging/Atlas.Messaging.csproj", "src/BuildingBlocks/Atlas.Messaging/"]
COPY ["src/BuildingBlocks/Atlas.Persistence/Atlas.Persistence.csproj", "src/BuildingBlocks/Atlas.Persistence/"]
COPY ["src/BuildingBlocks/Atlas.Security/Atlas.Security.csproj", "src/BuildingBlocks/Atlas.Security/"]
COPY ["src/BuildingBlocks/Atlas.Observability/Atlas.Observability.csproj", "src/BuildingBlocks/Atlas.Observability/"]
COPY ["src/Services/Atlas.Ledger/Atlas.Ledger.csproj", "src/Services/Atlas.Ledger/"]

# Restore dependencies
RUN dotnet restore "src/Services/Atlas.Ledger/Atlas.Ledger.csproj"

# Copy source code
COPY . .

# Build the application
WORKDIR "/src/src/Services/Atlas.Ledger"
RUN dotnet build "Atlas.Ledger.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Atlas.Ledger.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create non-root user
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

ENTRYPOINT ["dotnet", "Atlas.Ledger.dll"]
