using Logistics.Application.Abstractions;
using Logistics.Domain.Common;
using Logistics.Domain.Entities.Clients;
using Logistics.Domain.Enums;
using Logistics.Domain.Entities.ReferenceData;

namespace Logistics.Application.Features.Clients;

// ---------------------------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------------------------
public sealed record ClientListItem(
    string KeyClient, string RaisonSociale, string? RefClient, string? StatutClient, string? ClasseClient,
    string? VilleClient, string? PaysClient, bool Actif, bool Valide, DateTime? DateCreation);

public sealed record ClientDetail(
    string KeyClient, string SocieteKey, string RaisonSociale, string? RefClient, string? TypeClient,
    string? StatutClient, string? GroupeClient, string? ClasseClient, string? RisqueClient,
    string? ActiviteClient, string? DetailActivite, string? Commercial, string? AgenceCommercial,
    string? AgenceExploitation, string? AgentRecouvrement, string? Adresse1Client, string? Adresse2Client,
    string? CpClient, string? VilleClient, string? PaysClient, string? DepartementClient, string? RegionClient,
    string? LangueClient, string? TelClient, string? FaxClient, string? SiteWebClient, int? AnneeCreation,
    string? ObservationClient, string? RcClient, string? CentreRcClient, string? PatenteClient, string? IfClient,
    string? CnssClient, string? IceClient, string? DeviseFacturation, string? PaiementClient, string? Echeance,
    int? NbEcheance, bool FinMois, decimal? Plafond, string? CompteComptable, string? JournalVente,
    string? JournalDebours, PeriodiciteReleve ReleveFacture, int? NbFactureReleve, bool Ras, bool Actif,
    bool Valide, bool Supprime, string? ServiceAcquis, string? LieuDedouanement, int? NbJrFranchise,
    decimal? ImmobilisationMead, decimal? ImmobilisationPort, bool SuiviAt, DateTime? DateCreation,
    IReadOnlyList<AdresseClientDto> Adresses, IReadOnlyList<BanqueTiersDto> Banques,
    string? TypeAffectCommercial = null, string? KeyChargeClientele = null);

public sealed record AdresseClientDto(string KeyAdresse, string? Adresse1, string? Adresse2, string? Cp,
    string Ville, string? Pays, string? TelAdresse, string? FaxAdresse, string? Contact, string? Type);

public sealed record BanqueTiersDto(string KeyBanque, string? Banque, string? CompteRib, string? CodeBanque,
    string? AdresseBanque, string? Swift, string? IbanCode);

/// <summary>Payload for creating or replacing a client fiche.</summary>
public sealed record SaveClientRequest
{
    public required string RaisonSociale { get; init; }
    public string? RefClient { get; init; }
    public string? TypeClient { get; init; }
    public string? StatutClient { get; init; }
    public string? GroupeClient { get; init; }
    public string? ClasseClient { get; init; }
    public string? RisqueClient { get; init; }
    public string? ActiviteClient { get; init; }
    public string? DetailActivite { get; init; }
    public string? Commercial { get; init; }
    public string? AgenceCommercial { get; init; }
    public string? AgenceExploitation { get; init; }
    public string? TypeAffectCommercial { get; init; }
    public string? KeyChargeClientele { get; init; }
    public string? Adresse1Client { get; init; }
    public string? Adresse2Client { get; init; }
    public string? CpClient { get; init; }
    public string? VilleClient { get; init; }
    public string? PaysClient { get; init; }
    public string? DepartementClient { get; init; }
    public string? RegionClient { get; init; }
    public string? LangueClient { get; init; }
    public string? TelClient { get; init; }
    public string? FaxClient { get; init; }
    public string? SiteWebClient { get; init; }
    public int? AnneeCreation { get; init; }
    public string? ObservationClient { get; init; }
    public string? RcClient { get; init; }
    public string? CentreRcClient { get; init; }
    public string? PatenteClient { get; init; }
    public string? IfClient { get; init; }
    public string? CnssClient { get; init; }
    public string? IceClient { get; init; }
    public string? DeviseFacturation { get; init; }
    public string? PaiementClient { get; init; }
    public string? Echeance { get; init; }
    public int? NbEcheance { get; init; }
    public bool FinMois { get; init; }
    public decimal? Plafond { get; init; }
    public string? CompteComptable { get; init; }
    public string? JournalVente { get; init; }
    public string? JournalDebours { get; init; }
    public PeriodiciteReleve ReleveFacture { get; init; }
    public int? NbFactureReleve { get; init; }
    public bool Ras { get; init; }
    public string? LieuDedouanement { get; init; }
    public int? NbJrFranchise { get; init; }
    public decimal? ImmobilisationMead { get; init; }
    public decimal? ImmobilisationPort { get; init; }
    public bool SuiviAt { get; init; }
}

