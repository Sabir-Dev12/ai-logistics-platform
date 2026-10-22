using Logistics.Application.Abstractions;
using Logistics.Application.Features.Clients;
using Logistics.Application.Features.Invoicing;
using Logistics.Application.Features.Payments;
using Logistics.Application.Features.Bookings;
using Logistics.Application.Features.Quotations;
using Logistics.Application.Features.Edi;
using Logistics.Application.Features.Resources;
using Logistics.Application.Features.ReferenceData;
using Logistics.Application.Features.Shipments;
using Logistics.Application.Features.Tracking;
using Logistics.Infrastructure;

namespace Logistics.Api.Extensions;

public static class ApiServiceCollectionExtensions
{
    /// <summary>Registers the application use cases.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IReglementService, ReglementService>();
        services.AddScoped<IFactureVenteService, FactureVenteService>();
        services.AddScoped<IVoyageService, VoyageService>();
        services.AddScoped<IDossierService, DossierService>();
        services.AddScoped<IVoyageCostService, VoyageCostService>();
        services.AddScoped<ITrackingService, TrackingService>();
        services.AddScoped<IReferenceDataService, ReferenceDataService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IQuotationService, QuotationService>();
        services.AddScoped<IBaremeAchatService, BaremeAchatService>();
        services.AddScoped<IResourceService, ResourceService>();
        services.AddScoped<ICarburantService, CarburantService>();
        services.AddScoped<IVoyageCostRateProvider, VoyageCostRateProvider>();
        services.AddScoped<IEdiStcService, EdiStcService>();
        services.AddScoped<IEdiReconciliationService, EdiReconciliationService>();
        return services;
    }

    /// <summary>Registers the HTTP-specific adapters the infrastructure layer depends on.</summary>
    /// <remarks>
    /// The tenant context is now claims-based. The header adapter is still registered because the
    /// development fallback reads through it, but it is no longer the source of truth for identity.
    /// </remarks>
    public static IServiceCollection AddApiInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IHttpContextAccessorLike, HttpContextAccessorAdapter>();
        services.AddScoped<CallerContext>();
        services.AddScoped<ITenantContext, ClaimsTenantContext>();
        return services;
    }
}

/// <summary>Adapts ASP.NET Core's accessor to the narrow seam the infrastructure layer consumes.</summary>
internal sealed class HttpContextAccessorAdapter(IHttpContextAccessor accessor) : IHttpContextAccessorLike
{
    public string? GetHeader(string name)
    {
        var value = accessor.HttpContext?.Request.Headers[name].FirstOrDefault();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

/// <summary>
/// A minimal, self-contained API explorer.
/// </summary>
/// <remarks>
/// The conventional Swagger UI is fetched from a CDN at runtime, which is not viable in an offline
/// deployment, and the Swashbuckle packages that would vendor it are not restorable here. This page
/// renders the OpenAPI document produced by .NET 10's built-in generator with no external assets, so
/// the interactive documentation requirement is still met.
/// </remarks>
internal static class ApiExplorerHtml
{
    public const string Page = """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8" />
<title>Logistics API — OpenAPI explorer</title>
<style>
  :root { color-scheme: light dark; }
  body { font-family: ui-sans-serif, system-ui, sans-serif; margin: 0; padding: 2rem; line-height: 1.5; }
  h1 { margin-top: 0; }
  .op { border: 1px solid #8884; border-radius: 8px; margin: .5rem 0; padding: .75rem 1rem; }
  .verb { display: inline-block; min-width: 4.5rem; font-weight: 700; }
  .path { font-family: ui-monospace, monospace; }
  .summary { color: #888; margin-left: .5rem; }
  .tag { font-size: .75rem; background: #8882; border-radius: 4px; padding: .1rem .4rem; margin-left: .5rem; }
  #err { color: #c00; }
</style>
</head>
<body>
<h1>Logistics API</h1>
<p>Generated from <code>/openapi/v1.json</code>. Source: TransmarNetref legacy migration.</p>
<div id="app">Loading…</div>
<div id="err"></div>
<script>
(async () => {
  const app = document.getElementById('app');
  try {
    const res = await fetch('/openapi/v1.json');
    if (!res.ok) throw new Error('HTTP ' + res.status);
    const doc = await res.json();
    const paths = doc.paths || {};
    const groups = {};
    for (const [path, ops] of Object.entries(paths)) {
      for (const [verb, op] of Object.entries(ops)) {
        const tag = (op.tags && op.tags[0]) || 'default';
        (groups[tag] ||= []).push({ path, verb, op });
      }
    }
    app.innerHTML = '';
    const names = Object.keys(groups).sort();
    if (!names.length) { app.textContent = 'No operations found.'; return; }
    for (const name of names) {
      const h = document.createElement('h2');
      h.textContent = name;
      app.appendChild(h);
      for (const { path, verb, op } of groups[name]) {
        const d = document.createElement('div');
        d.className = 'op';
        const v = document.createElement('span');
        v.className = 'verb';
        v.textContent = verb.toUpperCase();
        d.appendChild(v);
        const p = document.createElement('span');
        p.className = 'path';
        p.textContent = path;
        d.appendChild(p);
        if (op.summary) {
          const s = document.createElement('span');
          s.className = 'summary';
          s.textContent = op.summary;
          d.appendChild(s);
        }
        if (op.deprecated) {
          const t = document.createElement('span');
          t.className = 'tag';
          t.textContent = 'deprecated';
          d.appendChild(t);
        }
        app.appendChild(d);
      }
    }
  } catch (e) {
    app.textContent = '';
    document.getElementById('err').textContent =
      'Could not load the OpenAPI document: ' + e.message +
      '. In non-Development environments the document is not mapped.';
  }
})();
</script>
</body>
</html>
""";
}
