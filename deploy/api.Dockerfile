# syntax=docker/dockerfile:1.7
# OmniOps API. Build from the repository root:  docker build -f deploy/api.Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /repo
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/ src/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/OmniOps.Api/OmniOps.Api.csproj -c Release -o /app/publish -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
# tzdata: invoices state times (e.g. a truck's arrival) in each company's time zone.
RUN apk add --no-cache tzdata wget \
 && mkdir -p /app/data/uploads && chown -R app:app /app/data
WORKDIR /app
COPY --from=build --chown=app:app /app/publish .
USER app
ENV ASPNETCORE_URLS=http://+:8080 \
    Storage__RootPath=/app/data/uploads
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
  CMD wget -qO- http://127.0.0.1:8080/health || exit 1
ENTRYPOINT ["dotnet", "OmniOps.Api.dll"]
