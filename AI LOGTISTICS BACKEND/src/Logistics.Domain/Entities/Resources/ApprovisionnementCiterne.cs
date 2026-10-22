using Logistics.Domain.Common;
using Logistics.Domain.Entities.ReferenceData;

namespace Logistics.Domain.Entities.Resources;

/// <summary>
/// A fuel-tank replenishment header (<c>t_approvisionnement_citerne</c>) — one delivery note.
/// </summary>
/// <remarks>
/// <para>
/// This is the document behind a tank's PMP change. Until it existed, <see cref="Citerne.Receptionner"/>
/// could move the weighted average with nothing recorded to explain why.
/// </para>
/// <para>
/// <b>The cost model is unusual and deliberate.</b> There is no unit price and no per-line amount anywhere in
/// the schema: the only cost column is the header's <c>montant_approvisionnement</c> (the BL's total), and
/// each line's cost is <b>allocated pro rata by quantity</b>. The per-line amount is therefore derived, never
/// stored — which is why <see cref="RepartirMontant"/> exists rather than a line <c>Montant</c> property.
/// </para>
/// <para>
/// <b>There is no delete or cancel path, in this API or in the legacy application.</b> The <c>supprimer</c>
/// column exists and is filtered on, but no code anywhere sets it, and no compensating statement exists that
/// would restore the previous stock and PMP. Reversal is genuinely impossible from this schema: the tank's
/// prior quantity and PMP are not retained, and the detail rows carry no snapshot. Rather than invent a
/// delete that would leave the tank's stock and PMP wrong, this entity exposes none — see
/// <see cref="EstAnnulable"/>.
/// </para>
/// </remarks>
public sealed class ApprovisionnementCiterne : Entity, ITenantScoped
{
    private readonly List<DetailApprovisionnementCiterne> _lignes = [];

    private ApprovisionnementCiterne() { }

    public ApprovisionnementCiterne(
        string keyApprovisionnement,
        string societeKey,
        string keyFournisseur,
        DateTime dateApprovisionnement,
        string? numBl,
        decimal montantApprovisionnement)
    {
        Id = string.IsNullOrWhiteSpace(keyApprovisionnement)
            ? throw new ArgumentException("Value is required.", nameof(keyApprovisionnement))
            : keyApprovisionnement.Trim();

        SocieteKey = string.IsNullOrWhiteSpace(societeKey)
            ? throw new ArgumentException("Value is required.", nameof(societeKey))
            : societeKey.Trim();

        KeyFournisseur = string.IsNullOrWhiteSpace(keyFournisseur)
            ? throw new ArgumentException("Value is required.", nameof(keyFournisseur))
            : keyFournisseur.Trim();

        DateApprovisionnement = dateApprovisionnement;
        NumBl = string.IsNullOrWhiteSpace(numBl) ? null : numBl.Trim();
        MontantApprovisionnement = montantApprovisionnement;
    }

    public string SocieteKey { get; private set; } = string.Empty;

    /// <summary>Supplier the fuel was bought from. Legacy: <c>key_fournisseur</c>, forced non-empty.</summary>
    public string KeyFournisseur { get; private set; } = string.Empty;

    public DateTime DateApprovisionnement { get; private set; }

    /// <summary>Delivery-note number. The legacy column is <c>num_bl</c>; there is no <c>num_bon</c> here.</summary>
    public string? NumBl { get; private set; }

    /// <summary>
    /// Total quantity delivered. The legacy form requires this to equal the sum of the lines, which
    /// <see cref="Valider"/> reproduces.
    /// </summary>
    public decimal QteTotale { get; private set; }

    /// <summary>The BL's total cost, and the <b>only</b> cost figure in the module. Lines are allocated from it.</summary>
    public decimal MontantApprovisionnement { get; private set; }

    /// <summary>Set when a scanned document is attached.</summary>
    public bool Attachement { get; private set; }

    /// <summary>
    /// Legacy <c>supprimer</c>. Read by the legacy list query, written by nothing — see the type remarks.
    /// </summary>
    public bool Supprimer { get; private set; }

    public IReadOnlyCollection<DetailApprovisionnementCiterne> Lignes => _lignes.AsReadOnly();

    /// <summary>
    /// Whether this delivery can be reversed.
    /// </summary>
    /// <remarks>
    /// Always <c>false</c>, and exposed rather than simply omitted so that a caller reading the API surface
    /// finds the answer rather than concluding it was forgotten. See the type remarks for why reversal is
    /// impossible rather than merely unimplemented.
    /// </remarks>
    public static bool EstAnnulable => false;

    /// <summary>Adds a tank line. The legacy allows one line per tank per delivery.</summary>
    public DetailApprovisionnementCiterne AjouterLigne(
        string keyCiterne, int? indexCompteur, decimal qteCiterne)
    {
        if (qteCiterne <= 0)
        {
            throw new BusinessRuleException(
                "approvisionnement.quantite", "La quantité livrée doit être strictement positive.");
        }

        if (_lignes.Any(l => string.Equals(l.KeyCiterne, keyCiterne, StringComparison.OrdinalIgnoreCase)))
        {
            // The detail's logical key is (key_approvisionnement, key_citerne); a second line for the same
            // tank would break it, and the legacy grid is one row per tank.
            throw new BusinessRuleException(
                "approvisionnement.citerne.doublon", $"La citerne « {keyCiterne} » figure déjà sur ce bon.");
        }

        var ligne = new DetailApprovisionnementCiterne(Id, keyCiterne, indexCompteur, qteCiterne);
        _lignes.Add(ligne);
        QteTotale = _lignes.Sum(l => l.QteCiterne);

        return ligne;
    }

