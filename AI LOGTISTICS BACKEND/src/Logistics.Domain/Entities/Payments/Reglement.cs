using Logistics.Domain.Common;
using Logistics.Domain.Entities.ReferenceData;
using Logistics.Domain.Enums;

namespace Logistics.Domain.Entities.Payments;

/// <summary>
/// Header of every payment/receipt (<c>t_entete_reglement</c>) — client receipts, supplier payments,
/// foreign-currency operations and customer advances all live in this one table, discriminated by
/// <see cref="TypeReglement"/>. The tenant column is <c>soc_reglement</c>.
/// </summary>
/// <remarks>
/// Two legacy quirks are handled explicitly here because they change behaviour:
/// <list type="bullet">
///   <item>the default <see cref="Statut"/> is <em>data-driven</em> (from <c>t_mode_paiement</c> or a
///   company parameter), never hard-coded;</item>
///   <item>bank fees are stored twice — as a snapshot on this header (for foreign payments) and as a
///   real <see cref="FraisBancaire"/> row used by the VAT declaration and the accounting integration.</item>
/// </list>
/// </remarks>
public sealed class EnteteReglement : Entity, ITenantScoped
{
    private readonly List<DetailReglement> _allocations = [];

    private EnteteReglement() { }

    public EnteteReglement(
        string keyReglement,
        string societeKey,
        TypeReglement typeReglement,
        string? keyTiers,
        string devise,
        DateTime? dateOperation)
    {
        Id = Require(keyReglement, nameof(keyReglement));
        SocieteKey = Require(societeKey, nameof(societeKey));
        TypeReglement = typeReglement;
        KeyTiers = Trim(keyTiers);
        Devise = Require(devise, nameof(devise)).ToUpperInvariant();
        DateOperation = dateOperation;
        NatureReglement = NatureReglement.Reglement;
        Statut = StatutReglement.Saisie;
        TauxDevise = 1m;
        Supprime = false;
    }

    public string KeyReglement => Id;
    public string SocieteKey { get; private set; } = string.Empty;
    public string? AgenceReglement { get; private set; }

    /// <summary>Polymorphic counterparty: a client, supplier, agent or personnel key.</summary>
    public string? KeyTiers { get; private set; }
    public TypeReglement TypeReglement { get; private set; }
    public NatureReglement NatureReglement { get; private set; }

    // Payment mode & currency
    public string? ModePaiement { get; private set; }
    /// <summary>Foreign-currency rate mode, e.g. "Taux de change au fixing".</summary>
    public string? ModeChange { get; private set; }
    public string Devise { get; private set; } = string.Empty;
    /// <summary>Rate applied to this payment. Forced to 1 when <see cref="Devise"/> is the company currency.</summary>
    public decimal TauxDevise { get; private set; }

    /// <summary>Amount in <see cref="Devise"/>.</summary>
    public decimal MontantDevise { get; private set; }
    /// <summary>Counter-value in the company currency.</summary>
    public decimal MontantMad { get; private set; }
    public string? MontantLettre { get; private set; }
    /// <summary>Portion of an AVANCE already allocated to invoices.</summary>
    public decimal MontantImputeAvance { get; private set; }

    // Dates
    public DateTime? DateSaisie { get; private set; }
    public DateTime? DateOperation { get; private set; }
    /// <summary>Drives ageing when <c>mode_paiement = 'TRAITE'</c>.</summary>
    public DateTime? DateEcheance { get; private set; }
    public DateTime? DateSignature { get; private set; }
    public DateTime? DateRemiseBanque { get; private set; }
    public DateTime? DateValidationBanque { get; private set; }
    /// <summary>Date of the "opération diverse" entry raised on a conversion difference.</summary>
    public DateTime? DateOd { get; private set; }

    public string? ReferencePaiement { get; private set; }
    public string? RefCompta { get; private set; }
    /// <summary>Denormalised invoice list. Recomputed from the allocations, never hand-maintained.</summary>
    public string? Facture { get; private set; }
    public string? Dossier { get; private set; }

