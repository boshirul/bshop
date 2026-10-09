namespace RetailShop.Domain.Returns;

public sealed class ComplaintReason
{
    private ComplaintReason()
    {
    }

    public ComplaintReason(string name, int displayOrder)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("A complaint reason is required.", nameof(name))
            : name.Trim();
        DisplayOrder = displayOrder;
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public string Name { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; } = true;
}
