using Logistics.Domain.Entities.Audit;
using Logistics.Domain.Entities.Clients;
using Logistics.Domain.Entities.Invoicing;
using Logistics.Domain.Entities.Payments;
using Logistics.Domain.Entities.ReferenceData;

namespace Logistics.Application.Abstractions;

/// <summary>
/// The single rule for list-query paging, enforced in one place.
/// </summary>
/// <remarks>
/// <para>
/// Without a bound, a client could ask for <c>pageSize=100000000</c> and the repository would attempt to
/// materialise that many rows. This is a robustness limit, not a business rule: it exists so that one
/// request cannot exhaust memory or saturate the database.
/// </para>
/// <para>
/// The rule lives here because it had already drifted. An identical private <c>GuardPaging</c> was
/// copy-pasted into two service files and applied to three of the ten list paths, so seven services passed
/// an unbounded page size straight through to <c>Take</c>. The copy-paste was the cause, and a second copy
/// would not be the fix. Repositories validate as well as services, because the repository is where
/// <c>Take</c> actually runs and therefore the only layer a caller cannot reach around.
/// </para>
/// </remarks>
public static class Paging
{
    /// <summary>Largest page a caller may request.</summary>
    public const int MaxPageSize = 200;

    /// <summary>Page size assumed when a caller supplies none.</summary>
    public const int DefaultPageSize = 50;

    /// <summary>
    /// Throws when the requested window is outside the allowed bounds.
    /// </summary>
    /// <remarks>
    /// Rejects rather than silently clamping. A caller that asked for 5,000 rows and quietly received 200
    /// would draw wrong conclusions from a truncated result — a silent clamp is a correctness bug dressed as
    /// a safety feature.
    /// </remarks>
    public static void Valider(int page, int pageSize)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(page), page, $"Page must be at least 1 (got {page}).");
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize), pageSize, $"Page size must be between 1 and {MaxPageSize} (got {pageSize}).");
        }
    }
}

/// <summary>A page of results plus the total row count, used by every list endpoint.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>
/// The ambient request context: which company (tenant), which agency and which user the current
/// request belongs to.
/// </summary>
/// <remarks>
/// The legacy application carries this in a mutable module-level singleton (<c>Constante</c>) built at
/// login and read by every SQL statement. A web API cannot do that — requests are concurrent — so the
/// same information is resolved per request (from claims once authentication is added; from
/// configuration until then) and injected. Tenant isolation moves from "every query remembers to add
/// <c>where soc_xxx = …</c>" to EF global query filters plus this explicit context, which is the main
/// correctness improvement over the legacy code.
/// </remarks>
public interface ITenantContext
{
    /// <summary>Company key (<c>t_societe.key_soc</c>).</summary>
    string SocieteKey { get; }
    /// <summary>Agency key (<c>t_agence.key_agence</c>), when the request is agency-scoped.</summary>
    string? AgenceKey { get; }
    /// <summary>Personnel key of the caller.</summary>
    string? UserKey { get; }
    /// <summary>
    /// The caller's display name (<c>nom + prénom</c>). The legacy audit tables store this rather than
    /// the key, so it is what gets written to <c>utilisateur</c>.
    /// </summary>
    string? UserDisplayName { get; }
    /// <summary>Workstation name; mirrors the legacy <c>poste_utilisateur</c>.</summary>
    string? Workstation { get; }
}

/// <summary>Generates the application-side keys the legacy code obtains from <c>dbo.GetKeyApp</c>.</summary>
/// <remarks>
/// The legacy numbering scheme lives entirely inside a SQL Server scalar function that is not in the
/// repository, so its format (prefix, padding, per-company behaviour) is unknown. This abstraction is
/// the seam: the default implementation produces migration-safe keys today, and can be replaced with a
/// faithful port once the function body is extracted from the live database.
/// </remarks>
public interface IKeyGenerator
{
    Task<string> NextAsync(string operation, CancellationToken cancellationToken = default);
}

