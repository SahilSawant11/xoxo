using Microsoft.EntityFrameworkCore;
using Xoxo.Api.Contracts;
using Xoxo.Infrastructure.Data;
using Xoxo.Infrastructure.Data.Models;

namespace Xoxo.Api.Services;

/// <summary>Outcome of a write: either the created/affected entity id or a client error.</summary>
public sealed record ServiceResult(bool Ok, Guid? Id = null, string? BillNo = null, string? Error = null, int LineCount = 0)
{
    public static ServiceResult Fail(string error) => new(false, Error: error);
}

/// <summary>Who/where a write came from. Null DeviceId = legacy direct API call.</summary>
public sealed record WriteOrigin(Guid? DeviceId, Guid? StoreId, string? ClientId, string? User);

/// <summary>
/// Sales, purchases and returns — shared by the REST endpoints and the
/// sync endpoint so both paths apply exactly the same rules. Callers own
/// SaveChanges / transactions.
/// </summary>
public sealed class BillingService(AppDbContext db)
{
    public async Task<ServiceResult> CreateSaleAsync(CreateSaleRequest request, WriteOrigin origin)
    {
        if (request.LineItems is null || request.LineItems.Count == 0) return ServiceResult.Fail("A sale needs at least one line item.");
        if (string.IsNullOrWhiteSpace(request.BillNo)) return ServiceResult.Fail("Bill number is required.");

        if (request.CustomerId is Guid customerId && !await db.Customers.AnyAsync(c => c.Id == customerId))
            return ServiceResult.Fail("Selected customer does not exist in the API database.");

        var materialIds = request.LineItems.Where(i => !string.IsNullOrWhiteSpace(i.MaterialId)).Select(i => i.MaterialId!).Distinct().ToList();
        if (materialIds.Count > 0)
        {
            var existing = await db.Materials.Where(m => materialIds.Contains(m.Id)).Select(m => m.Id).ToListAsync();
            var missing = materialIds.Except(existing).ToList();
            if (missing.Count > 0) return ServiceResult.Fail($"Unknown material(s): {string.Join(", ", missing)}");
        }

        if (await db.SalesBills.AnyAsync(b => b.DeviceId == origin.DeviceId && b.BillNo == request.BillNo))
            return ServiceResult.Fail($"Bill number {request.BillNo} already exists for this counter.");

        var now = DateTime.UtcNow;
        var bill = new SalesBill
        {
            Id = Guid.NewGuid(),
            BillNo = request.BillNo,
            CustomerId = request.CustomerId,
            BillType = "CounterSale.Sale",
            BillDate = request.BillDate ?? DateOnly.FromDateTime(DateTime.Now),
            PayMode = request.PayMode,
            TaxableValue = request.TaxableValue,
            TotalDiscount = request.TotalDiscount,
            TotalTax = request.TotalTax,
            TotalAmount = request.TotalAmount,
            BalanceDue = request.BalanceDue,
            Status = "paid",
            PermitNo = request.PermitNo,
            PermitHolderName = request.PermitHolderName,
            DeviceId = origin.DeviceId,
            StoreId = origin.StoreId,
            ClientId = origin.ClientId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var lineNo = 1;
        foreach (var item in request.LineItems)
        {
            bill.SaleLineItems.Add(new SaleLineItem
            {
                Id = Guid.NewGuid(),
                SalesBillId = bill.Id,
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
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
            });
        }
        db.SalesBills.Add(bill);

        // The counter already checked stock when it sold offline; the server
        // records the movement even if its own figure disagrees.
        foreach (var item in request.LineItems.Where(i => i.MaterialId is not null))
            await AdjustStockAsync(item.MaterialId!, batch: null, -item.Quantity);

        return new ServiceResult(true, bill.Id, bill.BillNo, LineCount: bill.SaleLineItems.Count);
    }

    /// <summary>Full return: keeps the bill (status 'returned'), logs every line, restores stock.</summary>
    public async Task<ServiceResult> ReturnSaleAsync(SalesBill? bill, WriteOrigin origin)
    {
        if (bill is null) return ServiceResult.Fail("Sales bill not found.");
        if (bill.IsDeleted || bill.ReturnedAt != null) return ServiceResult.Fail("Sales bill is already returned.");

        var now = DateTime.UtcNow;
        bill.IsDeleted = true; // existing reports/lists filter on IsDeleted
        bill.Status = "returned";
        bill.ReturnedAt = now;
        bill.ReturnedBy = origin.User;
        bill.UpdatedAt = now;

        foreach (var item in bill.SaleLineItems.Where(li => li.IsDeleted != true))
        {
            db.SalesReturnEntries.Add(new SalesReturnEntry
            {
                Id = Guid.NewGuid(),
                SalesBillId = bill.Id,
                BillNo = bill.BillNo,
                LineNumber = item.LineNumber ?? 0,
                MaterialId = item.MaterialId,
                MaterialName = item.MaterialName,
                Quantity = item.Quantity,
                Rate = item.Rate,
                Amount = item.Amount,
                IsFullBillReturn = true,
                ReturnedBy = origin.User,
                DeviceId = origin.DeviceId,
                ReturnedAt = now,
            });
            item.IsDeleted = true;
            item.UpdatedAt = now;
            if (!string.IsNullOrWhiteSpace(item.MaterialId)) await AdjustStockAsync(item.MaterialId, batch: null, item.Quantity);
        }
        return new ServiceResult(true, bill.Id, bill.BillNo);
    }

    public async Task<ServiceResult> CreatePurchaseAsync(CreatePurchaseRequest request, WriteOrigin origin)
    {
        if (request.LineItems is null || request.LineItems.Count == 0) return ServiceResult.Fail("A purchase needs at least one line item.");
        if (!await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId)) return ServiceResult.Fail("Supplier does not exist in the API database.");

        var now = DateTime.UtcNow;
        var bill = new PurchaseBill
        {
            Id = Guid.NewGuid(),
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
            BillDate = request.BillDate ?? DateOnly.FromDateTime(DateTime.Now),
            Status = "saved",
            DeviceId = origin.DeviceId,
            StoreId = origin.StoreId,
            ClientId = origin.ClientId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var lineNo = 1;
        foreach (var item in request.LineItems)
        {
            bill.PurchaseLineItems.Add(new PurchaseLineItem
            {
                Id = Guid.NewGuid(),
                PurchaseBillId = bill.Id,
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
                CreatedAt = now,
            });
        }
        db.PurchaseBills.Add(bill);
        foreach (var item in request.LineItems) await AdjustStockAsync(item.MaterialId, item.BatchNo, item.Qty);
        return new ServiceResult(true, bill.Id, bill.BillNo, LineCount: bill.PurchaseLineItems.Count);
    }

    public async Task<ServiceResult> ReturnPurchaseAsync(PurchaseBill? bill, WriteOrigin origin)
    {
        if (bill is null) return ServiceResult.Fail("Purchase bill not found.");
        if (bill.IsDeleted || bill.ReturnedAt != null) return ServiceResult.Fail("Purchase bill is already returned.");

        var now = DateTime.UtcNow;
        bill.IsDeleted = true;
        bill.Status = "returned";
        bill.ReturnedAt = now;
        bill.ReturnedBy = origin.User;
        bill.UpdatedAt = now;
        foreach (var item in bill.PurchaseLineItems)
        {
            db.PurchaseReturnEntries.Add(new PurchaseReturnEntry
            {
                Id = Guid.NewGuid(),
                PurchaseBillId = bill.Id,
                BillNo = bill.BillNo,
                LineNumber = item.LineNumber,
                MaterialId = item.MaterialId,
                BatchNo = item.BatchNo,
                Quantity = item.Qty,
                Rate = item.Rate,
                Amount = item.Amount,
                IsFullBillReturn = true,
                ReturnedBy = origin.User,
                DeviceId = origin.DeviceId,
                ReturnedAt = now,
            });
            await AdjustStockAsync(item.MaterialId, item.BatchNo, -item.Qty);
        }
        return new ServiceResult(true, bill.Id, bill.BillNo);
    }

    /// <summary>
    /// Adds [delta] to the material's stock. With a batch, that batch row is
    /// used (created if missing); without, the largest batch is used.
    /// Looks at pending (unsaved) rows too, so several lines for the same
    /// item in one request add up correctly.
    /// </summary>
    private async Task AdjustStockAsync(string materialId, string? batch, int delta)
    {
        InventoryStock? stock;
        if (batch != null)
        {
            stock = db.InventoryStocks.Local.FirstOrDefault(s => s.MaterialId == materialId && s.BatchNo == batch)
                    ?? await db.InventoryStocks.FirstOrDefaultAsync(s => s.MaterialId == materialId && s.BatchNo == batch);
            if (stock is null)
            {
                stock = new InventoryStock { Id = Guid.NewGuid(), MaterialId = materialId, BatchNo = batch, QtyOnHand = 0, ReorderLevel = 10 };
                db.InventoryStocks.Add(stock);
            }
        }
        else
        {
            stock = db.InventoryStocks.Local.Where(s => s.MaterialId == materialId).OrderByDescending(s => s.QtyOnHand).FirstOrDefault()
                    ?? await db.InventoryStocks.Where(s => s.MaterialId == materialId).OrderByDescending(s => s.QtyOnHand).FirstOrDefaultAsync();
            if (stock is null) return;
        }
        stock.QtyOnHand += delta;
        stock.UpdatedAt = DateTime.UtcNow;
    }
}
