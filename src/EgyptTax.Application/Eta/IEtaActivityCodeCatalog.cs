namespace EgyptTax.Application.Eta;

/// <summary>
/// P1.1 (ETA Wizard step 3) — read-only catalog of Egyptian
/// regulator-recognised business activity codes. Powers the
/// Arabic-searchable picker so operators can find their activity
/// without memorising the 4-digit code.
///
/// Source-of-truth lives outside this app (ETA publishes the list);
/// the MVP ships a hand-curated subset of the most common ~80 codes
/// for SMB customers (retail, freelance services, manufacturing,
/// trading). Production deployments overlay the latest export.
/// </summary>
public interface IEtaActivityCodeCatalog
{
    IReadOnlyList<EtaActivityCode> All();

    IReadOnlyList<EtaActivityCode> Search(string fragment, int limit = 20);
}

public sealed record EtaActivityCode(
    string Code,
    string NameAr,
    string NameEn,
    string CategoryAr,
    string CategoryEn);
