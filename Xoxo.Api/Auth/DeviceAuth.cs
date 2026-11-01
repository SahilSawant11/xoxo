using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Xoxo.Api.Services;
using Xoxo.Infrastructure.Data;
using Xoxo.Infrastructure.Data.Models;

namespace Xoxo.Api.Auth;

public sealed class AuthOptions
{
    /// <summary>When true (default), every /api call needs X-Device-Id + X-Device-Key.</summary>
    public bool RequireDeviceKey { get; set; } = true;

    /// <summary>Shared secret a new counter must present to register. Empty = registration closed.</summary>
    public string EnrollmentCode { get; set; } = "";
}

public sealed record AuthenticatedDevice(Guid DeviceId, Guid StoreId, string Name);

public static class DeviceKeys
{
    public const string DeviceIdHeader = "X-Device-Id";
    public const string DeviceKeyHeader = "X-Device-Key";
    public const string UserHeader = "X-Caskly-User";

    public static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    public static bool Matches(string key, string storedHash) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(key)), Encoding.ASCII.GetBytes(storedHash));
}

/// <summary>Rejects /api calls from unknown or deactivated counters.</summary>
public sealed class DeviceAuthMiddleware(RequestDelegate next)
{
    private static readonly string[] Open = ["/api/devices/register"];

    public async Task InvokeAsync(HttpContext http, AppDbContext db, Microsoft.Extensions.Options.IOptions<AuthOptions> options)
    {
        var path = http.Request.Path.Value ?? "";
        var guarded = path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) &&
                      !Open.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase));
        if (!guarded || !options.Value.RequireDeviceKey)
        {
            await next(http);
            return;
        }

        if (!Guid.TryParse(http.Request.Headers[DeviceKeys.DeviceIdHeader], out var deviceId) ||
            http.Request.Headers[DeviceKeys.DeviceKeyHeader].ToString() is not { Length: > 0 } key)
        {
            await Reject(http, "Missing device credentials.");
            return;
        }

        var device = await db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deviceId);
        if (device is null || !device.IsActive || !DeviceKeys.Matches(key, device.ApiKeyHash))
        {
            await Reject(http, "Unknown, inactive or mis-keyed device.");
            return;
        }

        http.Items[nameof(AuthenticatedDevice)] = new AuthenticatedDevice(device.Id, device.StoreId, device.Name);
        await db.Devices.Where(d => d.Id == device.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastSeenAt, DateTime.UtcNow));
        await next(http);
    }

    private static Task Reject(HttpContext http, string message)
    {
        http.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return http.Response.WriteAsJsonAsync(new { message });
    }
}

public static class HttpContextOriginExtensions
{
    public static AuthenticatedDevice? Device(this HttpContext http) =>
        http.Items.TryGetValue(nameof(AuthenticatedDevice), out var d) ? d as AuthenticatedDevice : null;

    /// <summary>Where a write came from, for audit columns.</summary>
    public static WriteOrigin Origin(this HttpContext http, string? clientId = null)
    {
        var device = http.Device();
        var user = http.Request.Headers[DeviceKeys.UserHeader].ToString();
        return new WriteOrigin(device?.DeviceId, device?.StoreId, clientId, string.IsNullOrWhiteSpace(user) ? null : user);
    }
}
