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
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "GestorEquipos.dll"]