    // Counterparty / bank details captured on the document
    public string? NomTier { get; private set; }
    public string? Tireur { get; private set; }
    public string? VilleTireur { get; private set; }
    public string? BanqueTireur { get; private set; }
    public string? BanqueTier { get; private set; }
    public string? CompteBanqueTier { get; private set; }
    public string? AdresseBanqueTier { get; private set; }
    public string? CodeBanqueTier { get; private set; }
    public string? SwiftCodeTier { get; private set; }
    public string? IbanCode { get; private set; }
    public string? AdresseTiers { get; private set; }
    public string? AgenceBanqueSoc { get; private set; }
    public string? CompteBancaireSoc { get; private set; }
    public string? KeyTresorie { get; private set; }

    // Status
    public StatutReglement Statut { get; private set; }
    public string? MotifImpaye { get; private set; }
    public string? CommentaireImpaye { get; private set; }
    public string? Observation { get; private set; }
    public string? AgRecouvrementSys { get; private set; }
    public string? AgRecouvrementSaisie { get; private set; }

    // Bank fees snapshot (kept in sync with the FraisBancaires collection)
    /// <summary>Typo is in the legacy schema: <c>comission_banque</c> with one 'm'.</summary>
    public decimal? ComissionBanque { get; private set; }
    public decimal? FraisBancaire { get; private set; }
    public decimal? Tva { get; private set; }

    // Conversion differences
    public decimal? EcartReglement { get; private set; }
    public decimal? EcartFactVente { get; private set; }
    public decimal? EcartFactAchat { get; private set; }
    public decimal? EcartConversion { get; private set; }

    // Settled-invoice totals, per side and per currency
    public decimal? MtFactCltMad { get; private set; }
    public decimal? MtFactCltBanque { get; private set; }
    public decimal? MtFactFrsMad { get; private set; }
    public decimal? MtFactFrsBanque { get; private set; }

    // Lettering / accounting
    public bool ValidationLettrage { get; private set; }
    public TypeLettrage? TypeLettrage { get; private set; }
    public string? CodeLettrage { get; private set; }
    public StatutDeclarationTva? StatutDeclarationTva { get; private set; }
    public string? MoisDeclaration { get; private set; }
    public string? KeyRemiseSys { get; private set; }
    public string? KeyDemandeReg { get; private set; }
    public bool Attachement { get; private set; }
    public bool Integre { get; private set; }
    public bool IntegreImp { get; private set; }
    public bool Supprime { get; private set; }

    public IReadOnlyCollection<DetailReglement> Allocations => _allocations.AsReadOnly();

    /// <summary>Financial direction as the <c>v_reglement</c> view computes it.</summary>
    public SensFinancier SensFinancier =>
        MontantDevise < 0 ? SensFinancier.Decaissement : SensFinancier.Encaissement;

    /// <summary>An AVANCE that is fully lettered becomes a REGLEMENT.</summary>
    public bool IsLettrableAvance =>
        NatureReglement == NatureReglement.Avance
        && !Supprime
        && !ValidationLettrage
        && MontantDevise - MontantImputeAvance > 0;

    /// <summary>Remaining unallocated advance amount.</summary>
    public decimal ResteAvance => MontantDevise - MontantImputeAvance;

    /// <summary>
    /// Reclassifies the payment as an unallocated customer advance. An advance only becomes a
    /// REGLEMENT again once its lettrage is validated.
    /// </summary>
    public void MarkAsAvance()
    {
        NatureReglement = NatureReglement.Avance;
        ValidationLettrage = false;
        MontantImputeAvance = 0m;
    }

    /// <summary>
    /// Assigns the default status. Reproduces the legacy precedence: the company parameter wins for
    /// foreign wire transfers at the fixing rate, otherwise the payment mode decides.
    /// </summary>
    public void ApplyDefaultStatut(string? statutFromParameter, ModePaiement? modePaiement)
    {
        var isForeignFixingWire = string.Equals(ModePaiement, "VIREMENT", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ModePaiement, "VIREMENT EN LIGNE", StringComparison.OrdinalIgnoreCase);

        if (isForeignFixingWire && string.Equals(ModeChange, "Taux de change au fixing", StringComparison.OrdinalIgnoreCase))
        {
            Statut = ParseStatut(statutFromParameter) ?? StatutReglement.Saisie;
            return;
        }

        Statut = ParseStatut(modePaiement?.DefaultStatutFor(TypeReglement)) ?? StatutReglement.Saisie;
    }

