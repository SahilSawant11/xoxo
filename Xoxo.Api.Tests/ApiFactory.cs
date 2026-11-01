using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xoxo.Infrastructure.Data;
using Xoxo.Infrastructure.Data.Models;

namespace Xoxo.Api.Tests;

/// <summary>
/// The real API, in-process, on a private in-memory SQLite database with
/// device auth switched ON. Seeds one material (stock 100) and one supplier.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string EnrollmentCode = "test-enroll";
    public static readonly Guid SupplierId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:RequireDeviceKey"] = "true",
            ["Auth:EnrollmentCode"] = EnrollmentCode,
        }));
        builder.ConfigureServices(services =>
        {
            _connection.Open();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            Seed(db);
        });
    }

    private static void Seed(AppDbContext db)
    {
        var now = DateTime.UtcNow;
        db.Materials.Add(new Material
        {
            Id = "M1", Barcode = "111", Name = "Royal Stag 750", Category = "Whisky", Packing = "750 ML",
            SaleRate = 1000, TaxPercent = 0, CreatedAt = now, UpdatedAt = now, Manufacturer = "Pernod",
        });
        db.InventoryStocks.Add(new InventoryStock { Id = Guid.NewGuid(), MaterialId = "M1", BatchNo = "-", QtyOnHand = 100, ReorderLevel = 10, UpdatedAt = now });
        db.Suppliers.Add(new Supplier { Id = SupplierId, Name = "Royal Spirits", BalanceType = "Credit", CreatedAt = now, UpdatedAt = now });
        db.Categories.Add(new Category { Name = "Whisky", CreatedAt = now, UpdatedAt = now });
        db.SaveChanges();
    }

    public T WithDb<T>(Func<AppDbContext, T> f)
    {
        using var scope = Services.CreateScope();
        return f(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Registers a new counter and returns a client that sends its credentials.</summary>
    public async Task<(HttpClient Client, Guid DeviceId)> RegisterDeviceAsync(string store = "Test Wines", Guid? deviceId = null)
    {
        var id = deviceId ?? Guid.NewGuid();
        var anon = CreateClient();
        var res = await anon.PostAsJsonAsync("/api/devices/register", new { deviceId = id, deviceName = "Counter", storeName = store, enrollmentCode = EnrollmentCode });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<RegisterResponse>();
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Device-Id", id.ToString());
        client.DefaultRequestHeaders.Add("X-Device-Key", body!.ApiKey);
        client.DefaultRequestHeaders.Add("X-Caskly-User", "cashier");
        return (client, id);
    }

    public sealed record RegisterResponse(Guid DeviceId, Guid StoreId, string ApiKey);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }
}
