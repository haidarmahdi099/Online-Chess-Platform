# syntax=docker/dockerfile:1.7
# Build with the repository root (ChessPlatform/) as the Docker context.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN --mount=type=cache,id=chessplatform-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/Services/Analysis/Analysis.Api/Analysis.Api.csproj -p:NuGetAudit=false
RUN --mount=type=cache,id=chessplatform-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/Services/Analysis/Analysis.Api/Analysis.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends stockfish \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080 \
    Engine__Path=/usr/games/stockfish \
    Engine__Threads=1 \
    Engine__HashMb=64 \
    Engine__MaxConcurrentAnalyses=2 \
    Engine__QueueTimeoutSeconds=5 \
    Engine__AnalysisTimeoutSeconds=20
EXPOSE 8080
ENTRYPOINT ["dotnet", "Analysis.Api.dll"]
