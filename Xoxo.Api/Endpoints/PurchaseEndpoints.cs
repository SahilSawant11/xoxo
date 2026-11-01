using Microsoft.EntityFrameworkCore;
using Xoxo.Api.Auth;
using Xoxo.Api.Contracts;
using Xoxo.Api.Services;
using Xoxo.Infrastructure.Data;
using Xoxo.Infrastructure.Data.Models;

namespace Xoxo.Api.Endpoints;

public static class PurchaseEndpoints
{
    public static IEndpointRouteBuilder MapPurchaseEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/purchases", async (CreatePurchaseRequest request, HttpContext http, AppDbContext db, BillingService billing) =>
        {
            var result = await billing.CreatePurchaseAsync(request, http.Origin());
            if (!result.Ok) return Results.BadRequest(new { message = result.Error });
            await db.SaveChangesAsync();
            return Results.Created($"/api/purchases/{result.Id}", new { Id = result.Id, BillNo = result.BillNo, LineItemCount = result.LineCount });
        })
        .WithName("CreatePurchase");

        app.MapGet("/api/purchases", async (string? search, DateOnly? date, AppDbContext db) =>
        {
            var query = db.PurchaseBills
                .Include(b => b.PurchaseLineItems)
                .ThenInclude(li => li.Material)
                .Include(b => b.Supplier)
                .Where(b => !b.IsDeleted);

            if (date.HasValue)
            {
                query = query.Where(b => b.BillDate == date.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(b => (b.BillNo != null && b.BillNo.Contains(s)) ||
                                         (b.ChallanNo != null && b.ChallanNo.Contains(s)) ||
                                         b.Supplier.Name.Contains(s));
            }

            var list = await query
                .OrderByDescending(b => b.CreatedAt)
                .Take(50)
                .Select(b => new
                {
                    b.Id,
                    b.BillNo,
                    b.SupplierId,
                    SupplierName = b.Supplier.Name,
                    b.ChallanNo,
                    b.NoteNo,
                    b.PayMode,
                    b.TpNo,
                    b.TpDate,
                    b.StNo,
                    b.Discount,
                    b.Vat,
                    b.Stamp,
                    b.Tcs,
                    b.LoadingFreight,
                    b.NetAmount,
                    b.TotalAmount,
                    b.BillDate,
                    b.Status,
                    b.CreatedAt,
                    b.UpdatedAt,
                    PurchaseLineItems = b.PurchaseLineItems.OrderBy(li => li.LineNumber).Select(li => new
                    {
                        li.Id,
                        li.PurchaseBillId,
                        li.MaterialId,
                        MaterialName = li.Material != null ? li.Material.Name : li.MaterialId,
                        li.BatchNo,
                        li.Packing,
                        li.Qty,
                        li.Rate,
                        li.DisPercent,
                        li.DisAmount,
                        li.TaxPercent,
                        li.TaxAmount,
                        li.Amount,
                        li.LineNumber
                    }).ToList()
                })
                .ToListAsync();

            return Results.Ok(list);
        })
        .WithName("GetPurchases");

        async Task<IResult> ReturnPurchaseHandler(Guid id, HttpContext http, AppDbContext db, BillingService billing)
        {
            var bill = await db.PurchaseBills.Include(b => b.PurchaseLineItems).FirstOrDefaultAsync(b => b.Id == id);
            if (bill is null) return Results.NotFound(new { message = "Purchase bill not found." });
            var result = await billing.ReturnPurchaseAsync(bill, http.Origin());
            if (!result.Ok) return Results.BadRequest(new { message = result.Error });
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Purchase bill returned and stock deducted successfully.", bill.Id, bill.BillNo });
        }

        app.MapDelete("/api/purchases/{id}", ReturnPurchaseHandler).WithName("DeletePurchase");
        app.MapPost("/api/purchases/{id}/return", ReturnPurchaseHandler).WithName("ReturnPurchase");

        return app;
    }
}
