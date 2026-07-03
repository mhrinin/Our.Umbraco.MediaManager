using Ideo.Umbraco.MediaManager.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core.Hosting;

namespace Ideo.Umbraco.MediaManager.Tests;

public sealed class ExportStoreTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), $"mm-export-tests-{Guid.NewGuid():N}");
    private readonly ExportStore store;

    public ExportStoreTests()
    {
        var hosting = new Mock<IHostingEnvironment>();
        hosting.SetupGet(h => h.LocalTempPath).Returns(tempRoot);
        store = new ExportStore(hosting.Object, NullLogger<ExportStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private string CompleteExport(Guid jobId, out string token)
    {
        var zipPath = store.CreateExportFile(jobId);
        File.WriteAllBytes(zipPath, [1, 2, 3]);
        token = store.Complete(jobId, zipPath);
        return zipPath;
    }

    [Fact]
    public void Resolve_WithIssuedToken_ReturnsFile()
    {
        var jobId = Guid.NewGuid();
        var zipPath = CompleteExport(jobId, out var token);

        var resolved = store.Resolve(jobId, token);

        Assert.NotNull(resolved);
        Assert.Equal(zipPath, resolved.ZipPath);
    }

    [Theory]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("not-hex-at-all")]
    [InlineData("")]
    public void Resolve_WithInvalidToken_ReturnsNull(string badToken)
    {
        var jobId = Guid.NewGuid();
        CompleteExport(jobId, out _);

        Assert.Null(store.Resolve(jobId, badToken));
    }

    [Fact]
    public void Resolve_WithWrongJobId_ReturnsNull()
    {
        CompleteExport(Guid.NewGuid(), out var token);

        Assert.Null(store.Resolve(Guid.NewGuid(), token));
    }

    [Fact]
    public void CreateExportFile_InvalidatesPreviousToken_AndWipesFolder()
    {
        var firstJob = Guid.NewGuid();
        var firstZip = CompleteExport(firstJob, out var firstToken);

        store.CreateExportFile(Guid.NewGuid());

        Assert.Null(store.Resolve(firstJob, firstToken));
        Assert.False(File.Exists(firstZip));
    }

    [Fact]
    public void Resolve_WhenFileDeleted_ReturnsNull()
    {
        var jobId = Guid.NewGuid();
        var zipPath = CompleteExport(jobId, out var token);
        File.Delete(zipPath);

        Assert.Null(store.Resolve(jobId, token));
    }
}
