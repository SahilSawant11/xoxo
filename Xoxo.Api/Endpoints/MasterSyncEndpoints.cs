using Microsoft.EntityFrameworkCore;
using Xoxo.Infrastructure.Data;
using Xoxo.Infrastructure.Data.Models;

namespace Xoxo.Api.Endpoints;

public sealed record SyncNamed(string Name, string? Description);
public sealed record SyncPermitHolder(string PermitNo, string Name, string? IssuedDate, string? WhereGranted);
public sealed record SyncMaterial(string Id, string Barcode, string Name, string Category, string Packing,
    decimal SaleRate, decimal TaxPercent, string? Manufacturer, bool IsActive = true);
public sealed record SyncSupplier(string ClientId, string Name, string? Address, string? ContactNo, string? Email,
    string? VatNo, string? BankDetails, decimal DisPercent, decimal OpeningBalance, string? BalanceType);
public sealed record SyncOpeningStock(string MaterialId, int QtyOnHand, int ReorderLevel = 10);

public sealed record SyncMastersRequest(
    List<SyncNamed>? Categories,
    List<SyncNamed>? Manufacturers,
    List<SyncNamed>? Packings,
    List<SyncPermitHolder>? LicenseHolders,
    List<SyncMaterial>? Materials,
    List<SyncSupplier>? Suppliers,
    List<SyncOpeningStock>? OpeningStock);

/// <summary>
/// Upserts a counter's master data so its bills can reference it. Safe to
/// call on every sync. Opening stock is applied only for materials the
/// server has no stock rows for (first connection), so it never overwrites
/// movements the server has been tracking.
/// </summary>
public static class MasterSyncEndpoints
{
    public static IEndpointRouteBuilder MapMasterSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sync/masters", async (SyncMastersRequest r, AppDbContext db) =>
        {
            var now = DateTime.UtcNow;
            var errors = new List<string>();
            int upserts = 0;

            foreach (var c in r.Categories ?? [])
            {
                var name = c.Name?.Trim();
                if (string.IsNullOrEmpty(name)) continue;
                var row = await db.Categories.FindAsync(name);
                if (row is null) db.Categories.Add(new Category { Name = name, Description = c.Description, CreatedAt = now, UpdatedAt = now });
                else { row.Description = c.Description; row.UpdatedAt = now; }
                upserts++;
            }
            foreach (var m in r.Manufacturers ?? [])
            {
                var name = m.Name?.Trim();
                if (string.IsNullOrEmpty(name)) continue;
                var row = await db.Manufacturers.FindAsync(name);
                if (row is null) db.Manufacturers.Add(new Manufacturer { Name = name, Description = m.Description, CreatedAt = now, UpdatedAt = now });
                else { row.Description = m.Description; row.UpdatedAt = now; }
                upserts++;
            }
            foreach (var p in r.Packings ?? [])
            {
                var name = p.Name?.Trim();
                if (string.IsNullOrEmpty(name)) continue;
                var row = await db.Packings.FindAsync(name);
                if (row is null) db.Packings.Add(new Packing { Name = name, Description = p.Description, CreatedAt = now, UpdatedAt = now });
                else { row.Description = p.Description; row.UpdatedAt = now; }
                upserts++;
            }
            foreach (var h in r.LicenseHolders ?? [])
            {
                var permit = h.PermitNo?.Trim();
                if (string.IsNullOrEmpty(permit)) continue;
                var row = await db.PermitHolders.FirstOrDefaultAsync(x => x.PermitNo == permit);
                if (row is null)
                    db.PermitHolders.Add(new PermitHolder { Id = permit, PermitNo = permit, Name = h.Name, IssuedDate = h.IssuedDate, WhereGranted = h.WhereGranted, CreatedAt = now, UpdatedAt = now });
                else { row.Name = h.Name; row.IssuedDate = h.IssuedDate; row.WhereGranted = h.WhereGranted; row.UpdatedAt = now; }
                upserts++;
            }
            foreach (var m in r.Materials ?? [])
            {
                if (string.IsNullOrWhiteSpace(m.Id)) continue;
                var barcode = string.IsNullOrWhiteSpace(m.Barcode) ? m.Id : m.Barcode;
                var clash = await db.Materials.AnyAsync(x => x.Barcode == barcode && x.Id != m.Id);
                if (clash) { errors.Add($"Material {m.Id}: barcode {barcode} already belongs to another material."); continue; }
                var row = await db.Materials.FindAsync(m.Id);
                if (row is null)
                {
                    row = new Material { Id = m.Id, CreatedAt = now };
                    db.Materials.Add(row);
                }
                row.Barcode = barcode;
                row.Name = m.Name;
                row.Category = m.Category;
                row.Packing = m.Packing;
                row.SaleRate = m.SaleRate;
                row.TaxPercent = m.TaxPercent;
                row.Manufacturer = m.Manufacturer ?? "";
                row.IsActive = m.IsActive;
                row.UpdatedAt = now;
                upserts++;
            }
            foreach (var s in r.Suppliers ?? [])
            {
                if (string.IsNullOrWhiteSpace(s.ClientId) || string.IsNullOrWhiteSpace(s.Name)) continue;
                var row = await db.Suppliers.FirstOrDefaultAsync(x => x.ClientId == s.ClientId);
                if (row is null)
                {
                    row = new Supplier { Id = Guid.NewGuid(), ClientId = s.ClientId, CreatedAt = now };
                    db.Suppliers.Add(row);
                }
                row.Name = s.Name;
                row.Address = s.Address;
                row.ContactNo = s.ContactNo;
                row.Email = s.Email;
                row.VatNo = s.VatNo;
                row.BankDetails = s.BankDetails;
                row.DisPercent = s.DisPercent;
                row.OpeningBalance = s.OpeningBalance;
                row.BalanceType = string.IsNullOrWhiteSpace(s.BalanceType) ? "Credit" : s.BalanceType;
                row.UpdatedAt = now;
                upserts++;
            }
            await db.SaveChangesAsync();

            int seeded = 0;
            foreach (var o in r.OpeningStock ?? [])
            {
                if (!await db.Materials.AnyAsync(m => m.Id == o.MaterialId)) continue;
                if (await db.InventoryStocks.AnyAsync(s => s.MaterialId == o.MaterialId)) continue;
                db.InventoryStocks.Add(new InventoryStock
                {
                    Id = Guid.NewGuid(), MaterialId = o.MaterialId, BatchNo = "-", QtyOnHand = o.QtyOnHand,
                    ReorderLevel = o.ReorderLevel, UpdatedAt = now,
                });
                seeded++;
            }
            await db.SaveChangesAsync();

            return Results.Ok(new { upserts, openingStockSeeded = seeded, errors });
        })
        .WithName("SyncMasters");

        return app;
    }
}
