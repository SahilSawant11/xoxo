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
    if (request.CustomerId is Guid customerId)
    {
        var customerExists = await db.Customers.AnyAsync(c => c.Id == customerId);
        if (!customerExists)
        {
            return Results.BadRequest(new { message = "Selected customer does not exist in the API database." });
        }
    }

    var materialIds = request.LineItems
        .Where(item => !string.IsNullOrWhiteSpace(item.MaterialId))
        .Select(item => item.MaterialId!)
        .Distinct()
        .ToList();

    if (materialIds.Count > 0)
    {
        var existingMaterialIds = await db.Materials
            .Where(m => materialIds.Contains(m.Id))
            .Select(m => m.Id)
            .ToListAsync();

        var missingMaterialIds = materialIds.Except(existingMaterialIds).ToList();
        if (missingMaterialIds.Count > 0)
        {
            return Results.BadRequest(new
            {
                message = "One or more selected materials do not exist in the API database.",
                materialIds = missingMaterialIds
            });
        }
    }

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

async Task<IResult> ReturnSaleHandler(Guid id, AppDbContext db)
{
    var bill = await db.SalesBills
        .Include(b => b.SaleLineItems)
        .FirstOrDefaultAsync(b => b.Id == id);

    if (bill is null)
    {
        return Results.NotFound(new { message = "Sales bill not found." });
    }

    if (bill.IsDeleted)
    {
        return Results.BadRequest(new { message = "Sales bill is already deleted or returned." });
    }

    bill.IsDeleted = true;
    bill.Status = "returned";
    bill.UpdatedAt = DateTime.UtcNow;

    foreach (var item in bill.SaleLineItems)
    {
        item.IsDeleted = true;
        item.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(item.MaterialId))
        {
            var stock = await db.InventoryStocks
                .Where(s => s.MaterialId == item.MaterialId)
                .OrderByDescending(s => s.QtyOnHand)
                .FirstOrDefaultAsync();

            if (stock is not null)
            {
                stock.QtyOnHand += item.Quantity;
                stock.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    await db.SaveChangesAsync();
    return Results.Ok(new { message = "Sales bill returned and stock restored successfully.", bill.Id, bill.BillNo });
}

app.MapDelete("/api/sales/{id}", ReturnSaleHandler).WithName("DeleteSale");
app.MapPost("/api/sales/{id}/return", ReturnSaleHandler).WithName("ReturnSale");

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

async Task<IResult> ReturnPurchaseHandler(Guid id, AppDbContext db)
{
    var bill = await db.PurchaseBills
        .Include(b => b.PurchaseLineItems)
        .FirstOrDefaultAsync(b => b.Id == id);

    if (bill is null)
    {
        return Results.NotFound(new { message = "Purchase bill not found." });
    }

    if (bill.IsDeleted)
    {
        return Results.BadRequest(new { message = "Purchase bill is already deleted or returned." });
    }

    bill.IsDeleted = true;
    bill.Status = "returned";
    bill.UpdatedAt = DateTime.UtcNow;

    foreach (var item in bill.PurchaseLineItems)
    {
        var stock = await db.InventoryStocks.FirstOrDefaultAsync(
            s => s.MaterialId == item.MaterialId && s.BatchNo == item.BatchNo);

        if (stock is not null)
        {
            stock.QtyOnHand -= item.Qty;
            stock.UpdatedAt = DateTime.UtcNow;
        }
    }

    await db.SaveChangesAsync();
    return Results.Ok(new { message = "Purchase bill returned and stock deducted successfully.", bill.Id, bill.BillNo });
}

app.MapDelete("/api/purchases/{id}", ReturnPurchaseHandler).WithName("DeletePurchase");
app.MapPost("/api/purchases/{id}/return", ReturnPurchaseHandler).WithName("ReturnPurchase");

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

record SaveSupplierRequest(
    string Name,
    string? Address,
    string? ContactNo,
    string? Email,
    string? VatNo,
    string? BankDetails,
    decimal DisPercent,
    decimal OpeningBalance,
    string BalanceType
);
