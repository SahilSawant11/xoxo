using Microsoft.EntityFrameworkCore;
using Xoxo.Api.Contracts;
using Xoxo.Infrastructure.Data;
using Xoxo.Infrastructure.Data.Models;

namespace Xoxo.Api.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/reports/sales?from=2026-06-15&to=2026-07-17
        // Matches the client's real DailySaleReport shape: one row per
        // material (aggregated across all bills in range), not one row
        // per transaction.
        app.MapGet("/api/reports/sales", async (DateOnly from, DateOnly to, AppDbContext db) =>
        {
            var bills = await db.SalesBills
                .Include(b => b.SaleLineItems)
                .Where(b => b.BillDate >= from && b.BillDate <= to && !b.IsDeleted)
                .ToListAsync();

            var allLines = bills.SelectMany(b => b.SaleLineItems).ToList();

            var items = allLines
            .GroupBy(li => new { li.BarcodeNo, li.MaterialName, li.Packing })
            .Select(g => new
            {
                materialId = g.Key.BarcodeNo,   // barcode == LocalItemCode, and it's always non-null
                materialName = g.Key.MaterialName,
                packing = g.Key.Packing,
                qtyCase = g.Sum(x => x.QtyCase),
                qtyLoose = g.Sum(x => x.Quantity),
                amount = g.Sum(x => x.Amount)
            })
            .OrderBy(x => x.materialName)
            .ToList();

            var summary = new
            {
                fromDate = from,
                toDate = to,
                totalBills = bills.Count,
                distinctBrands = items.Count,
                totalQtyCase = items.Sum(x => x.qtyCase),
                totalQtyLoose = items.Sum(x => x.qtyLoose),
                totalAmount = bills.Sum(b => b.TotalAmount),
                totalTax = bills.Sum(b => b.TotalTax),
                items
            };

            return Results.Ok(summary);
        })
        .WithName("GetSalesReport");


        // GET /api/reports/brandwise-stock?date=2026-06-27
        // Brand-wise daily stock register for excise compliance.
        // For each brand + bottle size: Opening Balance → Purchase → Sale → Closing Balance.
        // Opening is reverse-derived: Opening = CurrentStock + DaySale - DayPurchase.
        app.MapGet("/api/reports/brandwise-stock", async (DateOnly date, AppDbContext db) =>
        {
            var materials = await db.Materials.ToListAsync();

            // Sale quantities for the day, grouped by MaterialId
            var saleBills = await db.SalesBills
                .Include(b => b.SaleLineItems)
                .Where(b => b.BillDate == date && !b.IsDeleted)
                .ToListAsync();
            var saleLines = saleBills.SelectMany(b => b.SaleLineItems).ToList();
            var saleByMaterial = saleLines
                .Where(li => li.MaterialId != null)
                .GroupBy(li => li.MaterialId!)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            // Purchase quantities for the day, grouped by MaterialId
            var purchaseBills = await db.PurchaseBills
                .Include(b => b.PurchaseLineItems)
                .Where(b => b.BillDate == date && !b.IsDeleted)
                .ToListAsync();
            var purchaseLines = purchaseBills.SelectMany(b => b.PurchaseLineItems).ToList();
            var purchaseByMaterial = purchaseLines
                .GroupBy(li => li.MaterialId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

            // Current inventory stock (latest snapshot)
            var inventoryStocks = await db.InventoryStocks.ToListAsync();
            var stockByMaterial = inventoryStocks
                .GroupBy(s => s.MaterialId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.QtyOnHand));

            string[] sizeKeys = ["wine750", "wine375", "wine180", "beer650", "beer500Can", "beer330Can", "beer330Bottle"];

            string? MapCategory(string category)
            {
                var lower = category.ToLower().Trim();
                if (lower.Contains("fermented") || lower.Contains("strong beer")) return "FERMENTED BEER";
                if (lower.Contains("mild")) return "MILD BEER";
                if (lower.Contains("wine") || lower.Contains("whisky") || lower.Contains("whiskey") ||
                    lower.Contains("rum") || lower.Contains("vodka") || lower.Contains("brandy") ||
                    lower.Contains("gin")) return "WINE";
                if (lower.Contains("beer")) return "MILD BEER";
                return null;
            }

            // Flexible packing → column index. Handles "750 ML", "750ml", "650ML",
            // "500ml CAN", "330 ML (CAN)" etc.
            int PackingIndex(string packing)
            {
                var normalized = packing.Trim().ToLower().Replace(" ", "");
                var isCan = normalized.Contains("can");
                var numMatch = System.Text.RegularExpressions.Regex.Match(normalized, @"(\d+)");
                if (!numMatch.Success) return -1;
                var size = int.Parse(numMatch.Groups[1].Value);
                return size switch
                {
                    750 => 0,
                    375 => 1,
                    180 => 2,
                    650 => 3,
                    500 => 4,
                    330 => isCan ? 5 : 6,
                    _ => -1,
                };
            }

            // Build brand aggregations grouped by category
            var categoryData = new Dictionary<string, Dictionary<string, int[][]>>();

            foreach (var mat in materials)
            {
                var catName = MapCategory(mat.Category);
                if (catName == null) continue;

                var colIdx = PackingIndex(mat.Packing);
                if (colIdx < 0) continue;

                stockByMaterial.TryGetValue(mat.Id, out var currentStock);
                saleByMaterial.TryGetValue(mat.Id, out var saleQty);
                purchaseByMaterial.TryGetValue(mat.Id, out var purchaseQty);

                var closing = currentStock;
                var opening = closing + saleQty - purchaseQty;

                if (opening == 0 && saleQty == 0 && purchaseQty == 0 && closing == 0) continue;

                if (!categoryData.ContainsKey(catName))
                    categoryData[catName] = new Dictionary<string, int[][]>();

                if (!categoryData[catName].ContainsKey(mat.Name))
                    categoryData[catName][mat.Name] = new[] { new int[7], new int[7], new int[7], new int[7] };

                var agg = categoryData[catName][mat.Name];
                agg[0][colIdx] += opening;   // opening
                agg[1][colIdx] += purchaseQty; // purchase
                agg[2][colIdx] += saleQty;     // sale
                agg[3][colIdx] += closing;     // closing
            }

            object MakeSizeObj(int[] vals)
            {
                var dict = new Dictionary<string, int>();
                for (var i = 0; i < 7; i++) dict[sizeKeys[i]] = vals[i];
                return dict;
            }

            var categoryOrder = new[] { "FERMENTED BEER", "MILD BEER", "WINE" };
            var categories = new List<object>();

            foreach (var catName in categoryOrder)
            {
                if (!categoryData.TryGetValue(catName, out var brands) || brands.Count == 0)
                {
                    categories.Add(new
                    {
                        name = catName,
                        items = Array.Empty<object>(),
                        subtotal = new
                        {
                            openingBalance = MakeSizeObj(new int[7]),
                            purchase = MakeSizeObj(new int[7]),
                            sale = MakeSizeObj(new int[7]),
                            closingBalance = MakeSizeObj(new int[7])
                        }
                    });
                    continue;
                }

                var subOb = new int[7]; var subPu = new int[7]; var subSa = new int[7]; var subCl = new int[7];
                var items = new List<object>();

                foreach (var brandName in brands.Keys.OrderBy(k => k))
                {
                    var agg = brands[brandName];
                    items.Add(new
                    {
                        brandName,
                        tpNo = (string?)null,
                        openingBalance = MakeSizeObj(agg[0]),
                        purchase = MakeSizeObj(agg[1]),
                        sale = MakeSizeObj(agg[2]),
                        closingBalance = MakeSizeObj(agg[3])
                    });
                    for (var i = 0; i < 7; i++) { subOb[i] += agg[0][i]; subPu[i] += agg[1][i]; subSa[i] += agg[2][i]; subCl[i] += agg[3][i]; }
                }

                categories.Add(new
                {
                    name = catName,
                    items,
                    subtotal = new
                    {
                        openingBalance = MakeSizeObj(subOb),
                        purchase = MakeSizeObj(subPu),
                        sale = MakeSizeObj(subSa),
                        closingBalance = MakeSizeObj(subCl)
                    }
                });
            }

            return Results.Ok(new
            {
                reportDate = date,
                licenseHeader = "BEER A : WINE BR. II BRANDWISE STOCK REPORT",
                categories
            });
        })
        .WithName("GetBrandwiseStockReport");

        return app;
    }
}
