using Logistics.Application.Abstractions;
using Logistics.Domain.Common;
using Logistics.Domain.Entities.Audit;
using Logistics.Domain.Entities.Quotations;

namespace Logistics.Application.Features.Quotations;

/// <summary>
/// Legacy <c>T_TRACE_BAREME_ACHAT_COTATION.key_statut</c> codes.
/// </summary>
/// <remarks>
/// Only <see cref="Suppression"/> (<c>$STACH</c>) was directly observed in the source. The others follow
/// the application-wide convention — <c>$A…</c> for ajout, <c>$M…</c> for modification, <c>$S…</c> for
/// suppression, with the entity abbreviation appended — but were not seen literally. They are collected
/// here so a single reconciliation against the real <c>t_statut</c> rows fixes all of them at once.
/// </remarks>
public static class BaremeAchatTrace
{
    /// <summary>Observed in <c>frm_Liste_Tarif_Achat.vb</c>.</summary>
    public const string Suppression = "$STACH";

    // Convention-derived, pending confirmation against t_statut:
    public const string Creation = "$ATACH";
    public const string Modification = "$MTACH";
    public const string Validation = "$VTACH";
    public const string Duplication = "$DTACH";
}

public sealed record ColonneBaremeAchatDto(
    int IdColonne, string? LibelleColonne, decimal? TrancheDebut, decimal? TrancheFin,
    string? FormuleCalcul, decimal? MtMin, string? Commentaire);

public sealed record DetailBaremeAchatDto(int IdColonne, string Departement, decimal? Montant);

public sealed record PrestationBaremeAchatDto(
    string KeyLigne, string? CodeRubrique, string? LibelleRubrique, decimal? MtDevise, string? Devise, string? BaseCalcul);

public sealed record BaremeAchatDto(
    string KeyBareme, string? LibelleBareme, string? TypeBareme, string? BaseCalcul, string? Fournisseur,
    DateTime? DebutValidite, DateTime? FinValidite, StatutBareme Statut, bool IsModifiable,
    decimal? Arrondi1, decimal? Arrondi2, string? Devise, string? Flux, string? Incoterm,
    IReadOnlyList<ColonneBaremeAchatDto> Colonnes,
    IReadOnlyList<DetailBaremeAchatDto> Details,
    IReadOnlyList<PrestationBaremeAchatDto> Prestations);

public sealed record BaremeAchatSummaryDto(
    string KeyBareme, string? LibelleBareme, string? TypeBareme, string? Fournisseur,
    DateTime? DebutValidite, DateTime? FinValidite, StatutBareme Statut, string? Flux, string? Devise,
    int NbColonnes, int NbPrestations);

/// <summary>Result of pricing a value against a scale.</summary>
public sealed record TarifBaremeDto(
    string KeyBareme, string Departement, decimal Valeur, int? IdColonne, string? LibelleColonne,
    decimal? Montant, string? Devise, bool Trouve);

public sealed record CreateBaremeAchatRequest(
    string? LibelleBareme,
    TypeBareme? TypeBareme = null,
    string? BaseCalcul = null,
    string? Fournisseur = null,
    DateTime? DebutValidite = null,
    DateTime? FinValidite = null,
    decimal? Arrondi1 = null,
    decimal? Arrondi2 = null,
    string? Devise = null,
    FluxBareme? Flux = null,
    string? Incoterm = null,
    IReadOnlyList<ColonneBaremeAchatInput>? Colonnes = null);

public sealed record ColonneBaremeAchatInput(
    int IdColonne, string? LibelleColonne, decimal? TrancheDebut, decimal? TrancheFin,
    string? FormuleCalcul = null, decimal? MtMin = null, string? Commentaire = null);

public sealed record DetailBaremeAchatInput(int IdColonne, string Departement, decimal? Montant);

public sealed record PrestationBaremeAchatInput(
    string? CodeRubrique, string? LibelleRubrique, decimal? MtDevise, string? Devise, string? BaseCalcul = null);

