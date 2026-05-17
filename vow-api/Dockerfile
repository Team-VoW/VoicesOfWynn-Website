FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ffmpeg unzip \
    && rm -rf /var/lib/apt/lists/*

RUN curl -sSL https://aka.ms/getvsdbgsh \
    | bash /dev/stdin -v latest -l /vsdbg

WORKDIR /workspace

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG TARGETARCH

WORKDIR /src
COPY src/VoW.Api/VoW.Api.csproj src/VoW.Api/
RUN dotnet restore src/VoW.Api/VoW.Api.csproj -a $TARGETARCH
COPY . .
RUN dotnet publish src/VoW.Api/VoW.Api.csproj -c Release -a $TARGETARCH -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "VoW.Api.dll"]
