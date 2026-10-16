using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace RetailShop.Infrastructure.Persistence;

/// <summary>
/// Converts expected PostgreSQL integrity errors into safe, user-facing
/// messages. Unknown persistence failures intentionally remain unhandled so
/// the global exception handler records their trace identifier.
/// </summary>
internal static class DatabaseConflictMessages
{
    public static string? ForPurchase(DbUpdateException exception) =>
        GetMessage(FindPostgresException(exception), new Dictionary<string, string>
        {
            ["IX_Purchases_SupplierId_SupplierInvoiceNumber"] =
                "This supplier invoice number has already been used.",
            ["IX_Purchases_PurchaseNumber"] =
                "A purchase was created at the same time. Reload and try again.",
            ["IX_PurchaseDetails_PurchaseId_ProductId"] =
                "Each product can appear only once in a purchase."
        }, "A duplicate purchase record was detected. Reload and try again.");

    public static string? ForQuotation(DbUpdateException exception) =>
        GetMessage(FindPostgresException(exception), new Dictionary<string, string>
        {
            ["IX_Quotations_QuotationNumber"] =
                "A quotation was created at the same time. Reload and try again.",
            ["IX_QuotationDetails_QuotationId_ProductId"] =
                "Each product can appear only once in a quotation."
        }, "A duplicate quotation record was detected. Reload and try again.");

    private static string? GetMessage(
        PostgresException? exception,
        IReadOnlyDictionary<string, string> uniqueConstraintMessages,
        string genericUniqueMessage)
    {
        if (exception is null)
        {
            return null;
        }

        if (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return exception.ConstraintName is not null &&
                   uniqueConstraintMessages.TryGetValue(exception.ConstraintName, out var message)
                ? message
                : genericUniqueMessage;
        }

        if (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            return "The selected record is no longer available. Reload and try again.";
        }

        if (exception.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
        {
            return "Another transaction changed the same data. Reload and try again.";
        }

        return null;
    }

    private static PostgresException? FindPostgresException(Exception exception) =>
        exception switch
        {
            PostgresException postgresException => postgresException,
            { InnerException: not null } => FindPostgresException(exception.InnerException),
            _ => null
        };
}