/// <summary>Append-only audit trail writer (legacy <c>tools.Trace</c>).</summary>
public interface IAuditTrail
{
    /// <summary>
    /// Records an entity event. <paramref name="statutCode"/> is the legacy <c>$</c>-prefixed code
    /// (e.g. <c>$ACLT</c>, <c>$CVOY</c>).
    /// </summary>
    Task TraceAsync<TTrace>(
        string entityKey,
        string? statutCode,
        string? commentaire,
        string? keyDocument = null,
        double valeur = 0,
        CancellationToken cancellationToken = default)
        where TTrace : TraceEntry;

    /// <summary>Records a master-data change (legacy <c>tools.JournalReferentiel</c>).</summary>
    Task JournalAsync(
        string programme,
        string evenement,
        string? commentaire,
        CancellationToken cancellationToken = default);
}

/// <summary>Query surface for clients. Kept explicit rather than generic so the legacy
/// "space-insensitive uniqueness" checks can be expressed in SQL.</summary>
public interface IClientRepository
{
    Task<Client?> GetAsync(string keyClient, bool includeChildren, CancellationToken cancellationToken = default);
    /// <summary>Returns at most two exact reference matches in the current tenant, to detect ambiguity.</summary>
    Task<IReadOnlyList<Client>> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);
    Task<Client?> GetByRaisonSocialeAsync(string raisonSociale, CancellationToken cancellationToken = default);
    Task<bool> CompteComptableExistsAsync(string compteComptable, string? excludingClient, CancellationToken cancellationToken = default);
    Task<PagedResult<Client>> SearchAsync(
        string? search, string? statutClient, string? classeClient, bool? actif,
        int page, int pageSize, CancellationToken cancellationToken = default, bool? valide = null);
    /// <summary>Reports which tables still reference this client — the legacy delete guard.</summary>
    Task<IReadOnlyList<string>> FindReferencesAsync(string keyClient, CancellationToken cancellationToken = default);
    void Add(Client client);
    void Add(AdresseClient adresse);
    void Add(BanqueTiers banque);
    void Remove(Client client);
    void Remove(AdresseClient adresse);
    void Remove(BanqueTiers banque);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Query surface for payments.</summary>
public interface IReglementRepository
{
    Task<EnteteReglement?> GetAsync(string keyReglement, bool includeAllocations, CancellationToken cancellationToken = default);
    Task<PagedResult<EnteteReglement>> SearchAsync(
        Domain.Enums.TypeReglement? type, string? keyTiers, Domain.Enums.StatutReglement? statut,
        DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default, string? search = null, string? tiers = null);
    /// <summary>Open advances eligible for lettrage, mirroring the legacy lettrable-advance predicate.</summary>
    Task<IReadOnlyList<EnteteReglement>> GetLettrableAvancesAsync(string? keyTiers, string devise, CancellationToken cancellationToken = default);
    void Add(EnteteReglement reglement);
    void Add(FraisBancaire frais);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Query surface for sales invoices.</summary>
public interface IFactureVenteRepository
{
    Task<EnteteFactureVente?> GetAsync(string keyFacture, bool includeLines, CancellationToken cancellationToken = default);
    Task<PagedResult<EnteteFactureVente>> SearchAsync(
        string? keyClient, TypeFacture? type, string? statut, bool? factRegle,
        DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default, string? search = null);
    /// <summary>Open (unsettled) invoices for a client, used to drive allocation.</summary>
    Task<IReadOnlyList<EnteteFactureVente>> GetOpenInvoicesAsync(string keyClient, CancellationToken cancellationToken = default);
    Task<bool> NumFactureExistsAsync(string numFacture, CancellationToken cancellationToken = default);
    void Add(EnteteFactureVente facture);
    void Add(RepartitionDossier repartition);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Query surface for reference data, generic over the entity type.</summary>
public interface IReferenceDataRepository
{
    Task<IReadOnlyList<T>> ListAsync<T>(CancellationToken cancellationToken = default) where T : class;
    Task<T?> FindAsync<T>(params object[] keyValues) where T : class;
    Task<bool> AnyAsync<T>(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) where T : class;
    void Add<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Persistence unit-of-work used by services that span several repositories.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
