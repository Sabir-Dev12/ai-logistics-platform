using Logistics.Domain.Common;
using Logistics.Domain.Entities.ReferenceData;

namespace Logistics.Domain.Entities.Quotations;

/// <summary>Lifecycle of a purchase tariff scale (<c>t_entete_bareme_achat_cotation.Statut</c>).</summary>
public enum StatutBareme
{
    NonValide,
    Valide,
    Supprime
}

/// <summary>Complet / Groupage (<c>type_bareme</c>).</summary>
public enum TypeBareme
{
    Complet,
    Groupage
}

/// <summary>National / International (<c>flux</c>).</summary>
public enum FluxBareme
{
    National,
    International
}

/// <summary>
/// Purchase tariff scale header (<c>t_entete_bareme_achat_cotation</c>) — a grid of tranche-based
/// purchase prices per department, used to cost quotations.
/// </summary>
/// <remarks>
/// <para>
/// Tenant column is <c>soc_bareme</c>, not <c>societe</c> — the third distinct tenant column name in the
/// cotation module alone (<c>soc_cotation</c>, <c>societe</c>, <c>soc_bareme</c>).
/// </para>
/// <para>
/// The lifecycle is deliberately one-way: a scale is created <see cref="StatutBareme.NonValide"/> and
/// becomes <see cref="StatutBareme.Valide"/> through validation, after which <b>only a validated scale
/// may be modified</b>. Nothing returns it to draft, so a price already used on a quotation cannot be
/// quietly edited underneath it.
/// </para>
/// </remarks>
public sealed class BaremeAchatCotation : Entity, ITenantScoped
{
    private readonly List<ColonneBaremeAchat> _colonnes = [];
    private readonly List<DetailBaremeAchat> _details = [];
    private readonly List<PrestationBaremeAchat> _prestations = [];

    private BaremeAchatCotation() { }

    public BaremeAchatCotation(string keyBareme, string societeKey, string? libelleBareme, string? fournisseur)
    {
        Id = Require(keyBareme, nameof(keyBareme));
        SocieteKey = Require(societeKey, nameof(societeKey));
        LibelleBareme = Trim(libelleBareme);
        Fournisseur = Trim(fournisseur);
        Statut = StatutBareme.NonValide;
    }

    public string KeyBareme => Id;
    public string SocieteKey { get; private set; } = string.Empty;
    public string? LibelleBareme { get; private set; }
    public TypeBareme? TypeBareme { get; private set; }
    public string? BaseCalcul { get; private set; }
    public string? Fournisseur { get; private set; }
    public DateTime? DebutValidite { get; private set; }
    public DateTime? FinValidite { get; private set; }
    public StatutBareme Statut { get; private set; } = StatutBareme.NonValide;
    public decimal? Arrondi1 { get; private set; }
    public decimal? Arrondi2 { get; private set; }
    public string? Devise { get; private set; }
    public FluxBareme? Flux { get; private set; }
    public string? Incoterm { get; private set; }

    public IReadOnlyCollection<ColonneBaremeAchat> Colonnes => _colonnes.AsReadOnly();
    public IReadOnlyCollection<DetailBaremeAchat> Details => _details.AsReadOnly();
    public IReadOnlyCollection<PrestationBaremeAchat> Prestations => _prestations.AsReadOnly();

    public bool IsModifiable => Statut == StatutBareme.Valide;

    /// <summary>
    /// The cell value for a (column, department) pair, or null when the grid has no such cell.
    /// </summary>
    public decimal? Montant(int idColonne, string departement) =>
        _details.FirstOrDefault(d => d.IdColonne == idColonne
            && string.Equals(d.Departement, departement, StringComparison.OrdinalIgnoreCase))?.Montant;

    /// <summary>
    /// Picks the column whose tranche contains a value and returns its cell for a department.
    /// </summary>
    /// <remarks>
    /// Tranches are half-open on the upper bound where a bound is set, which is what keeps two adjacent
    /// tranches (0–100, 100–500) from both matching the value 100. Where a scale leaves a bound null the
    /// tranche is open at that end.
    /// </remarks>
    public decimal? Resoudre(string departement, decimal valeur)
    {
        var colonne = _colonnes
            .Where(c => c.Contient(valeur))
            .OrderBy(c => c.IdColonne)
            .FirstOrDefault();

        return colonne is null ? null : Montant(colonne.IdColonne, departement);
    }

