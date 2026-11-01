namespace Xoxo.Infrastructure.Data.Models;

/// <summary>A licensed shop. Every counter (device) belongs to one store.</summary>
public class Store
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? LicenseId { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<Device> Devices { get; set; } = new List<Device>();
}

/// <summary>
/// A Caskly install (one counter). Its id is the app's own device_id, so
/// bills it created offline map straight onto it. Authenticates with an
/// API key whose SHA-256 hash is stored here (the key itself never is).
/// </summary>
public class Device
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public string Name { get; set; } = null!;
    public string ApiKeyHash { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public Store Store { get; set; } = null!;
}

public class Category
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Manufacturer
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Packing
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Excise permit / licence holder a counter sale is made against.</summary>
public class PermitHolder
{
    public string Id { get; set; } = null!;
    public string PermitNo { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? IssuedDate { get; set; }
    public string? WhereGranted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Permanent record of each quantity returned on a sales bill.</summary>
public class SalesReturnEntry
{
    public Guid Id { get; set; }
    public Guid SalesBillId { get; set; }
    public string BillNo { get; set; } = null!;
    public int LineNumber { get; set; }
    public string? MaterialId { get; set; }
    public string MaterialName { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public bool IsFullBillReturn { get; set; }
    public string? ReturnedBy { get; set; }
    public Guid? DeviceId { get; set; }
    public DateTime ReturnedAt { get; set; }
}

/// <summary>Permanent record of each quantity returned to a supplier.</summary>
public class PurchaseReturnEntry
{
    public Guid Id { get; set; }
    public Guid PurchaseBillId { get; set; }
    public string? BillNo { get; set; }
    public int LineNumber { get; set; }
    public string MaterialId { get; set; } = null!;
    public string BatchNo { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public bool IsFullBillReturn { get; set; }
    public string? ReturnedBy { get; set; }
    public Guid? DeviceId { get; set; }
    public DateTime ReturnedAt { get; set; }
}

/// <summary>
/// One row per sync item a device has successfully pushed. The unique
/// (DeviceId, ClientId) key makes every push idempotent: a retry after a
/// dropped connection returns the original result instead of a duplicate.
/// </summary>
public class SyncReceipt
{
    public long Id { get; set; }
    public Guid DeviceId { get; set; }
    public string ClientId { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public string Operation { get; set; } = null!;
    public string? ServerId { get; set; }
    public DateTime ReceivedAt { get; set; }
}
