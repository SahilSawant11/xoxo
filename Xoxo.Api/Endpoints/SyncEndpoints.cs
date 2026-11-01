using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xoxo.Api.Auth;
using Xoxo.Api.Contracts;
using Xoxo.Api.Services;
using Xoxo.Infrastructure.Data;
using Xoxo.Infrastructure.Data.Models;

namespace Xoxo.Api.Endpoints;

/// <param name="ClientId">Unique per queue item on the device (e.g. the bill's local id, or "&lt;id&gt;:return").</param>
/// <param name="EntityType">"sale" or "purchase".</param>
/// <param name="Operation">"create" or "return".</param>
/// <param name="Payload">Create: the same body as POST /api/sales or /api/purchases. Return: {"id": server id or the create's clientId}.</param>
public sealed record SyncItem(string ClientId, string EntityType, string Operation, JsonElement Payload);

public sealed record SyncPushRequest(List<SyncItem> Items);

/// <param name="Status">"applied", "duplicate" (already applied earlier — safe to drop from the queue) or "rejected" (fix and retry, or surface to the user).</param>
public sealed record SyncItemResult(string ClientId, string Status, string? ServerId = null, string? BillNo = null, string? Error = null);

public static class SyncEndpoints
{
    public const int MaxItemsPerPush = 200;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sync/push", async (SyncPushRequest request, HttpContext http, AppDbContext db, BillingService billing) =>
        {
            var deviceId = http.Device()?.DeviceId
                ?? (Guid.TryParse(http.Request.Headers[DeviceKeys.DeviceIdHeader], out var id) ? id : (Guid?)null);
            if (deviceId is null) return Results.BadRequest(new { message = $"{DeviceKeys.DeviceIdHeader} header is required for sync." });
            if (request.Items is null || request.Items.Count == 0) return Results.Ok(new { results = Array.Empty<SyncItemResult>() });
            if (request.Items.Count > MaxItemsPerPush)
                return Results.BadRequest(new { message = $"Push at most {MaxItemsPerPush} items at a time." });

            var results = new List<SyncItemResult>(request.Items.Count);
            foreach (var item in request.Items)
                results.Add(await ApplyAsync(item, deviceId.Value, http, db, billing));
            return Results.Ok(new { results });
        })
        .WithName("SyncPush");

        return app;
    }

    private static async Task<SyncItemResult> ApplyAsync(SyncItem item, Guid deviceId, HttpContext http, AppDbContext db, BillingService billing)
    {
        if (string.IsNullOrWhiteSpace(item.ClientId)) return new(item.ClientId ?? "", "rejected", Error: "clientId is required.");

        var receipt = await db.SyncReceipts.AsNoTracking()
            .FirstOrDefaultAsync(r => r.DeviceId == deviceId && r.ClientId == item.ClientId);
        if (receipt != null) return new(item.ClientId, "duplicate", receipt.ServerId);

        var origin = http.Origin(item.ClientId) with { DeviceId = deviceId };
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var result = (item.EntityType, item.Operation) switch
            {
                ("sale", "create") => await billing.CreateSaleAsync(Deserialize<CreateSaleRequest>(item.Payload), origin),
                ("purchase", "create") => await billing.CreatePurchaseAsync(Deserialize<CreatePurchaseRequest>(item.Payload), origin),
                ("sale", "return") => await billing.ReturnSaleAsync(await FindSaleAsync(db, deviceId, item.Payload), origin),
                ("purchase", "return") => await billing.ReturnPurchaseAsync(await FindPurchaseAsync(db, deviceId, item.Payload), origin),
                _ => ServiceResult.Fail($"Unsupported sync item: {item.EntityType}/{item.Operation}."),
            };
            if (!result.Ok)
            {
                await tx.RollbackAsync();
                db.ChangeTracker.Clear();
                return new(item.ClientId, "rejected", Error: result.Error);
            }

            db.SyncReceipts.Add(new SyncReceipt
            {
                DeviceId = deviceId,
                ClientId = item.ClientId,
                EntityType = item.EntityType,
                Operation = item.Operation,
                ServerId = result.Id?.ToString(),
                ReceivedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            db.ChangeTracker.Clear();
            return new(item.ClientId, "applied", result.Id?.ToString(), result.BillNo);
        }
        catch (JsonException e)
        {
            await tx.RollbackAsync();
            db.ChangeTracker.Clear();
            return new(item.ClientId, "rejected", Error: $"Invalid payload: {e.Message}");
        }
        catch (DbUpdateException)
        {
            // Most likely a concurrent retry of the same item won the race.
            await tx.RollbackAsync();
            db.ChangeTracker.Clear();
            var again = await db.SyncReceipts.AsNoTracking()
                .FirstOrDefaultAsync(r => r.DeviceId == deviceId && r.ClientId == item.ClientId);
            return again != null
                ? new(item.ClientId, "duplicate", again.ServerId)
                : new(item.ClientId, "rejected", Error: "Database rejected the item.");
        }
    }

    private static T Deserialize<T>(JsonElement payload) =>
        payload.Deserialize<T>(Json) ?? throw new JsonException("Empty payload.");

    /// <summary>A return refers to its bill by server id or by the create's clientId.</summary>
    private static string? RefId(JsonElement payload) =>
        payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("id", out var v) ? v.GetString() : null;

    private static async Task<SalesBill?> FindSaleAsync(AppDbContext db, Guid deviceId, JsonElement payload)
    {
        var refId = RefId(payload);
        if (refId is null) return null;
        var q = db.SalesBills.Include(b => b.SaleLineItems);
        return Guid.TryParse(refId, out var id)
            ? await q.FirstOrDefaultAsync(b => b.Id == id)
            : await q.FirstOrDefaultAsync(b => b.DeviceId == deviceId && b.ClientId == refId);
    }

    private static async Task<PurchaseBill?> FindPurchaseAsync(AppDbContext db, Guid deviceId, JsonElement payload)
    {
        var refId = RefId(payload);
        if (refId is null) return null;
        var q = db.PurchaseBills.Include(b => b.PurchaseLineItems);
        return Guid.TryParse(refId, out var id)
            ? await q.FirstOrDefaultAsync(b => b.Id == id)
            : await q.FirstOrDefaultAsync(b => b.DeviceId == deviceId && b.ClientId == refId);
    }
}
