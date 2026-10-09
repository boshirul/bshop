using RetailShop.Domain.Contacts;

namespace RetailShop.Domain.Sales;

public sealed class CustomerLedgerEntry
{
    private CustomerLedgerEntry()
    {
    }

    public CustomerLedgerEntry(
        Guid customerId,
        DateTimeOffset entryDate,
        CustomerLedgerEntryType entryType,
        decimal debit,
        decimal credit,
        string referenceType,
        Guid referenceId,
        string referenceNumber,
        Guid createdBy,
        string? notes)
    {
        if (debit < 0 || credit < 0 || (debit == 0) == (credit == 0))
        {
            throw new InvalidOperationException(
                "A customer ledger entry requires either a debit or a credit.");
        }

        CustomerId = customerId;
        EntryDate = entryDate;
        EntryType = entryType;
        Debit = debit;
        Credit = credit;
        ReferenceType = referenceType.Trim();
        ReferenceId = referenceId;
        ReferenceNumber = referenceNumber.Trim();
        CreatedBy = createdBy;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid CustomerId { get; private set; }
    public Customer Customer { get; private set; } = null!;
    public DateTimeOffset EntryDate { get; private set; }
    public CustomerLedgerEntryType EntryType { get; private set; }
    public decimal Debit { get; private set; }
    public decimal Credit { get; private set; }
    public string ReferenceType { get; private set; } = string.Empty;
    public Guid ReferenceId { get; private set; }
    public string ReferenceNumber { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedOn { get; private set; } = DateTimeOffset.UtcNow;
}
