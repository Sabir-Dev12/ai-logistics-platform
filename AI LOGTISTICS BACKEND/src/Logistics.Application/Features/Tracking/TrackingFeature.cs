using Logistics.Application.Abstractions;
using Logistics.Domain.Common;
using Logistics.Domain.Entities.Audit;
using Logistics.Domain.Entities.Shipments;

namespace Logistics.Application.Features.Tracking;

/// <summary>
/// The entities that carry a <c>t_trace_*</c> audit trail.
/// </summary>
/// <remarks>
/// The legacy audit subsystem has 63 trace tables that share one shape and differ only in the name of
/// their entity-key column, written by a single <c>tools.Trace(NameTable, name_champ, …)</c> method
/// that interpolates the table name. That is not expressible in EF, so the entities actually needed by
/// the API are modelled concretely and enumerated here. Adding another one means adding a case to
/// <c>TrackingEntity</c>, a case in the repository switch and a <c>ConfigureTrace</c> call — the shape
/// of every table is identical, so there is nothing new to design.
/// </remarks>
public enum TrackingEntity
{
    Client,
    Voyage,
    Dossier,
    FactureVente,
    Reglement,
    Booking,
    Cotation
}

/// <summary>
/// One audit event, as the legacy <c>frm_Trace_*</c> viewers displayed it.
/// </summary>
/// <param name="LibelleStatut">
/// Resolved from <c>t_statut</c>. The legacy viewers joined it in; without it the raw <c>$</c>-codes are
/// unreadable, so the API always resolves it.
/// </param>
public sealed record TrackingEventDto(
    TrackingEntity Entite,
    string CleEntite,
    string? KeyStatut,
    string? LibelleStatut,
    bool IsSysteme,
    DateTime DateTraceUser,
    DateTime DateTraceSystem,
    string? Commentaire,
    string? Utilisateur,
    string? KeyDocument,
    double Valeur,
    string? PosteUtilisateur,
    string? Agence,
    string? Societe);

public sealed record RecordTrackingEventRequest(
    string KeyStatut,
    string Commentaire,
    string? KeyDocument = null,
    double Valeur = 0,
    DateTime? DateEvenement = null);

public sealed record StatutDto(string KeyStatut, string LibelleStatut, string? Utilisation, bool IsSysteme);

/// <summary>Latest known state of one entity, as a tracking screen would summarise it.</summary>
public sealed record EntityTrackingStatusDto(
    TrackingEntity Entite,
    string CleEntite,
    string? KeyStatut,
    string? LibelleStatut,
    DateTime? DateDernierEvenement,
    string? Utilisateur,
    string? Commentaire,
    int NbEvenements,
    IReadOnlyList<TrackingEventDto> DerniersEvenements);

public interface ITrackingRepository
{
    Task<bool> EntityExistsAsync(TrackingEntity entity, string entityKey, CancellationToken cancellationToken = default);
    Task<TraceEntry> RecordAsync(TrackingEntity entity, string entityKey, RecordTrackingEventRequest request,
        DateTime businessDate, CancellationToken cancellationToken = default);

    /// <summary>Audit events for one entity, most recent first.</summary>
    Task<IReadOnlyList<TraceEntry>> GetTimelineAsync(TrackingEntity entity, string entityKey, int take, CancellationToken cancellationToken = default);

