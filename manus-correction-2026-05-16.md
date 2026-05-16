# Correction to Manus Full Test Report (2026-05-16)

**One PASS row had a stale "Deferred" label.** Reposting the note for your records — the test result itself was correct, only the limitations table needs updating.

## What you wrote

In the "Known Limitations" section of the test report:

> | F.6 orphan cleanup | Deferred | PI/SI edit-path delete-then-insert leaves orphaned allocations. Reports ignore them. Low priority |

## What actually shipped

**F.6 orphan cleanup is NOT deferred — it shipped this session in commit `ade0caa`** (`v5 UI session 2`). Two changes, both verifiable in the diff:

### 1. SI edit-path orphan delete

`src/EgyptTax.Web/Pages/Invoices/SalesInvoiceEdit.razor` — when a line is removed from the draft, its `CostCenterAllocation` rows are deleted too:

```csharp
foreach (var lineId in existingIds.Where(id => !newIds.Contains(id)).ToList())
{
    _invoice.RemoveLine(lineId);
    // v5 F.6 v2 phase 2 — drop orphaned cost-center
    // allocations for the deleted line so the report
    // doesn't carry dead rows in master.cost_center_allocations.
    var orphans = await Db.Set<CostCenterAllocation>()
        .Where(a => a.SourceType == CostCenterAllocationSourceType.SalesInvoiceLine
            && a.SourceLineId == lineId)
        .ToListAsync();
    Db.Set<CostCenterAllocation>().RemoveRange(orphans);
}
```

### 2. PI edit-path refactor (root cause fix)

`src/EgyptTax.Web/Pages/Purchases/PurchaseInvoiceEdit.razor` — the edit-save was previously **delete-all + reinsert-all**, which orphaned every allocation on every save. Now it's diff-based:

```csharp
// Edit existing draft. v5 F.6 v2 phase 2 — diff-based update
// (mirror of SalesInvoiceEdit) so existing line ids survive
// edits, which keeps any CostCenterAllocation rows linked.
// Previously this path did delete-then-reinsert, which gave
// every line a fresh id and orphaned the operator's split work.
var existingIds = live.Lines.Select(l => l.Id).ToHashSet();
var keptIds = _form.Lines
    .Where(l => l.SavedLineId != Guid.Empty)
    .Select(l => l.SavedLineId).ToHashSet();
foreach (var droppedId in existingIds.Where(id => !keptIds.Contains(id)).ToList())
{
    live.RemoveLine(droppedId);
    var orphans = await Db.Set<CostCenterAllocation>()
        .Where(a => a.SourceType == CostCenterAllocationSourceType.PurchaseInvoiceLine
            && a.SourceLineId == droppedId)
        .ToListAsync();
    Db.Set<CostCenterAllocation>().RemoveRange(orphans);
}
foreach (var l in _form.Lines.Where(l => l.SavedLineId == Guid.Empty))
{
    live.AddLine(l.ItemId, l.ExpenseCategoryId, l.Quantity,
        MoneyEgp.From(l.UnitPrice), l.VatCategoryId, l.VatRatePercent, l.DeductibleFlag,
        costCenterId: l.CostCenterId == Guid.Empty ? null : l.CostCenterId);
}
```

`FormLine.SavedLineId` was added to track the underlying `PurchaseInvoiceLine.Id` across saves so the split editor's `CostCenterAllocation` rows don't get stranded.

## Updated limitations table

The Known Limitations table should read:

| Item | Status | Reason |
|------|--------|--------|
| F.2/F.3 auto mode | Deferred | Needs multi-currency foundation (per-document currency override) |
| ~~F.6 orphan cleanup~~ | **Shipped** in `ade0caa` | SI orphan-delete + PI diff-based edit-save |
| D.2.3 drag-to-reschedule | Deferred | Nice-to-have on Gantt |
| E.12 Manufacturing/BOM | Deferred | Not target market |
| Phase C | Deferred | Operator-pull |
| Sprint 6 visual QA | Pending | Needs operator page-by-page feedback |

## Pattern note

This is the third Manus pass where a "Deferred" claim was actually shipped (prior precedents in the 2026-05-15 reports: Fixed Asset Management marked "Not present" while `Domain/Documents/FixedAsset.cs` was fully shipped; Grouped Payment / Pricelists / Partner Ledger similarly mis-classified).

Recommendation when generating the limitations section: cross-check each entry against the most recent `git log --oneline | grep "v5"` rather than carrying forward notes from prior reports.

The rest of the 54/54 test result stands and matches what's in the working tree.
