using System;
using System.Collections.Generic;

namespace Xoxo.Infrastructure.Data.Models;

public partial class SaleLineItem
{
    public Guid Id { get; set; }

    public Guid SalesBillId { get; set; }

    public string BarcodeNo { get; set; } = null!;

    public string MaterialType { get; set; } = null!;

    public string MaterialName { get; set; } = null!;

    public string? BatchNo { get; set; }

    public string? Packing { get; set; }

    public int Quantity { get; set; }

    public decimal Rate { get; set; }

    public decimal? DiscountPercent { get; set; }

    public decimal? DiscountAmount { get; set; }

    public decimal TaxPercent { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal Amount { get; set; }

    public int? LineNumber { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool? IsDeleted { get; set; }

    public Guid? MaterialId { get; set; }

    public virtual Material? Material { get; set; }

    public virtual SalesBill SalesBill { get; set; } = null!;
}
