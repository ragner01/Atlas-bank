FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["gateways/Atlas.ApiGateway/Atlas.ApiGateway.csproj", "gateways/Atlas.ApiGateway/"]
RUN dotnet restore "gateways/Atlas.ApiGateway/Atlas.ApiGateway.csproj"
COPY . .
RUN dotnet build "gateways/Atlas.ApiGateway/Atlas.ApiGateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "gateways/Atlas.ApiGateway/Atlas.ApiGateway.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 5080
ENTRYPOINT ["dotnet", "Atlas.ApiGateway.dll"]
