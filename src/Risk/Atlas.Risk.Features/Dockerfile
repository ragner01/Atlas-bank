FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Risk/Atlas.Risk.Features/Atlas.Risk.Features.csproj", "src/Risk/Atlas.Risk.Features/"]
RUN dotnet restore "src/Risk/Atlas.Risk.Features/Atlas.Risk.Features.csproj"
COPY . .
RUN dotnet build "src/Risk/Atlas.Risk.Features/Atlas.Risk.Features.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/Risk/Atlas.Risk.Features/Atlas.Risk.Features.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 5301
ENTRYPOINT ["dotnet", "Atlas.Risk.Features.dll"]
