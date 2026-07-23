using Microsoft.EntityFrameworkCore;
using Xoxo.Infrastructure.Data;
using Xoxo.Infrastructure.Data.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseHttpsRedirection();

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

app.MapGet("/api/materials/{barcode}", async (string barcode, AppDbContext db) =>
{
    var material = await db.Materials.FirstOrDefaultAsync(m => m.Barcode == barcode || m.Id == barcode);
    return material is not null ? Results.Ok(material) : Results.NotFound();
})
.WithName("GetMaterialByBarcode");

app.MapGet("/api/customers", async (AppDbContext db) =>
{
    var customers = await db.Customers.ToListAsync();
    return Results.Ok(customers);
})
.WithName("GetCustomers");

app.MapPost("/api/sales", async (CreateSaleRequest request, AppDbContext db) =>
{
    var billId = Guid.NewGuid();

    var bill = new SalesBill
    {
        Id = billId,
        BillNo = request.BillNo,
        CustomerId = request.CustomerId,
        BillType = "CounterSale.Sale",
        BillDate = DateOnly.FromDateTime(DateTime.UtcNow),
        PayMode = request.PayMode,
        TaxableValue = request.TaxableValue,
        TotalDiscount = request.TotalDiscount,
        TotalTax = request.TotalTax,
        TotalAmount = request.TotalAmount,
        BalanceDue = request.BalanceDue,
        Status = "paid",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IsSynced = false,
        IsDeleted = false
    };

    var lineNo = 1;
    var lineItems = request.LineItems.Select(item => new SaleLineItem
    {
        Id = Guid.NewGuid(),
        SalesBillId = billId,
        MaterialId = item.MaterialId,
        BarcodeNo = item.BarcodeNo,
        MaterialType = item.MaterialType,
        MaterialName = item.MaterialName,
        BatchNo = item.BatchNo,
        Packing = item.Packing,
        Quantity = item.Quantity,
        QtyCase = item.QtyCase,
        Rate = item.Rate,
        DiscountPercent = item.DiscountPercent,
        DiscountAmount = item.DiscountAmount,
        TaxPercent = item.TaxPercent,
        TaxAmount = item.TaxAmount,
        Amount = item.Amount,
        LineNumber = lineNo++,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IsDeleted = false
    }).ToList();

    db.SalesBills.Add(bill);
    db.SaleLineItems.AddRange(lineItems);

    foreach (var item in request.LineItems.Where(i => i.MaterialId is not null))
    {
        var stock = await db.InventoryStocks
            .Where(s => s.MaterialId == item.MaterialId)
            .OrderByDescending(s => s.QtyOnHand)
            .FirstOrDefaultAsync();

        if (stock is not null)
        {
            stock.QtyOnHand -= item.Quantity;
            stock.UpdatedAt = DateTime.UtcNow;
        }
    }

    await db.SaveChangesAsync();

    return Results.Created($"/api/sales/{bill.Id}", new { bill.Id, bill.BillNo, LineItemCount = lineItems.Count });
})
.WithName("CreateSale");

app.MapGet("/api/suppliers", async (AppDbContext db) =>
{
    var suppliers = await db.Suppliers.ToListAsync();
    return Results.Ok(suppliers);
})
.WithName("GetSuppliers");

