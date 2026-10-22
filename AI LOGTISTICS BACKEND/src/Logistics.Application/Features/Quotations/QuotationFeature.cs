using Logistics.Application.Abstractions;
using Logistics.Application.Features.ReferenceData;
using Logistics.Domain.Common;
using Logistics.Domain.Entities.Audit;
using Logistics.Domain.Entities.Quotations;
using Logistics.Domain.Entities.ReferenceData;

namespace Logistics.Application.Features.Quotations;

/// <summary>Legacy <c>t_trace_cotation.key_statut</c> codes.</summary>
public static class CotationTrace
{
    public const string Creation = "$ACOT";
    public const string Modification = "$MCOT";
    public const string Duplication = "$DCOT";
    public const string Suppression = "$SCOT";
    public const string Annulation = "$ANCOT";
    public const string Validation = "$VCOT";
    public const string Gagnee = "$GCOT";
    public const string Perdue = "$PCOT";
    /// <summary>Original side of a counter-offer.</summary>
    public const string ContreOffreSource = "$CTOF";
    /// <summary>Copy side of a counter-offer.</summary>
    public const string ContreOffreCopie = "$CCOF";
    public const string AjoutPrix = "$APCOT";
    public const string ModificationPrix = "$MPCOT";
    public const string SuppressionPrix = "$SPCOT";
    public const string AjoutColisage = "$ADCOT";
    public const string SuppressionColisage = "$SDCOT";
    public const string AjoutAchat = "$AACOT";
    public const string ModificationAchat = "$MACOT";
    public const string SuppressionAchat = "$SACOT";
    public const string AjoutDocument = "$ADOCOT";
    public const string SuppressionDocument = "$SDOCOT";
    public const string EnvoiMail = "$EMC";
}

public sealed record ColisageCotationDto(
    string KeyColisageCotation, string? TypeColis, decimal? Qte, decimal? Poids, bool Gerbable,
    decimal? Longueur, decimal? Largeur, decimal? Hauteur, decimal? Volume, decimal? VolumeNgerbable,
    string? TypeEquipement, string? NatureEquipement, decimal? LargeurEquipement, decimal? MplUnitaire);

public sealed record PrixCotationDto(
    string KeyLignePrix, int? Position, string? CodeRubrique, string? LibelleRubrique, decimal? Quantite,
    TypeUtilisation? TypeUtilisation, decimal? MontantValeur, string? Devise, string? BaseCalcul,
    bool Arrondi, decimal? MontantAchat, string? DeviseAchat, string? Commentaire, string? CodeTva,
    decimal TauxTva);

public sealed record AchatCotationDto(
    string KeyLigne, string? CodeRubrique, string? LibelleRubrique, decimal? MtDevise, string? Devise,
    string? KeyFournisseur, string? LibelleFournisseur, DateTime? DateValidite, decimal? MtMad,
    string? LibelleJustif)
{
    public string? CheminJustif { get; init; }
}

public sealed record TauxChangeCotationDto(string Devise, decimal? TauxChange);

public sealed record DocumentCotationDto(string KeyDocument, string? LibelleDocument, string? CheminDocument, string? TypePieceDocument);

/// <summary>Totals shown on the quotation screen, computed from the modelled lines.</summary>
public sealed record CotationTotauxDto(
    decimal TotalVente, decimal TotalAchat, decimal Marge,
    int NbLignesPrix, int NbLignesAchat, int NbColisages,
    IReadOnlyList<string> DevisesUtilisees, IReadOnlyList<string> DevisesSansTaux);

public sealed record CotationDto(
    string KeyCotation, string NumCotation, string? AgenceCotation, DateTime DateCotation, DateTime? DateValidite,
    string? Statut, DateTime? DateStatut, string? CommentaireStatut, string? Sens, bool IsGroupage, bool IsConvertible,
    string KeyClient, string? Contact, string? Commercial, string? CreateurCotation,
    string? ProduitCotation, string? TypeTransport, string? NatureMarchandise, string? Mode, string? ModeTraitement,
    string? TypeTarif, string? IncotermCotation, string? PortChargement, string? PortDechargement,
    string? VilleChargement, string? VilleDechargement, string? PaysChargement, string? PaysDechargement,
    string? KeyCompagnie, string? IntituleCompagnie,
    bool MarchandiseDangereuse, string? ClasseMarchandise, bool MarchandiseFragile, bool MarchandiseGerbable,
    bool Assurance, decimal? ValeurMarchandise, string? Devise, bool TemperatureDirigee,
    decimal? TemperatureMin, decimal? TemperatureMax,
    decimal? PoidsReel, decimal? VolumeReel, decimal? PoidsTaxable, decimal? VolumeTaxable, decimal? MplReel,
    decimal? FranchiseMagasinage, decimal? FranchiseSurestarie,
    string? Concurrent, decimal? MontantConcurrent, string? MotifCotationPerdue, string? KeyRegroupement,
    string? CotationContreOffre, bool Attachement, bool Supprime,
    string? CommentaireCotation, string? CommentairePrix, string? CommentaireAssurance,
    string? ConditionGenerale, string? ConditionPaiement, string? PrestationNonIncluse,
    IReadOnlyList<ColisageCotationDto> Colisages, IReadOnlyList<PrixCotationDto> Prix,
    IReadOnlyList<AchatCotationDto> Achats, IReadOnlyList<TauxChangeCotationDto> TauxChange,
    IReadOnlyList<DocumentCotationDto> Documents, CotationTotauxDto Totaux)
{
    // Preserve header fields which are not displayed by every client on a full PUT.
    public UpdateCotationRequest? EditableFields { get; init; }
}