public sealed record DuplicateBaremeAchatRequest(string? LibelleBareme = null);

public sealed record BaremeAchatSearchRequest(
    string? Libelle = null,
    StatutBareme? Statut = null,
    string? Fournisseur = null,
    TypeBareme? TypeBareme = null,
    FluxBareme? Flux = null,
    bool? Modifiables = null,
    int Page = 1,
    int PageSize = 25);

public interface IBaremeAchatRepository
{
    Task<BaremeAchatCotation?> FindAsync(string keyBareme, bool includeChildren, CancellationToken cancellationToken = default);
    Task<PagedResult<BaremeAchatSummaryDto>> SearchAsync(BaremeAchatSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a scale with this label already exists.
    /// </summary>
    /// <remarks>
    /// The legacy uniqueness check is <c>UPPER(dbo.delete_caractere_speciaux(libelle_bareme))</c> — a
    /// database function that strips punctuation before comparing. That function is not available here,
    /// so this is a case-insensitive comparison <b>without</b> punctuation stripping, which is stricter
    /// (it will not treat "Tarif A" and "Tarif-A" as equal). Flagged rather than silently approximated.
    /// </remarks>
    Task<bool> LibelleExistsAsync(string libelleBareme, string? excludeKeyBareme = null, CancellationToken cancellationToken = default);

    void Add(BaremeAchatCotation bareme);
    void Add(ColonneBaremeAchat colonne);
    void Add(DetailBaremeAchat detail);
    void Add(PrestationBaremeAchat prestation);
    void Add(HistBaremeAchat archive);
    void Add(HistColonneBaremeAchat archive);
    void Add(HistDetailBaremeAchat archive);
    void Add(HistPrestationBaremeAchat archive);
    void Remove(ColonneBaremeAchat colonne);
    void Remove(DetailBaremeAchat detail);
    void Remove(PrestationBaremeAchat prestation);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IBaremeAchatService
{
    Task<PagedResult<BaremeAchatSummaryDto>> SearchAsync(BaremeAchatSearchRequest request, CancellationToken cancellationToken = default);
    Task<BaremeAchatDto> GetAsync(string keyBareme, CancellationToken cancellationToken = default);
    Task<BaremeAchatDto> CreateAsync(CreateBaremeAchatRequest request, CancellationToken cancellationToken = default);
    Task<BaremeAchatDto> UpdateAsync(string keyBareme, CreateBaremeAchatRequest request, CancellationToken cancellationToken = default);
    Task<BaremeAchatDto> ValiderAsync(string keyBareme, CancellationToken cancellationToken = default);
    Task<BaremeAchatDto> DuplicateAsync(string keyBareme, DuplicateBaremeAchatRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(string keyBareme, CancellationToken cancellationToken = default);
    Task<BaremeAchatDto> SetColonnesAsync(string keyBareme, IReadOnlyList<ColonneBaremeAchatInput> colonnes, CancellationToken cancellationToken = default);
    Task<BaremeAchatDto> SetDetailsAsync(string keyBareme, IReadOnlyList<DetailBaremeAchatInput> details, CancellationToken cancellationToken = default);
    Task<BaremeAchatDto> SetPrestationsAsync(string keyBareme, IReadOnlyList<PrestationBaremeAchatInput> prestations, CancellationToken cancellationToken = default);
    Task<TarifBaremeDto> ResoudreAsync(string keyBareme, string departement, decimal valeur, CancellationToken cancellationToken = default);
}

/// <summary>
/// Purchase tariff scale use cases — the port of <c>frm_Codification_Tarif_Achat.vb</c>,
/// <c>frm_Liste_Tarif_Achat.vb</c> and <c>frm_Duplication_Tarif_Achat.vb</c>.
/// </summary>
public sealed class BaremeAchatService(
    IBaremeAchatRepository baremes,
    IKeyGenerator keys,
    IAuditTrail audit,
    ITenantContext tenant) : IBaremeAchatService
{
    public Task<PagedResult<BaremeAchatSummaryDto>> SearchAsync(BaremeAchatSearchRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Page < 1)
        {
            throw new ArgumentException("La page doit être supérieure ou égale à 1.", nameof(request));
        }

        return baremes.SearchAsync(request with { PageSize = Math.Clamp(request.PageSize, 1, 200) }, cancellationToken);
    }

    public async Task<BaremeAchatDto> GetAsync(string keyBareme, CancellationToken cancellationToken = default) =>
        Map(await RequireAsync(keyBareme, cancellationToken));

    /// <summary>
    /// Creates a scale. It starts <see cref="StatutBareme.NonValide"/> and is not usable until validated.
    /// </summary>
    public async Task<BaremeAchatDto> CreateAsync(CreateBaremeAchatRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.LibelleBareme))
        {
            throw new BusinessRuleException("bareme.libelle", "Le libellé du barème est obligatoire.");
        }

        await EnsureLibelleLibreAsync(request.LibelleBareme, null, cancellationToken);

        var key = await keys.NextAsync("BAREME_ACHAT", cancellationToken);
        var bareme = new BaremeAchatCotation(key, tenant.SocieteKey, request.LibelleBareme, request.Fournisseur);

        ApplyEntete(bareme, request);

        // Initial tranches are part of creation: subsequent grid changes require validation.
        var initialColumns = request.Colonnes ?? [];
        if (initialColumns.Any(c => c.IdColonne <= 0))
            throw new BusinessRuleException("bareme.colonnes.numero", "Le numéro de colonne doit être positif.");
        if (initialColumns.Select(c => c.IdColonne).Distinct().Count() != initialColumns.Count)
            throw new BusinessRuleException("bareme.colonnes.doublon", "Les numéros de colonnes doivent être uniques.");
        foreach (var input in initialColumns)
        {
            var colonne = new ColonneBaremeAchat(key, input.IdColonne, input.LibelleColonne,
                input.TrancheDebut, input.TrancheFin);
            colonne.SetContenu(input.LibelleColonne, input.TrancheDebut, input.TrancheFin,
                input.FormuleCalcul, input.MtMin, input.Commentaire);
            bareme.AddColonne(colonne);
        }

        baremes.Add(bareme);
        await baremes.SaveChangesAsync(cancellationToken);

        await TraceAsync(key, BaremeAchatTrace.Creation, $"Création du barème {request.LibelleBareme}", cancellationToken);

        return Map(await RequireAsync(key, cancellationToken));
    }

