using System.Globalization;
using System.Text.Json;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EgyptTax.Infrastructure.Ai;

/// <summary>
/// v5 — read-only "tool box" the AI chat assistant can call to
/// answer follow-up questions the static snapshot doesn't cover
/// (e.g. arbitrary months, specific customers, top-N rankings).
///
/// The toolbox advertises its capabilities to the model via
/// <see cref="GetToolDefinitions"/> (OpenAI function-calling
/// schema). The chat handler executes each requested call through
/// <see cref="ExecuteAsync"/> and feeds the JSON result back to
/// the model as a "tool" role message.
///
/// SECURITY: every tool is strictly READ-ONLY and operates on
/// aggregates only — no row-level customer/supplier details leak
/// beyond the names already shown in the UI. Tool arguments are
/// validated before any DB query runs.
/// </summary>
public sealed class AiToolbox
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<AiToolbox> _log;

    public AiToolbox(IDbContextFactory<AppDbContext> dbFactory, ILogger<AiToolbox> log)
    {
        _dbFactory = dbFactory;
        _log = log;
    }

    /// <summary>OpenAI-style function definitions, serialized as-is
    /// into the request payload's <c>tools</c> array.</summary>
    public static IReadOnlyList<object> GetToolDefinitions() => Definitions;

    /// <summary>Dispatch a tool_call from the model. Always returns
    /// a JSON-encoded string (success or error) — never throws into
    /// the chat loop.</summary>
    public async Task<string> ExecuteAsync(string toolName, string argumentsJson, CancellationToken ct)
    {
        try
        {
            return toolName switch
            {
                "count_sales_invoices_by_state" => await CountSalesByState(argumentsJson, ct),
                "vat_for_month" => await VatForMonth(argumentsJson, ct),
                "top_sales_customers_this_month" => await TopCustomersThisMonth(argumentsJson, ct),
                _ => JsonError($"Unknown tool: {toolName}"),
            };
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "AI tool {Tool} failed with args {Args}", toolName, argumentsJson);
            return JsonError(ex.Message);
        }
    }

    private async Task<string> CountSalesByState(string argsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(argsJson);
        var stateStr = doc.RootElement.TryGetProperty("state", out var s) ? s.GetString() : null;
        if (string.IsNullOrWhiteSpace(stateStr)
            || !Enum.TryParse<DocumentState>(stateStr, ignoreCase: true, out var state))
        {
            return JsonError("state must be one of: Draft, Submitted, Approved, Posted, Voided");
        }
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var count = await db.Set<SalesInvoice>().AsNoTracking()
            .CountAsync(i => i.State == state, ct);
        return JsonSerializer.Serialize(new { state = state.ToString(), count });
    }

    private async Task<string> VatForMonth(string argsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(argsJson);
        var year = doc.RootElement.TryGetProperty("year", out var y) ? y.GetInt32() : 0;
        var month = doc.RootElement.TryGetProperty("month", out var m) ? m.GetInt32() : 0;
        if (year < 2020 || year > 2100 || month is < 1 or > 12)
        {
            return JsonError("year must be 2020-2100, month must be 1-12");
        }
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var sales = await db.Set<SalesInvoice>().AsNoTracking()
            .Where(i => i.State == DocumentState.Posted
                && i.DocumentDate >= start && i.DocumentDate < end)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Vat = g.Sum(x => x.VatTotal.Amount) })
            .FirstOrDefaultAsync(ct);
        var purchases = await db.Set<PurchaseInvoice>().AsNoTracking()
            .Where(p => p.State == DocumentState.Posted
                && p.DateReceived >= start && p.DateReceived < end)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Vat = g.Sum(x => x.VatTotal.Amount) })
            .FirstOrDefaultAsync(ct);
        var output = sales?.Vat ?? 0m;
        var input = purchases?.Vat ?? 0m;
        return JsonSerializer.Serialize(new
        {
            year, month,
            posted_sales_count = sales?.Count ?? 0,
            posted_purchases_count = purchases?.Count ?? 0,
            vat_output_egp = output,
            vat_input_egp = input,
            vat_net_egp = output - input,
        });
    }

    private async Task<string> TopCustomersThisMonth(string argsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(argsJson);
        var limit = doc.RootElement.TryGetProperty("limit", out var l) && l.ValueKind == JsonValueKind.Number
            ? l.GetInt32() : 5;
        limit = Math.Clamp(limit, 1, 20);

        var now = DateTime.UtcNow;
        var start = new DateOnly(now.Year, now.Month, 1);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var rows = await db.Set<SalesInvoice>().AsNoTracking()
            .Where(i => i.State == DocumentState.Posted && i.DocumentDate >= start)
            .GroupBy(i => i.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                Total = g.Sum(x => x.GrandTotal.Amount),
                Count = g.Count(),
            })
            .OrderByDescending(x => x.Total)
            .Take(limit)
            .ToListAsync(ct);

        var ids = rows.Select(r => r.CustomerId).ToArray();
        var names = await db.Set<Customer>().AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.Name.Arabic, c.Name.English })
            .ToDictionaryAsync(x => x.Id, ct);

        var result = rows.Select(r => new
        {
            customer_id = r.CustomerId,
            name_ar = names.TryGetValue(r.CustomerId, out var n) ? n.Arabic : "(?)",
            name_en = names.TryGetValue(r.CustomerId, out var nn) ? nn.English : "(?)",
            posted_invoice_count = r.Count,
            sales_total_egp = r.Total,
        }).ToArray();
        return JsonSerializer.Serialize(new { month_start = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), customers = result });
    }

    private static string JsonError(string message) =>
        JsonSerializer.Serialize(new { error = message });

    private static readonly IReadOnlyList<object> Definitions = new object[]
    {
        new
        {
            type = "function",
            function = new
            {
                name = "count_sales_invoices_by_state",
                description = "Return the number of sales invoices in a given workflow state. Use when the user asks how many invoices are Draft, Submitted, Approved, Posted, or Voided.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        state = new
                        {
                            type = "string",
                            @enum = new[] { "Draft", "Submitted", "Approved", "Posted", "Voided" },
                            description = "Workflow state to filter by.",
                        },
                    },
                    required = new[] { "state" },
                },
            },
        },
        new
        {
            type = "function",
            function = new
            {
                name = "vat_for_month",
                description = "Return VAT output (sales) and input (purchases) totals for a specific month, plus the net amount due. Use when the user asks about VAT for a past or future month other than the current one already in the snapshot.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        year = new { type = "integer", description = "4-digit year (e.g. 2026)." },
                        month = new { type = "integer", description = "1-12, where 1 = January." },
                    },
                    required = new[] { "year", "month" },
                },
            },
        },
        new
        {
            type = "function",
            function = new
            {
                name = "top_sales_customers_this_month",
                description = "Return the top N customers by sales total for the current calendar month (posted invoices only). Use when the user asks who is buying the most or wants a ranking.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        limit = new { type = "integer", description = "How many customers to return. Default 5, max 20." },
                    },
                    required = Array.Empty<string>(),
                },
            },
        },
    };
}
