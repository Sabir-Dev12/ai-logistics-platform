# ---------------------------------------------------------------------------------------------
# Multi-stage build for the Logistics API.
#
# VERIFICATION STATUS: the base images could not be pulled in the authoring environment (no
# outbound network, and no Docker daemon was running), so this image has NOT been built end to
# end. What HAS been verified is everything that a blind Dockerfile usually gets wrong: every
# COPY source in this file is checked against the real project layout by the verification harness
# (see tests/Logistics.Verify), so a renamed or deleted file fails a test rather than a build.
#
# One such defect was already found and fixed this way: an earlier revision copied
# `Directory.Packages.props`, which does not exist in this repository, and the build would have
# failed at the first COPY.
# ---------------------------------------------------------------------------------------------

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, in its own layer, so source edits do not invalidate the package cache.
#
# NOTE ON PACKAGE SOURCES: the repository's NuGet.config does `<clear />` on package sources. That
# is a workaround for the offline authoring machine, where every package is already in the global
# packages folder. It is deliberately NOT copied here: inside the image there is no warm cache, so
# restore must reach a real feed. Copying it would make the build fail with "no sources". The
# restore below therefore uses the default feed, which is what a networked CI agent expects.
COPY ["Directory.Build.props", "./"]
COPY ["src/Logistics.Domain/Logistics.Domain.csproj", "src/Logistics.Domain/"]
COPY ["src/Logistics.Application/Logistics.Application.csproj", "src/Logistics.Application/"]
COPY ["src/Logistics.Infrastructure/Logistics.Infrastructure.csproj", "src/Logistics.Infrastructure/"]
COPY ["src/Logistics.Api/Logistics.Api.csproj", "src/Logistics.Api/"]
RUN dotnet restore "src/Logistics.Api/Logistics.Api.csproj"

COPY . .

# --no-restore on purpose. `COPY . .` brings the offline NuGet.config into the build context, and a
# publish that restored again would then see no package sources at all. The packages are already
# present from the restore above, so restoring twice buys nothing and risks exactly that failure.
RUN dotnet publish "src/Logistics.Api/Logistics.Api.csproj" \
        -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish .

# Run as the non-root user that the ASP.NET Core images already provide.
USER $APP_UID

# This probe only proves the runtime is present and executable — `dotnet --info` succeeds even if
# the application is hung, so it is a process-liveness proxy, not a readiness signal. The aspnet
# image ships no HTTP client, so the honest readiness probe is the orchestrator calling
# /health/live and /health/ready directly (Kubernetes httpGet, or a compose healthcheck that uses
# a tool installed for the purpose).
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD ["dotnet", "--info"]

ENTRYPOINT ["dotnet", "Logistics.Api.dll"]
