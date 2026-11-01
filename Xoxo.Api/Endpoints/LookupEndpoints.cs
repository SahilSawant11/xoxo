using Microsoft.EntityFrameworkCore;
using Xoxo.Infrastructure.Data;
using Xoxo.Infrastructure.Data.Models;

namespace Xoxo.Api.Endpoints;

public sealed record SaveNamedLookupRequest(string Name, string? Description);
public sealed record SavePermitHolderRequest(string PermitNo, string Name, string? IssuedDate, string? WhereGranted);

/// <summary>
/// Categories, manufacturers, packing sizes (keyed by name — renaming
/// cascades to materials) and excise permit holders.
/// </summary>
public static class LookupEndpoints
{
    public static IEndpointRouteBuilder MapLookupEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Categories ───────────────────────────────────────────────
        app.MapGet("/api/categories", async (AppDbContext db) =>
            Results.Ok(await db.Categories.OrderBy(c => c.Name).ToListAsync())).WithName("GetCategories");

        app.MapPost("/api/categories", async (SaveNamedLookupRequest r, AppDbContext db) =>
        {
            var name = r.Name?.Trim();
            if (string.IsNullOrEmpty(name)) return Results.BadRequest(new { message = "Name is required." });
            if (await db.Categories.AnyAsync(c => c.Name == name)) return Results.Conflict(new { message = $"Category '{name}' already exists." });
            var now = DateTime.UtcNow;
            var c = new Category { Name = name, Description = r.Description, CreatedAt = now, UpdatedAt = now };
            db.Categories.Add(c);
            await db.SaveChangesAsync();
            return Results.Created($"/api/categories/{Uri.EscapeDataString(name)}", c);
        }).WithName("CreateCategory");

        app.MapPut("/api/categories/{name}", async (string name, SaveNamedLookupRequest r, AppDbContext db) =>
        {
            var existing = await db.Categories.FindAsync(name);
            if (existing is null) return Results.NotFound();
            var newName = r.Name?.Trim();
            if (string.IsNullOrEmpty(newName)) return Results.BadRequest(new { message = "Name is required." });
            if (newName == name)
            {
                existing.Description = r.Description;
                existing.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return Results.Ok(existing);
            }
            if (await db.Categories.AnyAsync(c => c.Name == newName)) return Results.Conflict(new { message = $"Category '{newName}' already exists." });
            // Key change: replace the row and re-point materials, atomically.
            await using var tx = await db.Database.BeginTransactionAsync();
            var renamed = new Category { Name = newName, Description = r.Description, CreatedAt = existing.CreatedAt, UpdatedAt = DateTime.UtcNow };
            db.Categories.Remove(existing);
            db.Categories.Add(renamed);
            await db.SaveChangesAsync();
            await db.Materials.Where(m => m.Category == name).ExecuteUpdateAsync(s => s.SetProperty(m => m.Category, newName));
            await tx.CommitAsync();
            return Results.Ok(renamed);
        }).WithName("UpdateCategory");

        // ── Manufacturers ────────────────────────────────────────────
        app.MapGet("/api/manufacturers", async (AppDbContext db) =>
            Results.Ok(await db.Manufacturers.OrderBy(m => m.Name).ToListAsync())).WithName("GetManufacturers");

        app.MapPost("/api/manufacturers", async (SaveNamedLookupRequest r, AppDbContext db) =>
        {
            var name = r.Name?.Trim();
            if (string.IsNullOrEmpty(name)) return Results.BadRequest(new { message = "Name is required." });
            if (await db.Manufacturers.AnyAsync(m => m.Name == name)) return Results.Conflict(new { message = $"Manufacturer '{name}' already exists." });
            var now = DateTime.UtcNow;
            var m = new Manufacturer { Name = name, Description = r.Description, CreatedAt = now, UpdatedAt = now };
            db.Manufacturers.Add(m);
            await db.SaveChangesAsync();
            return Results.Created($"/api/manufacturers/{Uri.EscapeDataString(name)}", m);
        }).WithName("CreateManufacturer");

