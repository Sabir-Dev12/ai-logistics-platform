using Logistics.Domain.Common;

namespace Logistics.Domain.Entities.Shipments;

/// <summary>
/// Per-leg cost input for the voyage cost engine (tractor / trailer / driver / kilometrage).
/// </summary>
public sealed record VoyageCostLeg(
    string KeyTrajet,
    string? KeyTracteur,
    string? KeyRemorque,
    string? KeyChauffeur1,
    string? KeyChauffeur2,
    decimal Kilometrage,
    decimal CoutKmTracteur = 0m,
    // The per-trajet figures below are the ALREADY-APPORTIONED share of a MONTHLY master cost, not a
    // daily rate. t_materiel.cout and t_personnel.cout are monthly, and the legacy engine divides by the
    // month's trajet count: `cout_trac = cout_tracteur / nb_trajet`. There is no per-day cost column
    // anywhere in the schema, so the earlier "CoutJour" naming modelled something that does not exist.
    decimal CoutTracteurParTrajet = 0m,
    decimal CoutRemorqueParTrajet = 0m,
    decimal CoutChauffeurParTrajet = 0m,
    decimal CoutKmGasoil = 0m,
    decimal CoutKmAdBlue = 0m)
{
    /// <summary>
    /// A leg is only costable when it carries a tractor and a non-zero distance — the legacy guard is
    /// <c>key_tracteur is not null and isnull(kilometrage,0) &lt;&gt; 0</c>.
    /// </summary>
    public bool IsCostable => !string.IsNullOrWhiteSpace(KeyTracteur) && Kilometrage != 0m;
}

/// <summary>
/// Equipment parked on the voyage, with its apportioned monthly cost.
/// </summary>
/// <remarks>See <see cref="VoyageCostLeg"/> — this is a monthly cost share, not a daily rate.</remarks>
public sealed record VoyageCostEquipment(string KeyLigneEquipement, string TypeEquipement, int Qte, decimal CoutParTrajet = 0m);

/// <summary>Personnel attached to the voyage, with their apportioned monthly cost.</summary>
public sealed record VoyageCostPersonnel(string KeyPersonnel, decimal CoutParTrajet = 0m);

/// <summary>
/// Everything the cost engine needs for one voyage. Inputs are passed in rather than read from the
/// database so the rules can be tested without one — which matters, because these rules were reverse
/// engineered from SQL and are easy to get subtly wrong.
/// </summary>
public sealed record VoyageCostInputs
{
    public required string KeyVoyage { get; init; }
    /// <summary>Calculation month in <c>MM/yyyy</c> form.</summary>
    public required string MoisCalcul { get; init; }
    public required string Devise { get; init; }
    public DateTime? DateVoyage { get; init; }
    public bool Supprime { get; init; }
    public string? KeyAgenceExploitation { get; init; }
    /// <summary>The agency's customs-clearance place, used to decide whether Coût MEAD applies.</summary>
    public string? LieuDedouanement { get; init; }
    /// <summary>Divisor for the equal split; 0 is treated as 1.</summary>
    public int NbDossVoyageSaisi { get; init; }

    public decimal CoutSociete { get; init; }
    public decimal CoutMead { get; init; }
    public decimal CoutAssuranceMarchandise { get; init; }
    public decimal CoutAgence { get; init; }
    public decimal CoutFraisBancaire { get; init; }

    public IReadOnlyList<VoyageCostLeg> Legs { get; init; } = [];
    public IReadOnlyList<VoyageCostEquipment> Equipements { get; init; } = [];
    public IReadOnlyList<VoyageCostPersonnel> Personnel { get; init; } = [];

    /// <summary>True when at least one attached dossier sits in this agency's customs warehouse.</summary>
    public bool HasMeadDossier { get; init; }

    /// <summary>Operations already recorded in <c>t_cout_operation_voyage_calcule</c>.</summary>
    public IReadOnlyCollection<string> OperationsDejaCalculees { get; init; } = [];

