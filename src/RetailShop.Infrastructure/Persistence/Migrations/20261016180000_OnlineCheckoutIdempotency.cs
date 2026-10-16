using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OnlineCheckoutIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckoutFingerprint",
                table: "OnlineOrders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "OnlineOrders",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnlineOrders_IdempotencyKey",
                table: "OnlineOrders",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OnlineOrders_IdempotencyKey",
                table: "OnlineOrders");

            migrationBuilder.DropColumn(
                name: "CheckoutFingerprint",
                table: "OnlineOrders");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "OnlineOrders");
        }
    }
}
