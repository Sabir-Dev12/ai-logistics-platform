using Logistics.Application.Abstractions;
using Logistics.Domain.Common;
using Logistics.Domain.Entities.Audit;
using Logistics.Domain.Entities.Bookings;
using Logistics.Domain.Entities.ReferenceData;
using Logistics.Application.Features.ReferenceData;
using Logistics.Domain.Entities.Shipments;
// Bookings attach to voyages, so the shipment repository contract is shared.
using Logistics.Application.Features.Shipments;

namespace Logistics.Application.Features.Bookings;

/// <summary>Legacy <c>t_trace_booking.key_statut</c> codes written by the booking module.</summary>
public static class BookingTrace
{
    public const string Creation = "$CBOK";
    public const string Modification = "$MBOK";
    public const string DuplicationSource = "$DBOK";
    public const string Annulation = "$ABOK";
    public const string Suppression = "$SBOK";
    public const string AjoutLigne = "$AEB";
    public const string SuppressionLigne = "$SEB";
    public const string Depotage = "$DEPB";
    public const string AnnulationDepotage = "$ADEPB";
    public const string AffectationPrevisionnel = "$ABVP";
    public const string DesaffectationPrevisionnel = "$DBVP";
    public const string AffectationEnlevement = "$ABVE";
    public const string DesaffectationEnlevement = "$DBVE";
}

public sealed record EquipementBookingDto(string KeyLigneEquipement, string? TypeEquipement, string? NatureEquipement, decimal Qte);

public sealed record ColisageBookingDto(
    string KeyColisageBooking, string? TypeColis, decimal? Qte, decimal? Poids, bool Gerbable,
    decimal? Longueur, decimal? Largeur, decimal? Hauteur, decimal? Volume, decimal? HauteurNgerbable,
    string? TypeEquipement, string? NatureEquipement);

public sealed record BookingDto(
    string KeyBooking, string NumBooking, string? TypeBooking, string? Sens, string? TypeChargement,
    string? Incoterm, string? Mode, DateTime? DateValidite, DateTime DateCreation,
    string KeyStatutBooking, DateTime? DateStatutBooking, string? CommentaireStatutBooking,
    string? KeyStatutBookingMead, string? LibelleStatutMead,
    string? KeyClient, string? IntituleClient, string? KeyDossier, string? KeyCotation,
    string? KeyVoyagePrevisionnel, string? KeyVoyageEnlevement, bool FinEnlevement,
    string? KeyExpediteur, string? IntituleExpediteur, string? PaysExpediteur,
    string? KeyDestinataire, string? IntituleDestinataire, string? PaysDestinataire,
    string? DescriptionMarchandise, bool MarchandiseDangereuse, bool MarchandiseGerbable, bool MarchandiseFragile,
    decimal? VolumePrevision, decimal? PoidsBrutPrevision, decimal? NbTotalColis,
    bool Depote, DateTime? DateDebutDepotage, decimal? ColisDepote, decimal? ColisSortie,
    bool Attachement, bool Supprime, decimal NbEquipements,
    IReadOnlyList<EquipementBookingDto> Equipements, IReadOnlyList<ColisageBookingDto> Colisages,
    string? Commentaire = null, string? NatureOperation = null,
    string? PortDepart = null, DateTime? DateDepartPrevue = null, DateTime? DateDisponibiliteMarchandise = null,
    string? Magasin = null, string? CodeBureauDouane = null, string? ConstatDepotageBooking = null);

/// <summary>List projection — the booking list grid.</summary>
public sealed record BookingSummaryDto(
    string KeyBooking, string NumBooking, string? TypeBooking, string? TypeChargement,
    string? IntituleClient, string? IntituleAgenceBooking, DateTime DateCreation,
    DateTime? DateValidite, string KeyStatutBooking, string? KeyStatutBookingMead,
    string? KeyDossier, string? KeyVoyagePrevisionnel, string? KeyVoyageEnlevement,
    decimal? VolumePrevision, decimal NbEquipements, bool Supprime);

/// <summary>
/// Capacity of a voyage's trailer, as the prévisionnel screen showed it.
/// </summary>
/// <param name="Libre">
/// <c>isnull(capacite_remorque,0) - sum(isnull(volume_prevision,0))</c> — note the sum uses the
/// <b>forecast</b> volume, not the actual one.
/// </param>
public sealed record BookingCapacityDto(
    string KeyVoyage, decimal CapaciteRemorque, decimal VolumeReserve, decimal Libre,
    int NbBookings, decimal TauxOccupation);

