using RetailShop.Domain.Common;
using RetailShop.Domain.Settings;

namespace RetailShop.Domain.Expenses;

public sealed class ExpenseVoucher : AuditableEntity
{
    private ExpenseVoucher() { }
    public ExpenseVoucher(string voucherNumber, DateOnly expenseDate, string payee, Guid? paymentMethodId, string? remarks, Guid createdBy)
    {
        VoucherNumber = voucherNumber; ExpenseDate = expenseDate; Payee = payee.Trim(); PaymentMethodId = paymentMethodId; GeneralRemarks = Clean(remarks); CreatedBy = createdBy;
    }
    public string VoucherNumber { get; private set; } = string.Empty;
    public DateOnly ExpenseDate { get; private set; }
    public string Payee { get; private set; } = string.Empty;
    public Guid? PaymentMethodId { get; private set; }
    public PaymentMethod? PaymentMethod { get; private set; }
    public string? GeneralRemarks { get; private set; }
    public ExpenseVoucherStatus Status { get; private set; } = ExpenseVoucherStatus.Draft;
    public decimal TotalAmount { get; private set; }
    public Guid? PostedBy { get; private set; }
    public DateTimeOffset? PostedOn { get; private set; }
    public Guid? VoidedBy { get; private set; }
    public DateTimeOffset? VoidedOn { get; private set; }
    public string? VoidReason { get; private set; }
    public ICollection<ExpenseVoucherDetail> Details { get; private set; } = [];
    public void ReplaceDetails(IEnumerable<ExpenseVoucherDetail> details)
    {
        if (Status != ExpenseVoucherStatus.Draft) throw new InvalidOperationException("Only draft vouchers can be edited.");
        Details.Clear(); foreach (var detail in details) Details.Add(detail); Recalculate();
    }
    public void Update(DateOnly date, string payee, Guid? paymentMethodId, string? remarks)
    {
        if (Status != ExpenseVoucherStatus.Draft) throw new InvalidOperationException("Only draft vouchers can be edited.");
        ExpenseDate = date; Payee = payee.Trim(); PaymentMethodId = paymentMethodId; GeneralRemarks = Clean(remarks);
    }
    public void Post(Guid by) { if (Status != ExpenseVoucherStatus.Draft || Details.Count == 0 || TotalAmount <= 0) throw new InvalidOperationException("Only a completed draft can be posted."); Status = ExpenseVoucherStatus.Posted; PostedBy = by; PostedOn = DateTimeOffset.UtcNow; }
    public void Void(Guid by, string reason) { if (Status != ExpenseVoucherStatus.Posted || string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("A posted voucher and void reason are required."); Status = ExpenseVoucherStatus.Voided; VoidedBy = by; VoidedOn = DateTimeOffset.UtcNow; VoidReason = reason.Trim(); }
    private void Recalculate() => TotalAmount = Details.Sum(x => x.Amount);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
