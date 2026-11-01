using Microsoft.EntityFrameworkCore;
using Xoxo.Api.Contracts;
using Xoxo.Infrastructure.Data;
using Xoxo.Infrastructure.Data.Models;

namespace Xoxo.Api.Endpoints;

public static class MasterEndpoints
{
    public static IEndpointRouteBuilder MapMasterEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/materials/{barcode}", async (string barcode, AppDbContext db) =>
        {
            var material = await db.Materials.FirstOrDefaultAsync(m => m.Barcode == barcode || m.Id == barcode);
            return material is not null ? Results.Ok(material) : Results.NotFound();
        })
        .WithName("GetMaterialByBarcode");

        app.MapGet("/api/suppliers", async (AppDbContext db) =>
        {
            var suppliers = await db.Suppliers.ToListAsync();
            return Results.Ok(suppliers);
        })
        .WithName("GetSuppliers");

        app.MapPost("/api/suppliers", async (SaveSupplierRequest request, AppDbContext db) =>
        {
            var supplier = new Supplier
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Address = request.Address,
                ContactNo = request.ContactNo,
                Email = request.Email,
                VatNo = request.VatNo,
                BankDetails = request.BankDetails,
                DisPercent = request.DisPercent,
                OpeningBalance = request.OpeningBalance,
                BalanceType = request.BalanceType,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync();

            return Results.Created($"/api/suppliers/{supplier.Id}", supplier);
        })
        .WithName("CreateSupplier");

        app.MapPut("/api/suppliers/{id}", async (Guid id, SaveSupplierRequest request, AppDbContext db) =>
        {
            var supplier = await db.Suppliers.FindAsync(id);
            if (supplier is null) return Results.NotFound();

            supplier.Name = request.Name;
            supplier.Address = request.Address;
            supplier.ContactNo = request.ContactNo;
            supplier.Email = request.Email;
            supplier.VatNo = request.VatNo;
            supplier.BankDetails = request.BankDetails;
            supplier.DisPercent = request.DisPercent;
            supplier.OpeningBalance = request.OpeningBalance;
            supplier.BalanceType = request.BalanceType;
            supplier.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Results.Ok(supplier);
        })
        .WithName("UpdateSupplier");

        // GET /api/materials — full list, powers Material Master's Prev/Next browsing
        app.MapGet("/api/materials", async (AppDbContext db) =>
        {
            var materials = await db.Materials.OrderBy(m => m.Name).ToListAsync();
            return Results.Ok(materials);
        })
        .WithName("GetMaterials");

        // POST /api/materials — Id is the LocalItemCode itself, supplied by
        // the person (not server-generated, since it's the client's own
        // business key). Rejects duplicates.
        app.MapPost("/api/materials", async (SaveMaterialRequest request, AppDbContext db) =>
        {
            var exists = await db.Materials.AnyAsync(m => m.Id == request.Id);
            if (exists) return Results.Conflict(new { message = $"Material code '{request.Id}' already exists." });

            var material = new Material
            {
                Id = request.Id,
                Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? request.Id : request.Barcode,
                Name = request.Name,
                Category = request.Category,
                Packing = request.Packing,
                SaleRate = request.SaleRate,
                TaxPercent = request.TaxPercent,
                StockQty = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Materials.Add(material);
            await db.SaveChangesAsync();

            return Results.Created($"/api/materials/{material.Id}", material);
        })
        .WithName("CreateMaterial");

        // PUT /api/materials/{id} — the code itself (Id) can't be changed
        // once created, since it's the primary key everything else links to.
        app.MapPut("/api/materials/{id}", async (string id, SaveMaterialRequest request, AppDbContext db) =>
        {
            var material = await db.Materials.FindAsync(id);
            if (material is null) return Results.NotFound();

            material.Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? material.Barcode : request.Barcode;
            material.Name = request.Name;
            material.Category = request.Category;
            material.Packing = request.Packing;
            material.SaleRate = request.SaleRate;
            material.TaxPercent = request.TaxPercent;
            material.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Results.Ok(material);
        })
        .WithName("UpdateMaterial");

        return app;
    }
}
