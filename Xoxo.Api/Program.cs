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
    await db.SaveChangesAsync();

    return Results.Created($"/api/sales/{bill.Id}", new { bill.Id, bill.BillNo, LineItemCount = lineItems.Count });
})
.WithName("CreateSale");

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