    /// <summary>
    /// Records the monetary payload and normalises the exchange rate. The rate is forced to 1 when
    /// paying in the company's own currency — the legacy code does the same before computing MAD.
    /// </summary>
    public void SetMontants(decimal montantDevise, decimal tauxDevise, string deviseSociete, int decimalesSociete)
    {
        var rate = string.Equals(Devise, deviseSociete, StringComparison.OrdinalIgnoreCase) ? 1m : tauxDevise;

        if (rate <= 0)
        {
            throw new BusinessRuleException("reglement.taux", "Le taux de change doit être strictement positif.");
        }

        TauxDevise = rate;
        MontantDevise = montantDevise;
        MontantMad = Math.Round(montantDevise * rate, decimalesSociete, MidpointRounding.AwayFromZero);
    }

    public void SetModePaiement(string? modePaiement)
    {
        ModePaiement = Trim(modePaiement);

        // ESPECE / VIREMENT are considered banked immediately: the legacy code sets all three dates
        // to the operation date.
        if (ModePaiement is "ESPECE" or "VIREMENT" && DateOperation.HasValue)
        {
            DateRemiseBanque = DateOperation;
            DateValidationBanque = DateOperation;
        }
    }

    public void SetModeChange(string? modeChange) => ModeChange = Trim(modeChange);

    public void SetAgence(string? agenceReglement) => AgenceReglement = Trim(agenceReglement);
    public void SetReferencePaiement(string? reference) => ReferencePaiement = Trim(reference);
    public void SetTresorie(string? keyTresorie) => KeyTresorie = Trim(keyTresorie);
    public void SetObservation(string? observation) => Observation = Trim(observation);
    public void SetMontantLettre(string? montantLettre) => MontantLettre = Trim(montantLettre);
    public void SetDossier(string? dossier) => Dossier = Trim(dossier);

    public void SetDates(
        DateTime? dateSaisie,
        DateTime? dateOperation,
        DateTime? dateEcheance,
        DateTime? dateSignature = null)
    {
        DateSaisie = dateSaisie;
        DateOperation = dateOperation;
        DateEcheance = dateEcheance;
        DateSignature = dateSignature;
    }

    /// <summary>Captures the drawer / counterparty bank details for the printed document.</summary>
    public void SetCoordonneesBancaires(
        string? nomTier,
        string? tireur,
        string? villeTireur,
        string? banqueTireur,
        string? banqueTier,
        string? compteBanqueTier,
        string? adresseBanqueTier,
        string? codeBanqueTier,
        string? swiftCodeTier,
        string? ibanCode,
        string? adresseTiers,
        string? agenceBanqueSoc,
        string? compteBancaireSoc)
    {
        NomTier = Trim(nomTier);
        Tireur = Trim(tireur);
        VilleTireur = Trim(villeTireur);
        BanqueTireur = Trim(banqueTireur);
        BanqueTier = Trim(banqueTier);
        CompteBanqueTier = Trim(compteBanqueTier);
        AdresseBanqueTier = Trim(adresseBanqueTier);
        CodeBanqueTier = Trim(codeBanqueTier);
        SwiftCodeTier = Trim(swiftCodeTier);
        IbanCode = Trim(ibanCode);
        AdresseTiers = Trim(adresseTiers);
        AgenceBanqueSoc = Trim(agenceBanqueSoc);
        CompteBancaireSoc = Trim(compteBancaireSoc);
    }

