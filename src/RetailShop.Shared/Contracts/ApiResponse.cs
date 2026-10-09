namespace RetailShop.Shared.Contracts;

public sealed record ApiResponse<T>(
    bool Succeeded,
    T? Data,
    string? Message,
    IReadOnlyCollection<string> Errors)
{
    private static readonly IReadOnlyCollection<string> NoErrors = Array.Empty<string>();

    public static ApiResponse<T> Success(T data, string? message = null) =>
        new(true, data, message, NoErrors);

    public static ApiResponse<T> Failure(
        IEnumerable<string> errors,
        string? message = null) =>
        new(false, default, message, errors.ToArray());
}
