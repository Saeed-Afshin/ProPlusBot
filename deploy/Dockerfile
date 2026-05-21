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
    && apt-get install -y --no-install-recommends ca-certificates curl fonts-dejavu-core \
    && curl -fsSL "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux" -o /usr/local/bin/yt-dlp \
    && chmod +x /usr/local/bin/yt-dlp \
    && curl -fsSL "https://codeberg.org/mikf/gallery-dl/releases/download/v1.32.1/gallery-dl.bin" -o /usr/local/bin/gallery-dl \
    && chmod +x /usr/local/bin/gallery-dl \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV MediaDownload__YtDlpPath=/usr/local/bin/yt-dlp
ENV MediaDownload__GalleryDlPath=/usr/local/bin/gallery-dl
ENV MediaDownload__AutoDownloadYtDlp=false
ENV MediaDownload__AutoDownloadGalleryDl=false
ENV MediaDownload__AutoDownloadFfmpeg=true

EXPOSE 8080
ENTRYPOINT ["dotnet", "ProPlusBot.dll"]
