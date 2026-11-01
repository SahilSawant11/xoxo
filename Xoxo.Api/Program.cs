using Microsoft.EntityFrameworkCore;
using Xoxo.Api.Auth;
using Xoxo.Api.Endpoints;
using Xoxo.Api.Services;
using Xoxo.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// The connection string is never committed. Provide it via user-secrets
// (development), or the ConnectionStrings__Default environment variable.
var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString) && !builder.Environment.IsEnvironment("Testing"))
{
    throw new InvalidOperationException(
        "ConnectionStrings:Default is not set. Run: dotnet user-secrets set \"ConnectionStrings:Default\" \"<connection string>\" " +
        "--project Xoxo.Api   (or set the ConnectionStrings__Default environment variable).");
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.AddScoped<BillingService>();

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        // No EnableRetryOnFailure: SQL Server's retrying strategy forbids the
        // explicit transactions sync uses (each item is all-or-nothing).
        options.UseSqlServer(connectionString));
}

var app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors();
app.UseHttpsRedirection();
app.UseMiddleware<DeviceAuthMiddleware>();

// Apply pending EF migrations at startup when enabled (Database:MigrateOnStartup).
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.MapGet("/health", async (AppDbContext db) =>
{
    var dbOk = await db.Database.CanConnectAsync();
    return dbOk
        ? Results.Ok(new { status = "ok", database = "ok", version = typeof(Program).Assembly.GetName().Version?.ToString() })
        : Results.Json(new { status = "degraded", database = "unreachable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
}).WithName("Health");

app.MapDeviceEndpoints();
app.MapSyncEndpoints();
app.MapSalesEndpoints();
app.MapPurchaseEndpoints();
app.MapMasterEndpoints();
app.MapLookupEndpoints();
app.MapInventoryEndpoints();
app.MapReportEndpoints();
app.MapDashboardEndpoints();

app.Run();

/// <summary>Exposed for integration tests (WebApplicationFactory).</summary>
public partial class Program;