    public async Task<BaremeAchatDto> UpdateAsync(string keyBareme, CreateBaremeAchatRequest request, CancellationToken cancellationToken = default)
    {
        var bareme = await RequireAsync(keyBareme, cancellationToken);
        EnsureModifiable(bareme, "modifié");

        if (!string.IsNullOrWhiteSpace(request.LibelleBareme))
        {
            await EnsureLibelleLibreAsync(request.LibelleBareme, keyBareme, cancellationToken);
        }

        ApplyEntete(bareme, request);

        await baremes.SaveChangesAsync(cancellationToken);
        await TraceAsync(keyBareme, BaremeAchatTrace.Modification, null, cancellationToken);

        return Map(await RequireAsync(keyBareme, cancellationToken));
    }

    public async Task<BaremeAchatDto> ValiderAsync(string keyBareme, CancellationToken cancellationToken = default)
    {
        var bareme = await RequireAsync(keyBareme, cancellationToken);
        bareme.Valider();

        await baremes.SaveChangesAsync(cancellationToken);
        await TraceAsync(keyBareme, BaremeAchatTrace.Validation, "Validation du barème", cancellationToken);

        return Map(await RequireAsync(keyBareme, cancellationToken));
    }

    /// <summary>
    /// Archives the scale and all of its lines, and marks it deleted.
    /// </summary>
    /// <remarks>
    /// The legacy delete never removed rows: it copied the header and every child into the
    /// <c>t_hist_*</c>/<c>T_HIST_*</c> tables with <c>Statut='Supprimé'</c>, then set the live status.
    /// The archive reuses the source <c>key_bareme</c>, so a second deletion of the same key would
    /// collide on the archive primary key — which is why the trace table, not the archive, is what
    /// distinguishes generations.
    /// </remarks>
    public async Task DeleteAsync(string keyBareme, CancellationToken cancellationToken = default)
    {
        var bareme = await RequireAsync(keyBareme, cancellationToken);

        if (bareme.Statut == StatutBareme.Supprime)
        {
            return;
        }

        Archive(bareme);
        bareme.MarquerSupprime();

        await baremes.SaveChangesAsync(cancellationToken);
        await TraceAsync(keyBareme, BaremeAchatTrace.Suppression, null, cancellationToken);
    }

