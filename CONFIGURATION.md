# Development vs production configuration

The app uses ASP.NET Core environments (`Development` / `Production`) so you can run a **separate Bale bot** and **separate PostgreSQL database** locally without touching production.

## Local development

1. Ensure `ASPNETCORE_ENVIRONMENT` is `Development` (default in `Properties/launchSettings.json` and VS Code `launch.json`).
2. Copy the example file:
   ```bash
   copy src\ProPlusBot\appsettings.Development.json.example src\ProPlusBot\appsettings.Development.json
   ```
3. Edit `appsettings.Development.json`:
   - `ConnectionStrings:DefaultConnection` → your local Postgres, e.g. database `ProPlusBotDb_Dev`
   - `Bot:Token` → token from your **development** Bale bot (not production)
   - `Bot:RequiredChannelUsername` / `RequiredChannelId` → dev channel if you use one
   - `SuperAdmin:PhoneNumber` → your test phone

`appsettings.Development.json` is **gitignored** and never committed.

### Optional: user secrets (no JSON file)

```bash
cd src/ProPlusBot
dotnet user-secrets set "Bot:Token" "YOUR_DEV_BOT_TOKEN"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=ProPlusBotDb_Dev;Username=postgres;Password=..."
```

User secrets override `appsettings.Development.json` when both are set.

## Production

Set secrets on the server (recommended), not in git:

| Setting | Environment variable |
|--------|----------------------|
| Database | `ConnectionStrings__DefaultConnection` |
| Bale bot token | `Bot__Token` |
| Payment live wallet | `Payment__LiveProviderToken` |
| Super admin phone | `SuperAdmin__PhoneNumber` |

Or use `appsettings.Production.local.json` on the server (also gitignored via `appsettings.*.local.json`).

Run with:

```bash
set ASPNETCORE_ENVIRONMENT=Production
dotnet run --project src/ProPlusBot
```

## Create the dev database

Use a **different database name** than production, e.g. `ProPlusBotDb_Dev`. Migrations run automatically on startup via `DatabaseInitializer`.

## Verify which environment is active

On startup the log shows:

- `Running as Development` or `Production`
- Database host/name (not password)
- Masked bot token prefix/suffix

**Never run long polling with the production bot token on your machine** while production is also running — use only the dev bot locally.

## Docker (PostgreSQL + Redis + app)

1. Copy the example env file and set secrets (bot token, JWT secret, Postgres/Redis passwords):

   ```bash
   copy .env.example .env
   ```

2. Start PostgreSQL, Redis, and the app:

   ```bash
   docker compose up -d --build
   ```

3. Open `http://localhost:8080` (or `APP_HTTP_PORT` from `.env`).

Compose mounts volumes for `tools`, `media`, and `data` (Data Protection keys). Migrations run on app startup.

On first start the app auto-downloads yt-dlp, gallery-dl, ffmpeg, and Deno into `/app/tools` (same as a normal run with `Download:AutoDownload*` enabled). The `app_tools` volume keeps them across restarts.

| Variable | Purpose |
|----------|---------|
| `POSTGRES_*` | Database container |
| `REDIS_*` | Redis container (conversation state when enabled in admin Settings) |
| `ConnectionStrings__DefaultConnection` | Overridden in compose to use host `db` |
| `Redis__ConnectionString` | Overridden in compose to use host `redis` |
| `Bot__Token`, `Jwt__Secret` | Required by the app |
| `Download__ToolsDirectory`, `Download__MediaDirectory` | Persisted download/tool paths |
| `DataProtection__KeysPath` | Admin cookie keys across restarts |

In admin **Settings**, set **session storage** to **Redis** to use the Redis container instead of in-memory state.

`.env` is gitignored; `.env.example` documents recommended overrides.
