using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xoxo.Api.Auth;
using Xoxo.Infrastructure.Data;
using Xoxo.Infrastructure.Data.Models;

namespace Xoxo.Api.Endpoints;

public sealed record RegisterDeviceRequest(Guid DeviceId, string DeviceName, string StoreName, string EnrollmentCode);

public static class DeviceEndpoints
{
    public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        // A counter registers once (its id is the app's own device_id) and
        // receives an API key, shown only in this response. Registering again
        // with the enrollment code rotates the key (lost-key recovery).
        app.MapPost("/api/devices/register", async (RegisterDeviceRequest request, AppDbContext db, IOptions<AuthOptions> options) =>
        {
            var code = options.Value.EnrollmentCode;
            if (string.IsNullOrWhiteSpace(code))
                return Results.Problem("Device registration is closed on this server.", statusCode: StatusCodes.Status503ServiceUnavailable);
            if (!string.Equals(request.EnrollmentCode, code, StringComparison.Ordinal))
                return Results.Json(new { message = "Invalid enrollment code." }, statusCode: StatusCodes.Status401Unauthorized);
            if (request.DeviceId == Guid.Empty || string.IsNullOrWhiteSpace(request.StoreName))
                return Results.BadRequest(new { message = "DeviceId and StoreName are required." });

            var storeName = request.StoreName.Trim();
            var store = await db.Stores.FirstOrDefaultAsync(s => s.Name == storeName);
            if (store is null)
            {
                store = new Store { Id = Guid.NewGuid(), Name = storeName, CreatedAt = DateTime.UtcNow };
                db.Stores.Add(store);
            }

            var key = DeviceKeys.NewKey();
            var device = await db.Devices.FirstOrDefaultAsync(d => d.Id == request.DeviceId);
            if (device is null)
            {
                device = new Device { Id = request.DeviceId, CreatedAt = DateTime.UtcNow };
                db.Devices.Add(device);
            }
            device.StoreId = store.Id;
            device.Name = string.IsNullOrWhiteSpace(request.DeviceName) ? "Counter" : request.DeviceName.Trim();
            device.ApiKeyHash = DeviceKeys.Hash(key);
            device.IsActive = true;

            await db.SaveChangesAsync();
            return Results.Ok(new { deviceId = device.Id, storeId = store.Id, apiKey = key });
        })
        .WithName("RegisterDevice");

        app.MapGet("/api/devices/me", (HttpContext http) =>
            http.Device() is { } d ? Results.Ok(d) : Results.Ok(new { message = "Device auth disabled" }))
        .WithName("CurrentDevice");

        return app;
    }
}