    /// <summary>Divisor used by the equal split, guarding the legacy <c>nb_doss_voyage_saisi = 0</c> case.</summary>
    public int CostDivisor => NbDossVoyageSaisi <= 0 ? 1 : NbDossVoyageSaisi;
}

/// <summary>One computed cost operation, spread over the voyage's dossiers.</summary>
public sealed record CoutOperationAllocation(string Operation, decimal MontantMad, decimal MontantDevise, int NbDossiers);

/// <summary>Per-leg kilometre detail produced by the gasoil / AdBlue routines.</summary>
public sealed record CoutKilometrageLigne(
    string KeyTrajet, string KeyVoyage, string? KeyMateriel, decimal Kilometrage,
    decimal PrixUnitaire, string NatureCout, decimal Montant);

/// <summary>Outcome of a cost-generation run.</summary>
public sealed record VoyageCostResult(
    IReadOnlyList<CoutOperationAllocation> Allocations,
    IReadOnlyList<CoutKilometrageLigne> KilometrageLignes,
    IReadOnlyList<string> OperationsGenerees,
    IReadOnlyList<string> OperationsIgnorees)
{
    public decimal TotalMad => Allocations.Sum(a => a.MontantMad);
}

/// <summary>
/// The voyage costing engine, extracted from <c>ClassGeneration_Cout_Voyage.vb</c>.
/// </summary>
/// <remarks>
/// <para>
/// The legacy routine was one long transaction of raw SQL that walked tractors, trailers, drivers,
/// gasoil and AdBlue and wrote the results into <c>T_REPARTITION_DOSSIER</c>. Three of its properties
/// are the whole reason this is split out as a pure function:
/// </para>
/// <list type="number">
///   <item><b>Eligibility is strict.</b> A voyage is only costable when it is not deleted, has been
///   validated (<c>date_voyage IS NOT NULL</c>) and its <c>date_voyage</c> falls in the calculation
///   month. Getting this wrong silently costs the wrong voyages rather than failing.</item>
///   <item><b>Idempotency is per operation, not per voyage.</b> Each operation is skipped only if a
///   marker for <em>that</em> operation exists, so a partially-costed voyage can be resumed.</item>
///   <item><b>The split is equal, not proportional.</b> Internal costs divide by
///   <c>nb_doss_voyage_saisi</c> (guarded against zero), whereas sales lines split pro rata to
///   <c>poids_taxable</c>. Using the wrong rule mis-allocates every cost.</item>
/// </list>
/// </remarks>
public static class VoyageCostEngine
{
    public const string CoutSociete = "Coût Société";
    public const string CoutAssuranceMarchandise = "Coût Assurance Marchandise";
    public const string CoutMead = "Coût MEAD";
    public const string CoutAgence = "Coût Agence";
    public const string CoutFraisBancaire = "Coût Frais Bancaire";
    public const string CoutTracteur = "Coût Tracteur";
    public const string CoutRemorque = "Coût Remorque";
    public const string CoutChauffeur = "Coût Chauffeur";
    public const string CoutGasoil = "Coût Gasoil";
    public const string CoutAdBlue = "Coût AdBlue";

    public const string NatureCoutGasoil = "Gasoil";
    public const string NatureCoutAdBlue = "AdBlue";

    /// <summary>
    /// Whether a voyage is eligible for the standard cost run.
    /// </summary>
    /// <remarks>
    /// Returns a reason rather than a bare boolean so the API can explain the refusal, which the
    /// legacy screen could not.
    /// </remarks>
    public static (bool Eligible, string? Reason) IsEligible(VoyageCostInputs inputs)
    {
        if (inputs.Supprime)
        {
            return (false, "Le voyage est supprimé.");
        }

        if (inputs.DateVoyage is null)
        {
            return (false, "Le voyage n'est pas validé (date_voyage est nulle).");
        }

        // The legacy filter is FORMAT(date_voyage,'MM/yyyy') = <mois>.
        var moisVoyage = inputs.DateVoyage.Value.ToString("MM/yyyy");
        if (!string.Equals(moisVoyage, inputs.MoisCalcul, StringComparison.Ordinal))
        {
            return (false, $"Le voyage est daté {moisVoyage}, hors du mois de calcul {inputs.MoisCalcul}.");
        }

        return (true, null);
    }

