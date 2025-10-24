FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Ledger/Atlas.Ledger.ReadModel/Atlas.Ledger.ReadModel.csproj", "src/Ledger/Atlas.Ledger.ReadModel/"]
RUN dotnet restore "src/Ledger/Atlas.Ledger.ReadModel/Atlas.Ledger.ReadModel.csproj"
COPY . .
WORKDIR "/src/src/Ledger/Atlas.Ledger.ReadModel"
RUN dotnet build "Atlas.Ledger.ReadModel.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Atlas.Ledger.ReadModel.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser
ENTRYPOINT ["dotnet", "Atlas.Ledger.ReadModel.dll"]
