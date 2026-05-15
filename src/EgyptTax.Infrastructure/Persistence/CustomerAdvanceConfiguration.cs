using EgyptTax.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class CustomerAdvanceConfiguration : IEntityTypeConfiguration<CustomerAdvance>
{
    public void Configure(EntityTypeBuilder<CustomerAdvance> b)
    {
        b.ToTable("customer_advances", schema: "master_data");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(a => a.CustomerId).HasColumnName("customer_id").IsRequired();
        b.Property(a => a.CashAccountId).HasColumnName("cash_account_id").IsRequired();
        b.ComplexProperty(a => a.Amount,
            p => p.Property(x => x.Amount).HasColumnName("amount")
                .HasColumnType("decimal(19,2)").IsRequired());
        b.Property(a => a.ReceivedDate).HasColumnName("received_date")
            .HasColumnType("date").IsRequired();
        b.Property(a => a.QuotationId).HasColumnName("quotation_id");
        b.Property(a => a.Notes).HasColumnName("notes").HasMaxLength(2000);
        b.Property(a => a.Status).HasColumnName("status")
            .HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(a => a.AppliedToInvoiceId).HasColumnName("applied_to_invoice_id");
        b.Property(a => a.AppliedAtUtc).HasColumnName("applied_at_utc")
            .HasColumnType("datetime2(3)");
        b.Property(a => a.CreatedAtUtc).HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)").IsRequired();
        b.Property(a => a.CreatedByUserId).HasColumnName("created_by_user_id");

        b.HasIndex(a => a.CustomerId).HasDatabaseName("ix_customer_advances_customer_id");
        b.HasIndex(a => a.Status).HasDatabaseName("ix_customer_advances_status");
    }
}