/// <summary>List projection for the quotation grid.</summary>
public sealed record CotationSummaryDto(
    string KeyCotation, string NumCotation, DateTime DateCotation, DateTime? DateValidite, string Statut,
    string KeyClient, string? ProduitCotation, string? TypeTransport, string? Commercial,
    string? Concurrent, string? MotifCotationPerdue, bool Attachement, bool Supprime);

public sealed record CreateCotationRequest(
    string ProduitCotation,
    string KeyClient,
    string? NumCotation = null,
    string? TypeTransport = null,
    string? NatureMarchandise = null,
    string? Mode = null,
    string? ModeTraitement = null,
    string? TypeTarif = null,
    string? Incoterm = null,
    string? PortChargement = null,
    string? PortDechargement = null,
    DateTime? DateValidite = null,
    string? Contact = null,
    string? Commercial = null,
    string? KeyCompagnie = null,
    string? IntituleCompagnie = null,
    // When true the standard conditions and franchises of the dossier type are copied in.
    bool AppliquerConditions = true);

public sealed record UpdateCotationRequest(
    string? NumCotation,
    string? TypeTransport,
    string? NatureMarchandise,
    string? Mode,
    string? ModeTraitement,
    string? TypeTarif,
    string? FrequenceDepart,
    string? TransitTime,
    string? LangueClient,
    string? Commentaire,
    string? CommentairePrix,
    DateTime? DateValidite = null,
    DateTime? DateLimiteEnlevement = null);

public sealed record CloturerCotationRequest(
    bool Gagnee,
    string? Commentaire = null,
    string? Concurrent = null,
    decimal? MontantConcurrent = null,
    string? DeviseConcurrent = null,
    string? MotifPerdue = null);

public sealed record MotifCotationRequest(string? Motif);

public sealed record DuplicateCotationRequest(string? NumCotation = null, bool ContreOffre = false);

public sealed record ColisageCotationInput(
    string? TypeColis, decimal? Qte, decimal? Poids, bool Gerbable, decimal? Longueur, decimal? Largeur,
    decimal? Hauteur, decimal? Volume, decimal? VolumeNgerbable = null, string? TypeEquipement = null,
    string? NatureEquipement = null, decimal? LargeurEquipement = null, decimal? MplUnitaire = null);

public sealed record PrixCotationInput(
    int? Position, string? CodeRubrique, string? LibelleRubrique, decimal? Quantite,
    TypeUtilisation? TypeUtilisation, decimal? MontantValeur, string? Devise, string? BaseCalcul,
    bool Arrondi = false, decimal? MontantAchat = null, string? DeviseAchat = null,
    string? Commentaire = null, string? CodeTva = null);

public sealed record AchatCotationInput(
    string? CodeRubrique, string? LibelleRubrique, decimal? MtDevise, string? Devise,
    string? KeyFournisseur, string? LibelleFournisseur, DateTime? DateValidite = null,
    string? CheminJustif = null, string? LibelleJustif = null);

public sealed record TauxChangeInput(string Devise, decimal? TauxChange);

public sealed record DocumentCotationInput(string? LibelleDocument, string? CheminDocument, string? TypePieceDocument = null);

public sealed record QuotationSearchRequest(
    string? KeyClient = null,
    string? Statut = null,
    string? ProduitCotation = null,
    string? Commercial = null,
    string? NumCotation = null,
    DateTime? DateDu = null,
    DateTime? DateAu = null,
    bool? Convertibles = null,
    bool IncludeSupprimes = false,
    int Page = 1,
    int PageSize = 25);

public interface IQuotationRepository
{
    Task<Cotation?> FindAsync(string keyCotation, bool includeChildren, CancellationToken cancellationToken = default);
    Task<PagedResult<CotationSummaryDto>> SearchAsync(QuotationSearchRequest request, CancellationToken cancellationToken = default);
    Task<bool> NumCotationExistsAsync(string numCotation, string? excludeKeyCotation = null, CancellationToken cancellationToken = default);

