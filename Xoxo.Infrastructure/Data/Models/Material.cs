using System;
using System.Collections.Generic;

namespace Xoxo.Infrastructure.Data.Models;

public partial class Material
{
    public Guid Id { get; set; }

    public string Barcode { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string Packing { get; set; } = null!;

    public decimal SaleRate { get; set; }

    public decimal TaxPercent { get; set; }

    public int StockQty { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<InventoryStock> InventoryStocks { get; set; } = new List<InventoryStock>();

    public virtual ICollection<PurchaseLineItem> PurchaseLineItems { get; set; } = new List<PurchaseLineItem>();

    public virtual ICollection<SaleLineItem> SaleLineItems { get; set; } = new List<SaleLineItem>();
}
