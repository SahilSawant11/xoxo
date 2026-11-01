using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Xoxo.Api.Tests;

public sealed class ApiTests : IDisposable
{
    private readonly ApiFactory _api = new();
    public void Dispose() => _api.Dispose();

    private static object SalePayload(string billNo, int qty = 2, string? date = null) => new
    {
        billNo,
        customerId = (string?)null,
        permitNo = "20598",
        permitHolderName = "B J RAMA",
        payMode = "Cash",
        taxableValue = 1000m * qty,
        totalDiscount = 0m,
        totalTax = 0m,
        totalAmount = 1000m * qty,
        balanceDue = 0m,
        billDate = date,
        lineItems = new[]
        {
            new
            {
                materialId = "M1", barcodeNo = "111", materialType = "Whisky", materialName = "Royal Stag 750",
                batchNo = (string?)null, packing = "750 ML", quantity = qty, qtyCase = 0, rate = 1000m,
                discountPercent = 0m, discountAmount = 0m, taxPercent = 0m, taxAmount = 0m, amount = 1000m * qty,
            },
        },
    };

    private static async Task<JsonElement[]> Push(HttpClient c, params object[] items)
    {
        var res = await c.PostAsJsonAsync("/api/sync/push", new { items });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("results").EnumerateArray().ToArray();
    }

    private int Stock() => _api.WithDb(db => db.InventoryStocks.Where(s => s.MaterialId == "M1").Sum(s => s.QtyOnHand));

    [Fact]
    public async Task Health_is_public_and_reports_database()
    {
        var res = await _api.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("\"database\":\"ok\"", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Api_rejects_unknown_devices_and_bad_enrollment()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.CreateClient().GetAsync("/api/materials")).StatusCode);

        var bad = await _api.CreateClient().PostAsJsonAsync("/api/devices/register",
            new { deviceId = Guid.NewGuid(), deviceName = "X", storeName = "S", enrollmentCode = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);

        var forged = _api.CreateClient();
        var (_, deviceId) = await _api.RegisterDeviceAsync();
        forged.DefaultRequestHeaders.Add("X-Device-Id", deviceId.ToString());
        forged.DefaultRequestHeaders.Add("X-Device-Key", "not-the-key");
        Assert.Equal(HttpStatusCode.Unauthorized, (await forged.GetAsync("/api/materials")).StatusCode);

        var (client, _) = await _api.RegisterDeviceAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/materials")).StatusCode);
    }

    [Fact]
    public async Task Sync_sale_is_idempotent_and_keeps_offline_details()
    {
        var (c, deviceId) = await _api.RegisterDeviceAsync();
        var item = new { clientId = "local-sale-abc", entityType = "sale", operation = "create", payload = SalePayload("CS-2627-000001", 2, "2026-10-28") };

        var first = await Push(c, item);
        Assert.Equal("applied", first[0].GetProperty("status").GetString());
        var serverId = first[0].GetProperty("serverId").GetString();

        // The counter lost the response and retries: no second bill, no second stock deduction.
        var retry = await Push(c, item);
        Assert.Equal("duplicate", retry[0].GetProperty("status").GetString());
        Assert.Equal(serverId, retry[0].GetProperty("serverId").GetString());

        Assert.Equal(98, Stock());
        var bill = _api.WithDb(db => db.SalesBills.Single());
        Assert.Equal(deviceId, bill.DeviceId);
        Assert.Equal("local-sale-abc", bill.ClientId);
        Assert.Equal("20598", bill.PermitNo);
        Assert.Equal(new DateOnly(2026, 10, 28), bill.BillDate); // the sale's real date, not the sync date
    }