    /// <summary>
    /// Duplicates a scale, archiving the source first.
    /// </summary>
    /// <remarks>
    /// The legacy duplication archives the source before copying, and the copy starts
    /// <see cref="StatutBareme.NonValide"/> so a duplicated price is never immediately live.
    /// </remarks>
    public async Task<BaremeAchatDto> DuplicateAsync(string keyBareme, DuplicateBaremeAchatRequest request, CancellationToken cancellationToken = default)
    {
        var source = await RequireAsync(keyBareme, cancellationToken);
        EnsureModifiable(source, "dupliqué");

        var libelle = string.IsNullOrWhiteSpace(request.LibelleBareme)
            ? $"{source.LibelleBareme} (copie)"
            : request.LibelleBareme.Trim();

        await EnsureLibelleLibreAsync(libelle, null, cancellationToken);

        Archive(source);

        var key = await keys.NextAsync("BAREME_ACHAT", cancellationToken);
        var copy = new BaremeAchatCotation(key, tenant.SocieteKey, libelle, source.Fournisseur);
        copy.SetEntete(libelle, source.TypeBareme, source.BaseCalcul, source.Fournisseur, source.DebutValidite,
            source.FinValidite, source.Arrondi1, source.Arrondi2, source.Devise, source.Flux, source.Incoterm);

        baremes.Add(copy);

        foreach (var colonne in source.Colonnes)
        {
            var ligne = new ColonneBaremeAchat(key, colonne.IdColonne, colonne.LibelleColonne,
                colonne.TrancheDebut, colonne.TrancheFin);
            ligne.SetContenu(colonne.LibelleColonne, colonne.TrancheDebut, colonne.TrancheFin,
                colonne.FormuleCalcul, colonne.MtMin, colonne.Commentaire);
            baremes.Add(ligne);
        }

        foreach (var detail in source.Details)
        {
            baremes.Add(new DetailBaremeAchat(key, detail.IdColonne, detail.Departement, detail.Montant));
        }

        foreach (var prestation in source.Prestations)
        {
            var ligne = new PrestationBaremeAchat(
                await keys.NextAsync("BAREME_PRESTATION", cancellationToken), key, prestation.SocieteKey);
            ligne.SetContenu(prestation.CodeRubrique, prestation.LibelleRubrique, prestation.MtDevise,
                prestation.Devise, prestation.BaseCalcul);
            baremes.Add(ligne);
        }

        await baremes.SaveChangesAsync(cancellationToken);

        await TraceAsync(key, BaremeAchatTrace.Duplication, $"Duplication depuis {source.LibelleBareme}", cancellationToken);
        await TraceAsync(keyBareme, BaremeAchatTrace.Duplication, $"Dupliqué vers {libelle}", cancellationToken);

        return Map(await RequireAsync(key, cancellationToken));
    }