    /// <summary>Audit events across several entity kinds, most recent first — the activity feed.</summary>
    Task<IReadOnlyList<TraceEntry>> GetActivityAsync(
        IReadOnlyCollection<TrackingEntity> entities, DateTime? from, DateTime? to, int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves <c>t_statut</c> labels for the given codes.
    /// </summary>
    /// <remarks>
    /// Deliberately ignores the soft-delete query filter: historical audit rows must still resolve
    /// their label even if the status has since been retired, otherwise old history becomes unreadable.
    /// </remarks>
    Task<IReadOnlyDictionary<string, string>> GetStatutLibellesAsync(IEnumerable<string?> keys, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Statut>> GetStatutsAsync(string? utilisation, CancellationToken cancellationToken = default);
}

public interface ITrackingService
{
    Task<IReadOnlyList<TrackingEventDto>> GetTimelineAsync(TrackingEntity entity, string entityKey, int take = 100, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrackingEventDto>> GetActivityAsync(
        IReadOnlyCollection<TrackingEntity>? entities, DateTime? from, DateTime? to, int take = 100,
        CancellationToken cancellationToken = default);
    Task<EntityTrackingStatusDto> GetStatusAsync(TrackingEntity entity, string entityKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StatutDto>> GetStatutsAsync(string? utilisation = null, CancellationToken cancellationToken = default);
    Task<TrackingEventDto> RecordEventAsync(
        TrackingEntity entity, string entityKey, RecordTrackingEventRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Tracking use cases: the migration of <c>frm_Tableau_Bord.vb</c> and the <c>frm_Trace_*</c> viewers.
/// </summary>
/// <remarks>
/// The legacy application had no way to ask "what happened to this dossier?" other than opening the
/// trace grid for that one entity type and reading <c>$</c>-prefixed codes off a table. This service
/// exposes the same data as a cross-entity feed with the status labels already resolved, which is what
/// makes a dashboard possible at all.
/// </remarks>
public sealed class TrackingService(ITrackingRepository tracking) : ITrackingService
{
    private static readonly TrackingEntity[] AllEntities = Enum.GetValues<TrackingEntity>();

    public async Task<IReadOnlyList<TrackingEventDto>> GetTimelineAsync(
        TrackingEntity entity, string entityKey, int take = 100, CancellationToken cancellationToken = default)
    {
        GuardEntityKey(entityKey);
        var entries = await tracking.GetTimelineAsync(entity, entityKey, Clamp(take), cancellationToken);

        return await MapManyAsync(entries, cancellationToken);
    }

    public async Task<IReadOnlyList<TrackingEventDto>> GetActivityAsync(
        IReadOnlyCollection<TrackingEntity>? entities, DateTime? from, DateTime? to, int take = 100,
        CancellationToken cancellationToken = default)
    {
        var scope = entities is { Count: > 0 } ? entities : AllEntities;
        var entries = await tracking.GetActivityAsync(scope, from, to, Clamp(take), cancellationToken);

        return await MapManyAsync(entries, cancellationToken);
    }

    /// <summary>
    /// Summarises an entity's latest state. The legacy equivalent was querying
    /// <c>t_trace_X where key_statut = '$…'</c> by hand to recover the creation date and user, because
    /// business tables carry no audit columns.
    /// </summary>
    public async Task<EntityTrackingStatusDto> GetStatusAsync(
        TrackingEntity entity, string entityKey, CancellationToken cancellationToken = default)
    {
        GuardEntityKey(entityKey);

        var entries = await tracking.GetTimelineAsync(entity, entityKey, 10, cancellationToken);
        var mapped = await MapManyAsync(entries, cancellationToken);
        var latest = mapped.FirstOrDefault();

        return new EntityTrackingStatusDto(
            entity, entityKey, latest?.KeyStatut, latest?.LibelleStatut, latest?.DateTraceSystem,
            latest?.Utilisateur, latest?.Commentaire, mapped.Count, mapped);
    }

    public async Task<IReadOnlyList<StatutDto>> GetStatutsAsync(
        string? utilisation = null, CancellationToken cancellationToken = default)
    {
        var statuts = await tracking.GetStatutsAsync(utilisation, cancellationToken);

        return statuts
            .OrderBy(s => s.LibelleStatut)
            .Select(s => new StatutDto(s.KeyStatut, s.LibelleStatut, s.Utilisation, s.IsSystem))
            .ToArray();
    }

    public async Task<TrackingEventDto> RecordEventAsync(
        TrackingEntity entity, string entityKey, RecordTrackingEventRequest request,
        CancellationToken cancellationToken = default)
    {
        GuardEntityKey(entityKey);

        if (!Enum.IsDefined(entity))
            throw new BusinessRuleException("tracking.entity", "Type d’entité non pris en charge.");
        var code = request.KeyStatut?.Trim();
        if (string.IsNullOrEmpty(code) || code.StartsWith('$'))
            throw new BusinessRuleException("tracking.status", "Sélectionnez un statut utilisateur autorisé.");
        var statuses = await tracking.GetStatutsAsync(null, cancellationToken);
        var selected = statuses.FirstOrDefault(s => s.KeyStatut == code && !s.Supprime && !s.IsSystem
            && (string.IsNullOrWhiteSpace(s.Utilisation) || string.Equals(s.Utilisation, entity.ToString(), StringComparison.OrdinalIgnoreCase)));
        if (selected is null)
            throw new BusinessRuleException("tracking.status", "Ce statut n’est pas autorisé pour cette entité.");
        if (string.IsNullOrWhiteSpace(request.Commentaire) || request.Commentaire.Length > 2000 || !double.IsFinite(request.Valeur))
            throw new BusinessRuleException("tracking.input", "Vérifiez le commentaire (1 à 2 000 caractères) et la valeur de l’événement.");
        if (!await tracking.EntityExistsAsync(entity, entityKey, cancellationToken))
            throw new EntityNotFoundException(entity.ToString(), entityKey);
        var when = request.DateEvenement ?? DateTime.UtcNow;
        if (when.Kind == DateTimeKind.Local) when = when.ToUniversalTime();
        else if (when.Kind == DateTimeKind.Unspecified) when = DateTime.SpecifyKind(when, DateTimeKind.Utc);
        var stored = await tracking.RecordAsync(entity, entityKey,
            request with { KeyStatut = code, Commentaire = request.Commentaire.Trim() }, when, cancellationToken);
        return Map(stored, new Dictionary<string, string> { [code] = selected.LibelleStatut }, entity);
    }

    private async Task<IReadOnlyList<TrackingEventDto>> MapManyAsync(
        IReadOnlyList<TraceEntry> entries, CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return [];
        }

        // One lookup for the whole page rather than per row.
        var labels = await tracking.GetStatutLibellesAsync(entries.Select(e => e.KeyStatut), cancellationToken);

        return entries.Select(e => Map(e, labels, ResolveEntity(e))).ToArray();
    }

    private static TrackingEventDto Map(
        TraceEntry entry, IReadOnlyDictionary<string, string> labels, TrackingEntity entity)
    {
        var key = entry.KeyStatut;
        var libelle = key is not null && labels.TryGetValue(key, out var found) ? found : null;

        return new TrackingEventDto(
            entity, entry.EntityKey, key, libelle,
            // `$`-prefixed codes are the system events the legacy UI hid from users.
            key?.StartsWith('$') ?? false,
            entry.DateTraceUser, entry.DateTraceSystem, entry.Commentaire, entry.Utilisateur,
            entry.KeyDocument, entry.Valeur, entry.PosteUtilisateur, entry.Agence, entry.Societe);
    }

    /// <summary>
    /// Recovers which trace table a row came from. The runtime type is the only discriminator available,
    /// because the legacy tables carry no discriminator column.
    /// </summary>
    private static TrackingEntity ResolveEntity(TraceEntry entry) => entry switch
    {
        TraceClient => TrackingEntity.Client,
        TraceVoyage => TrackingEntity.Voyage,
        TraceDossier => TrackingEntity.Dossier,
        TraceFactureVente => TrackingEntity.FactureVente,
        TraceReglement => TrackingEntity.Reglement,
        TraceBooking => TrackingEntity.Booking,
        TraceCotation => TrackingEntity.Cotation,
        _ => throw new BusinessRuleException("tracking.entity", $"Type de trace inconnu : {entry.GetType().Name}.")
    };

    private static int Clamp(int take) => take is < 1 or > 500 ? 100 : take;

    private static void GuardEntityKey(string entityKey)
    {
        if (string.IsNullOrWhiteSpace(entityKey))
        {
            throw new ArgumentException("La clé de l'entité est obligatoire.", nameof(entityKey));
        }
    }
}
