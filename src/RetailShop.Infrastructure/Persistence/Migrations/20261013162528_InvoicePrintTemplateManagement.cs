using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvoicePrintTemplateManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvoiceTemplateSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultFormat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    A4Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    ShortEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ShowLogo = table.Column<bool>(type: "boolean", nullable: false),
                    LogoPlacement = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HeaderText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FooterText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AccentColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    ThermalMarginMm = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceTemplateSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaleInvoiceSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateVersion = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleInvoiceSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleInvoiceSnapshots_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaleInvoiceSnapshots_SaleId",
                table: "SaleInvoiceSnapshots",
                column: "SaleId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceTemplateSettings");

            migrationBuilder.DropTable(
                name: "SaleInvoiceSnapshots");
        }
    }
}
