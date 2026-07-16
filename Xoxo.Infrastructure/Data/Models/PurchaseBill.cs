using System;
using System.Collections.Generic;

namespace Xoxo.Infrastructure.Data.Models;

public partial class PurchaseBill
{
    public Guid Id { get; set; }

    public string? BillNo { get; set; }

    public Guid SupplierId { get; set; }

    public string? ChallanNo { get; set; }

    public string? NoteNo { get; set; }

    public string PayMode { get; set; } = null!;

    public string? TpNo { get; set; }

    public DateOnly? TpDate { get; set; }

    public string? StNo { get; set; }

    public decimal Discount { get; set; }

    public decimal Vat { get; set; }

    public decimal Stamp { get; set; }

    public decimal Tcs { get; set; }

    public decimal LoadingFreight { get; set; }

    public decimal NetAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public DateOnly BillDate { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsSynced { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ICollection<PurchaseLineItem> PurchaseLineItems { get; set; } = new List<PurchaseLineItem>();

    public virtual Supplier Supplier { get; set; } = null!;
}
