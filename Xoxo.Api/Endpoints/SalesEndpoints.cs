using Microsoft.EntityFrameworkCore;
using Xoxo.Api.Auth;
using Xoxo.Api.Contracts;
using Xoxo.Api.Services;
using Xoxo.Infrastructure.Data;
using Xoxo.Infrastructure.Data.Models;

namespace Xoxo.Api.Endpoints;

public static class SalesEndpoints
{
    public static IEndpointRouteBuilder MapSalesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/sales/today", async (AppDbContext db) =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var bills = await db.SalesBills
                .Include(b => b.SaleLineItems)
                .Where(b => b.BillDate == today && !b.IsDeleted)
                .ToListAsync();
            return Results.Ok(bills);
        })
        .WithName("GetTodaysSales");

        app.MapPost("/api/sales", async (CreateSaleRequest request, HttpContext http, AppDbContext db, BillingService billing) =>
        {
            var result = await billing.CreateSaleAsync(request, http.Origin());
            if (!result.Ok) return Results.BadRequest(new { message = result.Error });
            await db.SaveChangesAsync();
            return Results.Created($"/api/sales/{result.Id}", new { Id = result.Id, BillNo = result.BillNo, LineItemCount = result.LineCount });
        })
        .WithName("CreateSale");

        app.MapGet("/api/sales", async (string? search, DateOnly? date, AppDbContext db) =>
        {
            var query = db.SalesBills
                .Include(b => b.SaleLineItems)
                .Include(b => b.Customer)
                .Where(b => !b.IsDeleted);

            if (date.HasValue)
            {
                query = query.Where(b => b.BillDate == date.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(b => b.BillNo.Contains(s) || (b.Customer != null && b.Customer.Name.Contains(s)));
            }

            var list = await query
                .OrderByDescending(b => b.CreatedAt)
                .Take(50)
                .Select(b => new
                {
                    b.Id,
                    b.BillNo,
                    b.CustomerId,
                    CustomerName = b.Customer != null ? b.Customer.Name : null,
                    b.BillType,
                    b.BillDate,
                    b.PayMode,
                    b.TaxableValue,
                    b.TotalDiscount,
                    b.TotalTax,
                    b.TotalAmount,
                    b.BalanceDue,
                    b.Status,
                    b.CreatedAt,
                    b.UpdatedAt,
                    SaleLineItems = b.SaleLineItems.Where(li => li.IsDeleted != true).OrderBy(li => li.LineNumber).Select(li => new
                    {
                        li.Id,
                        li.SalesBillId,
                        li.BarcodeNo,
                        li.MaterialId,
                        li.MaterialType,
                        li.MaterialName,
                        li.BatchNo,
                        li.Packing,
                        li.Quantity,
                        li.QtyCase,
                        li.Rate,
                        li.DiscountPercent,
                        li.DiscountAmount,
                        li.TaxPercent,
                        li.TaxAmount,
                        li.Amount,
                        li.LineNumber
                    }).ToList()
                })
                .ToListAsync();

            return Results.Ok(list);
        })
        .WithName("GetSales");

        async Task<IResult> ReturnSaleHandler(Guid id, HttpContext http, AppDbContext db, BillingService billing)
        {
            var bill = await db.SalesBills.Include(b => b.SaleLineItems).FirstOrDefaultAsync(b => b.Id == id);
            if (bill is null) return Results.NotFound(new { message = "Sales bill not found." });
            var result = await billing.ReturnSaleAsync(bill, http.Origin());
            if (!result.Ok) return Results.BadRequest(new { message = result.Error });
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Sales bill returned and stock restored successfully.", bill.Id, bill.BillNo });
        }

        app.MapDelete("/api/sales/{id}", ReturnSaleHandler).WithName("DeleteSale");
        app.MapPost("/api/sales/{id}/return", ReturnSaleHandler).WithName("ReturnSale");

        app.MapGet("/api/customers", async (AppDbContext db) =>
        {
            var customers = await db.Customers.ToListAsync();
            return Results.Ok(customers);
        })
        .WithName("GetCustomers");

        return app;
    }
}
