using System;
using System.Collections.Generic;

namespace Xoxo.Infrastructure.Data.Models;

public partial class InventoryStock
{
    public Guid Id { get; set; }

    public string MaterialId { get; set; } = null!;

    public string BatchNo { get; set; } = null!;

    public int QtyOnHand { get; set; }

    public int ReorderLevel { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Material Material { get; set; } = null!;
}
