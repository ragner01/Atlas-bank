FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/KycAml/Atlas.KycAml.Api/Atlas.KycAml.Api.csproj", "src/KycAml/Atlas.KycAml.Api/"]
COPY ["src/KycAml/Atlas.KycAml.Domain/Atlas.KycAml.Domain.csproj", "src/KycAml/Atlas.KycAml.Domain/"]
RUN dotnet restore "src/KycAml/Atlas.KycAml.Api/Atlas.KycAml.Api.csproj"
COPY . .
RUN dotnet build "src/KycAml/Atlas.KycAml.Api/Atlas.KycAml.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/KycAml/Atlas.KycAml.Api/Atlas.KycAml.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 5201
ENTRYPOINT ["dotnet", "Atlas.KycAml.Api.dll"]