    /// <summary>
    /// Allocates an amount to an invoice (<c>t_detail_reglement</c>). Amounts are always stored
    /// positive; direction is carried by <paramref name="sens"/>.
    /// </summary>
    /// <param name="decimales">Decimal places of the allocation currency.</param>
    public DetailReglement Allocate(
        string keyFacture,
        decimal montant,
        SensReglement sens,
        decimal rasIs = 0m,
        decimal rasTva = 0m)
    {
        if (Supprime)
        {
            throw new BusinessRuleException("reglement.deleted", "Un règlement supprimé ne peut plus être lettré.");
        }

        if (string.IsNullOrWhiteSpace(keyFacture))
        {
            throw new ArgumentException("Invoice key is required.", nameof(keyFacture));
        }

        if (montant <= 0)
        {
            throw new BusinessRuleException("reglement.allocation.positive", "Le montant affecté doit être positif.");
        }

        var existing = _allocations.FirstOrDefault(a => a.KeyFacture == keyFacture);
        if (existing is not null)
        {
            RefreshFactureList();
            return existing;
        }

        var allocation = new DetailReglement(Id, keyFacture, montant, Devise, sens, rasIs, rasTva);
        _allocations.Add(allocation);

        if (NatureReglement == NatureReglement.Avance)
        {
            MontantImputeAvance += montant;
        }

        AccumulateSettledTotals(montant, sens);
        RefreshFactureList();
        return allocation;
    }

    /// <summary>Removes an allocation (de-lettrage on the legacy screens).</summary>
    public void Deallocate(string keyFacture)
    {
        var allocation = _allocations.FirstOrDefault(a => a.KeyFacture == keyFacture);
        if (allocation is null)
        {
            return;
        }

        _allocations.Remove(allocation);

        if (NatureReglement == NatureReglement.Avance)
        {
            MontantImputeAvance -= allocation.Montant;
        }

        AccumulateSettledTotals(-allocation.Montant, allocation.Sens);
        RefreshFactureList();
    }

    /// <summary>
    /// Marks the advance as fully lettered: sets <c>validation_lettrage = 1</c> and flips the nature
    /// to REGLEMENT, exactly as the legacy validation screen does.
    /// </summary>
    public void ValidateLettrageAvance()
    {
        if (NatureReglement != NatureReglement.Avance)
        {
            throw new BusinessRuleException("reglement.lettrage.not_avance", "Seule une avance peut être lettrée.");
        }

        ValidationLettrage = true;
        NatureReglement = NatureReglement.Reglement;
    }

    /// <summary>
    /// Computes the conversion differences between the invoice-currency and bank-currency totals.
    /// Positive values are a loss on the bank side (the legacy sign convention).
    /// </summary>
    public void ComputeEcarts()
    {
        var ecartVente = (MtFactCltBanque ?? 0m) - (MtFactCltMad ?? 0m);
        var ecartAchat = (MtFactFrsBanque ?? 0m) - (MtFactFrsMad ?? 0m);

        EcartFactVente = Math.Round(ecartVente, 2, MidpointRounding.AwayFromZero);
        EcartFactAchat = Math.Round(ecartAchat, 2, MidpointRounding.AwayFromZero);
        EcartConversion = Math.Round(ecartVente + ecartAchat, 2, MidpointRounding.AwayFromZero);
        EcartReglement = EcartConversion;
    }

    /// <summary>
    /// Decides whether the conversion difference needs a manual "opération diverse" entry.
    /// Reproduces the legacy thresholds: below the automatic limit it is auto-posted, between the two
    /// limits the user must confirm, above the manual limit it is refused.
    /// </summary>
    public EcartDecision ClassifyEcart(decimal valeurOdAutomatique, decimal valeurOdManuel)
    {
        var ecart = Math.Abs(EcartReglement ?? 0m);

        if (ecart <= valeurOdAutomatique)
        {
            return EcartDecision.Automatique;
        }

        return ecart <= valeurOdManuel ? EcartDecision.ManuelRequis : EcartDecision.Refuse;
    }

    /// <summary>Records the bank-fee snapshot for a foreign payment.</summary>
    public void SetFraisBancaires(decimal? fraisBancaire, decimal? tva, decimal? comissionBanque)
    {
        FraisBancaire = fraisBancaire;
        Tva = tva;
        ComissionBanque = comissionBanque;
    }