    /// <summary>
    /// Recomputes the total from the lines.
    /// </summary>
    /// <remarks>
    /// The legacy form enforces <c>qte_totale = SUM(detail.qte_citerne)</c> before saving. Deriving it rather
    /// than accepting it means an inconsistent document cannot be constructed at all, which is stronger than
    /// validating one afterwards.
    /// </remarks>
    public void RecalculerTotal() => QteTotale = _lignes.Sum(l => l.QteCiterne);

    /// <summary>
    /// Allocates the header amount across the lines, pro rata to quantity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The legacy line is <c>nouv_mt = qte_citerne × montant_approvisionnement / qte_totale</c>
    /// (<c>frm_Saisie_Approvisionnement_Citerne.vb:189</c>). The result is then fed to the tank's PMP formula,
    /// which is what makes the header total the sole source of cost.
    /// </para>
    /// <para>
    /// <b>The residual is given to the last line</b>, which is an addition rather than a transcription. The
    /// legacy never rounds the allocated amount — it stays a <c>Double</c> straight into the PMP arithmetic —
    /// whereas money here is decimal to 2 places, so rounding each line independently could leave the
    /// allocations summing to a cent less or more than the BL. Assigning the remainder keeps the parts equal
    /// to the whole, which is what an accountant reconciling the document will check.
    /// </para>
    /// </remarks>
    public IReadOnlyList<(DetailApprovisionnementCiterne Ligne, decimal MontantAlloue)> RepartirMontant()
    {
        if (_lignes.Count == 0 || QteTotale <= 0)
        {
            throw new BusinessRuleException(
                "approvisionnement.repartition",
                "Impossible de répartir le montant : le bon ne comporte aucune quantité.");
        }

        var allocations = new List<(DetailApprovisionnementCiterne, decimal)>(_lignes.Count);
        var cumul = 0m;

        for (var i = 0; i < _lignes.Count; i++)
        {
            var ligne = _lignes[i];

            // The last line absorbs the rounding residual so the parts sum to the whole exactly.
            var montant = i == _lignes.Count - 1
                ? MontantApprovisionnement - cumul
                : Math.Round(
                    ligne.QteCiterne * MontantApprovisionnement / QteTotale, 2, MidpointRounding.AwayFromZero);

            cumul += montant;
            allocations.Add((ligne, montant));
        }

        return allocations;
    }

    /// <summary>Marks a scanned document as attached, or removes the last one.</summary>
    public void SetAttachement(bool attache) => Attachement = attache;

    /// <summary>
    /// Applies the only edit the legacy permits: the delivery note and the supplier.
    /// </summary>
    /// <remarks>
    /// Quantity and amount are deliberately not editable. The legacy update is
    /// <c>set num_bl=…, key_fournisseur=…</c> and nothing else, because changing the amount would silently
    /// invalidate a PMP that has already been applied to the tank and cannot be recomputed.
    /// </remarks>
    public void CorrigerEntete(string? numBl, string keyFournisseur)
    {
        NumBl = string.IsNullOrWhiteSpace(numBl) ? null : numBl.Trim();
        KeyFournisseur = string.IsNullOrWhiteSpace(keyFournisseur)
            ? throw new ArgumentException("Value is required.", nameof(keyFournisseur))
            : keyFournisseur.Trim();
    }
}

/// <summary>
/// One tank line of a replenishment (<c>t_detail_approvisionnement_citerne</c>).
/// </summary>
/// <remarks>
/// <b>No primary key and no tenant column were observed.</b> No surrogate key is ever inserted or selected —
/// the insert column list is exactly <c>(key_approvisionnement, key_citerne, index_compteur, qte_citerne)</c>
/// — so the logical key is the pair, and tenant scope is reached through the header. A database-side identity
/// could exist invisibly; the mapping therefore declares the pair as the key, matching what the code proves.
/// </remarks>
public sealed class DetailApprovisionnementCiterne : Entity
{
    private DetailApprovisionnementCiterne() { }

    internal DetailApprovisionnementCiterne(
        string keyApprovisionnement, string keyCiterne, int? indexCompteur, decimal qteCiterne)
    {
        Id = $"{keyApprovisionnement}:{keyCiterne}";
        KeyApprovisionnement = keyApprovisionnement;
        KeyCiterne = keyCiterne;
        IndexCompteur = indexCompteur;
        QteCiterne = qteCiterne;
    }

    public string KeyApprovisionnement { get; private set; } = string.Empty;

    /// <summary>The tank. Legacy column is <c>key_citerne</c> — not <c>num_citerne</c>.</summary>
    public string KeyCiterne { get; private set; } = string.Empty;

    /// <summary>Physical pump-counter reading, mirrored onto the tank in the same save.</summary>
    public int? IndexCompteur { get; private set; }

    /// <summary>
    /// Quantity delivered into this tank. The legacy grid labels it <c>qte_appro</c>; the column is
    /// <c>qte_citerne</c>.
    /// </summary>
    public decimal QteCiterne { get; private set; }
}
