FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["GoIsland.Api/GoIsland.Api.csproj", "GoIsland.Api/"]
RUN dotnet restore "GoIsland.Api/GoIsland.Api.csproj"

COPY . .
WORKDIR /src/GoIsland.Api
RUN dotnet publish "GoIsland.Api.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "GoIsland.Api.dll"]
