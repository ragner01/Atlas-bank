FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Atlas.Realtime.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Create non-root user for security
RUN groupadd -r atlas && useradd -r -g atlas atlas
RUN chown -R atlas:atlas /app
USER atlas

EXPOSE 5851
ENTRYPOINT ["dotnet", "Atlas.Realtime.dll"]

