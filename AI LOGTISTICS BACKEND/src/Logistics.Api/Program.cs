using Logistics.Api.Authentication;
using Logistics.Api.Extensions;
using Logistics.Api.Hubs;
using Logistics.Api.Middleware;
using Logistics.Api.OpenApi;
using Logistics.Api.Validation;
using Microsoft.AspNetCore.HttpLogging;
using Logistics.Infrastructure;
using Logistics.Infrastructure.AI.RAG;
using Logistics.Infrastructure.AI.Ollama;
using Logistics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    // A GLOBAL authorisation filter rather than per-controller attributes: a controller added later is
    // protected by default, so forgetting an attribute cannot silently expose it.
    options.Filters.Add(new AuthorizeFilter());

    // Bounds page/pageSize on every list endpoint. A global filter rather than a guard per service, because
    // copy-pasting the guard is what left seven of ten list paths unbounded in the first place.
    options.Filters.Add<PagingValidationFilter>();
})
.ConfigureApiBehaviorOptions(options =>
{
    // Without this, a model-validation failure returns a shape that differs from every other error the API
    // produces — no `code`, no `correlationId`, a different `type` URI. One shape is what lets a caller
    // write a single error handler, and the correlation id is what makes the failure traceable at all.
    options.InvalidModelStateResponseFactory = ValidationProblem.Create;
});
// Every ProblemDetails the framework or a controller produces is normalised here.
//
// Without this, an error returned by a controller through ControllerBase.Problem() — the 401 from the token
// endpoint, for instance — kept the framework's own shape: the RFC 9110 type URI, a `traceId`, and NO `code`
// or `correlationId`. That is a second error vocabulary, and it survived the earlier pass that unified
// validation failures because validation goes through a different factory. A caller could not branch on
// `code`, and the failure carried no correlation id to look up.
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var status = context.ProblemDetails.Status ?? StatusCodes.Status500InternalServerError;

        // Assigned, not ??=-ed. ControllerBase.Problem() pre-sets Type to the RFC 9110 link, so a null-coalescing
        // assignment leaves that in place and the type scheme stays inconsistent — which is exactly what a probe
        // showed after the first attempt at this.
        context.ProblemDetails.Type = $"https://httpstatuses.io/{status}";

        // TryAdd: the validation factory already sets a more precise code, which must win.
        context.ProblemDetails.Extensions.TryAdd("code", ProblemCodes.DepuisStatut(status));

        var correlationId = context.HttpContext.Items[CorrelationIdMiddleware.HeaderName]?.ToString()
            ?? context.HttpContext.TraceIdentifier;

        context.ProblemDetails.Extensions["correlationId"] = correlationId;
    };
});
builder.Services.AddHttpContextAccessor();

// CORS, driven by configuration and OFF unless origins are listed.
//
// A Web API is normally called from a browser front end on a different origin — a different port is enough —
// and the browser blocks that outright without a policy, so its absence is a deployment blocker rather than a
// hardening choice. But defaulting to AllowAnyOrigin would be the wrong fix: the allow-list is explicit and
// empty by default, so nothing is opened until an operator names the origins that should reach it.
//
// AllowCredentials is deliberately NOT enabled. This API authenticates with bearer tokens, which a browser
// does not attach automatically, so credentialed CORS buys nothing here — and leaving it off means a
// misconfigured origin cannot cause a browser to send the user's credentials to it.
var originesAutorisees = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

if (originesAutorisees.Length > 0)
{
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
        .WithOrigins(originesAutorisees)
        .AllowAnyHeader()
        .AllowAnyMethod()));
}

// Request logging, with the field set chosen rather than defaulted.
//
// The framework's default LoggingFields includes request and response HEADERS and request BODIES. That is
// unsafe here for two concrete reasons: the token endpoint takes a password in its body, and every other
// endpoint takes a bearer token in a header — the defaults would write both into the log in clear text.
// Only the shape of the request is recorded, which is what makes an endpoint slow or failing visible.
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod
        | HttpLoggingFields.RequestPath
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.Duration;

    // One line per request rather than four fragments that have to be reassembled by hand.
    options.CombineLogs = true;
});

