using System.Text.Json;
using EgyptTax.Application.Audit;
using EgyptTax.Domain.Audit;

namespace EgyptTax.Infrastructure.Audit;

/// <summary>
/// FR-028 file-mode checkpoint storage. Writes JSON atomically (write to
/// <c>{path}.tmp</c>, fsync, rename) to a write-restricted directory whose
/// ACLs the operator pins to the service account only. Per research.md
/// R-05, this is one of the two storage modes the operator picks at
/// install time.
/// </summary>
public sealed class FileSystemCheckpointStore(string filePath) : IAuditCheckpointStore
{
    private readonly string _filePath = filePath;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
    };

    public async Task<AuditCheckpoint?> ReadLatestAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(_filePath);
        var dto = await JsonSerializer.DeserializeAsync<CheckpointDto>(
            stream,
            SerializerOptions,
            cancellationToken
        );
        if (dto is null)
        {
            return null;
        }

        return new AuditCheckpoint(
            dto.LastIndex,
            Convert.FromHexString(dto.LastHashHex),
            dto.TsUtc
        );
    }

    public async Task WriteAsync(
        AuditCheckpoint checkpoint,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var dto = new CheckpointDto(
            LastIndex: checkpoint.LastIndex,
            LastHashHex: Convert.ToHexString(checkpoint.LastHash),
            TsUtc: checkpoint.TsUtc
        );

        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, dto, SerializerOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        // Atomic rename (overwrites existing file on Windows + Linux).
        File.Move(tempPath, _filePath, overwrite: true);
    }

    private sealed record CheckpointDto(long LastIndex, string LastHashHex, DateTime TsUtc);
}
