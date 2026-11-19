# syntax=docker/dockerfile:1

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore em camada separada (cache enquanto os .csproj/props não mudarem).
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/FinanceApp.Domain/FinanceApp.Domain.csproj src/FinanceApp.Domain/
COPY src/FinanceApp.Application/FinanceApp.Application.csproj src/FinanceApp.Application/
COPY src/FinanceApp.Infrastructure/FinanceApp.Infrastructure.csproj src/FinanceApp.Infrastructure/
COPY src/FinanceApp.API/FinanceApp.API.csproj src/FinanceApp.API/
RUN dotnet restore src/FinanceApp.API/FinanceApp.API.csproj

COPY src/ src/
RUN dotnet publish src/FinanceApp.API/FinanceApp.API.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# curl é usado pelo healthcheck do docker-compose (GET /health).
# tzdata é necessário para resolver App__TimeZone (ex.: America/Sao_Paulo, D-021).
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl tzdata \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

# Usuário não-root fornecido pela imagem oficial (UID 1654).
USER $APP_UID

ENTRYPOINT ["dotnet", "FinanceApp.API.dll"]
