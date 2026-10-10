FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Reglas de compilacion y versiones centralizadas: deben estar antes del restore
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/GrupoDigitalBank.Negocios/*.csproj ./
RUN dotnet restore
COPY src/GrupoDigitalBank.Negocios/ ./
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# APP_UID lo define la imagen base de Microsoft (numerico, 1654): hadolint no puede verlo
# hadolint ignore=DL3066
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "GrupoDigitalBank.Negocios.dll"]
