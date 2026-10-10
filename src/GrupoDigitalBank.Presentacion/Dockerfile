FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Reglas de compilacion y versiones centralizadas: deben estar antes del restore
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/GrupoDigitalBank.Presentacion/*.csproj ./
RUN dotnet restore
COPY src/GrupoDigitalBank.Presentacion/ ./
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Claves de Data Protection (se monta un volumen aqui, ver docker-compose.yml). El directorio debe existir en
# la imagen con el dueño no root: un volumen nuevo hereda ese dueño y el proceso puede escribir en el.
RUN mkdir /keys && chown $APP_UID /keys
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "GrupoDigitalBank.Presentacion.dll"]
