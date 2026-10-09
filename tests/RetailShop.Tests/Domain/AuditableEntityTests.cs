using RetailShop.Domain.Common;

namespace RetailShop.Tests.Domain;

public sealed class AuditableEntityTests
{
    [Fact]
    public void NewEntity_HasVersionSevenIdentifierAndUtcCreationTime()
    {
        var before = DateTimeOffset.UtcNow;

        var entity = new TestEntity();

        var after = DateTimeOffset.UtcNow;
        Assert.Equal(7, entity.Id.Version);
        Assert.InRange(entity.CreatedOn, before, after);
        Assert.False(entity.IsDeleted);
    }

    private sealed class TestEntity : AuditableEntity;
}
