using Microsoft.EntityFrameworkCore;
using Npgsql;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Tests.Persistence;

public sealed class DatabaseConflictMessagesTests
{
    [Fact]
    public void PurchaseSupplierInvoiceConflict_ReturnsSafeMessage()
    {
        var exception = CreateDbUpdateException(
            PostgresErrorCodes.UniqueViolation,
            "IX_Purchases_SupplierId_SupplierInvoiceNumber");

        var message = DatabaseConflictMessages.ForPurchase(exception);

        Assert.Equal("This supplier invoice number has already been used.", message);
    }

    [Fact]
    public void QuotationDetailConflict_ReturnsSafeMessage()
    {
        var exception = CreateDbUpdateException(
            PostgresErrorCodes.UniqueViolation,
            "IX_QuotationDetails_QuotationId_ProductId");

        var message = DatabaseConflictMessages.ForQuotation(exception);

        Assert.Equal("Each product can appear only once in a quotation.", message);
    }

    [Fact]
    public void UnknownPersistenceFailure_IsNotConvertedToAUserMessage()
    {
        var exception = CreateDbUpdateException("22001", null);

        Assert.Null(DatabaseConflictMessages.ForPurchase(exception));
        Assert.Null(DatabaseConflictMessages.ForQuotation(exception));
    }

    private static DbUpdateException CreateDbUpdateException(
        string sqlState,
        string? constraintName) =>
        new(
            "Persistence failed.",
            new PostgresException(
                "Database error.",
                "ERROR",
                "ERROR",
                sqlState,
                detail: null,
                hint: null,
                position: 0,
                internalPosition: 0,
                internalQuery: null,
                where: null,
                schemaName: null,
                tableName: null,
                columnName: null,
                dataTypeName: null,
                constraintName: constraintName,
                file: null,
                line: null,
                routine: null));
}