    /// <summary>
    /// Computes every eligible, not-yet-calculated operation for a voyage. Pure: nothing is persisted.
    /// </summary>
    public static VoyageCostResult Generate(VoyageCostInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var allocations = new List<CoutOperationAllocation>();
        var kilometrage = new List<CoutKilometrageLigne>();
        var generated = new List<string>();
        var skipped = new List<string>();

        var (eligible, _) = IsEligible(inputs);
        if (!eligible)
        {
            // Nothing is costable; report every operation as skipped so callers can surface why.
            return new VoyageCostResult([], [], [], StandardOperations().ToArray());
        }

        var divisor = inputs.CostDivisor;

        void Add(string operation, decimal montant, bool applies = true)
        {
            if (!applies)
            {
                return;
            }

            if (!ShouldCompute(inputs, operation))
            {
                skipped.Add(operation);
                return;
            }

            allocations.Add(new CoutOperationAllocation(operation, montant, montant, divisor));
            generated.Add(operation);
        }

        // Fixed company-level costs, each divided equally across the voyage's dossiers.
        Add(CoutSociete, Divide(inputs.CoutSociete, divisor));
        Add(CoutAssuranceMarchandise, Divide(inputs.CoutAssuranceMarchandise, divisor));
        Add(CoutFraisBancaire, Divide(inputs.CoutFraisBancaire, divisor));

        // MEAD only applies when a dossier actually sits in this agency's customs warehouse.
        Add(CoutMead, Divide(inputs.CoutMead, divisor), inputs.HasMeadDossier);

        // Agency cost is scoped to the exploitation agency and is meaningless without one.
        Add(CoutAgence, Divide(inputs.CoutAgence, divisor),
            !string.IsNullOrWhiteSpace(inputs.KeyAgenceExploitation));

        // Equipment and personnel daily rates, multiplied by the quantity on the voyage.
        Add(CoutTracteur, Divide(SumEquipment(inputs, "TRACTEUR"), divisor));
        Add(CoutRemorque, Divide(SumEquipment(inputs, "REMORQUE"), divisor));
        Add(CoutChauffeur, Divide(inputs.Personnel.Sum(p => p.CoutParTrajet), divisor));

        // Per-leg kilometre costs. Gasoil and AdBlue also produce detail rows in
        // t_cout_kilometrage_voyage, which is what the legacy screens reconcile against.
        var costableLegs = inputs.Legs.Where(l => l.IsCostable).ToArray();

        var tracteurKm = costableLegs.Sum(l => l.Kilometrage * l.CoutKmTracteur);
        Add(CoutTracteur, Divide(tracteurKm, divisor), tracteurKm != 0m);

        var gasoilTotal = costableLegs.Sum(l => l.Kilometrage * l.CoutKmGasoil);
        if (gasoilTotal != 0m && ShouldCompute(inputs, CoutGasoil))
        {
            foreach (var leg in costableLegs.Where(l => l.CoutKmGasoil != 0m))
            {
                kilometrage.Add(new CoutKilometrageLigne(
                    leg.KeyTrajet, inputs.KeyVoyage, leg.KeyTracteur, leg.Kilometrage,
                    leg.CoutKmGasoil, NatureCoutGasoil,
                    Math.Round(leg.Kilometrage * leg.CoutKmGasoil, 2, MidpointRounding.AwayFromZero)));
            }

            allocations.Add(new CoutOperationAllocation(CoutGasoil, Divide(gasoilTotal, divisor), Divide(gasoilTotal, divisor), divisor));
            generated.Add(CoutGasoil);
        }
        else if (gasoilTotal != 0m)
        {
            skipped.Add(CoutGasoil);
        }

        var adBlueTotal = costableLegs.Sum(l => l.Kilometrage * l.CoutKmAdBlue);
        if (adBlueTotal != 0m && ShouldCompute(inputs, CoutAdBlue))
        {
            foreach (var leg in costableLegs.Where(l => l.CoutKmAdBlue != 0m))
            {
                kilometrage.Add(new CoutKilometrageLigne(
                    leg.KeyTrajet, inputs.KeyVoyage, leg.KeyTracteur, leg.Kilometrage,
                    leg.CoutKmAdBlue, NatureCoutAdBlue,
                    Math.Round(leg.Kilometrage * leg.CoutKmAdBlue, 2, MidpointRounding.AwayFromZero)));
            }

            allocations.Add(new CoutOperationAllocation(CoutAdBlue, Divide(adBlueTotal, divisor), Divide(adBlueTotal, divisor), divisor));
            generated.Add(CoutAdBlue);
        }
        else if (adBlueTotal != 0m)
        {
            skipped.Add(CoutAdBlue);
        }

        return new VoyageCostResult(allocations, kilometrage, generated, skipped);
    }

