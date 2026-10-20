using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinanceAccountingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinanceAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NormalBalance = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystemControlled = table.Column<bool>(type: "boolean", nullable: false),
                    HasPostings = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_FinanceAccounts", x => x.Id);
                    table.CheckConstraint("CK_FinanceAccounts_Parent", "\"ParentId\" IS NULL OR \"ParentId\" <> \"Id\"");
                    table.ForeignKey(
                        name: "FK_FinanceAccounts_FinanceAccounts_ParentId",
                        column: x => x.ParentId,
                        principalTable: "FinanceAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceActivations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AutomaticPostingEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CutoverDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MandatoryMappingsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    OpeningBalancesApproved = table.Column<bool>(type: "boolean", nullable: false),
                    ReconciliationReady = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_FinanceActivations", x => x.Id);
                    table.CheckConstraint("CK_FinanceActivations_Enablement", "NOT \"AutomaticPostingEnabled\" OR (\"CutoverDate\" IS NOT NULL AND \"MandatoryMappingsApproved\" AND \"OpeningBalancesApproved\" AND \"ReconciliationReady\" AND \"ApprovedBy\" IS NOT NULL AND \"ApprovedOn\" IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "FiscalPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ClosedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_FiscalPeriods", x => x.Id);
                    table.CheckConstraint("CK_FiscalPeriods_DateRange", "\"EndDate\" >= \"StartDate\"");
                });

            migrationBuilder.CreateTable(
                name: "AccountMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentMethodId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpenseCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_AccountMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountMappings_ExpenseCategories_ExpenseCategoryId",
                        column: x => x.ExpenseCategoryId,
                        principalTable: "ExpenseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountMappings_FinanceAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "FinanceAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountMappings_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancialAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaskedReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_FinancialAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialAccounts_FinanceAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "FinanceAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PostedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostingKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReversesJournalId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_JournalEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalEntries_FiscalPeriods_FiscalPeriodId",
                        column: x => x.FiscalPeriodId,
                        principalTable: "FiscalPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalEntries_JournalEntries_ReversesJournalId",
                        column: x => x.ReversesJournalId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Debit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalLines", x => x.Id);
                    table.CheckConstraint("CK_JournalLines_DebitCredit", "(\"Debit\" > 0 AND \"Credit\" = 0) OR (\"Credit\" > 0 AND \"Debit\" = 0)");
                    table.ForeignKey(
                        name: "FK_JournalLines_FinanceAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "FinanceAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalLines_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountMappings_AccountId",
                table: "AccountMappings",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountMappings_ExpenseCategoryId",
                table: "AccountMappings",
                column: "ExpenseCategoryId",
                unique: true,
                filter: "\"ExpenseCategoryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountMappings_PaymentMethodId",
                table: "AccountMappings",
                column: "PaymentMethodId",
                unique: true,
                filter: "\"PaymentMethodId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountMappings_Type_Key",
                table: "AccountMappings",
                columns: new[] { "Type", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceAccounts_Code",
                table: "FinanceAccounts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceAccounts_ParentId",
                table: "FinanceAccounts",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAccounts_AccountId",
                table: "FinancialAccounts",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAccounts_Code",
                table: "FinancialAccounts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FiscalPeriods_StartDate_EndDate",
                table: "FiscalPeriods",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_FiscalPeriodId",
                table: "JournalEntries",
                column: "FiscalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_Number",
                table: "JournalEntries",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_ReversesJournalId",
                table: "JournalEntries",
                column: "ReversesJournalId",
                unique: true,
                filter: "\"ReversesJournalId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_SourceType_SourceId_PostingKind",
                table: "JournalEntries",
                columns: new[] { "SourceType", "SourceId", "PostingKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalLines_AccountId",
                table: "JournalLines",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalLines_JournalEntryId",
                table: "JournalLines",
                column: "JournalEntryId");

            migrationBuilder.Sql("""
                ALTER TABLE "FiscalPeriods"
                ADD CONSTRAINT "EX_FiscalPeriods_NoOverlap"
                EXCLUDE USING gist (daterange("StartDate", "EndDate", '[]') WITH &&)
                WHERE (NOT "IsDeleted");

                CREATE FUNCTION prevent_posted_journal_mutation() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'Posted journals and lines are immutable; create a reversal instead.';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER "TR_JournalEntries_Immutable"
                BEFORE UPDATE OR DELETE ON "JournalEntries"
                FOR EACH ROW EXECUTE FUNCTION prevent_posted_journal_mutation();

                CREATE TRIGGER "TR_JournalLines_Immutable"
                BEFORE UPDATE OR DELETE ON "JournalLines"
                FOR EACH ROW EXECUTE FUNCTION prevent_posted_journal_mutation();

                CREATE FUNCTION validate_new_journal_line() RETURNS trigger AS $$
                DECLARE account_active boolean;
                DECLARE account_kind varchar(10);
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM "JournalEntries"
                        WHERE "Id" = NEW."JournalEntryId"
                          AND xmin = txid_current()::text::xid) THEN
                        RAISE EXCEPTION 'Lines cannot be added to an already-posted journal.';
                    END IF;
                    SELECT "IsActive", "Kind" INTO account_active, account_kind
                    FROM "FinanceAccounts" WHERE "Id" = NEW."AccountId";
                    IF NOT COALESCE(account_active, false) OR account_kind <> 'Posting' THEN
                        RAISE EXCEPTION 'Journal lines require an active posting account.';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER "TR_JournalLines_ValidateInsert"
                BEFORE INSERT ON "JournalLines"
                FOR EACH ROW EXECUTE FUNCTION validate_new_journal_line();

                CREATE FUNCTION validate_journal_balance() RETURNS trigger AS $$
                DECLARE target_id uuid;
                DECLARE line_count integer;
                DECLARE debit_total numeric(18,2);
                DECLARE credit_total numeric(18,2);
                BEGIN
                    target_id := CASE WHEN TG_TABLE_NAME = 'JournalEntries' THEN NEW."Id" ELSE NEW."JournalEntryId" END;
                    SELECT COUNT(*), COALESCE(SUM("Debit"), 0), COALESCE(SUM("Credit"), 0)
                    INTO line_count, debit_total, credit_total
                    FROM "JournalLines" WHERE "JournalEntryId" = target_id;
                    IF line_count < 2 OR debit_total <= 0 OR debit_total <> credit_total THEN
                        RAISE EXCEPTION 'Posted journal must contain at least two balanced lines.';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE CONSTRAINT TRIGGER "TR_JournalEntries_Balanced"
                AFTER INSERT ON "JournalEntries"
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION validate_journal_balance();

                CREATE CONSTRAINT TRIGGER "TR_JournalLines_Balanced"
                AFTER INSERT ON "JournalLines"
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION validate_journal_balance();

                CREATE FUNCTION protect_posted_account_identity() RETURNS trigger AS $$
                BEGIN
                    IF (OLD."HasPostings" OR NEW."HasPostings") AND
                       (OLD."Code", OLD."Type", OLD."NormalBalance", OLD."Kind", OLD."ParentId")
                       IS DISTINCT FROM
                       (NEW."Code", NEW."Type", NEW."NormalBalance", NEW."Kind", NEW."ParentId") THEN
                        RAISE EXCEPTION 'Posted account identity and classification are immutable.';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER "TR_FinanceAccounts_ProtectIdentity"
                BEFORE UPDATE ON "FinanceAccounts"
                FOR EACH ROW EXECUTE FUNCTION protect_posted_account_identity();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "TR_FinanceAccounts_ProtectIdentity" ON "FinanceAccounts";
                DROP FUNCTION IF EXISTS protect_posted_account_identity();
                DROP TRIGGER IF EXISTS "TR_JournalLines_Balanced" ON "JournalLines";
                DROP TRIGGER IF EXISTS "TR_JournalEntries_Balanced" ON "JournalEntries";
                DROP FUNCTION IF EXISTS validate_journal_balance();
                DROP TRIGGER IF EXISTS "TR_JournalLines_ValidateInsert" ON "JournalLines";
                DROP FUNCTION IF EXISTS validate_new_journal_line();
                DROP TRIGGER IF EXISTS "TR_JournalLines_Immutable" ON "JournalLines";
                DROP TRIGGER IF EXISTS "TR_JournalEntries_Immutable" ON "JournalEntries";
                DROP FUNCTION IF EXISTS prevent_posted_journal_mutation();
                ALTER TABLE "FiscalPeriods" DROP CONSTRAINT IF EXISTS "EX_FiscalPeriods_NoOverlap";
                """);
            migrationBuilder.DropTable(
                name: "AccountMappings");

            migrationBuilder.DropTable(
                name: "FinanceActivations");

            migrationBuilder.DropTable(
                name: "FinancialAccounts");

            migrationBuilder.DropTable(
                name: "JournalLines");

            migrationBuilder.DropTable(
                name: "FinanceAccounts");

            migrationBuilder.DropTable(
                name: "JournalEntries");

            migrationBuilder.DropTable(
                name: "FiscalPeriods");
        }
    }
}
