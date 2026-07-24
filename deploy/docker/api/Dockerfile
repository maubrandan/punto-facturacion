# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore layer: only project files (invalidates less often than source)
COPY server/POSSystem.slnx ./server/
COPY server/POS.API/POS.API.csproj ./server/POS.API/
COPY server/POS.Application/POS.Application.csproj ./server/POS.Application/
COPY server/POS.Domain/POS.Domain.csproj ./server/POS.Domain/
COPY server/POS.Infrastructure/POS.Infrastructure.csproj ./server/POS.Infrastructure/

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore ./server/POS.API/POS.API.csproj

# Source layer: production projects only (tests excluded via .dockerignore / selective COPY)
COPY server/POS.API/ ./server/POS.API/
COPY server/POS.Application/ ./server/POS.Application/
COPY server/POS.Domain/ ./server/POS.Domain/
COPY server/POS.Infrastructure/ ./server/POS.Infrastructure/

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish ./server/POS.API/POS.API.csproj -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --start-period=90s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8080/health >/dev/null || exit 1
ENTRYPOINT ["dotnet", "POS.API.dll"]
