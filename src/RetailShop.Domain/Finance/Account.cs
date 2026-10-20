using RetailShop.Domain.Common;

namespace RetailShop.Domain.Finance;

public sealed class Account : AuditableEntity
{
    private Account() { }

    public Account(string code, string name, AccountType type, NormalBalance normalBalance,
        AccountKind kind, Guid? parentId = null, bool isSystemControlled = false)
    {
        SetIdentity(code, name, type, normalBalance, kind, parentId, isSystemControlled);
    }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public AccountType Type { get; private set; }
    public NormalBalance NormalBalance { get; private set; }
    public AccountKind Kind { get; private set; }
    public Guid? ParentId { get; private set; }
    public Account? Parent { get; private set; }
    public ICollection<Account> Children { get; private set; } = [];
    public bool IsActive { get; private set; } = true;
    public bool IsSystemControlled { get; private set; }
    public bool HasPostings { get; private set; }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Account name is required.");
        Name = name.Trim();
    }

    public void SetActive(bool active) => IsActive = active;
    public void MarkPosted() => HasPostings = true;

    public void AssignParent(Account? parent)
    {
        if (HasPostings) throw new InvalidOperationException("Posted account hierarchy cannot be changed.");
        if (parent is null) { ParentId = null; Parent = null; return; }
        if (parent.Id == Id) throw new ArgumentException("An account cannot be its own parent.");
        if (parent.Kind != AccountKind.Group) throw new ArgumentException("A parent account must be a group account.");
        if (parent.Type != Type) throw new ArgumentException("Parent and child account types must match.");
        for (var ancestor = parent; ancestor is not null; ancestor = ancestor.Parent)
            if (ancestor.Id == Id) throw new ArgumentException("Circular account hierarchy is not allowed.");
        Parent = parent; ParentId = parent.Id;
    }

    public void ChangeIdentity(string code, AccountType type, NormalBalance normalBalance,
        AccountKind kind, Guid? parentId)
    {
        if (HasPostings) throw new InvalidOperationException("Posted account identity cannot be changed.");
        SetIdentity(code, Name, type, normalBalance, kind, parentId, IsSystemControlled);
    }

    private void SetIdentity(string code, string name, AccountType type, NormalBalance normalBalance,
        AccountKind kind, Guid? parentId, bool systemControlled)
    {
        code = code.Trim().ToUpperInvariant();
        if (code.Length is < 2 or > 30 || code.Any(c => !char.IsLetterOrDigit(c) && c is not '.' and not '-'))
            throw new ArgumentException("Account code must be 2-30 letters, numbers, dots or hyphens.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Account name is required.");
        if (parentId == Id) throw new ArgumentException("An account cannot be its own parent.");
        Code = code; Name = name.Trim(); Type = type; NormalBalance = normalBalance;
        Kind = kind; ParentId = parentId; IsSystemControlled = systemControlled;
    }
}
