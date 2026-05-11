using EgyptTax.Application.Ocr;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tesseract;

namespace EgyptTax.Infrastructure.Ocr;

/// <summary>
/// G3.2 — Tesseract 5 wrapper. Loads ara + eng language packs from
/// <see cref="TesseractOptions.TessdataPath"/>; if the folder or the
/// expected .traineddata files are missing, returns
/// <see cref="ReceiptOcrResult.Unavailable"/> so the rest of the app
/// keeps working and the UI can show a "download tessdata first"
/// hint instead of crashing.
///
/// Why not embedded resources for tessdata: the two language files
/// total ~10 MB. Bloating the install for a feature operators may
/// not use is a worse default than asking them to drop two files
/// next to the exe. The installer (T247 WiX) can opt-in to
/// bundling them later for a turnkey edition.
/// </summary>
public sealed class TesseractReceiptOcrService : IReceiptOcrService
{
    private readonly string _tessdataPath;
    private readonly ILogger<TesseractReceiptOcrService> _log;
    private readonly Lazy<TesseractEngine?> _engine;
    private readonly string? _unavailableReason;

    public TesseractReceiptOcrService(
        IOptions<TesseractOptions> options,
        IHostEnvironment env,
        ILogger<TesseractReceiptOcrService> log)
    {
        _log = log;

        // Resolve the tessdata path: prefer the configured option, fall
        // back to a "tessdata" folder next to the exe.
        var configured = options.Value.TessdataPath;
        _tessdataPath = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(env.ContentRootPath, "tessdata")
            : configured;

        _unavailableReason = ResolveUnavailableReason(_tessdataPath);

        // Lazy engine — defer native-library load until the first
        // request hits. If tessdata is missing we never construct it.
        _engine = new Lazy<TesseractEngine?>(() =>
        {
            if (_unavailableReason is not null) return null;
            try
            {
                return new TesseractEngine(_tessdataPath, "ara+eng", EngineMode.Default);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Tesseract engine failed to initialise from {Path}", _tessdataPath);
                return null;
            }
        });
    }

    public Task<ReceiptOcrResult> RecognizeAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        if (_unavailableReason is not null)
        {
            return Task.FromResult(ReceiptOcrResult.Unavailable(_unavailableReason));
        }
        if (imageBytes is null || imageBytes.Length == 0)
        {
            return Task.FromResult(ReceiptOcrResult.Unavailable("No image data received."));
        }

        // Run synchronously — Tesseract is CPU-bound and not thread-safe
        // on the same engine instance. Wrap with Task.Run so we don't
        // block the request thread.
        return Task.Run(() =>
        {
            try
            {
                var engine = _engine.Value;
                if (engine is null)
                {
                    return ReceiptOcrResult.Unavailable(
                        $"Tesseract failed to load. Check tessdata path: {_tessdataPath}");
                }

                using var img = Pix.LoadFromMemory(imageBytes);
                using var page = engine.Process(img);
                var text = page.GetText() ?? "";
                var draft = ReceiptOcrExtractor.Extract(text);
                return ReceiptOcrResult.Success(draft, text);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "OCR recognition failed");
                return ReceiptOcrResult.Unavailable($"OCR failed: {ex.Message}");
            }
        }, cancellationToken);
    }

    private static string? ResolveUnavailableReason(string path)
    {
        if (!Directory.Exists(path))
            return $"Tessdata folder not found at '{path}'. Download ara.traineddata + eng.traineddata from https://github.com/tesseract-ocr/tessdata_fast and drop them in that folder.";
        var required = new[] { "ara.traineddata", "eng.traineddata" };
        var missing = required.Where(f => !File.Exists(Path.Combine(path, f))).ToList();
        if (missing.Count > 0)
            return $"Missing language pack(s) in '{path}': {string.Join(", ", missing)}. Get them from https://github.com/tesseract-ocr/tessdata_fast.";
        return null;
    }
}

public sealed class TesseractOptions
{
    /// <summary>Absolute or content-root-relative path to the folder
    /// holding ara.traineddata + eng.traineddata. Defaults to
    /// <c>{contentRoot}/tessdata</c> when null/blank.</summary>
    public string? TessdataPath { get; set; }
}
