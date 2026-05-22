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
