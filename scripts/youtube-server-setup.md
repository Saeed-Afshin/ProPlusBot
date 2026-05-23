# YouTube on the server (no manual Deno install)

ProPlusBot downloads **yt-dlp**, **ffmpeg**, **gallery-dl**, and **Deno** into the writable **`tools/`** folder at startup. You do **not** need shell access to install Deno on a managed host.

Your error log showed:

| Issue | Fix in ProPlusBot |
|--------|-------------------|
| `Ignoring unsupported remote component(s): ejs` | Use **`ejs:github`** (default) |
| `JS runtimes: none` | **`AutoDownloadDeno: true`** → `tools/deno/bin/deno` |
| `n challenge solving failed` | Bundled Deno + `ejs:github` |
| `GVS PO Token … not provided` | Optional env `YouTubePoToken` (hosting panel) |

References: [EJS wiki](https://github.com/yt-dlp/yt-dlp/wiki/EJS) · [PO Token Guide](https://github.com/yt-dlp/yt-dlp/wiki/PO-Token-Guide) · [Cookies](https://github.com/yt-dlp/yt-dlp/wiki/Extractors#exporting-youtube-cookies)

---

## Managed server checklist (no SSH install)

1. **Writable `tools/`** — same directory as auto-downloaded yt-dlp (usually under app publish root). If the disk is read-only, set `MediaDownload:ToolsDirectory` to a writable path (e.g. `/tmp/ProPlusBot/tools`).

2. **Outbound HTTPS** — app must reach `github.com` (Deno + EJS scripts + yt-dlp).

3. **Defaults** (already in `appsettings.json`):

```json
"AutoDownloadDeno": true,
"YouTubeRemoteComponents": "ejs:github",
"YouTubeExtractorArgs": "youtube:player_client=tv,web_safari"
```

4. **Cookies** — admin **Settings** → paste Netscape export with **SID** / **LOGIN_INFO**. See [youtube-cookies-setup.md](./youtube-cookies-setup.md).

5. **Redeploy / restart** — first start downloads Deno (~40–50 MB once). Check logs:

```text
Downloading Deno for YouTube EJS from https://github.com/...
YouTube JS runtime (deno:.../tools/deno/bin/deno): deno 2.x.x
yt-dlp version: 2026...
```

6. **Optional PO token** (only if formats still missing after Deno works) — set in hosting env **without SSH**:

```text
MediaDownload__YouTubePoToken=mweb.gvs+YOUR_TOKEN
```

Manual tokens expire quickly; a [PO Token Provider plugin](https://github.com/yt-dlp/yt-dlp/wiki/PO-Token-Guide) is better for production but needs plugin install (not supported on fully locked hosts).

---

## Environment variables (hosting panel)

```text
MediaDownload__ToolsDirectory=/tmp/ProPlusBot/tools
MediaDownload__AutoDownloadDeno=true
MediaDownload__YouTubeRemoteComponents=ejs:github
MediaDownload__YouTubeExtractorArgs=youtube:player_client=tv,web_safari
```

Leave `YouTubeJsRuntimes` empty to use bundled Deno.

To disable Deno download (only if you bundle `tools/deno/bin/deno` in your image):

```text
MediaDownload__AutoDownloadDeno=false
MediaDownload__YouTubeDenoPath=/app/publish/tools/deno/bin/deno
MediaDownload__YouTubeJsRuntimes=deno:/app/publish/tools/deno/bin/deno
```

---

## Verify after deploy

Admin → **گزارش خطاها** should no longer show `JS runtimes: none`.

If you have exec access for debugging only:

```bash
ls -la /path/to/tools/deno/bin/deno
/path/to/tools/deno/bin/deno --version
```

---

## PO token without server install

You **cannot** run bgutil on the server without installing something. Alternatives:

| Approach | Needs server install? |
|----------|------------------------|
| **Bundled Deno** (this app) | No — auto-download |
| **`YouTubePoToken` env var** | No — paste token from your PC |
| **PO Token Provider plugin** | Yes — yt-dlp plugin + often a sidecar |
| **External token API** | No — if you host bgutil elsewhere and pass token via env |

For a quick test from your PC: generate a token per [PO Token Guide](https://github.com/yt-dlp/yt-dlp/wiki/PO-Token-Guide), set `MediaDownload__YouTubePoToken` in the panel, restart the app.

---

## Troubleshooting

| Symptom | Action |
|---------|--------|
| `Failed to download Deno` | Check HTTPS, disk space, writable `tools/` |
| Still storyboard-only | Confirm log shows `YouTube JS runtime (deno:...)` |
| `ejs` ignored | Must be `ejs:github`, not `ejs` |
| Without cookies: “not a bot” | Refresh cookies in admin Settings |
| With Deno OK but PO warning | Set `YouTubePoToken` or use `tv` client only |
