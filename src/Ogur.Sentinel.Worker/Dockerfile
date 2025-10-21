FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

RUN apt-get update && \
    apt-get install -y ffmpeg && \
    rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://0.0.0.0:9090
EXPOSE 9090

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG VERSION=1.0.0-dev
WORKDIR /src

COPY ./Ogur.Sentinel.Worker/*.csproj ./Ogur.Sentinel.Worker/
COPY ./Ogur.Sentinel.Abstractions/*.csproj ./Ogur.Sentinel.Abstractions/
COPY ./Ogur.Sentinel.Core/*.csproj ./Ogur.Sentinel.Core/
RUN dotnet restore ./Ogur.Sentinel.Worker/Ogur.Sentinel.Worker.csproj

COPY . .
RUN dotnet publish ./Ogur.Sentinel.Worker/Ogur.Sentinel.Worker.csproj \
    -c Release \
    -o /app/publish \
    -p:Version=${VERSION} \
    --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV APP_VERSION=${VERSION}
LABEL version="${VERSION}"

ENTRYPOINT ["dotnet","Ogur.Sentinel.Worker.dll"]