    public void SetEntete(
        string? libelleBareme, TypeBareme? typeBareme, string? baseCalcul, string? fournisseur,
        DateTime? debutValidite, DateTime? finValidite, decimal? arrondi1, decimal? arrondi2,
        string? devise, FluxBareme? flux, string? incoterm)
    {
        if (debutValidite.HasValue && finValidite.HasValue && finValidite.Value.Date < debutValidite.Value.Date)
        {
            throw new BusinessRuleException(
                "bareme.validite", "La fin de validité ne peut pas être antérieure au début de validité.");
        }

        LibelleBareme = Trim(libelleBareme);
        TypeBareme = typeBareme;
        BaseCalcul = Trim(baseCalcul);
        Fournisseur = Trim(fournisseur);
        DebutValidite = debutValidite;
        FinValidite = finValidite;
        Arrondi1 = arrondi1;
        Arrondi2 = arrondi2;
        Devise = Trim(devise);
        Flux = flux;
        Incoterm = Trim(incoterm);
    }

    /// <summary>
    /// Validates the scale (Non Validé → Validé).
    /// </summary>
    /// <remarks>
    /// A scale with no column has no tranche to resolve and would silently price everything at null, so
    /// that is refused here rather than discovered on a quotation.
    /// </remarks>
    public void Valider()
    {
        if (Statut == StatutBareme.Supprime)
        {
            throw new BusinessRuleException("bareme.supprime", "Un barème supprimé ne peut pas être validé.");
        }

        if (_colonnes.Count == 0)
        {
            throw new BusinessRuleException("bareme.colonnes", "Un barème doit comporter au moins une colonne.");
        }

        Statut = StatutBareme.Valide;
    }

    /// <summary>Marks the scale deleted. The row itself is archived, not erased.</summary>
    public void MarquerSupprime() => Statut = StatutBareme.Supprime;

    private void EnsureModifiable(string operation)
    {
        if (!IsModifiable)
        {
            throw new BusinessRuleException(
                "bareme.statut",
                $"Seul un barème validé peut être {operation} (statut actuel : {Statut}).");
        }
    }

    public void AddColonne(ColonneBaremeAchat colonne) => _colonnes.Add(colonne);
    public void AddDetail(DetailBaremeAchat detail) => _details.Add(detail);
    public void AddPrestation(PrestationBaremeAchat prestation) => _prestations.Add(prestation);

    public void ClearColonnes() => _colonnes.Clear();
    public void ClearDetails() => _details.Clear();
    public void ClearPrestations() => _prestations.Clear();

    /// <summary>
    /// Removes a column and every detail cell that belongs to it.
    /// </summary>
    /// <remarks>
    /// The legacy code deleted the column's <c>t_detail_bareme_achat_cotation</c> rows alongside it;
    /// leaving them would orphan cells that no column can reach.
    /// </remarks>
    public void RemoveColonne(int idColonne)
    {
        _colonnes.RemoveAll(c => c.IdColonne == idColonne);
        _details.RemoveAll(d => d.IdColonne == idColonne);
    }

    public void RemoveDetail(int idColonne, string departement) =>
        _details.RemoveAll(d => d.IdColonne == idColonne
            && string.Equals(d.Departement, departement, StringComparison.OrdinalIgnoreCase));

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Column (tranche / formula) of a purchase tariff scale (<c>t_colonne_bareme_achat_cotation</c>).
/// Composite key <c>(key_bareme, id_colonne)</c>, no tenant column.
/// </summary>
public sealed class ColonneBaremeAchat : Entity
{
    private ColonneBaremeAchat() { }

    public ColonneBaremeAchat(string keyBareme, int idColonne, string? libelleColonne, decimal? trancheDebut, decimal? trancheFin)
    {
        Id = $"{keyBareme}:{idColonne}";
        KeyBareme = Require(keyBareme, nameof(keyBareme));
        IdColonne = idColonne;
        LibelleColonne = Trim(libelleColonne);
        TrancheDebut = trancheDebut;
        TrancheFin = trancheFin;
    }

    public string KeyBareme { get; private set; } = string.Empty;
    public int IdColonne { get; private set; }
    public string? LibelleColonne { get; private set; }
    public decimal? TrancheDebut { get; private set; }
    public decimal? TrancheFin { get; private set; }
    public string? FormuleCalcul { get; private set; }
    public decimal? MtMin { get; private set; }
    public string? Commentaire { get; private set; }

    /// <summary>
    /// Whether a value falls in this tranche.
    /// </summary>
    /// <remarks>
    /// The upper bound is <b>exclusive</b>. Adjacent tranches (0–100, 100–500) both contain their
    /// boundary if it is inclusive, so the value 100 would resolve to whichever column happened to be
    /// ordered first. A null bound leaves that end open.
    /// </remarks>
    public bool Contient(decimal valeur) =>
        (!TrancheDebut.HasValue || valeur >= TrancheDebut.Value)
        && (!TrancheFin.HasValue || valeur < TrancheFin.Value);

