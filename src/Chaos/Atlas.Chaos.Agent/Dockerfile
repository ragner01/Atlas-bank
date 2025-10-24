# Multi-stage build for Atlas.Chaos.Agent
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY Atlas.Chaos.Agent.csproj .
RUN dotnet restore

# Copy source code and build
COPY . .
RUN dotnet build -c Release --no-restore

# Publish
RUN dotnet publish -c Release --no-build -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user
RUN groupadd -r chaos && useradd -r -g chaos chaos
RUN chown -R chaos:chaos /app
USER chaos

# Copy published app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Atlas.Chaos.Agent.dll"]

