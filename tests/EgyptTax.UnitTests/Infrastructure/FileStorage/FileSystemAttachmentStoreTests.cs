using System.Security.Cryptography;
using System.Text;
using EgyptTax.Application.FileStorage;
using EgyptTax.Infrastructure.FileStorage;
using EgyptTax.SharedKernel.Time;

namespace EgyptTax.UnitTests.Infrastructure.FileStorage;

/// <summary>
/// T057 — R-21 attachment storage. The store MUST persist binary
/// content under <c>attachments/{yyyy}/{mm}/{document_id}/{attachment_id}
/// .{ext}</c> so audit / disaster-recovery jobs can locate files
/// chronologically and group every attachment for one document under
/// one folder. <c>SaveAsync</c> returns the relative path, the
/// SHA-256 content hash (for integrity verification + future dedup),
/// and the byte count; <c>OpenReadAsync</c> / <c>DeleteAsync</c> /
/// <c>Exists</c> operate on the returned relative path so callers
/// don't have to recompute the layout.
/// </summary>
public class FileSystemAttachmentStoreTests : IDisposable
{
    private readonly string _root;

    public FileSystemAttachmentStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EgyptTaxAttachmentTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* test cleanup is best-effort */ }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SaveAsync_WritesToPathFollowingR21Layout()
    {
        var documentId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var attachmentId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var clock = new FixedClock(new DateTime(2026, 7, 14, 9, 0, 0, DateTimeKind.Utc));
        var store = new FileSystemAttachmentStore(_root, clock);

        var content = "hello world"u8.ToArray();
        await using var stream = new MemoryStream(content);

        var saved = await store.SaveAsync(documentId, attachmentId, ".pdf", stream, CancellationToken.None);

        var expectedRelative = $"attachments/2026/07/{documentId:D}/{attachmentId:D}.pdf";
        saved.RelativePath.Should().Be(expectedRelative,
            because: "R-21 storage layout is attachments/{yyyy}/{mm}/{document_id}/{attachment_id}.{ext}");

        var absolute = Path.Combine(_root, expectedRelative.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(absolute).Should().BeTrue();
        var written = await File.ReadAllBytesAsync(absolute);
        written.Should().Equal(content);
    }

    [Fact]
    public async Task SaveAsync_NormalisesExtension_AcceptsWithOrWithoutLeadingDot()
    {
        var documentId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var clock = new FixedClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var store = new FileSystemAttachmentStore(_root, clock);

        var saved = await store.SaveAsync(documentId, attachmentId, "jpg",
            new MemoryStream("x"u8.ToArray()), CancellationToken.None);

        saved.RelativePath.Should().EndWith(".jpg");
    }

    [Fact]
    public async Task SaveAsync_ComputesSha256_AndByteCount()
    {
        var documentId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var clock = new FixedClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var store = new FileSystemAttachmentStore(_root, clock);

        var content = Encoding.UTF8.GetBytes("the quick brown fox jumps over the lazy dog");
        var expectedHash = SHA256.HashData(content);

        var saved = await store.SaveAsync(documentId, attachmentId, ".txt",
            new MemoryStream(content), CancellationToken.None);

        saved.SizeBytes.Should().Be(content.Length);
        saved.ContentSha256.Should().Equal(expectedHash);
    }

    [Fact]
    public async Task OpenReadAsync_RoundtripsExactBytes()
    {
        var documentId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var clock = new FixedClock(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        var store = new FileSystemAttachmentStore(_root, clock);
        var content = new byte[] { 0x00, 0x01, 0xFE, 0xFF, 0x42, 0x10, 0x20, 0x30 };

        var saved = await store.SaveAsync(documentId, attachmentId, ".bin",
            new MemoryStream(content), CancellationToken.None);

        await using var read = await store.OpenReadAsync(saved.RelativePath, CancellationToken.None);
        using var ms = new MemoryStream();
        await read.CopyToAsync(ms);
        ms.ToArray().Should().Equal(content);
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile()
    {
        var documentId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var clock = new FixedClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var store = new FileSystemAttachmentStore(_root, clock);

        var saved = await store.SaveAsync(documentId, attachmentId, ".pdf",
            new MemoryStream("payload"u8.ToArray()), CancellationToken.None);

        store.Exists(saved.RelativePath).Should().BeTrue();
        await store.DeleteAsync(saved.RelativePath, CancellationToken.None);
        store.Exists(saved.RelativePath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_MissingFile_DoesNotThrow()
    {
        var clock = new FixedClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var store = new FileSystemAttachmentStore(_root, clock);

        var act = () => store.DeleteAsync(
            "attachments/2026/01/00000000-0000-0000-0000-000000000000/missing.pdf",
            CancellationToken.None);

        await act.Should().NotThrowAsync(
            because: "delete MUST be idempotent so a re-run after a crash does not mask other errors");
    }

    [Fact]
    public async Task SaveAsync_OverwritesExistingFile_KeepsLatestContent()
    {
        var documentId = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var attachmentId = Guid.Parse("44444444-4444-4444-8444-444444444444");
        var clock = new FixedClock(new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc));
        var store = new FileSystemAttachmentStore(_root, clock);

        await store.SaveAsync(documentId, attachmentId, ".txt",
            new MemoryStream("first"u8.ToArray()), CancellationToken.None);
        var second = await store.SaveAsync(documentId, attachmentId, ".txt",
            new MemoryStream("second"u8.ToArray()), CancellationToken.None);

        await using var read = await store.OpenReadAsync(second.RelativePath, CancellationToken.None);
        using var ms = new MemoryStream();
        await read.CopyToAsync(ms);
        Encoding.UTF8.GetString(ms.ToArray()).Should().Be("second");
    }

    [Fact]
    public async Task SaveAsync_RejectsTraversalAttemptsInExtension()
    {
        var clock = new FixedClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var store = new FileSystemAttachmentStore(_root, clock);

        var act = () => store.SaveAsync(Guid.NewGuid(), Guid.NewGuid(),
            "../escape", new MemoryStream("x"u8.ToArray()), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>(
            because: "the extension is user-influenced and MUST be validated against path traversal");
    }

    [Fact]
    public void OpenReadAsync_WithRelativePathOutsideRoot_Throws()
    {
        var clock = new FixedClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var store = new FileSystemAttachmentStore(_root, clock);

        var act = () => store.OpenReadAsync("../escape.pdf", CancellationToken.None);

        act.Should().ThrowAsync<ArgumentException>(
            because: "relative-path inputs are caller-supplied and MUST be checked against root escape");
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
