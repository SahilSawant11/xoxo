using Microsoft.EntityFrameworkCore;
using Xoxo.Api.Contracts;
using Xoxo.Infrastructure.Data;
using Xoxo.Infrastructure.Data.Models;

namespace Xoxo.Api.Endpoints;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/inventory", async (AppDbContext db) =>
        {
            var stock = await db.InventoryStocks.Include(s => s.Material).ToListAsync();

            var grouped = stock
                .GroupBy(s => s.MaterialId)
                .Select(g => new
                {
                    materialId = g.Key,
                    barcode = g.First().Material.Barcode,
                    name = g.First().Material.Name,
                    category = g.First().Material.Category,
                    qtyOnHand = g.Sum(x => x.QtyOnHand),
                    reorderLevel = g.Min(x => x.ReorderLevel)
                });

            return Results.Ok(grouped);
        })
        .WithName("GetInventory");

        return app;
    }
}