    /// <summary>
    /// Replaces the columns, dropping the detail cells that belonged to removed columns.
    /// </summary>
    /// <remarks>
    /// A cell whose column no longer exists can never be reached by <c>Resoudre</c>, so leaving it behind
    /// would accumulate dead rows that silently inflate nothing but confuse any grid that reads details
    /// directly. The legacy code deleted them alongside the column.
    /// </remarks>
    public async Task<BaremeAchatDto> SetColonnesAsync(string keyBareme, IReadOnlyList<ColonneBaremeAchatInput> colonnes, CancellationToken cancellationToken = default)
    {
        var bareme = await RequireAsync(keyBareme, cancellationToken);
        EnsureModifiable(bareme, "modifié");

        var wanted = colonnes.Select(c => c.IdColonne).ToHashSet();

        foreach (var detail in bareme.Details.Where(d => !wanted.Contains(d.IdColonne)).ToArray())
        {
            baremes.Remove(detail);
        }

        foreach (var existante in bareme.Colonnes.ToArray())
        {
            baremes.Remove(existante);
        }

        foreach (var input in colonnes)
        {
            var ligne = new ColonneBaremeAchat(keyBareme, input.IdColonne, input.LibelleColonne,
                input.TrancheDebut, input.TrancheFin);
            ligne.SetContenu(input.LibelleColonne, input.TrancheDebut, input.TrancheFin,
                input.FormuleCalcul, input.MtMin, input.Commentaire);
            baremes.Add(ligne);
        }

        await baremes.SaveChangesAsync(cancellationToken);
        return Map(await RequireAsync(keyBareme, cancellationToken));
    }

    /// <summary>
    /// Replaces every cell of the grid, as the legacy save did.
    /// </summary>
    public async Task<BaremeAchatDto> SetDetailsAsync(string keyBareme, IReadOnlyList<DetailBaremeAchatInput> details, CancellationToken cancellationToken = default)
    {
        var bareme = await RequireAsync(keyBareme, cancellationToken);
        EnsureModifiable(bareme, "modifié");

        var colonnes = bareme.Colonnes.Select(c => c.IdColonne).ToHashSet();

        if (details.Any(d => !colonnes.Contains(d.IdColonne)))
        {
            throw new BusinessRuleException(
                "bareme.detail.colonne", "Un montant référence une colonne qui n'existe pas sur ce barème.");
        }

        foreach (var existant in bareme.Details.ToArray())
        {
            baremes.Remove(existant);
        }

        foreach (var input in details)
        {
            baremes.Add(new DetailBaremeAchat(keyBareme, input.IdColonne, input.Departement, input.Montant));
        }

        await baremes.SaveChangesAsync(cancellationToken);
        return Map(await RequireAsync(keyBareme, cancellationToken));
    }

    public async Task<BaremeAchatDto> SetPrestationsAsync(string keyBareme, IReadOnlyList<PrestationBaremeAchatInput> prestations, CancellationToken cancellationToken = default)
    {
        var bareme = await RequireAsync(keyBareme, cancellationToken);
        EnsureModifiable(bareme, "modifié");

        foreach (var existante in bareme.Prestations.ToArray())
        {
            baremes.Remove(existante);
        }

        foreach (var input in prestations)
        {
            var ligne = new PrestationBaremeAchat(
                await keys.NextAsync("BAREME_PRESTATION", cancellationToken), keyBareme, tenant.SocieteKey);
            ligne.SetContenu(input.CodeRubrique, input.LibelleRubrique, input.MtDevise, input.Devise, input.BaseCalcul);
            baremes.Add(ligne);
        }

        await baremes.SaveChangesAsync(cancellationToken);
        return Map(await RequireAsync(keyBareme, cancellationToken));
    }

