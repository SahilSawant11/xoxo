using Microsoft.EntityFrameworkCore;
using Xoxo.Infrastructure.Data.Models;

namespace Xoxo.Infrastructure.Data;

public partial class AppDbContext
{
    public virtual DbSet<Store> Stores { get; set; }
    public virtual DbSet<Device> Devices { get; set; }
    public virtual DbSet<Category> Categories { get; set; }
    public virtual DbSet<Manufacturer> Manufacturers { get; set; }
    public virtual DbSet<Packing> Packings { get; set; }
    public virtual DbSet<PermitHolder> PermitHolders { get; set; }
    public virtual DbSet<SalesReturnEntry> SalesReturnEntries { get; set; }
    public virtual DbSet<PurchaseReturnEntry> PurchaseReturnEntries { get; set; }
    public virtual DbSet<SyncReceipt> SyncReceipts { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Store>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.LicenseId).HasMaxLength(40);
        });

        modelBuilder.Entity<Device>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.ApiKeyHash).HasMaxLength(64);
            e.HasOne(x => x.Store).WithMany(s => s.Devices).HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.HasKey(x => x.Name);
            e.Property(x => x.Name).HasMaxLength(50);
            e.Property(x => x.Description).HasMaxLength(300);
        });
        modelBuilder.Entity<Manufacturer>(e =>
        {
            e.HasKey(x => x.Name);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(300);
        });
        modelBuilder.Entity<Packing>(e =>
        {
            e.HasKey(x => x.Name);
            e.Property(x => x.Name).HasMaxLength(50);
            e.Property(x => x.Description).HasMaxLength(300);
        });
        modelBuilder.Entity<PermitHolder>(e =>
        {
            e.Property(x => x.Id).HasMaxLength(50);
            e.Property(x => x.PermitNo).HasMaxLength(50);
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.IssuedDate).HasMaxLength(30);
            e.Property(x => x.WhereGranted).HasMaxLength(100);
            e.HasIndex(x => x.PermitNo).IsUnique();
        });

        modelBuilder.Entity<SalesReturnEntry>(e =>
        {
            e.Property(x => x.BillNo).HasMaxLength(40);
            e.Property(x => x.MaterialId).HasMaxLength(20);
            e.Property(x => x.MaterialName).HasMaxLength(200);
            e.Property(x => x.Rate).HasColumnType("decimal(10, 2)");
            e.Property(x => x.Amount).HasColumnType("decimal(10, 2)");
            e.Property(x => x.ReturnedBy).HasMaxLength(50);
            e.HasIndex(x => x.SalesBillId);
            e.HasIndex(x => x.ReturnedAt);
        });
        modelBuilder.Entity<PurchaseReturnEntry>(e =>
        {
            e.Property(x => x.BillNo).HasMaxLength(30);
            e.Property(x => x.MaterialId).HasMaxLength(20);
            e.Property(x => x.BatchNo).HasMaxLength(30);
            e.Property(x => x.Rate).HasColumnType("decimal(10, 2)");
            e.Property(x => x.Amount).HasColumnType("decimal(10, 2)");
            e.Property(x => x.ReturnedBy).HasMaxLength(50);
            e.HasIndex(x => x.PurchaseBillId);
            e.HasIndex(x => x.ReturnedAt);
        });

        modelBuilder.Entity<SyncReceipt>(e =>
        {
            e.Property(x => x.ClientId).HasMaxLength(80);
            e.Property(x => x.EntityType).HasMaxLength(30);
            e.Property(x => x.Operation).HasMaxLength(20);
            e.Property(x => x.ServerId).HasMaxLength(80);
            e.HasIndex(x => new { x.DeviceId, x.ClientId }).IsUnique();
        });

        modelBuilder.Entity<SalesBill>(e =>
        {
            e.Property(x => x.ClientId).HasMaxLength(80);
            e.Property(x => x.PermitNo).HasMaxLength(50);
            e.Property(x => x.PermitHolderName).HasMaxLength(150);
            e.Property(x => x.ReturnedBy).HasMaxLength(50);
            e.Property(x => x.BillNo).HasMaxLength(40);
            // Bill numbers are sequential per counter, so two counters can both
            // have CS-2627-000001. Unique per device instead of globally.
            var old = e.Metadata.FindIndex("UQ_SalesBills_BillNo");
            if (old != null) e.Metadata.RemoveIndex(old);
            e.HasIndex(x => new { x.DeviceId, x.BillNo }, "UQ_SalesBills_Device_BillNo").IsUnique();
            e.HasIndex(x => new { x.DeviceId, x.ClientId }, "IX_SalesBills_Device_ClientId");
            e.HasIndex(x => x.StoreId);
        });

        modelBuilder.Entity<PurchaseBill>(e =>
        {
            e.Property(x => x.ClientId).HasMaxLength(80);
            e.Property(x => x.ReturnedBy).HasMaxLength(50);
            e.HasIndex(x => new { x.DeviceId, x.ClientId }, "IX_PurchaseBills_Device_ClientId");
            e.HasIndex(x => x.StoreId);
            e.HasIndex(x => x.BillDate, "IX_PurchaseBills_BillDate");
        });

        modelBuilder.Entity<Supplier>(e =>
        {
            e.Property(x => x.ClientId).HasMaxLength(80);
            e.HasIndex(x => x.ClientId, "UQ_Suppliers_ClientId").IsUnique().HasFilter("[ClientId] IS NOT NULL");
        });

        modelBuilder.Entity<Material>(e =>
        {
            e.Property(x => x.Manufacturer).HasMaxLength(100).HasDefaultValue("");
            e.Property(x => x.IsActive).HasDefaultValue(true);
        });

        if (!Database.IsSqlServer()) MakePortable(modelBuilder);
    }

    /// <summary>
    /// The scaffolded model uses SQL Server functions as column defaults
    /// (newid(), sysutcdatetime()). Other providers (SQLite in tests) can't
    /// run them; the app always sets these values itself, so drop them.
    /// </summary>
    private static void MakePortable(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.GetDefaultValueSql() != null) property.SetDefaultValueSql(null);
                var type = property.GetColumnType();
                if (type != null && (type.StartsWith("datetime") || type.StartsWith("decimal"))) property.SetColumnType(null);
            }
        }
    }
}
