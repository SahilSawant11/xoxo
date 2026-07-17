using System;
using System.Collections.Generic;

namespace Xoxo.Infrastructure.Data.Models;

public partial class PurchaseLineItem
{
    public Guid Id { get; set; }

    public Guid PurchaseBillId { get; set; }

    public string MaterialId { get; set; } = null!;

    public string BatchNo { get; set; } = null!;

    public string? Packing { get; set; }

    public int Qty { get; set; }

    public decimal Rate { get; set; }

    public decimal DisPercent { get; set; }

    public decimal DisAmount { get; set; }

    public decimal TaxPercent { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal Amount { get; set; }

    public int LineNumber { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Material Material { get; set; } = null!;

    public virtual PurchaseBill PurchaseBill { get; set; } = null!;
}
