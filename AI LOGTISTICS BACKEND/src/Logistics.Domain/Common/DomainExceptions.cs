namespace Logistics.Domain.Common;

/// <summary>
/// Base type for all expected, business-level failures. The API layer translates these into
/// RFC 7807 <c>ProblemDetails</c> responses instead of HTTP 500s.
/// </summary>
public abstract class DomainException(string message) : Exception(message)
{
    /// <summary>Stable machine-readable code surfaced to API clients.</summary>
    public abstract string Code { get; }
}

/// <summary>The requested aggregate does not exist.</summary>
public sealed class EntityNotFoundException(string entity, string key)
    : DomainException($"'{entity}' with key '{key}' was not found.")
{
    public override string Code => "not_found";
    public string Entity { get; } = entity;
    public string Key { get; } = key;
}

/// <summary>A business invariant was violated (legacy equivalent: a validation MsgBox + refused save).</summary>
public sealed class BusinessRuleException(string rule, string message) : DomainException(message)
{
    public override string Code => "business_rule_violation";
    public string Rule { get; } = rule;
}

/// <summary>A uniqueness constraint enforced by the application (legacy: the <c>Existe()</c> helpers).</summary>
public sealed class DuplicateEntityException(string entity, string field, string value)
    : DomainException($"A '{entity}' with {field} '{value}' already exists.")
{
    public override string Code => "duplicate";
    public string Entity { get; } = entity;
    public string Field { get; } = field;
    public string Value { get; } = value;
}

/// <summary>
/// The caller is authenticated but not allowed to do this.
/// </summary>
/// <remarks>
/// Distinct from a business-rule violation on purpose: this must surface as <b>403</b>, not 422. A rule
/// violation means the request was understood and refused; a forbidden response means the caller should not
/// have been attempting it at all, and conflating the two hides authorisation failures inside validation
/// noise.
/// </remarks>
public sealed class ForbiddenException(string permission, string message) : DomainException(message)
{
    public override string Code => "forbidden";
    /// <summary>The permission that was required.</summary>
    public string Permission { get; } = permission;
}

/// <summary>
/// One or more submitted field values are unusable.
/// </summary>
/// <remarks>
/// <para>
/// Carries the same <c>validation_failed</c> code as the model-binding path, so a caller sees one error
/// vocabulary whether a field was rejected during deserialization or at the persistence boundary — the two
/// are the same class of problem and should not need different handling.
/// </para>
/// <para>
/// Exists because a value longer than its legacy column previously surfaced as a SQL truncation error,
/// which <c>DbUpdateException</c> reported as <b>409 Persistence conflict</b>. That is wrong twice over: a
/// caller cannot fix it by retrying, which 409 implies, and the generic message named no field at all.
/// </para>
/// </remarks>
public sealed class ChampInvalideException : DomainException
{
    public ChampInvalideException(IReadOnlyDictionary<string, string[]> erreurs)
        : base($"One or more fields are invalid: {string.Join(", ", erreurs.Keys.OrderBy(k => k))}.")
    {
        Erreurs = erreurs;
    }

    public ChampInvalideException(string champ, string message)
        : this(new Dictionary<string, string[]> { [champ] = [message] })
    {
    }

    public override string Code => "validation_failed";

    /// <summary>Field name to the problems with it, mirroring the model-binding error shape.</summary>
    public IReadOnlyDictionary<string, string[]> Erreurs { get; }
}

/// <summary>Optimistic-concurrency failure.</summary>
public sealed class ConcurrencyConflictException(string entity, string key)
    : DomainException($"'{entity}' with key '{key}' was modified by another user."){
    public override string Code => "concurrency_conflict";
}
