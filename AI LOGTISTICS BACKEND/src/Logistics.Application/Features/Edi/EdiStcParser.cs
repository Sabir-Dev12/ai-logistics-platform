using System.Globalization;

namespace Logistics.Application.Features.Edi;

/// <summary>
/// Fixed-width STC/EDI file layout and the anomalies each record can carry.
/// </summary>
/// <remarks>
/// <para>
/// The file is <b>1000 characters per line</b>, plain text. A record's type is decided by the two
/// characters at offset 9: <c>"00"</c> is a voyage header, anything else is a dossier line. That offset
/// therefore does double duty — a type flag on headers, the <c>POSITION</c> on details — which is the
/// single easiest thing to misread in this format.
/// </para>
/// <para>
/// Offsets below are 1-based because they come straight from the legacy <c>Mid(ligne, debut, longueur)</c>
/// calls; <see cref="Champ"/> converts. Keeping the original numbering makes the two directly comparable,
/// which matters when reconciling against the VB source.
/// </para>
/// </remarks>
public static class EdiStc
{
    public const int LongueurLigne = 1000;

    /// <summary>Record flag at offset 9 meaning "voyage header".</summary>
    public const string CodeEntete = "00";

    /// <summary>
    /// Width of one repeating invoice block: number (8) + date (8) + amount (10).
    /// </summary>
    /// <remarks>
    /// The extraction recorded a stride of 10, which cannot hold a 26-character block. 26 is the only
    /// self-consistent value and is the one used; see <c>LireFactures</c>.
    /// </remarks>
    public const int BlocFacture = 26;

    /// <summary>Separator inside the goods-description field, splitting description from HS code.</summary>
    public const string SeparateurHs = " HS ";

    /// <summary>Currency code in the file meaning EUR.</summary>
    public const string DeviseCodeEur = "00";

    /// <summary>COD code meaning "no COD" — the legacy test is <c>cod &lt;&gt; '10'</c>.</summary>
    public const string CodAucun = "10";

    /// <summary>Default packaging type when the field is blank.</summary>
    public const string EmballageDefaut = "COLIS";

    /// <summary>
    /// Anomaly labels, reproduced verbatim including accents and the <c>arrivé</c>/<c>arrivée</c>
    /// inconsistency the legacy code has between the port message and the date message.
    /// </summary>
    public static class Anomalies
    {
        // Header
        public const string NumeroVoyage = "N° Voyage invalide";
        public const string PortDepart = "Port départ invalide";
        public const string PortArrive = "Port arrivé invalide";
        public const string Navire = "Navire invalide";
        public const string TypeVoyage = "Type voyage invalide";
        public const string Remorque = "Remorque invalide";
        public const string Tc = "TC invalide";
        public const string DateDepart = "Date départ invalide";
        public const string DateArrivee = "Date arrivée invalide";
        public const string NatureVoyage = "Nature voyage invalide";
        public const string NbDossiers = "Nb dossiers invalide";
        public const string DatesIncoherentes = "Date arrivée inférieure à la date de départ";

        // Detail
        public const string Voyage = "Voyage invalide";
        public const string Position = "Position invalide";
        public const string Incoterm = "Incoterm invalide";
        public const string Devise = "Devise invalide";
        public const string Client = "Client invalide";
        public const string Commercial = "Commercial invalide";
        public const string LieuDedouanement = "Lieu Dédouanement invalide";
        public const string Cod = "COD invalide";
        public const string Colis = "Colis invalide";
        public const string Poids = "Poids invalide";
        public const string Volume = "Volume invalide";
        public const string Valeur = "Valeur invalide";
        public const string MontantAssignes = "Mt.Assignés invalide";
        public const string FactureEnDouble = "Facture en double";

        public static string DateFacture(int index) => $"Date facture {index} invalide";
        public static string MontantFacture(int index) => $"Montant facture {index} invalide";
    }

