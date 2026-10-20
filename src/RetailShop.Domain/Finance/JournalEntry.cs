using RetailShop.Domain.Common;

namespace RetailShop.Domain.Finance;

public sealed class JournalEntry : AuditableEntity
{
    private JournalEntry() { }
    public JournalEntry(string number, DateOnly businessDate, DateTimeOffset postedOn,
        string sourceType, Guid sourceId, string postingKind, string payloadHash,
        string? description, Guid postedBy, Guid fiscalPeriodId, Guid? reversesJournalId)
    {
        Number = number; BusinessDate = businessDate; PostedOn = postedOn;
        SourceType = sourceType; SourceId = sourceId; PostingKind = postingKind;
        PayloadHash = payloadHash; Description = description?.Trim(); PostedBy = postedBy;
        FiscalPeriodId = fiscalPeriodId; ReversesJournalId = reversesJournalId;
    }
    public string Number { get; private set; } = string.Empty;
    public DateOnly BusinessDate { get; private set; }
    public DateTimeOffset PostedOn { get; private set; }
    public string SourceType { get; private set; } = string.Empty;
    public Guid SourceId { get; private set; }
    public string PostingKind { get; private set; } = string.Empty;
    public string PayloadHash { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public JournalStatus Status { get; private set; } = JournalStatus.Posted;
    public Guid PostedBy { get; private set; }
    public Guid FiscalPeriodId { get; private set; }
    public FiscalPeriod FiscalPeriod { get; private set; } = null!;
    public Guid? ReversesJournalId { get; private set; }
    public JournalEntry? ReversesJournal { get; private set; }
    public JournalEntry? ReversalJournal { get; private set; }
    public ICollection<JournalLine> Lines { get; private set; } = [];
    public void AddLine(JournalLine line) => Lines.Add(line);
}

public sealed class JournalLine
{
    private JournalLine() { }
    public JournalLine(Guid accountId, decimal debit, decimal credit, string? description)
    {
        if (debit < 0 || credit < 0 || debit == 0 && credit == 0 || debit > 0 && credit > 0)
            throw new ArgumentException("A journal line must contain one positive debit or credit amount.");
        Id = Guid.CreateVersion7(); AccountId = accountId; Debit = debit; Credit = credit;
        Description = description?.Trim();
    }
    public Guid Id { get; private set; }
    public Guid JournalEntryId { get; private set; }
    public JournalEntry JournalEntry { get; private set; } = null!;
    public Guid AccountId { get; private set; }
    public Account Account { get; private set; } = null!;
    public decimal Debit { get; private set; }
    public decimal Credit { get; private set; }
    public string? Description { get; private set; }
}
