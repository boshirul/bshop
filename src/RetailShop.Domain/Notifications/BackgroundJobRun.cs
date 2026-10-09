using RetailShop.Domain.Common;

namespace RetailShop.Domain.Notifications;

public sealed class BackgroundJobRun : AuditableEntity
{
    private BackgroundJobRun()
    {
    }

    public BackgroundJobRun(string jobName, string runKey)
    {
        JobName = jobName.Trim();
        RunKey = runKey.Trim();
    }

    public string JobName { get; private set; } = string.Empty;

    public string RunKey { get; private set; } = string.Empty;

    public BackgroundJobRunStatus Status { get; private set; } =
        BackgroundJobRunStatus.Running;

    public DateTimeOffset StartedOn { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedOn { get; private set; }

    public int CreatedCount { get; private set; }

    public string? Error { get; private set; }

    public void Complete(int createdCount)
    {
        Status = BackgroundJobRunStatus.Completed;
        CreatedCount = createdCount;
        CompletedOn = DateTimeOffset.UtcNow;
    }

    public void Fail(Exception exception)
    {
        Status = BackgroundJobRunStatus.Failed;
        Error = exception.Message;
        CompletedOn = DateTimeOffset.UtcNow;
    }
}
