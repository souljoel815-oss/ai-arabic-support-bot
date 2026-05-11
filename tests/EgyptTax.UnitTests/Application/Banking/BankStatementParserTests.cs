using EgyptTax.Application.Banking;
using EgyptTax.Application.Banking.Parsers;

namespace EgyptTax.UnitTests.Application.Banking;

/// <summary>
/// G1.3 — covers the three Egyptian-bank CSV parsers + the registry's
/// auto-detect. Fixtures are synthetic CSV strings; real-customer
/// samples (when they arrive) get tacked on as named regressions.
/// </summary>
public class BankStatementParserTests
{
    private const string CibFixture =
        "\"Date\",\"Value Date\",\"Description\",\"Reference\",\"Debit\",\"Credit\",\"Running Balance\"\n" +
        "\"01/04/2026\",\"01/04/2026\",\"OPENING\",\"\",\"\",\"\",\"100000.00\"\n" +
        "\"02/04/2026\",\"02/04/2026\",\"TRANSFER FROM HOPE CO\",\"TRX-5001\",\"\",\"5,000.00\",\"105000.00\"\n" +
        "\"03/04/2026\",\"03/04/2026\",\"ATM WITHDRAWAL CAIRO\",\"ATM-9876\",\"2000.00\",\"\",\"103000.00\"\n";

    private const string NbeFixture =
        "Transaction Date,Description,Withdrawal,Deposit,Balance,Reference No\n" +
        "2026-04-01,Salary Credit,,15000.00,115000.00,SLR-001\n" +
        "2026-04-02,Cheque Payment,2500.00,,112500.00,CHQ-7700\n";

    private const string QnbFixture =
        "Posting Date|Description|Debit Amount|Credit Amount|Closing Balance|Cheque/Reference\n" +
        "01/04/2026|Online Transfer In|0.00|7500.00|107500.00|OT-1234\n" +
        "02/04/2026|POS Payment|350.50|0.00|107149.50|POS-9981\n";

    // ---------- CIB ----------

    [Fact]
    public void Cib_DetectsHeader()
    {
        var parser = new CibBankStatementParser();
        parser.CanParse(CibFixture.Split('\n')[0]).Should().BeTrue();
        parser.CanParse("garbage,header,row").Should().BeFalse();
    }

    [Fact]
    public void Cib_ParsesAllRows_HandlesQuotedThousandSeparators()
    {
        var parser = new CibBankStatementParser();
        var result = parser.Parse(CibFixture);

        result.Warnings.Should().BeEmpty();
        result.Lines.Should().HaveCount(3);

        // Line 2: TRANSFER FROM HOPE CO — credit 5,000.00 (with comma)
        var credit = result.Lines.Single(l => l.Description.Contains("HOPE"));
        credit.Credit.Should().Be(5000m);
        credit.Debit.Should().Be(0m);
        credit.BankReference.Should().Be("TRX-5001");

        // Line 3: ATM WITHDRAWAL — debit 2000
        var debit = result.Lines.Single(l => l.Description.Contains("ATM"));
        debit.Debit.Should().Be(2000m);
        debit.Credit.Should().Be(0m);

        result.PeriodStart.Should().Be(new DateOnly(2026, 4, 1));
        result.PeriodEnd.Should().Be(new DateOnly(2026, 4, 3));
        result.ClosingBalance.Should().Be(103000m);
    }

    [Fact]
    public void Cib_SkipsAmbiguousRow_WhenBothDebitAndCredit()
    {
        var fixture =
            "\"Date\",\"Value Date\",\"Description\",\"Reference\",\"Debit\",\"Credit\",\"Running Balance\"\n" +
            "\"01/04/2026\",\"01/04/2026\",\"AMBIGUOUS\",\"\",\"100.00\",\"100.00\",\"500.00\"\n";

        var parser = new CibBankStatementParser();
        var result = parser.Parse(fixture);

        result.Lines.Should().BeEmpty();
        result.Warnings.Should().ContainSingle(w => w.Contains("both debit and credit"));
    }

