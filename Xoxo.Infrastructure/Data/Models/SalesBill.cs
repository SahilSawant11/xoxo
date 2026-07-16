using System;
using System.Collections.Generic;

namespace Xoxo.Infrastructure.Data.Models;

public partial class SalesBill
{
    public Guid Id { get; set; }

    public string BillNo { get; set; } = null!;

    public Guid? CustomerId { get; set; }

    public string BillType { get; set; } = null!;

    public DateOnly BillDate { get; set; }

    public string PayMode { get; set; } = null!;

    public decimal TaxableValue { get; set; }

    public decimal TotalDiscount { get; set; }

    public decimal TotalTax { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal BalanceDue { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsSynced { get; set; }

    public bool IsDeleted { get; set; }

    public virtual Customer? Customer { get; set; }

    public virtual ICollection<SaleLineItem> SaleLineItems { get; set; } = new List<SaleLineItem>();
}
