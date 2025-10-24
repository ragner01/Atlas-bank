# Cards Vault Dockerfile - PCI DSS Compliant
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/Cards/Atlas.Cards.Vault/Atlas.Cards.Vault.csproj .
RUN dotnet restore

COPY src/Cards/Atlas.Cards.Vault/ .
RUN dotnet build -c Release -o /app/build

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Security: Run as non-root user
RUN groupadd -r cardsvault && useradd -r -g cardsvault cardsvault
USER cardsvault

COPY --from=build /app/build .

# PCI Compliance: Set secure defaults
ENV ASPNETCORE_URLS=http://+:5600
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5600

ENTRYPOINT ["dotnet", "Atlas.Cards.Vault.dll"]
