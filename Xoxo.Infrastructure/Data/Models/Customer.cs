using System;
using System.Collections.Generic;

namespace Xoxo.Infrastructure.Data.Models;

public partial class Customer
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Phone { get; set; }

    public string CustomerType { get; set; } = null!;

    public decimal Balance { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<SalesBill> SalesBills { get; set; } = new List<SalesBill>();
}
