using System.Text;
using OmniOps.Infrastructure.Services;

namespace OmniOps.Infrastructure.Tests.Services;

public sealed class LocalFileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "omniops-files-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveThenOpen_RoundTripsContent()
    {
        var store = new LocalFileStore(_root);
        await store.SaveAsync("org/job/photo.jpg", new MemoryStream(Encoding.UTF8.GetBytes("pixels")));

        await using var read = await store.OpenReadAsync("org/job/photo.jpg");
        using var reader = new StreamReader(read!);
        Assert.Equal("pixels", await reader.ReadToEndAsync());
        Assert.False(File.Exists(Path.Combine(_root, "org/job/photo.jpg.partial")));
    }

    [Fact]
    public async Task OpenRead_MissingFile_ReturnsNull()
    {
        Assert.Null(await new LocalFileStore(_root).OpenReadAsync("org/job/none.jpg"));
    }

    [Theory]
    [InlineData("../outside.jpg")]
    [InlineData("org/../../outside.jpg")]
    [InlineData("/etc/passwd.txt")]
    [InlineData("org\\job\\photo.jpg")]
    [InlineData("photo")]
    public async Task Keys_ThatCouldEscapeTheRoot_AreRejected(string key)
    {
        var store = new LocalFileStore(_root);
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(key, new MemoryStream([1])));
    }
}
