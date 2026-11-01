using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xoxo.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Baseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CustomerType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "CounterSale.Sale"),
                    Balance = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Customer__3214EC076E4AF7BE", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Materials",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Barcode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Packing = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SaleRate = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TaxPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 5m),
                    StockQty = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Material__3214EC075BE00FE4", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoreLicense",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    LicenseNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LicenseName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ValidityDate = table.Column<DateOnly>(type: "date", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__StoreLic__3214EC07F093FB3F", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ContactNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    VatNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    BankDetails = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DisPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    BalanceType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "Credit"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Supplier__3214EC0740457026", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesBills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    BillNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BillType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "CounterSale.Sale"),
                    BillDate = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "(CONVERT([date],sysutcdatetime()))"),
                    PayMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Cash"),
                    TaxableValue = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TotalDiscount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TotalTax = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    BalanceDue = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "draft"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    IsSynced = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__SalesBil__3214EC07C6136244", x => x.Id);
                    table.ForeignKey(
                        name: "FK__SalesBill__Custo__4BAC3F29",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InventoryStock",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    MaterialId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BatchNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "-"),
                    QtyOnHand = table.Column<int>(type: "int", nullable: false),
                    ReorderLevel = table.Column<int>(type: "int", nullable: false, defaultValue: 10),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Inventor__3214EC0799268D3A", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryStock_Materials",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PurchaseBills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    BillNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChallanNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    NoteNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PayMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Credit"),
                    TpNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    TpDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Discount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Vat = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Stamp = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Tcs = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    LoadingFreight = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    BillDate = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "(CONVERT([date],sysutcdatetime()))"),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "saved"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    IsSynced = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Purchase__3214EC071F1A028F", x => x.Id);
                    table.ForeignKey(
                        name: "FK__PurchaseB__Suppl__75A278F5",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SaleLineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    SalesBillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BarcodeNo = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    MaterialType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    MaterialName = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    BatchNo = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Packing = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: true, defaultValue: 0m),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    TaxPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    MaterialId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    QtyCase = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__SaleLine__3214EC079799C3EF", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleLineItems_Materials",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__SaleLineI__Sales__60A75C0F",
                        column: x => x.SalesBillId,
                        principalTable: "SalesBills",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PurchaseLineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    PurchaseBillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaterialId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BatchNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "-"),
                    Packing = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Qty = table.Column<int>(type: "int", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    DisPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DisAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TaxPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Purchase__3214EC0736F2C1C8", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseLineItems_Materials",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__PurchaseL__Purch__06CD04F7",
                        column: x => x.PurchaseBillId,
                        principalTable: "PurchaseBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryStock_MaterialId",
                table: "InventoryStock",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "UQ_InventoryStock_Material_Batch",
                table: "InventoryStock",
                columns: new[] { "MaterialId", "BatchNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Materials_Barcode",
                table: "Materials",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseBills_SupplierId",
                table: "PurchaseBills",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseLineItems_MaterialId",
                table: "PurchaseLineItems",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseLineItems_PurchaseBillId",
                table: "PurchaseLineItems",
                column: "PurchaseBillId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleLineItems_MaterialId",
                table: "SaleLineItems",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleLineItems_SalesBillId",
                table: "SaleLineItems",
                column: "SalesBillId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesBills_BillDate",
                table: "SalesBills",
                column: "BillDate");

            migrationBuilder.CreateIndex(
                name: "IX_SalesBills_CustomerId",
                table: "SalesBills",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "UQ_SalesBills_BillNo",
                table: "SalesBills",
                column: "BillNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryStock");

            migrationBuilder.DropTable(
                name: "PurchaseLineItems");

            migrationBuilder.DropTable(
                name: "SaleLineItems");

            migrationBuilder.DropTable(
                name: "StoreLicense");

            migrationBuilder.DropTable(
                name: "PurchaseBills");

            migrationBuilder.DropTable(
                name: "Materials");

            migrationBuilder.DropTable(
                name: "SalesBills");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "Customers");
        }
    }
}
