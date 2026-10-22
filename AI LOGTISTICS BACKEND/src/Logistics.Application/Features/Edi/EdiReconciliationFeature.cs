using Logistics.Application.Abstractions;
using Logistics.Application.Features.ReferenceData;
// Promotion creates voyages and dossiers, so the shipment repository contracts are shared.
using Logistics.Application.Features.Shipments;
using Logistics.Domain.Common;
using Logistics.Domain.Entities.Clients;
using Logistics.Domain.Entities.Edi;
using Logistics.Domain.Entities.ReferenceData;
using Logistics.Domain.Entities.Shipments;

namespace Logistics.Application.Features.Edi;

/// <summary>A reference the file mentioned but the application does not know.</summary>
public sealed record EdiAnomalieReconciliation(string Type, string Reference, string Code, string Message);

/// <summary>Outcome of reconciling the staged slice.</summary>
/// <param name="Promouvable">
/// True when nothing is left unresolved. The legacy promotion aborted if <b>any</b> anomaly remained on a
/// voyage or on any of its dossiers, so a partial promotion is not offered.
/// </param>
public sealed record EdiReconciliationResultat(
    bool Promouvable,
    int NbVoyages,
    int NbDossiers,
    IReadOnlyList<EdiAnomalieReconciliation> Anomalies);

/// <summary>Outcome of promoting the reconciled slice.</summary>
public sealed record EdiPromotionResultat(int NbVoyagesCrees, int NbDossiersCrees, IReadOnlyList<string> ReferencesVoyages);

