using System;
using System.Collections.Generic;

namespace Xoxo.Infrastructure.Data.Models;

public partial class Supplier
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Address { get; set; }

    public string? ContactNo { get; set; }

    public string? Email { get; set; }

    public string? VatNo { get; set; }

    public string? BankDetails { get; set; }

    public decimal DisPercent { get; set; }

    public decimal OpeningBalance { get; set; }

    public string BalanceType { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<PurchaseBill> PurchaseBills { get; set; } = new List<PurchaseBill>();
}
