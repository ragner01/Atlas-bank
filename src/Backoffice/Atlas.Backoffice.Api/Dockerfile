FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Backoffice/Atlas.Backoffice.Api/Atlas.Backoffice.Api.csproj", "src/Backoffice/Atlas.Backoffice.Api/"]
RUN dotnet restore "src/Backoffice/Atlas.Backoffice.Api/Atlas.Backoffice.Api.csproj"
COPY . .
RUN dotnet build "src/Backoffice/Atlas.Backoffice.Api/Atlas.Backoffice.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/Backoffice/Atlas.Backoffice.Api/Atlas.Backoffice.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 5210
ENTRYPOINT ["dotnet", "Atlas.Backoffice.Api.dll"]
