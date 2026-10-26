using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixJournalBalanceTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION validate_journal_balance() RETURNS trigger AS $$
                DECLARE target_id uuid;
                DECLARE line_count integer;
                DECLARE debit_total numeric(18,2);
                DECLARE credit_total numeric(18,2);
                BEGIN
                    IF TG_TABLE_NAME = 'JournalEntries' THEN
                        target_id := NEW."Id";
                    ELSE
                        target_id := NEW."JournalEntryId";
                    END IF;
                    SELECT COUNT(*), COALESCE(SUM("Debit"), 0), COALESCE(SUM("Credit"), 0)
                    INTO line_count, debit_total, credit_total
                    FROM "JournalLines" WHERE "JournalEntryId" = target_id;
                    IF line_count < 2 OR debit_total <= 0 OR debit_total <> credit_total THEN
                        RAISE EXCEPTION 'Posted journal must contain at least two balanced lines.';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION validate_journal_balance() RETURNS trigger AS $$
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
                """);
        }
    }
}
