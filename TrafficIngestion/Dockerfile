FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base

WORKDIR /app

EXPOSE 8080
EXPOSE 8081

ENV ASPNETCORE_HTTP_PORTS=8080 \
    Kestrel__EndpointDefaults__Protocols=Http2

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG BUILD_CONFIGURATION=Release

WORKDIR /src

COPY ["src/RedBlacksmith.TrafficIngestion.Core/RedBlacksmith.TrafficIngestion.Core.csproj", "src/RedBlacksmith.TrafficIngestion.Core/"]
COPY ["src/RedBlacksmith.TrafficIngestion.Application/RedBlacksmith.TrafficIngestion.Application.csproj", "src/RedBlacksmith.TrafficIngestion.Application/"]
COPY ["src/RedBlacksmith.TrafficIngestion.Contracts/RedBlacksmith.TrafficIngestion.Contracts.csproj", "src/RedBlacksmith.TrafficIngestion.Contracts/"]
COPY ["src/RedBlacksmith.TrafficIngestion.HealthChecks/RedBlacksmith.TrafficIngestion.HealthChecks.csproj", "src/RedBlacksmith.TrafficIngestion.HealthChecks/"]

RUN dotnet restore "src/RedBlacksmith.TrafficIngestion.Core/RedBlacksmith.TrafficIngestion.Core.csproj"

COPY . .

WORKDIR "/src/src/RedBlacksmith.TrafficIngestion.Core"

RUN dotnet build "./RedBlacksmith.TrafficIngestion.Core.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build \
    --no-restore

FROM build AS publish

ARG BUILD_CONFIGURATION=Release

RUN dotnet publish "./RedBlacksmith.TrafficIngestion.Core.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM base AS final

WORKDIR /app

COPY --from=publish /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "RedBlacksmith.TrafficIngestion.Core.dll"]