public sealed record CreateBookingRequest(
    string TypeBooking,
    string? NumBooking,
    string? TypeChargement,
    string? Incoterm,
    string? Mode,
    DateTime? DateValidite,
    string? KeyAgent,
    string? IntituleAgent,
    string? CommercialBooking,
    string? NomCommercialBooking,
    string? KeyCompagnie,
    string? IntituleCompagnie,
    string? KeyExpediteur = null,
    string? IntituleExpediteur = null,
    string? VilleExpediteur = null,
    string? PaysExpediteur = null,
    string? IntitulePaysExpediteur = null,
    string? KeyDestinataire = null,
    string? IntituleDestinataire = null,
    string? VilleDestinataire = null,
    string? PaysDestinataire = null,
    string? IntitulePaysDestinataire = null,
    string? KeyPayeur = null,
    string? IntitulePayeur = null,
    string? DescriptionMarchandise = null,
    bool MarchandiseGerbable = true,
    bool MarchandiseDangereuse = false,
    string? ClasseMarchandiseDangereuse = null,
    bool MarchandiseFragile = false,
    bool Assurance = false,
    decimal? ValeurMarchandise = null,
    string? DeviseAssurance = null,
    bool TemperatureDirigee = false,
    decimal? TemperatureMin = null,
    decimal? TemperatureMax = null,
    string? KeyCotation = null,
    string? Commentaire = null,
    string? NatureOperation = null,
    string? KeyFournisseurFret = null,
    decimal? PrevisionAchatFret = null,
    decimal? PrevisionVenteFret = null);

public sealed record UpdateBookingRequest(
    string? NumBooking,
    string? TypeChargement,
    string? Incoterm,
    string? Mode,
    DateTime? DateValidite,
    string? Commentaire,
    string? NatureOperation);

public sealed record DuplicateBookingRequest(string? NumBooking, DateTime? DateValidite = null);

public sealed record MotifRequest(string? Motif);

public sealed record DepotageBookingRequest(
    DateTime DateDepotage,
    string? Constat,
    decimal? ColisDepote,
    string? Magasin,
    string? CodeBureauDouane);

public sealed record AffecterVoyageRequest(string KeyVoyage);

public sealed record EquipementBookingInput(string? TypeEquipement, string? NatureEquipement, decimal Qte);

public sealed record ColisageBookingInput(
    string? TypeColis, decimal? Qte, decimal? Poids, bool Gerbable,
    decimal? Longueur, decimal? Largeur, decimal? Hauteur, decimal? Volume,
    string? TypeEquipement = null, string? NatureEquipement = null);

public sealed record BookingSearchRequest(
    string? KeyClient = null,
    string? KeyStatutBooking = null,
    string? TypeBooking = null,
    string? KeyAgence = null,
    string? KeyVoyage = null,
    string? NumBooking = null,
    DateTime? DateCreationDu = null,
    DateTime? DateCreationAu = null,
    bool? NonAffectes = null,
    bool IncludeSupprimes = false,
    int Page = 1,
    int PageSize = 25);