    /// <summary>Whether the quotation carries attached documents — a deletion guard.</summary>
    Task<bool> HasDocumentsAsync(string keyCotation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether <c>t_frais_annexe_cotation</c> holds rows for this quotation — a deletion guard.
    /// </summary>
    /// <remarks>
    /// This is the one place the codebase issues raw SQL, and deliberately so: the extraction evidenced
    /// only a single column of that table (<c>key_cotation</c>, from its one <c>WHERE</c> clause), so
    /// there is no honest entity to model. Guessing the rest of the shape would be worse than an
    /// explicit, narrow probe.
    /// </remarks>
    Task<bool> HasFraisAnnexesAsync(string keyCotation, CancellationToken cancellationToken = default);

    Task<FranchiseCotation?> FindFranchiseAsync(string typeDossier, string natureFranchise, CancellationToken cancellationToken = default);

    void Add(Cotation cotation);
    void Add(ColisageCotation colisage);
    void Add(PrixCotation prix);
    void Add(AchatCotation achat);
    void Add(TauxChangeCotation taux);
    void Add(DocumentCotation document);
    void Remove(ColisageCotation colisage);
    void Remove(PrixCotation prix);
    void Remove(AchatCotation achat);
    void Remove(DocumentCotation document);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IQuotationService
{
    Task<PagedResult<CotationSummaryDto>> SearchAsync(QuotationSearchRequest request, CancellationToken cancellationToken = default);
    Task<CotationDto> GetAsync(string keyCotation, CancellationToken cancellationToken = default);
    Task<CotationDto> CreateAsync(CreateCotationRequest request, CancellationToken cancellationToken = default);
    Task<CotationDto> UpdateAsync(string keyCotation, UpdateCotationRequest request, CancellationToken cancellationToken = default);
    Task<CotationDto> EnvoyerAsync(string keyCotation, CancellationToken cancellationToken = default);
    Task<CotationDto> CloturerAsync(string keyCotation, CloturerCotationRequest request, CancellationToken cancellationToken = default);
    Task<CotationDto> AnnulerAsync(string keyCotation, MotifCotationRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(string keyCotation, CancellationToken cancellationToken = default);
    Task<CotationDto> DuplicateAsync(string keyCotation, DuplicateCotationRequest request, CancellationToken cancellationToken = default);
    Task<CotationDto> SetColisagesAsync(string keyCotation, IReadOnlyList<ColisageCotationInput> colisages, CancellationToken cancellationToken = default);
    Task<CotationDto> SetPrixAsync(string keyCotation, IReadOnlyList<PrixCotationInput> prix, CancellationToken cancellationToken = default);
    Task<CotationDto> SetAchatsAsync(string keyCotation, IReadOnlyList<AchatCotationInput> achats, CancellationToken cancellationToken = default);
    Task<CotationDto> SetTauxChangeAsync(string keyCotation, IReadOnlyList<TauxChangeInput> taux, CancellationToken cancellationToken = default);
    Task<CotationDto> AddDocumentAsync(string keyCotation, DocumentCotationInput document, CancellationToken cancellationToken = default);
    Task<CotationDto> RemoveDocumentAsync(string keyCotation, string keyDocument, CancellationToken cancellationToken = default);
    Task<CotationDto> SetRegroupementAsync(string keyCotation, string? keyRegroupement, CancellationToken cancellationToken = default);
    Task<CotationTotauxDto> GetTotauxAsync(string keyCotation, CancellationToken cancellationToken = default);
}

/// <summary>
/// Quotation use cases — the port of <c>frm_Saisie_Cotation.vb</c>, <c>frm_Liste_Cotation.vb</c> and
/// <c>frm_Cloture_Cotation.vb</c>.
/// </summary>
public sealed class QuotationService(
    IQuotationRepository cotations,
    IKeyGenerator keys,
    IAuditTrail audit,
    ITenantContext tenant,
    IParametreSocieteProvider parametres) : IQuotationService
{
    /// <summary>
    /// The company's own currency. Until <c>t_parametre_societe</c> is migrated this is a constant, which
    /// is why it is named rather than inlined.
    /// </summary>
    private const string DeviseSociete = "MAD";

    public Task<PagedResult<CotationSummaryDto>> SearchAsync(QuotationSearchRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Page < 1)
        {
            throw new ArgumentException("La page doit être supérieure ou égale à 1.", nameof(request));
        }

        return cotations.SearchAsync(request with { PageSize = Math.Clamp(request.PageSize, 1, 200) }, cancellationToken);
    }

    public async Task<CotationDto> GetAsync(string keyCotation, CancellationToken cancellationToken = default) =>
        Map(await RequireAsync(keyCotation, cancellationToken), DeviseSociete);

    public async Task<CotationDto> CreateAsync(CreateCotationRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var key = await keys.NextAsync("COTATION", cancellationToken);
        var num = string.IsNullOrWhiteSpace(request.NumCotation)
            ? await keys.NextAsync("COTATION_NUM", cancellationToken)
            : request.NumCotation.Trim();

        await EnsureNumLibreAsync(num, null, cancellationToken);

        var cotation = new Cotation(key, tenant.SocieteKey, num, request.ProduitCotation, request.KeyClient, now);

        cotation.SetEntete(num, request.TypeTransport, request.NatureMarchandise, request.Mode,
            request.ModeTraitement, request.TypeTarif, null, null, null, null, null);
        cotation.SetParties(tenant.AgenceKey, request.Contact, request.Commercial, tenant.UserDisplayName);
        cotation.SetCompagnie(request.KeyCompagnie, request.IntituleCompagnie);
        cotation.SetItineraire(request.Incoterm, null, null, null, null, null, null, null, null, null, null,
            request.PortChargement, request.PortDechargement, null, null);
        cotation.SetValidite(request.DateValidite, null, null);

        // The standard conditions and franchises come from t_franchise_cotation, keyed by dossier type.
        if (request.AppliquerConditions)
        {
            var magasinage = await cotations.FindFranchiseAsync(request.ProduitCotation, FranchiseCotation.NatureMagasinage, cancellationToken);
            cotation.ApplyConditions(magasinage);

            var surestarie = await cotations.FindFranchiseAsync(request.ProduitCotation, FranchiseCotation.NatureSurestarie, cancellationToken);
            cotation.ApplyConditions(surestarie);
        }

        // The company's own currency always carries a rate of 1.
        cotation.AddTaux(new TauxChangeCotation(key, DeviseSociete, 1m));

        cotations.Add(cotation);
        await cotations.SaveChangesAsync(cancellationToken);

        await TraceAsync(key, CotationTrace.Creation, $"Création de la cotation {num}", cancellationToken);

        return Map(await RequireAsync(key, cancellationToken), DeviseSociete);
    }

    public async Task<CotationDto> UpdateAsync(string keyCotation, UpdateCotationRequest request, CancellationToken cancellationToken = default)
    {
        var cotation = await RequireAsync(keyCotation, cancellationToken);
        EnsureOuverte(cotation, "modifiée");

        await EnsureNumLibreAsync(request.NumCotation ?? cotation.NumCotation!, cotation.KeyCotation, cancellationToken);

        cotation.SetEntete(request.NumCotation ?? cotation.NumCotation, request.TypeTransport, request.NatureMarchandise,
            request.Mode, request.ModeTraitement, request.TypeTarif, request.FrequenceDepart, request.TransitTime,
            request.LangueClient, request.Commentaire, request.CommentairePrix);
        cotation.SetValidite(request.DateValidite ?? cotation.DateValidite, request.DateLimiteEnlevement, cotation.DateDepartPrev);

        await cotations.SaveChangesAsync(cancellationToken);
        await TraceAsync(keyCotation, CotationTrace.Modification, null, cancellationToken);

        return Map(await RequireAsync(keyCotation, cancellationToken), DeviseSociete);
    }

    public async Task<CotationDto> EnvoyerAsync(string keyCotation, CancellationToken cancellationToken = default)
    {
        var cotation = await RequireAsync(keyCotation, cancellationToken);

        // The legacy pre-check required at least one price line, and packing detail for groupage when the
        // company switch param_oblig_cloisage is on. That switch was previously hard-coded to false here,
        // which silently skipped the packing-detail requirement for every company that had it enabled.
        var obligCloisage = await parametres.EstOuiAsync(SetCodes.ParamObligCloisage, cancellationToken);

        cotation.Envoyer(DateTime.UtcNow, obligCloisage);

        await cotations.SaveChangesAsync(cancellationToken);
        await TraceAsync(keyCotation, CotationTrace.Validation, "Validation / envoi de la cotation", cancellationToken);

        return Map(await RequireAsync(keyCotation, cancellationToken), DeviseSociete);
    }

    public async Task<CotationDto> CloturerAsync(string keyCotation, CloturerCotationRequest request, CancellationToken cancellationToken = default)
    {
        var cotation = await RequireAsync(keyCotation, cancellationToken);

        cotation.Cloturer(request.Gagnee, DateTime.UtcNow, request.Commentaire, request.Concurrent,
            request.MontantConcurrent, request.DeviseConcurrent, request.MotifPerdue);

        await cotations.SaveChangesAsync(cancellationToken);
        await TraceAsync(keyCotation, request.Gagnee ? CotationTrace.Gagnee : CotationTrace.Perdue,
            request.Gagnee ? "Cotation gagnée" : $"Cotation perdue : {request.MotifPerdue}", cancellationToken);

        return Map(await RequireAsync(keyCotation, cancellationToken), DeviseSociete);
    }

    public async Task<CotationDto> AnnulerAsync(string keyCotation, MotifCotationRequest request, CancellationToken cancellationToken = default)
    {
        var cotation = await RequireAsync(keyCotation, cancellationToken);
        cotation.Annuler(DateTime.UtcNow, request.Motif);

        await cotations.SaveChangesAsync(cancellationToken);
        await TraceAsync(keyCotation, CotationTrace.Annulation, request.Motif, cancellationToken);

        return Map(await RequireAsync(keyCotation, cancellationToken), DeviseSociete);
    }

    /// <summary>
    /// Soft-deletes the quotation, refusing while it still carries documents or annex fees.
    /// </summary>
    /// <remarks>
    /// Both guards were legacy behaviour (messages 200056 and 200075). The delete is never physical:
    /// the legacy code set <c>supp=1</c> and a <c>Supprimée</c> status, and every listing filters on it.
    /// </remarks>
    public async Task DeleteAsync(string keyCotation, CancellationToken cancellationToken = default)
    {
        var cotation = await RequireAsync(keyCotation, cancellationToken);

        if (cotation.Supprime)
        {
            return;
        }

        if (await cotations.HasDocumentsAsync(keyCotation, cancellationToken))
        {
            throw new BusinessRuleException(
                "cotation.suppression.documents",
                "Cette cotation comporte des documents joints : supprimez-les avant de la supprimer.");
        }

        if (await cotations.HasFraisAnnexesAsync(keyCotation, cancellationToken))
        {
            throw new BusinessRuleException(
                "cotation.suppression.frais",
                "Cette cotation comporte des frais annexes : elle ne peut pas être supprimée.");
        }

        cotation.SoftDelete(DateTime.UtcNow);
        await cotations.SaveChangesAsync(cancellationToken);
        await TraceAsync(keyCotation, CotationTrace.Suppression, null, cancellationToken);
    }

    /// <summary>
    /// Duplicates a quotation, or issues a counter-offer from it.
    /// </summary>
    /// <remarks>
    /// Both operations copy the header plus packing, price, exchange-rate and document lines, and both
    /// leave the <em>source</em> traced. They differ in what happens to the source: a plain duplication
    /// leaves it alone, whereas a counter-offer moves it to <c>Contre Offre</c> and links the copy back
    /// through <c>cotation_contre_offre</c>.
    /// <para>
    /// Purchase lines are deliberately <b>not</b> copied — the legacy duplication omits them, so a copy
    /// starts with no cost basis.
    /// </para>
    /// </remarks>
    public async Task<CotationDto> DuplicateAsync(string keyCotation, DuplicateCotationRequest request, CancellationToken cancellationToken = default)
    {
        var source = await RequireAsync(keyCotation, cancellationToken);

        if (source.Supprime)
        {
            throw new BusinessRuleException("cotation.supprimee", "Une cotation supprimée ne peut pas être dupliquée.");
        }

        var now = DateTime.UtcNow;
        var num = string.IsNullOrWhiteSpace(request.NumCotation)
            ? await keys.NextAsync("COTATION_NUM", cancellationToken)
            : request.NumCotation.Trim();

        await EnsureNumLibreAsync(num, null, cancellationToken);

        var key = await keys.NextAsync("COTATION", cancellationToken);
        var copy = new Cotation(key, tenant.SocieteKey, num, source.ProduitCotation, source.KeyClient, now);

        CopyEntete(source, copy, num, now, source.DateValidite);

        if (request.ContreOffre)
        {
            copy.LinkContreOffre(source.KeyCotation);
            source.MarquerContreOffre(now);
        }

        cotations.Add(copy);

        await CopyLinesAsync(source, copy, key, cancellationToken);

        await cotations.SaveChangesAsync(cancellationToken);

        await TraceAsync(key, CotationTrace.Duplication, $"Duplication depuis {source.NumCotation}", cancellationToken);
        await TraceAsync(source.KeyCotation,
            request.ContreOffre ? CotationTrace.ContreOffreSource : CotationTrace.Duplication,
            request.ContreOffre ? $"Contre-offre émise : {num}" : $"Dupliquée vers {num}", cancellationToken);

        return Map(await RequireAsync(key, cancellationToken), DeviseSociete);
    }

    public async Task<CotationDto> SetColisagesAsync(string keyCotation, IReadOnlyList<ColisageCotationInput> colisages, CancellationToken cancellationToken = default)
    {
        var cotation = await RequireAsync(keyCotation, cancellationToken);
        EnsureModifiable(cotation);

        foreach (var existant in cotation.Colisages.ToArray())
        {
            cotations.Remove(existant);
        }

        foreach (var input in colisages)
        {
            var ligne = new ColisageCotation(
                await keys.NextAsync("DETAIL_COTATION", cancellationToken), keyCotation, input.TypeColis, input.Qte);

            ligne.SetDimensions(input.Poids, input.Gerbable, input.Longueur, input.Largeur, input.Hauteur,
                input.Volume, null, input.VolumeNgerbable, input.TypeEquipement, input.NatureEquipement,
                input.LargeurEquipement, input.MplUnitaire);
            cotations.Add(ligne);
        }

        await cotations.SaveChangesAsync(cancellationToken);

        if (colisages.Count > 0)
        {
            await TraceAsync(keyCotation, CotationTrace.AjoutColisage, $"{colisages.Count} ligne(s) de colisage", cancellationToken);
        }

        return Map(await RequireAsync(keyCotation, cancellationToken), DeviseSociete);
    }

    public async Task<CotationDto> SetPrixAsync(string keyCotation, IReadOnlyList<PrixCotationInput> prix, CancellationToken cancellationToken = default)
    {
        var cotation = await RequireAsync(keyCotation, cancellationToken);
        EnsureModifiable(cotation);

        foreach (var existant in cotation.PrixCotation.ToArray())
        {
            cotations.Remove(existant);
        }

        foreach (var input in prix)
        {
            var ligne = new PrixCotation(
                await keys.NextAsync("PRIX_COTATION", cancellationToken), keyCotation, tenant.SocieteKey);

            ligne.SetContenu(input.Position, input.CodeRubrique, input.LibelleRubrique, input.Quantite,
                input.TypeUtilisation, input.MontantValeur, input.Devise, input.BaseCalcul, input.Arrondi,
                input.MontantAchat, input.DeviseAchat, input.Commentaire, input.CodeTva);
            cotations.Add(ligne);
        }

        if (prix.Count > 0)
        {
            await TraceAsync(keyCotation, CotationTrace.AjoutPrix, $"{prix.Count} ligne(s) de prix", cancellationToken);
        }

        await cotations.SaveChangesAsync(cancellationToken);
        return Map(await RequireAsync(keyCotation, cancellationToken), DeviseSociete);
    }

    /// <summary>
    /// Replaces the purchase lines, revalidating each against the quotation's creation date and
    /// recomputing MAD equivalents from the current rates.
    /// </summary>
    public async Task<CotationDto> SetAchatsAsync(string keyCotation, IReadOnlyList<AchatCotationInput> achats, CancellationToken cancellationToken = default)
    {
        var cotation = await RequireAsync(keyCotation, cancellationToken);
        EnsureModifiable(cotation);

        foreach (var existant in cotation.Achats.ToArray())
        {
            cotations.Remove(existant);
        }

        foreach (var input in achats)
        {
            var ligne = new AchatCotation(await keys.NextAsync("ACHAT_COTATION", cancellationToken), keyCotation);
            ligne.SetContenu(input.CodeRubrique, tenant.SocieteKey, input.LibelleRubrique, input.MtDevise,
                input.Devise, input.KeyFournisseur, input.LibelleFournisseur, input.DateValidite,
                input.CheminJustif, input.LibelleJustif);

            ligne.Validate(cotation.DateCotation);
            ligne.RecalculerMad(cotation.TauxPour(input.Devise, DeviseSociete));

            cotations.Add(ligne);
        }

        if (achats.Count > 0)
        {
            await TraceAsync(keyCotation, CotationTrace.AjoutAchat, $"{achats.Count} ligne(s) d'achat", cancellationToken);
        }

        await cotations.SaveChangesAsync(cancellationToken);
        return Map(await RequireAsync(keyCotation, cancellationToken), DeviseSociete);
    }

    /// <summary>
    /// Deletes and rebuilds the exchange-rate table.
    /// </summary>
    /// <remarks>
    /// The legacy save always started with <c>delete from t_taux_change_cotation where key_cotation=…</c>,
    /// so the sale and purchase rate grids overwrite each other. Reproduced deliberately: the collision
    /// is real behaviour, and the company currency is always re-inserted at 1 so it cannot be
    /// accidentally removed.
    /// </remarks>
    public async Task<CotationDto> SetTauxChangeAsync(string keyCotation, IReadOnlyList<TauxChangeInput> taux, CancellationToken cancellationToken = default)
    {
        var cotation = await RequireAsync(keyCotation, cancellationToken);
        EnsureModifiable(cotation);

        cotation.ClearTaux();

        foreach (var input in taux.Where(t => !string.IsNullOrWhiteSpace(t.Devise)))
        {
            cotation.AddTaux(new TauxChangeCotation(
                keyCotation, input.Devise.Trim(), input.TauxChange ?? 0m));
        }

        if (cotation.TauxChange.All(t => !string.Equals(t.Devise, DeviseSociete, StringComparison.OrdinalIgnoreCase)))
        {
            cotation.AddTaux(new TauxChangeCotation(keyCotation, DeviseSociete, 1m));
        }

        // The MAD equivalent of every purchase line depends on these rates.
        foreach (var achat in cotation.Achats)
        {
            achat.RecalculerMad(cotation.TauxPour(achat.Devise, DeviseSociete));
        }

        await cotations.SaveChangesAsync(cancellationToken);
        return Map(await RequireAsync(keyCotation, cancellationToken), DeviseSociete);
    }

    /// <summary>
    /// Attaches a document and raises <c>attachement</c>.
    /// </summary>
    public async Task<CotationDto> AddDocumentAsync(string keyCotation, DocumentCotationInput document, CancellationToken cancellationToken = default)
    {
        var cotation = await RequireAsync(keyCotation, cancellationToken);

        var ligne = new DocumentCotation(
            await keys.NextAsync("DOCUMENT", cancellationToken), keyCotation,
            document.LibelleDocument, document.CheminDocument);
        ligne.SetPiece(document.TypePieceDocument);

        cotations.Add(ligne);
        cotation.SetAttachement(true);

        await cotations.SaveChangesAsync(cancellationToken);
        await TraceAsync(keyCotation, CotationTrace.AjoutDocument, document.LibelleDocument, cancellationToken);

        return Map(await RequireAsync(keyCotation, cancellationToken), DeviseSociete);
    }

    /// <summary>
    /// Removes a document and lowers <c>attachement</c> once the last one is gone.
    /// </summary>
    /// <remarks>
    /// The legacy reset condition is
    /// <c>key_cotation not in (select key_cotation from t_document_cotation)</c> — the flag is derived,
    /// never set by hand, so a stale <c>1</c> would make the list claim attachments that no longer exist.
    /// </remarks>
    public async Task<CotationDto> RemoveDocumentAsync(string keyCotation, string keyDocument, CancellationToken cancellationToken = default)
    {
        var cotation = await RequireAsync(keyCotation, cancellationToken);

        var document = cotation.Documents.FirstOrDefault(d => d.KeyDocument == keyDocument)
            ?? throw new EntityNotFoundException("DocumentCotation", keyDocument);

        cotations.Remove(document);
        await cotations.SaveChangesAsync(cancellationToken);

        // Re-read: the navigation may still hold the removed row in this graph.
        var restants = await cotations.HasDocumentsAsync(keyCotation, cancellationToken);
        cotation.SetAttachement(restants);

        await cotations.SaveChangesAsync(cancellationToken);
        await TraceAsync(keyCotation, CotationTrace.SuppressionDocument, document.LibelleDocument, cancellationToken);

        return Map(await RequireAsync(keyCotation, cancellationToken), DeviseSociete);
    }

    public async Task<CotationDto> SetRegroupementAsync(string keyCotation, string? keyRegroupement, CancellationToken cancellationToken = default)
    {
        var cotation = await RequireAsync(keyCotation, cancellationToken);
        cotation.SetRegroupement(keyRegroupement);

        await cotations.SaveChangesAsync(cancellationToken);
        await TraceAsync(keyCotation, CotationTrace.Modification,
            keyRegroupement is null ? "Annulation du regroupement" : $"Regroupement {keyRegroupement}", cancellationToken);

        return Map(await RequireAsync(keyCotation, cancellationToken), DeviseSociete);
    }

    public async Task<CotationTotauxDto> GetTotauxAsync(string keyCotation, CancellationToken cancellationToken = default)
    {
        var cotation = await RequireAsync(keyCotation, cancellationToken);
        return ComputeTotaux(cotation, DeviseSociete);
    }

    /// <summary>
    /// Totals across the price and purchase lines.
    /// </summary>
    /// <remarks>
    /// Percentage lines are resolved against the sum of the <c>Montant</c> lines, which is the freight
    /// base the legacy taxation routine used (the <c>base_fret</c> company parameter, whose value is a SQL
    /// predicate fragment such as <c>in ('FR','F1')</c> that selects which rubriques form the base — an
    /// earlier note here called it <c>param_base_fret</c>, which does not exist). A currency with no rate is
    /// reported rather than converted at 1 — silently treating EUR as MAD would understate the total by
    /// a factor of ten.
    /// </remarks>
    private static CotationTotauxDto ComputeTotaux(Cotation cotation, string deviseSociete)
    {
        var baseFret = cotation.PrixCotation
            .Where(p => p.TypeUtilisation == TypeUtilisation.Montant)
            .Sum(p => p.MontantLigne());

        var totalVente = cotation.PrixCotation.Sum(p => p.MontantLigne(baseFret));

        var devises = cotation.PrixCotation
            .Select(p => p.Devise ?? deviseSociete)
            .Concat(cotation.Achats.Select(a => a.Devise ?? deviseSociete))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var sansTaux = devises
            .Where(d => !string.Equals(d, deviseSociete, StringComparison.OrdinalIgnoreCase)
                && cotation.TauxPour(d, deviseSociete) == 0m)
            .ToArray();

        var totalAchat = cotation.Achats.Sum(a => a.MtMad ?? 0m);

        return new CotationTotauxDto(
            Math.Round(totalVente, 2, MidpointRounding.AwayFromZero),
            Math.Round(totalAchat, 2, MidpointRounding.AwayFromZero),
            Math.Round(totalVente - totalAchat, 2, MidpointRounding.AwayFromZero),
            cotation.PrixCotation.Count, cotation.Achats.Count, cotation.Colisages.Count,
            devises, sansTaux);
    }

    private static void CopyEntete(Cotation source, Cotation copy, string num, DateTime now, DateTime? dateValidite)
    {
        copy.SetEntete(num, source.TypeTransport, source.NatureMarchandise, source.Mode, source.ModeTraitement,
            source.TypeTarif, source.FrequenceDepart, source.TransitTime, source.LangueClient,
            source.CommentaireCotation, source.CommentairePrix);
        copy.SetParties(source.AgenceCotation, source.Contact, source.Commercial, source.CreateurCotation);
        copy.SetCompagnie(source.KeyCompagnie, source.IntituleCompagnie);
        copy.SetItineraire(source.IncotermCotation, source.LieuChargement, source.VilleChargement,
            source.PaysChargement, source.RegionChargement, source.DepartementChargement,
            source.LieuDechargement, source.VilleDechargement, source.PaysDechargement,
            source.RegionDechargement, source.DepartementDechargement,
            source.PortChargement, source.PortDechargement, source.Provenance, source.Destination);
        copy.SetMarchandise(source.NatureMarchandise, source.MarchandiseDangereuse, source.ClasseMarchandise,
            source.MarchandiseFragile, source.MarchandiseGerbable, source.Assurance, source.ValeurMarchandise,
            source.Devise, source.TemperatureDirigee, source.TemperatureMin, source.TemperatureMax);
        copy.SetMesures(source.PoidsReel, source.VolumeReel, source.VolumeNgerbable, source.PoidsTaxable,
            source.VolumeTaxable, source.MplReel, source.EquivalenceVolumePoids, source.EquivalenceMplPoids,
            source.EquivalenceMplVolume);
        copy.SetFranchises(source.FranchiseMagasinage, source.FranchiseSurestarie);

        // A counter-offer starts afresh; the copy's validity defaults to the source's.
        copy.SetValidite(dateValidite ?? now.Date, source.DateLimiteEnlevement, source.DateDepartPrev);
        copy.SetStatut(Cotation.StatutSaisie, now, null);
    }

    /// <summary>
    /// Copies packing, price, exchange-rate and document lines into the new quotation.
    /// </summary>
    /// <remarks>
    /// Purchase lines are omitted on purpose — the legacy duplication copies header, colisage, prix,
    /// taux, trace and document, but never <c>t_achat_cotation</c>.
    /// </remarks>
    private async Task CopyLinesAsync(Cotation source, Cotation copy, string newKey, CancellationToken cancellationToken)
    {
        foreach (var colisage in source.Colisages)
        {
            var ligne = new ColisageCotation(
                await keys.NextAsync("DETAIL_COTATION", cancellationToken), newKey, colisage.TypeColis, colisage.Qte);
            ligne.SetDimensions(colisage.Poids, colisage.Gerbable, colisage.Longueur, colisage.Largeur,
                colisage.Hauteur, colisage.Volume, colisage.HauteurNgerbable, colisage.VolumeNgerbable,
                colisage.TypeEquipement, colisage.NatureEquipement, colisage.LargeurEquipement, colisage.MplUnitaire);
            cotations.Add(ligne);
        }

        foreach (var prix in source.PrixCotation)
        {
            var ligne = new PrixCotation(
                await keys.NextAsync("PRIX_COTATION", cancellationToken), newKey, prix.SocieteKey);
            ligne.SetContenu(prix.Position, prix.CodeRubrique, prix.LibelleRubrique, prix.Quantite,
                prix.TypeUtilisation, prix.MontantValeur, prix.Devise, prix.BaseCalcul, prix.Arrondi,
                prix.MontantAchat, prix.DeviseAchat, prix.Commentaire, prix.CodeTva);
            cotations.Add(ligne);
        }

        foreach (var taux in source.TauxChange)
        {
            cotations.Add(new TauxChangeCotation(newKey, taux.Devise, taux.TauxChange));
        }

        foreach (var document in source.Documents)
        {
            var ligne = new DocumentCotation(
                await keys.NextAsync("DOCUMENT", cancellationToken), newKey,
                document.LibelleDocument, document.CheminDocument);
            ligne.SetPiece(document.TypePieceDocument);
            cotations.Add(ligne);
        }
    }

    private static void EnsureOuverte(Cotation cotation, string operation)
    {
        if (!cotation.IsOuverte)
        {
            throw new BusinessRuleException(
                "cotation.statut", $"La cotation est au statut « {cotation.Statut} » : elle ne peut pas être {operation}.");
        }
    }

    private static void EnsureModifiable(Cotation cotation)
    {
        if (cotation.Supprime)
        {
            throw new BusinessRuleException("cotation.supprimee", "Une cotation supprimée ne peut pas être modifiée.");
        }
    }

    private async Task EnsureNumLibreAsync(string num, string? excludeKey, CancellationToken cancellationToken)
    {
        if (await cotations.NumCotationExistsAsync(num, excludeKey, cancellationToken))
        {
            throw new DuplicateEntityException("Cotation", "n_cotation", num);
        }
    }

    private async Task<Cotation> RequireAsync(string keyCotation, CancellationToken cancellationToken) =>
        await cotations.FindAsync(keyCotation, true, cancellationToken)
        ?? throw new EntityNotFoundException("Cotation", keyCotation);

    private Task TraceAsync(string key, string code, string? commentaire, CancellationToken cancellationToken) =>
        audit.TraceAsync<TraceCotation>(key, code, commentaire, null, 0, cancellationToken);

    private static CotationDto Map(Cotation c, string deviseSociete)
    {
        var aujourdhui = DateTime.UtcNow;

        return new CotationDto(
            c.KeyCotation, c.NumCotation ?? string.Empty, c.AgenceCotation, c.DateCotation, c.DateValidite,
            c.Statut, c.DateStatut, c.CommentaireStatut, c.Sens, c.IsGroupage, c.IsConvertible(aujourdhui),
            c.KeyClient, c.Contact, c.Commercial, c.CreateurCotation,
            c.ProduitCotation, c.TypeTransport, c.NatureMarchandise, c.Mode, c.ModeTraitement,
            c.TypeTarif, c.IncotermCotation, c.PortChargement, c.PortDechargement,
            c.VilleChargement, c.VilleDechargement, c.PaysChargement, c.PaysDechargement,
            c.KeyCompagnie, c.IntituleCompagnie,
            c.MarchandiseDangereuse, c.ClasseMarchandise, c.MarchandiseFragile, c.MarchandiseGerbable,
            c.Assurance, c.ValeurMarchandise, c.Devise, c.TemperatureDirigee, c.TemperatureMin, c.TemperatureMax,
            c.PoidsReel, c.VolumeReel, c.PoidsTaxable, c.VolumeTaxable, c.MplReel,
            c.FranchiseMagasinage, c.FranchiseSurestarie,
            c.Concurrent, c.MontantConcurrent, c.MotifCotationPerdue, c.KeyRegroupement,
            c.CotationContreOffre, c.Attachement, c.Supprime,
            c.CommentaireCotation, c.CommentairePrix, c.CommentaireAssurance,
            c.ConditionGenerale, c.ConditionPaiement, c.PrestationNonIncluse,
            c.Colisages.Select(x => new ColisageCotationDto(x.KeyColisageCotation, x.TypeColis, x.Qte, x.Poids,
                x.Gerbable, x.Longueur, x.Largeur, x.Hauteur, x.Volume, x.VolumeNgerbable, x.TypeEquipement,
                x.NatureEquipement, x.LargeurEquipement, x.MplUnitaire)).ToArray(),
            c.PrixCotation.OrderBy(p => p.Position).Select(p => new PrixCotationDto(p.KeyLignePrix, p.Position,
                p.CodeRubrique, p.LibelleRubrique, p.Quantite, p.TypeUtilisation, p.MontantValeur, p.Devise,
                p.BaseCalcul, p.Arrondi, p.MontantAchat, p.DeviseAchat, p.Commentaire, p.CodeTva, p.TauxTva)).ToArray(),
            c.Achats.Select(a => new AchatCotationDto(a.KeyLigne, a.CodeRubrique, a.LibelleRubrique, a.MtDevise,
                a.Devise, a.KeyFournisseur, a.LibelleFournisseur, a.DateValidite, a.MtMad, a.LibelleJustif)
                { CheminJustif = a.CheminJustif }).ToArray(),
            c.TauxChange.Select(t => new TauxChangeCotationDto(t.Devise, t.TauxChange)).ToArray(),
            c.Documents.Select(d => new DocumentCotationDto(d.KeyDocument, d.LibelleDocument, d.CheminDocument,
                d.TypePieceDocument)).ToArray(),
            ComputeTotaux(c, deviseSociete))
        {
            EditableFields = new UpdateCotationRequest(c.NumCotation, c.TypeTransport, c.NatureMarchandise,
                c.Mode, c.ModeTraitement, c.TypeTarif, c.FrequenceDepart, c.TransitTime, c.LangueClient,
                c.CommentaireCotation, c.CommentairePrix, c.DateValidite, c.DateLimiteEnlevement)
        };
    }
}
