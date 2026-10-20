using RetailShop.Domain.Common;

namespace RetailShop.Domain.Finance;

public sealed class AccountMapping : AuditableEntity
{
    private AccountMapping() { }
    public AccountMapping(AccountMappingType type, string key, Guid accountId,
        Guid? paymentMethodId = null, Guid? expenseCategoryId = null)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Mapping key is required.");
        Type = type; Key = key.Trim().ToLowerInvariant(); AccountId = accountId;
        PaymentMethodId = paymentMethodId; ExpenseCategoryId = expenseCategoryId;
    }
    public AccountMappingType Type { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public Guid AccountId { get; private set; }
    public Account Account { get; private set; } = null!;
    public Guid? PaymentMethodId { get; private set; }
    public Guid? ExpenseCategoryId { get; private set; }
    public bool IsActive { get; private set; } = true;
}

public sealed class FinancialAccount : AuditableEntity
{
    private FinancialAccount() { }
    public FinancialAccount(string code, string name, FinancialAccountType type, Guid accountId,
        string? maskedReference = null)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Financial account code and name are required.");
        Code = code.Trim().ToUpperInvariant(); Name = name.Trim(); Type = type;
        AccountId = accountId; MaskedReference = maskedReference?.Trim();
    }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public FinancialAccountType Type { get; private set; }
    public Guid AccountId { get; private set; }
    public Account Account { get; private set; } = null!;
    public string? MaskedReference { get; private set; }
    public bool IsActive { get; private set; } = true;
}

public sealed class FinanceActivation : AuditableEntity
{
    public FinanceActivation() { Id = SingletonId; }
    public static Guid SingletonId { get; } = Guid.Parse("0199f8b7-0000-7000-8000-000000000001");
    public bool AutomaticPostingEnabled { get; private set; }
    public DateOnly? CutoverDate { get; private set; }
    public bool MandatoryMappingsApproved { get; private set; }
    public bool OpeningBalancesApproved { get; private set; }
    public bool ReconciliationReady { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedOn { get; private set; }
}