public sealed record SaveAdresseRequest(
    string? Adresse1, string? Adresse2, string? Cp, string Ville, string? Pays,
    string? TelAdresse, string? FaxAdresse, string? Contact);

public sealed record SaveBanqueRequest(
    string? Banque, string? CompteRib, string? CodeBanque, string? AdresseBanque, string? Swift, string? IbanCode);

public interface IClientService
{
    Task<PagedResult<ClientListItem>> SearchAsync(
        string? search, string? statutClient, string? classeClient, bool? actif, int page, int pageSize,
        CancellationToken cancellationToken = default, bool? valide = null);
    Task<ClientDetail> GetAsync(string keyClient, CancellationToken cancellationToken = default);
    Task<ClientDetail> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default);
    Task<ClientDetail> CreateAsync(SaveClientRequest request, CancellationToken cancellationToken = default);
    Task<ClientDetail> UpdateAsync(string keyClient, SaveClientRequest request, CancellationToken cancellationToken = default);
    Task<ClientDetail> ValidateAsync(string keyClient, CancellationToken cancellationToken = default);
    Task DeleteAsync(string keyClient, CancellationToken cancellationToken = default);
    Task<AdresseClientDto> AddAdresseAsync(string keyClient, SaveAdresseRequest request, CancellationToken cancellationToken = default);
    Task RemoveAdresseAsync(string keyClient, string keyAdresse, CancellationToken cancellationToken = default);
    Task<BanqueTiersDto> AddBanqueAsync(string keyClient, SaveBanqueRequest request, CancellationToken cancellationToken = default);
    Task RemoveBanqueAsync(string keyClient, string keyBanque, CancellationToken cancellationToken = default);
}

