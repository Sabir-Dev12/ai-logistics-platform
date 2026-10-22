using Logistics.Domain.Common;

namespace Logistics.Domain.Entities.Resources;

/// <summary>
/// Consignment fuel balance with a fellow company (<c>T_STOCK_GASOIL_CONFRERE</c>).
/// </summary>
/// <remarks>
/// <para>
/// A <i>confrère</i> is another haulier the company exchanges fuel with, held as a supplier row. This table
/// keeps the running balance of that exchange: <see cref="QteGasoilEntree"/> is what they have delivered to
/// us, <see cref="QteGasoilSortie"/> what we have taken out for them.
/// </para>
/// <para>
/// <b>The key is natural, not generated.</b> The row is identified by <c>(key_confrere, key_soc)</c> — the
/// partner and the company — and is found by that pair rather than by a <c>"GASOIL"</c> key. This is the only
/// table in the fuel module that does not draw from the shared sequence.
/// </para>
/// <para>
/// <b>The balance is stored, not derived, and that is a real fragility.</b> Every movement is also in
/// <c>t_consommation_gasoil</c>, so the balance could be computed from the journal — but the legacy stores it
/// and the reports read the stored figure directly, and migrated rows carry accumulated values no journal
/// reproduces. Reproducing the stored balance is therefore the faithful choice, with the consequence that it
/// is only ever as correct as the writes that maintain it: the legacy subtracts the whole quantity when a
/// voucher is deleted, and adjusts by the previous quantity when one is corrected. Both paths are modelled
/// here, because a balance that drifts is worse than one that is merely denormalised.
/// </para>
/// </remarks>
public sealed class StockGasoilConfrere : Entity
{
    private StockGasoilConfrere() { }

    public StockGasoilConfrere(string keyConfrere, string societeKey)
    {
        KeyConfrere = string.IsNullOrWhiteSpace(keyConfrere)
            ? throw new ArgumentException("Value is required.", nameof(keyConfrere))
            : keyConfrere.Trim();

        SocieteKey = string.IsNullOrWhiteSpace(societeKey)
            ? throw new ArgumentException("Value is required.", nameof(societeKey))
            : societeKey.Trim();

        Id = $"{SocieteKey}:{KeyConfrere}";
    }

    /// <summary>The fellow company. Despite the name it is a row of <c>T_FOURNISSEURS</c>.</summary>
    public string KeyConfrere { get; private set; } = string.Empty;

    /// <summary>Tenant. Column is <c>key_soc</c>.</summary>
    public string SocieteKey { get; private set; } = string.Empty;

    /// <summary>Fuel the partner has delivered to us.</summary>
    public decimal QteGasoilEntree { get; private set; }

    /// <summary>Fuel we have taken out on the partner's behalf.</summary>
    public decimal QteGasoilSortie { get; private set; }

    /// <summary>Our position with the partner: positive means they are ahead of us.</summary>
    public decimal Solde => QteGasoilEntree - QteGasoilSortie;

    /// <summary>Records a delivery received from the partner (a confrère voucher).</summary>
    public void AjouterEntree(decimal quantite) => QteGasoilEntree += Exiger(quantite, nameof(quantite));

    /// <summary>Records fuel taken out for the partner (a confrère issue).</summary>
    public void AjouterSortie(decimal quantite) => QteGasoilSortie += Exiger(quantite, nameof(quantite));

    /// <summary>
    /// Reverses a delivery, used when a confrère voucher is deleted.
    /// </summary>
    /// <remarks>
    /// The legacy subtracts the full quantity in its list screen. Without this the balance keeps counting fuel
    /// from a document that no longer exists, and the partner's statement stops reconciling.
    /// </remarks>
    public void RetirerEntree(decimal quantite) => QteGasoilEntree -= Exiger(quantite, nameof(quantite));

    /// <summary>Reverses an issue, used when a confrère issue is cancelled.</summary>
    public void RetirerSortie(decimal quantite) => QteGasoilSortie -= Exiger(quantite, nameof(quantite));

    /// <summary>
    /// Replaces a previously-recorded delivery quantity, used when a voucher is corrected.
    /// </summary>
    /// <remarks>
    /// Voluntarily cumulative: the legacy computes <c>(qte - anc_qte) + nouvelle_qte</c> rather than assigning
    /// the new value, because other documents may have moved the balance since. Assigning would silently erase
    /// those movements.
    /// </remarks>
    public void CorrigerEntree(decimal ancienneQuantite, decimal nouvelleQuantite) =>
        QteGasoilEntree = QteGasoilEntree - Exiger(ancienneQuantite, nameof(ancienneQuantite))
                          + Exiger(nouvelleQuantite, nameof(nouvelleQuantite));

    private static decimal Exiger(decimal quantite, string param) =>
        quantite < 0 ? throw new ArgumentOutOfRangeException(param, quantite, "Quantity cannot be negative.")
        : quantite;
}
