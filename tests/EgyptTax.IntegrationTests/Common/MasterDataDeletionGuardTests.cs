using EgyptTax.Application.Common.Guards;
using EgyptTax.Domain.Expenses;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Infrastructure.Common.Guards;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Common;

/// <summary>
/// T140 / FR-007 — guard against orphaning posted-document
/// snapshots by hard-deleting the underlying master-data row. The
/// guard is the application-layer enforcement point; the MVP UI
/// only exposes deactivate/reactivate, but future admin tooling +
/// migration scripts call into this BEFORE attempting any
/// destructive operation.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class MasterDataDeletionGuardTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task Supplier_With_PurchaseInvoice_IsBlocked()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (supplier, _, vat) = await SeedAsync(db);

        var draft = PurchaseInvoice.CreateDraft(
            supplier.Id, supplier.TaxProfile, "SUP-INV-X", new DateOnly(2026, 5, 7));
        draft.AddLine(itemId: null, expenseCategoryId: Guid.NewGuid(),
            quantity: 1m, unitPrice: MoneyEgp.From(100m),
            vatCategoryId: vat.Id, vatRatePercent: vat.RatePercent,
            deductibleFlag: false);
        db.Add(draft);
        await db.SaveChangesAsync();

        var guard = new SqlMasterDataDeletionGuard(db);
        var result = await guard.CheckSupplierAsync(supplier.Id);

        result.CanDelete.Should().BeFalse();
        result.References.Should().ContainSingle(r => r.ReferencingDocumentType == "PurchaseInvoice"
            && r.ReferenceCount == 1);
    }

    [Fact]
    public async Task Supplier_With_NoReferences_IsAllowed()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (supplier, _, _) = await SeedAsync(db);

        var guard = new SqlMasterDataDeletionGuard(db);
        var result = await guard.CheckSupplierAsync(supplier.Id);

        result.CanDelete.Should().BeTrue();
        result.References.Should().BeEmpty();
    }

    [Fact]
    public async Task Customer_With_SalesInvoice_IsBlocked()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (_, customer, vat) = await SeedAsync(db);

        var draft = SalesInvoice.CreateDraft(
            customer.Id, customer.TaxProfile, new DateOnly(2026, 5, 7));
        draft.AddLine(Guid.NewGuid(), 1m, MoneyEgp.From(500m), vat.Id, vat.RatePercent);
        db.Add(draft);
        await db.SaveChangesAsync();

        var guard = new SqlMasterDataDeletionGuard(db);
        var result = await guard.CheckCustomerAsync(customer.Id);

        result.CanDelete.Should().BeFalse();
        result.References.Should().ContainSingle(r => r.ReferencingDocumentType == "SalesInvoice");
    }

    [Fact]
    public async Task ExpenseCategory_With_Expense_IsBlocked()
    {
        await using var db = await _fixture.CreateContextAsync();
        var category = new DeductibleExpenseCategory(
            code: $"CAT-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("فئة", "Category"),
            defaultDeductible: true,
            defaultAccountId: Guid.NewGuid());
        db.Add(category);
        await db.SaveChangesAsync();

        var expense = Expense.CreateDraft(
            new DateOnly(2026, 5, 7), category.Id, MoneyEgp.From(200m),
            deductibleFlag: false,
            description: new ArabicEnglishText("وصف", "Desc"));
        db.Add(expense);
        await db.SaveChangesAsync();

        var guard = new SqlMasterDataDeletionGuard(db);
        var result = await guard.CheckExpenseCategoryAsync(category.Id);

        result.CanDelete.Should().BeFalse();
        result.References.Should().Contain(r => r.ReferencingDocumentType == "Expense"
            && r.ReferenceCount == 1);
    }

    [Fact]
    public async Task VatCategory_With_LinesOnBothSides_AggregatesBothCounts()
    {
        // FR-007 covers transitive references via lines: a VatCategory
        // is referenced by both SalesInvoiceLine and PurchaseInvoiceLine.
        // The guard MUST surface BOTH so the operator knows the full
        // blast radius of a hypothetical delete.
        await using var db = await _fixture.CreateContextAsync();
        var (supplier, customer, vat) = await SeedAsync(db);

        var sales = SalesInvoice.CreateDraft(customer.Id, customer.TaxProfile, new DateOnly(2026, 5, 7));
        sales.AddLine(Guid.NewGuid(), 1m, MoneyEgp.From(100m), vat.Id, vat.RatePercent);
        db.Add(sales);

        var purchase = PurchaseInvoice.CreateDraft(supplier.Id, supplier.TaxProfile, "SUP-INV-Y", new DateOnly(2026, 5, 7));
        purchase.AddLine(itemId: null, expenseCategoryId: Guid.NewGuid(),
            quantity: 2m, unitPrice: MoneyEgp.From(50m),
            vatCategoryId: vat.Id, vatRatePercent: vat.RatePercent,
            deductibleFlag: true);
        db.Add(purchase);
        await db.SaveChangesAsync();

        var guard = new SqlMasterDataDeletionGuard(db);
        var result = await guard.CheckVatCategoryAsync(vat.Id);

        result.CanDelete.Should().BeFalse();
        result.References.Should().Contain(r => r.ReferencingDocumentType == "SalesInvoiceLine");
        result.References.Should().Contain(r => r.ReferencingDocumentType == "PurchaseInvoiceLine");
    }

    private static async Task<(Supplier supplier, Customer customer, VatCategory vat)> SeedAsync(AppDbContext db)
    {
        var vat = new VatCategory(
            code: "Standard", name: new ArabicEnglishText("قياسي", "Standard"),
            ratePercent: 14m, effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null, recoverableInputVat: true);
        var supplier = new Supplier(
            code: $"SUP-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("مورد", "Supplier"),
            address: new ArabicEnglishText("القاهرة", "Cairo"),
            taxProfile: SupplierTaxProfile.RegisteredTaxpayer(
                EgyptianTin.Parse("123456789"), vat.Id));
        var customer = new Customer(
            code: $"CUST-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("عميل", "Customer"),
            address: PostalAddress.Create(
                new ArabicEnglishText("القاهرة", "Cairo"),
                "Cairo", "Downtown", "Tahrir", "1"),
            taxProfile: CustomerTaxProfile.B2BRegistered(
                EgyptianTin.Parse("987654321"), false, vat.Id));
        db.Add(vat); db.Add(supplier); db.Add(customer);
        await db.SaveChangesAsync();
        return (supplier, customer, vat);
    }
}
