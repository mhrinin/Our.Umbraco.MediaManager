using Ideo.Umbraco.MediaManager.Services;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;

namespace Ideo.Umbraco.MediaManager.Tests;

public class CleanupServiceTests
{
    private static (CleanupService service, Mock<IMediaEditingService> editing, Mock<IAuditService> audit) CreateService()
    {
        var editing = new Mock<IMediaEditingService>();
        var audit = new Mock<IAuditService>();
        audit
            .Setup(a => a.AddAsync(It.IsAny<AuditType>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Attempt.Succeed(AuditLogOperationStatus.Success));

        var security = new Mock<IBackOfficeSecurityAccessor>();
        security.SetupGet(s => s.BackOfficeSecurity).Returns((IBackOfficeSecurity?)null);

        // MediaFileManager is not used by DeleteMediaAsync, so it is safe to pass null here.
        var service = new CleanupService(editing.Object, null!, audit.Object, security.Object);
        return (service, editing, audit);
    }

    private static IMedia MediaWith(int id)
    {
        var media = new Mock<IMedia>();
        media.SetupGet(m => m.Id).Returns(id);
        media.SetupGet(m => m.Name).Returns($"media-{id}");
        return media.Object;
    }

    [Fact]
    public async Task DeleteMedia_DryRun_MutatesNothing()
    {
        var (service, editing, audit) = CreateService();

        var result = await service.DeleteMediaAsync([Guid.NewGuid(), Guid.NewGuid()], dryRun: true);

        Assert.Equal(2, result.Affected);
        Assert.Empty(result.Errors);
        editing.Verify(e => e.MoveToRecycleBinAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        audit.Verify(a => a.AddAsync(It.IsAny<AuditType>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteMedia_Execute_MovesToRecycleBinAndAudits()
    {
        var key = Guid.NewGuid();
        var (service, editing, audit) = CreateService();
        editing
            .Setup(e => e.MoveToRecycleBinAsync(key, It.IsAny<Guid>()))
            .ReturnsAsync(Attempt.SucceedWithStatus(ContentEditingOperationStatus.Success, MediaWith(7)));

        var result = await service.DeleteMediaAsync([key], dryRun: false);

        Assert.Equal(1, result.Affected);
        Assert.Empty(result.Errors);
        editing.Verify(e => e.MoveToRecycleBinAsync(key, It.IsAny<Guid>()), Times.Once);
        audit.Verify(a => a.AddAsync(AuditType.Delete, It.IsAny<Guid>(), 7, "media", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task DeleteMedia_Failure_RecordsErrorAndDoesNotAudit()
    {
        var key = Guid.NewGuid();
        var (service, editing, audit) = CreateService();
        editing
            .Setup(e => e.MoveToRecycleBinAsync(key, It.IsAny<Guid>()))
            .ReturnsAsync(Attempt.FailWithStatus<IMedia, ContentEditingOperationStatus>(ContentEditingOperationStatus.NotFound, null!));

        var result = await service.DeleteMediaAsync([key], dryRun: false);

        Assert.Equal(0, result.Affected);
        Assert.Single(result.Errors);
        audit.Verify(a => a.AddAsync(It.IsAny<AuditType>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
