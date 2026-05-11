# DaftarX — Linux container build for the EgyptTax.Web Blazor Server app.
# Multi-stage: SDK image restores + builds + publishes; runtime image
# carries only the published bits and serves over Kestrel on port 8080.
#
# This is for development / iteration on Linux containers. Production
# is the Windows-Service-on-prem MSI install — that path is unchanged.

ARG DOTNET_VERSION=8.0

# ----------------------------- build stage -----------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

# Copy NuGet metadata first so the restore layer caches across rebuilds.
COPY Directory.Packages.props Directory.Build.props ./
COPY EgyptTax.sln ./

# Project files only — restore layer.
COPY src/EgyptTax.Domain/EgyptTax.Domain.csproj                   src/EgyptTax.Domain/
COPY src/EgyptTax.SharedKernel/EgyptTax.SharedKernel.csproj       src/EgyptTax.SharedKernel/
COPY src/EgyptTax.Application/EgyptTax.Application.csproj         src/EgyptTax.Application/
COPY src/EgyptTax.Infrastructure/EgyptTax.Infrastructure.csproj   src/EgyptTax.Infrastructure/
COPY src/EgyptTax.Web/EgyptTax.Web.csproj                         src/EgyptTax.Web/

RUN dotnet restore src/EgyptTax.Web/EgyptTax.Web.csproj

# Now copy the rest and publish. We exclude the Windows-only
# Bootstrapper + Installer projects (they don't compile cleanly on
# Linux and aren't needed by the web app).
COPY src/EgyptTax.Domain/         src/EgyptTax.Domain/
COPY src/EgyptTax.SharedKernel/   src/EgyptTax.SharedKernel/
COPY src/EgyptTax.Application/    src/EgyptTax.Application/
COPY src/EgyptTax.Infrastructure/ src/EgyptTax.Infrastructure/
COPY src/EgyptTax.Web/            src/EgyptTax.Web/
COPY specs/008-egypt-tax-accounting/contracts/ specs/008-egypt-tax-accounting/contracts/

RUN dotnet publish src/EgyptTax.Web/EgyptTax.Web.csproj \
    -c Release \
    -o /publish \
    --no-restore \
    -p:UseAppHost=false \
    -p:TreatWarningsAsErrors=false

# ---------------------------- runtime stage ----------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

# QuestPDF needs libfontconfig for font discovery; curl is handy for
# the in-container healthcheck. tzdata so Africa/Cairo resolves.
RUN apt-get update \
 && apt-get install -y --no-install-recommends libfontconfig1 curl tzdata \
 && rm -rf /var/lib/apt/lists/*

ENV TZ=Africa/Cairo \
    ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /publish ./

# Writable mount points for attachments + logs + inspection bundles.
RUN mkdir -p /var/daftarx/attachments \
             /var/daftarx/inspection-bundles \
             /var/daftarx/audit-checkpoints \
             /app/logs

EXPOSE 8080

ENTRYPOINT ["dotnet", "EgyptTax.Web.dll"]
