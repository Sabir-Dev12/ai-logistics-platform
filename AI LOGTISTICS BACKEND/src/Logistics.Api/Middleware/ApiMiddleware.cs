using System.Data.Common;
using System.Diagnostics;
using Logistics.Domain.Common;
using Logistics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Logistics.Api.Middleware;

/// <summary>Stamps every request and response with a correlation id so logs and clients can join them.</summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Activity.Current?.Id ?? context.TraceIdentifier;
        }

        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Request")
            // The message-template overload, NOT a Dictionary. A scope is rendered by the console formatter
            // only when it is an IReadOnlyList<KeyValuePair<string, object>> — which the template form
            // produces. A Dictionary does not implement that, so it falls through to ToString() and every log
            // line prints the literal text "System.Collections.Generic.Dictionary`2[System.String,System.Object]"
            // in place of the correlation id. The effect is a scope that looks present and carries nothing.
            .BeginScope("CorrelationId:{CorrelationId}", correlationId))
        {
            await next(context);
        }
    }
}

/// <summary>
/// Converts every exception into an RFC 7807 <see cref="ProblemDetails"/> response.
/// </summary>
/// <remarks>
/// This is the single replacement for the legacy pattern of <c>MsgBox(ex.Message)</c> followed by a
/// hand-written row in <c>t_evenement_app</c> — a pattern repeated in roughly 1,700 places. Domain
/// exceptions carry a stable machine-readable code so clients can branch on the failure without
/// parsing French prose.
/// </remarks>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            await WriteProblemAsync(context, exception);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            logger.LogError(exception, "Response already started; cannot write ProblemDetails.");
            return;
        }

        var (status, title, code) = exception switch
        {
            EntityNotFoundException => (StatusCodes.Status404NotFound, "Resource not found", "not_found"),
            DuplicateEntityException => (StatusCodes.Status409Conflict, "Duplicate resource", "duplicate"),
            // 403 rather than 422: the caller should not have been attempting this, which is a different
            // statement from "the request was understood and refused".
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden", "forbidden"),
            // 400 rather than 409: a field whose value cannot be stored is the caller's to correct, and
            // retrying it unchanged will never succeed.
            ChampInvalideException => (StatusCodes.Status400BadRequest, "Validation failed", "validation_failed"),
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, "Concurrency conflict", "concurrency_conflict"),
            BusinessRuleException => (StatusCodes.Status422UnprocessableEntity, "Business rule violated", "business_rule_violation"),
            DomainException domain => (StatusCodes.Status400BadRequest, "Invalid request", domain.Code),
            // ArgumentOutOfRangeException derives from ArgumentException, so it must come first —
            // otherwise this arm is unreachable (CS8510).
            ArgumentOutOfRangeException => (StatusCodes.Status400BadRequest, "Invalid request", "invalid_argument"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request", "invalid_argument"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found", "not_found"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency conflict", "concurrency_conflict"),
            DbUpdateException => (StatusCodes.Status409Conflict, "Persistence conflict", "persistence_conflict"),
            // An unreachable or timing-out database is an infrastructure outage, not an application
            // fault. Reporting it as 503 lets a load balancer or orchestrator take the instance out of
            // rotation instead of treating it as a bug in the request.
            DbException => (StatusCodes.Status503ServiceUnavailable, "Database unavailable", "database_unavailable"),
            OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, "Request cancelled", "cancelled"),
            _ => (StatusCodes.Status500InternalServerError, "Server error", "server_error")
        };

        var correlationId = context.Items[CorrelationIdMiddleware.HeaderName]?.ToString();

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled error on {Method} {Path} (correlation {CorrelationId}).",
                context.Request.Method, context.Request.Path, correlationId);
        }
        else
        {
            logger.LogWarning("Request rejected on {Method} {Path}: {Code} (correlation {CorrelationId}).",
                context.Request.Method, context.Request.Path, code, correlationId);
        }

        // Internal details are never leaked for 5xx responses.
        var detail = status >= StatusCodes.Status500InternalServerError
            ? "An unexpected error occurred. Use the correlation id to locate the server log entry."
            : exception.Message;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
            Type = $"https://httpstatuses.io/{status}"
        };

        problem.Extensions["code"] = code;

        if (correlationId is not null)
        {
            problem.Extensions["correlationId"] = correlationId;
        }

        if (exception is EntityNotFoundException notFound)
        {
            problem.Extensions["entity"] = notFound.Entity;
            problem.Extensions["key"] = notFound.Key;
        }

        if (exception is BusinessRuleException rule)
        {
            problem.Extensions["rule"] = rule.Rule;
        }

        // Naming the required permission makes a 403 actionable rather than just a refusal.
        if (exception is ForbiddenException forbidden)
        {
            problem.Extensions["permission"] = forbidden.Permission;
        }

        // Same field-keyed shape the model-binding path produces, so one caller-side handler covers both.
        if (exception is ChampInvalideException invalide)
        {
            problem.Extensions["errors"] = invalide.Erreurs;
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(problem);
    }
}

/// <summary>
/// Readiness probe for SQL Server.
/// </summary>
/// <remarks>
/// Hand-written because <c>Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore</c> is
/// not resolvable offline. It reports the provider name and whether the schema is reachable, which is
/// more useful during a migration than a bare boolean.
/// </remarks>
public sealed class DatabaseHealthCheck(LogisticsDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy("SQL Server is reachable.")
                : HealthCheckResult.Unhealthy("SQL Server is not reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL Server probe failed.", ex);
        }
    }
}