public interface IBookingRepository
{
    Task<Booking?> FindAsync(string keyBooking, bool includeChildren, CancellationToken cancellationToken = default);
    Task<PagedResult<BookingSummaryDto>> SearchAsync(BookingSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the booking number is already used.
    /// </summary>
    /// <remarks>
    /// Deliberately **not** tenant-scoped: the legacy check is
    /// <c>where replace(num_booking,' ','') = …</c> with no <c>societe</c> predicate, and the same
    /// omission appears in the duplication form. So booking numbers are unique across the whole
    /// installation, not per company.
    /// </remarks>
    Task<bool> NumBookingExistsAsync(string numBooking, string? excludeKeyBooking = null, CancellationToken cancellationToken = default);

    Task<TypeDossier?> FindTypeDossierAsync(string typeDossier, CancellationToken cancellationToken = default);

    /// <summary>Sum of <c>volume_prevision</c> over the bookings attached to a voyage.</summary>
    Task<decimal> SumVolumePrevisionAsync(string keyVoyage, CancellationToken cancellationToken = default);

    Task<int> CountBookingsAsync(string keyVoyage, CancellationToken cancellationToken = default);

    /// <summary>
    /// The voyage a booking is attached to, if any.
    /// </summary>
    /// <remarks>
    /// The legacy selection predicate is
    /// <c>key_booking not in (select key_booking from t_detail_voyage_booking)</c> — with no voyage
    /// predicate — which means a booking may belong to **one** voyage at a time, globally.
    /// </remarks>
    Task<string?> FindLinkedVoyageAsync(string keyBooking, CancellationToken cancellationToken = default);

    Task<bool> IsLinkedAsync(string keyVoyage, string keyBooking, CancellationToken cancellationToken = default);

    void Add(Booking booking);
    void Add(ColisageBooking colisage);
    void Add(EquipementBooking equipement);
    void Add(DetailVoyageBooking lien);
    void Remove(ColisageBooking colisage);
    void Remove(EquipementBooking equipement);
    void Remove(DetailVoyageBooking lien);
    /// <summary>Removes the voyage link for a booking, by the composite key.</summary>
    Task RemoveAsync(string keyVoyage, string keyBooking, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IBookingService
{
    Task<PagedResult<BookingSummaryDto>> SearchAsync(BookingSearchRequest request, CancellationToken cancellationToken = default);
    Task<BookingDto> GetAsync(string keyBooking, CancellationToken cancellationToken = default);
    Task<BookingDto> CreateAsync(CreateBookingRequest request, CancellationToken cancellationToken = default);
    Task<BookingDto> UpdateAsync(string keyBooking, UpdateBookingRequest request, CancellationToken cancellationToken = default);
    Task<BookingDto> ValidateAsync(string keyBooking, CancellationToken cancellationToken = default);
    Task<BookingDto> DuplicateAsync(string keyBooking, DuplicateBookingRequest request, CancellationToken cancellationToken = default);
    Task<BookingDto> AnnulerAsync(string keyBooking, MotifRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(string keyBooking, CancellationToken cancellationToken = default);
    Task<BookingDto> DepotageAsync(string keyBooking, DepotageBookingRequest request, CancellationToken cancellationToken = default);
    Task<BookingDto> AnnulerDepotageAsync(string keyBooking, CancellationToken cancellationToken = default);
    Task<BookingDto> LierDossierAsync(string keyBooking, string keyDossier, CancellationToken cancellationToken = default);
    Task<BookingDto> DelierDossierAsync(string keyBooking, CancellationToken cancellationToken = default);
    Task<BookingDto> AffecterVoyageAsync(string keyBooking, AffecterVoyageRequest request, CancellationToken cancellationToken = default);
    Task<BookingDto> DesaffecterVoyageAsync(string keyBooking, string keyVoyage, CancellationToken cancellationToken = default);
    Task<BookingCapacityDto> GetCapaciteAsync(string keyVoyage, CancellationToken cancellationToken = default);
    Task<BookingDto> SetEquipementsAsync(string keyBooking, IReadOnlyList<EquipementBookingInput> equipements, CancellationToken cancellationToken = default);
    Task<BookingDto> SetColisagesAsync(string keyBooking, IReadOnlyList<ColisageBookingInput> colisages, CancellationToken cancellationToken = default);
}

/// <summary>
/// Booking use cases — the port of <c>frm_Creation_Booking.vb</c>, <c>frm_Liste_Booking.vb</c>,
/// <c>frm_Affectation_Booking_Voyage.vb</c> and the dépotage screens.
/// </summary>
public sealed class BookingService(
    IBookingRepository bookings,
    IDossierRepository dossiers,
    IVoyageRepository voyages,
    IReferenceDataRepository referenceData,
    IParametreSocieteProvider parametres,
    IKeyGenerator keys,
    IAuditTrail audit,
    ITenantContext tenant) : IBookingService
{
    public Task<PagedResult<BookingSummaryDto>> SearchAsync(BookingSearchRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Page < 1)
        {
            throw new ArgumentException("La page doit être supérieure ou égale à 1.", nameof(request));
        }

        return bookings.SearchAsync(request with { PageSize = Math.Clamp(request.PageSize, 1, 200) }, cancellationToken);
    }

    public async Task<BookingDto> GetAsync(string keyBooking, CancellationToken cancellationToken = default) =>
        Map(await RequireAsync(keyBooking, cancellationToken));

    public async Task<BookingDto> CreateAsync(CreateBookingRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var key = await keys.NextAsync("BOOKING", cancellationToken);

        // gener_num_booking = "Oui" means the company number is generated; anything else (including an
        // absent row) makes it the user's to supply. The previous version always generated it, which
        // silently overrode the numbering mode of every company that had opted for manual numbers — the
        // comment above it claimed the switch was checked, but the code never read it.
        var numerotationAutomatique = await parametres.EstOuiAsync(SetCodes.GenerNumBooking, cancellationToken);

        var numBooking = string.IsNullOrWhiteSpace(request.NumBooking)
            ? numerotationAutomatique
                ? await keys.NextAsync("BOOKING_NUM", cancellationToken)
                : throw new BusinessRuleException(
                    "booking.numero.obligatoire",
                    "La numérotation automatique des bookings est désactivée : le numéro doit être saisi.")
            : request.NumBooking.Trim();

        await EnsureNumBookingLibreAsync(numBooking, null, cancellationToken);

        var booking = new Booking(key, tenant.SocieteKey, numBooking, request.TypeBooking, now);

        ApplyEntete(booking, request.TypeChargement, request.Incoterm, request.Mode, request.DateValidite,
            request.Commentaire, request.NatureOperation, request.TypeBooking);

        var agence = await ResolveAgenceAsync(cancellationToken);
        booking.SetAgence(agence?.KeyAgence, agence?.IntituleAgence);
        booking.SetCompagnie(request.KeyCompagnie, request.IntituleCompagnie);
        booking.SetExpediteur(request.KeyExpediteur, request.IntituleExpediteur, request.VilleExpediteur,
            request.PaysExpediteur, request.IntitulePaysExpediteur);
        booking.SetDestinataire(request.KeyDestinataire, request.IntituleDestinataire, request.VilleDestinataire,
            request.PaysDestinataire, request.IntitulePaysDestinataire);

        booking.SetMarchandise(request.DescriptionMarchandise, request.MarchandiseGerbable,
            request.MarchandiseDangereuse, request.ClasseMarchandiseDangereuse, request.MarchandiseFragile,
            request.Assurance, request.ValeurMarchandise, request.DeviseAssurance,
            request.TemperatureDirigee, request.TemperatureMin, request.TemperatureMax);

        booking.SetPrevisionMarge(request.KeyFournisseurFret, request.PrevisionAchatFret, null, null,
            request.PrevisionVenteFret, null, null, null, null, null);

        // The client is derived, not chosen: t_type_dossier.client_dossier names which party it is.
        var typeDossier = await bookings.FindTypeDossierAsync(request.TypeBooking, cancellationToken);
        var (keyClient, intituleClient) = ResolveClient(typeDossier?.ClientDossier, request);

        booking.SetParties(keyClient, intituleClient, request.KeyAgent, request.IntituleAgent,
            request.CommercialBooking, request.NomCommercialBooking, request.KeyPayeur, request.IntitulePayeur);
        booking.LinkCotation(request.KeyCotation);

        // param_oblig_port is tested by the legacy code as `<> "Non"`, so an ABSENT row means the port is
        // REQUIRED. Hard-coding false here — as this call site previously did — relaxed a mandatory field
        // for every company that had never configured the switch.
        booking.Validate(await parametres.ObligatoireSaufNonAsync(SetCodes.ParamObligPort, cancellationToken));

        bookings.Add(booking);
        await bookings.SaveChangesAsync(cancellationToken);

        await TraceAsync(key, BookingTrace.Creation, $"Création du booking {numBooking}", cancellationToken);

        return Map(await RequireAsync(key, cancellationToken));
    }

    public async Task<BookingDto> UpdateAsync(string keyBooking, UpdateBookingRequest request, CancellationToken cancellationToken = default)
    {
        var booking = await RequireAsync(keyBooking, cancellationToken);

        if (booking.Supprime)
        {
            throw new BusinessRuleException("booking.supprime", "Un booking supprimé ne peut pas être modifié.");
        }

        await EnsureNumBookingLibreAsync(request.NumBooking ?? booking.NumBooking!, booking.KeyBooking, cancellationToken);

        // The booking type is not editable after creation — it drives the client-attribution rule and
        // the direction, so changing it would silently re-point key_client.
        ApplyEntete(booking, request.TypeChargement ?? booking.TypeChargement, request.Incoterm, request.Mode,
            request.DateValidite, request.Commentaire, request.NatureOperation, booking.TypeBooking);

        await bookings.SaveChangesAsync(cancellationToken);
        await TraceAsync(keyBooking, BookingTrace.Modification, null, cancellationToken);

        return Map(await RequireAsync(keyBooking, cancellationToken));
    }

    public async Task<BookingDto> ValidateAsync(string keyBooking, CancellationToken cancellationToken = default)
    {
        var booking = await RequireAsync(keyBooking, cancellationToken);
        // param_oblig_port is tested by the legacy code as `<> "Non"`, so an ABSENT row means the port is
        // REQUIRED. Hard-coding false here — as this call site previously did — relaxed a mandatory field
        // for every company that had never configured the switch.
        booking.Validate(await parametres.ObligatoireSaufNonAsync(SetCodes.ParamObligPort, cancellationToken));

        await bookings.SaveChangesAsync(cancellationToken);
        return Map(booking);
    }

    /// <summary>
    /// Duplicates a booking — the port of <c>frm_Liste_Booking.vb</c>.
    /// </summary>
    /// <remarks>
    /// The copy is forced to <c>$BOKC</c> and its validity date is reset to today, and both equipment
    /// and packing lines are re-keyed. The <em>source</em> booking gets a <c>$DBOK</c> trace, which is
    /// how the legacy list marks a booking as having been duplicated.
    /// </remarks>
    public async Task<BookingDto> DuplicateAsync(string keyBooking, DuplicateBookingRequest request, CancellationToken cancellationToken = default)
    {
        var source = await RequireAsync(keyBooking, cancellationToken);

        if (source.Supprime)
        {
            throw new BusinessRuleException("booking.supprime", "Un booking supprimé ne peut pas être dupliqué.");
        }

        var now = DateTime.UtcNow;
        var numBooking = string.IsNullOrWhiteSpace(request.NumBooking)
            ? await keys.NextAsync("BOOKING_NUM", cancellationToken)
            : request.NumBooking.Trim();

        await EnsureNumBookingLibreAsync(numBooking, null, cancellationToken);

        var key = await keys.NextAsync("BOOKING", cancellationToken);
        var copy = new Booking(key, tenant.SocieteKey, numBooking, source.TypeBooking, now);
        copy.UpdateEntete(numBooking, source.TypeBooking, source.TypeChargement, source.Incoterm, source.Mode,
            request.DateValidite ?? now.Date, source.LangueClient, source.Commentaire, source.NatureOperation);
        copy.SetAgence(source.AgenceBooking, source.IntituleAgenceBooking);
        copy.SetParties(source.KeyClient, source.IntituleClient, source.KeyAgent, source.IntituleAgent,
            source.CommercialBooking, source.NomCommercialBooking, source.KeyPayeur, source.IntitulePayeur);
        copy.SetCompagnie(source.KeyCompagnie, source.IntituleCompagnie);
        copy.SetExpediteur(source.KeyExpediteur, source.IntituleExpediteur, source.VilleExpediteur,
            source.PaysExpediteur, source.IntitulePaysExpediteur);
        copy.SetDestinataire(source.KeyDestinataire, source.IntituleDestinataire, source.VilleDestinataire,
            source.PaysDestinataire, source.IntitulePaysDestinataire);
        copy.SetItineraire(source.PortDepart, source.PortArrivee, source.DateDepartPrevue, source.DateArriveePrevue,
            source.TransitTime, source.DateDisponibiliteMarchandise, source.Provenance, source.Destination);
        copy.LinkCotation(source.KeyCotation);

        // The duplicate is explicitly flagged as such rather than starting at $ATRM.
        copy.SetStatut(Booking.StatutDuplication, now, null);

        bookings.Add(copy);

        foreach (var equipement in source.Equipements)
        {
            bookings.Add(new EquipementBooking(
                await keys.NextAsync("EXPLOITATION", cancellationToken), key,
                equipement.TypeEquipement, equipement.NatureEquipement, equipement.Qte));
        }

        foreach (var colisage in source.Colisages)
        {
            var ligne = new ColisageBooking(
                await keys.NextAsync("EXPLOITATION", cancellationToken), key, colisage.TypeColis, colisage.Qte);
            ligne.SetDimensions(colisage.Poids, colisage.Gerbable, colisage.Longueur, colisage.Largeur,
                colisage.Hauteur, colisage.Volume, colisage.TypeEquipement, colisage.NatureEquipement);
            bookings.Add(ligne);
        }

        await bookings.SaveChangesAsync(cancellationToken);

        await TraceAsync(key, BookingTrace.Creation, $"Duplication depuis {source.NumBooking}", cancellationToken);
        await TraceAsync(source.KeyBooking, BookingTrace.DuplicationSource, $"Dupliqué vers {numBooking}", cancellationToken);

        return Map(await RequireAsync(key, cancellationToken));
    }

    public async Task<BookingDto> AnnulerAsync(string keyBooking, MotifRequest request, CancellationToken cancellationToken = default)
    {
        var booking = await RequireAsync(keyBooking, cancellationToken);

        if (booking.IsAnnule)
        {
            throw new BusinessRuleException("booking.deja_annule", "Ce booking est déjà annulé.");
        }

        if (await bookings.FindLinkedVoyageAsync(keyBooking, cancellationToken) is { } lien)
        {
            throw new BusinessRuleException(
                "booking.annulation.affecte",
                $"Ce booking est affecté au voyage {lien} : désaffectez-le avant de l'annuler.");
        }

        booking.Annuler(DateTime.UtcNow, request.Motif);
        await bookings.SaveChangesAsync(cancellationToken);
        await TraceAsync(keyBooking, BookingTrace.Annulation, request.Motif, cancellationToken);

        return Map(booking);
    }

    /// <summary>
    /// Soft-deletes the booking (<c>supprimer=1</c> + <c>$SBOK</c>).
    /// </summary>
    /// <remarks>
    /// The legacy "supprimer" never issued a physical <c>DELETE</c>; every listing filters
    /// <c>supprimer=0</c>, so a hard delete here would silently destroy history.
    /// </remarks>
    public async Task DeleteAsync(string keyBooking, CancellationToken cancellationToken = default)
    {
        var booking = await RequireAsync(keyBooking, cancellationToken);

        if (booking.Supprime)
        {
            return;
        }

        if (await bookings.FindLinkedVoyageAsync(keyBooking, cancellationToken) is { } lien)
        {
            throw new BusinessRuleException(
                "booking.suppression.affecte",
                $"Ce booking est affecté au voyage {lien} : désaffectez-le avant de le supprimer.");
        }

        booking.SoftDelete(DateTime.UtcNow);
        await bookings.SaveChangesAsync(cancellationToken);
        await TraceAsync(keyBooking, BookingTrace.Suppression, null, cancellationToken);
    }

    public async Task<BookingDto> DepotageAsync(string keyBooking, DepotageBookingRequest request, CancellationToken cancellationToken = default)
    {
        var booking = await RequireAsync(keyBooking, cancellationToken);

        if (booking.Depote)
        {
            throw new BusinessRuleException("booking.deja_depote", "Ce booking a déjà été dépoté.");
        }

        booking.Depotage(request.DateDepotage, request.Constat, request.ColisDepote, request.Magasin, request.CodeBureauDouane);
        await bookings.SaveChangesAsync(cancellationToken);
        await TraceAsync(keyBooking, BookingTrace.Depotage, request.Constat, cancellationToken);

        return Map(booking);
    }

    public async Task<BookingDto> AnnulerDepotageAsync(string keyBooking, CancellationToken cancellationToken = default)
    {
        var booking = await RequireAsync(keyBooking, cancellationToken);

        if (!booking.Depote)
        {
            throw new BusinessRuleException("booking.non_depote", "Ce booking n'est pas dépoté.");
        }

        booking.CancelDepotage();
        await bookings.SaveChangesAsync(cancellationToken);
        await TraceAsync(keyBooking, BookingTrace.AnnulationDepotage, null, cancellationToken);

        return Map(booking);
    }

    /// <summary>
    /// Links the booking to a dossier and converts it to <c>$BOCV</c>.
    /// </summary>
    /// <remarks>
    /// Both sides are written (<c>t_booking.key_dossier</c> and <c>t_dossiers.key_booking</c>), which is
    /// what the legacy code did on every path. Writing only one side is the classic way this link rots:
    /// the booking list would show a dossier that does not point back, and vice versa. Both writes
    /// share one <c>SaveChanges</c>, so the pair cannot half-apply.
    /// </remarks>
    public async Task<BookingDto> LierDossierAsync(string keyBooking, string keyDossier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyDossier))
        {
            throw new ArgumentException("La clé du dossier est obligatoire.", nameof(keyDossier));
        }

        var booking = await RequireAsync(keyBooking, cancellationToken);

        if (booking.IsAnnule || booking.Supprime)
        {
            throw new BusinessRuleException("booking.lien.statut", "Un booking annulé ou supprimé ne peut pas être rattaché.");
        }

        if (booking.KeyDossier is { Length: > 0 } existant && existant != keyDossier)
        {
            throw new BusinessRuleException(
                "booking.lien.deja", $"Ce booking est déjà rattaché au dossier {existant}.");
        }

        var dossier = await dossiers.GetAsync(keyDossier, false, cancellationToken)
            ?? throw new EntityNotFoundException("Dossier", keyDossier);

        if (dossier.KeyBooking is { Length: > 0 } autre && autre != keyBooking)
        {
            throw new BusinessRuleException(
                "dossier.lien.deja", $"Le dossier {keyDossier} est déjà rattaché au booking {autre}.");
        }

        booking.LinkDossier(keyDossier, DateTime.UtcNow);
        dossier.LinkBooking(keyBooking);

        // One unit of work for the pair: a half-written reciprocal link is worse than no link.
        await bookings.SaveChangesAsync(cancellationToken);
        await dossiers.SaveChangesAsync(cancellationToken);

        await TraceAsync(keyBooking, BookingTrace.Modification, $"Rattachement au dossier {keyDossier}", cancellationToken);

        return Map(booking);
    }

    /// <summary>Detaches the dossier on both sides and returns the booking to <c>$ATRM</c>.</summary>
    public async Task<BookingDto> DelierDossierAsync(string keyBooking, CancellationToken cancellationToken = default)
    {
        var booking = await RequireAsync(keyBooking, cancellationToken);

        if (booking.KeyDossier is not { Length: > 0 } keyDossier)
        {
            return Map(booking);
        }

        // Detaching returns the booking to the initial state so it can be re-rattached.
        booking.SetStatut(Booking.StatutAttenteRattachement, DateTime.UtcNow, null);

        var dossier = await dossiers.GetAsync(keyDossier, false, cancellationToken);
        dossier?.LinkBooking(null);

        await bookings.SaveChangesAsync(cancellationToken);

        if (dossier is not null)
        {
            await dossiers.SaveChangesAsync(cancellationToken);
        }

        await TraceAsync(keyBooking, BookingTrace.Modification, "Détachement du dossier", cancellationToken);

        return Map(await RequireAsync(keyBooking, cancellationToken));
    }

    /// <summary>
    /// Attaches the booking to a voyage.
    /// </summary>
    /// <remarks>
    /// The legacy booking picker excludes <em>any</em> booking already present in
    /// <c>t_detail_voyage_booking</c>, regardless of which voyage holds it. A booking therefore belongs
    /// to one voyage at a time, and attaching it elsewhere is refused rather than silently
    /// double-booking the trailer.
    /// </remarks>
    public async Task<BookingDto> AffecterVoyageAsync(string keyBooking, AffecterVoyageRequest request, CancellationToken cancellationToken = default)
    {
        var booking = await RequireAsync(keyBooking, cancellationToken);

        if (booking.IsAnnule || booking.Supprime)
        {
            throw new BusinessRuleException("booking.affectation.statut", "Un booking annulé ou supprimé ne peut pas être affecté.");
        }

        var voyage = await voyages.GetAsync(request.KeyVoyage, true, cancellationToken)
            ?? throw new EntityNotFoundException("Voyage", request.KeyVoyage);

        if (await bookings.FindLinkedVoyageAsync(keyBooking, cancellationToken) is { } deja)
        {
            throw new BusinessRuleException(
                "booking.affectation.deja",
                deja == request.KeyVoyage
                    ? $"Ce booking est déjà affecté au voyage {deja}."
                    : $"Ce booking est déjà affecté au voyage {deja} ; un booking ne peut appartenir qu'à un seul voyage.");
        }

        await EnsureCapaciteAsync(booking, voyage, cancellationToken);

        bookings.Add(new DetailVoyageBooking(voyage.KeyVoyage, booking.KeyBooking));

        // The two voyage categories record the link differently: enlèvement sets fin_enlevement,
        // prévisionnel stores the voyage key.
        var previsionnel = voyage.CategorieVoyage is CategorieVoyage.Previsionnel;
        if (previsionnel)
        {
            booking.LinkVoyagePrevisionnel(voyage.KeyVoyage);
        }
        else
        {
            booking.LinkVoyageEnlevement(voyage.KeyVoyage, true);
        }

        await bookings.SaveChangesAsync(cancellationToken);
        await TraceAsync(keyBooking, previsionnel ? BookingTrace.AffectationPrevisionnel : BookingTrace.AffectationEnlevement,
            $"Affectation au voyage {voyage.KeyVoyage}", cancellationToken);

        return Map(await RequireAsync(keyBooking, cancellationToken));
    }

    public async Task<BookingDto> DesaffecterVoyageAsync(string keyBooking, string keyVoyage, CancellationToken cancellationToken = default)
    {
        var booking = await RequireAsync(keyBooking, cancellationToken);

        if (!await bookings.IsLinkedAsync(keyVoyage, keyBooking, cancellationToken))
        {
            throw new BusinessRuleException(
                "booking.desaffectation.absente", $"Ce booking n'est pas affecté au voyage {keyVoyage}.");
        }

        var voyage = await voyages.GetAsync(keyVoyage, false, cancellationToken);
        var previsionnel = voyage?.CategorieVoyage is CategorieVoyage.Previsionnel or null;

        if (previsionnel)
        {
            booking.LinkVoyagePrevisionnel(null);
        }
        else
        {
            booking.LinkVoyageEnlevement(null, false);
        }

        await bookings.RemoveAsync(keyVoyage, keyBooking, cancellationToken);
        await bookings.SaveChangesAsync(cancellationToken);
        await TraceAsync(keyBooking, previsionnel ? BookingTrace.DesaffectationPrevisionnel : BookingTrace.DesaffectationEnlevement,
            $"Désaffectation du voyage {keyVoyage}", cancellationToken);

        return Map(await RequireAsync(keyBooking, cancellationToken));
    }

    /// <summary>
    /// Remaining trailer capacity for a voyage, using <c>volume_prevision</c>.
    /// </summary>
    /// <remarks>
    /// The legacy screen displayed <c>txt_CapaciteRemorque - txt_CapaciteBooking</c> and left the
    /// comparison to the eye. Reporting the occupancy rate makes an over-booked trailer visible
    /// instead of merely permitted.
    /// </remarks>
    public async Task<BookingCapacityDto> GetCapaciteAsync(string keyVoyage, CancellationToken cancellationToken = default)
    {
        var voyage = await voyages.GetAsync(keyVoyage, false, cancellationToken)
            ?? throw new EntityNotFoundException("Voyage", keyVoyage);

        var capacite = voyage.CapaciteRemorque ?? 0m;
        var reserve = await bookings.SumVolumePrevisionAsync(keyVoyage, cancellationToken);
        var nb = await bookings.CountBookingsAsync(keyVoyage, cancellationToken);
        var libre = capacite - reserve;

        return new BookingCapacityDto(keyVoyage, capacite, reserve, libre, nb,
            capacite <= 0m ? 0m : Math.Round(reserve / capacite * 100m, 2, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Replaces the equipment lines wholesale.
    /// </summary>
    /// <remarks>
    /// The legacy screen diffed rows and issued per-row INSERT/UPDATE/DELETE. Replacing the set is
    /// equivalent in effect and avoids the surrogate-key bookkeeping; the line keys change, which is
    /// invisible to callers because they are regenerated on read.
    /// </remarks>
    public async Task<BookingDto> SetEquipementsAsync(string keyBooking, IReadOnlyList<EquipementBookingInput> equipements, CancellationToken cancellationToken = default)
    {
        var booking = await RequireAsync(keyBooking, cancellationToken);
        EnsureModifiable(booking);

        foreach (var existant in booking.Equipements.ToArray())
        {
            bookings.Remove(existant);
        }

        foreach (var input in equipements)
        {
            bookings.Add(new EquipementBooking(
                await keys.NextAsync("EXPLOITATION", cancellationToken), keyBooking,
                input.TypeEquipement, input.NatureEquipement, input.Qte));
        }

        await bookings.SaveChangesAsync(cancellationToken);

        if (equipements.Count > 0)
        {
            await TraceAsync(keyBooking, BookingTrace.AjoutLigne, $"{equipements.Count} ligne(s) d'équipement", cancellationToken);
        }

        return Map(await RequireAsync(keyBooking, cancellationToken));
    }

    /// <summary>
    /// Replaces the packing lines wholesale, mirroring the legacy
    /// <c>delete from t_colisage_booking where key_booking=…</c> then re-insert.
    /// </summary>
    public async Task<BookingDto> SetColisagesAsync(string keyBooking, IReadOnlyList<ColisageBookingInput> colisages, CancellationToken cancellationToken = default)
    {
        var booking = await RequireAsync(keyBooking, cancellationToken);
        EnsureModifiable(booking);

        foreach (var existant in booking.Colisages.ToArray())
        {
            bookings.Remove(existant);
        }

        booking.ClearColisages();

        foreach (var input in colisages)
        {
            var ligne = new ColisageBooking(
                await keys.NextAsync("EXPLOITATION", cancellationToken), keyBooking, input.TypeColis, input.Qte);

            ligne.SetDimensions(input.Poids, input.Gerbable, input.Longueur, input.Largeur, input.Hauteur,
                input.Volume, input.TypeEquipement, input.NatureEquipement);
            bookings.Add(ligne);
        }

        // The legacy code resets the actual measures whenever the packing detail is regenerated,
        // because they are derived from it.
        booking.ResetMesuresReelles();

        await bookings.SaveChangesAsync(cancellationToken);

        if (colisages.Count > 0)
        {
            await TraceAsync(keyBooking, BookingTrace.AjoutLigne, $"{colisages.Count} ligne(s) de colisage", cancellationToken);
        }

        return Map(await RequireAsync(keyBooking, cancellationToken));
    }

    private void EnsureModifiable(Booking booking)
    {
        if (booking.Supprime)
        {
            throw new BusinessRuleException("booking.supprime", "Un booking supprimé ne peut pas être modifié.");
        }

        // The legacy dimension screen refuses edits once the dossier has been dépoté.
        if (booking.Depote)
        {
            throw new BusinessRuleException(
                "booking.depote", "Les lignes d'un booking dépoté ne peuvent plus être modifiées.");
        }
    }

    /// <summary>
    /// Refuses an attachment that would overflow the trailer.
    /// </summary>
    /// <remarks>
    /// The legacy UI only <em>displayed</em> the free capacity; nothing stopped a booking from being
    /// attached beyond it. Enforcing it here is a deliberate tightening, documented in the README.
    /// </remarks>
    private async Task EnsureCapaciteAsync(Booking booking, Voyage voyage, CancellationToken cancellationToken)
    {
        var capacite = voyage.CapaciteRemorque ?? 0m;
        if (capacite <= 0m)
        {
            return;
        }

        var reserve = await bookings.SumVolumePrevisionAsync(voyage.KeyVoyage, cancellationToken);
        var demande = booking.VolumePrevision ?? 0m;

        if (reserve + demande > capacite)
        {
            throw new BusinessRuleException(
                "booking.capacite",
                $"Capacité du voyage {voyage.KeyVoyage} insuffisante : {reserve + demande} m³ demandés pour {capacite} m³ disponibles.");
        }
    }

    private static void ApplyEntete(
        Booking booking, string? typeChargement, string? incoterm, string? mode, DateTime? dateValidite,
        string? commentaire, string? natureOperation, string? typeBooking)
    {
        booking.UpdateEntete(booking.NumBooking, typeBooking, typeChargement, incoterm, mode, dateValidite,
            booking.LangueClient, commentaire, natureOperation);
    }

    private static (string? KeyClient, string? IntituleClient) ResolveClient(string? clientDossier, CreateBookingRequest request) =>
        (clientDossier ?? string.Empty).ToUpperInvariant() switch
        {
            "EXPEDITEUR" => (request.KeyExpediteur, request.IntituleExpediteur),
            "DESTINATAIRE" => (request.KeyDestinataire, request.IntituleDestinataire),
            "PAYEUR" => (request.KeyPayeur, request.IntitulePayeur),
            // No rule configured: fall back to the payer, as the legacy form left it blank.
            _ => (request.KeyPayeur, request.IntitulePayeur)
        };

    private async Task EnsureNumBookingLibreAsync(string numBooking, string? excludeKeyBooking, CancellationToken cancellationToken)
    {
        if (await bookings.NumBookingExistsAsync(numBooking, excludeKeyBooking, cancellationToken))
        {
            // Booking numbers are unique installation-wide, not per company.
            throw new DuplicateEntityException("Booking", "num_booking", numBooking);
        }
    }

    private async Task<Agence?> ResolveAgenceAsync(CancellationToken cancellationToken)
    {
        // The legacy form took the agency from the session, not from the request body.
        var agenceKey = tenant.AgenceKey;
        if (string.IsNullOrWhiteSpace(agenceKey))
        {
            return null;
        }

        return await referenceData.FindAsync<Agence>(agenceKey!);
    }

    private async Task<Booking> RequireAsync(string keyBooking, CancellationToken cancellationToken) =>
        await bookings.FindAsync(keyBooking, true, cancellationToken)
        ?? throw new EntityNotFoundException("Booking", keyBooking);

    private Task TraceAsync(string key, string code, string? commentaire, CancellationToken cancellationToken) =>
        audit.TraceAsync<TraceBooking>(key, code, commentaire, null, 0, cancellationToken);

    private static BookingDto Map(Booking b) => new(
        b.KeyBooking, b.NumBooking ?? string.Empty, b.TypeBooking, b.Sens, b.TypeChargement, b.Incoterm, b.Mode,
        b.DateValidite, b.DateCreation, b.KeyStatutBooking, b.DateStatutBooking, b.CommentaireStatutBooking,
        b.KeyStatutBookingMead, b.LibelleStatutMead, b.KeyClient, b.IntituleClient, b.KeyDossier, b.KeyCotation,
        b.KeyVoyagePrevisionnel, b.KeyVoyageEnlevement, b.FinEnlevement,
        b.KeyExpediteur, b.IntituleExpediteur, b.PaysExpediteur,
        b.KeyDestinataire, b.IntituleDestinataire, b.PaysDestinataire,
        b.DescriptionMarchandise, b.MarchandiseDangereuse, b.MarchandiseGerbable, b.MarchandiseFragile,
        b.VolumePrevision, b.PoidsBrutPrevision, b.NbTotalColis,
        b.Depote, b.DateDebutDepotage, b.ColisDepote, b.ColisSortie, b.Attachement, b.Supprime, b.NbEquipements,
        b.Equipements.Select(e => new EquipementBookingDto(e.KeyLigneEquipement, e.TypeEquipement, e.NatureEquipement, e.Qte)).ToArray(),
        b.Colisages.Select(c => new ColisageBookingDto(c.KeyColisageBooking, c.TypeColis, c.Qte, c.Poids, c.Gerbable,
            c.Longueur, c.Largeur, c.Hauteur, c.Volume, c.HauteurNgerbable, c.TypeEquipement, c.NatureEquipement)).ToArray(),
        b.Commentaire, b.NatureOperation, b.PortDepart, b.DateDepartPrevue, b.DateDisponibiliteMarchandise,
        b.Magasin, b.CodeBureauDouane, b.ConstatDepotageBooking);
}
