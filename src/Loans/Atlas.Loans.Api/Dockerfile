FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Loans/Atlas.Loans.Api/Atlas.Loans.Api.csproj", "src/Loans/Atlas.Loans.Api/"]
COPY ["src/Loans/Atlas.Loans.App/Atlas.Loans.App.csproj", "src/Loans/Atlas.Loans.App/"]
COPY ["src/Loans/Atlas.Loans.Domain/Atlas.Loans.Domain.csproj", "src/Loans/Atlas.Loans.Domain/"]
RUN dotnet restore "src/Loans/Atlas.Loans.Api/Atlas.Loans.Api.csproj"
COPY . .
RUN dotnet build "src/Loans/Atlas.Loans.Api/Atlas.Loans.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/Loans/Atlas.Loans.Api/Atlas.Loans.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 5221
ENTRYPOINT ["dotnet", "Atlas.Loans.Api.dll"]
