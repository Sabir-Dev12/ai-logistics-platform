using Logistics.Application.Abstractions;
using Logistics.Domain.Common;
using Logistics.Domain.Entities.ReferenceData;

namespace Logistics.Application.Features.ReferenceData;

/// <summary>The referentials the API can delete, and whose legacy guards are reproduced.</summary>
public enum ReferentialKind
{
    Pays,
    Devise,
    Ville,
    Port,
    Incoterm,
    ZonePays,
    Agence,
    ModePaiement,
    CompteTva,
    ClasseClient,
    StatutClient,
    Risque,
    SecteurActivite,
    GroupeSociete,
    Tresorie,
    NatureFraisBancaire,
    TypeIncidentPaiement
}

/// <summary>Result of asking whether a referential row can be removed.</summary>
public sealed record ReferenceCheckResult(
    ReferentialKind Referential,
    string Key,
    bool CanDelete,
    IReadOnlyList<string> ReferencingTables,
    string? BlockingReason)
{
    public string? Message => CanDelete
        ? null
        : BlockingReason ?? $"Suppression impossible : référencé par {string.Join(", ", ReferencingTables)}.";
}

/// <summary>
/// Reports what still references a referential row.
/// </summary>
/// <remarks>
/// This is the migration of the ~50 hand-written <c>Referencee()</c> methods scattered across the
/// <c>Class*.vb</c> files. They were inconsistent — several were commented out, one was an empty stub
/// that always returned <c>false</c>, and one checked a table that no longer exists — so the guard
/// behaviour below is deliberately explicit per referential rather than assumed uniform. Where the
/// legacy code had <em>no</em> guard, none is applied here either: silently adding one would break
/// existing delete workflows.
/// </remarks>
public interface IReferenceGuardRepository
{
    Task<IReadOnlyList<string>> FindReferencesAsync(ReferentialKind kind, string key, CancellationToken cancellationToken = default);
    /// <summary>Agency count, for the licence ceiling in <c>t_societe.nb_agence</c>.</summary>
    Task<int> CountAgencesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Which referentials use a soft-delete flag rather than a physical delete.</summary>
public static class ReferentialPolicy
{
    /// <summary>
    /// <c>t_agence</c> uses <c>supp=1</c> and <c>t_incoterm</c> uses <c>supp_incoterm=1</c>; every other
    /// referential in the legacy code is hard-deleted.
    /// </summary>
    public static bool UsesSoftDelete(ReferentialKind kind) =>
        kind is ReferentialKind.Agence or ReferentialKind.Incoterm;
}

public interface IReferenceDataService
{
    Task<ReferenceCheckResult> CheckReferencesAsync(ReferentialKind kind, string key, CancellationToken cancellationToken = default);
    /// <summary>Removes the row, refusing when something still references it.</summary>
    Task DeleteAsync(ReferentialKind kind, string key, CancellationToken cancellationToken = default);
}

public sealed class ReferenceDataService(
    IReferenceGuardRepository guards,
    IReferenceDataRepository referenceData,
    IAuditTrail audit,
    ITenantContext tenant) : IReferenceDataService
{
    public async Task<ReferenceCheckResult> CheckReferencesAsync(
        ReferentialKind kind, string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var references = await guards.FindReferencesAsync(kind, key, cancellationToken);

        if (references.Count > 0)
        {
            return new ReferenceCheckResult(kind, key, false, references, null);
        }

        // The agency licence ceiling is a cap on creation, not a reference, so it is reported as its
        // own blocking reason rather than as a referencing table.
        if (kind == ReferentialKind.Agence)
        {
            var societe = await referenceData.FindAsync<Domain.Entities.ReferenceData.Societe>(tenant.SocieteKey);
            var agences = await guards.CountAgencesAsync(cancellationToken);

            if (societe?.NbAgence is > 0 && agences >= societe.NbAgence)
            {
                return new ReferenceCheckResult(kind, key, true, [],
                    $".Attention : le nombre d'agences ({agences}) atteint le plafond de la licence ({societe.NbAgence}).");
            }
        }

        return new ReferenceCheckResult(kind, key, true, [], null);
    }

    public async Task DeleteAsync(ReferentialKind kind, string key, CancellationToken cancellationToken = default)
    {
        var check = await CheckReferencesAsync(kind, key, cancellationToken);

        if (!check.CanDelete)
        {
            throw new BusinessRuleException("referentiel.reference", check.Message!);
        }

        await RemoveAsync(kind, key, cancellationToken);

        await referenceData.SaveChangesAsync(cancellationToken);
        await audit.JournalAsync(kind.ToString(), "Suppression", $"Clé = {key}", cancellationToken);
    }

    /// <summary>
    /// Applies the soft-delete flag where the legacy referential used one, and removes the row
    /// otherwise.
    /// </summary>
    private async Task RemoveAsync(ReferentialKind kind, string key, CancellationToken cancellationToken)
    {
        switch (kind)
        {
            case ReferentialKind.Agence:
            {
                var entity = await referenceData.FindAsync<Agence>(key)
                    ?? throw new EntityNotFoundException(nameof(Agence), key);
                entity.SoftDelete();
                break;
            }

            case ReferentialKind.Incoterm:
            {
                var entity = await referenceData.FindAsync<Incoterm>(key)
                    ?? throw new EntityNotFoundException(nameof(Incoterm), key);
                entity.SoftDelete();
                break;
            }

            case ReferentialKind.Pays:
                Remove(await FindOrThrowAsync<Pays>(key), key);
                break;

            case ReferentialKind.Devise:
                Remove(await FindOrThrowAsync<Devise>(key), key);
                break;

            case ReferentialKind.Ville:
                Remove(await FindOrThrowAsync<Ville>(key), key);
                break;

            case ReferentialKind.Port:
                Remove(await FindOrThrowAsync<Port>(key), key);
                break;

            case ReferentialKind.ZonePays:
                Remove(await FindOrThrowAsync<ZonePays>(key), key);
                break;

            case ReferentialKind.ModePaiement:
                Remove(await FindOrThrowAsync<ModePaiement>(key), key);
                break;

            case ReferentialKind.ClasseClient:
                Remove(await FindOrThrowAsync<ClasseClient>(key), key);
                break;

            case ReferentialKind.StatutClient:
                Remove(await FindOrThrowAsync<StatutClient>(key), key);
                break;

            case ReferentialKind.Risque:
                Remove(await FindOrThrowAsync<Risque>(key), key);
                break;

            case ReferentialKind.SecteurActivite:
                Remove(await FindOrThrowAsync<SecteurActivite>(key), key);
                break;

            case ReferentialKind.GroupeSociete:
                Remove(await FindOrThrowAsync<GroupeSociete>(key), key);
                break;

            case ReferentialKind.Tresorie:
                Remove(await FindOrThrowAsync<Tresorie>(key), key);
                break;

            case ReferentialKind.NatureFraisBancaire:
                Remove(await FindOrThrowAsync<NatureFraisBancaire>(key), key);
                break;

            case ReferentialKind.TypeIncidentPaiement:
                Remove(await FindOrThrowAsync<TypeIncidentPaiement>(key), key);
                break;

            // t_compte_tva is keyed by (code_tva, tva_soc), so both parts are supplied.
            case ReferentialKind.CompteTva:
                Remove(await FindOrThrowAsync<CompteTva>(key, tenant.SocieteKey), key);
                break;

            default:
                throw new BusinessRuleException("referentiel.inconnu", $"Référentiel non pris en charge : {kind}.");
        }
    }

    private async Task<T> FindOrThrowAsync<T>(params object[] keyValues) where T : class =>
        await referenceData.FindAsync<T>(keyValues)
        ?? throw new EntityNotFoundException(typeof(T).Name, string.Join(':', keyValues.Select(k => k?.ToString())));

    private void Remove<T>(T entity, string key) where T : class
    {
        ArgumentNullException.ThrowIfNull(entity);
        referenceData.Remove(entity);
    }
}
