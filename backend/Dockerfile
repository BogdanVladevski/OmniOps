# Build context: repository root (docker build -f backend/Dockerfile .)
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

COPY backend/OmniOps.sln ./
COPY backend/OmniOps.Api/OmniOps.Api.csproj backend/OmniOps.Api/
COPY backend/OmniOps.Application/OmniOps.Application.csproj backend/OmniOps.Application/
COPY backend/OmniOps.Core/OmniOps.Core.csproj backend/OmniOps.Core/
COPY backend/OmniOps.Infrastructure/OmniOps.Infrastructure.csproj backend/OmniOps.Infrastructure/

RUN dotnet restore backend/OmniOps.Api/OmniOps.Api.csproj

COPY backend/ ./backend/
RUN dotnet publish backend/OmniOps.Api/OmniOps.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final
WORKDIR /app

RUN apk add --no-cache curl

RUN addgroup -S omniops && adduser -S omniops -G omniops
USER omniops

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
  CMD curl -f http://127.0.0.1:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "OmniOps.Api.dll"]
