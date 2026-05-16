namespace EgyptTax.Domain.MasterData;

/// <summary>
/// v5 D.1 — one tracked unit of an item that the operator opted
/// into via <see cref="Item.TracksSerials"/>. Pharmacies dispensing
/// meds + electronics retailers selling warrantied units need
/// per-unit identity; this entity carries the serial-string +
/// status + last-known customer-back-pointer for traceability.
///
/// Uniqueness is per-item: the same serial string can appear on
/// two different SKUs (think identical-looking units from different
/// product lines). The composite ux is <c>(item_id, serial_number)</c>.
///
/// v1 lifecycle: operator creates <c>InStock</c> rows on the
/// management page, flips status manually as units sell or get
/// returned. Sales-invoice auto-binding (operator pastes serials
/// on the line, post atomically transitions InStock → Sold +
/// records customer/invoice) is v2.
/// </summary>
public sealed class ItemSerial
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ItemId { get; init; }
    public string SerialNumber { get; init; } = "";
    public Guid? LotId { get; private set; }
    public ItemSerialStatus Status { get; private set; } = ItemSerialStatus.InStock;
    public Guid? CurrentLocationId { get; private set; }
    public Guid? CurrentCustomerId { get; private set; }
    public Guid? SoldOnSalesInvoiceId { get; private set; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? LastStatusChangeAtUtc { get; private set; }

    private ItemSerial() { }

    public ItemSerial(
        Guid itemId,
        string serialNumber,
        Guid? lotId = null,
        Guid? currentLocationId = null)
    {
        if (itemId == Guid.Empty) throw new ArgumentException("ItemId is required.", nameof(itemId));
        ArgumentException.ThrowIfNullOrWhiteSpace(serialNumber);
        ItemId = itemId;
        SerialNumber = serialNumber.Trim();
        LotId = lotId == Guid.Empty ? null : lotId;
        CurrentLocationId = currentLocationId == Guid.Empty ? null : currentLocationId;
        Status = ItemSerialStatus.InStock;
    }

    public void MarkSold(Guid customerId, Guid? salesInvoiceId, DateTime nowUtc)
    {
        if (Status != ItemSerialStatus.InStock && Status != ItemSerialStatus.Reserved)
        {
            throw new InvalidOperationException(
                $"Cannot mark serial {SerialNumber} as Sold from state {Status}.");
        }
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId is required for sold serial.", nameof(customerId));
        }
        Status = ItemSerialStatus.Sold;
        CurrentCustomerId = customerId;
        SoldOnSalesInvoiceId = salesInvoiceId;
        LastStatusChangeAtUtc = nowUtc;
    }

    public void MarkReturned(DateTime nowUtc)
    {
        if (Status != ItemSerialStatus.Sold)
        {
            throw new InvalidOperationException(
                $"Cannot return serial {SerialNumber} from state {Status} — only Sold serials can be Returned.");
        }
        Status = ItemSerialStatus.Returned;
        // Keep customer + invoice for traceability; they're still
        // the last party that owned the unit.
        LastStatusChangeAtUtc = nowUtc;
    }

    public void RestoreToStock(DateTime nowUtc)
    {
        if (Status == ItemSerialStatus.InStock) return;
        Status = ItemSerialStatus.InStock;
        CurrentCustomerId = null;
        SoldOnSalesInvoiceId = null;
        LastStatusChangeAtUtc = nowUtc;
    }

    public void Reserve(DateTime nowUtc)
    {
        if (Status != ItemSerialStatus.InStock)
        {
            throw new InvalidOperationException(
                $"Cannot reserve serial {SerialNumber} from state {Status} — only InStock serials can be Reserved.");
        }
        Status = ItemSerialStatus.Reserved;
        LastStatusChangeAtUtc = nowUtc;
    }

    public void MarkDamaged(DateTime nowUtc)
    {
        Status = ItemSerialStatus.Damaged;
        LastStatusChangeAtUtc = nowUtc;
    }

    public void WriteOff(DateTime nowUtc)
    {
        Status = ItemSerialStatus.WrittenOff;
        LastStatusChangeAtUtc = nowUtc;
    }
}

public enum ItemSerialStatus
{
    InStock,
    Reserved,
    Sold,
    Returned,
    Damaged,
    WrittenOff,
}