        app.MapPut("/api/manufacturers/{name}", async (string name, SaveNamedLookupRequest r, AppDbContext db) =>
        {
            var existing = await db.Manufacturers.FindAsync(name);
            if (existing is null) return Results.NotFound();
            var newName = r.Name?.Trim();
            if (string.IsNullOrEmpty(newName)) return Results.BadRequest(new { message = "Name is required." });
            if (newName == name)
            {
                existing.Description = r.Description;
                existing.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return Results.Ok(existing);
            }
            if (await db.Manufacturers.AnyAsync(m => m.Name == newName)) return Results.Conflict(new { message = $"Manufacturer '{newName}' already exists." });
            await using var tx = await db.Database.BeginTransactionAsync();
            var renamed = new Manufacturer { Name = newName, Description = r.Description, CreatedAt = existing.CreatedAt, UpdatedAt = DateTime.UtcNow };
            db.Manufacturers.Remove(existing);
            db.Manufacturers.Add(renamed);
            await db.SaveChangesAsync();
            await db.Materials.Where(m => m.Manufacturer == name).ExecuteUpdateAsync(s => s.SetProperty(m => m.Manufacturer, newName));
            await tx.CommitAsync();
            return Results.Ok(renamed);
        }).WithName("UpdateManufacturer");

        // ── Packing sizes ────────────────────────────────────────────
        app.MapGet("/api/packagings", async (AppDbContext db) =>
            Results.Ok(await db.Packings.OrderBy(p => p.Name).ToListAsync())).WithName("GetPackagings");

        app.MapPost("/api/packagings", async (SaveNamedLookupRequest r, AppDbContext db) =>
        {
            var name = r.Name?.Trim();
            if (string.IsNullOrEmpty(name)) return Results.BadRequest(new { message = "Name is required." });
            if (await db.Packings.AnyAsync(p => p.Name == name)) return Results.Conflict(new { message = $"Packing '{name}' already exists." });
            var now = DateTime.UtcNow;
            var p = new Packing { Name = name, Description = r.Description, CreatedAt = now, UpdatedAt = now };
            db.Packings.Add(p);
            await db.SaveChangesAsync();
            return Results.Created($"/api/packagings/{Uri.EscapeDataString(name)}", p);
        }).WithName("CreatePackaging");

        // ── Permit holders ───────────────────────────────────────────
        app.MapGet("/api/license-holders", async (AppDbContext db) =>
            Results.Ok(await db.PermitHolders.OrderBy(p => p.Name).ToListAsync())).WithName("GetLicenseHolders");

        app.MapPost("/api/license-holders", async (SavePermitHolderRequest r, AppDbContext db) =>
        {
            var permitNo = r.PermitNo?.Trim();
            if (string.IsNullOrEmpty(permitNo) || string.IsNullOrWhiteSpace(r.Name))
                return Results.BadRequest(new { message = "Permit number and name are required." });
            if (await db.PermitHolders.AnyAsync(p => p.PermitNo == permitNo))
                return Results.Conflict(new { message = $"Permit {permitNo} already exists." });
            var now = DateTime.UtcNow;
            var p = new PermitHolder
            {
                Id = permitNo, PermitNo = permitNo, Name = r.Name.Trim(), IssuedDate = r.IssuedDate,
                WhereGranted = r.WhereGranted, CreatedAt = now, UpdatedAt = now,
            };
            db.PermitHolders.Add(p);
            await db.SaveChangesAsync();
            return Results.Created($"/api/license-holders/{Uri.EscapeDataString(permitNo)}", p);
        }).WithName("CreateLicenseHolder");

        app.MapPut("/api/license-holders/{id}", async (string id, SavePermitHolderRequest r, AppDbContext db) =>
        {
            var p = await db.PermitHolders.FindAsync(id);
            if (p is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(r.Name)) return Results.BadRequest(new { message = "Name is required." });
            p.Name = r.Name.Trim();
            p.IssuedDate = r.IssuedDate;
            p.WhereGranted = r.WhereGranted;
            p.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(p);
        }).WithName("UpdateLicenseHolder");

        return app;
    }
}
