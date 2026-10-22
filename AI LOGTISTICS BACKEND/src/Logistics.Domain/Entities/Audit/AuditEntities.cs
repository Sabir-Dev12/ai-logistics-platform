using Logistics.Domain.Common;

namespace Logistics.Domain.Entities.Audit;

/// <summary>
/// Master-data change journal (<c>t_hist_referentiel</c>). Written on every create / update / delete
/// of a referential — the equivalent of the legacy <c>tools.JournalReferentiel(...)</c>, which has
/// roughly 370 call sites.
/// </summary>
/// <remarks>
/// <see cref="NomTable"/> is a misnomer inherited from the legacy schema: it holds a <em>business
/// label</em> ("Agence", "Pays", "Rubrique"), not a physical table name. <see cref="SocReferentiel"/>
/// is nullable because rows written before multi-tenancy was introduced have no company, and the
/// legacy readers explicitly tolerate that with <c>soc_referentiel = @soc or soc_referentiel is
/// null</c>. That same tolerance is preserved by the repository query methods.
/// </remarks>
public sealed class HistReferentiel
{
    private HistReferentiel() { }

    public HistReferentiel(
        string programme,
        string evenement,
        string? utilisateur,
        DateTime dateOperation,
        string? commentaire,
        string? socReferentiel,
        string? posteUtilisateur)
    {
        NomTable = programme;
        Evenement = evenement;
        Utilisateur = utilisateur;
        DateOperation = dateOperation;
        Commentaire = commentaire;
        SocReferentiel = socReferentiel;
        PosteUtilisateur = posteUtilisateur;
    }

    public long Id { get; private set; }
    /// <summary>Business label of the referential that changed.</summary>
    public string NomTable { get; private set; } = string.Empty;
    /// <summary>"Ajout" / "Modification" / "Suppression" / "Génération Reporting" / …</summary>
    public string Evenement { get; private set; } = string.Empty;
    /// <summary>
    /// The user's display name (<c>nom + prénom</c>), because that is what the legacy application
    /// writes — not the personnel key.
    /// </summary>
    public string? Utilisateur { get; private set; }
    public DateTime DateOperation { get; private set; }
    /// <summary>Field-by-field "Anc.xxx = …" difference string.</summary>
    public string? Commentaire { get; private set; }
    /// <summary>Tenant. NULL means visible to every company (legacy rows).</summary>
    public string? SocReferentiel { get; private set; }
    public string? PosteUtilisateur { get; private set; }
}

/// <summary>
/// Application event / error log (<c>t_evenement_app</c>), the equivalent of
/// <c>tools.InsertEventApp(...)</c>.
/// </summary>
/// <remarks>
/// The legacy implementation has two defects this design deliberately does not reproduce:
/// its <c>Catch</c> block calls itself (infinite recursion when the log table is unreachable), and
/// <c>code_message</c> is almost always the meaningless literal <c>"ex.HResult"</c>. Here the logger
/// is fail-safe and the code is a real, stable error code.
/// </remarks>
public sealed class EvenementApp
{
    private EvenementApp() { }

    public EvenementApp(
        DateTime dateEvenement,
        string? utilisateur,
        string message,
        string? codeMessage,
        string? programme,
        string? posteUtilisateur)
    {
        DateEvenement = dateEvenement;
        Utilisateur = utilisateur;
        Message = message;
        CodeMessage = codeMessage;
        Programme = programme;
        PosteUtilisateur = posteUtilisateur;
    }

    public long Id { get; private set; }
    public DateTime DateEvenement { get; private set; }
    public string? Utilisateur { get; private set; }
    public string Message { get; private set; } = string.Empty;
    /// <summary>Stable application error code (not the legacy constant "ex.HResult").</summary>
    public string? CodeMessage { get; private set; }
    /// <summary>Human context, e.g. "Pays.Create".</summary>
    public string? Programme { get; private set; }
    public string? PosteUtilisateur { get; private set; }
}

/// <summary>
/// Business audit trail entry for any entity, modelling the uniform 11-column shape of the 63 legacy
/// <c>t_trace_*</c> tables.
/// </summary>
/// <remarks>
/// Every legacy trace table differs only by the name of its entity-key column, and one writer
/// (<c>tools.Trace(NameTable, name_champ, …)</c>) serves all of them by string interpolation. That
/// design is not translatable to EF directly, so the entity key is modelled as a discriminated
/// <see cref="EntityKey"/> plus an <see cref="EntityType"/> discriminator, and a TPH-style inheritance
/// hierarchy is mapped onto the individual legacy tables where they are needed.
/// <para>
/// Two legacy behaviours are preserved because consumers depend on them: <see cref="DateTraceUser"/>
/// is the <em>user-asserted</em> business date (editable), while <see cref="DateTraceSystem"/> is the
/// server clock; and rows are <em>not</em> append-only — they are physically deleted and compensated
/// with an explicit "$SEvv" row.
/// </para>
/// </remarks>
public abstract class TraceEntry
{
    /// <summary>Required by EF Core for materialisation; <c>protected</c> because the type is abstract.</summary>
    protected TraceEntry()
    {
    }