    public void SetTresorerieRemise(string? keyTresorie, DateTime? dateRemiseBanque, string? keyRemiseSys, string? refCompta)
    {
        KeyTresorie = Trim(keyTresorie);
        DateRemiseBanque = dateRemiseBanque;
        KeyRemiseSys = Trim(keyRemiseSys);
        RefCompta = Trim(refCompta);
        Statut = StatutReglement.RemisEnBanque;
    }

    /// <summary>Reverses a bank remittance: back to "Portefeuille" with the remittance fields cleared.</summary>
    public void ClearRemise()
    {
        KeyTresorie = null;
        DateRemiseBanque = null;
        KeyRemiseSys = null;
        RefCompta = null;
        Statut = StatutReglement.Portefeuille;
    }

    /// <summary>Records an unpaid item (impayé) and its reason.</summary>
    public void MarkImpaye(string? motifImpaye, string? commentaireImpaye)
    {
        Statut = StatutReglement.Impaye;
        MotifImpaye = Trim(motifImpaye);
        CommentaireImpaye = Trim(commentaireImpaye);
    }

    /// <summary>Marks the payment as settled and stamps the settlement date.</summary>
    public void MarkRegle(DateTime dateDernierReglement)
    {
        Statut = StatutReglement.Regle;
        DateValidationBanque ??= dateDernierReglement;
    }

    public void SetStatutDeclarationTva(StatutDeclarationTva? statut, string? moisDeclaration)
    {
        StatutDeclarationTva = statut;
        MoisDeclaration = Trim(moisDeclaration);
    }

    public void SetLettrage(TypeLettrage? typeLettrage, string? codeLettrage)
    {
        TypeLettrage = typeLettrage;
        CodeLettrage = Trim(codeLettrage);
    }

    public void MarkIntegrated() => Integre = true;
    public void MarkIntegratedImpaye() => IntegreImp = true;
    public void SetAttachement(bool attachement) => Attachement = attachement;
    public void LinkDemande(string? keyDemandeReg) => KeyDemandeReg = Trim(keyDemandeReg);

    /// <summary>Soft delete. The legacy menu also offers a hard delete; that is a repository concern.</summary>
    public void SoftDelete() => Supprime = true;

    /// <summary>
    /// Recomputes <see cref="Facture"/>. In the legacy database this is the work of the scalar
    /// function <c>dbo.Recup_Fact_Reg(key_reglement)</c>; here it is derived from the allocations so
    /// the value can never drift from the truth.
    /// </summary>
    private void RefreshFactureList()
    {
        Facture = _allocations.Count == 0
            ? null
            : string.Join(',', _allocations.Select(a => a.KeyFacture));
    }

    private void AccumulateSettledTotals(decimal montant, SensReglement sens)
    {
        if (sens == SensReglement.Vente)
        {
            MtFactCltMad = (MtFactCltMad ?? 0m) + montant;
            MtFactCltBanque = (MtFactCltBanque ?? 0m) + montant;
        }
        else
        {
            MtFactFrsMad = (MtFactFrsMad ?? 0m) + montant;
            MtFactFrsBanque = (MtFactFrsBanque ?? 0m) + montant;
        }
    }

