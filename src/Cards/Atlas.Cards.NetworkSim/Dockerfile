# Network Simulator Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/Cards/Atlas.Cards.NetworkSim/Atlas.Cards.NetworkSim.csproj .
RUN dotnet restore

COPY src/Cards/Atlas.Cards.NetworkSim/ .
RUN dotnet build -c Release -o /app/build

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Security: Run as non-root user
RUN groupadd -r networksim && useradd -r -g networksim networksim
USER networksim

COPY --from=build /app/build .

ENV ASPNETCORE_URLS=http://+:5601
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5601

ENTRYPOINT ["dotnet", "Atlas.Cards.NetworkSim.dll"]
