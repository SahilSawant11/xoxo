using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xoxo.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SupplierClientId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                table: "Suppliers",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Suppliers_ClientId",
                table: "Suppliers",
                column: "ClientId",
                unique: true,
                filter: "[ClientId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Suppliers_ClientId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "Suppliers");
        }
    }
}
