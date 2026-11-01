namespace Xoxo.Infrastructure.Data.Models;

// Columns added for multi-counter / online sync. Kept out of the
// scaffolded files so re-scaffolding never loses them.

public partial class SalesBill
{
    /// <summary>Store the bill belongs to (null for legacy rows).</summary>
    public Guid? StoreId { get; set; }

    /// <summary>Counter (device) that created the bill.</summary>
    public Guid? DeviceId { get; set; }

    /// <summary>The device's own id for the bill — makes sync idempotent.</summary>
    public string? ClientId { get; set; }

    public string? PermitNo { get; set; }
    public string? PermitHolderName { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? ReturnedBy { get; set; }
}

public partial class PurchaseBill
{
    public Guid? StoreId { get; set; }
    public Guid? DeviceId { get; set; }
    public string? ClientId { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? ReturnedBy { get; set; }
}

public partial class Material
{
    public string Manufacturer { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