    /// <summary>
    /// Prices a value for a department against the scale's tranches.
    /// </summary>
    /// <remarks>
    /// Returns <c>Trouve = false</c> rather than throwing when no tranche matches, because "this scale
    /// has no price for that weight and destination" is a normal commercial answer, not an error.
    /// </remarks>
    public async Task<TarifBaremeDto> ResoudreAsync(string keyBareme, string departement, decimal valeur, CancellationToken cancellationToken = default)
    {
        var bareme = await RequireAsync(keyBareme, cancellationToken);

        var colonne = bareme.Colonnes
            .Where(c => c.Contient(valeur))
            .OrderBy(c => c.IdColonne)
            .FirstOrDefault();

        var montant = colonne is null ? null : bareme.Montant(colonne.IdColonne, departement);

        return new TarifBaremeDto(keyBareme, departement, valeur, colonne?.IdColonne,
            colonne?.LibelleColonne, montant, bareme.Devise, colonne is not null && montant.HasValue);
    }

    private void Archive(BaremeAchatCotation bareme)
    {
        baremes.Add(HistBaremeAchat.FromSource(bareme));

        foreach (var colonne in bareme.Colonnes)
        {
            baremes.Add(HistColonneBaremeAchat.FromSource(colonne));
        }

        foreach (var detail in bareme.Details)
        {
            baremes.Add(HistDetailBaremeAchat.FromSource(detail));
        }

        foreach (var prestation in bareme.Prestations)
        {
            baremes.Add(HistPrestationBaremeAchat.FromSource(prestation));
        }
    }

    private static void ApplyEntete(BaremeAchatCotation bareme, CreateBaremeAchatRequest request) =>
        bareme.SetEntete(request.LibelleBareme, request.TypeBareme, request.BaseCalcul, request.Fournisseur,
            request.DebutValidite, request.FinValidite, request.Arrondi1, request.Arrondi2,
            request.Devise, request.Flux, request.Incoterm);

    private static void EnsureModifiable(BaremeAchatCotation bareme, string operation)
    {
        if (!bareme.IsModifiable)
        {
            throw new BusinessRuleException(
                "bareme.statut",
                $"Seul un barème validé peut être {operation} (statut actuel : {bareme.Statut}).");
        }
    }

    private async Task EnsureLibelleLibreAsync(string libelle, string? excludeKey, CancellationToken cancellationToken)
    {
        if (await baremes.LibelleExistsAsync(libelle, excludeKey, cancellationToken))
        {
            throw new DuplicateEntityException("BaremeAchat", "libelle_bareme", libelle);
        }
    }

    private async Task<BaremeAchatCotation> RequireAsync(string keyBareme, CancellationToken cancellationToken) =>
        await baremes.FindAsync(keyBareme, true, cancellationToken)
        ?? throw new EntityNotFoundException("BaremeAchat", keyBareme);

    private Task TraceAsync(string key, string code, string? commentaire, CancellationToken cancellationToken) =>
        audit.TraceAsync<TraceBaremeAchat>(key, code, commentaire, null, 0, cancellationToken);

    private static BaremeAchatDto Map(BaremeAchatCotation b) => new(
        b.KeyBareme, b.LibelleBareme, b.TypeBareme?.ToString(), b.BaseCalcul, b.Fournisseur,
        b.DebutValidite, b.FinValidite, b.Statut, b.IsModifiable, b.Arrondi1, b.Arrondi2,
        b.Devise, b.Flux?.ToString(), b.Incoterm,
        b.Colonnes.OrderBy(c => c.IdColonne).Select(c => new ColonneBaremeAchatDto(c.IdColonne,
            c.LibelleColonne, c.TrancheDebut, c.TrancheFin, c.FormuleCalcul, c.MtMin, c.Commentaire)).ToArray(),
        b.Details.OrderBy(d => d.IdColonne).ThenBy(d => d.Departement)
            .Select(d => new DetailBaremeAchatDto(d.IdColonne, d.Departement, d.Montant)).ToArray(),
        b.Prestations.Select(p => new PrestationBaremeAchatDto(p.KeyLigne, p.CodeRubrique, p.LibelleRubrique,
            p.MtDevise, p.Devise, p.BaseCalcul)).ToArray());
}
