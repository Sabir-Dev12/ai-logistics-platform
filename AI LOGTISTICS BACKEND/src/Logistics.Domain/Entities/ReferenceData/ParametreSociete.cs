using Logistics.Domain.Common;

namespace Logistics.Domain.Entities.ReferenceData;

/// <summary>
/// One company setting (<c>t_parametre_societe</c>).
/// </summary>
/// <remarks>
/// <para>
/// A <b>key/value</b> table, not a wide one: one row per (company, setting). The primary key is
/// <c>(key_soc, key_parametre)</c> and the tenant column is <c>key_soc</c> — note that the sibling
/// <c>t_societe</c> and <c>t_franchise_cotation</c> use different tenant spellings, so this must not be
/// assumed uniform.
/// </para>
/// <para>
/// <b>An absent row is not the same as a false value.</b> Several settings are tested by the legacy code as
/// <c>valeur_parametre &lt;&gt; 'Non'</c>, which means a <em>missing</em> row evaluates to <b>true</b> —
/// mandatory, not optional. That asymmetry is the single most important thing about this table, and it is
/// why the accessor exposes two distinct boolean readings rather than one.
/// </para>
/// <para>
/// Values are stored as <b>text</b> for everything: booleans are <c>Oui</c>/<c>Non</c> (a few
/// <c>1</c>/<c>0</c>), numbers are numeric strings the caller converts, and some are not values at all but
/// <b>SQL predicate fragments</b> spliced into <c>WHERE</c> clauses. Those last ones cannot be modelled as
/// typed settings; see <see cref="SetCodes.ParametresFragmentsSql"/>.
/// </para>
/// </remarks>
public sealed class ParametreSociete : Entity, ITenantScoped
{
    private ParametreSociete() { }

    public ParametreSociete(string societeKey, string keyParametre, string? valeur, string? libelle = null)
    {
        SocieteKey = Require(societeKey, nameof(societeKey));
        KeyParametre = Require(keyParametre, nameof(keyParametre));
        ValeurParametre = Trim(valeur);
        LibelleParametre = Trim(libelle);

        // Synthetic and unmapped: the table's key is the pair.
        Id = $"{societeKey}:{KeyParametre}";
    }

    /// <summary>Tenant column is <c>key_soc</c> — not <c>societe</c>.</summary>
    public string SocieteKey { get; private set; } = string.Empty;

    /// <summary>Setting code. Free-form and mixed-case in the legacy data; matched case-insensitively.</summary>
    public string KeyParametre { get; private set; } = string.Empty;

    /// <summary>Human label. The legacy code never reads it back, but the column exists.</summary>
    public string? LibelleParametre { get; private set; }

    /// <summary>The setting's value as text — possibly a boolean, a number, or a SQL fragment.</summary>
    public string? ValeurParametre { get; private set; }

    public bool EstVide => string.IsNullOrWhiteSpace(ValeurParametre);

    /// <summary>
    /// Reads the value the way a <c>= "Oui"</c> comparison does: absent or anything else is false.
    /// </summary>
    public bool CommeOui() => string.Equals(ValeurParametre, "Oui", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads the value the way a <c>&lt;&gt; "Non"</c> comparison does.
    /// </summary>
    /// <remarks>
    /// <b>An absent row yields true.</b> That is the legacy behaviour, not an oversight: a company that never
    /// set <c>param_oblig_port</c> gets a mandatory port, because the check is "is it specifically Non?".
    /// Reading this as false would silently relax a required field for every company that never configured
    /// it.
    /// </remarks>
    public bool CommeNonDefaut() => !string.Equals(ValeurParametre, "Non", StringComparison.OrdinalIgnoreCase);

    /// <summary>Parses the value as a number, returning null when absent or unparseable.</summary>
    public decimal? CommeNombre() =>
        decimal.TryParse(ValeurParametre, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var nombre)
            ? nombre
            : null;

    public void SetValeur(string? valeur, string? libelle = null)
    {
        ValeurParametre = Trim(valeur);
        LibelleParametre = Trim(libelle) ?? LibelleParametre;
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// The company-parameter codes this backend reads, named rather than inlined.
/// </summary>
/// <remarks>
/// 212 codes exist in total; only the handful the API's own behaviour depends on are listed. The legacy
/// spellings are preserved verbatim, including the mixed casing and the typos, because they are matched as
/// literals against a case-insensitive column — a "corrected" spelling would simply never match.
/// </remarks>
public static class SetCodes
{
    /// <summary><c>= "Oui"</c>. Groupage quotations must carry packing detail before they can be sent.</summary>
    public const string ParamObligCloisage = "param_oblig_cloisage";

    /// <summary>
    /// <c>&lt;&gt; "Non"</c>. A loading port is required when a planned departure date is set —
    /// <b>and an absent row means required</b>.
    /// </summary>
    public const string ParamObligPort = "param_oblig_port";

    /// <summary><c>= "Oui"</c>, only for type <c>RE</c> bookings.</summary>
    public const string ParamObligProvenance = "param_oblig_provenance";

    /// <summary><c>= "Oui"</c>, only for type <c>RE</c> bookings.</summary>
    public const string ParamObligDestination = "param_oblig_destination";

    /// <summary>
    /// <c>= "Oui"</c> means the booking number is generated; <c>"Non"</c> makes it the user's to supply.
    /// </summary>
    public const string GenerNumBooking = "gener_num_booking";

    /// <summary>
    /// The freight base used to resolve percentage price lines.
    /// </summary>
    /// <remarks>
    /// <b>Not a value.</b> Its content is a SQL predicate fragment spliced after <c>… and code_rubrique </c>,
    /// e.g. <c>in ('FR','F1')</c>. It is named here so the backend can refuse to treat it as a typed setting
    /// rather than silently reading the fragment as data.
    /// </remarks>
    public const string BaseFret = "base_fret";

    /// <summary>
    /// Codes whose value is a SQL fragment rather than data.
    /// </summary>
    /// <remarks>
    /// Recorded explicitly so no code path mistakes one for a value. Splicing these into a query — as the
    /// legacy application does — would be a SQL-injection vector in an API that accepts them over HTTP, so
    /// this backend does not implement them.
    /// </remarks>
    public static readonly IReadOnlySet<string> ParametresFragmentsSql = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        BaseFret,
        "ch_comm",
        "GetChaffeurChargeur",
        "critere_nature_serv_fact",
        "critere_type_frs_sans_RC",
        "ch_agent_recouv",
        "param_user_commercial",
        "param_lieu_dedouanement",
        "Incoterm_exclus_PASSAGE",
        "repart_facture_ach",
        "mat_sortie_gasoil",
        "GetTypeVisa"
    };
}
