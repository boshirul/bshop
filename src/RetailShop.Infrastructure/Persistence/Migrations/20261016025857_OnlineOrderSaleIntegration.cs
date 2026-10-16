using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OnlineOrderSaleIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OnlineOrderId",
                table: "Sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_OnlineOrderId",
                table: "Sales",
                column: "OnlineOrderId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_OnlineOrders_OnlineOrderId",
                table: "Sales",
                column: "OnlineOrderId",
                principalTable: "OnlineOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sales_OnlineOrders_OnlineOrderId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_OnlineOrderId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "OnlineOrderId",
                table: "Sales");
        }
    }
}
