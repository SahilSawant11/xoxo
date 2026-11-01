using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xoxo.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class OnlineReadiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_SalesBills_BillNo",
                table: "SalesBills");

            migrationBuilder.AlterColumn<string>(
                name: "BillNo",
                table: "SalesBills",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                table: "SalesBills",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeviceId",
                table: "SalesBills",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PermitHolderName",
                table: "SalesBills",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PermitNo",
                table: "SalesBills",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnedAt",
                table: "SalesBills",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnedBy",
                table: "SalesBills",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StoreId",
                table: "SalesBills",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                table: "PurchaseBills",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeviceId",
                table: "PurchaseBills",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnedAt",
                table: "PurchaseBills",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnedBy",
                table: "PurchaseBills",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StoreId",
                table: "PurchaseBills",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Materials",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Manufacturer",
                table: "Materials",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "Manufacturers",
                columns: table => new
                {
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Manufacturers", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "Packings",
                columns: table => new
                {
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packings", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "PermitHolders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PermitNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IssuedDate = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    WhereGranted = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermitHolders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReturnEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseBillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    MaterialId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BatchNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    IsFullBillReturn = table.Column<bool>(type: "bit", nullable: false),
                    ReturnedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReturnedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReturnEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesReturnEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesBillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillNo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    MaterialId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MaterialName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    IsFullBillReturn = table.Column<bool>(type: "bit", nullable: false),
                    ReturnedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReturnedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesReturnEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    LicenseId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncReceipts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ServerId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApiKeyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesBills_Device_ClientId",
                table: "SalesBills",
                columns: new[] { "DeviceId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesBills_StoreId",
                table: "SalesBills",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "UQ_SalesBills_Device_BillNo",
                table: "SalesBills",
                columns: new[] { "DeviceId", "BillNo" },
                unique: true,
                filter: "[DeviceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseBills_BillDate",
                table: "PurchaseBills",
                column: "BillDate");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseBills_Device_ClientId",
                table: "PurchaseBills",
                columns: new[] { "DeviceId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseBills_StoreId",
                table: "PurchaseBills",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_StoreId",
                table: "Devices",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_PermitHolders_PermitNo",
                table: "PermitHolders",
                column: "PermitNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnEntries_PurchaseBillId",
                table: "PurchaseReturnEntries",
                column: "PurchaseBillId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnEntries_ReturnedAt",
                table: "PurchaseReturnEntries",
                column: "ReturnedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturnEntries_ReturnedAt",
                table: "SalesReturnEntries",
                column: "ReturnedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturnEntries_SalesBillId",
                table: "SalesReturnEntries",
                column: "SalesBillId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncReceipts_DeviceId_ClientId",
                table: "SyncReceipts",
                columns: new[] { "DeviceId", "ClientId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "Manufacturers");

            migrationBuilder.DropTable(
                name: "Packings");

            migrationBuilder.DropTable(
                name: "PermitHolders");

            migrationBuilder.DropTable(
                name: "PurchaseReturnEntries");

            migrationBuilder.DropTable(
                name: "SalesReturnEntries");

            migrationBuilder.DropTable(
                name: "SyncReceipts");

            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_SalesBills_Device_ClientId",
                table: "SalesBills");

            migrationBuilder.DropIndex(
                name: "IX_SalesBills_StoreId",
                table: "SalesBills");

            migrationBuilder.DropIndex(
                name: "UQ_SalesBills_Device_BillNo",
                table: "SalesBills");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseBills_BillDate",
                table: "PurchaseBills");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseBills_Device_ClientId",
                table: "PurchaseBills");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseBills_StoreId",
                table: "PurchaseBills");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "SalesBills");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "SalesBills");

            migrationBuilder.DropColumn(
                name: "PermitHolderName",
                table: "SalesBills");

            migrationBuilder.DropColumn(
                name: "PermitNo",
                table: "SalesBills");

            migrationBuilder.DropColumn(
                name: "ReturnedAt",
                table: "SalesBills");

            migrationBuilder.DropColumn(
                name: "ReturnedBy",
                table: "SalesBills");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "SalesBills");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "PurchaseBills");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "PurchaseBills");

            migrationBuilder.DropColumn(
                name: "ReturnedAt",
                table: "PurchaseBills");

            migrationBuilder.DropColumn(
                name: "ReturnedBy",
                table: "PurchaseBills");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "PurchaseBills");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "Manufacturer",
                table: "Materials");

            migrationBuilder.AlterColumn<string>(
                name: "BillNo",
                table: "SalesBills",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);

            migrationBuilder.CreateIndex(
                name: "UQ_SalesBills_BillNo",
                table: "SalesBills",
                column: "BillNo",
                unique: true);
        }
    }
}