    private static StatutReglement? ParseStatut(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalised = value.Trim().Replace(" ", string.Empty).Replace("é", "e").Replace("É", "E");

        return Enum.TryParse<StatutReglement>(normalised, ignoreCase: true, out var parsed) ? parsed : null;
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Outcome of classifying a conversion difference against the company thresholds.</summary>
public enum EcartDecision
{
    /// <summary>Below the automatic limit — post the counter-entry without asking.</summary>
    Automatique,
    /// <summary>Between the two limits — the user must confirm the counter-entry.</summary>
    ManuelRequis,
    /// <summary>Above the manual limit — refuse and require investigation.</summary>
    Refuse
}

/// <summary>
/// Payment-to-invoice allocation (<c>t_detail_reglement</c>). This <em>is</em> the allocation table —
/// there is no <c>t_affectation_reglement</c> or <c>t_reglement_facture</c>.
/// </summary>
/// <remarks>
/// The allocated <see cref="Montant"/> is stored positive for both directions; the sign is carried by
/// <see cref="Sens"/>. <see cref="KeyFacture"/> is polymorphic: a sales invoice, a purchase invoice,
/// or an expense-claim sheet.
/// </remarks>
public sealed class DetailReglement : Entity
{
    private DetailReglement() { }

    /// <summary>
    /// Creates an allocation. Public so callers (and tests) can construct one directly; in normal use
    /// it is produced by <see cref="EnteteReglement.Allocate"/>.
    /// </summary>
    public DetailReglement(
        string keyReglement,
        string keyFacture,
        decimal montant,
        string devise,
        SensReglement sens,
        decimal rasIs,
        decimal rasTva)
    {
        Id = $"{keyReglement}:{keyFacture}";
        KeyReglement = keyReglement;
        KeyFacture = keyFacture;
        Montant = montant;
        Devise = devise;
        Sens = sens;
        RasIs = rasIs;
        RasTva = rasTva;
    }

    public string KeyReglement { get; private set; } = string.Empty;
    public string KeyFacture { get; private set; } = string.Empty;
    /// <summary>Allocated amount, always positive.</summary>
    public decimal Montant { get; private set; }
    public string Devise { get; private set; } = string.Empty;
    public SensReglement Sens { get; private set; }
    /// <summary>Prorated withholding tax (IS) share.</summary>
    public decimal RasIs { get; private set; }
    /// <summary>Prorated withholding VAT share.</summary>
    public decimal RasTva { get; private set; }

    /// <summary>
    /// Prorates a global withholding amount over this allocation, reproducing the legacy
    /// <c>Round((rasGlobal * mtRegle) / netAPayerGlobal, 2, MidpointRounding.AwayFromZero)</c>.
    /// </summary>
    public void ProrateRas(decimal rasGlobal, decimal netAPayerGlobal)
    {
        if (netAPayerGlobal == 0)
        {
            RasIs = 0;
            RasTva = 0;
            return;
        }

        var ratio = Montant / netAPayerGlobal;
        RasIs = Math.Round(rasGlobal * ratio, 2, MidpointRounding.AwayFromZero);
        RasTva = Math.Round(RasTva * ratio, 2, MidpointRounding.AwayFromZero);
    }
}

/// <summary>
/// Bank fee (<c>T_FRAIS_BANCAIRES</c> — plural). Created by hand, or generated automatically from a
/// foreign-currency règlement and kept in sync with it.
/// </summary>
public sealed class FraisBancaire : Entity, ITenantScoped
{
    private FraisBancaire() { }

    public FraisBancaire(
        string keyFraisBancaire,
        string societeKey,
        string? natureFrais,
        string devise,
        decimal mtHtDevise,
        decimal mtTvaDevise,
        decimal tauxChange = 1m)
    {
        Id = Require(keyFraisBancaire, nameof(keyFraisBancaire));
        SocieteKey = Require(societeKey, nameof(societeKey));
        NatureFrais = Trim(natureFrais);
        Devise = Require(devise, nameof(devise)).ToUpperInvariant();
        MtHtDevise = mtHtDevise;
        MtTvaDevise = mtTvaDevise;
        TauxChange = tauxChange;
        Supprime = false;
    }

    /// <summary>Nature used for the rows auto-generated from a foreign payment.</summary>
    public const string NatureFraisBancaires = "FRAIS BANCAIRES";

    public string KeyFraisBancaire => Id;
    public string SocieteKey { get; private set; } = string.Empty;
    public string? KeyTresorie { get; private set; }
    public DateTime? DateFraisBancaire { get; private set; }
    public string? NatureFrais { get; private set; }
    public decimal MtHtDevise { get; private set; }
    public decimal MtTvaDevise { get; private set; }
    public string Devise { get; private set; } = string.Empty;
    public decimal TauxChange { get; private set; }
    /// <summary>Owning payment, when the fee was generated from one.</summary>
    public string? KeyReglement { get; private set; }
    public StatutDeclarationTva? StatutDeclaration { get; private set; }
    public string? MoisDeclaration { get; private set; }
    public bool Supprime { get; private set; }
    public bool Integre { get; private set; }

    /// <summary>Amount converted to the company currency, as the accounting integration computes it.</summary>
    public decimal MontantMad => string.Equals(Devise, "MAD", StringComparison.OrdinalIgnoreCase)
        ? MtHtDevise
        : Math.Round(MtHtDevise * (TauxChange == 0 ? 1m : TauxChange), 2, MidpointRounding.AwayFromZero);

    /// <summary>Rows with a zero net amount are skipped by the accounting integration.</summary>
    public bool IsAccountable => MtHtDevise != 0m;

    public void Update(decimal mtHtDevise, decimal mtTvaDevise, string? keyTresorie, DateTime? dateFraisBancaire)
    {
        MtHtDevise = mtHtDevise;
        MtTvaDevise = mtTvaDevise;
        KeyTresorie = Trim(keyTresorie);
        DateFraisBancaire = dateFraisBancaire;
    }

    public void LinkReglement(string? keyReglement) => KeyReglement = Trim(keyReglement);

    public void SetStatutDeclaration(StatutDeclarationTva? statut, string? moisDeclaration)
    {
        StatutDeclaration = statut;
        MoisDeclaration = Trim(moisDeclaration);
    }

    public void MarkIntegrated() => Integre = true;
    public void SoftDelete() => Supprime = true;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Bank remittance (<c>t_entete_remise</c>) grouping several règlements for deposit at the bank.
/// </summary>
public sealed class EnteteRemise : Entity, ITenantScoped
{
    private EnteteRemise() { }

    public EnteteRemise(string keyRemise, string societeKey, string? refRemise, string? typeRemise, string? keyTresorie, DateTime? dateRemise)
    {
        Id = Require(keyRemise, nameof(keyRemise));
        SocieteKey = Require(societeKey, nameof(societeKey));
        RefRemise = Trim(refRemise);
        TypeRemise = Trim(typeRemise);
        KeyTresorie = Trim(keyTresorie);
        DateRemise = dateRemise;
    }

    public string KeyRemise => Id;
    public string SocieteKey { get; private set; } = string.Empty;
    public string? RefRemise { get; private set; }
    public string? TypeRemise { get; private set; }
    public string? KeyTresorie { get; private set; }
    public DateTime? DateRemise { get; private set; }

    public void Update(string? refRemise, string? typeRemise, string? keyTresorie, DateTime? dateRemise)
    {
        RefRemise = Trim(refRemise);
        TypeRemise = Trim(typeRemise);
        KeyTresorie = Trim(keyTresorie);
        DateRemise = dateRemise;
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Company exchange rate for a given currency and date (<c>t_taux_change</c>).
/// </summary>
/// <remarks>
/// Rate resolution precedence in the legacy invoicing module is: a client-specific rate
/// (<c>t_taux_change_client</c>) valid for the invoice date, then a voyage rate
/// (<c>t_taux_change_voyage</c>), then this daily rate.
/// </remarks>
public sealed class TauxChange : Entity, ITenantScoped
{
    private TauxChange() { }

    public TauxChange(string societeKey, string devise, DateTime dateTaux, decimal tauxVente, decimal tauxAchat, decimal tauxChange)
    {
        Id = $"{societeKey}:{devise}:{dateTaux:yyyyMMdd}";
        SocieteKey = Require(societeKey, nameof(societeKey));
        Devise = Require(devise, nameof(devise)).ToUpperInvariant();
        DateTaux = dateTaux.Date;
        TauxVente = tauxVente;
        TauxAchat = tauxAchat;
        TauxChangeRate = tauxChange;
    }

    public string Devise { get; private set; } = string.Empty;
    public string SocieteKey { get; private set; } = string.Empty;
    public DateTime DateTaux { get; private set; }
    public decimal TauxVente { get; private set; }
    public decimal TauxAchat { get; private set; }
    /// <summary>Generic rate column, distinct from the buy/sell pair.</summary>
    public decimal TauxChangeRate { get; private set; }

    public void Update(decimal tauxVente, decimal tauxAchat, decimal tauxChange)
    {
        TauxVente = tauxVente;
        TauxAchat = tauxAchat;
        TauxChangeRate = tauxChange;
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();
}
