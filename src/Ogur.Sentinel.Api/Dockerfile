FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG VERSION=1.0.0-dev
WORKDIR /src

COPY ./Ogur.Sentinel.Api/*.csproj ./Ogur.Sentinel.Api/
COPY ./Ogur.Sentinel.Abstractions/*.csproj ./Ogur.Sentinel.Abstractions/
COPY ./Ogur.Sentinel.Core/*.csproj ./Ogur.Sentinel.Core/
RUN dotnet restore ./Ogur.Sentinel.Api/Ogur.Sentinel.Api.csproj

COPY . .
RUN dotnet publish ./Ogur.Sentinel.Api/Ogur.Sentinel.Api.csproj \
    -c Release \
    -o /app/publish \
    -p:Version=${VERSION} \
    --no-restore

FROM base AS final
WORKDIR /app

RUN mkdir -p /app/keys && chmod 777 /app/keys
COPY --from=build /app/publish .

ENV APP_VERSION=${VERSION}
LABEL version="${VERSION}"

ENTRYPOINT ["dotnet","Ogur.Sentinel.Api.dll"]