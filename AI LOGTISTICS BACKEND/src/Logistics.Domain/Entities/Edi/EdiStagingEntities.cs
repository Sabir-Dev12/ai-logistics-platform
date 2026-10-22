using Logistics.Domain.Common;

namespace Logistics.Domain.Entities.Edi;

/// <summary>
/// Staging row for one voyage header parsed from an STC/EDI file (<c>tmp_edi_voyage</c>).
/// </summary>
/// <remarks>
/// <para>
/// Staging is a <b>permanent table scoped by user + company + agency</b>, not a SQL temp table — the
/// legacy module has no <c>#tmp</c> anywhere. Rows accumulate across imports until the user clears them
/// or a promotion consumes them.
/// </para>
/// <para>
/// <c>commentaire</c> is not free text: it is the <b>anomaly register</b>, reset to null before each
/// validation pass and extended with one label per failed check. The operators work from that column, so
/// a row with text is a row that will not import.
/// </para>
/// </remarks>
public sealed class EdiVoyageStaging : Entity
{
    private EdiVoyageStaging() { }

    public EdiVoyageStaging(string keyEdiVoyage, string societe, string? utilisateur, string? agence, string? numVoyage)
    {
        Id = Require(keyEdiVoyage, nameof(keyEdiVoyage));
        Societe = Require(societe, nameof(societe));
        Utilisateur = Trim(utilisateur);
        Agence = Trim(agence);
        NumVoyage = Trim(numVoyage);
    }

    public string KeyEdiVoyage => Id;

    /// <summary>
    /// Tenant column is <c>soc</c> — and this table carries a <b>second</b> scoping column,
    /// <c>agence</c>, plus <c>utilisateur</c>. No other staging table in the module has all three.
    /// </summary>
    public string Societe { get; private set; } = string.Empty;
    public string? Agence { get; private set; }
    public string? Utilisateur { get; private set; }

    public string? NumVoyage { get; private set; }
    public string? PortDepart { get; private set; }
    /// <summary>Stored as <b>text</b>: the legacy validation is <c>IsDate()</c> on the raw field.</summary>
    public string? DateDepart { get; private set; }
    public string? PortArrive { get; private set; }
    public string? DateArrivee { get; private set; }
    public string? Navire { get; private set; }
    public string? Vehicule { get; private set; }
    public string? Caisse { get; private set; }
    /// <summary>Text, validated with <c>ISNUMERIC</c> then compared against the detail count.</summary>
    public string? NbDossiers { get; private set; }
    /// <summary>Stored decoded: "Complet" or "Groupage".</summary>
    public string? TypeVoyage { get; private set; }
    public string? Plomb { get; private set; }
    /// <summary>RI when a vehicle is present, empty on import, MI when set from the grid.</summary>
    public string? TypeDossier { get; private set; }

    /// <summary>The anomaly register. A non-empty value blocks promotion.</summary>
    public string? Commentaire { get; private set; }

    public bool ADesAnomalies => !string.IsNullOrWhiteSpace(Commentaire);

    public void SetEntete(
        string? portDepart, string? dateDepart, string? portArrive, string? dateArrivee, string? navire,
        string? vehicule, string? caisse, string? nbDossiers, string? typeVoyage, string? plomb, string? typeDossier)
    {
        PortDepart = Trim(portDepart);
        DateDepart = Trim(dateDepart);
        PortArrive = Trim(portArrive);
        DateArrivee = Trim(dateArrivee);
        Navire = Trim(navire);
        Vehicule = Trim(vehicule);
        Caisse = Trim(caisse);
        NbDossiers = Trim(nbDossiers);
        TypeVoyage = Trim(typeVoyage);
        Plomb = Trim(plomb);
        TypeDossier = Trim(typeDossier);
    }

    /// <summary>
    /// Adds one anomaly label to the register.
    /// </summary>
    /// <remarks>
    /// Labels accumulate rather than replace, which is what lets an operator fix several problems from a
    /// single validation pass instead of one per attempt.
    /// </remarks>
    public void AjouterAnomalie(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Commentaire = string.IsNullOrWhiteSpace(Commentaire) ? message : $"{Commentaire} / {message}";
    }

    /// <summary>Clears the register, as the legacy <c>controle()</c> did before each pass.</summary>
    public void ReinitialiserAnomalies() => Commentaire = null;

    public void SetAnomalies(string? commentaire) => Commentaire = Trim(commentaire);

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Staging row for one dossier line parsed from an STC/EDI file (<c>tmp_edi_dossier</c>).
/// </summary>
/// <remarks>
/// Holds both the file-derived columns and the <b>reconciliation</b> columns filled during validation —
/// <c>INCOTERM_CLT</c>, <c>DEVISE_CLT</c>, <c>COD_CLT</c>, <c>KEY_CLIENT</c> and friends resolve the
/// file's EDI codes against the application's referentials. A row imports only when the whole
/// reconciliation succeeded.
/// </remarks>
public sealed class EdiDossierStaging : Entity
{
    private EdiDossierStaging() { }

    public EdiDossierStaging(string keyEdiDossier, string societe, string? utilisateur, string? agence, string? numVoyage)
    {
        Id = Require(keyEdiDossier, nameof(keyEdiDossier));
        Societe = Require(societe, nameof(societe));
        Utilisateur = Trim(utilisateur);
        Agence = Trim(agence);
        NumVoyage = Trim(numVoyage);
    }

