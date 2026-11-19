
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG BUILD_CONFIGURATION=Release

WORKDIR /src

COPY ["src/RedBlacksmith.TrafficProcessing.Core/RedBlacksmith.TrafficProcessing.Core.csproj", "src/RedBlacksmith.TrafficProcessing.Core/"]
COPY ["RedBlacksmith.TrafficProcessing.Application/RedBlacksmith.TrafficProcessing.Application.csproj", "RedBlacksmith.TrafficProcessing.Application/"]
COPY ["RedBlacksmith.TrafficProcessing.Contracts/RedBlacksmith.TrafficProcessing.Contracts.csproj", "RedBlacksmith.TrafficProcessing.Contracts/"]
COPY ["RedBlacksmith.TrafficProcessing.Kafka/RedBlacksmith.TrafficProcessing.Kafka.csproj", "RedBlacksmith.TrafficProcessing.Kafka/"]
COPY ["RedBlacksmith.TrafficProcessing.Clickhouse/RedBlacksmith.TrafficProcessing.Clickhouse.csproj", "RedBlacksmith.TrafficProcessing.Clickhouse/"]
COPY ["RedBlacksmith.TrafficProcessing.Postgres/RedBlacksmith.TrafficProcessing.Postgres.csproj", "RedBlacksmith.TrafficProcessing.Postgres/"]

RUN dotnet restore "src/RedBlacksmith.TrafficProcessing.Core/RedBlacksmith.TrafficProcessing.Core.csproj"

COPY . .

WORKDIR /src/src/RedBlacksmith.TrafficProcessing.Core

RUN dotnet publish "./RedBlacksmith.TrafficProcessing.Core.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "RedBlacksmith.TrafficProcessing.Core.dll"]
