# syntax=docker/dockerfile:1.7
# Multi-stage build for the DaftarX Portal Web (010-website-portal).
# Stage 1: restore + publish using the .NET 8 SDK.
# Stage 2: distroless runtime — small, no shell, no package manager.
# Stage 3 (Tailwind): runs `npx tailwindcss` to compile site.css at build time.

ARG TAILWIND_VERSION=3.4.14
ARG DOTNET_SDK=8.0
ARG DOTNET_RUNTIME=8.0

# ----- Tailwind CSS build ---------------------------------------------------
FROM node:20-alpine AS tailwind
WORKDIR /css
COPY src/EgyptTax.Portal.Web/wwwroot/css/input.css /css/input.css
COPY src/EgyptTax.Portal.Web/tailwind.config.js /css/tailwind.config.js
RUN npm install -g tailwindcss@${TAILWIND_VERSION} \
 && tailwindcss -c /css/tailwind.config.js -i /css/input.css -o /css/site.css --minify

# ----- .NET publish ---------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK} AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/EgyptTax.Portal.Web/EgyptTax.Portal.Web.csproj \
    -c Release \
    -o /publish \
    --no-self-contained \
    /p:UseAppHost=false

# Drop the Tailwind-compiled CSS into the published wwwroot.
COPY --from=tailwind /css/site.css /publish/wwwroot/css/site.css

# ----- Runtime --------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_RUNTIME}-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "EgyptTax.Portal.Web.dll"]