    public void SetContenu(
        string? libelleColonne, decimal? trancheDebut, decimal? trancheFin, string? formuleCalcul,
        decimal? mtMin, string? commentaire)
    {
        if (trancheDebut.HasValue && trancheFin.HasValue && trancheFin.Value < trancheDebut.Value)
        {
            throw new BusinessRuleException(
                "bareme.colonne.tranche", "La fin de tranche ne peut pas être inférieure au début de tranche.");
        }

        LibelleColonne = Trim(libelleColonne);
        TrancheDebut = trancheDebut;
        TrancheFin = trancheFin;
        FormuleCalcul = Trim(formuleCalcul);
        MtMin = mtMin;
        Commentaire = Trim(commentaire);
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// One cell of a purchase tariff scale (<c>t_detail_bareme_achat_cotation</c>) — the amount for a
/// (column, department) pair. Composite key <c>(key_bareme, id_colonne, departement)</c>, no tenant.
/// </summary>
public sealed class DetailBaremeAchat : Entity
{
    private DetailBaremeAchat() { }

    public DetailBaremeAchat(string keyBareme, int idColonne, string departement, decimal? montant)
    {
        Id = $"{keyBareme}:{idColonne}:{departement}";
        KeyBareme = Require(keyBareme, nameof(keyBareme));
        IdColonne = idColonne;
        Departement = Require(departement, nameof(departement));
        Montant = montant;
    }

    public string KeyBareme { get; private set; } = string.Empty;
    public int IdColonne { get; private set; }
    /// <summary>Joins <c>T_DEPARTEMENT_REGION.departement</c>, which itself maps to a country.</summary>
    public string Departement { get; private set; } = string.Empty;
    public decimal? Montant { get; private set; }

    public void SetMontant(decimal? montant) => Montant = montant;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();
}

/// <summary>
/// Purchase-prestation line of a scale (<c>T_PRESTATION_BAREME_ACHAT_COTATION</c>).
/// </summary>
/// <remarks>
/// The tenant column here is <c>societe</c> and holds the <em>rubrique's</em> company rather than the
/// scale owner's — a subtlety that matters when a scale references a rubrique from another company.
/// </remarks>
public sealed class PrestationBaremeAchat : Entity, ITenantScoped
{
    private PrestationBaremeAchat() { }

    public PrestationBaremeAchat(string keyLigne, string keyBareme, string societeKey)
    {
        Id = Require(keyLigne, nameof(keyLigne));
        KeyBareme = Require(keyBareme, nameof(keyBareme));
        SocieteKey = Require(societeKey, nameof(societeKey));
    }

    public string KeyLigne => Id;
    public string KeyBareme { get; private set; } = string.Empty;
    public string SocieteKey { get; private set; } = string.Empty;
    public string? CodeRubrique { get; private set; }
    public string? LibelleRubrique { get; private set; }
    public decimal? MtDevise { get; private set; }
    public string? Devise { get; private set; }
    public string? BaseCalcul { get; private set; }

    public void SetContenu(string? codeRubrique, string? libelleRubrique, decimal? mtDevise, string? devise, string? baseCalcul)
    {
        CodeRubrique = Trim(codeRubrique);
        LibelleRubrique = Trim(libelleRubrique);
        MtDevise = mtDevise;
        Devise = Trim(devise);
        BaseCalcul = Trim(baseCalcul);
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Archive of a scale header (<c>t_hist_entete_bareme_achat_cotation</c>).
/// </summary>
/// <remarks>
/// Archiving happens <em>before</em> a physical delete or a "changement tarif", and the archived row
/// always carries <c>Statut='Supprimé'</c>. The key is reused rather than versioned, so the history holds
/// the same <c>key_bareme</c> as its source — meaning an archive is only meaningful alongside the trace.
/// </remarks>
public sealed class HistBaremeAchat : Entity, ITenantScoped
{
    private HistBaremeAchat() { }

    public HistBaremeAchat(string keyBareme, string? societeKey)
    {
        Id = Require(keyBareme, nameof(keyBareme));
        SocieteKey = Trim(societeKey) ?? string.Empty;
    }

    public string KeyBareme => Id;
    public string SocieteKey { get; private set; } = string.Empty;
    public string? LibelleBareme { get; private set; }
    public string? TypeBareme { get; private set; }
    public string? BaseCalcul { get; private set; }
    public string? Fournisseur { get; private set; }
    public DateTime? DebutValidite { get; private set; }
    public DateTime? FinValidite { get; private set; }
    /// <summary>Always <c>Supprimé</c> in the legacy archive path.</summary>
    public string? Statut { get; private set; }
    public decimal? Arrondi1 { get; private set; }
    public decimal? Arrondi2 { get; private set; }
    public string? Devise { get; private set; }
    public string? Flux { get; private set; }
    public string? Incoterm { get; private set; }

    /// <summary>Creates the archive of a live scale, forcing the historical status.</summary>
    public static HistBaremeAchat FromSource(BaremeAchatCotation source) =>
        new(source.KeyBareme, source.SocieteKey)
        {
            LibelleBareme = source.LibelleBareme,
            TypeBareme = source.TypeBareme?.ToString(),
            BaseCalcul = source.BaseCalcul,
            Fournisseur = source.Fournisseur,
            DebutValidite = source.DebutValidite,
            FinValidite = source.FinValidite,
            Statut = "Supprimé",
            Arrondi1 = source.Arrondi1,
            Arrondi2 = source.Arrondi2,
            Devise = source.Devise,
            Flux = source.Flux?.ToString(),
            Incoterm = source.Incoterm
        };

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Archive of a scale column (<c>T_HIST_COLONNE_BAREME_ACHAT_COTATION</c>).
/// </summary>
/// <remarks>
/// The legacy archive INSERT deliberately <b>omits</b> <c>commentaire</c> — the live table has it, the
/// history table does not. That omission is reproduced here rather than "fixed", because the archive
/// table genuinely has no such column.
/// </remarks>
public sealed class HistColonneBaremeAchat : Entity
{
    private HistColonneBaremeAchat() { }

    public HistColonneBaremeAchat(string keyBareme, int idColonne)
    {
        Id = $"{keyBareme}:{idColonne}";
        KeyBareme = Require(keyBareme, nameof(keyBareme));
        IdColonne = idColonne;
    }

    public string KeyBareme { get; private set; } = string.Empty;
    public int IdColonne { get; private set; }
    public string? LibelleColonne { get; private set; }
    public decimal? TrancheDebut { get; private set; }
    public decimal? TrancheFin { get; private set; }
    public string? FormuleCalcul { get; private set; }
    public decimal? MtMin { get; private set; }

    public static HistColonneBaremeAchat FromSource(ColonneBaremeAchat source) =>
        new(source.KeyBareme, source.IdColonne)
        {
            LibelleColonne = source.LibelleColonne,
            TrancheDebut = source.TrancheDebut,
            TrancheFin = source.TrancheFin,
            FormuleCalcul = source.FormuleCalcul,
            MtMin = source.MtMin
            // `commentaire` is intentionally not carried: the history table has no such column.
        };

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();
}

/// <summary>Archive of a scale cell (<c>T_HIST_DETAIL_BAREME_ACHAT_COTATION</c>).</summary>
public sealed class HistDetailBaremeAchat : Entity
{
    private HistDetailBaremeAchat() { }

    public HistDetailBaremeAchat(string keyBareme, int idColonne, string departement, decimal? montant)
    {
        Id = $"{keyBareme}:{idColonne}:{departement}";
        KeyBareme = Require(keyBareme, nameof(keyBareme));
        IdColonne = idColonne;
        Departement = Require(departement, nameof(departement));
        Montant = montant;
    }

    public string KeyBareme { get; private set; } = string.Empty;
    public int IdColonne { get; private set; }
    public string Departement { get; private set; } = string.Empty;
    public decimal? Montant { get; private set; }

    public static HistDetailBaremeAchat FromSource(DetailBaremeAchat source) =>
        new(source.KeyBareme, source.IdColonne, source.Departement, source.Montant);

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();
}

/// <summary>Archive of a scale prestation (<c>T_HIST_PRESTATION_BAREME_ACHAT_COTATION</c>).</summary>
public sealed class HistPrestationBaremeAchat : Entity, ITenantScoped
{
    private HistPrestationBaremeAchat() { }

    public HistPrestationBaremeAchat(string keyBareme, string keyLigne, string? societeKey)
    {
        Id = $"{keyBareme}:{keyLigne}";
        KeyBareme = Require(keyBareme, nameof(keyBareme));
        KeyLigne = Require(keyLigne, nameof(keyLigne));
        SocieteKey = Trim(societeKey) ?? string.Empty;
    }

    public string KeyBareme { get; private set; } = string.Empty;
    public string KeyLigne { get; private set; } = string.Empty;
    public string SocieteKey { get; private set; } = string.Empty;
    public string? CodeRubrique { get; private set; }
    public string? LibelleRubrique { get; private set; }
    public decimal? MtDevise { get; private set; }
    public string? Devise { get; private set; }
    public string? BaseCalcul { get; private set; }

    public static HistPrestationBaremeAchat FromSource(PrestationBaremeAchat source) =>
        new(source.KeyBareme, source.KeyLigne, source.SocieteKey)
        {
            CodeRubrique = source.CodeRubrique,
            LibelleRubrique = source.LibelleRubrique,
            MtDevise = source.MtDevise,
            Devise = source.Devise,
            BaseCalcul = source.BaseCalcul
        };

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
