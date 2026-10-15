using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserProfileAndExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarStorageKey",
                table: "Users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExpenseCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_ExpenseCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseVouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExpenseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Payee = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PaymentMethodId = table.Column<Guid>(type: "uuid", nullable: true),
                    GeneralRemarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    PostedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_ExpenseVouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseVouchers_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseVoucherDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseVoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
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
                    table.PrimaryKey("PK_ExpenseVoucherDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseVoucherDetails_ExpenseCategories_ExpenseCategoryId",
                        column: x => x.ExpenseCategoryId,
                        principalTable: "ExpenseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExpenseVoucherDetails_ExpenseVouchers_ExpenseVoucherId",
                        column: x => x.ExpenseVoucherId,
                        principalTable: "ExpenseVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_Code",
                table: "ExpenseCategories",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseVoucherDetails_ExpenseCategoryId",
                table: "ExpenseVoucherDetails",
                column: "ExpenseCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseVoucherDetails_ExpenseVoucherId",
                table: "ExpenseVoucherDetails",
                column: "ExpenseVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseVouchers_ExpenseDate_Status",
                table: "ExpenseVouchers",
                columns: new[] { "ExpenseDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseVouchers_PaymentMethodId",
                table: "ExpenseVouchers",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseVouchers_VoucherNumber",
                table: "ExpenseVouchers",
                column: "VoucherNumber",
                unique: true);

            migrationBuilder.InsertData(
                table: "ExpenseCategories",
                columns: new[] { "Id", "Code", "Name", "IsActive", "CreatedOn", "IsDeleted" },
                values: new object[,]
                {
                    { new Guid("1fdbec42-8f43-4d5a-9e90-881f9e4d0001"), "SALARY", "Salaries and Wages", true, new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero), false },
                    { new Guid("1fdbec42-8f43-4d5a-9e90-881f9e4d0002"), "RENT", "Shop Rent", true, new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero), false },
                    { new Guid("1fdbec42-8f43-4d5a-9e90-881f9e4d0003"), "UTILITIES", "Utilities", true, new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero), false },
                    { new Guid("1fdbec42-8f43-4d5a-9e90-881f9e4d0004"), "TRANSPORT", "Transportation", true, new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero), false },
                    { new Guid("1fdbec42-8f43-4d5a-9e90-881f9e4d0005"), "MARKETING", "Marketing and Advertising", true, new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero), false },
                    { new Guid("1fdbec42-8f43-4d5a-9e90-881f9e4d0006"), "REPAIRS", "Repairs and Maintenance", true, new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero), false },
                    { new Guid("1fdbec42-8f43-4d5a-9e90-881f9e4d0007"), "SUPPLIES", "Office and Shop Supplies", true, new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero), false },
                    { new Guid("1fdbec42-8f43-4d5a-9e90-881f9e4d0008"), "FEES", "License and Government Fees", true, new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero), false },
                    { new Guid("1fdbec42-8f43-4d5a-9e90-881f9e4d0009"), "COMMUNICATION", "Communication", true, new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero), false },
                    { new Guid("1fdbec42-8f43-4d5a-9e90-881f9e4d0010"), "OTHER", "Other Operating Expenses", true, new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero), false }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData("ExpenseCategories", "Id", new Guid("1fdbec42-8f43-4d5a-9e90-881f9e4d0001"));
            migrationBuilder.DeleteData("ExpenseCategories", "Id", new Guid("1fdbec42-8f43-4d5a-9e90-881f9e4d0002"));
            migrationBuilder.DeleteData("ExpenseCategories", "Id", new Guid("1fdbec42-8f43-4d5a-9e90-881f9e4d0003"));
            migrationBuilder.DeleteData("ExpenseCategories", "Id", new Guid("1fdbec42-8f43-4d5a-9e90-881f9e4d0004"));
            migrationBuilder.DeleteData("ExpenseCategories", "Id", new Guid("1fdbec42-8f43-4d5a-9e90-881f9e4d0005"));
            migrationBuilder.DeleteData("ExpenseCategories", "Id", new Guid("1fdbec42-8f43-4d5a-9e90-881f9e4d0006"));
            migrationBuilder.DeleteData("ExpenseCategories", "Id", new Guid("1fdbec42-8f43-4d5a-9e90-881f9e4d0007"));
            migrationBuilder.DeleteData("ExpenseCategories", "Id", new Guid("1fdbec42-8f43-4d5a-9e90-881f9e4d0008"));
            migrationBuilder.DeleteData("ExpenseCategories", "Id", new Guid("1fdbec42-8f43-4d5a-9e90-881f9e4d0009"));
            migrationBuilder.DeleteData("ExpenseCategories", "Id", new Guid("1fdbec42-8f43-4d5a-9e90-881f9e4d0010"));
            migrationBuilder.DropTable(
                name: "ExpenseVoucherDetails");

            migrationBuilder.DropTable(
                name: "ExpenseCategories");

            migrationBuilder.DropTable(
                name: "ExpenseVouchers");

            migrationBuilder.DropColumn(
                name: "AvatarStorageKey",
                table: "Users");
        }
    }
}