    /// <summary>
    /// The twelve operations a recompute purges. <c>Frais Autoroute</c> and <c>Frais Gasoil</c> are
    /// deliberately excluded: their own screens purge them.
    /// </summary>
    public static IReadOnlyList<string> StandardOperations() => CoutOperationVoyageCalcule.StandardOperations;

    private static bool ShouldCompute(VoyageCostInputs inputs, string operation) =>
        !inputs.OperationsDejaCalculees.Contains(operation);

    private static decimal SumEquipment(VoyageCostInputs inputs, string type) =>
        inputs.Equipements
            .Where(e => string.Equals(e.TypeEquipement, type, StringComparison.OrdinalIgnoreCase))
            .Sum(e => e.CoutParTrajet * e.Qte);

    private static decimal Divide(decimal montant, int divisor) =>
        Math.Round(montant / divisor, 3, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Per-leg kilometric cost detail (<c>t_cout_kilometrage_voyage</c>), written by the gasoil routine.
/// </summary>
/// <remarks>
/// Regenerated per leg: the legacy code deletes by <c>(key_voyage, key_trajet)</c> and re-inserts, and
/// its purge filters <c>nature_cout in ('Gasoil','AdBlue')</c>.
/// </remarks>
public sealed class CoutKilometrageVoyage : Entity
{
    public const string NatureGasoil = "Gasoil";
    public const string NatureAdBlue = "AdBlue";

    private CoutKilometrageVoyage() { }

    public CoutKilometrageVoyage(
        string keyTrajet, string keyVoyage, string? keyMateriel,
        decimal kilometrage, decimal prixUnitaire, string natureCout)
    {
        Id = $"{keyVoyage}:{keyTrajet}:{natureCout}";
        KeyTrajet = Require(keyTrajet, nameof(keyTrajet));
        KeyVoyage = Require(keyVoyage, nameof(keyVoyage));
        KeyMateriel = Trim(keyMateriel);
        Kilometrage = kilometrage;
        PrixUnitaire = prixUnitaire;
        NatureCout = Require(natureCout, nameof(natureCout));
        Montant = Math.Round(kilometrage * prixUnitaire, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Builds a row from an engine output line.</summary>
    public CoutKilometrageVoyage(CoutKilometrageLigne ligne)
        : this(ligne.KeyTrajet, ligne.KeyVoyage, ligne.KeyMateriel, ligne.Kilometrage, ligne.PrixUnitaire, ligne.NatureCout)
    {
    }

    public string KeyTrajet { get; private set; } = string.Empty;
    public string KeyVoyage { get; private set; } = string.Empty;
    public string? KeyMateriel { get; private set; }
    public decimal Kilometrage { get; private set; }
    public decimal PrixUnitaire { get; private set; }
    /// <summary>'Gasoil' or 'AdBlue' — the only two values the legacy purge recognises.</summary>
    public string NatureCout { get; private set; } = string.Empty;
    public decimal Montant { get; private set; }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
