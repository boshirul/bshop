namespace RetailShop.Application.Common;

public sealed record OperationResult<T>(
    bool Succeeded,
    T? Value,
    IReadOnlyCollection<string> Errors)
{
    private static readonly IReadOnlyCollection<string> NoErrors = Array.Empty<string>();

    public static OperationResult<T> Success(T value) =>
        new(true, value, NoErrors);

    public static OperationResult<T> Failure(params string[] errors) =>
        new(false, default, errors);

    public static OperationResult<T> Failure(IEnumerable<string> errors) =>
        new(false, default, errors.ToArray());
}
