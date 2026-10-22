using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Logistics.Api.OpenApi;

/// <summary>
/// Declares how to authenticate, so a generated client or a standard explorer can discover it.
/// </summary>
/// <remarks>
/// <para>
/// Without this the document described 123 paths and said nothing about authentication, even though every one
/// of them requires a bearer token. The consequences are concrete rather than cosmetic: a generated client
/// (NSwag, openapi-generator, Kiota) has no way to know a token is needed or where to put it, and a standard
/// explorer renders no Authorize control, so every "try it" returns 401 with nothing to explain why.
/// </para>
/// <para>
/// The requirement is declared <b>globally</b> and removed per-operation for the few endpoints that must stay
/// open. The reverse — declaring it on each of 158 operations — is the version that silently rots the first
/// time someone adds an endpoint and forgets.
/// </para>
/// </remarks>
public static class OpenApiSecurity
{
    /// <summary>Name of the declared security scheme.</summary>
    public const string SchemaName = "LogisticsToken";

    /// <summary>
    /// Paths that must remain callable without a token, and that the document actually describes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The token endpoint is here because it is how a caller obtains a token — requiring one to reach it would
    /// be a closed loop. The explorer and document paths serve HTML and JSON rather than API operations, so the
    /// exemption is already correct if a future generator starts describing them.
    /// </para>
    /// <para>
    /// <b>The health probes are deliberately NOT listed.</b> The probes are anonymous at runtime via
    /// <c>AllowAnonymous</c>, but the .NET 10 built-in generator does not describe <c>MapHealthChecks</c>
    /// endpoints at all — verified by probe, with tags and a summary attached and a successful build — so an
    /// entry for them here would be configuration that could never apply to anything. Runtime anonymity is
    /// enforced at the endpoint, not in this document.
    /// </para>
    /// </remarks>
    public static readonly IReadOnlySet<string> Anonymes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "/api/v1/auth/token",
        "/swagger",
        "/openapi/v1.json"
    };

    /// <summary>Declares the scheme and applies it to every operation by default.</summary>
    public static Task AjouterSchemaAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes[SchemaName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "opaque",
            Description =
                "Token issued by POST /api/v1/auth/token. Send it as `Authorization: Bearer <token>`. "
                + "The token is opaque, not a JWT, and carries the company it was issued for — so it also "
                + "determines which tenant's data is visible."
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemaName, document)] = []
        });

        return Task.CompletedTask;
    }

    /// <summary>Removes the global requirement from the endpoints that must stay open.</summary>
    /// <remarks>
    /// Sets an <b>empty list</b> rather than clearing to null: per the OpenAPI specification an operation's
    /// empty <c>security</c> array is what overrides the document-level requirement. Null would leave the
    /// global one in force and the endpoint would still be documented as needing a token.
    /// </remarks>
    public static Task RetirerSurAnonymesAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var chemin = context.Description.RelativePath;

        if (chemin is null)
        {
            return Task.CompletedTask;
        }

        var normalise = "/" + chemin.TrimStart('/');

        if (Anonymes.Contains(normalise))
        {
            operation.Security = [];
        }

        return Task.CompletedTask;
    }
}