    protected TraceEntry(
        string entityKey,
        string? societe,
        string? agence,
        string? keyStatut,
        DateTime dateTraceUser,
        DateTime dateTraceSystem,
        string? commentaire,
        string? utilisateur,
        string? keyDocument,
        double valeur,
        string? posteUtilisateur)
    {
        EntityKey = entityKey;
        Societe = societe;
        Agence = agence;
        KeyStatut = keyStatut;
        DateTraceUser = dateTraceUser;
        DateTraceSystem = dateTraceSystem;
        Commentaire = commentaire;
        Utilisateur = utilisateur;
        KeyDocument = keyDocument;
        Valeur = valeur;
        PosteUtilisateur = posteUtilisateur;
    }

    public long Id { get; protected set; }
    /// <summary>Value of the owning entity's key column (the legacy "name_champ").</summary>
    public string EntityKey { get; protected set; } = string.Empty;
    public string? Societe { get; protected set; }
    public string? Agence { get; protected set; }
    /// <summary>
    /// Change code, e.g. <c>$ACLT</c> (client created), <c>$CVOY</c> (voyage created),
    /// <c>$SREG</c> (règlement deleted). Joins to <c>t_statut.key_statut</c>.
    /// </summary>
    public string? KeyStatut { get; protected set; }
    /// <summary>Business date of the event, supplied by the caller.</summary>
    public DateTime DateTraceUser { get; protected set; }
    /// <summary>When the row was physically written (server clock).</summary>
    public DateTime DateTraceSystem { get; protected set; }
    public string? Commentaire { get; protected set; }
    /// <summary>User display name, as the legacy application stores it.</summary>
    public string? Utilisateur { get; protected set; }
    public string? KeyDocument { get; protected set; }
    /// <summary>Quantity/amount attached to the event; usually 0.</summary>
    public double Valeur { get; protected set; }
    public string? PosteUtilisateur { get; protected set; }
}

/// <summary>Trace trail for clients (<c>t_trace_client</c>, key column <c>key_client</c>).</summary>
public sealed class TraceClient : TraceEntry
{
    private TraceClient() { }

    public TraceClient(
        string keyClient, string? societe, string? agence, string? keyStatut,
        DateTime dateTraceUser, DateTime dateTraceSystem, string? commentaire,
        string? utilisateur, string? keyDocument, double valeur, string? posteUtilisateur)
        : base(keyClient, societe, agence, keyStatut, dateTraceUser, dateTraceSystem, commentaire, utilisateur, keyDocument, valeur, posteUtilisateur)
    {
    }
}

/// <summary>Trace trail for payments (<c>t_trace_reglement</c>, key column <c>key_reglement</c>).</summary>
public sealed class TraceReglement : TraceEntry
{
    private TraceReglement() { }

    public TraceReglement(
        string keyReglement, string? societe, string? agence, string? keyStatut,
        DateTime dateTraceUser, DateTime dateTraceSystem, string? commentaire,
        string? utilisateur, string? keyDocument, double valeur, string? posteUtilisateur)
        : base(keyReglement, societe, agence, keyStatut, dateTraceUser, dateTraceSystem, commentaire, utilisateur, keyDocument, valeur, posteUtilisateur)
    {
    }
}

/// <summary>Trace trail for sales invoices (<c>t_trace_facture_vente</c>).</summary>
public sealed class TraceFactureVente : TraceEntry
{
    private TraceFactureVente() { }

    public TraceFactureVente(
        string keyFacture, string? societe, string? agence, string? keyStatut,
        DateTime dateTraceUser, DateTime dateTraceSystem, string? commentaire,
        string? utilisateur, string? keyDocument, double valeur, string? posteUtilisateur)
        : base(keyFacture, societe, agence, keyStatut, dateTraceUser, dateTraceSystem, commentaire, utilisateur, keyDocument, valeur, posteUtilisateur)
    {
    }
}

/// <summary>Trace trail for dossiers (<c>t_trace_dossier</c>) — the most-written trace table after voyages.</summary>
public sealed class TraceDossier : TraceEntry
{
    private TraceDossier() { }

