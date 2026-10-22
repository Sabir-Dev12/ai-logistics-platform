using System.Security.Cryptography;
using Logistics.Application.Abstractions;
using Logistics.Application.Features.Bookings;
using Logistics.Domain.Entities.Bookings;
using Logistics.Domain.Entities.Edi;
using Logistics.Domain.Entities.ReferenceData;
using Logistics.Domain.Entities.Quotations;
using Logistics.Domain.Common;
using Logistics.Domain.Entities.Resources;
using Logistics.Application.Features.Quotations;
using Logistics.Application.Features.Authentication;
using Logistics.Application.Features.Edi;
using Logistics.Application.Features.Resources;
using Logistics.Application.Features.ReferenceData;
using Logistics.Application.Features.Shipments;
using Logistics.Application.Features.Tracking;
using Logistics.Domain.Entities.Audit;
using Logistics.Domain.Entities.Clients;
using Logistics.Domain.Entities.Invoicing;
using Logistics.Domain.Entities.Payments;
using Logistics.Domain.Entities.Shipments;
using Logistics.Domain.Enums;
using Logistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Logistics.Infrastructure;

/// <summary>
/// Resolves the ambient tenant/user for the current request.
/// </summary>
/// <remarks>
/// The legacy application builds a mutable global (<c>Constante</c>) once at login and every SQL
/// statement reads it. That is unsafe under concurrency, so the same facts are resolved per request
/// here. Until authentication is added they come from configuration, overridable per request through
/// headers so integration tests and API clients can switch company. Once JWT/claims land, only this
/// class changes.
/// </remarks>
/// <summary>
/// The authenticated caller for the current request, populated once by the authentication layer.
/// </summary>
/// <remarks>
/// Deliberately a plain scoped object rather than an HTTP abstraction: the tenant context then depends on
/// <em>what was proved</em> about the caller, not on where the value happened to come from. That is the
/// difference between reading a claim and reading a header.
/// </remarks>
public sealed class CallerContext
{
    /// <summary>The validated caller, or null when the request is unauthenticated.</summary>
    public CallerIdentity? Caller { get; set; }

    /// <summary>
    /// Whether headers may stand in for a token.
    /// </summary>
    /// <remarks>
    /// True only when explicitly enabled in Development. Anywhere else the answer is no, because the
    /// header path is exactly the hole this replaced.
    /// </remarks>
    public bool HeaderFallbackAllowed { get; set; }

    public string? HeaderSociete { get; set; }
    public string? HeaderAgence { get; set; }
    public string? HeaderUserKey { get; set; }
    public string? HeaderUserName { get; set; }
    public string? HeaderWorkstation { get; set; }
}

/// <summary>
/// Resolves the tenant from the <b>authenticated caller</b>.
/// </summary>
/// <remarks>
/// <para>
/// This replaced an implementation that read <c>X-Societe-Key</c> straight off the request. That version
/// let any caller name any company and read or write its data, which is a complete tenant-isolation
/// bypass — the global query filters downstream were only ever as trustworthy as this value.
/// </para>
/// <para>
/// The fallback to configuration exists so a developer can run the API without a token; it is gated on
/// <see cref="CallerContext.HeaderFallbackAllowed"/>, which is set from configuration and refused outright
/// outside Development. When it is off, an unauthenticated request cannot resolve a tenant at all and is
/// rejected rather than silently scoped to some default company.
/// </para>
/// </remarks>
public sealed class ClaimsTenantContext(CallerContext context, IConfiguration configuration) : ITenantContext
{
    public string SocieteKey =>
        context.Caller?.SocieteKey
        ?? Fallback("Tenant:SocieteKey", context.HeaderSociete, "X-Societe-Key")
        ?? throw new UnauthorizedAccessException(
            "No authenticated caller, so no company (tenant) could be resolved for this request.");

    public string? AgenceKey =>
        context.Caller?.AgenceKey ?? Fallback("Tenant:AgenceKey", context.HeaderAgence, "X-Agence-Key");

    public string? UserKey =>
        context.Caller?.UserKey ?? Fallback("Tenant:UserKey", context.HeaderUserKey, "X-User-Key");

    public string? UserDisplayName =>
        context.Caller?.UserDisplayName ?? Fallback("Tenant:UserDisplayName", context.HeaderUserName, "X-User-Name");

    public string? Workstation =>
        context.Caller?.Workstation ?? Fallback("Tenant:Workstation", context.HeaderWorkstation, "X-Workstation")
        ?? Environment.MachineName;

    /// <summary>
    /// Returns a development fallback value, or null when the fallback is not permitted.
    /// </summary>
    private string? Fallback(string configurationKey, string? headerValue, string headerName)
    {
        if (!context.HeaderFallbackAllowed)
        {
            return null;
        }

        // Best-effort audit of which requests took the insecure path.
        if (!string.IsNullOrWhiteSpace(headerValue))
        {
            Console.WriteLine($"[auth] WARNING: tenant resolved from the {headerName} header (development fallback).");
        }

        return headerValue ?? configuration[configurationKey];
    }
}

/// <summary>
/// Minimal abstraction over the HTTP context so the Application/Infrastructure layers stay testable
/// without a web host.
/// </summary>
public interface IHttpContextAccessorLike
{
    string? GetHeader(string name);
}

/// <summary>
/// Default <see cref="IKeyGenerator"/>.
/// </summary>
/// <remarks>
/// <b>Placeholder.</b> The legacy application obtains every surrogate key from the SQL Server scalar
/// function <c>dbo.GetKeyApp(@operation)</c>, whose body is not present anywhere in the legacy source
/// tree. Its prefix scheme, zero-padding width and per-company counter behaviour are therefore
/// unknown. This implementation produces unique, sortable, 20-character keys so the API is usable
/// today; it must be replaced by a faithful port (or by calling the existing DB function) before the
/// backend takes over from the desktop application, otherwise existing numbering expectations —
/// including the formatted document numbers produced by <c>tools.getCompteur</c> — will not be met.
/// </remarks>
public sealed class PlaceholderKeyGenerator : IKeyGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public Task<string> NextAsync(string operation, CancellationToken cancellationToken = default)
    {
        var prefix = new string(operation
            .Where(char.IsLetterOrDigit)
            .Take(3)
            .Select(char.ToUpperInvariant)
            .ToArray());

        // 12 Base32 characters of entropy (60 bits) — collision-free in practice and stable in length.
        Span<byte> buffer = stackalloc byte[8];
        RandomNumberGenerator.Fill(buffer);
        var value = BitConverter.ToUInt64(buffer);
        var suffix = new char[12];

        for (var i = 0; i < suffix.Length; i++)
        {
            suffix[i] = Alphabet[(int)(value % (ulong)Alphabet.Length)];
            value /= (ulong)Alphabet.Length;
        }

        return Task.FromResult($"{prefix}{new string(suffix)}");
    }
}

/// <summary>
/// Writes the legacy audit tables.
/// </summary>
/// <remarks>
/// Deliberately fail-safe: a failure to write an audit row must never fail the business operation, and
/// must never recurse. The legacy <c>InsertEventApp</c> calls itself from its own <c>Catch</c> block,
/// which stack-overflows when the log table is unreachable.
/// </remarks>
public sealed class AuditTrail(LogisticsDbContext db, ITenantContext tenant, ILogger<AuditTrail> logger) : IAuditTrail
{
    public async Task TraceAsync<TTrace>(
        string entityKey, string? statutCode, string? commentaire, string? keyDocument = null,
        double valeur = 0, CancellationToken cancellationToken = default)
        where TTrace : TraceEntry
    {
        try
        {
            var now = DateTime.UtcNow;

            var entry = (TTrace)Activator.CreateInstance(
                typeof(TTrace),
                entityKey, tenant.SocieteKey, tenant.AgenceKey, statutCode,
                now, now, commentaire, tenant.UserDisplayName, keyDocument, valeur, tenant.Workstation)!;

            db.Set<TTrace>().Add(entry);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Audit trace {Table} could not be written for {Key}.", typeof(TTrace).Name, entityKey);
        }
    }

    public async Task JournalAsync(
        string programme, string evenement, string? commentaire, CancellationToken cancellationToken = default)
    {
        try
        {
            db.HistReferentiel.Add(new HistReferentiel(
                programme, evenement, tenant.UserDisplayName, DateTime.UtcNow, commentaire,
                tenant.SocieteKey, tenant.Workstation));

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Referential journal entry for {Programme} could not be written.", programme);
        }
    }
}

public sealed class ClientRepository(LogisticsDbContext db) : IClientRepository
{
    public async Task<IReadOnlyList<Client>> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        await db.Clients.AsNoTracking().Where(c => c.RefClient == reference)
            .OrderBy(c => c.Id).Take(2).ToListAsync(cancellationToken);

    public Task<Client?> GetAsync(string keyClient, bool includeChildren, CancellationToken cancellationToken = default)
    {
        var query = db.Clients.AsQueryable();

        if (includeChildren)
        {
            query = query.Include(c => c.Adresses).Include(c => c.Banques);
        }

        return query.SingleOrDefaultAsync(c => c.Id == keyClient, cancellationToken);
    }

    /// <summary>
    /// Uniqueness on the company name ignores spaces and case, exactly as the legacy
    /// <c>replace(raison_sociale,' ','')</c> check does.
    /// </summary>
    public Task<Client?> GetByRaisonSocialeAsync(string raisonSociale, CancellationToken cancellationToken = default)
    {
        var needle = Normalise(raisonSociale);

        return db.Clients
            .AsNoTracking()
            .Where(c => c.RaisonSociale.Replace(" ", string.Empty).ToUpper() == needle)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> CompteComptableExistsAsync(string compteComptable, string? excludingClient, CancellationToken cancellationToken = default) =>
        db.Clients.AnyAsync(
            // `Id` is the mapped key; the `KeyClient` convenience property is unmapped and cannot be
            // translated by EF, so queries must always use `Id`.
            c => c.CompteComptable == compteComptable && (excludingClient == null || c.Id != excludingClient),
            cancellationToken);

    public async Task<PagedResult<Client>> SearchAsync(
        string? search, string? statutClient, string? classeClient, bool? actif,
        int page, int pageSize, CancellationToken cancellationToken = default, bool? valide = null)
    {
        var query = BuildSearchQuery(db.Clients.AsNoTracking(), search, statutClient, classeClient, actif, valide);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(c => c.RaisonSociale).Skip((page - 1) * pageSize)
            .Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Client>(items, page, pageSize, total);
    }

    internal static IQueryable<Client> BuildSearchQuery(IQueryable<Client> query,
        string? search, string? statutClient, string? classeClient, bool? actif, bool? valide = null)
    {
        if (valide.HasValue) query = query.Where(c => c.Valide == valide.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            query = query.Where(c => c.RaisonSociale.Contains(needle)
                || (c.RefClient != null && c.RefClient.Contains(needle))
                || c.Id.Contains(needle));
        }

        if (!string.IsNullOrWhiteSpace(statutClient))
        {
            query = query.Where(c => c.StatutClient == statutClient);
        }

        if (!string.IsNullOrWhiteSpace(classeClient))
        {
            query = query.Where(c => c.ClasseClient == classeClient);
        }

        if (actif.HasValue)
        {
            query = query.Where(c => c.Actif == actif.Value);
        }

        return query;
    }

    /// <summary>
    /// Reproduces the legacy delete guard. Only the tables that are part of this increment are
    /// checked; the remaining legacy references (contestations, relances, exonérations, taux de
    /// change client) are added as those modules are migrated.
    /// </summary>
    public async Task<IReadOnlyList<string>> FindReferencesAsync(string keyClient, CancellationToken cancellationToken = default)
    {
        var referencing = new List<string>();

        if (await db.EntetesFactureVente.AnyAsync(f => f.KeyClient == keyClient, cancellationToken))
        {
            referencing.Add("t_entete_facture_vente");
        }

        if (await db.EntetesReglement.AnyAsync(r => r.KeyTiers == keyClient, cancellationToken))
        {
            referencing.Add("t_entete_reglement");
        }

        // A client referenced by a dossier as client, payeur or expediteur cannot be removed either;
        // the legacy guard checks both key_client and key_payeur.
        if (await db.Dossiers.AnyAsync(
                d => d.KeyClient == keyClient || d.KeyPayeur == keyClient, cancellationToken))
        {
            referencing.Add("t_dossiers");
        }

        if (await db.ContactsTiers.AnyAsync(c => c.TiersContactKey == keyClient && c.TypeTiers == TypeTiers.Client, cancellationToken))
        {
            referencing.Add("t_contact_tiers");
        }

        if (await db.BanquesTiers.AnyAsync(b => b.KeyTier == keyClient && b.TypeTier == TypeTiers.Client, cancellationToken))
        {
            referencing.Add("t_banque_tiers");
        }

        return referencing;
    }

    public void Add(Client client) => db.Clients.Add(client);
    public void Add(AdresseClient adresse) => db.AdressesClient.Add(adresse);
    public void Add(BanqueTiers banque) => db.BanquesTiers.Add(banque);
    public void Remove(Client client) => db.Remove(client);
    public void Remove(AdresseClient adresse) => db.Remove(adresse);
    public void Remove(BanqueTiers banque) => db.Remove(banque);
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);

