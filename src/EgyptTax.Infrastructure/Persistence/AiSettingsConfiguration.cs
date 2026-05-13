using EgyptTax.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class AiSettingsConfiguration : IEntityTypeConfiguration<AiSettings>
{
    public void Configure(EntityTypeBuilder<AiSettings> b)
    {
        b.ToTable("ai_settings", schema: "settings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(x => x.Enabled).HasColumnName("enabled").IsRequired();
        b.Property(x => x.EncryptedApiKey).HasColumnName("encrypted_api_key").HasMaxLength(2000);
        b.Property(x => x.ModelName).HasColumnName("model_name")
            .HasMaxLength(100).IsUnicode(false).IsRequired();
        b.Property(x => x.MonthlyBudgetEgp).HasColumnName("monthly_budget_egp").IsRequired();
    }
}

internal sealed class ReceiptScanConfiguration : IEntityTypeConfiguration<ReceiptScan>
{
    public void Configure(EntityTypeBuilder<ReceiptScan> b)
    {
        b.ToTable("receipt_scans", schema: "settings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(x => x.ScannedAtUtc).HasColumnName("scanned_at_utc")
            .HasColumnType("datetime2(3)").IsRequired();
        b.Property(x => x.ScannedByUserId).HasColumnName("scanned_by_user_id");
        b.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(260).IsRequired();
        b.Property(x => x.MimeType).HasColumnName("mime_type")
            .HasMaxLength(100).IsUnicode(false).IsRequired();
        b.Property(x => x.FileSizeBytes).HasColumnName("file_size_bytes").IsRequired();

        // 4000 keeps SQL Server in nvarchar instead of nvarchar(max),
        // which SQLite rejects. SQLite ignores length constraints
        // anyway, so this is a no-op there. Claude responses for
        // receipt OCR are typically <500 chars; the ReceiptScan
        // ctor truncates anything longer with a marker.
        b.Property(x => x.RawResponseJson).HasColumnName("raw_response_json")
            .HasMaxLength(4000).IsRequired();

        b.Property(x => x.ExtractedVendor).HasColumnName("extracted_vendor").HasMaxLength(200);
        b.Property(x => x.ExtractedDate).HasColumnName("extracted_date").HasColumnType("date");
        b.Property(x => x.ExtractedTotalEgp).HasColumnName("extracted_total_egp")
            .HasColumnType("decimal(19,2)");
        b.Property(x => x.ExtractedVatEgp).HasColumnName("extracted_vat_egp")
            .HasColumnType("decimal(19,2)");
        b.Property(x => x.ExtractedCategory).HasColumnName("extracted_category").HasMaxLength(100);

        b.Property(x => x.InputTokens).HasColumnName("input_tokens").IsRequired();
        b.Property(x => x.OutputTokens).HasColumnName("output_tokens").IsRequired();
        b.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);

        b.HasIndex(x => x.ScannedAtUtc).HasDatabaseName("ix_receipt_scans_scanned_at_utc");
    }
}

internal sealed class AiChatLogConfiguration : IEntityTypeConfiguration<AiChatLog>
{
    public void Configure(EntityTypeBuilder<AiChatLog> b)
    {
        b.ToTable("ai_chat_logs", schema: "settings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(x => x.AskedAtUtc).HasColumnName("asked_at_utc")
            .HasColumnType("datetime2(3)").IsRequired();
        b.Property(x => x.AskedByUserId).HasColumnName("asked_by_user_id");
        b.Property(x => x.Question).HasColumnName("question").HasMaxLength(1000).IsRequired();
        b.Property(x => x.RawResponseJson).HasColumnName("raw_response_json")
            .HasMaxLength(4000).IsRequired();
        b.Property(x => x.Reply).HasColumnName("reply").HasMaxLength(2000);
        b.Property(x => x.OpenUrl).HasColumnName("open_url").HasMaxLength(500).IsUnicode(false);
        b.Property(x => x.OpenLabel).HasColumnName("open_label").HasMaxLength(200);
        b.Property(x => x.InputTokens).HasColumnName("input_tokens").IsRequired();
        b.Property(x => x.OutputTokens).HasColumnName("output_tokens").IsRequired();
        b.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);

        b.HasIndex(x => x.AskedAtUtc).HasDatabaseName("ix_ai_chat_logs_asked_at_utc");
    }
}
