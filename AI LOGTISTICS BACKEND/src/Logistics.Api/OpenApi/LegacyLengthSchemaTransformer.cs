using Logistics.Infrastructure.Persistence;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Logistics.Api.OpenApi;

/// <summary>
/// Publishes the legacy column lengths into the OpenAPI schemas as <c>maxLength</c>.
/// </summary>
/// <remarks>
/// <para>
/// Without this the contract declared no length limits at all — not one of the 183 schemas carried a
/// <c>maxLength</c> — so a generated client had no way to know that a value it was about to send would be
/// refused, and would learn only from a 400.
/// </para>
/// <para>
/// <b>The limits come from the EF model, not from attributes on the request DTOs.</b> That is the same choice
/// the persistence-boundary guard makes, and for the same reason: there is one statement of each limit and EF
/// already owns it, so the contract cannot drift from the columns. Annotating 55 request records by hand would
/// be a second copy of the schema, and the copy is what rots.
/// </para>
/// <para>
/// DTO properties are matched to entity properties <b>by name</b>, which is a convention rather than a
/// guarantee. It is applied conservatively: a name that maps to different lengths in different entities is
/// skipped entirely rather than guessed at, because publishing the wrong limit is worse than publishing none.
/// </para>
/// </remarks>
public sealed class LegacyLengthSchemaTransformer : IOpenApiSchemaTransformer
{
    private readonly IReadOnlyDictionary<string, int> _longueurs;

    public LegacyLengthSchemaTransformer(IServiceScopeFactory scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        using var portee = scopes.CreateScope();
        var db = portee.ServiceProvider.GetRequiredService<LogisticsDbContext>();

        // Name -> the single length every entity agrees on. Ambiguous names are dropped.
        //
        // Matched case-INSENSITIVELY, and that is essential rather than defensive: EF property names are
        // PascalCase while the schema's property names follow the JSON naming policy and are camelCase, so an
        // ordinal lookup finds nothing at all. The first version of this transformer used the default comparer
        // and published zero constraints — the failure mode was silence, not an error.
        _longueurs = db.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(string) && p.GetMaxLength() is > 0)
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(p => p.GetMaxLength()!.Value).Distinct().Count() == 1)
            .ToDictionary(g => g.Key, g => g.First().GetMaxLength()!.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>How many distinct property names a limit is published for. Exposed for the harness.</summary>
    public int LongueursConnues => _longueurs.Count;

    public Task TransformAsync(
        OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);

        if (schema.Properties is null || _longueurs.Count == 0)
        {
            return Task.CompletedTask;
        }

        foreach (var (nom, propriete) in schema.Properties)
        {
            // Only where the contract does not already say, so an explicit constraint on a DTO still wins.
            if (propriete is OpenApiSchema { MaxLength: null } concret
                && _longueurs.TryGetValue(nom, out var maximum))
            {
                concret.MaxLength = maximum;
            }
        }

        return Task.CompletedTask;
    }
}
