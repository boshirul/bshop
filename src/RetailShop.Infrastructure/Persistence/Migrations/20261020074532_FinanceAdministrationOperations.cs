using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinanceAdministrationOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinancialReconciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExternalOpeningBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExternalClosingBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BookBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CompletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_FinancialReconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialReconciliations_FinancialAccounts_FinancialAccount~",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FundTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceFinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationFinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FeeAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransferDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_FundTransfers", x => x.Id);
                    table.CheckConstraint("CK_FundTransfers_Accounts", "\"SourceFinancialAccountId\" <> \"DestinationFinancialAccountId\"");
                    table.CheckConstraint("CK_FundTransfers_Amount", "\"Amount\" > 0 AND \"Fee\" >= 0");
                    table.ForeignKey(
                        name: "FK_FundTransfers_FinanceAccounts_FeeAccountId",
                        column: x => x.FeeAccountId,
                        principalTable: "FinanceAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FundTransfers_FinancialAccounts_DestinationFinancialAccount~",
                        column: x => x.DestinationFinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FundTransfers_FinancialAccounts_SourceFinancialAccountId",
                        column: x => x.SourceFinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FundTransfers_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ManualJournalDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_ManualJournalDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManualJournalDrafts_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ManualJournalDraftLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ManualJournalDraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Debit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualJournalDraftLines", x => x.Id);
                    table.CheckConstraint("CK_ManualJournalDraftLines_Amount", "(\"Debit\" > 0 AND \"Credit\" = 0) OR (\"Credit\" > 0 AND \"Debit\" = 0)");
                    table.ForeignKey(
                        name: "FK_ManualJournalDraftLines_FinanceAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "FinanceAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ManualJournalDraftLines_ManualJournalDrafts_ManualJournalDr~",
                        column: x => x.ManualJournalDraftId,
                        principalTable: "ManualJournalDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialReconciliations_FinancialAccountId_StatementDate",
                table: "FinancialReconciliations",
                columns: new[] { "FinancialAccountId", "StatementDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FundTransfers_DestinationFinancialAccountId",
                table: "FundTransfers",
                column: "DestinationFinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FundTransfers_FeeAccountId",
                table: "FundTransfers",
                column: "FeeAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FundTransfers_JournalEntryId",
                table: "FundTransfers",
                column: "JournalEntryId",
                unique: true,
                filter: "\"JournalEntryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FundTransfers_SourceFinancialAccountId",
                table: "FundTransfers",
                column: "SourceFinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ManualJournalDraftLines_AccountId",
                table: "ManualJournalDraftLines",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ManualJournalDraftLines_ManualJournalDraftId",
                table: "ManualJournalDraftLines",
                column: "ManualJournalDraftId");

            migrationBuilder.CreateIndex(
                name: "IX_ManualJournalDrafts_JournalEntryId",
                table: "ManualJournalDrafts",
                column: "JournalEntryId",
                unique: true,
                filter: "\"JournalEntryId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinancialReconciliations");

            migrationBuilder.DropTable(
                name: "FundTransfers");

            migrationBuilder.DropTable(
                name: "ManualJournalDraftLines");

            migrationBuilder.DropTable(
                name: "ManualJournalDrafts");
        }
    }
}
