FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Ogur.Sentinel.Worker/Ogur.Sentinel.Worker.csproj", "Ogur.Sentinel.Worker/"]
RUN dotnet restore "Ogur.Sentinel.Worker/Ogur.Sentinel.Worker.csproj"
COPY . .
WORKDIR "/src/Ogur.Sentinel.Worker"
RUN dotnet build "./Ogur.Sentinel.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Ogur.Sentinel.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Ogur.Sentinel.Worker.dll"]