    public string KeyEdiDossier => Id;
    public string Societe { get; private set; } = string.Empty;
    public string? Agence { get; private set; }
    public string? Utilisateur { get; private set; }

    public string? NumVoyage { get; private set; }
    /// <summary>Text; validated with <c>ISNUMERIC</c> and <c>&gt;= 1</c>.</summary>
    public string? Position { get; private set; }

    public string? CodeExpediteur { get; private set; }
    public string? NomExpediteur { get; private set; }
    public string? Adresse1Expediteur { get; private set; }
    public string? Adresse2Expediteur { get; private set; }
    public string? Adresse3Expediteur { get; private set; }
    public string? VilleExpediteur { get; private set; }
    public string? RefExpediteur { get; private set; }

    public string? CodeDestinataire { get; private set; }
    public string? NomDestinataire { get; private set; }
    public string? Adresse1Destinataire { get; private set; }
    public string? Adresse2Destinataire { get; private set; }
    public string? Adresse3Destinataire { get; private set; }
    public string? VilleDestinataire { get; private set; }
    public string? RefDestinataire { get; private set; }

    /// <summary>Description only — the HS code is split out into <see cref="Sh4"/>.</summary>
    public string? Marchandise { get; private set; }
    public string? Emballage { get; private set; }
    public string? Plan { get; private set; }

    public string? Colis { get; private set; }
    public string? Poids { get; private set; }
    public string? Volume { get; private set; }
    public string? ValeurMarchandise { get; private set; }

    public string? Incoterm { get; private set; }
    public string? Cod { get; private set; }
    public string? Valeur { get; private set; }
    public string? Devise { get; private set; }
    /// <summary>HS code, the part of the goods field after the <c>" HS "</c> separator.</summary>
    public string? Sh4 { get; private set; }

    // Five repeating invoice blocks. The casing below is the legacy column spelling, which is itself
    // inconsistent (`FACture1` but `FACture1_Montant`).
    public string? Facture1 { get; private set; }
    public string? Facture1Date { get; private set; }
    public string? Facture1Montant { get; private set; }
    public string? Facture2 { get; private set; }
    public string? Facture2Date { get; private set; }
    public string? Facture2Montant { get; private set; }
    public string? Facture3 { get; private set; }
    public string? Facture3Date { get; private set; }
    public string? Facture3Montant { get; private set; }
    public string? Facture4 { get; private set; }
    public string? Facture4Date { get; private set; }
    public string? Facture4Montant { get; private set; }
    public string? Facture5 { get; private set; }
    public string? Facture5Date { get; private set; }
    public string? Facture5Montant { get; private set; }

    // Reconciliation columns, filled during validation.
    /// <summary>Written at import from the file; overwritten by the referential resolution.</summary>
    public string? DeviseClt { get; private set; }
    public string? IncotermClt { get; private set; }
    public string? CodClt { get; private set; }
    public string? KeyClient { get; private set; }
    public string? Client { get; private set; }
    public string? KeyCommercial { get; private set; }
    public string? Commercial { get; private set; }
    public string? LieuDedouanement { get; private set; }

    public string? Commentaire { get; private set; }

    public bool ADesAnomalies => !string.IsNullOrWhiteSpace(Commentaire);

    /// <summary>The dossier number the promotion builds: <c>num_voyage-position</c>.</summary>
    public string? NumDossier =>
        string.IsNullOrWhiteSpace(NumVoyage) || string.IsNullOrWhiteSpace(Position)
            ? null
            : $"{NumVoyage.Trim()}-{Position.Trim()}";

    public void AjouterAnomalie(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Commentaire = string.IsNullOrWhiteSpace(Commentaire) ? message : $"{Commentaire} / {message}";
    }

    public void ReinitialiserAnomalies() => Commentaire = null;

    public void SetCodeHs(string? sh4) => Sh4 = Trim(sh4);

    /// <summary>Stores the five invoice blocks. <paramref name="factures"/> is ordered 1..5.</summary>
    public void SetFactures(IReadOnlyList<(string? Numero, string? Date, string? Montant)> factures)
    {
        ArgumentNullException.ThrowIfNull(factures);

        for (var i = 0; i < Math.Min(5, factures.Count); i++)
        {
            var (numero, date, montant) = factures[i];

            switch (i)
            {
                case 0: Facture1 = Trim(numero); Facture1Date = Trim(date); Facture1Montant = Trim(montant); break;
                case 1: Facture2 = Trim(numero); Facture2Date = Trim(date); Facture2Montant = Trim(montant); break;
                case 2: Facture3 = Trim(numero); Facture3Date = Trim(date); Facture3Montant = Trim(montant); break;
                case 3: Facture4 = Trim(numero); Facture4Date = Trim(date); Facture4Montant = Trim(montant); break;
                default: Facture5 = Trim(numero); Facture5Date = Trim(date); Facture5Montant = Trim(montant); break;
            }
        }
    }

    /// <summary>Records the result of resolving the file's codes against the referentials.</summary>
    public void SetReconciliation(
        string? incotermClt, string? deviseClt, string? codClt,
        string? keyClient, string? client, string? keyCommercial, string? commercial, string? lieuDedouanement)
    {
        IncotermClt = Trim(incotermClt);
        DeviseClt = Trim(deviseClt);
        CodClt = Trim(codClt);
        KeyClient = Trim(keyClient);
        Client = Trim(client);
        KeyCommercial = Trim(keyCommercial);
        Commercial = Trim(commercial);
        LieuDedouanement = Trim(lieuDedouanement);
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
