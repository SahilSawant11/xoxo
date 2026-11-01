namespace Xoxo.Api.Contracts;

public record CreateSaleRequest(
    string BillNo,
    Guid? CustomerId,
    string PayMode,
    decimal TaxableValue,
    decimal TotalDiscount,
    decimal TotalTax,
    decimal TotalAmount,
    decimal BalanceDue,
    List<CreateSaleLineItemRequest> LineItems,
    string? PermitNo = null,
    string? PermitHolderName = null,
    DateOnly? BillDate = null
);

public record CreateSaleLineItemRequest(
    
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
public record CreatePurchaseRequest(
    // Server Guid, or the counter's own supplier id (resolved via Supplier.ClientId).
    string SupplierId,
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
    List<CreatePurchaseLineItemRequest> LineItems,
    DateOnly? BillDate = null
);

public record CreatePurchaseLineItemRequest(
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

public record SaveMaterialRequest(
    string Id,
    string? Barcode,
    string Name,
    string Category,
    string Packing,
    decimal SaleRate,
    decimal TaxPercent
);

public record SaveSupplierRequest(
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