public interface IEdiReconciliationRepository
{
    Task<IReadOnlyList<EdiVoyageStaging>> GetVoyagesStagedAsync(string societe, string? agence, string? utilisateur, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EdiDossierStaging>> GetDossiersStagedAsync(string societe, string? agence, string? utilisateur, CancellationToken cancellationToken = default);
    Task<int> CompterDossiersAsync(string numVoyage, string societe, string? utilisateur, CancellationToken cancellationToken = default);

    /// <summary>Resolves a port by its <b>label</b> — the file carries labels, not port codes.</summary>
    Task<Port?> FindPortParLibelleAsync(string libelle, CancellationToken cancellationToken = default);

    /// <summary>Resolves a client by its <c>ref_client</c>, which the file supplies as the destination code.</summary>
    Task<Client?> FindClientParRefAsync(string refClient, CancellationToken cancellationToken = default);

    Task<Incoterm?> FindIncotermParEdiAsync(string incotermEdi, CancellationToken cancellationToken = default);
    Task<Devise?> FindDeviseParEdiAsync(string deviseEdi, CancellationToken cancellationToken = default);
    Task<Cod?> FindCodParEdiAsync(string codEdi, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a voyage already carries this agent reference — the duplicate-import guard.
    /// </summary>
    /// <remarks>
    /// The legacy check is <c>novoyage not in (select ref_agent from t_voyages where supprimer=0)</c>: the
    /// file's voyage number is stored as the voyage's <c>ref_agent</c>, so re-importing a file would
    /// otherwise duplicate every voyage in it.
    /// </remarks>
    Task<bool> RefAgentExisteAsync(string refAgent, CancellationToken cancellationToken = default);

    /// <summary>
    /// The display name of a member of personnel, for resolving a commercial key.
    /// </summary>
    /// <remarks>
    /// The legacy's EDI insert takes <c>commercial_dossier</c> from <c>KEY_COMMERCIAL_CLT</c> and
    /// <c>nom_commercial_dossier</c> from <c>INTITULE_COMMERCIAL_CLT</c> — but the second is a <b>join alias</b>
    /// in the client expansion query, not a column of <c>t_clients</c>. The client row holds only the key, so the
    /// name has to be resolved from the personnel record it points at.
    /// </remarks>
    Task<string?> FindIntitulePersonnelAsync(string keyPersonnel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces a client's <c>ref_client</c> with the destination code the imported file supplied.
    /// </summary>
    /// <remarks>
    /// <b>A write to master data performed by an import.</b> The legacy does this at
    /// <c>frm_Integration_EDI_IMPORT_STC.vb:1130</c>, and it is surprising enough to state plainly: importing a
    /// file changes the client record, and the value written comes from the <i>file</i> rather than from
    /// anything a user entered on the client screen.
    /// </remarks>
    Task DefinirRefClientAsync(string keyClient, string refClient, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IEdiReconciliationService
{
    /// <summary>Resolves the staged slice against the referentials and records what could not be resolved.</summary>
    Task<EdiReconciliationResultat> ReconcilierAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates voyages and dossiers from a fully reconciled slice.
    /// </summary>
    /// <remarks>
    /// Refuses outright while any anomaly remains, matching the legacy promotion.
    /// </remarks>
    Task<EdiPromotionResultat> PromouvoirAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Referential reconciliation and promotion for the STC/EDI import.
/// </summary>
/// <remarks>
/// <para>
/// This is the step the parser deliberately leaves out. The file speaks <b>EDI codes</b> — port labels, a
/// destination reference, two-character incoterm and currency codes, a COD code — while the application
/// speaks primary keys. Resolving one onto the other is the whole job, and every failure mode is a
/// missing or wrong key rather than a malformed field.
/// </para>
/// <para>
/// The special COD value <c>"10"</c> means <b>no COD</b>, so it short-circuits the lookup instead of
/// failing it; treating it as a code would mark every COD-less dossier invalid.
/// </para>
/// </remarks>
public sealed class EdiReconciliationService(
    IEdiReconciliationRepository referentiels,
    IEdiStagingRepository staging,
    IVoyageRepository voyages,
    IDossierRepository dossiers,
    IRapportPoidsVolumeProvider rapports,
    IKeyGenerator keys,
    ITenantContext tenant) : IEdiReconciliationService
{
    public async Task<EdiReconciliationResultat> ReconcilierAsync(CancellationToken cancellationToken = default)
    {
        var societe = tenant.SocieteKey;
        var agence = tenant.AgenceKey;
        var utilisateur = tenant.UserDisplayName;

        var entetes = await referentiels.GetVoyagesStagedAsync(societe, agence, utilisateur, cancellationToken);
        var details = await referentiels.GetDossiersStagedAsync(societe, agence, utilisateur, cancellationToken);
        var anomalies = new List<EdiAnomalieReconciliation>();

        // The staged voyage numbers, so a detail can be checked against what the same file declared.
        var voyagesDuFichier = entetes
            .Select(e => e.NumVoyage)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entete in entetes)
        {
            entete.ReinitialiserAnomalies();

            if (await ReferentielAbsent(entete.PortDepart, referentiels.FindPortParLibelleAsync, cancellationToken))
            {
                Ajouter(entete, null, anomalies, "Voyage", entete.NumVoyage, entete.PortDepart, EdiStc.Anomalies.PortDepart);
            }

            if (await ReferentielAbsent(entete.PortArrive, referentiels.FindPortParLibelleAsync, cancellationToken))
            {
                Ajouter(entete, null, anomalies, "Voyage", entete.NumVoyage, entete.PortArrive, EdiStc.Anomalies.PortArrive);
            }

            // A voyage already carrying this ref_agent has been imported before.
            if (!string.IsNullOrWhiteSpace(entete.NumVoyage)
                && await referentiels.RefAgentExisteAsync(entete.NumVoyage!, cancellationToken))
            {
                entete.AjouterAnomalie("Voyage déjà intégré");
                anomalies.Add(new EdiAnomalieReconciliation("Voyage", entete.NumVoyage!, entete.NumVoyage!,
                    "Voyage déjà intégré"));
            }

            // NB_DOSSIERS is a promise about the file; check it against what the file actually contains.
            if (int.TryParse(entete.NbDossiers, out var attendus))
            {
                var reels = details.Count(d => string.Equals(d.NumVoyage, entete.NumVoyage, StringComparison.OrdinalIgnoreCase));
                if (attendus != reels)
                {
                    entete.AjouterAnomalie($"Nb dossiers invalide ({attendus} annoncés, {reels} présents)");
                    anomalies.Add(new EdiAnomalieReconciliation("Voyage", entete.NumVoyage!, entete.NbDossiers!,
                        $"Nb dossiers invalide ({attendus} annoncés, {reels} présents)"));
                }
            }
        }

        foreach (var detail in details)
        {
            detail.ReinitialiserAnomalies();

            // A detail whose voyage is absent or unknown cannot be attached to anything.
            if (string.IsNullOrWhiteSpace(detail.NumVoyage)
                || !voyagesDuFichier.Contains(detail.NumVoyage!))
            {
                Ajouter(null, detail, anomalies, "Dossier", detail.NumDossier ?? detail.NumVoyage ?? "?",
                    detail.NumVoyage ?? string.Empty, EdiStc.Anomalies.Voyage);

                // Nothing downstream can succeed without a voyage, so stop here for this row.
                continue;
            }

            var incoterm = string.IsNullOrWhiteSpace(detail.Incoterm)
                ? null
                : await referentiels.FindIncotermParEdiAsync(detail.Incoterm!, cancellationToken);
            if (incoterm is null)
            {
                Ajouter(null, detail, anomalies, "Dossier", detail.NumDossier ?? "?", detail.Incoterm ?? string.Empty,
                    EdiStc.Anomalies.Incoterm);
            }

            var devise = string.IsNullOrWhiteSpace(detail.Devise)
                ? null
                : await referentiels.FindDeviseParEdiAsync(detail.Devise!, cancellationToken);
            if (devise is null)
            {
                Ajouter(null, detail, anomalies, "Dossier", detail.NumDossier ?? "?", detail.Devise ?? string.Empty,
                    EdiStc.Anomalies.Devise);
            }

            // "10" means no COD at all, so it is an absence rather than a lookup failure.
            Cod? cod = null;
            if (!string.IsNullOrWhiteSpace(detail.Cod) && detail.Cod != EdiStc.CodAucun)
            {
                cod = await referentiels.FindCodParEdiAsync(detail.Cod!, cancellationToken);
                if (cod is null)
                {
                    Ajouter(null, detail, anomalies, "Dossier", detail.NumDossier ?? "?", detail.Cod!,
                        EdiStc.Anomalies.Cod);
                }
            }

            var client = string.IsNullOrWhiteSpace(detail.CodeDestinataire)
                ? null
                : await referentiels.FindClientParRefAsync(detail.CodeDestinataire!, cancellationToken);
            if (client is null)
            {
                Ajouter(null, detail, anomalies, "Dossier", detail.NumDossier ?? "?",
                    detail.CodeDestinataire ?? string.Empty, EdiStc.Anomalies.Client);
            }
            else if (string.IsNullOrWhiteSpace(client.LieuDedouanement))
            {
                // The legacy code fell back to the client's own customs location.
                detail.SetReconciliation(incoterm?.IncotermCode, devise?.CodeDevise, cod?.IntituleCod,
                    client.KeyClient, client.RaisonSociale, null, null, null);

                Ajouter(null, detail, anomalies, "Dossier", detail.NumDossier ?? "?", client.KeyClient,
                    EdiStc.Anomalies.LieuDedouanement);
            }

            // The commercial is stored as a KEY and, separately, as a display name. Passing the key for both —
            // which an earlier version did — left the name column holding a personnel key, so anything showing
            // the commercial's name showed a code. The name is resolved from the personnel row the key points at,
            // because the legacy's INTITULE_COMMERCIAL_CLT is a join alias rather than a stored column.
            var nomCommercial = string.IsNullOrWhiteSpace(client?.Commercial)
                ? null
                : await referentiels.FindIntitulePersonnelAsync(client!.Commercial!, cancellationToken);

            detail.SetReconciliation(
                incoterm?.IncotermCode,
                devise?.CodeDevise,
                cod?.IntituleCod,
                client?.KeyClient,
                client?.RaisonSociale,
                client?.Commercial,
                nomCommercial,
                client?.LieuDedouanement);
        }

        await referentiels.SaveChangesAsync(cancellationToken);

        var promouvable = entetes.All(e => !e.ADesAnomalies) && details.All(d => !d.ADesAnomalies);

        return new EdiReconciliationResultat(promouvable, entetes.Count, details.Count, anomalies);
    }

    /// <summary>
    /// Creates one voyage per staged header and one dossier per staged detail.
    /// </summary>
    /// <remarks>
    /// The file's voyage number becomes the voyage's <c>ref_agent</c> — that is the column the
    /// duplicate-import guard reads, and the reason a re-import is refused rather than silently
    /// duplicating. Equipment detail lines from the file are <b>not</b> created yet; see the README.
    /// </remarks>
    public async Task<EdiPromotionResultat> PromouvoirAsync(CancellationToken cancellationToken = default)
    {
        var societe = tenant.SocieteKey;
        var agence = tenant.AgenceKey;
        var utilisateur = tenant.UserDisplayName;

        var entetes = await referentiels.GetVoyagesStagedAsync(societe, agence, utilisateur, cancellationToken);
        var details = await referentiels.GetDossiersStagedAsync(societe, agence, utilisateur, cancellationToken);

        if (entetes.Count == 0 && details.Count == 0)
        {
            throw new BusinessRuleException("edi.promotion.vide", "Aucune ligne en attente d'intégration.");
        }

        if (entetes.Any(e => e.ADesAnomalies) || details.Any(d => d.ADesAnomalies))
        {
            // The legacy promotion refused while any anomaly remained, so a partial import is not offered.
            throw new BusinessRuleException(
                "edi.promotion.anomalies",
                "Des anomalies subsistent : l'intégration est refusée tant qu'elles ne sont pas corrigées.");
        }

        var now = DateTime.UtcNow;
        var parNumVoyage = new Dictionary<string, Voyage>(StringComparer.OrdinalIgnoreCase);
        var references = new List<string>();

        foreach (var entete in entetes)
        {
            var key = await keys.NextAsync("VOYAGE", cancellationToken);
            var voyage = new Voyage(key, societe, entete.TypeDossier, entete.NumVoyage, now);

            // The file's voyage number is stored as ref_agent, which is exactly what the duplicate-import
            // guard reads — so it is the agent reference, not the vessel, that carries it.
            voyage.SetPartenaires(
                keyAgent: null, intituleAgent: null, refAgent: entete.NumVoyage,
                keyCompagnie: null, intituleCompagnie: null, refCompagnie: null,
                keyConsignataire: null, intituleConsignataire: null, refConsignataire: null,
                keyTractionnaire: null, intituleTractionnaire: null, refTractionnaire: null);

            voyage.SetItineraire(
                dateDepartPrevu: null, dateArriveePrevu: null,
                dateDepart: DateStaging(entete.DateDepart), dateArrivee: DateStaging(entete.DateArrivee),
                portDepart: entete.PortDepart, portArrivee: entete.PortArrive,
                navireDepart: entete.Navire, navireArrivee: null,
                dateDepartTrans: null, dateArriveeTrans: null, portTransbordement: null);

            voyages.Add(voyage);
            references.Add(entete.NumVoyage ?? key);

            await AjouterEquipementAsync(entete, voyage, cancellationToken);

            if (!string.IsNullOrWhiteSpace(entete.NumVoyage))
            {
                parNumVoyage[entete.NumVoyage!] = voyage;
            }
        }

        var nbDossiers = 0;

        foreach (var detail in details)
        {
            if (string.IsNullOrWhiteSpace(detail.NumVoyage)
                || !parNumVoyage.TryGetValue(detail.NumVoyage!, out var voyage))
            {
                continue;
            }

            var key = await keys.NextAsync("DOSSIER", cancellationToken);
            var dossier = new Dossier(key, societe, detail.NumDossier, voyage.TypeVoyage, now);

            // The ratio row the legacy import always uses — `produit = 'ROUTIER'` is hard-coded in
            // frm_Integration_EDI_IMPORT_STC.vb:927, because an STC file cannot express which row applies.
            var ratios = await rapports.PourProduitAsync(RapportPoidsVolume.ProduitRoutier, cancellationToken);

            var poidsBrut = NombreStaging(detail.Poids);
            var volume = NombreStaging(detail.Volume);

            // Taxable measures are what a voyage's sales amount is split by, so an imported dossier without
            // them cannot be allocated against. The legacy computes them here with the same max-of-three rule
            // as the quotation screen (lines 1055-1080).
            var mesures = MesuresTaxables.Calculer(
                poidsReel: poidsBrut ?? 0m,
                volumeReel: volume,
                volumeNonGerbable: null,
                mplReel: null,
                equivalenceVolumeKgs: ratios?.VolumeKgs,
                equivalenceMplKgs: ratios?.MplKgs,
                equivalenceMplVolume: ratios?.MplVolume);

            dossier.SetMarchandise(
                descriptionMarchandise: detail.Marchandise,
                gerbable: false, dangereuse: false, classeDangereuse: null, fragile: false, assurance: false,
                valeurMarchandise: NombreStaging(detail.ValeurMarchandise), deviseValeur: detail.DeviseClt,
                tauxChangeValMarch: null,
                nbColis: NombreStaging(detail.Colis), typeColis: detail.Emballage,
                suspendu: null, marque: detail.NomDestinataire);

            dossier.SetMesures(
                poidsBrut: poidsBrut, volume: volume, mpl: null,
                poidsTaxable: mesures.PoidsTaxable, volumeTaxable: mesures.VolumeTaxable,
                equivalenceVolumePoids: ratios?.VolumeKgs, equivalenceMplPoids: ratios?.MplKgs,
                equivalenceMplVolume: ratios?.MplVolume);

            // KeyVoyageInter is the single active link; DossierVoyage is the pairing audit trail.
            dossier.AssignVoyage(voyage.KeyVoyage, PositionStaging(detail.Position));

            dossiers.Add(dossier);
            dossiers.Add(new DossierVoyage(dossier.KeyDossier, voyage.KeyVoyage));

            // The packing line. The legacy writes it unconditionally, with the package type from the file's
            // EMBALLAGE field and the package count from COLIS (frm_Integration_EDI_IMPORT_STC.vb:1104).
            // Without it the dossier has totals but no packing detail to print.
            dossiers.Add(new ColisageDossier(
                dossier.KeyDossier,
                string.IsNullOrWhiteSpace(detail.Emballage) ? EdiStc.EmballageDefaut : detail.Emballage,
                NombreStaging(detail.Colis) ?? 0m,
                detail.Marchandise));

            // COD. The legacy writes it only when the file carries one (frm_Integration_EDI_IMPORT_STC.vb:1106)
            // and draws its key from the "COD" sequence — separate from the fuel family's shared one.
            if (!string.IsNullOrWhiteSpace(detail.CodClt))
            {
                dossiers.Add(new CodDossier(
                    await keys.NextAsync("COD", cancellationToken), dossier.KeyDossier, detail.CodClt!));
            }

            // Up to five supplier invoices, each written only when its number is present (lines 1109-1123).
            // These are what the supplier billed, not the dossier's own sales invoice.
            (string? Numero, string? Date, string? Montant)[] factures =
            [
                (detail.Facture1, detail.Facture1Date, detail.Facture1Montant),
                (detail.Facture2, detail.Facture2Date, detail.Facture2Montant),
                (detail.Facture3, detail.Facture3Date, detail.Facture3Montant),
                (detail.Facture4, detail.Facture4Date, detail.Facture4Montant),
                (detail.Facture5, detail.Facture5Date, detail.Facture5Montant)
            ];

            foreach (var (numero, date, montant) in factures)
            {
                if (string.IsNullOrWhiteSpace(numero))
                {
                    continue;
                }

                dossiers.Add(new FactureFournisseurDossier(
                    dossier.KeyDossier, numero!, DateStaging(date), NombreStaging(montant), detail.DeviseClt));
            }

            nbDossiers++;
        }

        await voyages.SaveChangesAsync(cancellationToken);
        await dossiers.SaveChangesAsync(cancellationToken);

        // The import writes back to the CLIENT MASTER.
        //
        // frm_Integration_EDI_IMPORT_STC.vb:1130 sets every client named in the file to the highest destination
        // code the file supplied for it. Two things about that are worth stating rather than burying:
        //
        //  * It is master data being changed by an import. A user who has set a client's reference deliberately
        //    will find it replaced by whatever a file happened to carry.
        //  * `max()` is a LEXICOGRAPHIC maximum over the codes, reproduced here rather than "the last one seen",
        //    because the two differ as soon as codes have different lengths.
        //
        // DIVERGENCE, deliberate: the legacy would also write NULL for a client whose staging lines all have a
        // blank destination code — `max()` over an empty set of values is NULL — silently BLANKING an existing
        // reference. Clients with no destination code in the file are skipped here instead. The legacy behaviour
        // is more plausibly an oversight in a statement that was never meant to erase anything than an intent,
        // and it is a data-loss path no caller asked for.
        var parClient = details
            .Where(d => !string.IsNullOrWhiteSpace(d.KeyClient) && !string.IsNullOrWhiteSpace(d.CodeDestinataire))
            .GroupBy(d => d.KeyClient!, StringComparer.OrdinalIgnoreCase);

        foreach (var groupe in parClient)
        {
            var reference = groupe.Select(d => d.CodeDestinataire!.Trim()).Max(StringComparer.Ordinal)!;

            await referentiels.DefinirRefClientAsync(groupe.Key, reference, cancellationToken);
        }

        // The staging slice is consumed by a successful promotion.
        await staging.PurgerAsync(societe, agence, utilisateur, cancellationToken);
        await staging.SaveChangesAsync(cancellationToken);

        return new EdiPromotionResultat(entetes.Count, nbDossiers, references);
    }

    /// <summary>
    /// Creates the voyage's equipment lines from the file's vehicle and box numbers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Transcribed from <c>frm_Integration_EDI_IMPORT_STC.vb:996-1048</c>. Two branches, discriminated by the
    /// file's <c>TYPE_dossier</c>, and every line's key comes from the <b><c>"EXPLOITATION"</c></b> sequence —
    /// a third distinct sequence, alongside <c>"VOYAGE"</c> and the fuel family's <c>"GASOIL"</c>.
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <b>"RI"</b> (road) — one <c>REMORQUE</c> line whose nature falls back to <c>REMORQUE</c> when the
    /// fleet row has none, plus a <c>CAISSE</c> line when the file names a box. Each gets its own key and
    /// exactly one detail row, and only the trailer's detail carries the seal number.
    /// </item>
    /// <item>
    /// <b>"MI"</b> (maritime) — a single <c>TC</c> line whose <c>qte</c> is the <b>number of detail rows</b>
    /// (two when both a box and a vehicle are given, otherwise one), with both rows sharing that one key.
    /// </item>
    /// </list>
    /// <para>
    /// The <c>t_materiel</c> reference is resolved from the plate number rather than carried in the file, and
    /// is <b>null for the maritime branch</b> even when the plate matches a fleet row — the legacy hardcodes
    /// null there, because a container in transit is not necessarily the company's asset.
    /// </para>
    /// </remarks>
    private async Task AjouterEquipementAsync(
        EdiVoyageStaging entete, Voyage voyage, CancellationToken cancellationToken)
    {
        var numVehicule = entete.Vehicule;
        var numCaisse = entete.Caisse;
        var aVehicule = !string.IsNullOrWhiteSpace(numVehicule);
        var aCaisse = !string.IsNullOrWhiteSpace(numCaisse);

        if (string.Equals(entete.TypeDossier, "RI", StringComparison.OrdinalIgnoreCase))
        {
            var remorque = await voyages.FindMaterielParImmatriculationAsync("REMORQUE", numVehicule, cancellationToken);
            var keyRemorque = await keys.NextAsync("EXPLOITATION", cancellationToken);

            var ligne = new EquipementVoyage(
                keyRemorque, voyage.KeyVoyage, "REMORQUE",
                string.IsNullOrWhiteSpace(remorque?.NatureMateriel) ? "REMORQUE" : remorque!.NatureMateriel!);
            ligne.SetQuantity(1);

            var detail = new DetailEquipementVoyage(keyRemorque, remorque?.Id, numVehicule);
            detail.SetNumPlomb(entete.Plomb);
            ligne.AddDetail(detail);
            voyages.Add(ligne);

            if (aCaisse)
            {
                var caisse = await voyages.FindMaterielParImmatriculationAsync("CAISSE", numCaisse, cancellationToken);
                var keyCaisse = await keys.NextAsync("EXPLOITATION", cancellationToken);

                var ligneCaisse = new EquipementVoyage(keyCaisse, voyage.KeyVoyage, "CAISSE", "CAISSE");
                ligneCaisse.SetQuantity(1);
                // No seal on the box line: the legacy passes NULL for it.
                ligneCaisse.AddDetail(new DetailEquipementVoyage(keyCaisse, caisse?.Id, numCaisse));
                voyages.Add(ligneCaisse);
            }

            return;
        }

        if (string.Equals(entete.TypeDossier, "MI", StringComparison.OrdinalIgnoreCase) && (aVehicule || aCaisse))
        {
            // One key for the line, shared by its detail rows.
            var keyTc = await keys.NextAsync("EXPLOITATION", cancellationToken);

            var ligneTc = new EquipementVoyage(keyTc, voyage.KeyVoyage, "TC", "TC");
            // qte counts the units attached below, which is what makes qte=2 mean two containers.
            ligneTc.SetQuantity(aVehicule && aCaisse ? 2 : 1);

            if (aCaisse)
            {
                ligneTc.AddDetail(new DetailEquipementVoyage(keyTc, null, numCaisse));
            }

            if (aVehicule)
            {
                ligneTc.AddDetail(new DetailEquipementVoyage(keyTc, null, numVehicule));
            }

            voyages.Add(ligneTc);
        }
    }

    /// <summary>
    /// Parses a staging date.
    /// </summary>
    /// <remarks>
    /// Staging keeps dates as <b>text</b> — the legacy validation is <c>IsDate()</c> on the raw field, so
    /// an unparseable value is a data problem the reconciliation surfaces rather than a crash here.
    /// </remarks>
    private static DateTime? DateStaging(string? valeur) =>
        DateTime.TryParse(valeur, System.Globalization.CultureInfo.GetCultureInfo("fr-FR"),
            System.Globalization.DateTimeStyles.None, out var date)
            ? date
            : null;

    private static decimal? NombreStaging(string? valeur) =>
        decimal.TryParse(valeur, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var nombre)
            ? nombre
            : null;

    private static int? PositionStaging(string? valeur) =>
        int.TryParse(valeur, out var position) ? position : null;

    private static void Ajouter(        EdiVoyageStaging? entete, EdiDossierStaging? detail, List<EdiAnomalieReconciliation> anomalies,
        string type, string? reference, string? code, string message)
    {
        entete?.AjouterAnomalie(message);
        detail?.AjouterAnomalie(message);
        anomalies.Add(new EdiAnomalieReconciliation(type, reference ?? string.Empty, code ?? string.Empty, message));
    }

    private static async Task<bool> ReferentielAbsent(
        string? code, Func<string, CancellationToken, Task<Port?>> lookup, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return true;
        }

        return await lookup(code, cancellationToken) is null;
    }
}
