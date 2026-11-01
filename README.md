# Caskly API (xoxo)

ASP.NET Core (.NET 10) + EF Core + SQL Server. Today the Caskly app runs
fully offline; this API is what counters sync to once a store goes online
(several counters, owner dashboards, backups off-site).

## Layout

| Path | What |
|---|---|
| `Xoxo.Api/Program.cs` | Startup only: config, auth, health, endpoint registration |
| `Xoxo.Api/Endpoints/` | One file per feature (sales, purchases, masters, lookups, inventory, reports, dashboard, devices, sync) |
| `Xoxo.Api/Services/BillingService.cs` | Sale / purchase / return rules, shared by REST and sync |
| `Xoxo.Api/Auth/DeviceAuth.cs` | Per-counter API keys |
| `Xoxo.Infrastructure/Data/` | `AppDbContext` (scaffolded) + `AppDbContext.Online.cs` (new tables), models, migrations |
| `Xoxo.Api.Tests/` | Integration tests — the real API in-process on in-memory SQLite |

## Run locally

```bash
cp .env.example .env            # pick a password
docker compose up -d            # SQL Server on localhost:1433
dotnet tool restore
dotnet user-secrets set "ConnectionStrings:Default" \
  "Server=localhost,1433;Database=Caskly;User Id=sa;Password=<your .env password>;TrustServerCertificate=True" \
  --project Xoxo.Api
dotnet ef database update --project Xoxo.Infrastructure --startup-project Xoxo.Infrastructure \
  --connection "<same connection string>"
dotnet run --project Xoxo.Api   # Swagger at /swagger (Development only)
```

In Development, device auth is off (`appsettings.Development.json`).
Everywhere else it is on.

### Existing database created before migrations (e.g. `Product6Pos`)

Run `scripts/adopt_migrations_existing_db.sql` once against it, then
`dotnet ef database update`. That marks the **Baseline** migration as
already applied and adds only the new tables/columns.

## Secrets

Nothing secret is committed. Provide at runtime via user-secrets or
environment variables:

| Setting | Env var | Notes |
|---|---|---|
| `ConnectionStrings:Default` | `ConnectionStrings__Default` | Required; the API refuses to start without it |
| `Auth:EnrollmentCode` | `Auth__EnrollmentCode` | Code a new counter needs to register. Empty = registration closed |
| `Auth:RequireDeviceKey` | `Auth__RequireDeviceKey` | Default `true` |
| `Database:MigrateOnStartup` | `Database__MigrateOnStartup` | Apply pending migrations at boot |

> The old SQL `sa` password was committed in `appsettings.json` before
> this change and is still in git history. **Change that password** on
> any server where it was used.

## Counters (devices)

1. Set `Auth:EnrollmentCode` on the server.
2. The app registers once:
   `POST /api/devices/register {deviceId, deviceName, storeName, enrollmentCode}`
   → `{deviceId, storeId, apiKey}`. `deviceId` is the app's own `device_id`.
   The key is shown only once; registering again rotates it.
3. Every `/api/*` call sends `X-Device-Id` and `X-Device-Key`
   (and optionally `X-Caskly-User` for audit columns). `/health` is public.

## Sync protocol

1. `POST /api/sync/masters` — upload the counter's categories, manufacturers,
   packings, permit holders, materials, suppliers (keyed by the counter's own
   ids) and, on first connection, opening stock. Idempotent; safe every sync.
   Opening stock only seeds materials the server has no stock for.
2. `POST /api/sync/push` with up to 200 queued items:

```json
{ "items": [
  { "clientId": "local-sale-…", "entityType": "sale", "operation": "create",
    "payload": { "billNo": "CS-2627-000042", "billDate": "2026-10-28", "permitNo": "20598", "lineItems": [ … ] } },
  { "clientId": "local-sale-…:return", "entityType": "sale", "operation": "return",
    "payload": { "id": "local-sale-…" } }
] }
```

Response, per item: `applied` | `duplicate` | `rejected` (+ `serverId`,
`billNo`, `error`).

- **Idempotent:** `(device, clientId)` is recorded with the result. A
  retry after a lost response returns `duplicate` with the original
  `serverId` and nothing is applied twice. Drop both `applied` and
  `duplicate` items from the device queue.
- **All-or-nothing per item;** one bad item never blocks the rest.
- **Dates:** send `billDate` — a sale synced days later keeps its real date.
- **Bill numbers** are unique per counter (two counters may both issue
  `CS-2627-000001`).
- **Purchases** may reference suppliers by the counter's own id (`SUP-001`),
  resolved via `Suppliers.ClientId` — upload masters first.
- **Returns** reference the bill by server id or by the create's clientId.
  Returned bills are kept (`status = returned`, `ReturnedAt/By`) and every
  returned line is logged in `SalesReturnEntries` / `PurchaseReturnEntries`.

## Tests

```bash
dotnet test
```

CI (`.github/workflows/ci.yml`) also applies all migrations to a real SQL
Server container and boots the API against it.
