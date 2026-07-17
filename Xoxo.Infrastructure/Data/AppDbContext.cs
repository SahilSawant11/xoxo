using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Xoxo.Infrastructure.Data.Models;

namespace Xoxo.Infrastructure.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<InventoryStock> InventoryStocks { get; set; }

    public virtual DbSet<Material> Materials { get; set; }

    public virtual DbSet<PurchaseBill> PurchaseBills { get; set; }

    public virtual DbSet<PurchaseLineItem> PurchaseLineItems { get; set; }

    public virtual DbSet<SaleLineItem> SaleLineItems { get; set; }

    public virtual DbSet<SalesBill> SalesBills { get; set; }

    public virtual DbSet<StoreLicense> StoreLicenses { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Customer__3214EC076E4AF7BE");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Balance).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CustomerType)
                .HasMaxLength(50)
                .HasDefaultValue("CounterSale.Sale");
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<InventoryStock>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Inventor__3214EC0799268D3A");

            entity.ToTable("InventoryStock");

            entity.HasIndex(e => e.MaterialId, "IX_InventoryStock_MaterialId");

            entity.HasIndex(e => new { e.MaterialId, e.BatchNo }, "UQ_InventoryStock_Material_Batch").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.BatchNo)
                .HasMaxLength(30)
                .HasDefaultValue("-");
            entity.Property(e => e.MaterialId).HasMaxLength(20);
            entity.Property(e => e.ReorderLevel).HasDefaultValue(10);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Material).WithMany(p => p.InventoryStocks)
                .HasForeignKey(d => d.MaterialId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InventoryStock_Materials");
        });

        modelBuilder.Entity<Material>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Material__3214EC075BE00FE4");

            entity.HasIndex(e => e.Barcode, "UQ_Materials_Barcode").IsUnique();

            entity.Property(e => e.Id).HasMaxLength(20);
            entity.Property(e => e.Barcode).HasMaxLength(50);
            entity.Property(e => e.Category).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Packing).HasMaxLength(50);
            entity.Property(e => e.SaleRate).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.TaxPercent)
                .HasDefaultValue(5m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<PurchaseBill>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Purchase__3214EC071F1A028F");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.BillDate).HasDefaultValueSql("(CONVERT([date],sysutcdatetime()))");
            entity.Property(e => e.BillNo).HasMaxLength(30);
            entity.Property(e => e.ChallanNo).HasMaxLength(30);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Discount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.LoadingFreight).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.NetAmount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.NoteNo).HasMaxLength(30);
            entity.Property(e => e.PayMode)
                .HasMaxLength(20)
                .HasDefaultValue("Credit");
            entity.Property(e => e.StNo).HasMaxLength(30);
            entity.Property(e => e.Stamp).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("saved");
            entity.Property(e => e.Tcs).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.TpNo).HasMaxLength(30);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Vat).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Supplier).WithMany(p => p.PurchaseBills)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PurchaseB__Suppl__75A278F5");
        });

        modelBuilder.Entity<PurchaseLineItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Purchase__3214EC0736F2C1C8");

            entity.HasIndex(e => e.PurchaseBillId, "IX_PurchaseLineItems_PurchaseBillId");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Amount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.BatchNo)
                .HasMaxLength(30)
                .HasDefaultValue("-");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DisAmount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.DisPercent).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.MaterialId).HasMaxLength(20);
            entity.Property(e => e.Packing).HasMaxLength(50);
            entity.Property(e => e.Rate).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.TaxPercent).HasColumnType("decimal(5, 2)");

            entity.HasOne(d => d.Material).WithMany(p => p.PurchaseLineItems)
                .HasForeignKey(d => d.MaterialId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseLineItems_Materials");

            entity.HasOne(d => d.PurchaseBill).WithMany(p => p.PurchaseLineItems)
                .HasForeignKey(d => d.PurchaseBillId)
                .HasConstraintName("FK__PurchaseL__Purch__06CD04F7");
        });

        modelBuilder.Entity<SaleLineItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__SaleLine__3214EC079799C3EF");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.BarcodeNo)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.BatchNo)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.DiscountAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DiscountPercent)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.MaterialId).HasMaxLength(20);
            entity.Property(e => e.MaterialName)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.MaterialType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Packing)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Rate).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxPercent).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Material).WithMany(p => p.SaleLineItems)
                .HasForeignKey(d => d.MaterialId)
                .HasConstraintName("FK_SaleLineItems_Materials");

            entity.HasOne(d => d.SalesBill).WithMany(p => p.SaleLineItems)
                .HasForeignKey(d => d.SalesBillId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__SaleLineI__Sales__60A75C0F");
        });

        modelBuilder.Entity<SalesBill>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__SalesBil__3214EC07C6136244");

            entity.HasIndex(e => e.BillDate, "IX_SalesBills_BillDate");

            entity.HasIndex(e => e.BillNo, "UQ_SalesBills_BillNo").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.BalanceDue).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.BillDate).HasDefaultValueSql("(CONVERT([date],sysutcdatetime()))");
            entity.Property(e => e.BillNo).HasMaxLength(30);
            entity.Property(e => e.BillType)
                .HasMaxLength(50)
                .HasDefaultValue("CounterSale.Sale");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.PayMode)
                .HasMaxLength(20)
                .HasDefaultValue("Cash");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("draft");
            entity.Property(e => e.TaxableValue).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.TotalDiscount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.TotalTax).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Customer).WithMany(p => p.SalesBills)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK__SalesBill__Custo__4BAC3F29");
        });

        modelBuilder.Entity<StoreLicense>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StoreLic__3214EC07F093FB3F");

            entity.ToTable("StoreLicense");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.LicenseName).HasMaxLength(150);
            entity.Property(e => e.LicenseNo).HasMaxLength(50);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Supplier__3214EC0740457026");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Address).HasMaxLength(300);
            entity.Property(e => e.BalanceType)
                .HasMaxLength(10)
                .HasDefaultValue("Credit");
            entity.Property(e => e.BankDetails).HasMaxLength(200);
            entity.Property(e => e.ContactNo).HasMaxLength(30);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DisPercent).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.OpeningBalance).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.VatNo).HasMaxLength(30);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
