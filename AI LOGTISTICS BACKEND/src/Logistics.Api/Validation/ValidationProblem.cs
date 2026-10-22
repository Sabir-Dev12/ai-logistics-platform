using Logistics.Api.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Logistics.Api.Validation;

/// <summary>
/// Makes model-validation failures look like every other error the API returns.
/// </summary>
/// <remarks>
/// <para>
/// The framework's default 400 is a <c>ValidationProblemDetails</c> that differs from the shape
/// <see cref="ExceptionHandlingMiddleware"/> produces for everything else: it has no <c>code</c>, no
/// <c>correlationId</c>, and a different <c>type</c> URI (the RFC 9110 link rather than
/// <c>https://httpstatuses.io/{status}</c>). The practical consequences are that a client cannot write one
/// error handler, and — worse — a validation failure carrying no correlation id cannot be traced in the
/// logs at all, which defeats the point of the correlation middleware for the most common client error
/// there is.
/// </para>
/// <para>
/// The <c>errors</c> map is kept, because field-level detail is the one thing the default response gets
/// right and it is genuinely useful.
/// </para>
/// </remarks>
public static class ValidationProblem
{
    /// <summary>The stable code a caller can branch on for a validation failure.</summary>
    public const string Code = "validation_failed";

    /// <summary>
    /// Returns a <c>ValidationProblemDetails</c> shaped identically to the middleware's errors.
    /// </summary>
    public static IActionResult Create(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Create(context, context.ModelState);
    }

    /// <summary>
    /// Same shape, with the model state supplied explicitly.
    /// </summary>
    /// <remarks>
    /// The two-argument form exists because <see cref="ActionContext"/> couples the request to a specific
    /// <see cref="ModelStateDictionary"/>; a filter that has already accumulated errors elsewhere would
    /// otherwise have to construct a context in order to throw its own state away.
    /// </remarks>
    public static IActionResult Create(ActionContext context, ModelStateDictionary modelState)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(modelState);

        var correlationId = context.HttpContext.Items[CorrelationIdMiddleware.HeaderName]?.ToString()
            ?? context.HttpContext.TraceIdentifier;

        var problem = new ValidationProblemDetails(modelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = "One or more fields are missing or invalid. See 'errors' for details.",
            Instance = context.HttpContext.Request.Path,
            // Same type scheme as every other error, so a client can parse one shape.
            Type = $"https://httpstatuses.io/{StatusCodes.Status400BadRequest}"
        };

        problem.Extensions["code"] = Code;

        // Present even though a validation failure is not a server fault: without it the request is
        // untraceable, and validation errors are the most frequent thing a caller hits.
        problem.Extensions["correlationId"] = correlationId;

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" }
        };
    }
}
