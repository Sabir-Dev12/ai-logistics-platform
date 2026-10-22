namespace Logistics.Domain.Common;

/// <summary>
/// Base class for every persisted aggregate/entity.
/// </summary>
/// <remarks>
/// The legacy TransmarNetref schema has no <c>date_creation</c> / <c>date_modification</c> /
/// <c>utilisateur_creation</c> bookkeeping columns on business tables; "who created this and when"
/// is recovered by querying the per-entity <c>t_trace_*</c> table for the creation status code
/// (e.g. <c>$ACLT</c> for a client, <c>$CVOY</c> for a voyage). We therefore deliberately do NOT
/// invent audit columns here — doing so would break a straight data migration from the legacy
/// database. Auditing is recorded in the dedicated audit tables instead (see the Audit entities).
/// </remarks>
public abstract class Entity
{
    public string Id { get; protected set; } = string.Empty;

    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Domain events raised by this entity, awaiting dispatch.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    public override bool Equals(object? obj) =>
        obj is Entity other
        && other.GetType() == GetType()
        && other.Id == Id
        && !string.IsNullOrEmpty(Id);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

public interface IDomainEvent
{
    DateTimeOffset OccurredAtUtc { get; }
}
