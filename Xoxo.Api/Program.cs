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
    var material = await db.Materials.FirstOrDefaultAsync(m => m.Barcode == barcode);
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

// GET /api/reports/sales?from=2026-07-01&to=2026-07-15
// Powers all 3 Reports tabs — Day-wise/Monthly/Custom just send
// different from/to values computed client-side.
app.MapGet("/api/reports/sales", async (DateOnly from, DateOnly to, AppDbContext db) =>
{
    var bills = await db.SalesBills
        .Include(b => b.SaleLineItems)
        .Where(b => b.BillDate >= from && b.BillDate <= to && !b.IsDeleted)
        .ToListAsync();

    var lineItems = bills
        .SelectMany(b => b.SaleLineItems.Select(li => new
        {
            billNo = b.BillNo,
            billDate = b.BillDate,
            materialId = li.MaterialId,
            materialName = li.MaterialName,
            packing = li.Packing,
            quantity = li.Quantity,
            rate = li.Rate,
            amount = li.Amount
        }))
        .OrderByDescending(x => x.billDate)
        .ToList();

    var summary = new
    {
        fromDate = from,
        toDate = to,
        totalBills = bills.Count,
        totalLineItems = lineItems.Count,
        totalQty = lineItems.Sum(x => x.quantity),
        totalAmount = bills.Sum(b => b.TotalAmount),
        totalTax = bills.Sum(b => b.TotalTax),
        distinctBrands = lineItems.Select(x => x.materialName).Distinct().Count(),
        lineItems
    };

    return Results.Ok(summary);
})
.WithName("GetSalesReport");

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