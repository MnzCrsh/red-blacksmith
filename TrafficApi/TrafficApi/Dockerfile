FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
ARG BUILD_CONFIGURATION=Release

WORKDIR /src

COPY . .

RUN dotnet publish "RedBlacksmith.TrafficApi/RedBlacksmith.TrafficApi.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "RedBlacksmith.TrafficApi.dll"]