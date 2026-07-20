namespace StickerPicker.Core.Tests;

public sealed class LibraryImporterTests
{
    [Fact]
    public async Task ImportAsync_RemovesStaleTemporaryFiles()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        fixture.Library.CreateCategory("clean");
        var categoryDirectory = Path.Combine(fixture.Paths.LibraryRoot, "clean");
        var temporaryFile = Path.Combine(categoryDirectory, ".stickerpicker-abandoned.tmp");
        File.WriteAllBytes(temporaryFile, new byte[1024]);
        File.SetLastWriteTimeUtc(temporaryFile, DateTime.UtcNow.AddDays(-2));

        await fixture.Library.ImportAsync([fixture.WritePng("valid.png")], "clean");

        Assert.False(File.Exists(temporaryFile));
        Assert.Single(fixture.Library.Stickers);
    }

    [Fact]
    public async Task ImportAsync_CancellationPropagatesWithoutTemporaryFiles()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        fixture.Library.CreateCategory("cancelled");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Library.ImportAsync(
            [fixture.WritePng("cancelled.png")],
            "cancelled",
            cancellation.Token));

        var categoryDirectory = Path.Combine(fixture.Paths.LibraryRoot, "cancelled");
        Assert.Empty(Directory.EnumerateFiles(categoryDirectory, ".stickerpicker-*.tmp"));
        Assert.Empty(fixture.Library.Stickers);
    }
}
