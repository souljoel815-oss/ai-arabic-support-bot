using EgyptTax.Domain.Invoices;

namespace EgyptTax.UnitTests.Domain.Invoices;

/// <summary>
/// G2.2 — covers the WhatsApp dispatch lifecycle (Sent → Delivered
/// → Read; Failed terminal; idempotency on the webhook-driven
/// transitions).
/// </summary>
public class InvoiceWhatsAppDispatchTests
{
    private static readonly Guid Invoice = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    private static readonly DateTime T0 = new(2026, 5, 11, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_DefaultsToSent_AndStoresFields()
    {
        var d = New(providerId: "wamid.HBgL12345");

        d.DeliveryStatus.Should().Be(WhatsAppDispatchStatus.Sent);
        d.SalesInvoiceId.Should().Be(Invoice);
        d.RecipientPhone.Should().Be("+201001234567");
        d.MessageBody.Should().Be("Hello — invoice attached.");
        d.SentAtUtc.Should().Be(T0);
        d.SentByUserId.Should().Be(User);
        d.ProviderMessageId.Should().Be("wamid.HBgL12345");
    }

    [Fact]
    public void Constructor_BlankProviderId_NormalisesToNull()
    {
        var d = New(providerId: "   ");
        d.ProviderMessageId.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsBlankPhone(string? bad)
    {
        var act = () => new InvoiceWhatsAppDispatch(
            salesInvoiceId: Invoice,
            recipientPhone: bad!,
            messageBody: "x",
            sentAtUtc: T0,
            sentByUserId: User);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkDelivered_FlipsStatus_AndStoresTimestamp()
    {
        var d = New();
        d.MarkDelivered(T0.AddMinutes(2));

        d.DeliveryStatus.Should().Be(WhatsAppDispatchStatus.Delivered);
        d.DeliveredAtUtc.Should().Be(T0.AddMinutes(2));
    }

    [Fact]
    public void MarkDelivered_Idempotent_PreservesOriginalTimestamp()
    {
        var d = New();
        d.MarkDelivered(T0.AddMinutes(2));
        d.MarkDelivered(T0.AddMinutes(5));
        d.DeliveredAtUtc.Should().Be(T0.AddMinutes(2));
    }

    [Fact]
    public void MarkRead_AfterDelivered_FlipsToRead()
    {
        var d = New();
        d.MarkDelivered(T0.AddMinutes(2));
        d.MarkRead(T0.AddHours(1));

        d.DeliveryStatus.Should().Be(WhatsAppDispatchStatus.Read);
        d.ReadAtUtc.Should().Be(T0.AddHours(1));
        d.DeliveredAtUtc.Should().Be(T0.AddMinutes(2)); // preserved
    }

    [Fact]
    public void MarkRead_DirectlyFromSent_AlsoFlips()
    {
        var d = New();
        d.MarkRead(T0.AddHours(1));
        d.DeliveryStatus.Should().Be(WhatsAppDispatchStatus.Read);
    }

    [Fact]
    public void MarkFailed_FlipsStatus_AndStoresReason()
    {
        var d = New();
        d.MarkFailed(T0.AddSeconds(10), "Invalid phone number per Meta API.");

        d.DeliveryStatus.Should().Be(WhatsAppDispatchStatus.Failed);
        d.FailedAtUtc.Should().Be(T0.AddSeconds(10));
        d.FailureReason.Should().Be("Invalid phone number per Meta API.");
    }

    [Fact]
    public void MarkFailed_AfterRead_DoesNotOverwrite()
    {
        // Webhook race: Read came first, then a stale Failed event.
        // The customer demonstrably read the message — Failed is bogus.
        var d = New();
        d.MarkRead(T0.AddHours(1));
        d.MarkFailed(T0.AddHours(2), "stale failure event");

        d.DeliveryStatus.Should().Be(WhatsAppDispatchStatus.Read);
    }

    [Fact]
    public void MarkDelivered_AfterFailed_DoesNotOverwrite()
    {
        var d = New();
        d.MarkFailed(T0, "Invalid phone");
        d.MarkDelivered(T0.AddMinutes(1));
        d.DeliveryStatus.Should().Be(WhatsAppDispatchStatus.Failed);
    }

    private static InvoiceWhatsAppDispatch New(string? providerId = "wamid.HBgL12345") =>
        new(
            salesInvoiceId: Invoice,
            recipientPhone: "+201001234567",
            messageBody: "Hello — invoice attached.",
            sentAtUtc: T0,
            sentByUserId: User,
            providerMessageId: providerId);
}