    [Fact]
    public void Cib_TooFewColumns_Warns()
    {
        var fixture =
            "\"Date\",\"Value Date\",\"Description\",\"Reference\",\"Debit\",\"Credit\",\"Running Balance\"\n" +
            "\"01/04/2026\",\"01/04/2026\",\"BAD ROW\"\n";

        var parser = new CibBankStatementParser();
        var result = parser.Parse(fixture);

        result.Lines.Should().BeEmpty();
        result.Warnings.Should().ContainSingle(w => w.Contains("expected 7 columns"));
    }

    // ---------- NBE ----------

    [Fact]
    public void Nbe_DetectsHeader()
    {
        var parser = new NbeBankStatementParser();
        parser.CanParse("Transaction Date,Description,Withdrawal,Deposit,Balance,Reference No").Should().BeTrue();
        parser.CanParse("\"Date\",\"Debit\",\"Credit\"").Should().BeFalse();
    }

    [Fact]
    public void Nbe_WithdrawalMapsToDebit_DepositMapsToCredit()
    {
        var parser = new NbeBankStatementParser();
        var result = parser.Parse(NbeFixture);

        result.Lines.Should().HaveCount(2);

        var salary = result.Lines.Single(l => l.Description == "Salary Credit");
        salary.Credit.Should().Be(15000m);
        salary.Debit.Should().Be(0m);

        var cheque = result.Lines.Single(l => l.Description == "Cheque Payment");
        cheque.Debit.Should().Be(2500m);
        cheque.Credit.Should().Be(0m);
        cheque.BankReference.Should().Be("CHQ-7700");
    }

    // ---------- QNB ----------

    [Fact]
    public void Qnb_DetectsHeader_RequiresPipe()
    {
        var parser = new QnbBankStatementParser();
        parser.CanParse("Posting Date|Description|Debit Amount|Credit Amount|Closing Balance|Cheque/Reference").Should().BeTrue();
        // Same column names but with commas → not QNB (would be CIB-ish).
        parser.CanParse("Posting Date,Description,Debit Amount,Credit Amount,Closing Balance,Cheque/Reference").Should().BeFalse();
    }

    [Fact]
    public void Qnb_ParsesPipeDelimitedRows()
    {
        var parser = new QnbBankStatementParser();
        var result = parser.Parse(QnbFixture);

        result.Lines.Should().HaveCount(2);
        result.Lines[0].Credit.Should().Be(7500m);
        result.Lines[0].Debit.Should().Be(0m);
        result.Lines[1].Debit.Should().Be(350.50m);
        result.Lines[1].Credit.Should().Be(0m);
        result.ClosingBalance.Should().Be(107149.50m);
    }

    // ---------- Registry ----------

    [Fact]
    public void Registry_AutoDetects_Cib()
    {
        var parser = BankStatementParserRegistry.Detect(CibFixture);
        parser.Should().NotBeNull();
        parser!.BankName.Should().Be("CIB");
    }

    [Fact]
    public void Registry_AutoDetects_Nbe()
    {
        var parser = BankStatementParserRegistry.Detect(NbeFixture);
        parser.Should().NotBeNull();
        parser!.BankName.Should().Be("NBE");
    }

    [Fact]
    public void Registry_AutoDetects_Qnb()
    {
        var parser = BankStatementParserRegistry.Detect(QnbFixture);
        parser.Should().NotBeNull();
        parser!.BankName.Should().Be("QNB");
    }

    [Fact]
    public void Registry_ReturnsNull_ForUnknownFormat()
    {
        var garbage = "this,is,not,a,bank,statement\n1,2,3,4,5,6\n";
        BankStatementParserRegistry.Detect(garbage).Should().BeNull();
    }

    [Fact]
    public void Registry_ReturnsNull_ForEmptyInput()
    {
        BankStatementParserRegistry.Detect("").Should().BeNull();
        BankStatementParserRegistry.Detect("   \n\n").Should().BeNull();
    }

    [Fact]
    public void Registry_LooksUp_ByName_CaseInsensitive()
    {
        BankStatementParserRegistry.ByName("cib").Should().NotBeNull();
        BankStatementParserRegistry.ByName("CIB").Should().NotBeNull();
        BankStatementParserRegistry.ByName("Unknown").Should().BeNull();
    }
}
