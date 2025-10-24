FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/KycAml/Atlas.KycAml.Worker/Atlas.KycAml.Worker.csproj", "src/KycAml/Atlas.KycAml.Worker/"]
COPY ["src/Risk/Atlas.Risk.Domain/Atlas.Risk.Domain.csproj", "src/Risk/Atlas.Risk.Domain/"]
RUN dotnet restore "src/KycAml/Atlas.KycAml.Worker/Atlas.KycAml.Worker.csproj"
COPY . .
RUN dotnet build "src/KycAml/Atlas.KycAml.Worker/Atlas.KycAml.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/KycAml/Atlas.KycAml.Worker/Atlas.KycAml.Worker.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Atlas.KycAml.Worker.dll"]
