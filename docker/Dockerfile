# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy solution and project definitions for optimized Docker caching
COPY FollowerCounter.sln ./
COPY src/FollowerCounter.Domain/FollowerCounter.Domain.csproj src/FollowerCounter.Domain/
COPY src/FollowerCounter.Application/FollowerCounter.Application.csproj src/FollowerCounter.Application/
COPY src/FollowerCounter.Infrastructure/FollowerCounter.Infrastructure.csproj src/FollowerCounter.Infrastructure/
COPY src/FollowerCounter.Api/FollowerCounter.Api.csproj src/FollowerCounter.Api/
COPY src/FollowerCounter.Worker/FollowerCounter.Worker.csproj src/FollowerCounter.Worker/
COPY tests/FollowerCounter.UnitTests/FollowerCounter.UnitTests.csproj tests/FollowerCounter.UnitTests/
COPY tests/FollowerCounter.IntegrationTests/FollowerCounter.IntegrationTests.csproj tests/FollowerCounter.IntegrationTests/

# Restore dependencies
RUN dotnet restore FollowerCounter.sln

# Copy entire source tree
COPY src/ src/

# Build and Publish API
RUN dotnet publish src/FollowerCounter.Api/FollowerCounter.Api.csproj -c Release -o /app/publish/api --no-restore

# Build and Publish Worker
RUN dotnet publish src/FollowerCounter.Worker/FollowerCounter.Worker.csproj -c Release -o /app/publish/worker --no-restore

# ============================================================
# API Service Runtime Image
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS api
WORKDIR /app
COPY --from=build /app/publish/api .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "FollowerCounter.Api.dll"]

# ============================================================
# Background Worker Service Runtime Image
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS worker
WORKDIR /app
COPY --from=build /app/publish/worker .

ENV DOTNET_ENVIRONMENT=Production

USER $APP_UID

ENTRYPOINT ["dotnet", "FollowerCounter.Worker.dll"]
