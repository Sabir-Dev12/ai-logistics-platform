using Logistics.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Logistics.Api.Validation;

/// <summary>
/// Bounds the <c>page</c> and <c>pageSize</c> query parameters on every list endpoint.
/// </summary>
/// <remarks>
/// <para>
/// Applied globally from <see cref="Microsoft.Extensions.DependencyInjection.MvcCoreMvcBuilderExtensions"/>
/// registration in <c>Program</c>, so a list endpoint added later is covered without anyone remembering to
/// guard it.
/// </para>
/// <para>
/// This exists because the previous approach had already failed. A private <c>GuardPaging</c> was
/// copy-pasted into two service files and applied to three of the ten list paths, leaving seven services
/// passing <c>pageSize</c> straight into <c>Take</c>; the repositories contain twelve paging sites in total.
/// Guarding each one is the same copy-paste that produced the gap. The query string, by contrast, is the
/// actual attack surface and there is exactly one of it.
/// </para>
/// <para>
/// The failure is reported through <see cref="ModelStateDictionary"/> rather than by short-circuiting with a
/// hand-built response, so it renders through
/// <see cref="ValidationProblem.Create"/> and looks like every other error the API returns — including
/// carrying a correlation id.
/// </para>
/// </remarks>
public sealed class PagingValidationFilter : IActionFilter
{
    /// <inheritdoc />
    public void OnActionExecuting(ActionExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var query = context.HttpContext.Request.Query;

        // Only validated when supplied. An absent pageSize means "use the default", which is not an error,
        // and a non-numeric value is already rejected by query binding.
        ValiderChamp(context, query, "page", page =>
        {
            if (page < 1)
            {
                return $"Page must be at least 1 (got {page}).";
            }

            return null;
        });

        ValiderChamp(context, query, "pageSize", pageSize =>
        {
            if (pageSize < 1 || pageSize > Paging.MaxPageSize)
            {
                return $"Page size must be between 1 and {Paging.MaxPageSize} (got {pageSize}).";
            }

            return null;
        });

        if (!context.ModelState.IsValid)
        {
            // The ActionContext is rebuilt because the filter's own is the one holding the errors: the
            // ActionContext supplied to an action filter exposes a different ModelState instance.
            var actionContext = new ActionContext(context.HttpContext, context.RouteData, context.ActionDescriptor);

            context.Result = ValidationProblem.Create(actionContext, context.ModelState);
        }
    }

    /// <inheritdoc />
    public void OnActionExecuted(ActionExecutedContext context)
    {
        // Nothing to do after the action; the request is read-only by the time it runs.
    }

    private static void ValiderChamp(
        ActionExecutingContext context,
        IQueryCollection query,
        string nom,
        Func<int, string?> regle)
    {
        // TryParse keeps a malformed value out of this filter's hands: binding reports it with its own
        // message, and reporting it twice would produce two errors for one mistake.
        if (!query.TryGetValue(nom, out var valeurs) || valeurs.Count == 0)
        {
            return;
        }

        if (!int.TryParse(valeurs[0], System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var valeur))
        {
            return;
        }

        if (regle(valeur) is { } message)
        {
            context.ModelState.AddModelError(nom, message);
        }
    }
}
