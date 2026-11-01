using Microsoft.EntityFrameworkCore;
using Xoxo.Api.Contracts;
using Xoxo.Infrastructure.Data;
using Xoxo.Infrastructure.Data.Models;

namespace Xoxo.Api.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/dashboard/summary — everything the Dashboard needs in one
        // call: today vs yesterday, 7-day trend, category/payment breakdowns,
        // top sellers, recent bills, top customers. All computed from real
        // SalesBills/SaleLineItems — no separate calls per widget.
        app.MapGet("/api/dashboard/summary", async (AppDbContext db) =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var yesterday = today.AddDays(-1);
            var last30Start = today.AddDays(-29);
            var last365Start = today.AddDays(-364);

            var recentBills = await db.SalesBills
                .Include(b => b.SaleLineItems)
                .Include(b => b.Customer)
                .Where(b => b.BillDate >= last30Start && !b.IsDeleted)
                .ToListAsync();

            var yearlyBills = await db.SalesBills
                .Where(b => b.BillDate >= last365Start && !b.IsDeleted)
                .ToListAsync();

            var todaysBills = recentBills.Where(b => b.BillDate == today).ToList();
            var yesterdaysBills = recentBills.Where(b => b.BillDate == yesterday).ToList();

            var todaySales = todaysBills.Sum(b => b.TotalAmount);
            var yesterdaySales = yesterdaysBills.Sum(b => b.TotalAmount);
            var todayBillCount = todaysBills.Count;
            var yesterdayBillCount = yesterdaysBills.Count;
            var avgBillValue = todayBillCount > 0 ? todaySales / todayBillCount : 0;

            double? salesDeltaPercent = yesterdaySales > 0
                ? (double)((todaySales - yesterdaySales) / yesterdaySales * 100)
                : null;
            double? billsDeltaPercent = yesterdayBillCount > 0
                ? (double)(todayBillCount - yesterdayBillCount) / yesterdayBillCount * 100
                : null;

            var last7Days = new List<object>();
            for (var i = 6; i >= 0; i--)
            {
                var d = today.AddDays(-i);
                var amount = recentBills.Where(b => b.BillDate == d).Sum(b => b.TotalAmount);
                last7Days.Add(new { date = d, amount });
            }

            var transactionActivity = new List<object>();
            for (var i = 364; i >= 0; i--)
            {
                var d = today.AddDays(-i);
                var dayBills = yearlyBills.Where(b => b.BillDate == d).ToList();
                transactionActivity.Add(new
                {
                    date = d,
                    billCount = dayBills.Count,
                    amount = dayBills.Sum(b => b.TotalAmount)
                });
            }

            var categoryBreakdown = recentBills
                .SelectMany(b => b.SaleLineItems)
                .GroupBy(li => li.MaterialType)
                .Select(g => new { category = g.Key, amount = g.Sum(x => x.Amount) })
                .OrderByDescending(x => x.amount)
                .ToList();

            var totalForMix = recentBills.Sum(b => b.TotalAmount);
            var paymentMix = recentBills
                .GroupBy(b => b.PayMode)
                .Select(g => new
                {
                    payMode = g.Key,
                    amount = g.Sum(b => b.TotalAmount),
                    percent = totalForMix > 0 ? (double)(g.Sum(b => b.TotalAmount) / totalForMix * 100) : 0
                })
                .OrderByDescending(x => x.amount)
                .ToList();

            var topSellingItems = recentBills
                .SelectMany(b => b.SaleLineItems)
                .GroupBy(li => new { li.MaterialId, li.MaterialName, li.Packing })
                .Select(g => new
                {
                    materialName = g.Key.MaterialName,
                    packing = g.Key.Packing,
                    qty = g.Sum(x => x.Quantity),
                    amount = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.qty)
                .Take(5)
                .ToList();

            var recentTransactions = recentBills
                .OrderByDescending(b => b.CreatedAt)
                .Take(8)
                .Select(b => new
                {
                    billNo = b.BillNo,
                    billDate = b.BillDate,
                    customerName = b.Customer != null ? b.Customer.Name : "Counter Sale",
                    payMode = b.PayMode,
                    totalAmount = b.TotalAmount,
                    status = b.Status
                })
                .ToList();

            var topCustomers = recentBills
                .Where(b => b.Customer != null)
                .GroupBy(b => new { b.CustomerId, Name = b.Customer!.Name })
                .Select(g => new
                {
                    customerName = g.Key.Name,
                    billCount = g.Count(),
                    totalAmount = g.Sum(b => b.TotalAmount)
                })
                .OrderByDescending(x => x.totalAmount)
                .Take(5)
                .ToList();

            var summary = new
            {
                todaySales,
                todayBillCount,
                avgBillValue,
                salesDeltaPercent,
                billsDeltaPercent,
                dailyTarget = 250000m, // placeholder — no real settings/target source exists yet
                last7Days,
                categoryBreakdown,
                paymentMix,
                topSellingItems,
                recentTransactions,
                topCustomers,
                transactionActivity
            };

            return Results.Ok(summary);
        })
        .WithName("GetDashboardSummary");

        return app;
    }
}
