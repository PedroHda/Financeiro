FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY Financeiro.Api/Financeiro.Api.csproj Financeiro.Api/
COPY Financeiro.Sync.Contracts/Financeiro.Sync.Contracts.csproj Financeiro.Sync.Contracts/
RUN dotnet restore Financeiro.Api/Financeiro.Api.csproj
COPY Financeiro.Api/ Financeiro.Api/
COPY Financeiro.Sync.Contracts/ Financeiro.Sync.Contracts/
RUN dotnet publish Financeiro.Api/Financeiro.Api.csproj -c Release --no-restore -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out/ ./
RUN mkdir -p /app/private/keys && chown -R $APP_UID:$APP_UID /app/private
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
ENTRYPOINT ["dotnet", "Financeiro.Api.dll"]
