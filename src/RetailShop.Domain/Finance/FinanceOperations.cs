using RetailShop.Domain.Common;

namespace RetailShop.Domain.Finance;

public enum FinanceDocumentStatus { Draft, Posted, Reversed }
public enum ReconciliationStatus { Draft, Completed }
public enum OpeningBalanceBatchStatus { Draft, Approved, Posted, Reversed }

public sealed class ManualJournalDraft : AuditableEntity
{
    private ManualJournalDraft() { }
    public ManualJournalDraft(DateOnly date, string description, string? reference, Guid createdBy)
    { BusinessDate=date; Description=Required(description); Reference=Clean(reference); CreatedBy=createdBy; }
    public DateOnly BusinessDate { get; private set; }
    public string Description { get; private set; } = "";
    public string? Reference { get; private set; }
    public FinanceDocumentStatus Status { get; private set; }
    public Guid? JournalEntryId { get; private set; }
    public ICollection<ManualJournalDraftLine> Lines { get; private set; } = [];
    public void ReplaceLines(IEnumerable<ManualJournalDraftLine> lines) { if(Status!=FinanceDocumentStatus.Draft) throw new InvalidOperationException("Posted drafts are immutable."); Lines.Clear(); foreach(var x in lines) Lines.Add(x); }
    public void MarkPosted(Guid journalId) { if(Status!=FinanceDocumentStatus.Draft) throw new InvalidOperationException("Draft is already posted."); JournalEntryId=journalId; Status=FinanceDocumentStatus.Posted; }
    private static string Required(string v)=>string.IsNullOrWhiteSpace(v)?throw new ArgumentException("Description is required."):v.Trim();
    private static string? Clean(string? v)=>string.IsNullOrWhiteSpace(v)?null:v.Trim();
}
public sealed class ManualJournalDraftLine
{
    private ManualJournalDraftLine() { }
    public ManualJournalDraftLine(Guid accountId, decimal debit, decimal credit, string? description=null) { AccountId=accountId; Debit=debit; Credit=credit; Description=description?.Trim(); }
    public Guid Id { get; private init; }=Guid.CreateVersion7(); public Guid ManualJournalDraftId { get; private set; }
    public Guid AccountId { get; private set; } public decimal Debit { get; private set; } public decimal Credit { get; private set; } public string? Description { get; private set; }
}
public sealed class FundTransfer : AuditableEntity
{
    private FundTransfer() { }
    public FundTransfer(Guid sourceId,Guid destinationId,decimal amount,decimal fee,Guid? feeAccountId,DateOnly date,string? reference,string? description,Guid createdBy)
    { if(sourceId==destinationId) throw new ArgumentException("Source and destination accounts must differ."); if(amount<=0||fee<0) throw new ArgumentException("Transfer amount must be positive and fee cannot be negative."); SourceFinancialAccountId=sourceId;DestinationFinancialAccountId=destinationId;Amount=amount;Fee=fee;FeeAccountId=feeAccountId;TransferDate=date;Reference=reference?.Trim();Description=description?.Trim();CreatedBy=createdBy; }
    public Guid SourceFinancialAccountId{get;private set;} public Guid DestinationFinancialAccountId{get;private set;} public decimal Amount{get;private set;} public decimal Fee{get;private set;} public Guid? FeeAccountId{get;private set;} public DateOnly TransferDate{get;private set;} public string? Reference{get;private set;} public string? Description{get;private set;} public FinanceDocumentStatus Status{get;private set;} public Guid? JournalEntryId{get;private set;}
    public void MarkPosted(Guid journalId){if(Status!=FinanceDocumentStatus.Draft)throw new InvalidOperationException("Transfer is already posted.");JournalEntryId=journalId;Status=FinanceDocumentStatus.Posted;}
}
public sealed class FinancialReconciliation : AuditableEntity
{
    private FinancialReconciliation() { }
    public FinancialReconciliation(Guid financialAccountId,DateOnly statementDate,decimal opening,decimal closing,decimal bookBalance,Guid createdBy)
    { FinancialAccountId=financialAccountId;StatementDate=statementDate;ExternalOpeningBalance=opening;ExternalClosingBalance=closing;BookBalance=bookBalance;CreatedBy=createdBy; }
    public Guid FinancialAccountId{get;private set;} public DateOnly StatementDate{get;private set;} public decimal ExternalOpeningBalance{get;private set;} public decimal ExternalClosingBalance{get;private set;} public decimal BookBalance{get;private set;} public decimal Difference=>ExternalClosingBalance-BookBalance; public ReconciliationStatus Status{get;private set;} public Guid? CompletedBy{get;private set;} public DateTimeOffset? CompletedOn{get;private set;}
    public void Complete(Guid by){if(Difference!=0)throw new InvalidOperationException("A reconciliation with a difference cannot be completed.");Status=ReconciliationStatus.Completed;CompletedBy=by;CompletedOn=DateTimeOffset.UtcNow;}
}
public sealed class OpeningBalanceBatch : AuditableEntity
{
 private OpeningBalanceBatch(){} public OpeningBalanceBatch(DateOnly date,string description,Guid by){BusinessDate=date;Description=string.IsNullOrWhiteSpace(description)?throw new ArgumentException("Description is required."):description.Trim();CreatedBy=by;}
 public DateOnly BusinessDate{get;private set;} public string Description{get;private set;}=""; public OpeningBalanceBatchStatus Status{get;private set;} public Guid? ApprovedBy{get;private set;} public DateTimeOffset? ApprovedOn{get;private set;} public Guid? JournalEntryId{get;private set;} public ICollection<OpeningBalanceLine> Lines{get;private set;}=[];
 public void ReplaceLines(IEnumerable<OpeningBalanceLine> lines){if(Status!=OpeningBalanceBatchStatus.Draft)throw new InvalidOperationException("Only draft opening batches can be changed.");Lines.Clear();foreach(var x in lines)Lines.Add(x);}
 public void Approve(Guid by){if(Status!=OpeningBalanceBatchStatus.Draft)throw new InvalidOperationException("Opening batch is not a draft.");if(Lines.Count<2||Lines.Sum(x=>x.Debit)!=Lines.Sum(x=>x.Credit)||Lines.Sum(x=>x.Debit)<=0)throw new InvalidOperationException("Opening balances must contain balanced positive entries.");Status=OpeningBalanceBatchStatus.Approved;ApprovedBy=by;ApprovedOn=DateTimeOffset.UtcNow;}
 public void MarkPosted(Guid journal){if(Status!=OpeningBalanceBatchStatus.Approved)throw new InvalidOperationException("Opening batch is not approved.");Status=OpeningBalanceBatchStatus.Posted;JournalEntryId=journal;}
}
public sealed class OpeningBalanceLine
{
 private OpeningBalanceLine(){} public OpeningBalanceLine(Guid accountId,decimal debit,decimal credit,string? reconciliationType=null,Guid? reconciliationId=null){if(debit<0||credit<0||debit==0&&credit==0||debit>0&&credit>0)throw new ArgumentException("One positive debit or credit is required.");AccountId=accountId;Debit=debit;Credit=credit;ReconciliationType=reconciliationType?.Trim();ReconciliationId=reconciliationId;}
 public Guid Id{get;private init;}=Guid.CreateVersion7(); public Guid OpeningBalanceBatchId{get;private set;} public Guid AccountId{get;private set;} public decimal Debit{get;private set;} public decimal Credit{get;private set;} public string? ReconciliationType{get;private set;} public Guid? ReconciliationId{get;private set;}
}
public sealed class FinanceActivationAudit : AuditableEntity
{
 private FinanceActivationAudit(){} public FinanceActivationAudit(string action,string details,Guid by){Action=action;Details=details;CreatedBy=by;} public string Action{get;private set;}="";public string Details{get;private set;}="";
}