    /// <summary>Reads a 1-based, length-delimited field, trimmed and with control characters removed.</summary>
    public static string Champ(string ligne, int debut, int longueur)
    {
        if (string.IsNullOrEmpty(ligne) || debut < 1 || debut > ligne.Length)
        {
            return string.Empty;
        }

        var disponible = Math.Min(longueur, ligne.Length - (debut - 1));
        return disponible <= 0
            ? string.Empty
            : ligne.Substring(debut - 1, disponible).Trim().Trim('\0');
    }

    /// <summary>
    /// Parses a date field in <c>yyyyMMdd</c> order, as the file stores it.
    /// </summary>
    /// <remarks>
    /// The legacy code reassembles the field as <c>Mid(37,2)/Mid(35,2)/Mid(31,4)</c> — day, month, year —
    /// which is the same thing as reading it as <c>yyyyMMdd</c>. The all-zero field means "no date" and
    /// yields null rather than an anomaly, because the file uses it for optional dates.
    /// </remarks>
    public static DateTime? DateEdi(string champ)
    {
        if (string.IsNullOrWhiteSpace(champ) || champ.Trim() == new string('0', 8))
        {
            return null;
        }

        return DateTime.TryParseExact(champ.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    /// <summary>
    /// Reads a numeric field that is genuinely text in the file.
    /// </summary>
    /// <remarks>
    /// Staging columns are character data holding raw file text, so the legacy validation is
    /// <c>ISNUMERIC()</c> plus a cast rather than a typed read. Blank is treated as absent, not as zero.
    /// </remarks>
    public static decimal? NombreEdi(string champ)
    {
        if (string.IsNullOrWhiteSpace(champ))
        {
            return null;
        }

        return decimal.TryParse(champ.Trim().Replace(',', '.'), NumberStyles.Number,
            CultureInfo.InvariantCulture, out var valeur)
            ? valeur
            : null;
    }

    /// <summary>Scales a file value by a divisor — grams to kilograms, cents to currency units.</summary>
    public static decimal? Diviser(decimal? valeur, decimal diviseur) =>
        valeur.HasValue ? valeur.Value / diviseur : null;
}

/// <summary>One voyage header parsed from the file, with the anomalies found on it.</summary>
public sealed record EdiEnteteVoyage(
    int NumeroLigne,
    string NumVoyage,
    string PortDepart,
    DateTime? DateDepart,
    string PortArrive,
    DateTime? DateArrivee,
    string Navire,
    string Vehicule,
    string Caisse,
    int? NbDossiers,
    string TypeVoyage,
    string Plomb,
    string TypeDossier,
    IReadOnlyList<string> Anomalies)
{
    public bool Valide => Anomalies.Count == 0;
}

/// <summary>One dossier line parsed from the file, with the anomalies found on it.</summary>
public sealed record EdiLigneDossier(
    int NumeroLigne,
    string NumVoyage,
    int? Position,
    string CodeExpediteur,
    string NomExpediteur,
    string CodeDestinataire,
    string NomDestinataire,
    string VilleDestinataire,
    string Marchandise,
    string CodeHs,
    string Emballage,
    decimal? Colis,
    decimal? Poids,
    decimal? Volume,
    decimal? ValeurMarchandise,
    string Incoterm,
    string Cod,
    decimal? Valeur,
    string Devise,
    IReadOnlyList<EdiFacture> Factures,
    IReadOnlyList<string> Anomalies)
{
    public bool Valide => Anomalies.Count == 0;

    /// <summary>The dossier number the promotion builds: <c>num_voyage-position</c>.</summary>
    public string? NumDossier => Position.HasValue ? $"{NumVoyage}-{Position.Value}" : null;
}

/// <summary>One of the five repeating supplier-invoice blocks on a dossier line.</summary>
public sealed record EdiFacture(int Index, string Numero, DateTime? Date, decimal? Montant)
{
    public bool Renseignee => !string.IsNullOrWhiteSpace(Numero) || Date.HasValue || Montant.HasValue;
}

/// <summary>A file-level problem that prevents the whole file from being imported.</summary>
public sealed record EdiErreurFichier(int NumeroLigne, string Message);

/// <summary>Outcome of parsing an STC/EDI file.</summary>
public sealed record EdiResultatAnalyse(
    bool FichierAccepte,
    int NbLignes,
    IReadOnlyList<EdiErreurFichier> ErreursFichier,
    IReadOnlyList<EdiEnteteVoyage> Entetes,
    IReadOnlyList<EdiLigneDossier> Dossiers)
{
    public int NbAnomalies => Entetes.Sum(e => e.Anomalies.Count) + Dossiers.Sum(d => d.Anomalies.Count);
}

/// <summary>
/// Parser for the STC/EDI fixed-width import file.
/// </summary>
/// <remarks>
/// <para>
/// Pure: it reads strings and returns records, touching nothing. That is deliberate — the format is
/// entirely offset-driven and the failure modes are silent (a one-character shift reads a plausible but
/// wrong value), so the parsing has to be testable without a database or a file.
/// </para>
/// <para>
/// Only <b>format and type</b> checks happen here. Checks that need reference data — does the port exist,
/// is the client known, was this voyage already imported — belong to the resolution step, because they
/// cannot be answered from the bytes alone.
/// </para>
/// </remarks>
public static class EdiStcParser
{
    /// <summary>
    /// Parses the file.
    /// </summary>
    /// <remarks>
    /// Misaligned lines reject the <b>whole file</b> before any record is produced, which is what the
    /// legacy <c>verification_decalage_ligne</c> did: a file with a shifted line would otherwise import a
    /// complete but quietly wrong set of voyages.
    /// </remarks>
    public static EdiResultatAnalyse Analyser(IEnumerable<string> lignes)
    {
        ArgumentNullException.ThrowIfNull(lignes);

        var toutes = lignes.ToList();
        var erreurs = new List<EdiErreurFichier>();

        for (var i = 0; i < toutes.Count; i++)
        {
            var ligne = toutes[i] ?? string.Empty;

            // Trailing blank lines are an artefact of file reading, not a misalignment.
            if (ligne.Length == 0 && i == toutes.Count - 1)
            {
                continue;
            }

            if (ligne.Length != EdiStc.LongueurLigne)
            {
                erreurs.Add(new EdiErreurFichier(i + 1,
                    $"Décalage ligne {i + 1} : {ligne.Length} caractères au lieu de {EdiStc.LongueurLigne}."));
            }
        }

        if (erreurs.Count > 0)
        {
            // Rejected wholesale: no partial import.
            return new EdiResultatAnalyse(false, toutes.Count, erreurs, [], []);
        }

        var entetes = new List<EdiEnteteVoyage>();
        var dossiers = new List<EdiLigneDossier>();

        for (var i = 0; i < toutes.Count; i++)
        {
            var ligne = toutes[i] ?? string.Empty;
            if (ligne.Length == 0)
            {
                continue;
            }

            var numero = i + 1;
            var type = EdiStc.Champ(ligne, 9, 2);

            if (type == EdiStc.CodeEntete)
            {
                entetes.Add(LireEntete(ligne, numero));
            }
            else
            {
                dossiers.Add(LireDossier(ligne, numero));
            }
        }

        return new EdiResultatAnalyse(true, toutes.Count, [], entetes, dossiers);
    }

    private static EdiEnteteVoyage LireEntete(string ligne, int numero)
    {
        var anomalies = new List<string>();

        var numVoyage = EdiStc.Champ(ligne, 1, 8);
        var portDepart = EdiStc.Champ(ligne, 11, 20);
        var dateDepart = EdiStc.DateEdi(EdiStc.Champ(ligne, 31, 8));
        var portArrive = EdiStc.Champ(ligne, 39, 20);
        var dateArrivee = EdiStc.DateEdi(EdiStc.Champ(ligne, 59, 8));
        var navire = EdiStc.Champ(ligne, 67, 20);
        var vehicule = EdiStc.Champ(ligne, 87, 20);
        var caisse = EdiStc.Champ(ligne, 107, 20);
        var nbDossiersTexte = EdiStc.Champ(ligne, 127, 2);
        var typeVoyageCode = EdiStc.Champ(ligne, 129, 1);
        var plomb = EdiStc.Champ(ligne, 130, 15);

        if (string.IsNullOrWhiteSpace(numVoyage)) anomalies.Add(EdiStc.Anomalies.NumeroVoyage);
        if (string.IsNullOrWhiteSpace(portDepart)) anomalies.Add(EdiStc.Anomalies.PortDepart);
        if (string.IsNullOrWhiteSpace(portArrive)) anomalies.Add(EdiStc.Anomalies.PortArrive);
        if (string.IsNullOrWhiteSpace(navire)) anomalies.Add(EdiStc.Anomalies.Navire);
        if (dateDepart is null) anomalies.Add(EdiStc.Anomalies.DateDepart);
        if (dateArrivee is null) anomalies.Add(EdiStc.Anomalies.DateArrivee);

        // The file stores a decoded-nature character: C = Complet, G = Groupage.
        var typeVoyage = typeVoyageCode.ToUpperInvariant() switch
        {
            "C" => "Complet",
            "G" => "Groupage",
            _ => string.Empty
        };

        if (typeVoyage.Length == 0) anomalies.Add(EdiStc.Anomalies.NatureVoyage);

        var nbDossiers = EdiStc.NombreEdi(nbDossiersTexte);
        if (nbDossiers is null || nbDossiers < 1)
        {
            anomalies.Add(EdiStc.Anomalies.NbDossiers);
        }

        // A vehicle (flat trailer) types the record RI; without one it needs a container.
        //
        // The legacy "Remorque invalide" message belongs to the case where the type was set to RI
        // independently of the vehicle — possible from the grid, but not on import, where the type is
        // *derived* from the vehicle. That check therefore cannot fire here and lives in the resolution
        // step instead, rather than sitting unreachable in the parser.
        var typeDossier = string.IsNullOrWhiteSpace(vehicule) ? string.Empty : "RI";

        if (typeDossier != "RI" && string.IsNullOrWhiteSpace(caisse))
        {
            anomalies.Add(EdiStc.Anomalies.Tc);
        }

        if (dateDepart.HasValue && dateArrivee.HasValue && dateArrivee.Value.Date < dateDepart.Value.Date)
        {
            anomalies.Add(EdiStc.Anomalies.DatesIncoherentes);
        }

        return new EdiEnteteVoyage(numero, numVoyage, portDepart, dateDepart, portArrive, dateArrivee,
            navire, vehicule, caisse, nbDossiers is null ? null : (int)nbDossiers.Value, typeVoyage, plomb,
            typeDossier, anomalies);
    }

    private static EdiLigneDossier LireDossier(string ligne, int numero)
    {
        var anomalies = new List<string>();

        var numVoyage = EdiStc.Champ(ligne, 1, 8);
        var position = EdiStc.NombreEdi(EdiStc.Champ(ligne, 9, 2));

        if (string.IsNullOrWhiteSpace(numVoyage)) anomalies.Add(EdiStc.Anomalies.Voyage);
        if (position is null || position < 1) anomalies.Add(EdiStc.Anomalies.Position);

        // The goods field packs the description and the HS code around a " HS " separator.
        var marchandiseBrute = EdiStc.Champ(ligne, 437, 40);
        var separateur = marchandiseBrute.IndexOf(EdiStc.SeparateurHs, StringComparison.Ordinal);
        var marchandise = separateur >= 0 ? marchandiseBrute[..separateur] : marchandiseBrute;
        var codeHs = separateur >= 0
            ? marchandiseBrute[(separateur + EdiStc.SeparateurHs.Length)..]
            : string.Empty;

        var emballage = EdiStc.Champ(ligne, 477, 40);
        if (string.IsNullOrWhiteSpace(emballage))
        {
            emballage = EdiStc.EmballageDefaut;
        }

        // Colis is text in the file and validated as numeric.
        var colis = EdiStc.NombreEdi(EdiStc.Champ(ligne, 517, 8));
        if (colis is null) anomalies.Add(EdiStc.Anomalies.Colis);

        // Weight arrives in grams and volume in thousandths, so both are scaled down.
        var poids = EdiStc.Diviser(EdiStc.NombreEdi(EdiStc.Champ(ligne, 525, 8)), 1000m);
        if (poids is null) anomalies.Add(EdiStc.Anomalies.Poids);

        var volume = EdiStc.Diviser(EdiStc.NombreEdi(EdiStc.Champ(ligne, 533, 6)), 1000m);
        if (volume is null) anomalies.Add(EdiStc.Anomalies.Volume);

        // Value and assigned amount arrive in cents.
        var valeurMarchandise = EdiStc.Diviser(EdiStc.NombreEdi(EdiStc.Champ(ligne, 549, 8)), 100m);
        if (valeurMarchandise is null) anomalies.Add(EdiStc.Anomalies.Valeur);

        var incoterm = EdiStc.Champ(ligne, 557, 2);
        var cod = EdiStc.Champ(ligne, 559, 2);
        // NOTE: the extraction recorded this field as 10 characters, but offset 561 + 10 would run to
        // 570 and overlap DEVISE at 569. A fixed-width layout cannot overlap, so 8 is the consistent
        // reading and is what is used here.
        var valeur = EdiStc.Diviser(EdiStc.NombreEdi(EdiStc.Champ(ligne, 561, 8)), 100m);
        if (valeur is null) anomalies.Add(EdiStc.Anomalies.MontantAssignes);

        var devise = EdiStc.Champ(ligne, 569, 2);

        var factures = LireFactures(ligne, anomalies);

        return new EdiLigneDossier(numero, numVoyage, position is null ? null : (int)position.Value,
            EdiStc.Champ(ligne, 11, 8), EdiStc.Champ(ligne, 19, 30),
            EdiStc.Champ(ligne, 179, 8), EdiStc.Champ(ligne, 187, 30), EdiStc.Champ(ligne, 307, 25),
            marchandise, codeHs, emballage, colis, poids, volume, valeurMarchandise,
            incoterm, cod, valeur,
            // The file uses "00" to mean EUR.
            devise == EdiStc.DeviseCodeEur ? "EUR" : devise,
            factures, anomalies);
    }

    /// <summary>
    /// Reads the five repeating invoice blocks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each block is a number (8), a date (8) and an amount (10), so the self-consistent stride is 26.
    /// The extraction recorded a stride of 10, which cannot hold a 26-character block; 26 is used because
    /// it is the only value that keeps the blocks from overlapping. This is the one layout detail that
    /// could not be reconciled against the source and should be confirmed against a real file.
    /// </para>
    /// <para>
    /// The legacy check rejects a file where any two invoice numbers match, because the same supplier
    /// invoice cannot legitimately appear twice on one dossier.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<EdiFacture> LireFactures(string ligne, List<string> anomalies)
    {
        var factures = new List<EdiFacture>();

        for (var index = 1; index <= 5; index++)
        {
            var baseOffset = 573 + ((index - 1) * EdiStc.BlocFacture);
            var numero = EdiStc.Champ(ligne, baseOffset, 8);
            var date = EdiStc.DateEdi(EdiStc.Champ(ligne, baseOffset + 8, 8));
            var montant = EdiStc.Diviser(EdiStc.NombreEdi(EdiStc.Champ(ligne, baseOffset + 16, 10)), 100m);

            var renseignee = !string.IsNullOrWhiteSpace(numero) || date.HasValue || montant.HasValue;

            if (renseignee)
            {
                if (date is null) anomalies.Add(EdiStc.Anomalies.DateFacture(index));
                if (montant is null) anomalies.Add(EdiStc.Anomalies.MontantFacture(index));
            }

            factures.Add(new EdiFacture(index, numero, date, montant));
        }

        var doubles = factures
            .Where(f => !string.IsNullOrWhiteSpace(f.Numero))
            .GroupBy(f => f.Numero.Trim(), StringComparer.OrdinalIgnoreCase)
            .Any(g => g.Count() > 1);

        if (doubles)
        {
            anomalies.Add(EdiStc.Anomalies.FactureEnDouble);
        }

        return factures;
    }
}