/// <summary>
/// Client use cases. The interesting behaviour is all in the delete guard and the uniqueness rules,
/// both of which the legacy WinForms screen implemented inline with <c>MsgBox</c> prompts.
/// </summary>
public sealed class ClientService(
    IClientRepository clients,
    IReferenceDataRepository referenceData,
    IKeyGenerator keys,
    IAuditTrail audit,
    ITenantContext tenant) : IClientService
{
    /// <summary>Legacy trace codes for client events on <c>t_trace_client</c>.</summary>
    private const string TraceAjout = "$ACLT";
    private const string TraceModification = "$MCLT";
    private const string TraceSuppression = "$SCLT";
    private const string TraceValidation = "$VCLT";
    private const string TraceAdresseAjout = "$ADLIV";
    private const string TraceAdresseSuppression = "$SDLIV";
    private const string TraceBanqueAjout = "$ABCLT";
    private const string TraceBanqueSuppression = "$SBCLT";

    /// <summary>
    /// Tables the legacy code refuses to delete a client while they still reference it. Propagated
    /// verbatim so the API returns the same blockers the desktop application did.
    /// </summary>
    private static readonly string[] DeleteBlockers =
    [
        "t_entete_facture_vente", "t_dossiers", "t_entete_reglement", "t_contact_tiers",
        "t_banque_tiers", "t_taux_change_client", "t_exoneration_tva"
    ];

    public async Task<PagedResult<ClientListItem>> SearchAsync(
        string? search, string? statutClient, string? classeClient, bool? actif, int page, int pageSize,
        CancellationToken cancellationToken = default, bool? valide = null)
    {
        GuardPaging(page, pageSize);

        var result = await clients.SearchAsync(search, statutClient, classeClient, actif, page, pageSize, cancellationToken, valide);

        return new PagedResult<ClientListItem>(
            result.Items.Select(c => new ClientListItem(
                c.KeyClient, c.RaisonSociale, c.RefClient, c.StatutClient, c.ClasseClient,
                c.VilleClient, c.PaysClient, c.Actif, c.Valide, c.DateCreation)).ToArray(),
            result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<ClientDetail> GetAsync(string keyClient, CancellationToken cancellationToken = default) =>
        Map(await RequireAsync(keyClient, includeChildren: true, cancellationToken));

    public async Task<ClientDetail> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        cancellationToken.ThrowIfCancellationRequested();
        var matches = await clients.FindByReferenceAsync(reference, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (matches.Count == 0) throw new EntityNotFoundException("ClientReference", reference);
        if (matches.Count != 1)
            throw new BusinessRuleException("client.reference.ambiguous", "Plusieurs clients portent cette référence. Précisez la clé interne du client.");
        // Reuse the normal aggregate read and its access checks; never choose the first ambiguous match.
        return await GetAsync(matches[0].KeyClient, cancellationToken);
    }

    public async Task<ClientDetail> CreateAsync(SaveClientRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureRaisonSocialeAvailableAsync(request.RaisonSociale, null, cancellationToken);

        var key = await keys.NextAsync("client", cancellationToken);
        var client = new Client(key, tenant.SocieteKey, request.RaisonSociale, request.PaysClient);

        client.Update(ToDetails(request));
        await ApplyAccountingAsync(client, request, cancellationToken);

        clients.Add(client);
        await clients.SaveChangesAsync(cancellationToken);

        await audit.TraceAsync<Domain.Entities.Audit.TraceClient>(
            client.KeyClient, TraceAjout, $"Raison sociale = {client.RaisonSociale}", cancellationToken: cancellationToken);

        return Map(client);
    }

    public async Task<ClientDetail> UpdateAsync(string keyClient, SaveClientRequest request, CancellationToken cancellationToken = default)
    {
        var client = await RequireAsync(keyClient, includeChildren: false, cancellationToken);

        await EnsureRaisonSocialeAvailableAsync(request.RaisonSociale, keyClient, cancellationToken);

        var diff = DescribeChanges(client, request);
        client.Update(ToDetails(request));
        await ApplyAccountingAsync(client, request, cancellationToken);

        await clients.SaveChangesAsync(cancellationToken);

        if (diff.Length > 0)
        {
            await audit.TraceAsync<Domain.Entities.Audit.TraceClient>(
                keyClient, TraceModification, diff, cancellationToken: cancellationToken);
        }

        return Map(client);
    }

    public async Task<ClientDetail> ValidateAsync(string keyClient, CancellationToken cancellationToken = default)
    {
        var client = await RequireAsync(keyClient, includeChildren: false, cancellationToken);

        // The legacy rule reads the switch `GenerationCompteComptableClient` from the company
        // parameter store (t_parametre_societe), which is not part of this increment. Until it is
        // migrated we apply the stricter branch: a ledger account is required.
        const bool companyGenerates = false;
        client.Validate(companyGeneratesComptes: companyGenerates, transfertComptabilite: !companyGenerates);

        await clients.SaveChangesAsync(cancellationToken);
        await audit.TraceAsync<Domain.Entities.Audit.TraceClient>(
            keyClient, TraceValidation, "Validation du client", cancellationToken: cancellationToken);

        return Map(client);
    }

    public async Task DeleteAsync(string keyClient, CancellationToken cancellationToken = default)
    {
        var client = await RequireAsync(keyClient, includeChildren: false, cancellationToken);

        var references = await clients.FindReferencesAsync(keyClient, cancellationToken);
        if (references.Count > 0)
        {
            throw new BusinessRuleException(
                "client.delete.referenced",
                $"Suppression impossible : le client est référencé par {string.Join(", ", references)}.");
        }

        client.SoftDelete();
        await clients.SaveChangesAsync(cancellationToken);

        await audit.TraceAsync<Domain.Entities.Audit.TraceClient>(
            keyClient, TraceSuppression, $"Suppression du client {client.RaisonSociale}", cancellationToken: cancellationToken);
    }

    public async Task<AdresseClientDto> AddAdresseAsync(
        string keyClient, SaveAdresseRequest request, CancellationToken cancellationToken = default)
    {
        var client = await RequireAsync(keyClient, true, cancellationToken);
        var key = await keys.NextAsync("COMMERCIAL", cancellationToken);

        var adresse = new AdresseClient(key, client.KeyClient, request.Adresse1, request.Adresse2, request.Ville, request.Pays);
        adresse.Update(request.Adresse1, request.Adresse2, request.Cp, request.Ville, request.Pays,
            request.TelAdresse, request.FaxAdresse, request.Contact);

        clients.Add(adresse);
        await clients.SaveChangesAsync(cancellationToken);

        await audit.TraceAsync<Domain.Entities.Audit.TraceClient>(
            keyClient, TraceAdresseAjout, $"Adresse de livraison = {request.Ville}", cancellationToken: cancellationToken);

        return new AdresseClientDto(adresse.KeyAdresse, adresse.Adresse1, adresse.Adresse2, adresse.Cp,
            adresse.Ville, adresse.Pays, adresse.TelAdresse, adresse.FaxAdresse, adresse.Contact, adresse.Type);
    }

    public async Task RemoveAdresseAsync(string keyClient, string keyAdresse, CancellationToken cancellationToken = default)
    {
        var client = await RequireAsync(keyClient, true, cancellationToken);
        var adresse = client.Adresses.FirstOrDefault(a => a.KeyAdresse == keyAdresse)
            ?? throw new EntityNotFoundException("AdresseClient", keyAdresse);

        // The legacy screen refuses to remove the principal address.
        if (adresse.IsPrincipal)
        {
            throw new BusinessRuleException("adresse.principal", "L'adresse principale ne peut pas être supprimée.");
        }

        clients.Remove(adresse);
        await clients.SaveChangesAsync(cancellationToken);

        await audit.TraceAsync<Domain.Entities.Audit.TraceClient>(
            keyClient, TraceAdresseSuppression, $"Suppression adresse {keyAdresse}", cancellationToken: cancellationToken);
    }

    public async Task<BanqueTiersDto> AddBanqueAsync(
        string keyClient, SaveBanqueRequest request, CancellationToken cancellationToken = default)
    {
        var client = await RequireAsync(keyClient, true, cancellationToken);

        // The RIB must be globally unique (legacy `Existe("compte_rib", …)`).
        if (!string.IsNullOrWhiteSpace(request.CompteRib)
            && await referenceData.AnyAsync<BanqueTiers>(x => x.CompteRib == request.CompteRib, cancellationToken))
        {
            throw new DuplicateEntityException("BanqueTiers", "compte_rib", request.CompteRib);
        }

        var key = await keys.NextAsync("COMMERCIAL", cancellationToken);
        var banque = new BanqueTiers(key, client.KeyClient, TypeTiers.Client, request.Banque, request.CompteRib);
        banque.Update(request.Banque, request.CompteRib, request.CodeBanque, request.AdresseBanque, request.Swift, request.IbanCode);

        clients.Add(banque);
        await clients.SaveChangesAsync(cancellationToken);

        await audit.TraceAsync<Domain.Entities.Audit.TraceClient>(
            keyClient, TraceBanqueAjout, $"Banque = {request.Banque}", cancellationToken: cancellationToken);

        return new BanqueTiersDto(banque.KeyBanque, banque.Banque, banque.CompteRib, banque.CodeBanque,
            banque.AdresseBanque, banque.Swift, banque.IbanCode);
    }

    public async Task RemoveBanqueAsync(string keyClient, string keyBanque, CancellationToken cancellationToken = default)
    {
        var client = await RequireAsync(keyClient, true, cancellationToken);
        var banque = client.Banques.FirstOrDefault(x => x.KeyBanque == keyBanque)
            ?? throw new EntityNotFoundException("BanqueTiers", keyBanque);

        clients.Remove(banque);
        await clients.SaveChangesAsync(cancellationToken);

        await audit.TraceAsync<Domain.Entities.Audit.TraceClient>(
            keyClient, TraceBanqueSuppression, $"Suppression banque {keyBanque}", cancellationToken: cancellationToken);
    }

    private async Task<Client> RequireAsync(string keyClient, bool includeChildren = false, CancellationToken cancellationToken = default) =>
        await clients.GetAsync(keyClient, includeChildren, cancellationToken)
        ?? throw new EntityNotFoundException("Client", keyClient);

    private async Task EnsureRaisonSocialeAvailableAsync(string raisonSociale, string? excludingKey, CancellationToken cancellationToken)
    {
        var existing = await clients.GetByRaisonSocialeAsync(raisonSociale, cancellationToken);
        if (existing is not null && !string.Equals(existing.KeyClient, excludingKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new DuplicateEntityException("Client", "RAISON_SOCIALE", raisonSociale);
        }
    }

    /// <summary>
    /// Applies the ledger-account rules. The legacy code either derives the account from the name's
    /// first letter via <c>t_correspondance_compte</c> or requires the user to type it, and enforces
    /// global uniqueness either way. Derivation is not reproduced yet (the counter table is missing
    /// from the source tree), so the supplied value is validated and stored.
    /// </summary>
    private async Task ApplyAccountingAsync(Client client, SaveClientRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CompteComptable))
        {
            return;
        }

        if (await clients.CompteComptableExistsAsync(request.CompteComptable, client.KeyClient, cancellationToken))
        {
            throw new DuplicateEntityException("Client", "COMPTE_COMPTABLE", request.CompteComptable);
        }

        client.SetCompteComptable(request.CompteComptable);
    }

    /// <summary>
    /// Delegates to <see cref="Paging.Valider"/> so there is one rule, not a copy per service.
    /// </summary>
    /// <remarks>
    /// The duplicated copy of this method was the cause of the bound being applied to only three of ten list
    /// paths; see <see cref="Paging"/> for why the rule lives there.
    /// </remarks>
    private static void GuardPaging(int page, int pageSize) => Paging.Valider(page, pageSize);

    private static ClientDetails ToDetails(SaveClientRequest r) => new()
    {
        RaisonSociale = r.RaisonSociale,
        RefClient = r.RefClient,
        TypeClient = r.TypeClient,
        StatutClient = r.StatutClient,
        GroupeClient = r.GroupeClient,
        ClasseClient = r.ClasseClient,
        RisqueClient = r.RisqueClient,
        ActiviteClient = r.ActiviteClient,
        DetailActivite = r.DetailActivite,
        Commercial = r.Commercial,
        AgenceCommercial = r.AgenceCommercial,
        AgenceExploitation = r.AgenceExploitation,
        TypeAffectCommercial = r.TypeAffectCommercial,
        KeyChargeClientele = r.KeyChargeClientele,
        Adresse1Client = r.Adresse1Client,
        Adresse2Client = r.Adresse2Client,
        CpClient = r.CpClient,
        VilleClient = r.VilleClient,
        PaysClient = r.PaysClient,
        DepartementClient = r.DepartementClient,
        RegionClient = r.RegionClient,
        LangueClient = r.LangueClient,
        TelClient = r.TelClient,
        FaxClient = r.FaxClient,
        SiteWebClient = r.SiteWebClient,
        AnneeCreation = r.AnneeCreation,
        ObservationClient = r.ObservationClient,
        RcClient = r.RcClient,
        CentreRcClient = r.CentreRcClient,
        PatenteClient = r.PatenteClient,
        IfClient = r.IfClient,
        CnssClient = r.CnssClient,
        IceClient = r.IceClient,
        DeviseFacturation = r.DeviseFacturation,
        PaiementClient = r.PaiementClient,
        Echeance = r.Echeance,
        NbEcheance = r.NbEcheance,
        FinMois = r.FinMois,
        Plafond = r.Plafond,
        JournalVente = r.JournalVente,
        JournalDebours = r.JournalDebours,
        ReleveFacture = r.ReleveFacture,
        NbFactureReleve = r.NbFactureReleve,
        Ras = r.Ras,
        LieuDedouanement = r.LieuDedouanement,
        NbJrFranchise = r.NbJrFranchise,
        ImmobilisationMead = r.ImmobilisationMead,
        ImmobilisationPort = r.ImmobilisationPort,
        SuiviAt = r.SuiviAt
    };

    /// <summary>
    /// Builds the "Anc.xxx = …" difference string the legacy journal stores, so the audit trail reads
    /// the same as it always has.
    /// </summary>
    private static string DescribeChanges(Client c, SaveClientRequest r)
    {
        var parts = new List<string>();

        void Compare(string label, string? oldValue, string? newValue)
        {
            var normalisedNew = string.IsNullOrWhiteSpace(newValue) ? null : newValue.Trim();
            if (!string.Equals(oldValue, normalisedNew, StringComparison.Ordinal))
            {
                parts.Add($"Anc.{label}= {oldValue ?? "null"}");
            }
        }

        Compare("Raison sociale", c.RaisonSociale, r.RaisonSociale);
        Compare("Statut", c.StatutClient, r.StatutClient);
        Compare("Classe", c.ClasseClient, r.ClasseClient);
        Compare("Risque", c.RisqueClient, r.RisqueClient);
        Compare("Ville", c.VilleClient, r.VilleClient);
        Compare("Pays", c.PaysClient, r.PaysClient);
        Compare("Devise", c.DeviseFacturation, r.DeviseFacturation);
        Compare("Paiement", c.PaiementClient, r.PaiementClient);
        Compare("Compte", c.CompteComptable, r.CompteComptable);

        return string.Join(", ", parts);
    }

    private static ClientDetail Map(Client c) => new(
        c.KeyClient, c.SocieteKey, c.RaisonSociale, c.RefClient, c.TypeClient, c.StatutClient, c.GroupeClient,
        c.ClasseClient, c.RisqueClient, c.ActiviteClient, c.DetailActivite, c.Commercial, c.AgenceCommercial,
        c.AgenceExploitation, c.AgentRecouvrement, c.Adresse1Client, c.Adresse2Client, c.CpClient, c.VilleClient,
        c.PaysClient, c.DepartementClient, c.RegionClient, c.LangueClient, c.TelClient, c.FaxClient, c.SiteWebClient,
        c.AnneeCreation, c.ObservationClient, c.RcClient, c.CentreRcClient, c.PatenteClient, c.IfClient, c.CnssClient,
        c.IceClient, c.DeviseFacturation, c.PaiementClient, c.Echeance, c.NbEcheance, c.FinMois, c.Plafond,
        c.CompteComptable, c.JournalVente, c.JournalDebours, c.ReleveFacture, c.NbFactureReleve, c.Ras, c.Actif,
        c.Valide, c.Supprime, c.ServiceAcquis, c.LieuDedouanement, c.NbJrFranchise, c.ImmobilisationMead,
        c.ImmobilisationPort, c.SuiviAt, c.DateCreation,
        c.Adresses.Select(a => new AdresseClientDto(a.KeyAdresse, a.Adresse1, a.Adresse2, a.Cp, a.Ville,
            a.Pays, a.TelAdresse, a.FaxAdresse, a.Contact, a.Type)).ToArray(),
        c.Banques.Select(x => new BanqueTiersDto(x.KeyBanque, x.Banque, x.CompteRib, x.CodeBanque,
            x.AdresseBanque, x.Swift, x.IbanCode)).ToArray(),
        c.TypeAffectCommercial, c.KeyChargeClientele);
}
