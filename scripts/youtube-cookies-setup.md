
# YouTube cookies setup (Chrome & Firefox)

Also available as **[youtube-cookies-setup.html](./youtube-cookies-setup.html)** (open in a browser).

ProPlusBot uses **yt-dlp** with a Netscape-format `cookies.txt` so YouTube does not block downloads with “Sign in to confirm you’re not a bot.”

**Do not commit real cookie files to git.** Keep them local or on the server only.

---

## Quick workflow

1. Export cookies from Chrome or Firefox (below).
2. Save as `scripts/youtube-cookies.txt` (overwrite the placeholder).
3. Optional: run `scripts/encode-youtube-cookies.bat` → `youtube-cookies.b64.txt` for env vars.
4. On the server, use **one** of:
   - **Admin panel** → Settings → paste Netscape cookies or Base64 (stored in DB, **no redeploy**), **or**
   - Copy `youtube-cookies.txt` to `src/ProPlusBot/tools/youtube-cookies.txt`, **or**
   - Upload `youtube-cookies.b64.txt` to `tools/` (see server section below).

See [yt-dlp FAQ – passing cookies](https://github.com/yt-dlp/yt-dlp/wiki/FAQ#how-do-i-pass-cookies-to-yt-dlp).

---

## Safety tips

- Use a **dedicated Google account** for the bot, not your personal account.
- Install extensions only from official store links below; avoid copycat names.
- Treat `youtube-cookies.txt` like a **password** — anyone with it can use your YouTube session until cookies expire.
- Refresh cookies every few weeks when downloads start failing again.

---

## Google Chrome

### 1. Install extension

**[Get cookies.txt LOCALLY](https://chromewebstore.google.com/detail/get-cookiestxt-locally/cclelndahbckbenkjhflpdbgdldlbecc)**  
(Open-source; exports to a file on your PC only.)

### 2. Export

1. Open a new **Incognito** window (`Ctrl+Shift+N`).
2. Go to [https://www.youtube.com](https://www.youtube.com) and confirm you can **play a video**.
3. Click the extension icon → export **current site** or YouTube cookies.
4. Save the file as:
   ```
   D:\ProPlusBot\scripts\youtube-cookies.txt
   ```
   (overwrite the placeholder)

### 3. Encode for server env (optional)

```bat
cd D:\ProPlusBot\scripts
encode-youtube-cookies.bat
```

Output: `youtube-cookies.b64.txt` + clipboard.  
Set on server: `Download__YouTubeCookiesBase64=<one line from file>` then **restart** the app.

---

## Mozilla Firefox

### 1. Install extension

**[cookies.txt](https://addons.mozilla.org/en-US/firefox/addon/cookies-txt/)** (by rahulbot)

### 2. Export

1. Open a new **Private** window (`Ctrl+Shift+P`).
2. Open [https://www.youtube.com](https://www.youtube.com) and play a video.
3. Extension → export cookies for YouTube / current site.
4. Save as `scripts\youtube-cookies.txt`.

### 3. Encode (optional)

Same as Chrome: run `encode-youtube-cookies.bat`.

---

## Server configuration

### Option A — Admin panel (recommended)

1. Open the bot **Settings** page (`/Settings`) as an admin.
2. Under **کوکی یوتیوب**, paste either:
   - Full **Netscape** `cookies.txt` content, or
   - One-line **Base64** from `encode-youtube-cookies.bat`
3. Click **ذخیره**. Takes effect on the next YouTube download — **no redeploy**, no env var size limits.
4. When cookies expire, paste new content and save again. Use **حذف کوکی** to clear.

Admin cookies override files/env vars when set.

### Option B — Cookie file on disk

Copy `youtube-cookies.txt` to the app `tools` folder on the server:

| Deploy type | Path |
|-------------|------|
| Local / IIS | `src\ProPlusBot\tools\youtube-cookies.txt` |
| Docker | Mount volume → `/app/tools/youtube-cookies.txt` |

When cookies expire: replace the file only. **No app redeploy.** Usually **no restart.**

Config (optional):

```json
"Download": {
  "YouTubeCookiesFile": "youtube-cookies.txt"
}
```

### Option C — Base64 file on server (no long env var)

Many hosts **reject env vars over ~1024–4096 characters** (a 2 KB `.b64.txt` is too large for the panel).

1. Run `encode-youtube-cookies.bat` on your PC.
2. Upload `youtube-cookies.b64.txt` to the server `tools/` folder (SFTP, volume mount, etc.).
3. Optional short env var: `Download__YouTubeCookiesBase64File=youtube-cookies.b64.txt`  
   (If the file is at `tools/youtube-cookies.b64.txt`, the app finds it automatically.)
4. **Restart** the app after the first upload or when you replace the file.

### Option D — Inline Base64 environment variable

Only if your host allows **long** env values (often **≤ 1024** chars on Windows `setx` / small panels):

`Download__YouTubeCookiesBase64=<entire one line from .b64.txt>`

Then **restart** the app.

**Priority:** `youtube-cookies.txt` on disk wins over any Base64 source.

---

## Player client & PO token (server)

Cookies with `SID` / `LOGIN_INFO` are necessary but not always enough. YouTube may return **only storyboard** rows (`sb0`–`sb3`) if yt-dlp uses the wrong client (e.g. `web` / `mweb` without a **PO token**).

**Defaults in `appsettings.json`:**

```json
"YouTubeExtractorArgs": "youtube:player_client=tv,web_safari",
"YouTubeRemoteComponents": "ejs:github",
"YouTubeJsRuntimes": "deno"
```

Install **Deno** (or Node 20+) on the server — see **[youtube-server-setup.md](./youtube-server-setup.md)**. Plain `"ejs"` is ignored; use `ejs:github`.

On the server, **do not** override with `Download__YouTubeExtractorArgs=youtube:player_client=web` unless you also supply a PO token ([PO Token Guide](https://github.com/yt-dlp/yt-dlp/wiki/PO-Token-Guide)).

Optional env:

```text
Download__YouTubePoToken=web.gvs+YOUR_TOKEN
```

After deploy, logs should show `yt-dlp version: …` (use a recent release). Restart the app so the bundled yt-dlp binary can auto-update on startup if `AutoDownloadYtDlp` is enabled.

---

## Verify

After starting the bot, check logs for:

```text
YouTube cookies ready (admin panel): /tmp/ProPlusBot/youtube-cookies/youtube-cookies.active.txt
yt-dlp version: 2025.xx.xx
```

If you see a warning that cookies are missing, the path or mount is wrong.

---

## Files in `scripts/`

| File | Purpose |
|------|---------|
| `youtube-cookies-setup.html` | This guide (browser) |
| `youtube-cookies-setup.md` | This guide (Markdown) |
| `youtube-cookies.sample.txt` | Empty template (safe to commit) |
| `youtube-cookies.txt` | Your export (keep private) |
| `encode-youtube-cookies.bat` | Build Base64 for env vars |
| `youtube-cookies.b64.txt` | Generated Base64 (keep private) |
| `pack.bat` | Zip `src\ProPlusBot` for deployment |
