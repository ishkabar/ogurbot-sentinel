FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

RUN apt-get update && \
    apt-get install -y ffmpeg libopus0 libopus-dev libsodium23 curl unzip && \
    curl -L -o /tmp/libdave.zip $(curl -s https://api.github.com/repos/discord/libdave/releases/latest | grep -o 'https://[^"]*libdave-Linux-X64-boringssl\.zip') && \
    unzip /tmp/libdave.zip -d /tmp/libdave && \
    cp /tmp/libdave/lib/libdave.so /app/libdave.so && \
    rm -rf /tmp/libdave.zip /tmp/libdave && \
    apt-get purge -y curl unzip && \
    apt-get autoremove -y && \
    rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://0.0.0.0:9090
EXPOSE 9090

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
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
ARG VERSION=1.0.0-dev
WORKDIR /app
COPY --from=build /app/publish .

ENV APP_VERSION=${VERSION}
LABEL version="${VERSION}"

ENTRYPOINT ["dotnet","Ogur.Sentinel.Worker.dll"]