// .NET 10 ships first-class OpenAPI generation in the framework (Microsoft.AspNetCore.OpenApi).
// Swashbuckle is deliberately NOT used: its SwaggerUI / ApiDescription.Server packages are not
// resolvable in this offline environment, and the built-in generator is the supported path on .NET 10.
// The security scheme and global requirement are declared by transformer rather than by annotating 158
// operations: the global form cannot be forgotten when an endpoint is added, and the handful of genuinely
// anonymous endpoints opt out explicitly.
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer(OpenApiSecurity.AjouterSchemaAsync);
    options.AddOperationTransformer(OpenApiSecurity.RetirerSurAnonymesAsync);

    // Publishes the legacy column lengths as maxLength on every schema, derived from the EF model rather than
    // from attributes on 55 request records — one statement of each limit, which is the copy that cannot drift.
    options.AddSchemaTransformer<LegacyLengthSchemaTransformer>();
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiInfrastructure();
builder.Services.AddApiAuthentication(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddLocalOllama(builder.Configuration);
builder.Services.AddChatSignalR();
builder.Services.AddLogisticsMcp();
builder.Services.AddKnowledgeBase(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddRagBackend();

// Liveness + readiness. The DbContext probe is hand-written because the
// Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore package is not available offline.
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("API is running."), tags: ["live"])
    .AddCheck<DatabaseHealthCheck>("sqlserver", tags: ["ready"]);

// The caller context is a singleton decision (development fallback or not) applied per request, so the
// flag is resolved once at startup rather than read from configuration on every request.
var headerFallback = builder.Environment.IsDevelopment()
    && string.Equals(builder.Configuration["Authentication:AllowHeaderFallback"], "true", StringComparison.OrdinalIgnoreCase);

// HTTPS redirection and HSTS, OFF unless configured.
//
// Enabling redirection blindly is a hazard rather than a hardening default: an API behind a TLS-terminating
// proxy receives plain HTTP from that proxy, so redirecting to HTTPS there produces either a loop or a broken
// health probe. Correct behaviour depends on the deployment, so this is a decision rather than a default.
//
// The failure guarded against here is subtler than a loop. HttpsRedirectionMiddleware needs to know the HTTPS
// port, and when it cannot determine one it logs a warning and REDIRECTS NOTHING — so an operator who enables
// the switch gets a silent no-op while reasonably believing traffic is being upgraded. That is the same shape
// as the request-log category suppressed by a blanket rule, and the OpenAPI transformer publishing zero
// constraints: configured, running, doing nothing. So when the switch is on and no port can be found, the host
// refuses to start instead of pretending.
var redirigerHttps = builder.Configuration.GetValue("Hosting:HttpsRedirection", false);
var portHttps = builder.Configuration.GetValue<int?>("Hosting:HttpsPort");

if (redirigerHttps)
{
    var portDetermine = portHttps
        ?? (builder.Configuration["urls"] ?? builder.Configuration["ASPNETCORE_URLS"] ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) ? uri : null)
            .FirstOrDefault(u => u?.Scheme == Uri.UriSchemeHttps)
            ?.Port;

    if (portDetermine is null or 0)
    {
        throw new InvalidOperationException(
            "Hosting:HttpsRedirection is enabled but no HTTPS port could be determined, which would make the "
            + "redirect a silent no-op. Set Hosting:HttpsPort, or include an https:// URL in urls.");
    }

    builder.Services.AddHttpsRedirection(options => options.HttpsPort = portDetermine);
}

var app = builder.Build();

if (redirigerHttps)
{
    app.UseHttpsRedirection();

    // HSTS outside Development only. A browser caches it for months, so pinning it while developing makes
    // localhost unusable over HTTP long after the setting is removed.
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }
}

app.UseMiddleware<CorrelationIdMiddleware>();

// After the correlation middleware, so its log scope carries the correlation id, and BEFORE the exception
// handler, so the log records the status the handler ultimately wrote rather than nothing at all.
app.UseHttpLogging();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Before authentication, so a preflight OPTIONS request is answered without one — a browser never sends
// credentials on a preflight, so requiring authentication for it would fail every cross-origin call.
if (originesAutorisees.Length > 0)
{
    app.UseCors();
}

app.UseAuthentication();

// Runs after authentication so the principal is resolved, and before routing so every endpoint — including
// controllers and health checks — sees the same caller context.
app.UseMiddleware<CallerContextMiddleware>(headerFallback);
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{    // Serves the generated document at /openapi/v1.json
    app.MapOpenApi();

    // Minimal, dependency-free API explorer. The stock Swagger UI is loaded from a CDN and would not
    // work without outbound network access, so the document is rendered by a small inline script.
    app.MapGet("/swagger", () => Results.Content(ApiExplorerHtml.Page, "text/html"))
        .AllowAnonymous()
        .ExcludeFromDescription();
}

// Health probes stay anonymous: an orchestrator has no token, and refusing them would make the readiness
// gate itself the outage.
// The tags and summary are declared so the probes actually appear in the OpenAPI document. Without metadata a
// MapHealthChecks endpoint is undocumented, which would leave the security transformer's anonymous exemptions
// for these paths as configuration that never applies to anything.
//
// WithTags/WithSummary are available on IEndpointConventionBuilder, which MapHealthChecks returns. Produces is
// NOT — it is a RouteHandlerBuilder extension — so the 200/503 pair is left to the summary rather than
// invented. (An earlier attempt called Produces here; it did not compile, and a chained `dotnet run --no-build`
// then silently verified the previous binary.)
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") })
    .AllowAnonymous()
    .WithTags("Health")
    .WithSummary("Liveness probe: is the process running? 200 healthy, 503 unhealthy.");

app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") })
    .AllowAnonymous()
    .WithTags("Health")
    .WithSummary("Readiness probe: can this instance serve traffic? Checks the database. 200 healthy, 503 unhealthy.");

app.MapControllers();
app.MapLogisticsMcp();
app.MapHub<ChatHub>(ChatHub.Endpoint, options => options.CloseOnAuthenticationExpiration = true);

app.Run();

/// <summary>Exposed so the integration test host can reference the entry-point assembly.</summary>
public partial class Program;

/// <summary>
/// A stable code for an error that reached the client without one.
/// </summary>
/// <remarks>
/// Matches the vocabulary the exception middleware already uses, so a caller branching on code sees one set
/// of values regardless of which layer produced the error.
/// </remarks>
internal static class ProblemCodes
{
    public static string DepuisStatut(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "invalid_request",
        StatusCodes.Status401Unauthorized => "unauthorized",
        StatusCodes.Status403Forbidden => "forbidden",
        StatusCodes.Status404NotFound => "not_found",
        StatusCodes.Status409Conflict => "conflict",
        StatusCodes.Status422UnprocessableEntity => "business_rule_violation",
        StatusCodes.Status499ClientClosedRequest => "cancelled",
        StatusCodes.Status503ServiceUnavailable => "database_unavailable",
        _ => status >= 500 ? "server_error" : "request_error"
    };
}
