using System;
using System.Collections.Generic;

namespace Xoxo.Infrastructure.Data.Models;

public partial class StoreLicense
{
    public Guid Id { get; set; }

    public string LicenseNo { get; set; } = null!;

    public string LicenseName { get; set; } = null!;

    public DateOnly? ValidityDate { get; set; }

    public DateTime UpdatedAt { get; set; }
}