    public TraceDossier(
        string keyDossier, string? societe, string? agence, string? keyStatut,
        DateTime dateTraceUser, DateTime dateTraceSystem, string? commentaire,
        string? utilisateur, string? keyDocument, double valeur, string? posteUtilisateur)
        : base(keyDossier, societe, agence, keyStatut, dateTraceUser, dateTraceSystem, commentaire, utilisateur, keyDocument, valeur, posteUtilisateur)
    {
    }
}

/// <summary>Trace trail for voyages (<c>t_trace_voyage</c>) — the most-written trace table overall.</summary>
public sealed class TraceVoyage : TraceEntry
{
    private TraceVoyage() { }

    public TraceVoyage(
        string keyVoyage, string? societe, string? agence, string? keyStatut,
        DateTime dateTraceUser, DateTime dateTraceSystem, string? commentaire,
        string? utilisateur, string? keyDocument, double valeur, string? posteUtilisateur)
        : base(keyVoyage, societe, agence, keyStatut, dateTraceUser, dateTraceSystem, commentaire, utilisateur, keyDocument, valeur, posteUtilisateur)
    {
    }
}

/// <summary>Trace trail for bookings (<c>t_trace_booking</c>), keyed on <c>key_booking</c>.</summary>
public sealed class TraceBooking : TraceEntry
{
    private TraceBooking() { }

    public TraceBooking(
        string keyBooking, string? societe, string? agence, string? keyStatut,
        DateTime dateTraceUser, DateTime dateTraceSystem, string? commentaire,
        string? utilisateur, string? keyDocument, double valeur, string? posteUtilisateur)
        : base(keyBooking, societe, agence, keyStatut, dateTraceUser, dateTraceSystem, commentaire, utilisateur, keyDocument, valeur, posteUtilisateur)
    {
    }
}

/// <summary>Trace trail for quotations (<c>t_trace_cotation</c>), keyed on <c>key_cotation</c>.</summary>
public sealed class TraceCotation : TraceEntry
{
    private TraceCotation() { }

    public TraceCotation(
        string keyCotation, string? societe, string? agence, string? keyStatut,
        DateTime dateTraceUser, DateTime dateTraceSystem, string? commentaire,
        string? utilisateur, string? keyDocument, double valeur, string? posteUtilisateur)
        : base(keyCotation, societe, agence, keyStatut, dateTraceUser, dateTraceSystem, commentaire, utilisateur, keyDocument, valeur, posteUtilisateur)
    {
    }
}

/// <summary>
/// Trace trail for purchase tariff scales (<c>T_TRACE_BAREME_ACHAT_COTATION</c>), keyed on
/// <c>key_bareme</c>.
/// </summary>
/// <remarks>
/// This table is essential rather than incidental: the archive tables reuse the source
/// <c>key_bareme</c>, so several archived generations of the same scale share a primary key. Only the
/// trace rows carry the sequence of events that distinguish them.
/// </remarks>
public sealed class TraceBaremeAchat : TraceEntry
{
    private TraceBaremeAchat() { }

    public TraceBaremeAchat(
        string keyBareme, string? societe, string? agence, string? keyStatut,
        DateTime dateTraceUser, DateTime dateTraceSystem, string? commentaire,
        string? utilisateur, string? keyDocument, double valeur, string? posteUtilisateur)
        : base(keyBareme, societe, agence, keyStatut, dateTraceUser, dateTraceSystem, commentaire, utilisateur, keyDocument, valeur, posteUtilisateur)
    {
    }
}

/// <summary>
/// Trace trail for employees (<c>t_trace_personnel</c>), keyed on <c>key_personnel</c>.
/// </summary>
/// <remarks>
/// Written the first time a user changes their own password, so the audit trail for credential changes exists
/// rather than being implied. A password change is exactly the event an auditor asks about, and the legacy
/// records it with status <c>$MPWD</c> (<c>ClassPersonnel.vb:969</c>).
/// </remarks>
public sealed class TracePersonnel : TraceEntry
{
    /// <summary>The legacy status code for a password change.</summary>
    public const string StatutModificationMotDePasse = "$MPWD";

    private TracePersonnel() { }

    public TracePersonnel(
        string keyPersonnel, string? societe, string? agence, string? keyStatut,
        DateTime dateTraceUser, DateTime dateTraceSystem, string? commentaire,
        string? utilisateur, string? keyDocument, double valeur, string? posteUtilisateur)
        : base(keyPersonnel, societe, agence, keyStatut, dateTraceUser, dateTraceSystem, commentaire, utilisateur, keyDocument, valeur, posteUtilisateur)
    {
    }
}
