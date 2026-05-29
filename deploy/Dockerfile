# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ProPlusBot/ProPlusBot.csproj src/ProPlusBot/
RUN dotnet restore src/ProPlusBot/ProPlusBot.csproj
COPY src/ProPlusBot/ src/ProPlusBot/
RUN dotnet publish src/ProPlusBot/ProPlusBot.csproj -c Release -o /app/publish

# Run
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates fonts-dejavu-core \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

RUN mkdir -p /app/data/dataprotection-keys /app/tools /app/media

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV DataProtection__KeysPath=/app/data/dataprotection-keys
ENV Download__ToolsDirectory=/app/tools
ENV Download__MediaDirectory=/app/media
ENV Download__AutoDownloadYtDlp=true
ENV Download__AutoDownloadGalleryDl=true
ENV Download__AutoDownloadFfmpeg=true
ENV Download__AutoDownloadDeno=true

EXPOSE 8080
ENTRYPOINT ["dotnet", "ProPlusBot.dll"]
