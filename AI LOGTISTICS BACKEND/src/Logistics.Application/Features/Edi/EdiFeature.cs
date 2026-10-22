using Logistics.Application.Abstractions;
using Logistics.Domain.Entities.Edi;

namespace Logistics.Application.Features.Edi;

/// <summary>Summary of a staging import, as the legacy grid header showed it.</summary>
public sealed record EdiImportResultat(
    bool FichierAccepte,
    int NbLignes,
    int NbEntetes,
    int NbDossiers,
    int NbAnomalies,
    IReadOnlyList<EdiErreurFichier> ErreursFichier,
    IReadOnlyList<EdiAnomalieResume> Anomalies);

public sealed record EdiAnomalieResume(int NumeroLigne, string Type, string Reference, IReadOnlyList<string> Messages);

public interface IEdiStagingRepository
{
    /// <summary>
    /// Removes every staged row for a user's slice.
    /// </summary>
    /// <remarks>
    /// Staging is scoped by company <b>and</b> agency <b>and</b> user, and an import replaces that slice
    /// wholesale — the legacy screens prompted before doing so and then emptied it.
    /// </remarks>
    Task<int> PurgerAsync(string societe, string? agence, string? utilisateur, CancellationToken cancellationToken = default);

    Task<int> CompterDossiersStagedAsync(string numVoyage, string societe, string? utilisateur, CancellationToken cancellationToken = default);

    void Add(EdiVoyageStaging voyage);
    void Add(EdiDossierStaging dossier);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IEdiStcService
{
    /// <summary>
    /// Analyses a file without touching the database.
    /// </summary>
    /// <remarks>
    /// The parser is pure, so this is a genuine dry run: it reports what would be staged and which
    /// records carry anomalies, and changes nothing.
    /// </remarks>
    EdiResultatAnalyse Analyser(IReadOnlyList<string> lignes);

    /// <summary>Parses a file and stages its records for the current user.</summary>
    Task<EdiImportResultat> ImporterAsync(IReadOnlyList<string> lignes, CancellationToken cancellationToken = default);
}

/// <summary>
/// STC/EDI import use cases — the port of <c>frm_Integration_EDI_IMPORT_STC.vb</c>.
/// </summary>
/// <remarks>
/// Staging only. Promotion into <c>t_voyages</c> / <c>t_dossiers</c> requires the referential
/// reconciliation (ports, clients, incoterms, currencies, COD) that turns the file's EDI codes into
/// application keys, and is not part of this increment — a staged row that has not been reconciled
/// would fail at promotion anyway.
/// </remarks>
public sealed class EdiStcService(
    IEdiStagingRepository staging,
    IKeyGenerator keys,
    ITenantContext tenant) : IEdiStcService
{
    public EdiResultatAnalyse Analyser(IReadOnlyList<string> lignes) => EdiStcParser.Analyser(lignes);

    public async Task<EdiImportResultat> ImporterAsync(IReadOnlyList<string> lignes, CancellationToken cancellationToken = default)
    {
        var analyse = EdiStcParser.Analyser(lignes);

        // A file with misaligned lines is rejected before anything is written, so a failed import never
        // leaves a half-populated staging slice behind.
        if (!analyse.FichierAccepte)
        {
            return new EdiImportResultat(false, analyse.NbLignes, 0, 0, 0, analyse.ErreursFichier, []);
        }

        await staging.PurgerAsync(tenant.SocieteKey, tenant.AgenceKey, tenant.UserDisplayName, cancellationToken);

        foreach (var entete in analyse.Entetes)
        {
            var row = new EdiVoyageStaging(
                await keys.NextAsync("SEQ_EDI", cancellationToken),
                tenant.SocieteKey, tenant.UserDisplayName, tenant.AgenceKey, entete.NumVoyage);

            row.SetEntete(entete.PortDepart,
                entete.DateDepart?.ToString("dd/MM/yyyy"), entete.PortArrive,
                entete.DateArrivee?.ToString("dd/MM/yyyy"), entete.Navire, entete.Vehicule, entete.Caisse,
                entete.NbDossiers?.ToString(), entete.TypeVoyage, entete.Plomb, entete.TypeDossier);

            foreach (var anomalie in entete.Anomalies)
            {
                row.AjouterAnomalie(anomalie);
            }

            staging.Add(row);
        }

        foreach (var dossier in analyse.Dossiers)
        {
            var row = new EdiDossierStaging(
                await keys.NextAsync("SEQ_EDI", cancellationToken),
                tenant.SocieteKey, tenant.UserDisplayName, tenant.AgenceKey, dossier.NumVoyage);

            row.SetFactures(dossier.Factures
                .Select(f => ((string?)f.Numero, f.Date?.ToString("dd/MM/yyyy"), f.Montant?.ToString()))
                .ToArray());

            row.SetCodeHs(dossier.CodeHs);

            foreach (var anomalie in dossier.Anomalies)
            {
                row.AjouterAnomalie(anomalie);
            }

            staging.Add(row);
        }

        await staging.SaveChangesAsync(cancellationToken);

        var anomalies = analyse.Entetes
            .Where(e => !e.Valide)
            .Select(e => new EdiAnomalieResume(e.NumeroLigne, "Voyage", e.NumVoyage, e.Anomalies))
            .Concat(analyse.Dossiers
                .Where(d => !d.Valide)
                .Select(d => new EdiAnomalieResume(d.NumeroLigne, "Dossier",
                    d.NumDossier ?? d.NumVoyage, d.Anomalies)))
            .ToArray();

        return new EdiImportResultat(true, analyse.NbLignes, analyse.Entetes.Count, analyse.Dossiers.Count,
            analyse.NbAnomalies, [], anomalies);
    }
}
