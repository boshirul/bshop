using RetailShop.Domain.Common;

namespace RetailShop.Domain.Finance;

public sealed class FiscalPeriod : AuditableEntity
{
    private FiscalPeriod() { }
    public FiscalPeriod(string name, DateOnly startDate, DateOnly endDate)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Period name is required.");
        if (endDate < startDate) throw new ArgumentException("Period end date cannot precede start date.");
        Name = name.Trim(); StartDate = startDate; EndDate = endDate;
    }
    public string Name { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public FiscalPeriodStatus Status { get; private set; } = FiscalPeriodStatus.Open;
    public Guid? ClosedBy { get; private set; }
    public DateTimeOffset? ClosedOn { get; private set; }
    public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;
    public bool AllowsPosting(bool allowSoftClosed) => Status == FiscalPeriodStatus.Open ||
        allowSoftClosed && Status == FiscalPeriodStatus.SoftClosed;
    public void ChangeStatus(FiscalPeriodStatus status, Guid performedBy, DateTimeOffset now)
    {
        Status = status;
        ClosedBy = status == FiscalPeriodStatus.Open ? null : performedBy;
        ClosedOn = status == FiscalPeriodStatus.Open ? null : now;
    }
}
