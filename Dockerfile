# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY GestorEquipos.csproj .
RUN dotnet restore GestorEquipos.csproj

COPY . .
RUN dotnet publish GestorEquipos.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Fuentes TrueType libres (Liberation ~ Arial/Times New Roman/Courier New, DejaVu Sans ~
# Verdana) requeridas por Services/Implementations/LocalFontResolver.cs para generar los
# PDF con MigraDoc. Ambos paquetes están en el repo "main" de Debian (sin EULA ni descargas
# externas), a diferencia de ttf-mscorefonts-installer que vive en "contrib".
RUN apt-get update \
    && apt-get install -y --no-install-recommends fontconfig fonts-liberation fonts-dejavu-core \
    && fc-cache -f \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --uid 1000 --create-home --shell /usr/sbin/nologin appuser

COPY --from=build --chown=appuser:appuser /app/publish .

# OPS-04: run as a non-root user rather than the image's default root. The Data Protection
# key ring (cookies/antiforgery) now persists under this user's home directory — the
# dataprotection_keys volume mount in docker-compose.yml must match (see there).
USER appuser

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "GestorEquipos.dll"]