    [Fact]
    public async Task Bill_numbers_are_unique_per_counter_not_globally()
    {
        var (counter1, _) = await _api.RegisterDeviceAsync();
        var (counter2, _) = await _api.RegisterDeviceAsync();
        var a = await Push(counter1, new { clientId = "a", entityType = "sale", operation = "create", payload = SalePayload("CS-2627-000001") });
        var b = await Push(counter2, new { clientId = "b", entityType = "sale", operation = "create", payload = SalePayload("CS-2627-000001") });
        Assert.Equal("applied", a[0].GetProperty("status").GetString());
        Assert.Equal("applied", b[0].GetProperty("status").GetString());

        var dup = await Push(counter1, new { clientId = "c", entityType = "sale", operation = "create", payload = SalePayload("CS-2627-000001") });
        Assert.Equal("rejected", dup[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Sync_return_keeps_bill_logs_lines_and_restores_stock()
    {
        var (c, _) = await _api.RegisterDeviceAsync();
        await Push(c, new { clientId = "sale-1", entityType = "sale", operation = "create", payload = SalePayload("CS-1", 3) });
        Assert.Equal(97, Stock());

        var ret = await Push(c, new { clientId = "sale-1:return", entityType = "sale", operation = "return", payload = new { id = "sale-1" } });
        Assert.Equal("applied", ret[0].GetProperty("status").GetString());
        Assert.Equal(100, Stock());

        var (bill, entries) = _api.WithDb(db => (db.SalesBills.Single(), db.SalesReturnEntries.ToList()));
        Assert.Equal("returned", bill.Status);
        Assert.NotNull(bill.ReturnedAt);
        Assert.Equal("cashier", bill.ReturnedBy);
        Assert.Equal(3, entries.Single().Quantity);

        var again = await Push(c, new { clientId = "sale-1:return-2", entityType = "sale", operation = "return", payload = new { id = "sale-1" } });
        Assert.Equal("rejected", again[0].GetProperty("status").GetString());
        Assert.Equal(100, Stock());
    }

    [Fact]
    public async Task One_bad_item_does_not_block_the_rest_of_the_batch()
    {
        var (c, _) = await _api.RegisterDeviceAsync();
        var results = await Push(c,
            new { clientId = "ok-1", entityType = "sale", operation = "create", payload = SalePayload("CS-10") },
            new { clientId = "bad", entityType = "sale", operation = "create", payload = new { billNo = 123 } },
            new { clientId = "unknown-material", entityType = "purchase", operation = "create", payload = new { supplierId = Guid.NewGuid(), payMode = "Credit", lineItems = new[] { new { materialId = "M1", batchNo = "B", qty = 1, rate = 1m, disPercent = 0m, disAmount = 0m, taxPercent = 0m, taxAmount = 0m, amount = 1m } } } },
            new { clientId = "ok-2", entityType = "sale", operation = "create", payload = SalePayload("CS-11") });

        Assert.Equal(new[] { "applied", "rejected", "rejected", "applied" }, results.Select(r => r.GetProperty("status").GetString()).ToArray());
        Assert.Equal(2, _api.WithDb(db => db.SalesBills.Count()));
        Assert.Equal(2, _api.WithDb(db => db.SyncReceipts.Count()));
    }

    [Fact]
    public async Task Sync_purchase_adds_stock_per_batch()
    {
        var (c, _) = await _api.RegisterDeviceAsync();
        var payload = new
        {
            supplierId = ApiFactory.SupplierId, billNo = "INV-9", payMode = "Credit", discount = 0m, vat = 0m, stamp = 0m, tcs = 0m,
            loadingFreight = 0m, netAmount = 600m, totalAmount = 600m, billDate = "2026-10-30",
            lineItems = new[] { new { materialId = "M1", batchNo = "B-26", packing = "12x750", qty = 12, rate = 50m, disPercent = 0m, disAmount = 0m, taxPercent = 0m, taxAmount = 0m, amount = 600m } },
        };
        var r = await Push(c, new { clientId = "local-purchase-1", entityType = "purchase", operation = "create", payload });
        Assert.Equal("applied", r[0].GetProperty("status").GetString());
        Assert.Equal(112, Stock());
        Assert.Equal(12, _api.WithDb(db => db.InventoryStocks.Single(s => s.BatchNo == "B-26").QtyOnHand));
    }

    [Fact]
    public async Task Category_rename_cascades_to_materials()
    {
        var (c, _) = await _api.RegisterDeviceAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync("/api/categories", new { name = "Whisky" })).StatusCode);
        var res = await c.PutAsJsonAsync("/api/categories/Whisky", new { name = "Scotch Whisky", description = "IMFL" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Scotch Whisky", _api.WithDb(db => db.Materials.Single().Category));
        Assert.False(_api.WithDb(db => db.Categories.Any(x => x.Name == "Whisky")));
    }

    [Fact]
    public async Task Legacy_sales_endpoint_still_works_and_records_device()
    {
        var (c, deviceId) = await _api.RegisterDeviceAsync();
        var res = await c.PostAsJsonAsync("/api/sales", SalePayload("CS-LEGACY"));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal(deviceId, _api.WithDb(db => db.SalesBills.Single().DeviceId));

        var list = await c.GetFromJsonAsync<JsonElement>("/api/sales");
        Assert.Equal(1, list.GetArrayLength());
    }

    [Fact]
    public async Task Permit_holders_can_be_created_and_listed()
    {
        var (c, _) = await _api.RegisterDeviceAsync();
        var res = await c.PostAsJsonAsync("/api/license-holders", new { permitNo = "20598", name = "B J RAMA", issuedDate = "24-Mar-2017", whereGranted = "Life Time" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync("/api/license-holders", new { permitNo = "20598", name = "Dup" })).StatusCode);
        var list = await c.GetFromJsonAsync<JsonElement>("/api/license-holders");
        Assert.Equal("B J RAMA", list[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task First_connection_uploads_masters_then_bills_using_counter_ids()
    {
        var (c, _) = await _api.RegisterDeviceAsync();
        var masters = new
        {
            categories = new[] { new { name = "Rum", description = (string?)null } },
            manufacturers = new[] { new { name = "Bacardi", description = (string?)null } },
            packings = new[] { new { name = "750 ML", description = (string?)null } },
            licenseHolders = new[] { new { permitNo = "20598", name = "B J RAMA", issuedDate = "24-Mar-2017", whereGranted = "Life Time" } },
            materials = new[] { new { id = "SKU-1006", barcode = "8901001000066", name = "Bacardi White Rum", category = "Rum", packing = "750 ML", saleRate = 1180m, taxPercent = 18m, manufacturer = "Bacardi", isActive = true } },
            suppliers = new[] { new { clientId = "SUP-001", name = "Royal Spirits Distributors", address = "Goa", contactNo = (string?)null, email = (string?)null, vatNo = (string?)null, bankDetails = (string?)null, disPercent = 0m, openingBalance = 0m, balanceType = "Credit" } },
            openingStock = new[] { new { materialId = "SKU-1006", qtyOnHand = 16, reorderLevel = 10 } },
        };
        var res = await c.PostAsJsonAsync("/api/sync/masters", masters);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("openingStockSeeded").GetInt32());
        Assert.Equal(0, body.GetProperty("errors").GetArrayLength());

        // A purchase referencing the counter's supplier id "SUP-001".
        var purchase = new
        {
            supplierId = "SUP-001", billNo = "INV-1", payMode = "Credit", discount = 0m, vat = 0m, stamp = 0m, tcs = 0m,
            loadingFreight = 0m, netAmount = 1200m, totalAmount = 1200m,
            lineItems = new[] { new { materialId = "SKU-1006", batchNo = "-", packing = "12x750", qty = 12, rate = 100m, disPercent = 0m, disAmount = 0m, taxPercent = 0m, taxAmount = 0m, amount = 1200m } },
        };
        var r = await Push(c, new { clientId = "local-purchase-x", entityType = "purchase", operation = "create", payload = purchase });
        Assert.Equal("applied", r[0].GetProperty("status").GetString());
        Assert.Equal(28, _api.WithDb(db => db.InventoryStocks.Where(s => s.MaterialId == "SKU-1006").Sum(s => s.QtyOnHand)));

        // Uploading masters again is harmless and does not reset stock.
        var again = await (await c.PostAsJsonAsync("/api/sync/masters", masters)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, again.GetProperty("openingStockSeeded").GetInt32());
        Assert.Equal(28, _api.WithDb(db => db.InventoryStocks.Where(s => s.MaterialId == "SKU-1006").Sum(s => s.QtyOnHand)));
        Assert.Equal(1, _api.WithDb(db => db.Suppliers.Count(s => s.ClientId == "SUP-001")));
    }

    [Fact]
    public async Task Barcode_clash_is_reported_not_fatal()
    {
        var (c, _) = await _api.RegisterDeviceAsync();
        var res = await c.PostAsJsonAsync("/api/sync/masters", new
        {
            materials = new[]
            {
                new { id = "OTHER", barcode = "111", name = "Clash", category = "Whisky", packing = "750 ML", saleRate = 1m, taxPercent = 0m, manufacturer = "", isActive = true },
                new { id = "NEW", barcode = "222", name = "Fine", category = "Whisky", packing = "750 ML", saleRate = 1m, taxPercent = 0m, manufacturer = "", isActive = true },
            },
        });
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("errors").GetArrayLength());
        Assert.True(_api.WithDb(db => db.Materials.Any(m => m.Id == "NEW")));
    }

    /// <summary>
    /// Contract test: replays sync items generated by the Flutter app itself
    /// (Ruts test/online_sync_test.dart). Fails if either side renames a field.
    /// </summary>
    [Fact]
    public async Task App_generated_sync_payloads_are_accepted()
    {
        var (c, _) = await _api.RegisterDeviceAsync();
        var masters = new
        {
            materials = new[]
            {
                new { id = "A", barcode = "BC-A", name = "Item A", category = "Whisky", packing = "750 ML", saleRate = 100m, taxPercent = 0m, manufacturer = "", isActive = true },
                new { id = "B", barcode = "BC-B", name = "Item B", category = "Whisky", packing = "750 ML", saleRate = 100m, taxPercent = 0m, manufacturer = "", isActive = true },
            },
            suppliers = new[] { new { clientId = "SUP-001", name = "Royal Spirits", address = (string?)null, contactNo = (string?)null, email = (string?)null, vatNo = (string?)null, bankDetails = (string?)null, disPercent = 0m, openingBalance = 0m, balanceType = "Credit" } },
            openingStock = new[] { new { materialId = "A", qtyOnHand = 50, reorderLevel = 10 }, new { materialId = "B", qtyOnHand = 50, reorderLevel = 10 } },
        };
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/sync/masters", masters)).StatusCode);

        var json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sync_push_sample.json"));
        var push = async () =>
        {
            var res = await c.PostAsync("/api/sync/push", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("results").EnumerateArray()
                .Select(r => (r.GetProperty("status").GetString(), r.TryGetProperty("error", out var e) ? e.ToString() : null)).ToList();
        };

        var first = await push();
        Assert.All(first, r => Assert.True(r.Item1 == "applied", $"expected applied, got {r.Item1}: {r.Item2}"));
        var bill = _api.WithDb(db => db.PurchaseBills.Single());
        Assert.Equal(new DateOnly(2026, 11, 1), bill.TpDate);

        var again = await push();
        Assert.All(again, r => Assert.Equal("duplicate", r.Item1));
    }
}
