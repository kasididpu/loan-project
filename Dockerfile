# Multi-stage build for the API image used by the local HA stack (Phase 9).
# One image, run in different roles (App:Role = api | worker) via docker-compose.
# The schema stays Azure SQL Database-compatible, so this same image could point
# at a managed database in a cloud deployment without a rebuild.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore against the project files alone first, so Docker reuses the (slow)
# restore layer whenever only source — not the dependency graph — has changed.
COPY src/LoanProject.Domain/LoanProject.Domain.csproj src/LoanProject.Domain/
COPY src/LoanProject.Application/LoanProject.Application.csproj src/LoanProject.Application/
COPY src/LoanProject.Infrastructure/LoanProject.Infrastructure.csproj src/LoanProject.Infrastructure/
COPY src/LoanProject.Api/LoanProject.Api.csproj src/LoanProject.Api/
RUN dotnet restore src/LoanProject.Api/LoanProject.Api.csproj

# Copy the rest of the source and publish a release build.
COPY src/ src/
RUN dotnet publish src/LoanProject.Api/LoanProject.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# curl backs the container HEALTHCHECK below (the aspnet image ships without it).
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

# Run as the non-root user the .NET image provides (defence in depth).
USER $APP_UID

# Kestrel listens on 8080 inside the container; nginx and the compose network
# reach every replica there.
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

HEALTHCHECK --interval=10s --timeout=5s --start-period=40s --retries=5 \
    CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "LoanProject.Api.dll"]
