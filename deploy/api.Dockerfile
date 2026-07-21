# ByteBazaar API — ASP.NET Core 10 Web API
#
# Build context is the REPO ROOT (see docker-compose.prod.yml), because the Api
# project references Application/Domain/Infrastructure as sibling projects.
#
#   docker build -f deploy/api.Dockerfile -t bytebazaar-api .

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore against the csproj files alone first, so the (slow) restore layer is
# cached and only re-runs when a project file actually changes.
COPY backend/ByteBazaar.sln ./backend/
COPY backend/src/ByteBazaar.Api/ByteBazaar.Api.csproj                     ./backend/src/ByteBazaar.Api/
COPY backend/src/ByteBazaar.Application/ByteBazaar.Application.csproj     ./backend/src/ByteBazaar.Application/
COPY backend/src/ByteBazaar.Domain/ByteBazaar.Domain.csproj               ./backend/src/ByteBazaar.Domain/
COPY backend/src/ByteBazaar.Infrastructure/ByteBazaar.Infrastructure.csproj ./backend/src/ByteBazaar.Infrastructure/
RUN dotnet restore backend/src/ByteBazaar.Api/ByteBazaar.Api.csproj

COPY backend/ ./backend/
RUN dotnet publish backend/src/ByteBazaar.Api/ByteBazaar.Api.csproj \
      -c Release -o /app/publish \
      --no-restore \
      /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is required by the compose healthcheck; the aspnet image ships without it.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

# Run as the non-root user the .NET images provide rather than root.
USER $APP_UID

COPY --from=build /app/publish .

# appsettings.json pins "Urls" to localhost:5080, which would make the container
# unreachable from other services. ASPNETCORE_URLS overrides it and binds to all
# interfaces inside the container network.
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=1

EXPOSE 8080

ENTRYPOINT ["dotnet", "ByteBazaar.Api.dll"]
