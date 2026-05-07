// Stage 1 minimum-viable entry point. Replaced in Stage 2 with full
// MediatR / Hangfire / Blazor / authentication wiring per tasks T037, T050,
// T054, T058, T073-T076. This stub exists only so the solution builds clean
// and CI is green at the Stage 1 exit gate (T012).

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () =>
    "EgyptTax — Stage 1 scaffold. Full Blazor application ships in Stage 2 onwards.");

app.Run();
