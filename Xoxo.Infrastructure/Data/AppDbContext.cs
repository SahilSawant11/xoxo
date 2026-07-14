using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Xoxo.Infrastructure.Data.Models;

namespace Xoxo.Infrastructure.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Customer> Customers { get; set; }
    public virtual DbSet<Material> Materials { get; set; }
    public virtual DbSet<SalesBill> SalesBills { get; set; }
    public virtual DbSet<SaleLineItem> SaleLineItems { get; set; }
    public virtual DbSet<StoreLicense> StoreLicenses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Customer configuration
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.CustomerType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
        });

        // Material configuration
        modelBuilder.Entity<Material>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Barcode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Packing).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
        });

        // SalesBill configuration
        modelBuilder.Entity<SalesBill>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.BillNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.BillType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PayMode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(e => e.Customer)
                  .WithMany(e => e.SalesBills)
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // SaleLineItem configuration
        modelBuilder.Entity<SaleLineItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.BarcodeNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.MaterialType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.MaterialName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.BatchNo).HasMaxLength(50);
            entity.Property(e => e.Packing).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(e => e.SalesBill)
                  .WithMany(e => e.SaleLineItems)
                  .HasForeignKey(e => e.SalesBillId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // StoreLicense configuration
        modelBuilder.Entity<StoreLicense>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.LicenseNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.LicenseName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            // No CreatedAt property in StoreLicense model
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
