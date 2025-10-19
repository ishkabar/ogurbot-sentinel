# Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Prosto i bez kombinacji z warstwami: skopiuj całe źródła z repo (kontekst = ".")
COPY ./src ./ 

# Publish
RUN dotnet publish ./Ogur.Sentinel.Worker/Ogur.Sentinel.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app

# default appsettings + assets (jeśli trzymasz w repo – opcjonalnie)
COPY ./src/Ogur.Sentinel.Worker/appsettings /app/appsettings

# binaria
COPY --from=build /app/publish ./

# ffmpeg dla voice
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg && rm -rf /var/lib/apt/lists/*

ENV DOTNET_URLS=http://0.0.0.0:9090
EXPOSE 9090
ENTRYPOINT ["dotnet", "Ogur.Sentinel.Worker.dll"]
