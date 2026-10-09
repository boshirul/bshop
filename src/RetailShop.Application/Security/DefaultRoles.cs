namespace RetailShop.Application.Security;

public static class DefaultRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Salesperson = "Salesperson";

    public static IReadOnlyCollection<string> All { get; } =
        [Admin, Manager, Salesperson];
}