    private static string Normalise(string value) => value.Replace(" ", string.Empty).ToUpperInvariant();
}

public sealed class ReglementRepository(LogisticsDbContext db) : IReglementRepository
{
    public Task<EnteteReglement?> GetAsync(string keyReglement, bool includeAllocations, CancellationToken cancellationToken = default)
    {
        var query = db.EntetesReglement.AsQueryable();

        if (includeAllocations)
        {
            query = query.Include(r => r.Allocations);
        }

        return query.SingleOrDefaultAsync(r => r.Id == keyReglement, cancellationToken);
    }

    public async Task<PagedResult<EnteteReglement>> SearchAsync(
        TypeReglement? type, string? keyTiers, StatutReglement? statut, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken cancellationToken = default, string? search = null, string? tiers = null)
    {
        var query = ApplyTextFilters(db.EntetesReglement.AsNoTracking(), db.Clients.AsNoTracking(), db.Personnel.AsNoTracking(), search, tiers);

        if (type.HasValue)
        {
            query = query.Where(r => r.TypeReglement == type.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyTiers))
        {
            query = query.Where(r => r.KeyTiers == keyTiers);
        }

        if (statut.HasValue)
        {
            query = query.Where(r => r.Statut == statut.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(r => r.DateOperation >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(r => r.DateOperation <= to.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.DateOperation)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<EnteteReglement>(items, page, pageSize, total);
    }

    internal static IQueryable<EnteteReglement> ApplyTextFilters(IQueryable<EnteteReglement> query,
        IQueryable<Client> clients, IQueryable<Personnel> personnel, string? search, string? tiers)
    {
        foreach (var filter in new[] { (Text: search, Payment: true), (Text: tiers, Payment: false) })
        {
            if (string.IsNullOrWhiteSpace(filter.Text)) continue;
            var needle = filter.Text.Trim();
            var includePayment = filter.Payment;
            // These subqueries retain the directory's tenant and deletion filters.
            // Type guards prevent a supplier key from matching an unrelated client.
            var clientKeys = clients.Where(c => c.RaisonSociale.Contains(needle) || (c.RefClient != null && c.RefClient.Contains(needle))).Select(c => c.Id);
            var employeeKeys = personnel.Where(p => ((p.NomPersonnel ?? "") + " " + (p.PrenomPersonnel ?? "")).Contains(needle)).Select(p => p.Id);
            query = query.Where(r => (includePayment && (r.Id.Contains(needle) || (r.ReferencePaiement != null && r.ReferencePaiement.Contains(needle))))
                || (r.KeyTiers != null && r.KeyTiers.Contains(needle))
                || (r.NomTier != null && r.NomTier.Contains(needle))
                || (r.TypeReglement == TypeReglement.Client && clientKeys.Contains(r.KeyTiers!))
                || (r.TypeReglement == TypeReglement.Personnel && employeeKeys.Contains(r.KeyTiers!)));
        }
        return query;
    }

    /// <summary>
    /// Mirrors the legacy lettrable-advance predicate: an AVANCE is lettrable while it is not deleted,
    /// its lettrage is not yet validated, it still has an unallocated remainder, and — crucially — its
    /// currency matches the invoice's.
    /// </summary>
    public async Task<IReadOnlyList<EnteteReglement>> GetLettrableAvancesAsync(
        string? keyTiers, string devise, CancellationToken cancellationToken = default)
    {
        var currency = devise.ToUpperInvariant();

        return await db.EntetesReglement
            .AsNoTracking()
            .Where(r => r.NatureReglement == NatureReglement.Avance
                && !r.ValidationLettrage
                && r.Devise == currency
                && r.MontantDevise - r.MontantImputeAvance > 0
                && (keyTiers == null || r.KeyTiers == keyTiers))
            .OrderBy(r => r.DateOperation)
            .ToListAsync(cancellationToken);
    }

    public void Add(EnteteReglement reglement) => db.EntetesReglement.Add(reglement);
    public void Add(FraisBancaire frais) => db.FraisBancaires.Add(frais);
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}

public sealed class FactureVenteRepository(LogisticsDbContext db) : IFactureVenteRepository
{
    public Task<EnteteFactureVente?> GetAsync(string keyFacture, bool includeLines, CancellationToken cancellationToken = default)
    {
        var query = db.EntetesFactureVente.AsQueryable();

        if (includeLines)
        {
            query = query.Include(f => f.Lignes).Include(f => f.TvaDetail);
        }

        return query.SingleOrDefaultAsync(f => f.Id == keyFacture, cancellationToken);
    }

    public async Task<PagedResult<EnteteFactureVente>> SearchAsync(
        string? keyClient, TypeFacture? type, string? statut, bool? factRegle, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken cancellationToken = default, string? search = null)
    {
        var query = ApplySearchFilter(db.EntetesFactureVente.AsNoTracking(), search);

        if (!string.IsNullOrWhiteSpace(keyClient))
        {
            query = query.Where(f => f.KeyClient == keyClient);
        }

        if (type.HasValue)
        {
            query = query.Where(f => f.TypeFacture == type.Value);
        }

        if (!string.IsNullOrWhiteSpace(statut))
        {
            query = query.Where(f => f.StatutFacture == statut);
        }

        if (factRegle.HasValue)
        {
            query = query.Where(f => f.FactRegle == factRegle.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(f => f.DateFacture >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(f => f.DateFacture <= to.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(f => f.DateFacture)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<EnteteFactureVente>(items, page, pageSize, total);
    }

    internal static IQueryable<EnteteFactureVente> ApplySearchFilter(IQueryable<EnteteFactureVente> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var needle = search.Trim();
        return query.Where(f => f.Id.Contains(needle)
            || (f.NumFacture != null && f.NumFacture.Contains(needle))
            || (f.IntituleClient != null && f.IntituleClient.Contains(needle)));
    }

    public async Task<IReadOnlyList<EnteteFactureVente>> GetOpenInvoicesAsync(string keyClient, CancellationToken cancellationToken = default) =>
        await db.EntetesFactureVente
            .AsNoTracking()
            .Where(f => f.KeyClient == keyClient && !f.FactRegle)
            .OrderBy(f => f.DateEcheance)
            .ToListAsync(cancellationToken);

    public Task<bool> NumFactureExistsAsync(string numFacture, CancellationToken cancellationToken = default) =>
        db.EntetesFactureVente.AnyAsync(f => f.NumFacture == numFacture, cancellationToken);

    public void Add(EnteteFactureVente facture) => db.EntetesFactureVente.Add(facture);
    public void Add(RepartitionDossier repartition) => db.RepartitionsDossier.Add(repartition);
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// Generic reference-data access. Reference tables are small and read-mostly, so a single generic
/// repository replaces what were ~50 hand-written <c>Class*.vb</c> files in the legacy application.
/// </summary>
public sealed class ReferenceDataRepository(LogisticsDbContext db) : IReferenceDataRepository
{
    public async Task<IReadOnlyList<T>> ListAsync<T>(CancellationToken cancellationToken = default) where T : class =>
        await db.Set<T>().AsNoTracking().ToListAsync(cancellationToken);

    public async Task<T?> FindAsync<T>(params object[] keyValues) where T : class =>
        await db.Set<T>().FindAsync(keyValues);

    public Task<bool> AnyAsync<T>(
        System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        where T : class =>
        db.Set<T>().AnyAsync(predicate, cancellationToken);

    public void Add<T>(T entity) where T : class => db.Set<T>().Add(entity);
    public void Remove<T>(T entity) where T : class => db.Set<T>().Remove(entity);
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}

public sealed class UnitOfWork(LogisticsDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// Reproduces the legacy <c>Referencee()</c> guards.
/// </summary>
/// <remarks>
/// <para>
/// The legacy guards were inconsistent and occasionally broken: <c>ClassPort.Referencee</c> was
/// commented out, <c>ClassAutreSortie.Referencee</c> was an empty stub returning <c>false</c>, and
/// <c>ClassPays</c> had none at all — so the desktop application would happily orphan client rows by
/// deleting a country. Every guard below is therefore an <em>improvement</em> on the legacy behaviour
/// rather than a faithful copy, and the extra checks are documented in the README's "fixes for defects
/// found in the legacy code" section.
/// </para>
/// <para>
/// Only tables mapped by this backend are checked. The remaining legacy references (contestations,
/// relances, exonérations, taux de change client) join the list as those modules are migrated.
/// </para>
/// </remarks>
public sealed class ReferenceGuardRepository(LogisticsDbContext db) : IReferenceGuardRepository
{
    public async Task<IReadOnlyList<string>> FindReferencesAsync(
        ReferentialKind kind, string key, CancellationToken cancellationToken = default)
    {
        var referencing = new List<string>();

        switch (kind)
        {
            case ReferentialKind.Pays:
                if (await db.Clients.AnyAsync(c => c.PaysClient == key, cancellationToken)) Add("t_clients");
                if (await db.Villes.AnyAsync(v => v.PaysVille == key, cancellationToken)) Add("t_ville");
                if (await db.Dossiers.AnyAsync(d => d.PaysExpediteur == key || d.PaysDestinataire == key, cancellationToken)) Add("t_dossiers");
                break;

            case ReferentialKind.Devise:
                if (await db.Clients.AnyAsync(c => c.DeviseFacturation == key, cancellationToken)) Add("t_clients");
                if (await db.TauxChanges.AnyAsync(t => t.Devise == key, cancellationToken)) Add("t_taux_change");
                if (await db.EntetesReglement.AnyAsync(r => r.Devise == key, cancellationToken)) Add("t_entete_reglement");
                if (await db.EntetesFactureVente.AnyAsync(f => f.DeviseFacture == key || f.DeviseReg == key, cancellationToken)) Add("t_entete_facture_vente");
                if (await db.Dossiers.AnyAsync(d => d.DeviseFret == key, cancellationToken)) Add("t_dossiers");
                break;

            case ReferentialKind.Ville:
                if (await db.Clients.AnyAsync(c => c.VilleClient == key, cancellationToken)) Add("t_clients");
                if (await db.Agences.AnyAsync(a => a.VilleAgence == key, cancellationToken)) Add("t_agence");
                if (await db.Dossiers.AnyAsync(d => d.VilleExpediteur == key || d.VilleDestinataire == key, cancellationToken)) Add("t_dossiers");
                break;

            case ReferentialKind.Port:
            {
                // t_voyages stores port labels, not t_port keys, so the guard has to resolve the label
                // — the same asymmetry the legacy statistic queries relied on.
                var port = await db.Ports.AsNoTracking().FirstOrDefaultAsync(p => p.Id == key, cancellationToken);
                if (port?.LibellePort is { Length: > 0 } libelle)
                {
                    if (await db.Voyages.AnyAsync(v => v.PortDepart == libelle || v.PortArrivee == libelle, cancellationToken)) Add("t_voyages");
                }

                break;
            }

            case ReferentialKind.Incoterm:
                if (await db.Dossiers.AnyAsync(d => d.Incoterm == key, cancellationToken)) Add("t_dossiers");
                break;

            case ReferentialKind.ZonePays:
                if (await db.Pays.AnyAsync(p => p.ZonePaysKey == key, cancellationToken)) Add("t_pays");
                break;

            case ReferentialKind.Agence:
                if (await db.Voyages.AnyAsync(v => v.KeyAgenceExploitation == key, cancellationToken)) Add("t_voyages");
                if (await db.Dossiers.AnyAsync(d => d.KeyAgenceExploitation == key, cancellationToken)) Add("t_dossiers");
                if (await db.EntetesReglement.AnyAsync(r => r.AgenceReglement == key, cancellationToken)) Add("t_entete_reglement");
                if (await db.Tresoreries.AnyAsync(t => t.AgenceTresorie == key, cancellationToken)) Add("t_tresorie");
                break;

            case ReferentialKind.ModePaiement:
                if (await db.Clients.AnyAsync(c => c.PaiementClient == key, cancellationToken)) Add("t_clients");
                if (await db.EntetesReglement.AnyAsync(r => r.ModePaiement == key, cancellationToken)) Add("t_entete_reglement");
                break;

            case ReferentialKind.CompteTva:
                // Keyed by (code_tva, tva_soc); the code alone is enough to detect use.
                if (await db.DetailsFactureVente.AnyAsync(l => l.CodeTva == key, cancellationToken)) Add("T_DETAIL_FACTURE_VENTE");
                if (await db.TvaFactureVente.AnyAsync(t => t.CodeTva == key, cancellationToken)) Add("T_TVA_FACTURE_VENTE");
                break;

            case ReferentialKind.ClasseClient:
                if (await db.Clients.AnyAsync(c => c.ClasseClient == key, cancellationToken)) Add("t_clients");
                break;

            case ReferentialKind.StatutClient:
                if (await db.Clients.AnyAsync(c => c.StatutClient == key, cancellationToken)) Add("t_clients");
                break;

            case ReferentialKind.Risque:
                if (await db.Clients.AnyAsync(c => c.RisqueClient == key, cancellationToken)) Add("t_clients");
                break;

            case ReferentialKind.SecteurActivite:
                if (await db.Clients.AnyAsync(c => c.ActiviteClient == key, cancellationToken)) Add("t_clients");
                break;

            case ReferentialKind.GroupeSociete:
                if (await db.Clients.AnyAsync(c => c.GroupeClient == key, cancellationToken)) Add("t_clients");
                break;

            case ReferentialKind.Tresorie:
                if (await db.EntetesReglement.AnyAsync(r => r.KeyTresorie == key, cancellationToken)) Add("t_entete_reglement");
                if (await db.FraisBancaires.AnyAsync(f => f.KeyTresorie == key, cancellationToken)) Add("T_FRAIS_BANCAIRES");
                if (await db.EntetesRemise.AnyAsync(r => r.KeyTresorie == key, cancellationToken)) Add("t_entete_remise");
                break;

            case ReferentialKind.NatureFraisBancaire:
                if (await db.FraisBancaires.AnyAsync(f => f.NatureFrais == key, cancellationToken)) Add("T_FRAIS_BANCAIRES");
                break;

            case ReferentialKind.TypeIncidentPaiement:
                if (await db.EntetesReglement.AnyAsync(r => r.MotifImpaye == key, cancellationToken)) Add("t_entete_reglement");
                break;

            default:
                break;
        }

        return referencing;

        void Add(string table)
        {
            if (!referencing.Contains(table, StringComparer.Ordinal))
            {
                referencing.Add(table);
            }
        }
    }

    public Task<int> CountAgencesAsync(CancellationToken cancellationToken = default) =>
        db.Agences.CountAsync(cancellationToken);
}

/// <summary>
/// Booking persistence.
/// </summary>
/// <remarks>
/// Tenant scoping is explicit (<c>t_booking.societe</c>) because the soft-delete query filter already
/// occupies the single <c>HasQueryFilter</c> slot for this entity.
/// </remarks>
public sealed class BookingRepository(LogisticsDbContext db, ITenantContext tenant) : IBookingRepository
{
    public async Task<Booking?> FindAsync(string keyBooking, bool includeChildren, CancellationToken cancellationToken = default)
    {
        var query = db.Bookings.Where(b => b.SocieteKey == tenant.SocieteKey);

        if (includeChildren)
        {
            query = query
                .Include(b => b.Colisages)
                .Include(b => b.Equipements);
        }

        return await query.FirstOrDefaultAsync(b => b.Id == keyBooking, cancellationToken);
    }

    public async Task<PagedResult<BookingSummaryDto>> SearchAsync(BookingSearchRequest r, CancellationToken cancellationToken = default)
    {
        var societe = tenant.SocieteKey;

        // A soft-deleted booking still occupies its number, so the "include" view drops the filter.
        var source = r.IncludeSupprimes ? db.Bookings.IgnoreQueryFilters() : db.Bookings;
        var query = source.AsNoTracking().Where(b => b.SocieteKey == societe);

        if (!string.IsNullOrWhiteSpace(r.KeyClient)) query = query.Where(b => b.KeyClient == r.KeyClient);
        if (!string.IsNullOrWhiteSpace(r.KeyStatutBooking)) query = query.Where(b => b.KeyStatutBooking == r.KeyStatutBooking);
        if (!string.IsNullOrWhiteSpace(r.TypeBooking)) query = query.Where(b => b.TypeBooking == r.TypeBooking);
        if (!string.IsNullOrWhiteSpace(r.KeyAgence)) query = query.Where(b => b.AgenceBooking == r.KeyAgence);
        if (r.DateCreationDu.HasValue) query = query.Where(b => b.DateCreation >= r.DateCreationDu.Value);
        if (r.DateCreationAu.HasValue) query = query.Where(b => b.DateCreation <= r.DateCreationAu.Value);

        if (!string.IsNullOrWhiteSpace(r.NumBooking))
        {
            var num = r.NumBooking.Trim();
            query = query.Where(b => b.NumBooking != null && b.NumBooking.Contains(num));
        }

        if (r.NonAffectes == true)
        {
            query = query.Where(b => !db.DetailsVoyageBooking.Any(l => l.KeyBooking == b.KeyBooking));
        }

        if (!string.IsNullOrWhiteSpace(r.KeyVoyage))
        {
            var keyVoyage = r.KeyVoyage;
            query = query.Where(b => db.DetailsVoyageBooking.Any(l => l.KeyVoyage == keyVoyage && l.KeyBooking == b.KeyBooking));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(b => b.DateCreation)
            .ThenBy(b => b.Id)
            .Skip((r.Page - 1) * r.PageSize)
            .Take(r.PageSize)
            .Select(b => new BookingSummaryDto(
                b.Id, b.NumBooking!, b.TypeBooking, b.TypeChargement, b.IntituleClient, b.IntituleAgenceBooking,
                b.DateCreation, b.DateValidite, b.KeyStatutBooking, b.KeyStatutBookingMead,
                b.KeyDossier, b.KeyVoyagePrevisionnel, b.KeyVoyageEnlevement, b.VolumePrevision,
                db.EquipementsBooking.Where(e => e.KeyBooking == b.Id).Sum(e => (decimal?)e.Qte) ?? 0m,
                b.Supprime))
            .ToListAsync(cancellationToken);

        return new PagedResult<BookingSummaryDto>(items, r.Page, r.PageSize, total);
    }

    /// <summary>
    /// Whether the number is taken anywhere in the installation.
    /// </summary>
    /// <remarks>
    /// No tenant predicate and no soft-delete filter: the legacy check is
    /// <c>where replace(num_booking,' ','') = …</c> over the whole table, so numbers are globally
    /// unique and a deleted booking keeps its own. Spaces are stripped on both sides because the
    /// legacy comparison did the same.
    /// </remarks>
    public async Task<bool> NumBookingExistsAsync(string numBooking, string? excludeKeyBooking = null, CancellationToken cancellationToken = default)
    {
        var normalise = numBooking.Replace(" ", string.Empty);

        return await db.Bookings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(b => b.NumBooking != null
                && b.NumBooking.Replace(" ", string.Empty) == normalise
                && (excludeKeyBooking == null || b.Id != excludeKeyBooking), cancellationToken);
    }

    public Task<TypeDossier?> FindTypeDossierAsync(string typeDossier, CancellationToken cancellationToken = default) =>
        db.TypesDossier.AsNoTracking().FirstOrDefaultAsync(t => t.Id == typeDossier, cancellationToken);

    /// <summary>
    /// Sum of the <b>forecast</b> volume over a voyage's bookings.
    /// </summary>
    /// <remarks>
    /// The legacy capacity query is
    /// <c>sum(isnull(t_booking.volume_prevision,0))</c> joined through <c>t_detail_voyage_booking</c>.
    /// Using the actual <c>volume</c> instead would under-report until dépotage.
    /// </remarks>
    public async Task<decimal> SumVolumePrevisionAsync(string keyVoyage, CancellationToken cancellationToken = default) =>
        await db.DetailsVoyageBooking
            .Where(l => l.KeyVoyage == keyVoyage)
            .Join(db.Bookings, l => l.KeyBooking, b => b.Id, (l, b) => b.VolumePrevision)
            .SumAsync(v => v ?? 0m, cancellationToken);

    public Task<int> CountBookingsAsync(string keyVoyage, CancellationToken cancellationToken = default) =>
        db.DetailsVoyageBooking.CountAsync(l => l.KeyVoyage == keyVoyage, cancellationToken);

    public async Task<string?> FindLinkedVoyageAsync(string keyBooking, CancellationToken cancellationToken = default) =>
        await db.DetailsVoyageBooking
            .AsNoTracking()
            .Where(l => l.KeyBooking == keyBooking)
            .Select(l => l.KeyVoyage)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> IsLinkedAsync(string keyVoyage, string keyBooking, CancellationToken cancellationToken = default) =>
        db.DetailsVoyageBooking.AnyAsync(l => l.KeyVoyage == keyVoyage && l.KeyBooking == keyBooking, cancellationToken);

    public void Add(Booking booking) => db.Bookings.Add(booking);
    public void Add(ColisageBooking colisage) => db.ColisagesBooking.Add(colisage);
    public void Add(EquipementBooking equipement) => db.EquipementsBooking.Add(equipement);
    public void Add(DetailVoyageBooking lien) => db.DetailsVoyageBooking.Add(lien);
    public void Remove(ColisageBooking colisage) => db.ColisagesBooking.Remove(colisage);
    public void Remove(EquipementBooking equipement) => db.EquipementsBooking.Remove(equipement);
    public void Remove(DetailVoyageBooking lien) => db.DetailsVoyageBooking.Remove(lien);

    public async Task RemoveAsync(string keyVoyage, string keyBooking, CancellationToken cancellationToken = default)
    {
        var lien = await db.DetailsVoyageBooking
            .FirstOrDefaultAsync(l => l.KeyVoyage == keyVoyage && l.KeyBooking == keyBooking, cancellationToken);

        if (lien is not null)
        {
            db.DetailsVoyageBooking.Remove(lien);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// Quotation persistence.
/// </summary>
/// <remarks>
/// Tenant scoping is explicit. Note that <c>Cotation</c> maps <see cref="ITenantScoped.SocieteKey"/> to
/// <c>soc_cotation</c> while <c>PrixCotation</c> maps it to plain <c>societe</c>, so the same predicate
/// compiles to different columns per entity — which is why both are filtered through the entity
/// property rather than a shared string.
/// </remarks>
public sealed class QuotationRepository(LogisticsDbContext db, ITenantContext tenant) : IQuotationRepository
{
    public async Task<Cotation?> FindAsync(string keyCotation, bool includeChildren, CancellationToken cancellationToken = default)
    {
        var query = db.Cotations.Where(c => c.SocieteKey == tenant.SocieteKey);

        if (includeChildren)
        {
            query = query
                .Include(c => c.Colisages)
                .Include(c => c.PrixCotation)
                .Include(c => c.Achats)
                .Include(c => c.TauxChange)
                .Include(c => c.Documents);
        }

        return await query.FirstOrDefaultAsync(c => c.Id == keyCotation, cancellationToken);
    }

    public async Task<PagedResult<CotationSummaryDto>> SearchAsync(QuotationSearchRequest r, CancellationToken cancellationToken = default)
    {
        var societe = tenant.SocieteKey;
        var aujourdhui = DateTime.UtcNow.Date;

        var source = r.IncludeSupprimes ? db.Cotations.IgnoreQueryFilters() : db.Cotations;
        var query = source.AsNoTracking().Where(c => c.SocieteKey == societe);

        if (!string.IsNullOrWhiteSpace(r.KeyClient)) query = query.Where(c => c.KeyClient == r.KeyClient);
        if (!string.IsNullOrWhiteSpace(r.Statut)) query = query.Where(c => c.Statut == r.Statut);
        if (!string.IsNullOrWhiteSpace(r.ProduitCotation)) query = query.Where(c => c.ProduitCotation == r.ProduitCotation);
        if (!string.IsNullOrWhiteSpace(r.Commercial)) query = query.Where(c => c.Commercial == r.Commercial);
        if (r.DateDu.HasValue) query = query.Where(c => c.DateCotation >= r.DateDu.Value);
        if (r.DateAu.HasValue) query = query.Where(c => c.DateCotation <= r.DateAu.Value);

        if (!string.IsNullOrWhiteSpace(r.NumCotation))
        {
            var num = r.NumCotation.Trim();
            query = query.Where(c => c.NumCotation != null && c.NumCotation.Contains(num));
        }

        if (r.Convertibles == true)
        {
            // Only a won quotation still inside its validity window may become a booking or dossier.
            query = query.Where(c => c.Statut == Cotation.StatutGagnee
                && (c.DateValidite == null || c.DateValidite >= aujourdhui));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.DateCotation)
            .ThenBy(c => c.Id)
            .Skip((r.Page - 1) * r.PageSize)
            .Take(r.PageSize)
            .Select(c => new CotationSummaryDto(
                c.Id, c.NumCotation!, c.DateCotation, c.DateValidite, c.Statut, c.KeyClient,
                c.ProduitCotation, c.TypeTransport, c.Commercial, c.Concurrent, c.MotifCotationPerdue,
                c.Attachement, c.Supprime))
            .ToListAsync(cancellationToken);

        return new PagedResult<CotationSummaryDto>(items, r.Page, r.PageSize, total);
    }

    public Task<bool> NumCotationExistsAsync(string numCotation, string? excludeKeyCotation = null, CancellationToken cancellationToken = default)
    {
        var normalise = numCotation.Replace(" ", string.Empty);

        return db.Cotations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(c => c.NumCotation != null
                && c.NumCotation.Replace(" ", string.Empty) == normalise
                && (excludeKeyCotation == null || c.Id != excludeKeyCotation), cancellationToken);
    }

    public Task<bool> HasDocumentsAsync(string keyCotation, CancellationToken cancellationToken = default) =>
        db.DocumentsCotation.AsNoTracking().AnyAsync(d => d.KeyCotation == keyCotation, cancellationToken);

    /// <summary>
    /// Probes <c>t_frais_annexe_cotation</c> for rows.
    /// </summary>
    /// <remarks>
    /// The only raw SQL in the backend, and deliberately so. The schema extraction evidenced exactly one
    /// column of this table — <c>key_cotation</c>, from the single <c>WHERE</c> clause that uses it — so
    /// there is no honest entity to model. The sibling <c>t_frais_annexe</c> has a known shape, but the
    /// two have different guard semantics and there is no evidence they match, so guessing would be
    /// worse than a narrow, explicit probe.
    /// </remarks>
    public async Task<bool> HasFraisAnnexesAsync(string keyCotation, CancellationToken cancellationToken = default)
    {
        var count = await db.Database
            .SqlQuery<int>($"select count(*) as Value from t_frais_annexe_cotation where key_cotation = {keyCotation}")
            .SingleAsync(cancellationToken);

        return count > 0;
    }

    public Task<FranchiseCotation?> FindFranchiseAsync(string typeDossier, string natureFranchise, CancellationToken cancellationToken = default) =>
        db.FranchisesCotation
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.SocieteKey == tenant.SocieteKey
                && f.TypeDossier == typeDossier
                && f.NatureFranchise == natureFranchise, cancellationToken);

    public void Add(Cotation cotation) => db.Cotations.Add(cotation);
    public void Add(ColisageCotation colisage) => db.ColisagesCotation.Add(colisage);
    public void Add(PrixCotation prix) => db.PrixCotation.Add(prix);
    public void Add(AchatCotation achat) => db.AchatsCotation.Add(achat);
    public void Add(TauxChangeCotation taux) => db.TauxChangeCotation.Add(taux);
    public void Add(DocumentCotation document) => db.DocumentsCotation.Add(document);
    public void Remove(ColisageCotation colisage) => db.ColisagesCotation.Remove(colisage);
    public void Remove(PrixCotation prix) => db.PrixCotation.Remove(prix);
    public void Remove(AchatCotation achat) => db.AchatsCotation.Remove(achat);
    public void Remove(DocumentCotation document) => db.DocumentsCotation.Remove(document);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// Purchase tariff scale persistence.
/// </summary>
/// <remarks>
/// Nothing here is soft-deleted: the legacy "delete" archives every row into the <c>hist</c> tables and
/// flips the status, so the live tables stay authoritative and no query filter is needed.
/// </remarks>
public sealed class BaremeAchatRepository(LogisticsDbContext db, ITenantContext tenant) : IBaremeAchatRepository
{
    public async Task<BaremeAchatCotation?> FindAsync(string keyBareme, bool includeChildren, CancellationToken cancellationToken = default)
    {
        var query = db.BaremesAchatCotation.Where(b => b.SocieteKey == tenant.SocieteKey);

        if (includeChildren)
        {
            query = query
                .Include(b => b.Colonnes)
                .Include(b => b.Details)
                .Include(b => b.Prestations);
        }

        return await query.FirstOrDefaultAsync(b => b.Id == keyBareme, cancellationToken);
    }

    public async Task<PagedResult<BaremeAchatSummaryDto>> SearchAsync(BaremeAchatSearchRequest r, CancellationToken cancellationToken = default)
    {
        var societe = tenant.SocieteKey;
        var query = db.BaremesAchatCotation.AsNoTracking().Where(b => b.SocieteKey == societe);
        // Active listing by default; archived scales remain accessible through an explicit status filter.
        if (!r.Statut.HasValue) query = query.Where(b => b.Statut != StatutBareme.Supprime);

        if (!string.IsNullOrWhiteSpace(r.Libelle))
        {
            var libelle = r.Libelle.Trim();
            query = query.Where(b => b.LibelleBareme != null && b.LibelleBareme.Contains(libelle));
        }

        if (r.Statut.HasValue) query = query.Where(b => b.Statut == r.Statut.Value);
        if (r.TypeBareme.HasValue) query = query.Where(b => b.TypeBareme == r.TypeBareme.Value);
        if (r.Flux.HasValue) query = query.Where(b => b.Flux == r.Flux.Value);
        if (!string.IsNullOrWhiteSpace(r.Fournisseur)) query = query.Where(b => b.Fournisseur == r.Fournisseur);

        if (r.Modifiables == true)
        {
            // Only a validated scale may be edited, so this is the "editable" filter.
            query = query.Where(b => b.Statut == StatutBareme.Valide);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(b => b.LibelleBareme)
            .ThenBy(b => b.Id)
            .Skip((r.Page - 1) * r.PageSize)
            .Take(r.PageSize)
            .Select(b => new BaremeAchatSummaryDto(
                b.Id, b.LibelleBareme, b.TypeBareme.HasValue ? b.TypeBareme.Value.ToString() : null, b.Fournisseur,
                b.DebutValidite, b.FinValidite, b.Statut, b.Flux.HasValue ? b.Flux.Value.ToString() : null, b.Devise,
                b.Colonnes.Count, b.Prestations.Count))
            .ToListAsync(cancellationToken);

        return new PagedResult<BaremeAchatSummaryDto>(items, r.Page, r.PageSize, total);
    }

    /// <summary>
    /// Case-insensitive label check.
    /// </summary>
    /// <remarks>
    /// The legacy comparison ran <c>UPPER(dbo.delete_caractere_speciaux(libelle_bareme))</c>, i.e. it also
    /// stripped punctuation. That database function is not available here, so this check is strictly
    /// narrower — it will not consider "Tarif A" and "Tarif-A" duplicates. Flagged rather than silently
    /// approximated with a client-side regex that would not translate to SQL.
    /// </remarks>
    public Task<bool> LibelleExistsAsync(string libelleBareme, string? excludeKeyBareme = null, CancellationToken cancellationToken = default)
    {
        var normalise = libelleBareme.Trim().ToUpperInvariant();

        return db.BaremesAchatCotation
            .AsNoTracking()
            .AnyAsync(b => b.LibelleBareme != null
                && b.LibelleBareme.ToUpper() == normalise
                && (excludeKeyBareme == null || b.Id != excludeKeyBareme), cancellationToken);
    }

    public void Add(BaremeAchatCotation bareme) => db.BaremesAchatCotation.Add(bareme);
    public void Add(ColonneBaremeAchat colonne) => db.ColonnesBaremeAchat.Add(colonne);
    public void Add(DetailBaremeAchat detail) => db.DetailsBaremeAchat.Add(detail);
    public void Add(PrestationBaremeAchat prestation) => db.PrestationsBaremeAchat.Add(prestation);
    public void Add(HistBaremeAchat archive) => db.HistBaremesAchat.Add(archive);
    public void Add(HistColonneBaremeAchat archive) => db.HistColonnesBaremeAchat.Add(archive);
    public void Add(HistDetailBaremeAchat archive) => db.HistDetailsBaremeAchat.Add(archive);
    public void Add(HistPrestationBaremeAchat archive) => db.HistPrestationsBaremeAchat.Add(archive);
    public void Remove(ColonneBaremeAchat colonne) => db.ColonnesBaremeAchat.Remove(colonne);
    public void Remove(DetailBaremeAchat detail) => db.DetailsBaremeAchat.Remove(detail);
    public void Remove(PrestationBaremeAchat prestation) => db.PrestationsBaremeAchat.Remove(prestation);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// Resource persistence (personnel, matériel, equipment referentials, fuel cost rates).
/// </summary>
/// <remarks>
/// <c>t_personnel</c> has no soft-delete flag, so activity is expressed as <c>date_sortie is null</c> and
/// applied here explicitly rather than through a query filter — that keeps "show leavers" expressible.
/// </remarks>
public sealed class ResourceRepository(LogisticsDbContext db, ITenantContext tenant) : IResourceRepository
{
    public Task<Personnel?> FindPersonnelAsync(string keyPersonnel, CancellationToken cancellationToken = default) =>
        db.Personnel.FirstOrDefaultAsync(p => p.SocieteKey == tenant.SocieteKey && p.Id == keyPersonnel, cancellationToken);

    public async Task<PagedResult<Personnel>> SearchPersonnelAsync(PersonnelSearchRequest r, CancellationToken cancellationToken = default)
    {
        var societe = tenant.SocieteKey;
        var query = db.Personnel.AsNoTracking().Where(p => p.SocieteKey == societe);

        // No `supprimer` column exists: a departure is `date_sortie`.
        if (!r.IncludeSortis)
        {
            query = query.Where(p => p.DateSortie == null);
        }

        if (!string.IsNullOrWhiteSpace(r.KeyFonction)) query = query.Where(p => p.KeyFonctionPersonnel == r.KeyFonction);
        if (!string.IsNullOrWhiteSpace(r.Agence)) query = query.Where(p => p.AgencePersonnel == r.Agence);

        if (!string.IsNullOrWhiteSpace(r.Recherche))
        {
            var terme = r.Recherche.Trim();
            query = query.Where(p =>
                (p.NomPersonnel != null && p.NomPersonnel.Contains(terme))
                || (p.PrenomPersonnel != null && p.PrenomPersonnel.Contains(terme))
                || (p.Matricule != null && p.Matricule.Contains(terme)));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.NomPersonnel).ThenBy(p => p.PrenomPersonnel).ThenBy(p => p.Id)
            .Skip((r.Page - 1) * r.PageSize)
            .Take(r.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Personnel>(items, r.Page, r.PageSize, total);
    }

    /// <summary>
    /// Loads a driver, tolerating a leaver.
    /// </summary>
    /// <remarks>
    /// Historic voyages were driven by people who have since left, so excluding departed staff here would
    /// make old voyages impossible to re-cost.
    /// </remarks>
    public Task<Personnel?> FindChauffeurAsync(string keyChauffeur, CancellationToken cancellationToken = default) =>
        db.Personnel.FirstOrDefaultAsync(p => p.SocieteKey == tenant.SocieteKey && p.Id == keyChauffeur, cancellationToken);

    public void Add(Personnel personnel) => db.Personnel.Add(personnel);

    public Task<Materiel?> FindMaterielAsync(string keyMateriel, CancellationToken cancellationToken = default) =>
        db.Materiels.FirstOrDefaultAsync(m => m.SocieteKey == tenant.SocieteKey && m.Id == keyMateriel, cancellationToken);

    public async Task<PagedResult<Materiel>> SearchMaterielAsync(MaterielSearchRequest r, CancellationToken cancellationToken = default)
    {
        var societe = tenant.SocieteKey;

        // Disposal (`statut='S'`) is a business state, so it is only excluded when the caller asks.
        var source = r.IncludeRetires ? db.Materiels.IgnoreQueryFilters() : db.Materiels;
        var query = source.AsNoTracking().Where(m => m.SocieteKey == societe);
        if (!r.IncludeRetires) query = query.Where(m => m.Statut != StatutMateriel.Sorti && m.DateSortie == null);

        if (!string.IsNullOrWhiteSpace(r.TypeMateriel)) query = query.Where(m => m.TypeMateriel == r.TypeMateriel);
        if (r.Statut.HasValue) query = query.Where(m => m.Statut == r.Statut.Value);

        if (!string.IsNullOrWhiteSpace(r.Recherche))
        {
            var terme = r.Recherche.Trim();
            query = query.Where(m =>
                (m.Immatriculation != null && m.Immatriculation.Contains(terme))
                || (m.Intitule != null && m.Intitule.Contains(terme)));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(m => m.TypeMateriel).ThenBy(m => m.Immatriculation).ThenBy(m => m.Id)
            .Skip((r.Page - 1) * r.PageSize)
            .Take(r.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Materiel>(items, r.Page, r.PageSize, total);
    }

    public void Add(Materiel materiel) => db.Materiels.Add(materiel);

    public Task<TypeMateriel?> FindTypeMaterielAsync(string typeMateriel, CancellationToken cancellationToken = default) =>
        db.TypesMateriel.AsNoTracking().FirstOrDefaultAsync(t => t.Id == typeMateriel, cancellationToken);

    public Task<NatureMateriel?> FindNatureMaterielAsync(string natureMateriel, CancellationToken cancellationToken = default) =>
        db.NaturesMateriel.AsNoTracking().FirstOrDefaultAsync(n => n.Id == natureMateriel, cancellationToken);

    /// <summary>
    /// The fuel cost per kilometre in force for a tractor on a date, for one product.
    /// </summary>
    /// <remarks>
    /// Both window bounds are inclusive, matching the legacy
    /// <c>date_debut &lt;= date_depart and date_fin &gt;= date_depart</c>. The product discriminator is
    /// mandatory: two rows are normally live in the same window, so omitting it returns an arbitrary one.
    /// </remarks>
    public Task<CoutKilometrageGasoil?> FindCoutKmAsync(
        string keyTracteur, DateTime date, ProduitGasoil produit, CancellationToken cancellationToken = default)
    {
        var jour = date.Date;

        return db.CoutsKilometrageGasoil
            .AsNoTracking()
            .Where(c => c.KeyTracteur == keyTracteur
                && c.ProduitGasoil == produit
                && c.DateDebut <= jour
                && c.DateFin >= jour)
            .OrderByDescending(c => c.DateDebut)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// Staging persistence for the STC/EDI import.
/// </summary>
/// <remarks>
/// The slice is scoped by company <b>and</b> agency <b>and</b> user. Agency and user are nullable, and a
/// nullable comparison in SQL is <c>= NULL</c>, which matches nothing — so the predicates are written out
/// longhand rather than left to a captured variable.
/// </remarks>
public sealed class EdiStagingRepository(LogisticsDbContext db) : IEdiStagingRepository
{
    public async Task<int> PurgerAsync(string societe, string? agence, string? utilisateur, CancellationToken cancellationToken = default)
    {
        var voyages = await db.EdiVoyagesStaging
            .Where(v => v.Societe == societe
                && (agence == null ? v.Agence == null : v.Agence == agence)
                && (utilisateur == null ? v.Utilisateur == null : v.Utilisateur == utilisateur))
            .ToListAsync(cancellationToken);

        var dossiers = await db.EdiDossiersStaging
            .Where(d => d.Societe == societe
                && (agence == null ? d.Agence == null : d.Agence == agence)
                && (utilisateur == null ? d.Utilisateur == null : d.Utilisateur == utilisateur))
            .ToListAsync(cancellationToken);

        db.EdiVoyagesStaging.RemoveRange(voyages);
        db.EdiDossiersStaging.RemoveRange(dossiers);

        return voyages.Count + dossiers.Count;
    }

    public Task<int> CompterDossiersStagedAsync(string numVoyage, string societe, string? utilisateur, CancellationToken cancellationToken = default) =>
        db.EdiDossiersStaging.CountAsync(d => d.NumVoyage == numVoyage && d.Societe == societe
            && (utilisateur == null ? d.Utilisateur == null : d.Utilisateur == utilisateur), cancellationToken);

    public void Add(EdiVoyageStaging voyage) => db.EdiVoyagesStaging.Add(voyage);
    public void Add(EdiDossierStaging dossier) => db.EdiDossiersStaging.Add(dossier);
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// Referential lookups for the STC/EDI reconciliation.
/// </summary>
/// <remarks>
/// Each lookup goes through the <b>EDI code</b> column rather than the application key, because the file
/// carries its own code space: port labels instead of port codes, a destination reference instead of a
/// client key, and two-character EDI codes for incoterm, currency and COD. Assuming any of those equals
/// the application key would resolve nothing, or resolve the wrong row.
/// </remarks>
public sealed class EdiReconciliationRepository(LogisticsDbContext db, ITenantContext tenant) : IEdiReconciliationRepository
{
    public async Task<IReadOnlyList<EdiVoyageStaging>> GetVoyagesStagedAsync(
        string societe, string? agence, string? utilisateur, CancellationToken cancellationToken = default) =>
        await db.EdiVoyagesStaging
            .Where(v => v.Societe == societe
                && (agence == null ? v.Agence == null : v.Agence == agence)
                && (utilisateur == null ? v.Utilisateur == null : v.Utilisateur == utilisateur))
            .OrderBy(v => v.NumVoyage)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<EdiDossierStaging>> GetDossiersStagedAsync(
        string societe, string? agence, string? utilisateur, CancellationToken cancellationToken = default) =>
        await db.EdiDossiersStaging
            .Where(d => d.Societe == societe
                && (agence == null ? d.Agence == null : d.Agence == agence)
                && (utilisateur == null ? d.Utilisateur == null : d.Utilisateur == utilisateur))
            .OrderBy(d => d.NumVoyage).ThenBy(d => d.Position)
            .ToListAsync(cancellationToken);

    public Task<int> CompterDossiersAsync(string numVoyage, string societe, string? utilisateur, CancellationToken cancellationToken = default) =>
        db.EdiDossiersStaging.CountAsync(d => d.NumVoyage == numVoyage && d.Societe == societe
            && (utilisateur == null ? d.Utilisateur == null : d.Utilisateur == utilisateur), cancellationToken);

    /// <summary>Ports are matched on the label, which is what the file supplies.</summary>
    public Task<Port?> FindPortParLibelleAsync(string libelle, CancellationToken cancellationToken = default) =>
        db.Ports.AsNoTracking().FirstOrDefaultAsync(p => p.LibellePort == libelle, cancellationToken);

    /// <summary>The destination code in the file is the client's <c>ref_client</c>.</summary>
    public Task<Client?> FindClientParRefAsync(string refClient, CancellationToken cancellationToken = default) =>
        db.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.SocieteKey == tenant.SocieteKey && c.RefClient == refClient, cancellationToken);

    public Task<Incoterm?> FindIncotermParEdiAsync(string incotermEdi, CancellationToken cancellationToken = default) =>
        db.Incoterms
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.IncotermEdi == incotermEdi, cancellationToken);

    public Task<Devise?> FindDeviseParEdiAsync(string deviseEdi, CancellationToken cancellationToken = default) =>
        db.Devises.AsNoTracking().FirstOrDefaultAsync(d => d.DeviseEdi == deviseEdi, cancellationToken);

    public Task<Cod?> FindCodParEdiAsync(string codEdi, CancellationToken cancellationToken = default) =>
        db.Cods.AsNoTracking().FirstOrDefaultAsync(c => c.CodEdi == codEdi, cancellationToken);

    public Task<bool> RefAgentExisteAsync(string refAgent, CancellationToken cancellationToken = default) =>
        db.Voyages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(v => v.SocieteKey == tenant.SocieteKey && v.RefAgent == refAgent, cancellationToken);

    /// <summary>
    /// The display name of a member of personnel, for resolving a commercial key.
    /// </summary>
    /// <remarks>
    /// Composed in memory rather than in SQL: concatenating two possibly-NULL columns in the database produces
    /// dialect-specific results, and a missing forename should leave a clean surname rather than a stray space.
    /// </remarks>
    public async Task<string?> FindIntitulePersonnelAsync(
        string keyPersonnel, CancellationToken cancellationToken = default)
    {
        var personne = await db.Personnel
            .AsNoTracking()
            .Where(p => p.Id == keyPersonnel)
            .Select(p => new { p.PrenomPersonnel, p.NomPersonnel })
            .FirstOrDefaultAsync(cancellationToken);

        if (personne is null)
        {
            return null;
        }

        var nom = $"{personne.PrenomPersonnel} {personne.NomPersonnel}".Trim();

        return string.IsNullOrWhiteSpace(nom) ? null : nom;
    }

    /// <summary>
    /// Replaces a client's reference with the destination code the imported file supplied.
    /// </summary>
    /// <remarks>
    /// Uses a set-based update rather than loading the client: nothing about the entity is needed, and an import
    /// can touch many clients. Note this path does <b>not</b> pass through the context's <c>SaveChanges</c> length
    /// guard — <c>ExecuteUpdate</c> writes directly — which is acceptable only because the column's limit is a
    /// bounded code and is published in the contract.
    /// </remarks>
    public Task DefinirRefClientAsync(
        string keyClient, string refClient, CancellationToken cancellationToken = default) =>
        db.Clients
            .Where(c => c.Id == keyClient)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.RefClient, refClient), cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// Resolves roles from <c>t_personnel</c>.
/// </summary>
/// <remarks>
/// The legacy flag is <c>admin</c> on the employee row, and the token's user key is the personnel key.
/// A service account that is not tied to an employee row therefore resolves to no privileges, which is the
/// intended behaviour: a machine identity should not silently inherit an administrator's rights.
/// </remarks>
public sealed class UserRoleResolver(LogisticsDbContext db, ILogger<UserRoleResolver> logger) : IUserRoleResolver
{
    public async Task<CallerRoles> ResolveAsync(string? userKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userKey))
        {
            return CallerRoles.Aucun;
        }

        try
        {
            var personnel = await db.Personnel
                .AsNoTracking()
                // IgnoreQueryFilters: personnel has no soft-delete flag, but this keeps the lookup working
                // if one is ever added.
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == userKey, cancellationToken);

            if (personnel is null)
            {
                // A service account with no employee row is unprivileged: a machine identity should not
                // silently inherit an administrator's rights.
                return CallerRoles.Aucun;
            }

            // An administrator is unrestricted, so the scope lookup is skipped rather than returned as an
            // empty set — the two would otherwise be indistinguishable downstream.
            if (personnel.Admin)
            {
                return new CallerRoles(true);
            }

            var types = await db.PersonnelTypesDossier
                .AsNoTracking()
                .Where(s => s.KeyPersonnel == userKey)
                .Select(s => s.TypeDossier)
                .ToListAsync(cancellationToken);

            return new CallerRoles(false, types);
        }
        catch (Exception ex)
        {
            // Fails closed to NO privileges rather than to a privileged default. The consequence is that an
            // administrator cannot change settings while the database is unreachable — not that anyone
            // becomes one.
            logger.LogWarning(ex,
                "Could not resolve roles for {UserKey}; issuing an unprivileged identity.", userKey);

            return CallerRoles.Aucun;
        }
    }
}

/// <summary>
/// Exposes the current caller's roles to the application layer.
/// </summary>
/// <remarks>
/// Derived from the identity the authentication handler built out of the <b>signed</b> token, so it cannot
/// be influenced by the request. A scoped accessor rather than a method parameter keeps authorisation out
/// of every service signature while still making the dependency visible in the constructor.
/// </remarks>
public sealed class CallerRolesAccessor(CallerContext context) : ICallerRolesAccessor
{
    public CallerRoles Roles => context.Caller is null
        ? CallerRoles.Aucun
        : new CallerRoles(context.Caller.EstAdmin, context.Caller.TypesDossierAutorises);
}

/// <summary>
/// Fuel tank and issue persistence.
/// </summary>
/// <remarks>
/// Tenant columns differ per table here (<c>SOCIETE</c>, <c>societe_sortie</c>, <c>societe_bon</c>,
/// <c>soc_prix</c>), so each predicate goes through the entity property rather than a shared string.
/// </remarks>
public sealed class CarburantRepository(LogisticsDbContext db, ITenantContext tenant) : ICarburantRepository
{
    public Task<Citerne?> FindCiterneAsync(string keyCiterne, CancellationToken cancellationToken = default) =>
        db.Citernes.FirstOrDefaultAsync(c => c.SocieteKey == tenant.SocieteKey && c.Id == keyCiterne, cancellationToken);

    public async Task<PagedResult<Citerne>> SearchCiternesAsync(
        string? recherche, TypeCiterne? type, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = db.Citernes.AsNoTracking().Where(c => c.SocieteKey == tenant.SocieteKey);

        if (type.HasValue)
        {
            query = query.Where(c => c.TypeCiterne == type.Value);
        }

        if (!string.IsNullOrWhiteSpace(recherche))
        {
            var terme = recherche.Trim();
            query = query.Where(c => c.IntituleCiterne != null && c.IntituleCiterne.Contains(terme));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.IntituleCiterne).ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Citerne>(items, page, pageSize, total);
    }

    /// <summary>
    /// Loads an issue <b>including cancelled ones</b>.
    /// </summary>
    /// <remarks>
    /// The soft-delete filter would hide a cancelled movement, and cancelling is idempotent — reloading one
    /// has to succeed to return it unchanged rather than reporting it missing.
    /// </remarks>
    public Task<SortieGasoil?> FindSortieAsync(string keySortie, CancellationToken cancellationToken = default) =>
        db.SortiesGasoil
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.SocieteKey == tenant.SocieteKey && s.Id == keySortie, cancellationToken);

    public async Task<PagedResult<SortieGasoil>> SearchSortiesAsync(
        string? keyCiterne, DateTime? du, DateTime? au, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = db.SortiesGasoil.AsNoTracking().Where(s => s.SocieteKey == tenant.SocieteKey);

        if (!string.IsNullOrWhiteSpace(keyCiterne)) query = query.Where(s => s.KeyCiterne == keyCiterne);
        if (du.HasValue) query = query.Where(s => s.DateSortie >= du.Value);
        if (au.HasValue) query = query.Where(s => s.DateSortie <= au.Value);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.DateSortie).ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SortieGasoil>(items, page, pageSize, total);
    }

    public void Add(Citerne citerne) => db.Citernes.Add(citerne);
    public void Add(SortieGasoil sortie) => db.SortiesGasoil.Add(sortie);
    public void Add(JaugeGasoil jauge) => db.JaugesGasoil.Add(jauge);

    /// <summary>
    /// Loads a delivery note including its tank lines.
    /// </summary>
    /// <remarks>
    /// The lines are included because the DTO re-derives the per-tank allocation, which is never stored —
    /// there is no line amount column in the schema.
    /// </remarks>
    public Task<ApprovisionnementCiterne?> FindApprovisionnementAsync(
        string keyApprovisionnement, CancellationToken cancellationToken = default) =>
        db.ApprovisionnementsCiterne
            .Include(a => a.Lignes)
            .FirstOrDefaultAsync(a => a.Id == keyApprovisionnement, cancellationToken);

    public async Task<PagedResult<ApprovisionnementCiterne>> SearchApprovisionnementsAsync(
        string? keyFournisseur, DateTime? du, DateTime? au, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        Paging.Valider(page, pageSize);

        var query = db.ApprovisionnementsCiterne
            .AsNoTracking()
            .Include(a => a.Lignes)
            .Where(a => a.SocieteKey == tenant.SocieteKey);

        if (!string.IsNullOrWhiteSpace(keyFournisseur))
        {
            query = query.Where(a => a.KeyFournisseur == keyFournisseur);
        }

        if (du.HasValue)
        {
            query = query.Where(a => a.DateApprovisionnement >= du.Value);
        }

        if (au.HasValue)
        {
            query = query.Where(a => a.DateApprovisionnement <= au.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.DateApprovisionnement)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ApprovisionnementCiterne>(items, page, pageSize, total);
    }

    public void Add(ApprovisionnementCiterne approvisionnement) =>
        db.ApprovisionnementsCiterne.Add(approvisionnement);

    public void Add(ConsommationGasoil consommation) => db.ConsommationsGasoil.Add(consommation);

    public void Add(BonGasoil bon) => db.BonsGasoil.Add(bon);

    public void Add(StockGasoilConfrere stock) => db.StocksGasoilConfrere.Add(stock);

    public void Add(CoutKilometrageGasoil ratio) => db.CoutsKilometrageGasoil.Add(ratio);

    /// <summary>
    /// The movements of one vehicle and product not yet counted in a measurement, oldest first.
    /// </summary>
    /// <remarks>
    /// Ordered because the window is defined by the first two full tanks in date order, and tracked because
    /// the caller marks the consumed rows.
    /// </remarks>
    public async Task<IReadOnlyList<ConsommationGasoil>> GetNonCalculeesAsync(
        string keyMateriel, string typeCiterne, CancellationToken cancellationToken = default) =>
        await db.ConsommationsGasoil
            .Where(c => c.SocieteKey == tenant.SocieteKey
                        && c.KeyMateriel == keyMateriel
                        && c.TypeCiterne == typeCiterne
                        && !c.RatioCalcule)
            .OrderBy(c => c.DateConsommation)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CoutKilometrageGasoil>> GetRatiosAsync(
        string keyMateriel, CancellationToken cancellationToken = default) =>
        await db.CoutsKilometrageGasoil
            .AsNoTracking()
            .Where(r => r.KeyTracteur == keyMateriel)
            .OrderByDescending(r => r.DateFin)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Finds a consignment balance by partner.
    /// </summary>
    /// <remarks>
    /// Tracked rather than <c>AsNoTracking</c>: the caller increments the balance and relies on the same
    /// <c>SaveChanges</c> to persist it.
    /// </remarks>
    public Task<StockGasoilConfrere?> FindStockConfrereAsync(
        string keyConfrere, CancellationToken cancellationToken = default) =>
        db.StocksGasoilConfrere
            .FirstOrDefaultAsync(s => s.SocieteKey == tenant.SocieteKey && s.KeyConfrere == keyConfrere,
                cancellationToken);

    public Task<BonGasoil?> FindBonAsync(string keyBon, CancellationToken cancellationToken = default) =>
        db.BonsGasoil.FirstOrDefaultAsync(b => b.Id == keyBon, cancellationToken);

    public async Task<PagedResult<BonGasoil>> SearchBonsAsync(
        string? keyFournisseur, DateTime? du, DateTime? au, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        Paging.Valider(page, pageSize);

        var query = db.BonsGasoil
            .AsNoTracking()
            .Where(b => b.SocieteKey == tenant.SocieteKey);

        if (!string.IsNullOrWhiteSpace(keyFournisseur))
        {
            query = query.Where(b => b.KeyFournisseur == keyFournisseur);
        }

        if (du.HasValue)
        {
            query = query.Where(b => b.DateBon >= du.Value);
        }

        if (au.HasValue)
        {
            query = query.Where(b => b.DateBon <= au.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(b => b.DateBon)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<BonGasoil>(items, page, pageSize, total);
    }

    /// <summary>
    /// Finds a journal row by the key it shares with its operational document.
    /// </summary>
    /// <remarks>
    /// <c>IgnoreQueryFilters</c> because the caller is the cancellation path: a row already soft-deleted must
    /// still be found, or cancelling twice would leave the document cancelled and the journal row live.
    /// </remarks>
    public Task<ConsommationGasoil?> FindConsommationAsync(
        string keyConsommation, CancellationToken cancellationToken = default) =>
        db.ConsommationsGasoil
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == keyConsommation, cancellationToken);
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// Reads company settings from <c>t_parametre_societe</c>.
/// </summary>
/// <remarks>
/// <para>
/// The code is matched with <c>UPPER(col) = UPPER(@code)</c> rather than a plain equality. The legacy reads
/// are literal comparisons that work only because SQL Server's default collation happens to be
/// case-insensitive; the setting codes carry deliberate mixed casing (<c>Impr_jour_magasinage_facture</c>,
/// <c>Saisie_ach_deb_rub</c>) which a case-sensitive collation would fail to match. Upper-casing both sides
/// makes the lookup correct under either collation.
/// </para>
/// <para>
/// One lookup helper serves all the typed readings, so the "absent row" behaviour is defined in exactly one
/// place — which matters, because the two boolean readings disagree about what absence means.
/// </para>
/// </remarks>
public sealed class ParametreSocieteProvider(LogisticsDbContext db, ITenantContext tenant) : IParametreSocieteProvider
{
    public async Task<bool> EstOuiAsync(string keyParametre, CancellationToken cancellationToken = default) =>
        (await ChercherAsync(keyParametre, cancellationToken))?.CommeOui() ?? false;

    /// <summary>
    /// <c>&lt;&gt; "Non"</c> semantics, in which an <b>absent row is true</b>.
    /// </summary>
    /// <remarks>
    /// The default when the row is missing is therefore <c>true</c>, not <c>false</c>. Returning false here
    /// would treat every unconfigured company as having opted out of a required field.
    /// </remarks>
    public async Task<bool> ObligatoireSaufNonAsync(string keyParametre, CancellationToken cancellationToken = default) =>
        (await ChercherAsync(keyParametre, cancellationToken))?.CommeNonDefaut() ?? true;

    public async Task<string?> TexteAsync(string keyParametre, CancellationToken cancellationToken = default) =>
        (await ChercherAsync(keyParametre, cancellationToken))?.ValeurParametre;

    public async Task<decimal?> NombreAsync(string keyParametre, CancellationToken cancellationToken = default) =>
        (await ChercherAsync(keyParametre, cancellationToken))?.CommeNombre();

    public async Task<IReadOnlyList<ParametreSociete>> ListerAsync(CancellationToken cancellationToken = default) =>
        await db.ParametresSociete
            .AsNoTracking()
            .Where(p => p.SocieteKey == tenant.SocieteKey)
            .OrderBy(p => p.KeyParametre)
            .ToListAsync(cancellationToken);

    private async Task<ParametreSociete?> ChercherAsync(string keyParametre, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(keyParametre))
        {
            return null;
        }

        var societe = tenant.SocieteKey;
        var recherche = keyParametre.Trim().ToUpperInvariant();

        return await db.ParametresSociete
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.SocieteKey == societe && p.KeyParametre.ToUpper() == recherche,
                cancellationToken);
    }
}

/// <summary>
/// Authenticates a user against <c>t_personnel</c> using the legacy password encoding.
/// </summary>
/// <remarks>
/// The identifier is matched case-insensitively because the legacy comparison is a literal one that depends on
/// a case-insensitive collation to agree — an employee who types their login with different capitalisation
/// signs in on the desktop and must be able to here too.
/// </remarks>
public sealed class UtilisateurAuthenticator(
    LogisticsDbContext db, IAuditTrail audit, ILogger<UtilisateurAuthenticator> logger)
    : IUtilisateurAuthenticator
{
    public async Task<UtilisateurLegacy?> AuthentifierAsync(
        string societeKey, string? identifiant, string? motDePasse, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(societeKey) || string.IsNullOrWhiteSpace(identifiant))
        {
            return null;
        }

        var recherche = identifiant.Trim().ToUpperInvariant();

        var personnel = await db.Personnel
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.SocieteKey == societeKey
                     && p.IdentifiantApp != null
                     && p.IdentifiantApp.ToUpper() == recherche,
                cancellationToken);

        if (personnel is null)
        {
            return null;
        }

        // A blocked account, or an employee who has left, cannot authenticate whatever the password says.
        // `t_personnel` has no delete flag at all — departure is recorded as date_sortie plus a revoked login —
        // so this check is the only thing standing between a former employee and a valid token.
        if (personnel.Bloque || !personnel.IsActif)
        {
            logger.LogWarning(
                "Refused authentication for {KeyPersonnel}: {Raison}.",
                personnel.Id,
                personnel.Bloque ? "account is blocked" : "employee has left (date_sortie is set)");

            return null;
        }

        if (!MotDePasseLegacy.Verifier(motDePasse, personnel.PassWord))
        {
            return null;
        }

        if (personnel.PremiereConnexion)
        {
            // Allowed through, because there is no change-password endpoint yet and refusing would lock the
            // employee out entirely. Reported so the gap is visible rather than silent.
            logger.LogWarning(
                "User {KeyPersonnel} signed in on a first connection; the forced password change is not "
                + "implemented in this API.",
                personnel.Id);
        }

        return new UtilisateurLegacy(
            personnel.Id,
            personnel.SocieteKey,
            personnel.AgencePersonnel,
            $"{personnel.PrenomPersonnel} {personnel.NomPersonnel}".Trim() is { Length: > 0 } nom
                ? nom
                : personnel.IdentifiantApp,
            personnel.Admin,
            personnel.PremiereConnexion);
    }

    /// <summary>
    /// Changes a user's own password.
    /// </summary>
    /// <remarks>
    /// The three failure modes are reported differently on purpose, unlike the login path where conflating them
    /// is essential. Here the caller has already proved they are this user, so telling them which part of *their
    /// own request* was wrong leaks nothing — and a generic failure would leave them guessing whether they
    /// mistyped the old password or chose one the rules reject.
    /// </remarks>
    public async Task<bool> ChangerMotDePasseAsync(
        string keyPersonnel, string? ancienMotDePasse, string? nouveauMotDePasse,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyPersonnel))
        {
            throw new EntityNotFoundException("Personnel", keyPersonnel ?? string.Empty);
        }

        // Loaded TRACKED: the change is persisted by the same context.
        var personnel = await db.Personnel
            .FirstOrDefaultAsync(p => p.Id == keyPersonnel, cancellationToken)
            ?? throw new EntityNotFoundException("Personnel", keyPersonnel);

        // The current password is re-verified before anything is written. That is the point of the operation,
        // not a formality: without it a stolen token could be escalated into permanent account ownership.
        if (!MotDePasseLegacy.Verifier(ancienMotDePasse, personnel.PassWord))
        {
            logger.LogWarning("Refused a password change for {KeyPersonnel}: current password is wrong.",
                personnel.Id);

            return false;
        }

        if (ReglesMotDePasse.MotifDeRefus(nouveauMotDePasse) is { } motif)
        {
            throw new BusinessRuleException("motdepasse.regles", motif);
        }

        // Already encoded, so the entity never holds the plaintext.
        personnel.ChangerMotDePasse(MotDePasseLegacy.Coder(nouveauMotDePasse), premiereConnexion: false);

        await db.SaveChangesAsync(cancellationToken);

        // $MPWD, as ClassPersonnel.ModifyPWD writes at line 969. A credential change is exactly the event an
        // auditor asks about, so it is traced rather than left to the logger.
        await audit.TraceAsync<TracePersonnel>(
            personnel.Id,
            TracePersonnel.StatutModificationMotDePasse,
            "Modification du Mot de Passe",
            keyDocument: null,
            valeur: 0,
            cancellationToken);

        return true;
    }
}

/// <summary>
/// Resolves weight/volume equivalence ratios from <c>t_rapport_poids_volume</c>.
/// </summary>
/// <remarks>
/// Matched with <c>UPPER(produit) = UPPER(@produit)</c> for the same reason the parameters are: the codes are
/// stored with deliberate casing and the legacy reads are literal comparisons that depend on a
/// case-insensitive collation.
/// </remarks>
public sealed class RapportPoidsVolumeProvider(LogisticsDbContext db, ITenantContext tenant)
    : IRapportPoidsVolumeProvider
{
    public async Task<RapportPoidsVolume?> PourProduitAsync(
        string produit, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(produit))
        {
            return null;
        }

        var societe = tenant.SocieteKey;
        var recherche = produit.Trim().ToUpperInvariant();

        return await db.RapportsPoidsVolume
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.SocieteKey == societe && r.Produit.ToUpper() == recherche,
                cancellationToken);
    }
}

/// <summary>
/// Read model over the <c>t_trace_*</c> audit tables.
/// </summary>
/// <remarks>
/// The 63 legacy trace tables are structurally identical, so the only thing the repository has to do
/// per entity is pick the right <c>DbSet</c>. Everything else is shared.
/// <para>
/// Every read is scoped to the caller's company through <c>soc_trace</c>. The legacy viewers did this
/// by remembering to append the predicate to each hand-written query; here it cannot be forgotten.
/// </para>
/// </remarks>
public sealed class TrackingRepository(LogisticsDbContext db, ITenantContext tenant) : ITrackingRepository
{
    public Task<bool> EntityExistsAsync(TrackingEntity entity, string entityKey, CancellationToken cancellationToken = default) => entity switch
    {
        TrackingEntity.Client => db.Clients.AnyAsync(x => x.Id == entityKey && x.SocieteKey == tenant.SocieteKey, cancellationToken),
        TrackingEntity.Voyage => db.Voyages.AnyAsync(x => x.Id == entityKey && x.SocieteKey == tenant.SocieteKey, cancellationToken),
        TrackingEntity.Dossier => db.Dossiers.AnyAsync(x => x.Id == entityKey && x.SocieteKey == tenant.SocieteKey, cancellationToken),
        TrackingEntity.FactureVente => db.EntetesFactureVente.AnyAsync(x => x.Id == entityKey && x.SocieteKey == tenant.SocieteKey, cancellationToken),
        TrackingEntity.Reglement => db.EntetesReglement.AnyAsync(x => x.Id == entityKey && x.SocieteKey == tenant.SocieteKey, cancellationToken),
        TrackingEntity.Booking => db.Bookings.AnyAsync(x => x.Id == entityKey && x.SocieteKey == tenant.SocieteKey, cancellationToken),
        TrackingEntity.Cotation => db.Cotations.AnyAsync(x => x.Id == entityKey && x.SocieteKey == tenant.SocieteKey, cancellationToken),
        _ => Task.FromResult(false)
    };

    public async Task<TraceEntry> RecordAsync(TrackingEntity entity, string entityKey, RecordTrackingEventRequest request,
        DateTime businessDate, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        TraceEntry entry = entity switch
        {
            TrackingEntity.Client => new TraceClient(entityKey, tenant.SocieteKey, tenant.AgenceKey, request.KeyStatut, businessDate, now, request.Commentaire, tenant.UserDisplayName, request.KeyDocument, request.Valeur, tenant.Workstation),
            TrackingEntity.Voyage => new TraceVoyage(entityKey, tenant.SocieteKey, tenant.AgenceKey, request.KeyStatut, businessDate, now, request.Commentaire, tenant.UserDisplayName, request.KeyDocument, request.Valeur, tenant.Workstation),
            TrackingEntity.Dossier => new TraceDossier(entityKey, tenant.SocieteKey, tenant.AgenceKey, request.KeyStatut, businessDate, now, request.Commentaire, tenant.UserDisplayName, request.KeyDocument, request.Valeur, tenant.Workstation),
            TrackingEntity.FactureVente => new TraceFactureVente(entityKey, tenant.SocieteKey, tenant.AgenceKey, request.KeyStatut, businessDate, now, request.Commentaire, tenant.UserDisplayName, request.KeyDocument, request.Valeur, tenant.Workstation),
            TrackingEntity.Reglement => new TraceReglement(entityKey, tenant.SocieteKey, tenant.AgenceKey, request.KeyStatut, businessDate, now, request.Commentaire, tenant.UserDisplayName, request.KeyDocument, request.Valeur, tenant.Workstation),
            TrackingEntity.Booking => new TraceBooking(entityKey, tenant.SocieteKey, tenant.AgenceKey, request.KeyStatut, businessDate, now, request.Commentaire, tenant.UserDisplayName, request.KeyDocument, request.Valeur, tenant.Workstation),
            TrackingEntity.Cotation => new TraceCotation(entityKey, tenant.SocieteKey, tenant.AgenceKey, request.KeyStatut, businessDate, now, request.Commentaire, tenant.UserDisplayName, request.KeyDocument, request.Valeur, tenant.Workstation),
            _ => throw new BusinessRuleException("tracking.entity", "Type d’entité non pris en charge.")
        };
        db.Add(entry);
        // An explicit user action must report persistence failures, unlike best-effort business audit.
        await db.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public Task<IReadOnlyList<TraceEntry>> GetTimelineAsync(
        TrackingEntity entity, string entityKey, int take, CancellationToken cancellationToken = default) =>
        QueryAsync(entity, entityKey, null, null, take, cancellationToken);

    public async Task<IReadOnlyList<TraceEntry>> GetActivityAsync(
        IReadOnlyCollection<TrackingEntity> entities, DateTime? from, DateTime? to, int take,
        CancellationToken cancellationToken = default)
    {
        var all = new List<TraceEntry>();

        // Each entity contributes its own page before the global take, so one busy entity cannot crowd
        // every other one out of the feed.
        foreach (var entity in entities)
        {
            all.AddRange(await QueryAsync(entity, null, from, to, take, cancellationToken));
        }

        return all.OrderByDescending(t => t.DateTraceSystem).Take(take).ToArray();
    }

    public async Task<IReadOnlyDictionary<string, string>> GetStatutLibellesAsync(
        IEnumerable<string?> keys, CancellationToken cancellationToken = default)
    {
        var wanted = keys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (wanted.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var societe = tenant.SocieteKey;

        // IgnoreQueryFilters on purpose: a retired status must still label its historical events,
        // otherwise old audit history becomes unreadable. A NULL soc_statut means the code is global.
        var rows = await db.Statuts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => wanted.Contains(s.Id) && (s.SocieteKey == null || s.SocieteKey == "" || s.SocieteKey == societe))
            .Select(s => new { s.Id, s.LibelleStatut })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.Id, r => r.LibelleStatut, StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<Statut>> GetStatutsAsync(string? utilisation, CancellationToken cancellationToken = default)
    {
        var societe = tenant.SocieteKey;

        var query = db.Statuts
            .AsNoTracking()
            // A NULL soc_statut means the status is shared by every company.
            .Where(s => s.SocieteKey == null || s.SocieteKey == "" || s.SocieteKey == societe);

        if (!string.IsNullOrWhiteSpace(utilisation))
        {
            query = query.Where(s => s.Utilisation == utilisation);
        }

        return await query.ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<TraceEntry>> QueryAsync(
        TrackingEntity entity, string? entityKey, DateTime? from, DateTime? to, int take,
        CancellationToken cancellationToken) => entity switch
        {
            TrackingEntity.Client => await ReadAsync(db.TracesClient, entityKey, from, to, take, cancellationToken),
            TrackingEntity.Voyage => await ReadAsync(db.TracesVoyage, entityKey, from, to, take, cancellationToken),
            TrackingEntity.Dossier => await ReadAsync(db.TracesDossier, entityKey, from, to, take, cancellationToken),
            TrackingEntity.FactureVente => await ReadAsync(db.TracesFactureVente, entityKey, from, to, take, cancellationToken),
            TrackingEntity.Reglement => await ReadAsync(db.TracesReglement, entityKey, from, to, take, cancellationToken),
            TrackingEntity.Booking => await ReadAsync(db.TracesBooking, entityKey, from, to, take, cancellationToken),
            TrackingEntity.Cotation => await ReadAsync(db.TracesCotation, entityKey, from, to, take, cancellationToken),
            _ => []
        };

    private async Task<List<TraceEntry>> ReadAsync<T>(
        DbSet<T> set, string? entityKey, DateTime? from, DateTime? to, int take, CancellationToken cancellationToken)
        where T : TraceEntry
    {
        var societe = tenant.SocieteKey;

        var query = set.AsNoTracking().Where(t => t.Societe == societe);

        if (entityKey is not null)
        {
            query = query.Where(t => t.EntityKey == entityKey);
        }

        if (from.HasValue)
        {
            query = query.Where(t => t.DateTraceSystem >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(t => t.DateTraceSystem <= to.Value);
        }

        var rows = await query
            .OrderByDescending(t => t.DateTraceSystem)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows.Cast<TraceEntry>().ToList();
    }
}

/// <summary>Voyage persistence, including the "already costed" marker table.</summary>
public sealed class VoyageRepository(LogisticsDbContext db) : IVoyageRepository
{
    public Task<Voyage?> GetAsync(string keyVoyage, bool includeChildren, CancellationToken cancellationToken = default)
    {
        var query = db.Voyages.AsQueryable();

        if (includeChildren)
        {
            query = query
                .Include(v => v.Trajets)
                .Include(v => v.Equipements).ThenInclude(e => e.Details);
        }

        // `Id` is the mapped key; the `KeyVoyage` convenience property is unmapped and cannot be
        // translated by EF.
        return query.SingleOrDefaultAsync(v => v.Id == keyVoyage, cancellationToken);
    }

    public async Task<PagedResult<Voyage>> SearchAsync(string? search, CategorieVoyage? categorie,
        string? keyAgence, DateTime? from, DateTime? to, bool? validatedOnly, int page, int pageSize,
        CancellationToken cancellationToken = default, VoyageSearchStatus? statut = null)
    {
        var query = ApplyStatusFilter(db.Voyages.AsNoTracking(), statut);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            query = query.Where(v => (v.NumVoyage != null && v.NumVoyage.Contains(needle))
                || v.Id.Contains(needle)
                || (v.NavireDepart != null && v.NavireDepart.Contains(needle)));
        }

        if (categorie.HasValue)
        {
            query = query.Where(v => v.CategorieVoyage == categorie.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyAgence))
        {
            query = query.Where(v => v.KeyAgenceExploitation == keyAgence);
        }

        // The legacy lists filter on either the planned or the actual departure date.
        if (from.HasValue)
        {
            query = query.Where(v => v.DateDepartPrevu >= from.Value || v.DateDepart >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(v => v.DateDepartPrevu <= to.Value || v.DateDepart <= to.Value);
        }

        if (validatedOnly == true)
        {
            query = query.Where(v => v.DateVoyage != null);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(v => v.DateDepartPrevu ?? v.DateCreation)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Voyage>(items, page, pageSize, total);
    }

    internal static IQueryable<Voyage> ApplyStatusFilter(IQueryable<Voyage> query, VoyageSearchStatus? statut) => statut switch
    {
        null => query,
        VoyageSearchStatus.Brouillon => query.Where(v => v.DateVoyage == null && v.DateCloture == null),
        VoyageSearchStatus.Valide => query.Where(v => v.DateVoyage != null && v.DateCloture == null),
        VoyageSearchStatus.Cloture => query.Where(v => v.DateCloture != null),
        _ => throw new ArgumentOutOfRangeException(nameof(statut))
    };

    public Task<bool> NumVoyageExistsAsync(string numVoyage, string? excludingKey, CancellationToken cancellationToken = default) =>
        db.Voyages.AnyAsync(
            v => v.NumVoyage == numVoyage && (excludingKey == null || v.Id != excludingKey), cancellationToken);

    public async Task<(int Total, int Uninvoiced)> CountDossiersAsync(string keyVoyage, CancellationToken cancellationToken = default)
    {
        var query = db.Dossiers.AsNoTracking().Where(d => d.KeyVoyageInter == keyVoyage);

        var total = await query.CountAsync(cancellationToken);
        // "Non Facturé" is the blocker the legacy clôture check looks for.
        var uninvoiced = await query.CountAsync(d => d.StatutFacturation == StatutFacturation.NonFacture, cancellationToken);

        return (total, uninvoiced);
    }

    public async Task<IReadOnlyList<string>> GetCalculatedOperationsAsync(string keyVoyage, CancellationToken cancellationToken = default) =>
        await db.CoutsOperationVoyageCalcule
            .AsNoTracking()
            .Where(c => c.KeyVoyage == keyVoyage)
            .Select(c => c.Operation)
            .ToListAsync(cancellationToken);

    public void RemoveCostMarker(string keyVoyage, string operation) =>
        db.CoutsOperationVoyageCalcule.Remove(new CoutOperationVoyageCalcule(keyVoyage, operation));

    /// <summary>
    /// True when an attached, non-deleted dossier sits in the agency's customs warehouse — the exact
    /// legacy condition
    /// <c>key_voyage in (select key_voyage_inter from t_dossiers where supprimer=0 and magasin_douane=&lt;lieu&gt;)</c>.
    /// With no warehouse configured the answer is simply no.
    /// </summary>
    public Task<bool> HasMeadDossierAsync(string keyVoyage, string? lieuDedouanement, CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(lieuDedouanement)
            ? Task.FromResult(false)
            // The Dossiers query filter already excludes soft-deleted rows.
            : db.Dossiers.AnyAsync(d => d.KeyVoyageInter == keyVoyage && d.MagasinDouane == lieuDedouanement, cancellationToken);

    public void Add(CoutKilometrageVoyage ligne) => db.CoutsKilometrageVoyage.Add(ligne);

    public async Task RemoveKilometrageAsync(string keyVoyage, CancellationToken cancellationToken = default)
    {
        var rows = await db.CoutsKilometrageVoyage
            .Where(c => c.KeyVoyage == keyVoyage)
            .ToListAsync(cancellationToken);

        db.CoutsKilometrageVoyage.RemoveRange(rows);
    }

    public void Add(Voyage voyage) => db.Voyages.Add(voyage);
    public void Add(EquipementVoyage equipement) => db.EquipementsVoyage.Add(equipement);

    public async Task<Materiel?> FindMaterielParImmatriculationAsync(
        string typeMateriel, string? immatriculation, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(immatriculation))
        {
            return null;
        }

        var plaque = immatriculation.Trim();

        // Case-insensitively, because a plate typed into a file and a plate captured in the fleet screen are
        // the same plate, and the legacy comparison depends on a case-insensitive collation to agree.
        return await db.Materiels
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.TypeMateriel == typeMateriel && m.Immatriculation != null
                     && m.Immatriculation.ToUpper() == plaque.ToUpper(),
                cancellationToken);
    }
    public void Add(CoutOperationVoyageCalcule marker) => db.CoutsOperationVoyageCalcule.Add(marker);
    public void RemoveTrajet(TrajetVoyage trajet) => db.Remove(trajet);
    public void RemoveEquipement(EquipementVoyage equipement) => db.Remove(equipement);
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}

/// <summary>Dossier persistence, including packing lines and the voyage association table.</summary>
public sealed class DossierRepository(LogisticsDbContext db) : IDossierRepository
{
    public Task<Dossier?> GetAsync(string keyDossier, bool includeColisages, CancellationToken cancellationToken = default)
    {
        var query = db.Dossiers.AsQueryable();

        if (includeColisages)
        {
            query = query.Include(d => d.Colisages);
        }

        return query.SingleOrDefaultAsync(d => d.Id == keyDossier, cancellationToken);
    }

    public async Task<PagedResult<Dossier>> SearchAsync(string? search, string? keyClient, string? keyVoyage,
        string? typeDossier, StatutFacturation? statutFacturation, bool? meadOnly, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = db.Dossiers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            query = query.Where(d => (d.NumDossier != null && d.NumDossier.Contains(needle))
                || d.Id.Contains(needle)
                || (d.NumHbl != null && d.NumHbl.Contains(needle)));
        }

        if (!string.IsNullOrWhiteSpace(keyClient))
        {
            query = query.Where(d => d.KeyClient == keyClient);
        }

        if (!string.IsNullOrWhiteSpace(keyVoyage))
        {
            query = query.Where(d => d.KeyVoyageInter == keyVoyage);
        }

        if (!string.IsNullOrWhiteSpace(typeDossier))
        {
            query = query.Where(d => d.TypeDossier == typeDossier);
        }

        if (statutFacturation.HasValue)
        {
            query = query.Where(d => d.StatutFacturation == statutFacturation.Value);
        }

        if (meadOnly == true)
        {
            query = query.Where(d => d.MagasinDouane != null);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(d => d.DateCreation)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Dossier>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<Dossier>> GetByVoyageAsync(string keyVoyage, CancellationToken cancellationToken = default) =>
        await db.Dossiers
            .AsNoTracking()
            .Where(d => d.KeyVoyageInter == keyVoyage)
            .OrderBy(d => d.Position)
            .ToListAsync(cancellationToken);

    public Task<bool> NumDossierExistsAsync(string numDossier, string? excludingKey, CancellationToken cancellationToken = default) =>
        db.Dossiers.AnyAsync(d => d.NumDossier == numDossier && (excludingKey == null || d.Id != excludingKey), cancellationToken);

    /// <summary>
    /// Sum of taxable weight across a voyage's dossiers — the denominator of the pro-rata sales
    /// allocation. Computed in SQL so it stays correct for large voyages.
    /// </summary>
    public async Task<decimal> SumPoidsTaxableAsync(string keyVoyage, CancellationToken cancellationToken = default) =>
        await db.Dossiers
            .AsNoTracking()
            .Where(d => d.KeyVoyageInter == keyVoyage)
            .SumAsync(d => d.PoidsTaxable ?? 0m, cancellationToken);

    public void Add(Dossier dossier) => db.Dossiers.Add(dossier);
    public void Add(DossierVoyage association) => db.DossiersVoyage.Add(association);
    public void Add(ColisageDossier colisage) => db.ColisagesDossier.Add(colisage);
    public void Add(CodDossier cod) => db.CodsDossier.Add(cod);
    public void Add(FactureFournisseurDossier facture) => db.FacturesFournisseurDossier.Add(facture);
    public void Remove(Dossier dossier) => db.Remove(dossier);
    public void Remove(ColisageDossier colisage) => db.Remove(colisage);
    public void Remove(DossierVoyage association) => db.Remove(association);
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the persistence stack. SQL Server is the target platform, with retry enabled because
    /// the legacy deployment points at remote Express instances over a WAN link.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("LogisticsDatabase")
            ?? throw new InvalidOperationException("Connection string 'LogisticsDatabase' is not configured.");

        services.AddDbContext<LogisticsDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
                sql.MigrationsAssembly(typeof(LogisticsDbContext).Assembly.FullName);
            }));

        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IReglementRepository, ReglementRepository>();
        services.AddScoped<IFactureVenteRepository, FactureVenteRepository>();
        services.AddScoped<IVoyageRepository, VoyageRepository>();
        services.AddScoped<IDossierRepository, DossierRepository>();
        services.AddScoped<ITrackingRepository, TrackingRepository>();
        services.AddScoped<IReferenceGuardRepository, ReferenceGuardRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IQuotationRepository, QuotationRepository>();
        services.AddScoped<IBaremeAchatRepository, BaremeAchatRepository>();
        services.AddScoped<IResourceRepository, ResourceRepository>();
        services.AddScoped<ICarburantRepository, CarburantRepository>();
        services.AddScoped<IUserRoleResolver, UserRoleResolver>();
        services.AddScoped<IUtilisateurAuthenticator, UtilisateurAuthenticator>();
        services.AddScoped<ICallerRolesAccessor, CallerRolesAccessor>();
        services.AddScoped<IParametreSocieteProvider, ParametreSocieteProvider>();
        services.AddScoped<IRapportPoidsVolumeProvider, RapportPoidsVolumeProvider>();
        services.AddScoped<IEdiStagingRepository, EdiStagingRepository>();
        services.AddScoped<IEdiReconciliationRepository, EdiReconciliationRepository>();
        services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddSingleton<IKeyGenerator, PlaceholderKeyGenerator>();

        return services;
    }
}
