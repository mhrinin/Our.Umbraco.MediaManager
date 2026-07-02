using Ideo.Umbraco.MediaManager.Models;
using Ideo.Umbraco.MediaManager.Services;

namespace Ideo.Umbraco.MediaManager.Tests;

public class MediaScanLogicTests
{
    private static ScanResult Result(ScanType type, long reclaimableBytes, params ScanItem[] items)
        => new(Guid.NewGuid(), type, items, reclaimableBytes);

    private static ScanItem Candidate(Guid key, long sizeBytes)
        => ScanItem.ForMedia(key, "file", "/media/file", sizeBytes);

    [Fact]
    public void ComputeReclaimableBytes_AllNull_IsZero()
    {
        Assert.Equal(0, MediaScanLogic.ComputeReclaimableBytes(null, null, null));
    }

    [Fact]
    public void ComputeReclaimableBytes_NoOverlap_SumsAllScans()
    {
        var unused = Result(ScanType.UnusedMedia, 100, Candidate(Guid.NewGuid(), 100));
        var orphaned = Result(ScanType.OrphanedFiles, 40);
        var duplicates = Result(ScanType.Duplicates, 25, Candidate(Guid.NewGuid(), 25));

        Assert.Equal(165, MediaScanLogic.ComputeReclaimableBytes(unused, orphaned, duplicates));
    }

    [Fact]
    public void ComputeReclaimableBytes_UnusedDuplicate_CountedOnce()
    {
        // The same media item is both unused and a duplicate copy: its 30 bytes appear in both
        // scans' totals and must be subtracted once.
        var sharedKey = Guid.NewGuid();
        var unused = Result(ScanType.UnusedMedia, 130, Candidate(sharedKey, 30), Candidate(Guid.NewGuid(), 100));
        var duplicates = Result(ScanType.Duplicates, 55, Candidate(sharedKey, 30), Candidate(Guid.NewGuid(), 25));

        Assert.Equal(155, MediaScanLogic.ComputeReclaimableBytes(unused, null, duplicates));
    }

    [Fact]
    public void IsUnusedMedia_FileNotReferenced_IsOrphan()
    {
        var referenced = new HashSet<int> { 200, 300 };

        Assert.True(MediaScanLogic.IsUnusedMedia(100, "/media/abc/file.jpg", trashed: false, referenced));
    }

    [Fact]
    public void IsUnusedMedia_FileReferenced_IsNotOrphan()
    {
        var referenced = new HashSet<int> { 100, 200 };

        Assert.False(MediaScanLogic.IsUnusedMedia(100, "/media/abc/file.jpg", trashed: false, referenced));
    }

    [Fact]
    public void IsUnusedMedia_Trashed_IsNotOrphan()
    {
        // Already in the recycle bin — must not be re-flagged.
        Assert.False(MediaScanLogic.IsUnusedMedia(100, "/media/abc/file.jpg", trashed: true, new HashSet<int>()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsUnusedMedia_Folder_IsNotOrphan(string? filePath)
    {
        Assert.False(MediaScanLogic.IsUnusedMedia(100, filePath, trashed: false, new HashSet<int>()));
    }

    [Fact]
    public void ExtractMediaKeys_UdiForm_ReturnsKey()
    {
        var key = Guid.NewGuid();
        var value = $"<img data-udi=\"umb://media/{key:N}\" />";

        Assert.Contains(key, MediaScanLogic.ExtractMediaKeys(value));
    }

    [Fact]
    public void ExtractMediaKeys_DashedGuid_ReturnsKey()
    {
        var key = Guid.NewGuid();
        var value = $"{{\"mediaKey\":\"{key}\"}}";

        Assert.Contains(key, MediaScanLogic.ExtractMediaKeys(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Just some text with no references.")]
    public void ExtractMediaKeys_NoReference_ReturnsEmpty(string? value)
    {
        Assert.Empty(MediaScanLogic.ExtractMediaKeys(value));
    }

    [Theory]
    [InlineData("1071/kitten.jpg", "1071/kitten.jpg")]
    [InlineData("/1071/kitten.jpg", "1071/kitten.jpg")]
    [InlineData("deep\\nested\\file.bin", "deep/nested/file.bin")]
    [InlineData("\\lead\\file.png", "lead/file.png")]
    public void ToZipEntryName_NormalizesToRelativeForwardSlashes(string input, string expected)
    {
        Assert.Equal(expected, MediaScanLogic.ToZipEntryName(input));
    }

    [Theory]
    [InlineData("cache", true)]
    [InlineData("1071/cache", true)]
    [InlineData("Cache/", true)]
    [InlineData("1071", false)]
    [InlineData("1071/images", false)]
    public void IsCacheDirectory_DetectsCacheFolder(string directory, bool expected)
    {
        Assert.Equal(expected, MediaScanLogic.IsCacheDirectory(directory));
    }
}
