// Stage 1+2 minimum-viable entry point. Replaced as Stage 2 progresses
// with full Hangfire / Blazor / authentication wiring per tasks T050,
// T054, T058, T073-T076. Currently wires the MediatR pipeline (T037) so
// the Application layer's behaviours are exercised once handlers land.

using EgyptTax.Application;
using EgyptTax.Application.Common.Abstractions;
using EgyptTax.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddScoped<ICurrentUser, AnonymousCurrentUser>();

var app = builder.Build();

app.MapGet("/", () =>
    "EgyptTax — Stage 1+2 scaffold. MediatR pipeline wired; full Blazor application ships in subsequent stages.");

app.Run();