app.MapPost("/api/purchases", async (CreatePurchaseRequest request, AppDbContext db) =>
{
    var billId = Guid.NewGuid();

    var bill = new PurchaseBill
    {
        Id = billId,
        BillNo = request.BillNo,
        SupplierId = request.SupplierId,
        ChallanNo = request.ChallanNo,
        NoteNo = request.NoteNo,
        PayMode = request.PayMode,
        TpNo = request.TpNo,
        TpDate = request.TpDate,
        StNo = request.StNo,
        Discount = request.Discount,
        Vat = request.Vat,
        Stamp = request.Stamp,
        Tcs = request.Tcs,
        LoadingFreight = request.LoadingFreight,
        NetAmount = request.NetAmount,
        TotalAmount = request.TotalAmount,
        BillDate = DateOnly.FromDateTime(DateTime.UtcNow),
        Status = "saved",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IsSynced = false,
        IsDeleted = false
    };

    var lineNo = 1;
    var lineItems = request.LineItems.Select(item => new PurchaseLineItem
    {
        Id = Guid.NewGuid(),
        PurchaseBillId = billId,
        MaterialId = item.MaterialId,
        BatchNo = item.BatchNo,
        Packing = item.Packing,
        Qty = item.Qty,
        Rate = item.Rate,
        DisPercent = item.DisPercent,
        DisAmount = item.DisAmount,
        TaxPercent = item.TaxPercent,
        TaxAmount = item.TaxAmount,
        Amount = item.Amount,
        LineNumber = lineNo++,
        CreatedAt = DateTime.UtcNow
    }).ToList();

    db.PurchaseBills.Add(bill);
    db.PurchaseLineItems.AddRange(lineItems);

    foreach (var item in request.LineItems)
    {
        var stock = await db.InventoryStocks.FirstOrDefaultAsync(
            s => s.MaterialId == item.MaterialId && s.BatchNo == item.BatchNo);

        if (stock is null)
        {
            stock = new InventoryStock
            {
                Id = Guid.NewGuid(),
                MaterialId = item.MaterialId,
                BatchNo = item.BatchNo,
                QtyOnHand = 0,
                ReorderLevel = 10,
                UpdatedAt = DateTime.UtcNow
            };
            db.InventoryStocks.Add(stock);
        }

        stock.QtyOnHand += item.Qty;
        stock.UpdatedAt = DateTime.UtcNow;
    }

    await db.SaveChangesAsync();

    return Results.Created($"/api/purchases/{bill.Id}", new { bill.Id, bill.BillNo, LineItemCount = lineItems.Count });
})
.WithName("CreatePurchase");

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

// GET /api/dashboard/summary — everything the Dashboard needs in one
// call: today vs yesterday, 7-day trend, category/payment breakdowns,
// top sellers, recent bills, top customers. All computed from real
// SalesBills/SaleLineItems — no separate calls per widget.
app.MapGet("/api/dashboard/summary", async (AppDbContext db) =>
{
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var yesterday = today.AddDays(-1);
    var last30Start = today.AddDays(-29);

    var recentBills = await db.SalesBills
        .Include(b => b.SaleLineItems)
        .Include(b => b.Customer)
        .Where(b => b.BillDate >= last30Start && !b.IsDeleted)
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
        topCustomers
    };

    return Results.Ok(summary);
})
.WithName("GetDashboardSummary");

app.Run();

record CreateSaleRequest(
    string BillNo,
    Guid? CustomerId,
    string PayMode,
    decimal TaxableValue,
    decimal TotalDiscount,
    decimal TotalTax,
    decimal TotalAmount,
    decimal BalanceDue,
    List<CreateSaleLineItemRequest> LineItems
);

record CreateSaleLineItemRequest(
    
    string? MaterialId,
    string BarcodeNo,
    string MaterialType,
    string MaterialName,
    string? BatchNo,
    string? Packing,
    int Quantity,
    int QtyCase,
    decimal Rate,
    decimal DiscountPercent,
    decimal DiscountAmount,
    decimal TaxPercent,
    decimal TaxAmount,
    decimal Amount
);
record CreatePurchaseRequest(
    Guid SupplierId,
    string? BillNo,
    string? ChallanNo,
    string? NoteNo,
    string PayMode,
    string? TpNo,
    DateOnly? TpDate,
    string? StNo,
    decimal Discount,
    decimal Vat,
    decimal Stamp,
    decimal Tcs,
    decimal LoadingFreight,
    decimal NetAmount,
    decimal TotalAmount,
    List<CreatePurchaseLineItemRequest> LineItems
);

record CreatePurchaseLineItemRequest(
    string MaterialId,
    string BatchNo,
    string? Packing,
    int Qty,
    decimal Rate,
    decimal DisPercent,
    decimal DisAmount,
    decimal TaxPercent,
    decimal TaxAmount,
    decimal Amount
);

record SaveMaterialRequest(
    string Id,
    string? Barcode,
    string Name,
    string Category,
    string Packing,
    decimal SaleRate,
    decimal TaxPercent
);