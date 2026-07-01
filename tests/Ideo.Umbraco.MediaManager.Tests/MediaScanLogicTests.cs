using Ideo.Umbraco.MediaManager.Services;

namespace Ideo.Umbraco.MediaManager.Tests;

public class MediaScanLogicTests
{
    [Fact]
    public void IsOrphanMedia_FileNotReferenced_IsOrphan()
    {
        var referenced = new HashSet<int> { 200, 300 };

        Assert.True(MediaScanLogic.IsOrphanMedia(100, "/media/abc/file.jpg", referenced));
    }

    [Fact]
    public void IsOrphanMedia_FileReferenced_IsNotOrphan()
    {
        var referenced = new HashSet<int> { 100, 200 };

        Assert.False(MediaScanLogic.IsOrphanMedia(100, "/media/abc/file.jpg", referenced));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsOrphanMedia_Folder_IsNotOrphan(string? filePath)
    {
        Assert.False(MediaScanLogic.IsOrphanMedia(100, filePath, new HashSet<int>()));
    }

    [Theory]
    [InlineData("/media/1071/Kitten.JPG", "1071/kitten.jpg")]
    [InlineData("1071\\file.PNG", "1071/file.png")]
    [InlineData("media/abc/x.gif", "abc/x.gif")]
    [InlineData("/abc/y.webp", "abc/y.webp")]
    public void NormalizeMediaPath_NormalizesConsistently(string input, string expected)
    {
        Assert.Equal(expected, MediaScanLogic.NormalizeMediaPath(input));
    }

    [Fact]
    public void NormalizeMediaPath_OwnedAndFilesystemPaths_Match()
    {
        // A stored media path ("/media/1071/file.jpg") and the filesystem-relative path
        // ("1071/file.jpg") must normalize to the same key so owned files are excluded.
        Assert.Equal(
            MediaScanLogic.NormalizeMediaPath("/media/1071/file.jpg"),
            MediaScanLogic.NormalizeMediaPath("1071/file.jpg"));
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
