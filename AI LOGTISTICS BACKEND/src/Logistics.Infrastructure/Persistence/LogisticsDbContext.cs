using Logistics.Domain.Common;
using Logistics.Domain.Entities.Audit;
using Logistics.Domain.Entities.Bookings;
using Logistics.Domain.Entities.Clients;
using Logistics.Domain.Entities.Edi;
using Logistics.Domain.Entities.Quotations;
using Logistics.Domain.Entities.Resources;
using Logistics.Domain.Entities.Invoicing;
using Logistics.Domain.Entities.Payments;
using Logistics.Domain.Entities.ReferenceData;
using Logistics.Domain.Entities.Shipments;
using Logistics.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Logistics.Infrastructure.Persistence;

/// <summary>
/// EF Core context mapped onto the <em>legacy</em> TransmarNetref table and column names.
/// </summary>
/// <remarks>
/// <para>
/// Table and column names deliberately keep the legacy spellings, including the inconsistent casing
/// (<c>t_clients</c> vs <c>T_ENTETE_FACTURE_VENTE</c>), the French vocabulary and the two known typos
/// (<c>comission_banque</c> with one 'm', <c>FRAIS_BANCAIRES</c> in the plural). Preserving them is
/// what makes a straight data migration from the existing SQL Server database possible, and it keeps
/// the schema readable to the DBAs and business analysts who already know it.
/// </para>
/// <para>
/// The C# side is free to use proper PascalCase names because every property is mapped explicitly.
/// </para>
/// <para>
/// <b>Schema provenance.</b> The legacy repository contains no DDL at all — no <c>.sql</c>,
/// <c>.mdf</c> or <c>.bak</c> — so every column, type and length below was reverse-engineered from
/// raw SQL string literals across ~2,000 VB files. Types and lengths are therefore <em>inferred</em>
/// and must be reconciled against the live database before any data migration. Genuinely uncertain
/// columns carry a comment saying so.
/// </para>
/// </remarks>
public sealed class LogisticsDbContext(DbContextOptions<LogisticsDbContext> options) : DbContext(options)
{
    // Reference data
    public DbSet<Societe> Societes => Set<Societe>();
    public DbSet<Agence> Agences => Set<Agence>();
    public DbSet<Pays> Pays => Set<Pays>();
    public DbSet<ZonePays> ZonesPays => Set<ZonePays>();
    public DbSet<Ville> Villes => Set<Ville>();
    public DbSet<Devise> Devises => Set<Devise>();
    public DbSet<Port> Ports => Set<Port>();
    public DbSet<Incoterm> Incoterms => Set<Incoterm>();
    public DbSet<TypeDossier> TypesDossier => Set<TypeDossier>();
    public DbSet<ModePaiement> ModesPaiement => Set<ModePaiement>();
    public DbSet<CompteTva> ComptesTva => Set<CompteTva>();
    public DbSet<NatureFrais> NaturesFrais => Set<NatureFrais>();
    public DbSet<ClasseClient> ClassesClient => Set<ClasseClient>();
    public DbSet<StatutClient> StatutsClient => Set<StatutClient>();
    public DbSet<Risque> Risques => Set<Risque>();
    public DbSet<SecteurActivite> SecteursActivite => Set<SecteurActivite>();
    public DbSet<GroupeSociete> GroupesSociete => Set<GroupeSociete>();
    public DbSet<Tresorie> Tresoreries => Set<Tresorie>();
    public DbSet<NatureFraisBancaire> NaturesFraisBancaire => Set<NatureFraisBancaire>();
    public DbSet<TypeIncidentPaiement> TypesIncidentPaiement => Set<TypeIncidentPaiement>();
    public DbSet<Cod> Cods => Set<Cod>();
    public DbSet<ParametreSociete> ParametresSociete => Set<ParametreSociete>();
    public DbSet<RapportPoidsVolume> RapportsPoidsVolume => Set<RapportPoidsVolume>();

    // Clients
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<AdresseClient> AdressesClient => Set<AdresseClient>();
    public DbSet<TiersContact> ContactsTiers => Set<TiersContact>();
    public DbSet<BanqueTiers> BanquesTiers => Set<BanqueTiers>();

    // Payments
    public DbSet<EnteteReglement> EntetesReglement => Set<EnteteReglement>();
    public DbSet<DetailReglement> DetailsReglement => Set<DetailReglement>();
    public DbSet<FraisBancaire> FraisBancaires => Set<FraisBancaire>();
    public DbSet<EnteteRemise> EntetesRemise => Set<EnteteRemise>();
    public DbSet<TauxChange> TauxChanges => Set<TauxChange>();

    // Invoicing
    public DbSet<EnteteFactureVente> EntetesFactureVente => Set<EnteteFactureVente>();
    public DbSet<DetailFactureVente> DetailsFactureVente => Set<DetailFactureVente>();
    public DbSet<TvaFactureVente> TvaFactureVente => Set<TvaFactureVente>();
    public DbSet<RepartitionDossier> RepartitionsDossier => Set<RepartitionDossier>();

    // Shipments (Exploitation)
    public DbSet<Voyage> Voyages => Set<Voyage>();
    public DbSet<TrajetVoyage> TrajetsVoyage => Set<TrajetVoyage>();
    public DbSet<EquipementVoyage> EquipementsVoyage => Set<EquipementVoyage>();
    public DbSet<DetailEquipementVoyage> DetailsEquipementVoyage => Set<DetailEquipementVoyage>();
    public DbSet<Dossier> Dossiers => Set<Dossier>();
    public DbSet<ColisageDossier> ColisagesDossier => Set<ColisageDossier>();
    public DbSet<DossierVoyage> DossiersVoyage => Set<DossierVoyage>();
    public DbSet<CodDossier> CodsDossier => Set<CodDossier>();
    public DbSet<FactureFournisseurDossier> FacturesFournisseurDossier => Set<FactureFournisseurDossier>();
    public DbSet<CoutOperationVoyageCalcule> CoutsOperationVoyageCalcule => Set<CoutOperationVoyageCalcule>();
    public DbSet<CoutKilometrageVoyage> CoutsKilometrageVoyage => Set<CoutKilometrageVoyage>();
    public DbSet<Statut> Statuts => Set<Statut>();

    // Bookings
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<ColisageBooking> ColisagesBooking => Set<ColisageBooking>();
    public DbSet<EquipementBooking> EquipementsBooking => Set<EquipementBooking>();
    public DbSet<DetailVoyageBooking> DetailsVoyageBooking => Set<DetailVoyageBooking>();

    // Quotations
    public DbSet<Cotation> Cotations => Set<Cotation>();
    public DbSet<ColisageCotation> ColisagesCotation => Set<ColisageCotation>();
    public DbSet<PrixCotation> PrixCotation => Set<PrixCotation>();
    public DbSet<AchatCotation> AchatsCotation => Set<AchatCotation>();
    public DbSet<TauxChangeCotation> TauxChangeCotation => Set<TauxChangeCotation>();
    public DbSet<DocumentCotation> DocumentsCotation => Set<DocumentCotation>();
    public DbSet<BaseCalculCotation> BasesCalculCotation => Set<BaseCalculCotation>();
    public DbSet<FranchiseCotation> FranchisesCotation => Set<FranchiseCotation>();

    // Purchase tariff scales ("barème achat") — a self-contained pricing feature of the cotation module
    public DbSet<BaremeAchatCotation> BaremesAchatCotation => Set<BaremeAchatCotation>();
    public DbSet<ColonneBaremeAchat> ColonnesBaremeAchat => Set<ColonneBaremeAchat>();
    public DbSet<DetailBaremeAchat> DetailsBaremeAchat => Set<DetailBaremeAchat>();
    public DbSet<PrestationBaremeAchat> PrestationsBaremeAchat => Set<PrestationBaremeAchat>();
    public DbSet<HistBaremeAchat> HistBaremesAchat => Set<HistBaremeAchat>();
    public DbSet<HistColonneBaremeAchat> HistColonnesBaremeAchat => Set<HistColonneBaremeAchat>();
    public DbSet<HistDetailBaremeAchat> HistDetailsBaremeAchat => Set<HistDetailBaremeAchat>();
    public DbSet<HistPrestationBaremeAchat> HistPrestationsBaremeAchat => Set<HistPrestationBaremeAchat>();

    // Resources
    public DbSet<Personnel> Personnel => Set<Personnel>();
    public DbSet<Materiel> Materiels => Set<Materiel>();
    public DbSet<TypeMateriel> TypesMateriel => Set<TypeMateriel>();
    public DbSet<NatureMateriel> NaturesMateriel => Set<NatureMateriel>();
    public DbSet<CoutKilometrageGasoil> CoutsKilometrageGasoil => Set<CoutKilometrageGasoil>();
    public DbSet<PersonnelTypeDossier> PersonnelTypesDossier => Set<PersonnelTypeDossier>();

    // Carburant / gasoil
    public DbSet<Citerne> Citernes => Set<Citerne>();
    public DbSet<SortieGasoil> SortiesGasoil => Set<SortieGasoil>();
    public DbSet<BonGasoil> BonsGasoil => Set<BonGasoil>();
    public DbSet<JaugeGasoil> JaugesGasoil => Set<JaugeGasoil>();
    public DbSet<PrixGasoil> PrixGasoil => Set<PrixGasoil>();
    public DbSet<ApprovisionnementCiterne> ApprovisionnementsCiterne => Set<ApprovisionnementCiterne>();
    public DbSet<DetailApprovisionnementCiterne> DetailsApprovisionnementCiterne => Set<DetailApprovisionnementCiterne>();
    public DbSet<ConsommationGasoil> ConsommationsGasoil => Set<ConsommationGasoil>();
    public DbSet<StockGasoilConfrere> StocksGasoilConfrere => Set<StockGasoilConfrere>();

    // EDI staging
    public DbSet<EdiVoyageStaging> EdiVoyagesStaging => Set<EdiVoyageStaging>();
    public DbSet<EdiDossierStaging> EdiDossiersStaging => Set<EdiDossierStaging>();

    // Audit
    public DbSet<HistReferentiel> HistReferentiel => Set<HistReferentiel>();
    public DbSet<EvenementApp> EvenementsApp => Set<EvenementApp>();
    public DbSet<TraceClient> TracesClient => Set<TraceClient>();
    public DbSet<TraceReglement> TracesReglement => Set<TraceReglement>();
    public DbSet<TraceFactureVente> TracesFactureVente => Set<TraceFactureVente>();
    public DbSet<TraceDossier> TracesDossier => Set<TraceDossier>();
    public DbSet<TraceVoyage> TracesVoyage => Set<TraceVoyage>();
    public DbSet<TraceBooking> TracesBooking => Set<TraceBooking>();
    public DbSet<TraceCotation> TracesCotation => Set<TraceCotation>();
    public DbSet<TraceBaremeAchat> TracesBaremeAchat => Set<TraceBaremeAchat>();
    public DbSet<TracePersonnel> TracesPersonnel => Set<TracePersonnel>();

    /// <summary>
    /// Rejects values that cannot be stored, before the database does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EF Core performs no client-side length checking: a string longer than its column is sent as-is and
    /// SQL Server raises a truncation error, which arrives as a <c>DbUpdateException</c> and was reported to
    /// the caller as <b>409 Persistence conflict</b> with no indication of which field was at fault. That is
    /// the wrong status — a caller cannot resolve it by retrying — and a useless message.
    /// </para>
    /// <para>
    /// The limits come from the model's own <c>MaxLength</c> metadata rather than from attributes on the
    /// request DTOs, so this cannot drift away from the columns: there is one statement of each limit and
    /// EF already owns it. It also means the guarantee covers every entity and every future write path
    /// without anyone remembering to annotate anything.
    /// </para>
    /// <para>
    /// All offending fields are reported together. Naming only the first would make a caller fix a
    /// many-field problem one round-trip at a time.
    /// </para>
    /// </remarks>
    private void ValiderLongueurs()
    {
        Dictionary<string, string[]>? erreurs = null;

        foreach (var entree in ChangeTracker.Entries())
        {
            if (entree.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            foreach (var propriete in entree.Properties)
            {
                if (propriete.Metadata.ClrType != typeof(string) || propriete.CurrentValue is not string valeur)
                {
                    continue;
                }

                if (propriete.Metadata.GetMaxLength() is not { } maximum || valeur.Length <= maximum)
                {
                    continue;
                }

                erreurs ??= new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                erreurs[propriete.Metadata.Name] =
                    [$"Length must not exceed {maximum} characters (got {valeur.Length})."];
            }
        }

        if (erreurs is not null)
        {
            throw new ChampInvalideException(erreurs);
        }
    }

    public override int SaveChanges()
    {
        ValiderLongueurs();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValiderLongueurs();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ValiderLongueurs();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValiderLongueurs();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Gives every decimal a deliberate scale instead of EF's implicit <c>decimal(18,2)</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Without this, any decimal property that is not explicitly configured falls back to EF's default of
    /// <c>decimal(18,2)</c>, and EF says plainly what that means: the value "will be silently truncated if it
    /// does not fit". The warning it emits on every cold start (<c>Model.Validation</c> 30000) named <b>76
    /// properties</b>, and they were not incidental ones — <c>Dossier.Volume</c>, <c>Dossier.VolumeTaxable</c>,
    /// <c>ColisageDossier.Volume</c>, <c>CoutKilometrageGasoil.RatioConsommation</c>, <c>Dossier.TauxChangeValMarch</c>.
    /// </para>
    /// <para>
    /// <b>The domain contradicts the storage.</b> This codebase measures volumes to <b>three</b> decimals —
    /// <c>MesuresTaxables</c> and the EDI reader both work at 3dp — so a volume of 12.345 was being stored as
    /// 12.34 with no error raised anywhere. A silent truncation of a taxable measure is a wrong invoice, not a
    /// cosmetic defect.
    /// </para>
    /// <para>
    /// <c>(18,3)</c> rather than <c>(18,2)</c> is the safest default available here: it accommodates the 3dp
    /// volumes, weights and ratios the domain actually computes. Money stays at 2dp wherever it is explicitly
    /// configured, because an explicit configuration still wins over a convention. Raising the scale costs
    /// nothing in correctness and only a byte or two of storage; lowering it silently corrupts.
    /// </para>
    /// </remarks>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<decimal>().HavePrecision(18, 3);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureReferenceData(modelBuilder);
        ConfigureClients(modelBuilder);
        ConfigurePayments(modelBuilder);
        ConfigureInvoicing(modelBuilder);
        ConfigureShipments(modelBuilder);
        ConfigureBookings(modelBuilder);
        ConfigureQuotations(modelBuilder);
        ConfigureBaremesAchat(modelBuilder);
        ConfigureResources(modelBuilder);
        ConfigureCarburant(modelBuilder);
        ConfigureEdi(modelBuilder);
        ConfigureAudit(modelBuilder);
    }

    private static void ConfigureReferenceData(ModelBuilder b)
    {
        b.Entity<Societe>(e =>
        {
            e.ToTable("t_societe");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_soc").HasMaxLength(10);
            e.Property(x => x.IntituleSoc).HasColumnName("intitule_soc").HasMaxLength(120).IsRequired();
            e.Property(x => x.SocAbrege).HasColumnName("soc_abrege").HasMaxLength(30);
            e.Property(x => x.NumRc).HasColumnName("num_rc").HasMaxLength(30);
            e.Property(x => x.CentreRc).HasColumnName("centre_rc").HasMaxLength(30);
            e.Property(x => x.NumIf).HasColumnName("num_if").HasMaxLength(30);
            e.Property(x => x.NumPatente).HasColumnName("num_patente").HasMaxLength(30);
            e.Property(x => x.NumIce).HasColumnName("num_ice").HasMaxLength(30);
            e.Property(x => x.NumCnss).HasColumnName("num_cnss").HasMaxLength(30);
            e.Property(x => x.Adresse1Soc).HasColumnName("adresse1_soc").HasMaxLength(120);
            e.Property(x => x.Adresse2Soc).HasColumnName("adresse2_soc").HasMaxLength(120);
            e.Property(x => x.CpSoc).HasColumnName("cp_soc").HasMaxLength(15);
            e.Property(x => x.VilleSoc).HasColumnName("ville_soc").HasMaxLength(60);
            e.Property(x => x.PaysSoc).HasColumnName("pays_soc").HasMaxLength(3);
            e.Property(x => x.TelSoc).HasColumnName("tel_soc").HasMaxLength(30);
            e.Property(x => x.FaxSoc).HasColumnName("fax_soc").HasMaxLength(30);
            e.Property(x => x.EmailSoc).HasColumnName("email_soc").HasMaxLength(120);
            e.Property(x => x.WebSoc).HasColumnName("web_soc").HasMaxLength(120);
            e.Property(x => x.DeviseSoc).HasColumnName("devise_soc").HasMaxLength(5).IsRequired();
            e.Property(x => x.CpGeneralClient).HasColumnName("cp_general_client").HasMaxLength(20);
            e.Property(x => x.CpGeneralFournisseur).HasColumnName("cp_general_fournisseur").HasMaxLength(20);
            e.Property(x => x.CpGeneralDebours).HasColumnName("cp_general_debours").HasMaxLength(20);
            e.Property(x => x.JournalVenteSoc).HasColumnName("journal_vente_soc").HasMaxLength(10);
            e.Property(x => x.JournalAchatSoc).HasColumnName("journal_achat_soc").HasMaxLength(10);
            e.Property(x => x.JournalDeboursSoc).HasColumnName("journal_debours_soc").HasMaxLength(10);
            e.Property(x => x.JournalOdSoc).HasColumnName("journal_od_soc").HasMaxLength(10);
            e.Property(x => x.CptGain).HasColumnName("cpt_gain").HasMaxLength(20);
            e.Property(x => x.CptPerte).HasColumnName("cpt_perte").HasMaxLength(20);
            e.Property(x => x.CptFraisBanque).HasColumnName("cpt_frais_banque").HasMaxLength(20);
            e.Property(x => x.CptTvaBanque).HasColumnName("cpt_tva_banque").HasMaxLength(20);
            e.Property(x => x.CptCommissionBanque).HasColumnName("cpt_commission_banque").HasMaxLength(20);
            e.Property(x => x.NumerotationFacture).HasColumnName("numerotation_facture").HasMaxLength(30);
            e.Property(x => x.DeclarationTva).HasColumnName("declaration_tva").HasMaxLength(30);
            e.Property(x => x.ClotureMoisVente).HasColumnName("cloture_mois_vente").HasMaxLength(30);
            e.Property(x => x.ClotureMoisAchat).HasColumnName("cloture_mois_achat").HasMaxLength(30);
            e.Property(x => x.GenerationDateFacture).HasColumnName("generation_date_facture").HasMaxLength(50);
            e.Property(x => x.PeriodeFacturation).HasColumnName("PERIODE_FACTURATION");
            e.Property(x => x.PeriodeAchat).HasColumnName("periode_achat");
            e.Property(x => x.TauxRasIs).HasColumnName("TAUX_RAS_IS");
            e.Property(x => x.TauxRasTva).HasColumnName("TAUX_RAS_TVA");
            e.Property(x => x.PlafondTtcRas).HasColumnName("PLAFOND_TTC_RAS");
            e.Property(x => x.TtcExcluRas).HasColumnName("TTC_EXCLU_RAS");
            e.Property(x => x.NbJrFranchise).HasColumnName("nb_jr_franchise");
            e.Property(x => x.NbAgence).HasColumnName("nb_agence");
            e.Property(x => x.NbJrClientActif).HasColumnName("NB_JR_CLIENT_ACTIF");
            e.Property(x => x.CoutSociete).HasColumnName("cout_societe").HasPrecision(18, 4);
            e.Property(x => x.CoutMead).HasColumnName("cout_mead").HasPrecision(18, 4);
            e.Property(x => x.CoutAssuranceMarchandise).HasColumnName("cout_assurance_marchandise").HasPrecision(18, 4);
            e.Property(x => x.DepartementSoc).HasColumnName("departement_soc").HasMaxLength(60);
            e.Property(x => x.RegionSoc).HasColumnName("region_soc").HasMaxLength(60);
            e.Property(x => x.Langue).HasColumnName("langue").HasMaxLength(20);
        });

        b.Entity<Agence>(e =>
        {
            e.ToTable("t_agence");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_agence").HasMaxLength(10);
            e.Property(x => x.SocieteKey).HasColumnName("soc_agence").HasMaxLength(10).IsRequired();
            e.Property(x => x.CodeAgence).HasColumnName("code_agence").HasMaxLength(20);
            e.Property(x => x.IntituleAgence).HasColumnName("intitule_agence").HasMaxLength(120);
            e.Property(x => x.AgenceAbrege).HasColumnName("agence_abrege").HasMaxLength(30);
            e.Property(x => x.Adresse1Agence).HasColumnName("adresse1_agence").HasMaxLength(120);
            e.Property(x => x.Adresse2Agence).HasColumnName("adresse2_agence").HasMaxLength(120);
            e.Property(x => x.VilleAgence).HasColumnName("ville_agence").HasMaxLength(60);
            e.Property(x => x.PaysAgence).HasColumnName("pays_agence").HasMaxLength(3);
            e.Property(x => x.CpAgence).HasColumnName("cp_agence").HasMaxLength(15);
            e.Property(x => x.TelAgence).HasColumnName("tel_agence").HasMaxLength(30);
            e.Property(x => x.FaxAgence).HasColumnName("fax_agence").HasMaxLength(30);
            e.Property(x => x.EmailAgence).HasColumnName("email_agence").HasMaxLength(120);
            e.Property(x => x.JournalVenteAg).HasColumnName("journal_vente_ag").HasMaxLength(10);
            e.Property(x => x.JournalAchatAg).HasColumnName("journal_achat_ag").HasMaxLength(10);
            e.Property(x => x.JournalDeboursAg).HasColumnName("journal_debours_ag").HasMaxLength(10);
            e.Property(x => x.LieuDedouanement).HasColumnName("lieu_dedouanement").HasMaxLength(60);
            e.Property(x => x.ScanMead).HasColumnName("scan_MEAD");
            e.Property(x => x.ImprimanteEtiquette).HasColumnName("imprimante_etiquette").HasMaxLength(60);
            e.Property(x => x.CoutAgence).HasColumnName("cout_agence").HasPrecision(18, 4);
            // The legacy soft-delete column on this table is `supp`, not `supprimer`.
            e.Property(x => x.Supprime).HasColumnName("supp");
            e.HasIndex(x => new { x.SocieteKey, x.CodeAgence });
            e.HasQueryFilter(x => !x.Supprime);
        });

        b.Entity<Pays>(e =>
        {
            e.ToTable("T_PAYS");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("CODE_PAYS").HasMaxLength(3);
            e.Property(x => x.IntitulePays).HasColumnName("INTITULE_PAYS").HasMaxLength(60);
            e.Property(x => x.Alpha2).HasColumnName("ALPHA2").HasMaxLength(2);
            e.Property(x => x.Accord).HasColumnName("ACCORD").HasMaxLength(30);
            e.Property(x => x.ZonePaysKey).HasColumnName("ZONE_PAYS").HasMaxLength(30);
            e.HasIndex(x => x.Alpha2).IsUnique().HasFilter("[ALPHA2] IS NOT NULL");
        });

        b.Entity<ZonePays>(e =>
        {
            e.ToTable("T_ZONE_PAYS");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("KEY_ZONE").HasMaxLength(30);
        });

        b.Entity<Ville>(e =>
        {
            e.ToTable("T_VILLE");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("KEY_VILLE").HasMaxLength(10);
            e.Property(x => x.IntituleVille).HasColumnName("INTITULE_VILLE").HasMaxLength(60).IsRequired();
            e.Property(x => x.PaysVille).HasColumnName("PAYS_VILLE").HasMaxLength(3);
        });

        b.Entity<Devise>(e =>
        {
            e.ToTable("t_devise");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("code_devise").HasMaxLength(5);
            e.Property(x => x.LibelleDevise).HasColumnName("libelle_devise").HasMaxLength(50);
            e.Property(x => x.LibelleFacture).HasColumnName("libelle_facture").HasMaxLength(50);
            e.Property(x => x.LibelleCent).HasColumnName("libelle_cent").HasMaxLength(50);
            e.Property(x => x.NbDecimal).HasColumnName("nb_decimal").HasDefaultValue(2);
            // The EDI code an inbound file carries for this currency (e.g. "00" meaning EUR).
            e.Property(x => x.DeviseEdi).HasColumnName("devise_edi").HasMaxLength(10);
            e.HasIndex(x => x.DeviseEdi);
        });

        b.Entity<Port>(e =>
        {
            e.ToTable("t_port");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("code_port").HasMaxLength(10);
            e.Property(x => x.LibellePort).HasColumnName("libelle_port").HasMaxLength(60);
            e.Property(x => x.VillePortKey).HasColumnName("ville_port").HasMaxLength(10);
        });

        b.Entity<Incoterm>(e =>
        {
            e.ToTable("t_incoterm");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("INCOTERM").HasMaxLength(10);
            e.Property(x => x.LibelleIncoterm).HasColumnName("LIBELLE_INCOTERM").HasMaxLength(60);
            e.Property(x => x.Supprime).HasColumnName("supp_incoterm");
            e.Property(x => x.FactureAssigneImport).HasColumnName("facture_assigne_import");
            e.Property(x => x.FactureAssigneExport).HasColumnName("facture_assigne_export");
            e.Property(x => x.Livraison).HasColumnName("livraison");
            // The EDI code an inbound file carries for this incoterm — not the incoterm code itself.
            e.Property(x => x.IncotermEdi).HasColumnName("INCOTERM_edi").HasMaxLength(10);
            e.HasIndex(x => x.IncotermEdi);
        });

        b.Entity<TypeDossier>(e =>
        {
            e.ToTable("t_type_dossier");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("type_dossier").HasMaxLength(10);
            e.Property(x => x.IntituleType).HasColumnName("intitule_type").HasMaxLength(100);
            // Legacy stores the French label ("Import"/"Export"/"National") in a free-text column.
            e.Property(x => x.Sens).HasColumnName("SENS").HasMaxLength(20).HasConversion(LegacyConverters.SensDossierConverter);
            e.Property(x => x.NatureService).HasColumnName("nature_service").HasMaxLength(100);
            e.Property(x => x.Produit).HasColumnName("produit").HasMaxLength(50);
            // Drives which party becomes a booking's key_client (EXPEDITEUR/DESTINATAIRE/PAYEUR).
            e.Property(x => x.ClientDossier).HasColumnName("client_dossier").HasMaxLength(20);
            e.Property(x => x.SocTypeDossier).HasColumnName("soc_type_dossier").HasMaxLength(10);
        });

        b.Entity<Cod>(e =>
        {
            // Key is the free-text label; no tenant column evidenced.
            e.ToTable("t_cod");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("intitule_cod").HasMaxLength(50);
            // The EDI code an inbound file carries; "10" on the file side means "no COD".
            e.Property(x => x.CodEdi).HasColumnName("cod_edi").HasMaxLength(10);
        });

        b.Entity<RapportPoidsVolume>(e =>
        {
            // Tenant column is soc_rapport — a third distinct spelling in this area of the schema.
            e.ToTable("t_rapport_poids_volume");
            e.HasKey(x => new { x.SocieteKey, x.Produit });
            e.Property(x => x.SocieteKey).HasColumnName("soc_rapport").HasMaxLength(10);
            e.Property(x => x.Produit).HasColumnName("produit").HasMaxLength(30);
            e.Property(x => x.VolumeKgs).HasColumnName("volume_kgs");
            e.Property(x => x.MplKgs).HasColumnName("mpl_kgs");
            e.Property(x => x.MplVolume).HasColumnName("mpl_volume");
            e.Ignore(x => x.Id);
        });

        b.Entity<ParametreSociete>(e =>
        {
            // KEY/VALUE: one row per (company, setting). No soft-delete column exists, so no filter.
            e.ToTable("t_parametre_societe");
            e.HasKey(x => new { x.SocieteKey, x.KeyParametre });
            // Tenant column is key_soc — not `societe`, as neighbouring reference tables use.
            e.Property(x => x.SocieteKey).HasColumnName("key_soc").HasMaxLength(10);
            // Mixed-case codes matched as literals; the pair is the identity.
            e.Property(x => x.KeyParametre).HasColumnName("key_parametre").HasMaxLength(50);
            e.Property(x => x.LibelleParametre).HasColumnName("libelle_parametre").HasMaxLength(100);
            // Everything is text: booleans, numeric strings, and SQL predicate fragments alike.
            e.Property(x => x.ValeurParametre).HasColumnName("valeur_parametre").HasMaxLength(1000);
            e.Ignore(x => x.Id);
            e.Ignore(x => x.EstVide);
        });

        b.Entity<ModePaiement>(e =>
        {
            e.ToTable("t_mode_paiement");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("mode_paiement").HasMaxLength(50);
            e.Property(x => x.StatutClient).HasColumnName("statut_client").HasMaxLength(50);
            e.Property(x => x.StatutFrs).HasColumnName("statut_frs").HasMaxLength(50);
            e.Property(x => x.StatutAgent).HasColumnName("statut_agent").HasMaxLength(50);
            e.Property(x => x.CorrespondPaiement).HasColumnName("correspond_paiement").HasMaxLength(50);
            e.Property(x => x.CorrespondanceSominfac).HasColumnName("correspondance_sominfac").HasMaxLength(50);
            e.Ignore(x => x.IsImmediate);
            e.Ignore(x => x.UsesEcheance);
        });

        b.Entity<CompteTva>(e =>
        {
            e.ToTable("t_compte_tva");
            // Legacy logical PK is the pair (code_tva, tva_soc).
            e.HasKey(x => new { x.Id, x.SocieteKey });
            e.Property(x => x.Id).HasColumnName("code_tva").HasMaxLength(20);
            e.Property(x => x.SocieteKey).HasColumnName("tva_soc").HasMaxLength(10);
            e.Property(x => x.Valeur).HasColumnName("valeur").HasPrecision(9, 4);
            e.Property(x => x.CompteVente).HasColumnName("compte_vente").HasMaxLength(20);
            e.Property(x => x.CompteAchat).HasColumnName("compte_achat").HasMaxLength(20);
            e.Property(x => x.CodeTaxe).HasColumnName("CODE_TAXE").HasMaxLength(20);
            e.Property(x => x.Vente).HasColumnName("vente");
            e.Ignore(x => x.IsDebours);
        });

        b.Entity<NatureFrais>(e =>
        {
            e.ToTable("t_nature_frais");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("libelle_nature").HasMaxLength(100);
            e.Property(x => x.SocieteKey).HasColumnName("societe").HasMaxLength(10).IsRequired();
        });

        b.Entity<ClasseClient>(e =>
        {
            e.ToTable("t_classe_client");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("classe_client").HasMaxLength(50);
            e.Property(x => x.SocieteKey).HasColumnName("societe").HasMaxLength(10).IsRequired();
        });

        b.Entity<StatutClient>(e =>
        {
            e.ToTable("t_statut_client");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("statut_client").HasMaxLength(50);
        });

        b.Entity<Risque>(e =>
        {
            e.ToTable("t_risque");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("intitule_risque").HasMaxLength(50);
        });

        b.Entity<SecteurActivite>(e =>
        {
            e.ToTable("t_secteur_activite");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_secteur_activite").HasMaxLength(100);
            e.Property(x => x.SocieteKey).HasColumnName("soc_secteur").HasMaxLength(10).IsRequired();
        });

        b.Entity<GroupeSociete>(e =>
        {
            e.ToTable("t_groupe_societe");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("libelle_groupe").HasMaxLength(100);
            e.Property(x => x.SocieteKey).HasColumnName("soc_groupe").HasMaxLength(10).IsRequired();
        });

        b.Entity<Tresorie>(e =>
        {
            e.ToTable("t_tresorie");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_tresorie").HasMaxLength(30);
            e.Property(x => x.SocieteKey).HasColumnName("soc_tresorie").HasMaxLength(10).IsRequired();
            e.Property(x => x.AgenceTresorie).HasColumnName("agence_tresorie").HasMaxLength(10);
            e.Property(x => x.LibelleTresorie).HasColumnName("libelle_tresorie").HasMaxLength(100);
            e.Property(x => x.TypeTresorie).HasColumnName("type_tresorie").HasMaxLength(1)
                .HasConversion(LegacyConverters.TypeTresorie);
            e.Property(x => x.AdresseTresorie).HasColumnName("adresse_tresorie").HasMaxLength(150);
            e.Property(x => x.CompteBanqueTresorie).HasColumnName("compte_banque_tresorie").HasMaxLength(50);
            e.Property(x => x.JournalComptabilite).HasColumnName("journal_comptabilite").HasMaxLength(10);
            e.Property(x => x.GenerationRemise).HasColumnName("generation_remise");
            e.Property(x => x.NumCompteComptable).HasColumnName("num_compte_comptable").HasMaxLength(20);
            e.Property(x => x.IfTresorie).HasColumnName("if_tresorie").HasMaxLength(30);
            e.Property(x => x.IceTresorie).HasColumnName("ice_tresorie").HasMaxLength(30);
        });

        b.Entity<NatureFraisBancaire>(e =>
        {
            e.ToTable("T_NATURE_FRAIS_BANCAIRE");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("nature_frais").HasMaxLength(100);
            e.Property(x => x.SocieteKey).HasColumnName("societe").HasMaxLength(10).IsRequired();
            e.Property(x => x.TauxTva).HasColumnName("taux_tva").HasPrecision(9, 4);
            e.Property(x => x.CompteComptable).HasColumnName("compte_comptable").HasMaxLength(20);
        });

        b.Entity<TypeIncidentPaiement>(e =>
        {
            e.ToTable("T_TYPE_INCIDENT_PAIEMENT");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("LIBELLE_INCIDENT").HasMaxLength(100);
            e.Property(x => x.SocieteKey).HasColumnName("soc_type_incident").HasMaxLength(10).IsRequired();
        });
    }

    private static void ConfigureClients(ModelBuilder b)
    {
        b.Entity<Client>(e =>
        {
            e.ToTable("t_clients");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("KEY_CLIENT").HasMaxLength(20);
            e.Property(x => x.SocieteKey).HasColumnName("SOC_CLIENT").HasMaxLength(5).IsRequired();
            e.Property(x => x.RaisonSociale).HasColumnName("RAISON_SOCIALE").HasMaxLength(150).IsRequired();
            e.Property(x => x.RefClient).HasColumnName("REF_CLIENT").HasMaxLength(50);
            e.Property(x => x.TypeClient).HasColumnName("TYPE_CLIENT").HasMaxLength(50);
            e.Property(x => x.StatutClient).HasColumnName("STATUT_CLIENT").HasMaxLength(50);
            e.Property(x => x.GroupeClient).HasColumnName("GROUPE_CLIENT").HasMaxLength(50);
            e.Property(x => x.ClasseClient).HasColumnName("CLASSE_CLIENT").HasMaxLength(50);
            e.Property(x => x.RisqueClient).HasColumnName("RISQUE_CLIENT").HasMaxLength(50);
            e.Property(x => x.ActiviteClient).HasColumnName("ACTIVITE_CLIENT").HasMaxLength(100);
            e.Property(x => x.DetailActivite).HasColumnName("DETAIL_ACTIVITE").HasMaxLength(150);
            e.Property(x => x.Commercial).HasColumnName("COMMERCIAL").HasMaxLength(20);
            e.Property(x => x.AgenceCommercial).HasColumnName("AGENCE_COMMERCIAL").HasMaxLength(20);
            e.Property(x => x.AgenceExploitation).HasColumnName("AGENCE_EXPLOITATION").HasMaxLength(20);
            e.Property(x => x.TypeAffectCommercial).HasColumnName("TYPE_AFFECT_COMMERCIAL").HasMaxLength(50);
            e.Property(x => x.KeyChargeClientele).HasColumnName("KEY_CHARGE_CLIENTELE").HasMaxLength(20);
            e.Property(x => x.AgentRecouvrement).HasColumnName("AGENT_RECOUVREMENT").HasMaxLength(20);
            e.Property(x => x.Adresse1Client).HasColumnName("ADRESSE1_CLIENT").HasMaxLength(150);
            e.Property(x => x.Adresse2Client).HasColumnName("ADRESSE2_CLIENT").HasMaxLength(150);
            e.Property(x => x.CpClient).HasColumnName("CP_CLIENT").HasMaxLength(10);
            e.Property(x => x.VilleClient).HasColumnName("VILLE_CLIENT").HasMaxLength(100);
            e.Property(x => x.PaysClient).HasColumnName("PAYS_CLIENT").HasMaxLength(10);
            e.Property(x => x.DepartementClient).HasColumnName("DEPARTEMENT_CLIENT").HasMaxLength(100);
            e.Property(x => x.RegionClient).HasColumnName("REGION_CLIENT").HasMaxLength(100);
            e.Property(x => x.LangueClient).HasColumnName("LANGUE_CLIENT").HasMaxLength(10);
            e.Property(x => x.TelClient).HasColumnName("TEL_CLIENT").HasMaxLength(30);
            e.Property(x => x.FaxClient).HasColumnName("FAX_CLIENT").HasMaxLength(30);
            e.Property(x => x.SiteWebClient).HasColumnName("SITE_WEB_CLIENT").HasMaxLength(100);
            e.Property(x => x.AnneeCreation).HasColumnName("ANNEE_CREATION");
            e.Property(x => x.ObservationClient).HasColumnName("OBSERVATION_CLIENT");
            e.Property(x => x.RcClient).HasColumnName("RC_CLIENT").HasMaxLength(30);
            e.Property(x => x.CentreRcClient).HasColumnName("CENTRE_RC_CLIENT").HasMaxLength(30);
            e.Property(x => x.PatenteClient).HasColumnName("PATENTE_CLIENT").HasMaxLength(30);
            e.Property(x => x.IfClient).HasColumnName("IF_CLIENT").HasMaxLength(30);
            e.Property(x => x.CnssClient).HasColumnName("CNSS_CLIENT").HasMaxLength(30);
            e.Property(x => x.IceClient).HasColumnName("ICE_CLIENT").HasMaxLength(30);
            e.Property(x => x.DeviseFacturation).HasColumnName("DEVISE_FACTURATION").HasMaxLength(5);
            e.Property(x => x.PaiementClient).HasColumnName("PAIEMENT_CLIENT").HasMaxLength(50);
            e.Property(x => x.Echeance).HasColumnName("ECHEANCE").HasMaxLength(20);
            e.Property(x => x.NbEcheance).HasColumnName("NB_ECHEANCE");
            e.Property(x => x.FinMois).HasColumnName("FIN_MOIS");
            e.Property(x => x.Plafond).HasColumnName("PLAFOND").HasPrecision(18, 2);
            e.Property(x => x.CompteComptable).HasColumnName("COMPTE_COMPTABLE").HasMaxLength(30);
            e.Property(x => x.JournalVente).HasColumnName("JOURNAL_VENTE").HasMaxLength(10);
            e.Property(x => x.JournalDebours).HasColumnName("JOURNAL_DEBOURS").HasMaxLength(10);
            e.Property(x => x.ReleveFacture).HasColumnName("RELEVE_FACTURE").HasMaxLength(1)
                .HasConversion(LegacyConverters.PeriodiciteReleve);
            e.Property(x => x.NbFactureReleve).HasColumnName("NB_FACTURE_RELEVE");
            e.Property(x => x.Ras).HasColumnName("RAS");
            e.Property(x => x.Valide).HasColumnName("VALIDE");
            e.Property(x => x.Actif).HasColumnName("ACTIF");
            e.Property(x => x.Attachement).HasColumnName("ATTACHEMENT");
            e.Property(x => x.Supprime).HasColumnName("SUPPRIMER");
            e.Property(x => x.ServiceAcquis).HasColumnName("SERVICE_ACQUIS").HasMaxLength(200);
            e.Property(x => x.LieuDedouanement).HasColumnName("LIEU_DEDOUANEMENT").HasMaxLength(100);
            e.Property(x => x.NbJrFranchise).HasColumnName("NB_JR_FRANCHISE");
            e.Property(x => x.ImmobilisationMead).HasColumnName("IMMOBILISATION_MEAD").HasPrecision(18, 2);
            e.Property(x => x.ImmobilisationPort).HasColumnName("IMMOBILISATION_PORT").HasPrecision(18, 2);
            e.Property(x => x.SuiviAt).HasColumnName("SUIVI_AT");
            e.Property(x => x.DateCreation).HasColumnName("DATE_CREATION");
            e.Property(x => x.CodeLettrage).HasColumnName("CODE_LETTRAGE").HasMaxLength(20);
            e.Ignore(x => x.IsForeign);

            e.HasQueryFilter(x => !x.Supprime);

            e.HasIndex(x => new { x.SocieteKey, x.RaisonSociale });
            e.HasIndex(x => x.CompteComptable).IsUnique().HasFilter("[COMPTE_COMPTABLE] IS NOT NULL");

            // Children are reached through the aggregate; the contacts table is polymorphic and is
            // therefore queried directly rather than through a navigation.
            e.HasMany(x => x.Adresses).WithOne().HasForeignKey(a => a.KeyClient).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Banques).WithOne().HasForeignKey(x => x.KeyTier).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AdresseClient>(e =>
        {
            e.ToTable("t_adresse_client");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("KEY_ADRESSE").HasMaxLength(20);
            e.Property(x => x.KeyClient).HasColumnName("KEY_CLIENT").HasMaxLength(20).IsRequired();
            e.Property(x => x.Adresse1).HasColumnName("ADRESSE1").HasMaxLength(150);
            e.Property(x => x.Adresse2).HasColumnName("ADRESSE2").HasMaxLength(150);
            e.Property(x => x.Cp).HasColumnName("CP").HasMaxLength(10);
            e.Property(x => x.Ville).HasColumnName("VILLE").HasMaxLength(100).IsRequired();
            e.Property(x => x.Pays).HasColumnName("PAYS").HasMaxLength(10);
            e.Property(x => x.TelAdresse).HasColumnName("TEL_ADRESSE").HasMaxLength(30);
            e.Property(x => x.FaxAdresse).HasColumnName("FAX_ADRESSE").HasMaxLength(30);
            e.Property(x => x.Contact).HasColumnName("CONTACT").HasMaxLength(100);
            e.Property(x => x.Type).HasColumnName("TYPE").HasMaxLength(50);
            e.Ignore(x => x.IsPrincipal);
        });

        b.Entity<TiersContact>(e =>
        {
            e.ToTable("t_contact_tiers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("KEY_CONTACT").HasMaxLength(20);
            e.Property(x => x.TiersContactKey).HasColumnName("TIERS_CONTACT").HasMaxLength(20).IsRequired();
            e.Property(x => x.TypeTiers).HasColumnName("TYPE_TIERS_CONTACT").HasMaxLength(1)
                .HasConversion(LegacyConverters.TypeTiers);
            e.Property(x => x.NomPrenom).HasColumnName("NOM_PRENOM").HasMaxLength(100).IsRequired();
            e.Property(x => x.KeyFonctionContact).HasColumnName("KEY_FONCTION_CONTACT").HasMaxLength(20);
            e.Property(x => x.EmailContact).HasColumnName("EMAIL_CONTACT").HasMaxLength(100);
            e.Property(x => x.TelContact).HasColumnName("TEL_CONTACT").HasMaxLength(30);
            e.Property(x => x.FaxContact).HasColumnName("FAX_CONTACT").HasMaxLength(30);
            e.Property(x => x.GsmContact).HasColumnName("GSM_CONTACT").HasMaxLength(30);
            e.Property(x => x.CompteActive).HasColumnName("COMPTE_ACTIVE");
            e.Property(x => x.MotPasseTrack).HasColumnName("MOT_PASSE_TRACK").HasMaxLength(100);
            e.HasIndex(x => new { x.TypeTiers, x.TiersContactKey });
        });

        b.Entity<BanqueTiers>(e =>
        {
            e.ToTable("t_banque_tiers");
            // Legacy composite key: (key_banque, key_tier).
            e.HasKey(x => new { x.Id, x.KeyTier });
            e.Property(x => x.Id).HasColumnName("KEY_BANQUE").HasMaxLength(20);
            e.Property(x => x.KeyTier).HasColumnName("KEY_TIER").HasMaxLength(20);
            e.Property(x => x.TypeTier).HasColumnName("TYPE_TIER").HasMaxLength(1)
                .HasConversion(LegacyConverters.TypeTiers);
            e.Property(x => x.Banque).HasColumnName("BANQUE").HasMaxLength(100);
            e.Property(x => x.CompteRib).HasColumnName("COMPTE_RIB").HasMaxLength(50);
            e.Property(x => x.CodeBanque).HasColumnName("CODE_BANQUE").HasMaxLength(30);
            e.Property(x => x.AdresseBanque).HasColumnName("ADRESSE_BANQUE").HasMaxLength(100);
            e.Property(x => x.Swift).HasColumnName("SWIFT").HasMaxLength(30);
            e.Property(x => x.IbanCode).HasColumnName("IBAN_CODE").HasMaxLength(50);
            e.HasIndex(x => x.CompteRib).IsUnique().HasFilter("[COMPTE_RIB] IS NOT NULL");
        });
    }

    private static void ConfigurePayments(ModelBuilder b)
    {
        b.Entity<EnteteReglement>(e =>
        {
            e.ToTable("t_entete_reglement");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_reglement").HasMaxLength(30);
            e.Property(x => x.SocieteKey).HasColumnName("soc_reglement").HasMaxLength(10).IsRequired();
            e.Property(x => x.AgenceReglement).HasColumnName("agence_reglement").HasMaxLength(10);
            e.Property(x => x.KeyTiers).HasColumnName("key_tiers").HasMaxLength(30);
            e.Property(x => x.TypeReglement).HasColumnName("type_reglement").HasMaxLength(20)
                .HasConversion(LegacyConverters.TypeReglement);
            e.Property(x => x.NatureReglement).HasColumnName("nature_reglement").HasMaxLength(20)
                .HasConversion(LegacyConverters.NatureReglement);
            e.Property(x => x.ModePaiement).HasColumnName("mode_paiement").HasMaxLength(50);
            e.Property(x => x.ModeChange).HasColumnName("mode_change").HasMaxLength(50);
            e.Property(x => x.Devise).HasColumnName("devise").HasMaxLength(5).IsRequired();
            e.Property(x => x.TauxDevise).HasColumnName("taux_devise").HasPrecision(18, 6);
            e.Property(x => x.MontantDevise).HasColumnName("montant_devise").HasPrecision(18, 3);
            e.Property(x => x.MontantMad).HasColumnName("montant_mad").HasPrecision(18, 3);
            e.Property(x => x.MontantLettre).HasColumnName("montant_lettre").HasMaxLength(500);
            e.Property(x => x.MontantImputeAvance).HasColumnName("montant_impute_avance").HasPrecision(18, 3);
            e.Property(x => x.DateSaisie).HasColumnName("date_saisie");
            e.Property(x => x.DateOperation).HasColumnName("date_operation");
            e.Property(x => x.DateEcheance).HasColumnName("date_echeance");
            e.Property(x => x.DateSignature).HasColumnName("date_signature");
            e.Property(x => x.DateRemiseBanque).HasColumnName("date_remise_banque");
            e.Property(x => x.DateValidationBanque).HasColumnName("date_validation_banque");
            e.Property(x => x.DateOd).HasColumnName("date_od");
            e.Property(x => x.ReferencePaiement).HasColumnName("reference_paiement").HasMaxLength(50);
            e.Property(x => x.RefCompta).HasColumnName("ref_compta").HasMaxLength(30);
            e.Property(x => x.Facture).HasColumnName("facture").HasMaxLength(500);
            e.Property(x => x.Dossier).HasColumnName("dossier").HasMaxLength(30);
            e.Property(x => x.NomTier).HasColumnName("nom_tier").HasMaxLength(150);
            e.Property(x => x.Tireur).HasColumnName("tireur").HasMaxLength(100);
            e.Property(x => x.VilleTireur).HasColumnName("ville_tireur").HasMaxLength(100);
            e.Property(x => x.BanqueTireur).HasColumnName("banque_tireur").HasMaxLength(100);
            e.Property(x => x.BanqueTier).HasColumnName("banque_tier").HasMaxLength(100);
            e.Property(x => x.CompteBanqueTier).HasColumnName("compte_banque_tier").HasMaxLength(50);
            e.Property(x => x.AdresseBanqueTier).HasColumnName("adresse_banque_tier").HasMaxLength(150);
            e.Property(x => x.CodeBanqueTier).HasColumnName("code_banque_tier").HasMaxLength(30);
            e.Property(x => x.SwiftCodeTier).HasColumnName("swift_code_tier").HasMaxLength(30);
            e.Property(x => x.IbanCode).HasColumnName("iban_code").HasMaxLength(50);
            e.Property(x => x.AdresseTiers).HasColumnName("adresse_tiers").HasMaxLength(150);
            e.Property(x => x.AgenceBanqueSoc).HasColumnName("agence_banque_soc").HasMaxLength(100);
            e.Property(x => x.CompteBancaireSoc).HasColumnName("compte_bancaire_soc").HasMaxLength(50);
            e.Property(x => x.KeyTresorie).HasColumnName("key_tresorie").HasMaxLength(30);
            e.Property(x => x.Statut).HasColumnName("statut").HasMaxLength(30)
                .HasConversion(LegacyConverters.StatutReglement);
            e.Property(x => x.MotifImpaye).HasColumnName("motif_impaye").HasMaxLength(100);
            e.Property(x => x.CommentaireImpaye).HasColumnName("commentaire_impaye").HasMaxLength(500);
            e.Property(x => x.Observation).HasColumnName("observation").HasMaxLength(500);
            e.Property(x => x.AgRecouvrementSys).HasColumnName("ag_recouvrement_sys").HasMaxLength(20);
            e.Property(x => x.AgRecouvrementSaisie).HasColumnName("ag_recouvrement_saisie").HasMaxLength(20);
            // NOTE: the legacy column really is spelled with a single 'm'.
            e.Property(x => x.ComissionBanque).HasColumnName("comission_banque").HasPrecision(18, 3);
            e.Property(x => x.FraisBancaire).HasColumnName("frais_bancaire").HasPrecision(18, 3);
            e.Property(x => x.Tva).HasColumnName("tva").HasPrecision(18, 3);
            e.Property(x => x.EcartReglement).HasColumnName("ecart_reglement").HasPrecision(18, 3);
            e.Property(x => x.EcartFactVente).HasColumnName("ecart_fact_vente").HasPrecision(18, 3);
            e.Property(x => x.EcartFactAchat).HasColumnName("ecart_fact_achat").HasPrecision(18, 3);
            e.Property(x => x.EcartConversion).HasColumnName("ecart_conversion").HasPrecision(18, 3);
            e.Property(x => x.MtFactCltMad).HasColumnName("mt_fact_clt_mad").HasPrecision(18, 3);
            e.Property(x => x.MtFactCltBanque).HasColumnName("mt_fact_clt_banque").HasPrecision(18, 3);
            e.Property(x => x.MtFactFrsMad).HasColumnName("mt_fact_frs_mad").HasPrecision(18, 3);
            e.Property(x => x.MtFactFrsBanque).HasColumnName("mt_fact_frs_banque").HasPrecision(18, 3);
            e.Property(x => x.ValidationLettrage).HasColumnName("validation_lettrage");
            e.Property(x => x.TypeLettrage).HasColumnName("type_lettrage").HasMaxLength(1)
                .HasConversion(LegacyConverters.TypeLettrage);
            e.Property(x => x.CodeLettrage).HasColumnName("code_lettrage").HasMaxLength(20);
            e.Property(x => x.StatutDeclarationTva).HasColumnName("statut_declaration_tva").HasMaxLength(30)
                .HasConversion(LegacyConverters.StatutDeclarationTva);
            e.Property(x => x.MoisDeclaration).HasColumnName("mois_declaration").HasMaxLength(10);
            e.Property(x => x.KeyRemiseSys).HasColumnName("key_remise_sys").HasMaxLength(30);
            e.Property(x => x.KeyDemandeReg).HasColumnName("key_demande_reg").HasMaxLength(30);
            e.Property(x => x.Attachement).HasColumnName("attachement");
            e.Property(x => x.Integre).HasColumnName("integre");
            e.Property(x => x.IntegreImp).HasColumnName("integre_imp");
            e.Property(x => x.Supprime).HasColumnName("supprimer");

            e.Ignore(x => x.SensFinancier);
            e.Ignore(x => x.IsLettrableAvance);
            e.Ignore(x => x.ResteAvance);

            e.HasQueryFilter(x => !x.Supprime);

            e.HasIndex(x => new { x.SocieteKey, x.Statut });
            e.HasIndex(x => x.KeyTiers);
            e.HasIndex(x => x.KeyRemiseSys);

            e.HasMany(x => x.Allocations).WithOne().HasForeignKey(a => a.KeyReglement).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DetailReglement>(e =>
        {
            e.ToTable("t_detail_reglement");
            // Legacy composite key: (key_reglement, key_facture).
            e.HasKey(x => new { x.KeyReglement, x.KeyFacture });
            e.Property(x => x.KeyReglement).HasColumnName("key_reglement").HasMaxLength(30);
            e.Property(x => x.KeyFacture).HasColumnName("key_facture").HasMaxLength(30);
            e.Property(x => x.Montant).HasColumnName("montant").HasPrecision(18, 3);
            e.Property(x => x.Devise).HasColumnName("devise").HasMaxLength(5);
            e.Property(x => x.Sens).HasColumnName("sens").HasMaxLength(1)
                .HasConversion(LegacyConverters.SensReglement);
            e.Property(x => x.RasIs).HasColumnName("ras_is").HasPrecision(18, 3);
            e.Property(x => x.RasTva).HasColumnName("ras_tva").HasPrecision(18, 3);
            e.Ignore(x => x.Id);
        });

        b.Entity<FraisBancaire>(e =>
        {
            e.ToTable("T_FRAIS_BANCAIRES");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_frais_bancaire").HasMaxLength(30);
            e.Property(x => x.SocieteKey).HasColumnName("soc_frais_bancaire").HasMaxLength(10).IsRequired();
            e.Property(x => x.KeyTresorie).HasColumnName("key_tresorie").HasMaxLength(30);
            e.Property(x => x.DateFraisBancaire).HasColumnName("date_frais_bancaire");
            e.Property(x => x.NatureFrais).HasColumnName("nature_frais").HasMaxLength(100);
            e.Property(x => x.MtHtDevise).HasColumnName("mt_ht_devise").HasPrecision(18, 3);
            e.Property(x => x.MtTvaDevise).HasColumnName("mt_tva_devise").HasPrecision(18, 3);
            e.Property(x => x.Devise).HasColumnName("devise").HasMaxLength(5);
            e.Property(x => x.TauxChange).HasColumnName("taux_change").HasPrecision(18, 6);
            e.Property(x => x.KeyReglement).HasColumnName("key_reglement").HasMaxLength(30);
            e.Property(x => x.StatutDeclaration).HasColumnName("statut_declaration").HasMaxLength(30)
                .HasConversion(LegacyConverters.StatutDeclarationTva);
            e.Property(x => x.MoisDeclaration).HasColumnName("mois_declaration").HasMaxLength(10);
            e.Property(x => x.Supprime).HasColumnName("supprimer");
            e.Property(x => x.Integre).HasColumnName("integre");
            e.Ignore(x => x.MontantMad);
            e.Ignore(x => x.IsAccountable);
            e.HasQueryFilter(x => !x.Supprime);
        });

        b.Entity<EnteteRemise>(e =>
        {
            e.ToTable("t_entete_remise");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_remise").HasMaxLength(30);
            e.Property(x => x.SocieteKey).HasColumnName("soc_remise").HasMaxLength(10).IsRequired();
            e.Property(x => x.RefRemise).HasColumnName("ref_remise").HasMaxLength(50);
            e.Property(x => x.TypeRemise).HasColumnName("type_remise").HasMaxLength(50);
            e.Property(x => x.KeyTresorie).HasColumnName("key_tresorie").HasMaxLength(30);
            e.Property(x => x.DateRemise).HasColumnName("date_remise");
        });

        b.Entity<TauxChange>(e =>
        {
            e.ToTable("t_taux_change");
            // Legacy natural key: (societe, devise, date_taux).
            e.HasKey(x => new { x.SocieteKey, x.Devise, x.DateTaux });
            e.Property(x => x.SocieteKey).HasColumnName("societe").HasMaxLength(10);
            e.Property(x => x.Devise).HasColumnName("devise").HasMaxLength(5);
            e.Property(x => x.DateTaux).HasColumnName("date_taux").HasColumnType("date");
            e.Property(x => x.TauxVente).HasColumnName("taux_vente").HasPrecision(18, 6);
            e.Property(x => x.TauxAchat).HasColumnName("taux_achat").HasPrecision(18, 6);
            e.Property(x => x.TauxChangeRate).HasColumnName("taux_change").HasPrecision(18, 6);
            e.Ignore(x => x.Id);
        });
    }

    private static void ConfigureInvoicing(ModelBuilder b)
    {
        b.Entity<EnteteFactureVente>(e =>
        {
            e.ToTable("T_ENTETE_FACTURE_VENTE");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("KEY_FACTURE").HasMaxLength(30);
            e.Property(x => x.SocieteKey).HasColumnName("SOC_FACT").HasMaxLength(10).IsRequired();
            e.Property(x => x.NumFacture).HasColumnName("NUM_FACTURE").HasMaxLength(30);
            e.Property(x => x.TypeFacture).HasColumnName("TYPE_FACTURE").HasMaxLength(1)
                .HasConversion(LegacyConverters.TypeFacture);
            e.Property(x => x.AgFact).HasColumnName("AG_FACT").HasMaxLength(10);
            e.Property(x => x.Langue).HasColumnName("LANGUE").HasMaxLength(5);
            e.Property(x => x.TypeOperation).HasColumnName("TYPE_OPERATION").HasMaxLength(50);
            e.Property(x => x.NatureService).HasColumnName("nature_service").HasMaxLength(100);
            e.Property(x => x.KeyDossier).HasColumnName("KEY_DOSSIER").HasMaxLength(30);
            e.Property(x => x.KeyVoyageInt).HasColumnName("KEY_VOYAGE_INT").HasMaxLength(30);
            e.Property(x => x.KeyClient).HasColumnName("KEY_CLIENT").HasMaxLength(30);
            e.Property(x => x.IntituleClient).HasColumnName("INTITULE_CLIENT").HasMaxLength(200);
            e.Property(x => x.MoisFacture).HasColumnName("MOIS_FACTURE").HasMaxLength(7);
            e.Property(x => x.DateFacture).HasColumnName("DATE_FACTURE");
            e.Property(x => x.DateEcheance).HasColumnName("DATE_ECHEANCE");
            e.Property(x => x.CommentaireFacture).HasColumnName("COMMENTAIRE_FACTURE").HasMaxLength(500);
            e.Property(x => x.CommentaireExoneration).HasColumnName("COMMENTAIRE_EXONERATION");
            e.Property(x => x.TtcMad).HasColumnName("TTC_MAD").HasPrecision(18, 3);
            e.Property(x => x.DeboursMad).HasColumnName("DEBOURS_MAD").HasPrecision(18, 3);
            e.Property(x => x.AssigneMad).HasColumnName("ASSIGNE_MAD").HasPrecision(18, 3);
            e.Property(x => x.HtMad).HasColumnName("HT_MAD").HasPrecision(18, 3);
            e.Property(x => x.TvaMad).HasColumnName("TVA_MAD").HasPrecision(18, 3);
            e.Property(x => x.TaxeRegionale).HasColumnName("TAXE_REGIONALE").HasPrecision(18, 3);
            e.Property(x => x.TtcDevise).HasColumnName("TTC_DEVISE").HasPrecision(18, 3);
            e.Property(x => x.DeviseFacture).HasColumnName("DEVISE_FACTURE").HasMaxLength(5).IsRequired();
            e.Property(x => x.TauxDeviseFacture).HasColumnName("TAUX_DEVISE_FACTURE").HasPrecision(18, 6);
            e.Property(x => x.TtcReg).HasColumnName("TTC_REG").HasPrecision(18, 3);
            e.Property(x => x.DeviseReg).HasColumnName("DEVISE_REG").HasMaxLength(5);
            e.Property(x => x.TauxDeviseReg).HasColumnName("TAUX_DEVISE_REG").HasPrecision(18, 6);
            e.Property(x => x.MontantLettreMad).HasColumnName("MONTANT_LETTRE_MAD").HasMaxLength(500);
            e.Property(x => x.MontantLettreDevise).HasColumnName("MONTANT_LETTRE_DEVISE").HasMaxLength(500);
            e.Property(x => x.RasIs).HasColumnName("RAS_IS").HasPrecision(18, 3);
            e.Property(x => x.RasTva).HasColumnName("RAS_TVA").HasPrecision(18, 3);
            e.Property(x => x.TauxRasIs).HasColumnName("TAUX_RAS_IS").HasPrecision(9, 4);
            e.Property(x => x.TauxRasTva).HasColumnName("TAUX_RAS_TVA").HasPrecision(9, 4);
            e.Property(x => x.NetAPayer).HasColumnName("NET_a_payer").HasPrecision(18, 3);
            e.Property(x => x.MtRegle).HasColumnName("mt_regle").HasPrecision(18, 3);
            e.Property(x => x.FactRegle).HasColumnName("fact_regle");
            e.Property(x => x.DateDernierReglement).HasColumnName("date_dernier_reglement");
            e.Property(x => x.NumFactureAvoir).HasColumnName("NUM_FACTURE_AVOIR").HasMaxLength(30);
            e.Property(x => x.KeyFactureAvoir).HasColumnName("KEY_FACTURE_AVOIR").HasMaxLength(30);
            e.Property(x => x.KeyAvoir).HasColumnName("key_avoir").HasMaxLength(30);
            e.Property(x => x.NumAvoir).HasColumnName("NUM_AVOIR").HasMaxLength(30);
            e.Property(x => x.KeyReleveFacture).HasColumnName("key_releve_facture").HasMaxLength(30);
            e.Property(x => x.Integre).HasColumnName("integre");
            e.Property(x => x.StatutFacture).HasColumnName("STATUT_FACTURE").HasMaxLength(50);
            e.Property(x => x.Attachement).HasColumnName("attachement");
            e.Property(x => x.EnvoiEmail).HasColumnName("envoi_email");
            e.Property(x => x.MagasinDouane).HasColumnName("magasin_douane").HasMaxLength(50);
            e.Property(x => x.KeyExoneration).HasColumnName("key_exoneration").HasMaxLength(30);
            e.Property(x => x.DateCommission).HasColumnName("date_commission");
            e.Property(x => x.Adresse1Client).HasColumnName("ADRESSE1_CLIENT").HasMaxLength(200);
            e.Property(x => x.Adresse2Client).HasColumnName("ADRESSE2_CLIENT").HasMaxLength(200);
            e.Property(x => x.CpClient).HasColumnName("CP_CLIENT").HasMaxLength(20);
            e.Property(x => x.VilleClient).HasColumnName("VILLE_CLIENT").HasMaxLength(100);
            e.Property(x => x.PaysClient).HasColumnName("PAYS_CLIENT").HasMaxLength(100);

            e.Ignore(x => x.Signe);
            e.Ignore(x => x.TotalHtMad);
            e.Ignore(x => x.EncoursMad);
            e.Ignore(x => x.IsFullySettled);

            e.HasIndex(x => x.NumFacture).IsUnique().HasFilter("[NUM_FACTURE] IS NOT NULL");
            e.HasIndex(x => x.KeyClient);

            e.HasMany(x => x.Lignes).WithOne().HasForeignKey(l => l.KeyFacture).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.TvaDetail).WithOne().HasForeignKey(t => t.KeyFacture).OnDelete(DeleteBehavior.Cascade);
            // Public collections deliberately expose read-only views. EF must populate the
            // backing lists, including _tva whose name does not match TvaDetail by convention.
            e.Navigation(x => x.Lignes).HasField("_lignes").UsePropertyAccessMode(PropertyAccessMode.Field);
            e.Navigation(x => x.TvaDetail).HasField("_tva").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<DetailFactureVente>(e =>
        {
            e.ToTable("T_DETAIL_FACTURE_VENTE");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_ligne").HasMaxLength(30);
            e.Property(x => x.KeyFacture).HasColumnName("KEY_FACTURE").HasMaxLength(30).IsRequired();
            e.Property(x => x.CodeRubrique).HasColumnName("CODE_RUBRIQUE").HasMaxLength(20);
            e.Property(x => x.SocRubrique).HasColumnName("SOC_RUBRIQUE").HasMaxLength(10);
            e.Property(x => x.Position).HasColumnName("POSITION");
            e.Property(x => x.LibelleRubrique).HasColumnName("LIBELLE_RUBRIQUE").HasMaxLength(200);
            e.Property(x => x.Commentaire).HasColumnName("Commentaire").HasMaxLength(500);
            e.Property(x => x.MtTotalDevise).HasColumnName("MT_TOTAL_DEVISE").HasPrecision(18, 3);
            e.Property(x => x.TauxRemise).HasColumnName("TAUX_REMISE").HasPrecision(9, 4);
            e.Property(x => x.MtDevise).HasColumnName("MT_DEVISE").HasPrecision(18, 3);
            e.Property(x => x.Devise).HasColumnName("DEVISE").HasMaxLength(5);
            e.Property(x => x.TauxChange).HasColumnName("TAUX_CHANGE").HasPrecision(18, 6);
            e.Property(x => x.MtHtMad).HasColumnName("MT_HT_MAD").HasPrecision(18, 3);
            e.Property(x => x.TauxTvaInit).HasColumnName("TAUX_TVA_INIT").HasPrecision(9, 4);
            e.Property(x => x.TauxTva).HasColumnName("TAUX_TVA").HasPrecision(9, 4);
            e.Property(x => x.CodeTva).HasColumnName("CODE_TVA").HasMaxLength(20);
            e.Property(x => x.Imprimable).HasColumnName("IMPRIMABLE");
            e.Property(x => x.NatureLigne).HasColumnName("nature_Rubrique").HasMaxLength(50)
                .HasConversion(LegacyConverters.NatureLigne);
            e.Ignore(x => x.IsExonerationEffective);
        });

        b.Entity<TvaFactureVente>(e =>
        {
            e.ToTable("T_TVA_FACTURE_VENTE");
            // Legacy logical key: one row per invoice and rate.
            e.HasKey(x => new { x.KeyFacture, x.CodeTva });
            e.Property(x => x.KeyFacture).HasColumnName("KEY_FACTURE").HasMaxLength(30);
            e.Property(x => x.CodeTva).HasColumnName("CODE_TVA").HasMaxLength(20);
            e.Property(x => x.SocieteKey).HasColumnName("SOCIETE").HasMaxLength(10);
            e.Property(x => x.Ht).HasColumnName("HT").HasPrecision(18, 3);
            e.Property(x => x.Tva).HasColumnName("TVA").HasPrecision(18, 3);
            e.Property(x => x.MtImpute).HasColumnName("MT_IMPUTE").HasPrecision(18, 3);
            e.Ignore(x => x.Reliquat);
            e.Ignore(x => x.IsFullyImputed);
            e.Ignore(x => x.Id);
        });

        b.Entity<RepartitionDossier>(e =>
        {
            e.ToTable("T_REPARTITION_DOSSIER");
            // The legacy table has no key at all; a surrogate identity is added (purely additive).
            e.HasKey(x => x.RepartitionId);
            e.Property(x => x.RepartitionId).HasColumnName("repartition_id").ValueGeneratedOnAdd();
            e.Ignore(x => x.Id);
            e.Property(x => x.KeyFacture).HasColumnName("KEY_FACTURE").HasMaxLength(30).IsRequired();
            e.Property(x => x.KeyLigneDetailFact).HasColumnName("KEY_LIGNE_DETAILFACT").HasMaxLength(30);
            e.Property(x => x.KeyVoyageInter).HasColumnName("key_voy_inter").HasMaxLength(30);
            e.Property(x => x.KeyDossierInter).HasColumnName("key_dossier_inter").HasMaxLength(30);
            e.Property(x => x.MontantMad).HasColumnName("MONTANT_MAD").HasPrecision(18, 3);
            e.Property(x => x.MontantDevise).HasColumnName("montant_devise").HasPrecision(18, 3);
            e.Property(x => x.Devise).HasColumnName("devise").HasMaxLength(5);
            e.Property(x => x.NatureRepartition).HasColumnName("nature_repartition").HasMaxLength(20)
                .HasConversion(LegacyConverters.NatureRepartition);
            e.Property(x => x.TypeRepartition).HasColumnName("type_repartition").HasMaxLength(100);
            e.Property(x => x.MoisCout).HasColumnName("mois_cout").HasMaxLength(7);
            e.Property(x => x.KeyLigneTaxation).HasColumnName("Key_ligne_taxation").HasMaxLength(30);
            e.HasIndex(x => x.KeyFacture);
        });
    }

    private static void ConfigureShipments(ModelBuilder b)
    {
        b.Entity<Voyage>(e =>
        {
            e.ToTable("t_voyages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("KEY_VOYAGE").HasMaxLength(20);
            // NOTE: the tenant column on this table is soc_voyage, not `societe`.
            e.Property(x => x.SocieteKey).HasColumnName("SOC_VOYAGE").HasMaxLength(10).IsRequired();
            e.Property(x => x.DateCreation).HasColumnName("DATE_CREATION");
            e.Property(x => x.NumVoyage).HasColumnName("NUM_VOYAGE").HasMaxLength(30);
            e.Property(x => x.TypeVoyage).HasColumnName("TYPE_VOYAGE").HasMaxLength(4);
            e.Property(x => x.NatureVoyage).HasColumnName("NATURE_VOYAGE").HasMaxLength(30)
                .HasConversion(LegacyConverters.NatureVoyageConverter);
            e.Property(x => x.CategorieVoyage).HasColumnName("categorie_voyage").HasMaxLength(20)
                .HasConversion(LegacyConverters.CategorieVoyageConverter);
            e.Property(x => x.ServiceVoyage).HasColumnName("service_voyage").HasMaxLength(30);
            e.Property(x => x.KeyAgenceSaisie).HasColumnName("key_agence_saisie").HasMaxLength(10);
            e.Property(x => x.IntituleAgenceSaisie).HasColumnName("intitule_agence_saisie").HasMaxLength(60);
            e.Property(x => x.KeyAgenceExploitation).HasColumnName("KEY_AGENCE_EXPLOITATION").HasMaxLength(10);
            e.Property(x => x.IntituleAgenceExploitation).HasColumnName("intitule_agence_exploitation").HasMaxLength(60);
            e.Property(x => x.DateDepartPrevu).HasColumnName("DATE_DEPART_PREVU");
            e.Property(x => x.DateArriveePrevu).HasColumnName("DATE_ARRIVEE_PREVU");
            e.Property(x => x.DateDepart).HasColumnName("DATE_DEPART");
            e.Property(x => x.DateArrivee).HasColumnName("DATE_ARRIVEE");
            e.Property(x => x.DateDepartTrans).HasColumnName("DATE_DEPART_TRANS");
            e.Property(x => x.DateArriveeTrans).HasColumnName("DATE_ARRIVEE_TRANS");
            e.Property(x => x.PortTransbordement).HasColumnName("PORT_TRANSBORDEMENT").HasMaxLength(60);
            // Ports and vessels are stored as free-text labels, not as t_port / t_navire keys.
            e.Property(x => x.PortDepart).HasColumnName("PORT_DEPART").HasMaxLength(60);
            e.Property(x => x.PortArrivee).HasColumnName("PORT_ARRIVEE").HasMaxLength(60);
            e.Property(x => x.NavireDepart).HasColumnName("NAVIRE_DEPART").HasMaxLength(60);
            e.Property(x => x.NavireArrivee).HasColumnName("NAVIRE_ARRIVEE").HasMaxLength(60);
            e.Property(x => x.Provenance).HasColumnName("provenance").HasMaxLength(60);
            e.Property(x => x.Destination).HasColumnName("destination").HasMaxLength(60);
            e.Property(x => x.KeyAgent).HasColumnName("key_agent").HasMaxLength(10);
            e.Property(x => x.IntituleAgent).HasColumnName("intitule_agent").HasMaxLength(80);
            e.Property(x => x.RefAgent).HasColumnName("REF_AGENT").HasMaxLength(30);
            e.Property(x => x.KeyCompagnie).HasColumnName("KEY_COMPAGNIE").HasMaxLength(10);
            e.Property(x => x.IntituleCompagnie).HasColumnName("INTITULE_COMPAGNIE").HasMaxLength(80);
            e.Property(x => x.RefCompagnie).HasColumnName("REF_COMPAGNIE").HasMaxLength(30);
            e.Property(x => x.KeyConsignataire).HasColumnName("KEY_CONSIGNATAIRE").HasMaxLength(10);
            e.Property(x => x.IntituleConsignataire).HasColumnName("INTITULE_CONSIGNATAIRE").HasMaxLength(80);
            e.Property(x => x.RefConsignataire).HasColumnName("REF_CONSIGNATAIRE").HasMaxLength(30);
            e.Property(x => x.KeyTractionnaire).HasColumnName("KEY_TRACTIONNAIRE").HasMaxLength(10);
            e.Property(x => x.IntituleTractionnaire).HasColumnName("INTITULE_TRACTIONNAIRE").HasMaxLength(80);
            e.Property(x => x.RefTractionnaire).HasColumnName("REF_TRACTIONNAIRE").HasMaxLength(30);
            e.Property(x => x.KeyTransporteurInter).HasColumnName("key_transporteur_inter").HasMaxLength(10);
            e.Property(x => x.IntituleTransporteurInter).HasColumnName("intitule_transporteur_inter").HasMaxLength(80);
            e.Property(x => x.RefTransporteurInter).HasColumnName("ref_transporteur_inter").HasMaxLength(30);
            e.Property(x => x.KeyExploitant).HasColumnName("KEY_EXPLOITANT").HasMaxLength(10);
            e.Property(x => x.IntituleExploitant).HasColumnName("INTITULE_EXPLOITANT").HasMaxLength(80);
            e.Property(x => x.KeyChauffeur1).HasColumnName("key_chauffeur1").HasMaxLength(10);
            e.Property(x => x.IntituleChauffeur1).HasColumnName("intitule_chauffeur1").HasMaxLength(80);
            e.Property(x => x.KeyChauffeur2).HasColumnName("key_chauffeur2").HasMaxLength(10);
            e.Property(x => x.IntituleChauffeur2).HasColumnName("intitule_chauffeur2").HasMaxLength(80);
            e.Property(x => x.NumAcquit).HasColumnName("NUM_ACQUIT").HasMaxLength(30);
            e.Property(x => x.DateAcquit).HasColumnName("DATE_ACQUIT");
            e.Property(x => x.NumManifeste).HasColumnName("NUM_MANIFESTE").HasMaxLength(30);
            e.Property(x => x.DateManifeste).HasColumnName("DATE_MANIFESTE");
            e.Property(x => x.NumDs).HasColumnName("NUM_DS").HasMaxLength(30);
            e.Property(x => x.DateDs).HasColumnName("DATE_DS");
            e.Property(x => x.NumCnt).HasColumnName("num_cnt").HasMaxLength(30);
            e.Property(x => x.NumTriptyque).HasColumnName("num_triptyque").HasMaxLength(30);
            e.Property(x => x.DateTriptyque).HasColumnName("date_triptyque");
            e.Property(x => x.Commentaire).HasColumnName("commentaire");
            e.Property(x => x.ModeTraitement).HasColumnName("mode_traitement").HasMaxLength(30);
            e.Property(x => x.KeyStatutVoyage).HasColumnName("key_statut_voyage").HasMaxLength(12).IsRequired();
            e.Property(x => x.DateStatutVoyage).HasColumnName("date_statut_voyage");
            e.Property(x => x.CommentaireStatutVoyage).HasColumnName("commentaire_statut_voyage");
            e.Property(x => x.KeyStatutVoyageMead).HasColumnName("key_statut_voyage_mead").HasMaxLength(12);
            e.Property(x => x.LibelleStatutVoyageMead).HasColumnName("libelle_statut_voyage_mead").HasMaxLength(60);
            e.Property(x => x.DateDernierEvenementMead).HasColumnName("date_dernier_evenement_mead");
            e.Property(x => x.CommentaireDernierEvenementMead).HasColumnName("commentaire_dernier_evenement_mead");
            e.Property(x => x.EntreeMead).HasColumnName("entree_mead");
            e.Property(x => x.MagasinMead).HasColumnName("magasin_mead").HasMaxLength(20);
            e.Property(x => x.NbDossVoyageSaisi).HasColumnName("nb_doss_voyage_saisi");
            e.Property(x => x.TotalPoidsBrutSaisi).HasColumnName("total_Poids_Brut_saisi");
            e.Property(x => x.TotalNbColisSaisi).HasColumnName("total_nbcolis_saisi");
            e.Property(x => x.TemperatureDirigee).HasColumnName("temperature_dirigee");
            e.Property(x => x.NbJourFranchise).HasColumnName("nb_jour_franchise");
            e.Property(x => x.StatutReception).HasColumnName("statut_reception").HasMaxLength(20)
                .HasConversion(LegacyConverters.StatutReceptionConverter);
            // NULL means "not yet validated" — the pivot of the whole costing module.
            e.Property(x => x.DateVoyage).HasColumnName("date_voyage");
            e.Property(x => x.Supprime).HasColumnName("supprimer");
            e.Property(x => x.Attachement).HasColumnName("attachement");
            e.Property(x => x.Tare).HasColumnName("tare");
            e.Property(x => x.NumCaisse).HasColumnName("num_caisse").HasMaxLength(30);
            e.Property(x => x.DateCloture).HasColumnName("date_cloture");
            e.Property(x => x.DatePremiereFacture).HasColumnName("date_premiere_facture");
            e.Property(x => x.KeyBonCommande).HasColumnName("key_bon_commande").HasMaxLength(20);
            e.Property(x => x.KeyVoyageEnlevementLivraison).HasColumnName("key_voyage_enlevement_livraison").HasMaxLength(20);
            e.Property(x => x.KeyDepart).HasColumnName("key_depart").HasMaxLength(20);
            e.Property(x => x.NumBooking).HasColumnName("num_booking").HasMaxLength(30);
            e.Property(x => x.NumRemorque).HasColumnName("num_remorque").HasMaxLength(30);
            e.Property(x => x.NumTracteur).HasColumnName("num_tracteur").HasMaxLength(30);
            e.Property(x => x.NumTc).HasColumnName("num_tc").HasMaxLength(30);
            e.Property(x => x.LocaliteDepart).HasColumnName("localite_depart").HasMaxLength(10);
            e.Property(x => x.LocaliteArrivee).HasColumnName("localite_arrivee").HasMaxLength(10);
            e.Property(x => x.Client).HasColumnName("client").HasMaxLength(120);
            e.Property(x => x.TypeOperation).HasColumnName("type_operation").HasMaxLength(20);
            e.Property(x => x.CapaciteRemorque).HasColumnName("capacite_remorque");
            e.Property(x => x.DateDebutDepotage).HasColumnName("date_debut_depotage");
            e.Property(x => x.DateFinDepotage).HasColumnName("date_fin_depotage");
            e.Property(x => x.DepotePar).HasColumnName("depote_par").HasMaxLength(80);
            e.Property(x => x.ChargerMagasin).HasColumnName("charger_magasin").HasMaxLength(80);
            e.Property(x => x.DateEchangeCompagnie).HasColumnName("date_echange_compagnie");
            e.Property(x => x.DatePrevisionEchange).HasColumnName("date_prevision_echange");

            e.Ignore(x => x.IsValide);
            e.Ignore(x => x.IsCloture);
            e.Ignore(x => x.IsMead);
            e.Ignore(x => x.CostDivisor);

            e.HasQueryFilter(x => !x.Supprime);

            e.HasIndex(x => new { x.SocieteKey, x.NumVoyage });
            e.HasIndex(x => x.DateVoyage);
            e.HasIndex(x => x.KeyAgenceExploitation);

            e.HasMany(x => x.Trajets).WithOne().HasForeignKey(t => t.KeyVoyage).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Equipements).WithOne().HasForeignKey(x => x.KeyVoyage).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TrajetVoyage>(e =>
        {
            e.ToTable("t_trajet_voyages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_trajet").HasMaxLength(20);
            e.Property(x => x.KeyVoyage).HasColumnName("key_voyage").HasMaxLength(20).IsRequired();
            e.Property(x => x.Ordre).HasColumnName("ordre");
            e.Property(x => x.Provenance).HasColumnName("provenance").HasMaxLength(60);
            e.Property(x => x.Destination).HasColumnName("destination").HasMaxLength(60);
            e.Property(x => x.PaysProvenance).HasColumnName("pays_provenance").HasMaxLength(10);
            e.Property(x => x.PaysDestination).HasColumnName("pays_destination").HasMaxLength(10);
            e.Property(x => x.DateDepartPrevu).HasColumnName("date_depart_prevu");
            e.Property(x => x.DateArriveePrevu).HasColumnName("date_arrivee_prevu");
            e.Property(x => x.DateDepart).HasColumnName("date_depart");
            e.Property(x => x.DateArrivee).HasColumnName("date_arrivee");
            e.Property(x => x.KeyTracteur).HasColumnName("key_tracteur").HasMaxLength(20);
            e.Property(x => x.NumTracteur).HasColumnName("num_tracteur").HasMaxLength(30);
            e.Property(x => x.KeyRemorque).HasColumnName("key_remorque").HasMaxLength(20);
            e.Property(x => x.NumRemorque).HasColumnName("num_remorque").HasMaxLength(30);
            e.Property(x => x.TypeRemorque).HasColumnName("type_remorque").HasMaxLength(20);
            e.Property(x => x.KeyChauffeur1).HasColumnName("key_chauffeur1").HasMaxLength(20);
            e.Property(x => x.IntituleChauffeur1).HasColumnName("intitule_chauffeur1").HasMaxLength(80);
            e.Property(x => x.KeyChauffeur2).HasColumnName("key_chauffeur2").HasMaxLength(20);
            e.Property(x => x.IntituleChauffeur2).HasColumnName("intitule_chauffeur2").HasMaxLength(80);
            e.Property(x => x.Kilometrage).HasColumnName("kilometrage");
            e.Property(x => x.FraisAutoroute).HasColumnName("frais_autoroute");
            e.Property(x => x.NumPlomb).HasColumnName("num_plomb").HasMaxLength(30);
            e.Property(x => x.Flux).HasColumnName("flux").HasMaxLength(20);
            e.Property(x => x.LocaliteDepart).HasColumnName("localite_depart").HasMaxLength(10);
            e.Property(x => x.LocaliteArrivee).HasColumnName("localite_arrivee").HasMaxLength(10);
            e.Property(x => x.IntituleLocaliteDepart).HasColumnName("intitule_localite_depart").HasMaxLength(60);
            e.Property(x => x.IntituleLocaliteArrivee).HasColumnName("intitule_localite_arrivee").HasMaxLength(60);
            e.Ignore(x => x.IsCostable);
            e.HasIndex(x => new { x.KeyVoyage, x.Ordre });
        });

        b.Entity<EquipementVoyage>(e =>
        {
            e.ToTable("t_equipement_voyage");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_ligne_equipement").HasMaxLength(20);
            e.Property(x => x.KeyVoyage).HasColumnName("key_voyage").HasMaxLength(20).IsRequired();
            e.Property(x => x.TypeEquipementCode).HasColumnName("type_equipement").HasMaxLength(20).IsRequired();
            e.Property(x => x.NatureEquipement).HasColumnName("nature_equipement").HasMaxLength(20);
            e.Property(x => x.Qte).HasColumnName("qte");
            e.Ignore(x => x.TypeEquipement);
            e.HasIndex(x => x.KeyVoyage);

            e.HasMany(x => x.Details).WithOne().HasForeignKey(d => d.KeyLigneEquipement).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DetailEquipementVoyage>(e =>
        {
            e.ToTable("t_detail_equipement_voyage");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasMaxLength(60);
            e.Property(x => x.KeyLigneEquipement).HasColumnName("key_ligne_equipement").HasMaxLength(20).IsRequired();
            // Nullable: the LCL variant inserts only a number, with no t_materiel reference.
            e.Property(x => x.KeyEquipement).HasColumnName("key_equipement").HasMaxLength(20);
            e.Property(x => x.NumEquipement).HasColumnName("num_equipement").HasMaxLength(30);
            e.Property(x => x.NumPlomb).HasColumnName("num_plomb").HasMaxLength(30);
            e.Property(x => x.Tare).HasColumnName("tare");
            e.Property(x => x.DateRestitution).HasColumnName("date_restitution");
            e.Property(x => x.TypeColis).HasColumnName("type_colis").HasMaxLength(20);
            e.Property(x => x.TypeColisContenant).HasColumnName("type_colis_contenant").HasMaxLength(20);
            e.Property(x => x.NbColis).HasColumnName("nb_colis");
            e.Property(x => x.PoidsBrut).HasColumnName("poids_brut");
            e.Property(x => x.Volume).HasColumnName("volume");
            e.Property(x => x.NatureMarchandise).HasColumnName("nature_marchandise").HasMaxLength(120);
        });

        b.Entity<Dossier>(e =>
        {
            e.ToTable("t_dossiers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_dossier").HasMaxLength(20);
            // NOTE: this table's tenant column really is called `societe`.
            e.Property(x => x.SocieteKey).HasColumnName("societe").HasMaxLength(10).IsRequired();
            e.Property(x => x.NumDossier).HasColumnName("num_dossier").HasMaxLength(30);
            e.Property(x => x.DateCreation).HasColumnName("date_creation");
            e.Property(x => x.TypeDossier).HasColumnName("type_dossier").HasMaxLength(4);
            e.Property(x => x.KeyVoyageInter).HasColumnName("key_voyage_inter").HasMaxLength(20);
            e.Property(x => x.KeyBooking).HasColumnName("key_booking").HasMaxLength(20);
            e.Property(x => x.KeyCotation).HasColumnName("key_cotation").HasMaxLength(20);
            e.Property(x => x.KeyAgenceExploitation).HasColumnName("key_agence_exploitation").HasMaxLength(10);
            e.Property(x => x.IntituleAgenceExploitation).HasColumnName("intitule_agence_exploitation").HasMaxLength(60);
            e.Property(x => x.KeyAgenceCommerciale).HasColumnName("key_agence_commerciale").HasMaxLength(10);
            e.Property(x => x.IntituleAgenceCommerciale).HasColumnName("intitule_agence_commerciale").HasMaxLength(60);
            e.Property(x => x.KeyAgenceSaisie).HasColumnName("key_agence_saisie").HasMaxLength(10);
            e.Property(x => x.IntituleAgenceSaisie).HasColumnName("intitule_agence_saisie").HasMaxLength(60);
            e.Property(x => x.KeyClient).HasColumnName("key_client").HasMaxLength(20);
            e.Property(x => x.IntituleClient).HasColumnName("intitule_client").HasMaxLength(120);
            e.Property(x => x.CommercialDossier).HasColumnName("commercial_dossier").HasMaxLength(20);
            e.Property(x => x.NomCommercialDossier).HasColumnName("nom_commercial_dossier").HasMaxLength(120);
            e.Property(x => x.KeyExploitant).HasColumnName("key_exploitant").HasMaxLength(20);
            e.Property(x => x.NomExploitant).HasColumnName("nom_exploitant").HasMaxLength(120);
            e.Property(x => x.KeyAgent).HasColumnName("key_agent").HasMaxLength(20);
            e.Property(x => x.IntituleAgent).HasColumnName("intitule_agent").HasMaxLength(80);
            e.Property(x => x.RefAgent).HasColumnName("ref_agent").HasMaxLength(30);
            e.Property(x => x.KeyTransitaire).HasColumnName("key_transitaire").HasMaxLength(20);
            e.Property(x => x.IntituleTransitaire).HasColumnName("intitule_transitaire").HasMaxLength(120);
            e.Property(x => x.ReferenceTransitaire).HasColumnName("reference_transitaire").HasMaxLength(60);
            e.Property(x => x.NumHbl).HasColumnName("num_hbl").HasMaxLength(30);
            e.Property(x => x.KeyExpediteur).HasColumnName("key_expediteur").HasMaxLength(20);
            e.Property(x => x.IntituleExpediteur).HasColumnName("intitule_expediteur").HasMaxLength(120);
            e.Property(x => x.Adresse1Expediteur).HasColumnName("adresse1_expediteur").HasMaxLength(120);
            e.Property(x => x.Adresse2Expediteur).HasColumnName("adresse2_expediteur").HasMaxLength(120);
            e.Property(x => x.CpExpediteur).HasColumnName("CP_expediteur").HasMaxLength(15);
            e.Property(x => x.VilleExpediteur).HasColumnName("ville_expediteur").HasMaxLength(60);
            e.Property(x => x.PaysExpediteur).HasColumnName("pays_expediteur").HasMaxLength(10);
            e.Property(x => x.IntitulePaysExpediteur).HasColumnName("intitule_pays_expediteur").HasMaxLength(60);
            e.Property(x => x.RefExpediteur).HasColumnName("ref_expediteur").HasMaxLength(30);
            e.Property(x => x.KeyDestinataire).HasColumnName("key_destinataire").HasMaxLength(20);
            e.Property(x => x.IntituleDestinataire).HasColumnName("intitule_destinataire").HasMaxLength(120);
            e.Property(x => x.Adresse1Destinataire).HasColumnName("adresse1_destinataire").HasMaxLength(120);
            e.Property(x => x.Adresse2Destinataire).HasColumnName("adresse2_destinataire").HasMaxLength(120);
            e.Property(x => x.CpDestinataire).HasColumnName("cp_destinataire").HasMaxLength(15);
            e.Property(x => x.VilleDestinataire).HasColumnName("ville_destinataire").HasMaxLength(60);
            e.Property(x => x.PaysDestinataire).HasColumnName("pays_destinataire").HasMaxLength(10);
            e.Property(x => x.IntitulePaysDestinataire).HasColumnName("intitule_pays_destinataire").HasMaxLength(60);
            e.Property(x => x.RefDestinataire).HasColumnName("ref_destinataire").HasMaxLength(30);
            e.Property(x => x.KeyPayeur).HasColumnName("key_payeur").HasMaxLength(20);
            e.Property(x => x.IntitulePayeur).HasColumnName("intitule_payeur").HasMaxLength(120);
            e.Property(x => x.KeyPayeurMead).HasColumnName("key_payeur_mead").HasMaxLength(20);
            e.Property(x => x.IntitulePayeurMead).HasColumnName("intitule_payeur_mead").HasMaxLength(120);
            e.Property(x => x.KeyClientMead).HasColumnName("key_client_mead").HasMaxLength(20);
            e.Property(x => x.IntituleClientMead).HasColumnName("intitule_client_mead").HasMaxLength(120);
            e.Property(x => x.Incoterm).HasColumnName("incoterm").HasMaxLength(10);
            e.Property(x => x.DescriptionMarchandise).HasColumnName("description_marchandise");
            e.Property(x => x.MarchandiseGerbable).HasColumnName("marchandise_gerbable");
            e.Property(x => x.MarchandiseDangereuse).HasColumnName("marchandise_dangereuse");
            e.Property(x => x.ClasseMarchandiseDangereuse).HasColumnName("classe_marchandise_dangereuse").HasMaxLength(10);
            e.Property(x => x.MarchandiseFragile).HasColumnName("marchandise_fragile");
            e.Property(x => x.Assurance).HasColumnName("assurance");
            e.Property(x => x.ValeurMarchandise).HasColumnName("valeur_marchandise");
            e.Property(x => x.DeviseValeurMarchandise).HasColumnName("devise_valeur_marchandise").HasMaxLength(5);
            e.Property(x => x.TauxChangeValMarch).HasColumnName("taux_change_val_march");
            e.Property(x => x.Marque).HasColumnName("marque").HasMaxLength(120);
            e.Property(x => x.NbColis).HasColumnName("nb_colis");
            e.Property(x => x.TypeColis).HasColumnName("type_colis").HasMaxLength(20);
            e.Property(x => x.Suspendu).HasColumnName("suspendu");
            e.Property(x => x.PoidsBrut).HasColumnName("Poids_brut");
            e.Property(x => x.Volume).HasColumnName("volume");
            e.Property(x => x.Mpl).HasColumnName("mpl");
            e.Property(x => x.PoidsTaxable).HasColumnName("poids_taxable");
            e.Property(x => x.VolumeTaxable).HasColumnName("volume_taxable");
            e.Property(x => x.EquivalenceVolumePoids).HasColumnName("equivalence_volume_poids");
            e.Property(x => x.EquivalenceMplPoids).HasColumnName("EQUIVALENCE_MPL_POIDS");
            e.Property(x => x.EquivalenceMplVolume).HasColumnName("EQUIVALENCE_MPL_VOLUME");
            e.Property(x => x.Position).HasColumnName("position");
            e.Property(x => x.CommentaireDossier).HasColumnName("Commentaire_dossier");
            e.Property(x => x.ServiceDossier).HasColumnName("service_dossier").HasMaxLength(60);
            e.Property(x => x.NatureOperation).HasColumnName("nature_operation").HasMaxLength(30);
            e.Property(x => x.MagasinDouane).HasColumnName("magasin_douane").HasMaxLength(20);
            e.Property(x => x.KeyStatutDossier).HasColumnName("key_statut_dossier").HasMaxLength(12).IsRequired();
            e.Property(x => x.LibelleStatutDossier).HasColumnName("libelle_statut_dossier").HasMaxLength(60);
            e.Property(x => x.DateStatutDossier).HasColumnName("date_statut_dossier");
            e.Property(x => x.DateDernierEvenement).HasColumnName("date_dernier_evenement");
            e.Property(x => x.MontantAssigne).HasColumnName("montant_assigne");
            e.Property(x => x.DeviseMontantAssigne).HasColumnName("devise_montant_assigne").HasMaxLength(5);
            e.Property(x => x.HsCode).HasColumnName("hs_code").HasMaxLength(30);
            e.Property(x => x.CodeRegime).HasColumnName("code_regime").HasMaxLength(20);
            e.Property(x => x.CodeBureau).HasColumnName("code_bureau").HasMaxLength(20);
            e.Property(x => x.NumDum).HasColumnName("num_dum").HasMaxLength(30);
            e.Property(x => x.DateDum).HasColumnName("date_dum");
            e.Property(x => x.ValeurDeclareeDouane).HasColumnName("valeur_declaree_douane");
            e.Property(x => x.LivraisonPar).HasColumnName("livraison_par").HasMaxLength(30);
            e.Property(x => x.TemperatureDirigee).HasColumnName("temperature_dirigee");
            e.Property(x => x.TemperatureMin).HasColumnName("temperature_min");
            e.Property(x => x.TemperatureMax).HasColumnName("temperature_max");
            e.Property(x => x.MontantFret).HasColumnName("montant_fret");
            e.Property(x => x.DeviseFret).HasColumnName("devise_fret").HasMaxLength(5);
            e.Property(x => x.NbUmMead).HasColumnName("NB_UM_MEAD");
            e.Property(x => x.Attachement).HasColumnName("attachement");
            e.Property(x => x.Supprime).HasColumnName("supprimer");
            e.Property(x => x.DateDepotage).HasColumnName("date_depotage");
            e.Property(x => x.LieuDepotage).HasColumnName("lieu_depotage").HasMaxLength(20)
                .HasConversion(LegacyConverters.LieuDepotageConverter);
            e.Property(x => x.ConstatDepotage).HasColumnName("constat_depotage");
            e.Property(x => x.ColisDepote).HasColumnName("colis_depote");
            e.Property(x => x.ColisSortie).HasColumnName("colis_sortie");
            e.Property(x => x.RefDepotage).HasColumnName("ref_depotage").HasMaxLength(30);
            e.Property(x => x.DateClotureIncident).HasColumnName("date_cloture_incident");
            e.Property(x => x.CommentaireIncident).HasColumnName("commentaire_incident");
            // The legacy query spells this `commentaite_cloture_incident` (typo). Corrected here.
            e.Property(x => x.CommentaireClotureIncident).HasColumnName("commentaire_cloture_incident");
            e.Property(x => x.KeyStatutMead).HasColumnName("key_statut_mead").HasMaxLength(12);
            e.Property(x => x.LibelleStatutDossierMead).HasColumnName("libelle_statut_dossier_mead").HasMaxLength(60);
            e.Property(x => x.DateDernierEvenementMead).HasColumnName("date_dernier_evenement_mead");
            e.Property(x => x.CommentaireDernierEvenementMead).HasColumnName("commentaire_dernier_evenement_mead");
            e.Property(x => x.StatutFacturation).HasColumnName("statut_facturation").HasMaxLength(30)
                .HasConversion(LegacyConverters.StatutFacturationConverter);
            e.Property(x => x.StatutCloture).HasColumnName("statut_cloture").HasMaxLength(20)
                .HasConversion(LegacyConverters.StatutClotureConverter);
            e.Property(x => x.Blocage).HasColumnName("blocage").HasMaxLength(50);
            e.Property(x => x.Litige).HasColumnName("litige");
            e.Property(x => x.AssigneRegle).HasColumnName("assigne_regle");
            e.Property(x => x.DossierRegle).HasColumnName("dossier_regle");
            e.Property(x => x.TotalAssigneAchat).HasColumnName("total_assigne_achat");
            e.Property(x => x.TotalAssigneVente).HasColumnName("total_assigne_vente");
            e.Property(x => x.RemiseMagasinage).HasColumnName("remise_magasinage");
            e.Property(x => x.Cod).HasColumnName("cod");
            e.Property(x => x.DateEchange).HasColumnName("date_echange");
            e.Property(x => x.DateDebut).HasColumnName("date_debut");
            e.Property(x => x.DateFin).HasColumnName("date_fin");
            e.Property(x => x.DateSortie).HasColumnName("date_sortie");
            e.Property(x => x.NumBad).HasColumnName("num_bad").HasMaxLength(30);
            e.Property(x => x.DateBad).HasColumnName("date_bad");

            e.Ignore(x => x.IsMead);
            e.Ignore(x => x.HasStockRemaining);

            e.HasQueryFilter(x => !x.Supprime);

            e.HasIndex(x => x.KeyVoyageInter);
            e.HasIndex(x => x.KeyClient);
            e.HasIndex(x => new { x.SocieteKey, x.NumDossier });
            e.HasIndex(x => x.KeyStatutDossier);

            e.HasMany(x => x.Colisages).WithOne().HasForeignKey(c => c.KeyDossier).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ColisageDossier>(e =>
        {
            e.ToTable("t_colisage_dossier");
            // The legacy code never supplies this key: it updates and deletes by it, so it is generated.
            e.HasKey(x => x.KeyLigneColisage);
            e.Property(x => x.KeyLigneColisage).HasColumnName("key_ligne_colisage").ValueGeneratedOnAdd();
            e.Property(x => x.KeyDossier).HasColumnName("key_dossier").HasMaxLength(20).IsRequired();
            e.Property(x => x.TypeColis).HasColumnName("type_colis").HasMaxLength(20).IsRequired();
            e.Property(x => x.NbColis).HasColumnName("nb_colis");
            e.Property(x => x.NatureMarchandise).HasColumnName("nature_marchandise").HasMaxLength(120);
            e.Property(x => x.PoidsBrut).HasColumnName("poids_brut");
            e.Property(x => x.Volume).HasColumnName("volume");
            e.Property(x => x.Marque).HasColumnName("marque").HasMaxLength(120);
            e.Property(x => x.CodeNgp).HasColumnName("code_ngp").HasMaxLength(30);
            e.Property(x => x.ValeurDevise).HasColumnName("valeur_devise");
            e.Property(x => x.Onssa).HasColumnName("onssa");
            // No soft-delete column: packing rows are hard-deleted.
            e.HasIndex(x => x.KeyDossier);
        });

        b.Entity<DossierVoyage>(e =>
        {
            e.ToTable("t_dossiers_voyage");
            e.HasKey(x => new { x.KeyDossier, x.KeyVoyage });
            e.Property(x => x.KeyDossier).HasColumnName("key_dossier").HasMaxLength(20);
            e.Property(x => x.KeyVoyage).HasColumnName("key_voyage").HasMaxLength(20);
            e.Ignore(x => x.Id);
            e.HasIndex(x => x.KeyVoyage);
        });

        b.Entity<CoutOperationVoyageCalcule>(e =>
        {
            e.ToTable("t_cout_operation_voyage_calcule");
            // Two columns only: this is a "already computed" marker, not a money table.
            e.HasKey(x => new { x.KeyVoyage, x.Operation });
            e.Property(x => x.KeyVoyage).HasColumnName("key_voyage").HasMaxLength(20);
            e.Property(x => x.Operation).HasColumnName("operation").HasMaxLength(60);
            e.Ignore(x => x.Id);
        });

        b.Entity<CoutKilometrageVoyage>(e =>
        {
            e.ToTable("t_cout_kilometrage_voyage");
            // The legacy table has no key; rows are deleted by (key_voyage, key_trajet) and re-inserted,
            // so a synthetic surrogate is added (purely additive, safe for migration).
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasMaxLength(80);
            e.Property(x => x.KeyTrajet).HasColumnName("key_trajet").HasMaxLength(20);
            e.Property(x => x.KeyVoyage).HasColumnName("key_voyage").HasMaxLength(20);
            e.Property(x => x.KeyMateriel).HasColumnName("key_materiel").HasMaxLength(20);
            e.Property(x => x.Kilometrage).HasColumnName("kilometrage");
            e.Property(x => x.PrixUnitaire).HasColumnName("prix_unitaire");
            // Only 'Gasoil' and 'AdBlue' are recognised by the legacy purge.
            e.Property(x => x.NatureCout).HasColumnName("nature_cout").HasMaxLength(30);
            e.Property(x => x.Montant).HasColumnName("montant");
            e.HasIndex(x => new { x.KeyVoyage, x.KeyTrajet });
        });

        b.Entity<Statut>(e =>
        {            e.ToTable("t_statut");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_statut").HasMaxLength(12);
            e.Property(x => x.LibelleStatut).HasColumnName("libelle_statut").HasMaxLength(60).IsRequired();
            e.Property(x => x.TypeStatut).HasColumnName("type_statut").HasMaxLength(30);
            e.Property(x => x.Utilisation).HasColumnName("utilisation").HasMaxLength(30);
            // Nullable in the legacy schema: NULL means the status is global.
            e.Property(x => x.SocieteKey).HasColumnName("soc_statut").HasMaxLength(10);
            e.Property(x => x.DocObligatoire).HasColumnName("doc_obligatoire");
            e.Property(x => x.QteObligatoire).HasColumnName("qte_obligatoire");
            e.Property(x => x.Supprime).HasColumnName("supprimer");
            e.Ignore(x => x.IsSystem);
            e.HasQueryFilter(x => !x.Supprime);
            e.HasIndex(x => x.Utilisation);
        });
    }

    private static void ConfigureBookings(ModelBuilder b)
    {
        b.Entity<Booking>(e =>
        {
            e.ToTable("t_booking");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_booking").HasMaxLength(30);
            // NOTE: the tenant column on this table is `societe`.
            e.Property(x => x.SocieteKey).HasColumnName("societe").HasMaxLength(10).IsRequired();
            e.Property(x => x.NumBooking).HasColumnName("num_booking").HasMaxLength(30).IsRequired();
            e.Property(x => x.DateCreation).HasColumnName("date_creation");
            e.Property(x => x.AgenceBooking).HasColumnName("agence_booking").HasMaxLength(10);
            e.Property(x => x.IntituleAgenceBooking).HasColumnName("intitule_agence_booking").HasMaxLength(60);
            e.Property(x => x.TypeBooking).HasColumnName("type_booking").HasMaxLength(4).IsRequired();
            e.Property(x => x.TypeChargement).HasColumnName("type_chargement").HasMaxLength(50);
            e.Property(x => x.Incoterm).HasColumnName("incoterm").HasMaxLength(10);
            e.Property(x => x.Mode).HasColumnName("mode").HasMaxLength(20);
            e.Property(x => x.DateValidite).HasColumnName("date_validite");
            e.Property(x => x.KeyDossier).HasColumnName("key_dossier").HasMaxLength(20);
            e.Property(x => x.KeyCotation).HasColumnName("key_cotation").HasMaxLength(30);
            e.Property(x => x.KeyAgent).HasColumnName("key_agent").HasMaxLength(20);
            e.Property(x => x.IntituleAgent).HasColumnName("intitule_agent").HasMaxLength(120);
            e.Property(x => x.CommercialBooking).HasColumnName("commercial_booking").HasMaxLength(20);
            e.Property(x => x.NomCommercialBooking).HasColumnName("nom_commercial_booking").HasMaxLength(120);
            e.Property(x => x.KeyCompagnie).HasColumnName("key_compagnie").HasMaxLength(20);
            e.Property(x => x.IntituleCompagnie).HasColumnName("intitule_compagnie").HasMaxLength(150);
            e.Property(x => x.KeyClient).HasColumnName("key_client").HasMaxLength(20);
            e.Property(x => x.IntituleClient).HasColumnName("intitule_client").HasMaxLength(120);
            e.Property(x => x.KeyExpediteur).HasColumnName("key_expediteur").HasMaxLength(20);
            e.Property(x => x.IntituleExpediteur).HasColumnName("intitule_expediteur").HasMaxLength(120);
            e.Property(x => x.VilleExpediteur).HasColumnName("ville_expediteur").HasMaxLength(60);
            e.Property(x => x.PaysExpediteur).HasColumnName("pays_expediteur").HasMaxLength(10);
            e.Property(x => x.IntitulePaysExpediteur).HasColumnName("intitule_pays_expediteur").HasMaxLength(60);
            e.Property(x => x.KeyDestinataire).HasColumnName("key_destinataire").HasMaxLength(20);
            e.Property(x => x.IntituleDestinataire).HasColumnName("intitule_destinataire").HasMaxLength(120);
            e.Property(x => x.VilleDestinataire).HasColumnName("ville_destinataire").HasMaxLength(60);
            e.Property(x => x.PaysDestinataire).HasColumnName("pays_destinataire").HasMaxLength(10);
            e.Property(x => x.IntitulePaysDestinataire).HasColumnName("intitule_pays_destinataire").HasMaxLength(60);
            e.Property(x => x.DescriptionMarchandise).HasColumnName("description_marchandise");
            e.Property(x => x.MarchandiseGerbable).HasColumnName("marchandise_gerbable");
            e.Property(x => x.MarchandiseDangereuse).HasColumnName("marchandise_dangereuse");
            e.Property(x => x.ClasseMarchandiseDangereuse).HasColumnName("classe_marchandise_dangereuse").HasMaxLength(20);
            e.Property(x => x.MarchandiseFragile).HasColumnName("marchandise_fragile");
            e.Property(x => x.Assurance).HasColumnName("assurance");
            e.Property(x => x.ValeurMarchandise).HasColumnName("valeur_marchandise");
            // Legacy name suggests insurance only; it is the currency of valeur_marchandise generally.
            e.Property(x => x.DeviseAssurance).HasColumnName("devise_assurance").HasMaxLength(5);
            e.Property(x => x.TemperatureDirigee).HasColumnName("temperature_dirigee");
            e.Property(x => x.TemperatureMin).HasColumnName("temperature_min");
            e.Property(x => x.TemperatureMax).HasColumnName("temperature_max");
            e.Property(x => x.Suspendu).HasColumnName("suspendu");
            e.Property(x => x.LangueClient).HasColumnName("langue_client").HasMaxLength(20);
            e.Property(x => x.PoidsBrutPrevision).HasColumnName("Poids_brut_prevision");
            e.Property(x => x.VolumePrevision).HasColumnName("volume_prevision");
            e.Property(x => x.MplPrevision).HasColumnName("mpl_prevision");
            e.Property(x => x.PoidsTaxablePrevision).HasColumnName("poids_taxable_prevision");
            e.Property(x => x.VolumeTaxablePrevision).HasColumnName("volume_taxable_prevision");
            e.Property(x => x.EquivalenceVolumePoids).HasColumnName("equivalence_volume_poids");
            e.Property(x => x.EquivalenceMplPoids).HasColumnName("EQUIVALENCE_MPL_POIDS");
            e.Property(x => x.EquivalenceMplVolume).HasColumnName("EQUIVALENCE_MPL_VOLUME");
            e.Property(x => x.PoidsBrut).HasColumnName("Poids_brut");
            e.Property(x => x.Volume).HasColumnName("volume");
            e.Property(x => x.Mpl).HasColumnName("mpl");
            e.Property(x => x.PoidsTaxable).HasColumnName("poids_taxable");
            e.Property(x => x.VolumeTaxable).HasColumnName("volume_taxable");
            e.Property(x => x.PoidsMagasinage).HasColumnName("Poids_magasinage");
            e.Property(x => x.PortDepart).HasColumnName("port_depart").HasMaxLength(100);
            e.Property(x => x.PortArrivee).HasColumnName("port_arrivee").HasMaxLength(100);
            e.Property(x => x.DateDepartPrevue).HasColumnName("date_depart_prevue");
            e.Property(x => x.DateArriveePrevue).HasColumnName("date_arrivee_prevue");
            e.Property(x => x.TransitTime).HasColumnName("transit_time").HasMaxLength(50);
            e.Property(x => x.DateDisponibiliteMarchandise).HasColumnName("date_disponibilite_marchandise");
            e.Property(x => x.Provenance).HasColumnName("provenance").HasMaxLength(100);
            e.Property(x => x.Destination).HasColumnName("destination").HasMaxLength(100);
            e.Property(x => x.Commentaire).HasColumnName("Commentaire");
            e.Property(x => x.KeyStatutBooking).HasColumnName("key_statut_booking").HasMaxLength(20).IsRequired();
            e.Property(x => x.DateStatutBooking).HasColumnName("date_statut_booking");
            e.Property(x => x.CommentaireStatutBooking).HasColumnName("commentaire_statut_booking");
            e.Property(x => x.KeyStatutBookingMead).HasColumnName("key_statut_booking_mead").HasMaxLength(20);
            // Denormalised copy of t_statut.libelle_statut; never joined back.
            e.Property(x => x.LibelleStatutMead).HasColumnName("libelle_statut_mead").HasMaxLength(60);
            e.Property(x => x.DateDernierEvenementMead).HasColumnName("date_dernier_evenement_mead");
            e.Property(x => x.Blocage).HasColumnName("blocage").HasMaxLength(50);
            e.Property(x => x.DateDebutDepotage).HasColumnName("date_debut_depotage");
            e.Property(x => x.DateFinDepotage).HasColumnName("date_fin_depotage");
            e.Property(x => x.ConstatDepotageBooking).HasColumnName("constat_depotage_booking");
            e.Property(x => x.ColisDepote).HasColumnName("colis_depote");
            e.Property(x => x.Depote).HasColumnName("depote");
            e.Property(x => x.ColisSortie).HasColumnName("colis_sortie");
            e.Property(x => x.TypeColis).HasColumnName("type_colis").HasMaxLength(20);
            e.Property(x => x.NbTotalColis).HasColumnName("nb_total_colis");
            e.Property(x => x.NbJrsFranchise).HasColumnName("nb_jrs_franchise");
            e.Property(x => x.CodeBureauDouane).HasColumnName("code_bureau_douane").HasMaxLength(20);
            e.Property(x => x.Magasin).HasColumnName("magasin").HasMaxLength(80);
            e.Property(x => x.KeyVoyagePrevisionnel).HasColumnName("key_voyage_previsionnel").HasMaxLength(20);
            e.Property(x => x.KeyVoyageEnlevement).HasColumnName("key_voyage_enlevement").HasMaxLength(20);
            e.Property(x => x.FinEnlevement).HasColumnName("fin_enlevement");
            e.Property(x => x.NumDumBooking).HasColumnName("num_dum_booking").HasMaxLength(30);
            e.Property(x => x.DateDumBooking).HasColumnName("date_dum_booking");
            e.Property(x => x.KeyPayeur).HasColumnName("key_payeur").HasMaxLength(20);
            e.Property(x => x.IntitulePayeur).HasColumnName("intitule_payeur").HasMaxLength(120);
            e.Property(x => x.NatureOperation).HasColumnName("nature_operation").HasMaxLength(30);
            e.Property(x => x.KeyChargeOperation).HasColumnName("key_charge_operation").HasMaxLength(20);
            e.Property(x => x.NomChargeOperation).HasColumnName("nom_charge_operation").HasMaxLength(120);
            e.Property(x => x.FranchiseSurestarie).HasColumnName("franchise_surestarie");
            e.Property(x => x.KeyFournisseurFret).HasColumnName("key_fournisseur_fret").HasMaxLength(20);
            e.Property(x => x.PrevisionAchatFret).HasColumnName("prevision_achat_fret");
            e.Property(x => x.DeviseAchatFret).HasColumnName("devise_achat_fret").HasMaxLength(5);
            e.Property(x => x.TauxChangeAchatFret).HasColumnName("taux_change_achat_fret");
            e.Property(x => x.PrevisionVenteFret).HasColumnName("prevision_vente_fret");
            e.Property(x => x.DeviseVenteFret).HasColumnName("devise_vente_fret").HasMaxLength(5);
            e.Property(x => x.TauxChangeVenteFret).HasColumnName("taux_change_vente_fret");
            e.Property(x => x.KeyFournisseurFraisLocaux).HasColumnName("key_fournisseur_frais_locaux").HasMaxLength(20);
            e.Property(x => x.PrevisionAchatFraisLocaux).HasColumnName("prevision_achat_frais_locaux");
            e.Property(x => x.PrevisionVenteFraisLocaux).HasColumnName("prevision_vente_frais_locaux");
            e.Property(x => x.Attachement).HasColumnName("attachement");
            e.Property(x => x.Supprime).HasColumnName("supprimer");

            e.Ignore(x => x.Sens);
            e.Ignore(x => x.IsAnnule);
            e.Ignore(x => x.IsConverti);
            e.Ignore(x => x.IsMead);
            e.Ignore(x => x.NbEquipements);
            e.Ignore(x => x.IsComplet);
            e.Ignore(x => x.IsGroupage);

            e.HasQueryFilter(x => !x.Supprime);

            // num_booking is enforced unique across the WHOLE table: the legacy check has no societe
            // filter, so the index must not be tenant-scoped either.
            e.HasIndex(x => x.NumBooking).IsUnique();
            e.HasIndex(x => new { x.SocieteKey, x.KeyStatutBooking });
            e.HasIndex(x => x.KeyDossier);
            e.HasIndex(x => x.KeyCotation);

            e.HasMany(x => x.Colisages).WithOne().HasForeignKey(c => c.KeyBooking).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Equipements).WithOne().HasForeignKey(x => x.KeyBooking).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ColisageBooking>(e =>
        {
            e.ToTable("t_colisage_booking");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_colisage_booking").HasMaxLength(30);
            e.Property(x => x.KeyBooking).HasColumnName("key_booking").HasMaxLength(30).IsRequired();
            e.Property(x => x.TypeColis).HasColumnName("type_colis").HasMaxLength(100);
            e.Property(x => x.Qte).HasColumnName("qte");
            e.Property(x => x.Poids).HasColumnName("poids");
            e.Property(x => x.Gerbable).HasColumnName("gerbable");
            e.Property(x => x.Longueur).HasColumnName("longueur");
            e.Property(x => x.Largeur).HasColumnName("largeur");
            e.Property(x => x.Hauteur).HasColumnName("hauteur");
            e.Property(x => x.Volume).HasColumnName("volume");
            e.Property(x => x.HauteurNgerbable).HasColumnName("hauteur_ngerbable");
            e.Property(x => x.TypeEquipement).HasColumnName("type_equipement").HasMaxLength(100);
            e.Property(x => x.NatureEquipement).HasColumnName("nature_equipement").HasMaxLength(100);
            e.HasIndex(x => x.KeyBooking);

            // The packing lines are always rewritten wholesale per booking on save, so the pair is the
            // real identity even though a surrogate is modelled.
            e.HasIndex(x => new { x.KeyBooking, x.TypeColis });
        });

        b.Entity<EquipementBooking>(e =>
        {
            e.ToTable("t_equipement_booking");
            // Exactly five columns in the legacy table.
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_ligne_equipement").HasMaxLength(30);
            e.Property(x => x.KeyBooking).HasColumnName("key_booking").HasMaxLength(30).IsRequired();
            e.Property(x => x.TypeEquipement).HasColumnName("type_equipement").HasMaxLength(100);
            // Joins t_nature_materiel, whose coefficient yields the TEU count.
            e.Property(x => x.NatureEquipement).HasColumnName("nature_equipement").HasMaxLength(100);
            e.Property(x => x.Qte).HasColumnName("qte");
            e.HasIndex(x => x.KeyBooking);
        });

        b.Entity<DetailVoyageBooking>(e =>
        {
            e.ToTable("t_detail_voyage_booking");
            // Exactly two columns; no surrogate key in the legacy table.
            e.HasKey(x => new { x.KeyVoyage, x.KeyBooking });
            e.Property(x => x.KeyVoyage).HasColumnName("key_voyage").HasMaxLength(20);
            e.Property(x => x.KeyBooking).HasColumnName("key_booking").HasMaxLength(30);
            e.Ignore(x => x.Id);
            e.HasIndex(x => x.KeyBooking);
        });
    }

    private static void ConfigureQuotations(ModelBuilder b)
    {
        b.Entity<Cotation>(e =>
        {
            e.ToTable("t_cotation");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_cotation").HasMaxLength(30);
            e.Property(x => x.NumCotation).HasColumnName("n_cotation").HasMaxLength(50).IsRequired();
            // NOTE: the tenant column on this table is `soc_cotation`, NOT `societe`.
            e.Property(x => x.SocieteKey).HasColumnName("soc_cotation").HasMaxLength(10).IsRequired();
            e.Property(x => x.AgenceCotation).HasColumnName("agence_cotation").HasMaxLength(10);
            e.Property(x => x.DateCotation).HasColumnName("date_cotation");
            e.Property(x => x.DateValidite).HasColumnName("date_validite");
            e.Property(x => x.KeyClient).HasColumnName("key_client").HasMaxLength(20).IsRequired();
            e.Property(x => x.Contact).HasColumnName("contact").HasMaxLength(100);
            e.Property(x => x.Commercial).HasColumnName("commercial").HasMaxLength(20);
            // Free text: the author's display name, never a personnel key.
            e.Property(x => x.CreateurCotation).HasColumnName("createur_cotation").HasMaxLength(100);
            e.Property(x => x.ProduitCotation).HasColumnName("produit_cotation").HasMaxLength(10).IsRequired();
            e.Property(x => x.TypeTransport).HasColumnName("type_transport").HasMaxLength(50);
            e.Property(x => x.NatureMarchandise).HasColumnName("nature_marchandise").HasMaxLength(200);
            // Free text, not a t_statut code — the French literals are the domain.
            e.Property(x => x.Statut).HasColumnName("statut").HasMaxLength(30).IsRequired();
            e.Property(x => x.DateStatut).HasColumnName("date_statut");
            e.Property(x => x.CommentaireStatut).HasColumnName("commentaire_statut");
            e.Property(x => x.VolumeReel).HasColumnName("volume_reel");
            e.Property(x => x.VolumeNgerbable).HasColumnName("volume_ngerbable");
            e.Property(x => x.VolumeTaxable).HasColumnName("volume_taxable");
            e.Property(x => x.PoidsTaxable).HasColumnName("POIDS_TAXABLE");
            e.Property(x => x.PoidsReel).HasColumnName("poids_reel");
            e.Property(x => x.MplReel).HasColumnName("mpl_reel");
            e.Property(x => x.EquivalenceVolumePoids).HasColumnName("EQUIVALENCE_VOLUME_POIDS");
            e.Property(x => x.EquivalenceMplPoids).HasColumnName("equivalence_mpl_poids");
            e.Property(x => x.EquivalenceMplVolume).HasColumnName("equivalence_mpl_volume");
            e.Property(x => x.CommentaireCotation).HasColumnName("commentaire_cotation");
            e.Property(x => x.CommentairePrix).HasColumnName("commentaire_prix");
            e.Property(x => x.CommentaireAssurance).HasColumnName("commentaire_assurance").HasMaxLength(255);
            e.Property(x => x.IncotermCotation).HasColumnName("INCOTERM_COTATION").HasMaxLength(20);
            e.Property(x => x.LieuChargement).HasColumnName("lieu_chargement").HasMaxLength(100);
            e.Property(x => x.VilleChargement).HasColumnName("ville_chargement").HasMaxLength(100);
            e.Property(x => x.PaysChargement).HasColumnName("pays_chargement").HasMaxLength(10);
            e.Property(x => x.RegionChargement).HasColumnName("region_chargement").HasMaxLength(100);
            e.Property(x => x.DepartementChargement).HasColumnName("departement_chargement").HasMaxLength(100);
            e.Property(x => x.LieuDechargement).HasColumnName("lieu_dechargement").HasMaxLength(100);
            e.Property(x => x.VilleDechargement).HasColumnName("ville_dechargement").HasMaxLength(100);
            e.Property(x => x.PaysDechargement).HasColumnName("pays_dechargement").HasMaxLength(10);
            e.Property(x => x.RegionDechargement).HasColumnName("region_dechargement").HasMaxLength(100);
            e.Property(x => x.DepartementDechargement).HasColumnName("departement_dechargement").HasMaxLength(100);
            e.Property(x => x.PortChargement).HasColumnName("port_chargement").HasMaxLength(100);
            e.Property(x => x.PortDechargement).HasColumnName("port_dechargement").HasMaxLength(100);
            e.Property(x => x.FrequenceDepart).HasColumnName("frequence_depart").HasMaxLength(50);
            e.Property(x => x.TransitTime).HasColumnName("transit_time").HasMaxLength(50);
            e.Property(x => x.MarchandiseDangereuse).HasColumnName("MARCHANDISE_DANGEREUSE");
            e.Property(x => x.ClasseMarchandise).HasColumnName("classe_marchandise").HasMaxLength(20);
            e.Property(x => x.MarchandiseFragile).HasColumnName("marchandise_fragile");
            e.Property(x => x.MarchandiseGerbable).HasColumnName("marchandise_gerbable");
            e.Property(x => x.FranchiseMagasinage).HasColumnName("franchise_magasinage");
            e.Property(x => x.FranchiseSurestarie).HasColumnName("franchise_surestarie");
            e.Property(x => x.ValeurMarchandise).HasColumnName("valeur_marchandise");
            e.Property(x => x.Devise).HasColumnName("devise").HasMaxLength(5);
            e.Property(x => x.TemperatureMin).HasColumnName("temperature_min");
            e.Property(x => x.TemperatureMax).HasColumnName("temperature_max");
            e.Property(x => x.TemperatureDirigee).HasColumnName("temperature_dirigee");
            e.Property(x => x.Mode).HasColumnName("mode").HasMaxLength(20);
            e.Property(x => x.TypeTarif).HasColumnName("type_tarif").HasMaxLength(20);
            e.Property(x => x.ModeTraitement).HasColumnName("mode_traitement").HasMaxLength(50);
            e.Property(x => x.LangueClient).HasColumnName("langue_client").HasMaxLength(20);
            e.Property(x => x.Assurance).HasColumnName("assurance");
            e.Property(x => x.DateLimiteEnlevement).HasColumnName("date_limite_enlevement");
            e.Property(x => x.DateDepartPrev).HasColumnName("date_depart_prev");
            e.Property(x => x.Provenance).HasColumnName("provenance").HasMaxLength(100);
            e.Property(x => x.Destination).HasColumnName("destination").HasMaxLength(100);
            e.Property(x => x.KeyCompagnie).HasColumnName("key_compagnie").HasMaxLength(20);
            e.Property(x => x.IntituleCompagnie).HasColumnName("intitule_compagnie").HasMaxLength(150);
            e.Property(x => x.Concurrent).HasColumnName("concurrent").HasMaxLength(150);
            e.Property(x => x.MontantConcurrent).HasColumnName("montant_concurrent");
            e.Property(x => x.DeviseConcurrent).HasColumnName("devise_concurrent").HasMaxLength(5);
            e.Property(x => x.MotifCotationPerdue).HasColumnName("motif_cotation_perdue").HasMaxLength(255);
            // Soft-delete flag; the legacy delete is always soft.
            e.Property(x => x.Supprime).HasColumnName("supp");
            e.Property(x => x.CotationContreOffre).HasColumnName("cotation_contre_offre").HasMaxLength(20);
            e.Property(x => x.KeyRegroupement).HasColumnName("key_regroupement").HasMaxLength(20);
            e.Property(x => x.Attachement).HasColumnName("attachement");
            e.Property(x => x.PrestationNonIncluse).HasColumnName("prestation_non_incluse");
            e.Property(x => x.ConditionPaiement).HasColumnName("condition_paiement");
            e.Property(x => x.ConditionGenerale).HasColumnName("condition_generale");

            e.Ignore(x => x.Sens);
            e.Ignore(x => x.IsGroupage);
            e.Ignore(x => x.IsOuverte);

            e.HasQueryFilter(x => !x.Supprime);

            e.HasIndex(x => x.NumCotation);
            e.HasIndex(x => new { x.SocieteKey, x.Statut });
            e.HasIndex(x => x.KeyClient);
            e.HasIndex(x => x.CotationContreOffre);

            e.HasMany(x => x.Colisages).WithOne().HasForeignKey(c => c.KeyCotation).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.PrixCotation).WithOne().HasForeignKey(p => p.KeyCotation).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Achats).WithOne().HasForeignKey(a => a.KeyCotation).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.TauxChange).WithOne().HasForeignKey(t => t.KeyCotation).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Documents).WithOne().HasForeignKey(d => d.KeyCotation).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ColisageCotation>(e =>
        {
            // No tenant column: scope is reached through key_cotation.
            e.ToTable("t_colisage_cotation");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_colisage_cotation").HasMaxLength(30);
            e.Property(x => x.KeyCotation).HasColumnName("key_cotation").HasMaxLength(30).IsRequired();
            // Also matched against t_prix_cotation.base_calcul for equipment-priced lines.
            e.Property(x => x.TypeColis).HasColumnName("type_colis").HasMaxLength(100);
            e.Property(x => x.Qte).HasColumnName("qte");
            e.Property(x => x.Poids).HasColumnName("poids");
            e.Property(x => x.Gerbable).HasColumnName("gerbable");
            e.Property(x => x.Longueur).HasColumnName("longueur");
            e.Property(x => x.Largeur).HasColumnName("largeur");
            e.Property(x => x.Hauteur).HasColumnName("hauteur");
            e.Property(x => x.Volume).HasColumnName("volume");
            e.Property(x => x.HauteurNgerbable).HasColumnName("hauteur_ngerbable");
            e.Property(x => x.VolumeNgerbable).HasColumnName("volume_ngerbable");
            // These two store equipment LABELS, not keys.
            e.Property(x => x.TypeEquipement).HasColumnName("type_equipement").HasMaxLength(100);
            e.Property(x => x.NatureEquipement).HasColumnName("nature_equipement").HasMaxLength(100);
            e.Property(x => x.LargeurEquipement).HasColumnName("largeur_equipement");
            e.Property(x => x.MplUnitaire).HasColumnName("mpl_unitaire");
            e.HasIndex(x => x.KeyCotation);
        });

        b.Entity<PrixCotation>(e =>
        {
            e.ToTable("t_prix_cotation");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_ligne_prix").HasMaxLength(30);
            e.Property(x => x.KeyCotation).HasColumnName("key_cotation").HasMaxLength(30).IsRequired();
            // NOTE: plain `societe` here, unlike t_cotation's `soc_cotation`.
            e.Property(x => x.SocieteKey).HasColumnName("societe").HasMaxLength(10).IsRequired();
            e.Property(x => x.Position).HasColumnName("position");
            e.Property(x => x.CodeRubrique).HasColumnName("code_rubrique").HasMaxLength(20);
            e.Property(x => x.LibelleRubrique).HasColumnName("libelle_rubrique").HasMaxLength(200);
            e.Property(x => x.Quantite).HasColumnName("quantite");
            e.Property(x => x.TypeUtilisation).HasColumnName("type_utilisation").HasMaxLength(20)
                .HasConversion(LegacyConverters.TypeUtilisationNullable);
            e.Property(x => x.MontantValeur).HasColumnName("montant_valeur");
            e.Property(x => x.Devise).HasColumnName("devise").HasMaxLength(5);
            e.Property(x => x.BaseCalcul).HasColumnName("base_calcul").HasMaxLength(100);
            e.Property(x => x.Arrondi).HasColumnName("arrondi");
            e.Property(x => x.MontantAchat).HasColumnName("montant_achat");
            e.Property(x => x.DeviseAchat).HasColumnName("devise_achat").HasMaxLength(5);
            e.Property(x => x.Commentaire).HasColumnName("commentaire").HasMaxLength(255);
            // Percentage literal like "20%", parsed by TauxTva.
            e.Property(x => x.CodeTva).HasColumnName("code_tva").HasMaxLength(20);
            e.Ignore(x => x.TauxTva);
            e.HasIndex(x => x.KeyCotation);
        });

        b.Entity<AchatCotation>(e =>
        {
            // No direct tenant column; soc_rubrique holds the RUBRIQUE's company.
            e.ToTable("t_achat_cotation");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_ligne").HasMaxLength(30);
            e.Property(x => x.KeyCotation).HasColumnName("key_cotation").HasMaxLength(30).IsRequired();
            e.Property(x => x.CodeRubrique).HasColumnName("code_rubrique").HasMaxLength(20);
            e.Property(x => x.SocRubrique).HasColumnName("soc_rubrique").HasMaxLength(10);
            e.Property(x => x.LibelleRubrique).HasColumnName("libelle_rubrique").HasMaxLength(200);
            e.Property(x => x.MtDevise).HasColumnName("mt_devise");
            e.Property(x => x.Devise).HasColumnName("devise").HasMaxLength(5);
            e.Property(x => x.KeyFournisseur).HasColumnName("key_fournisseur").HasMaxLength(20);
            e.Property(x => x.LibelleFournisseur).HasColumnName("libelle_fournisseur").HasMaxLength(200);
            e.Property(x => x.DateValidite).HasColumnName("Date_Validite");
            e.Property(x => x.CheminJustif).HasColumnName("chemin_justif").HasMaxLength(500);
            e.Property(x => x.LibelleJustif).HasColumnName("libelle_justif").HasMaxLength(255);
            // MAD equivalent, recomputed whenever the quotation's rates change.
            e.Property(x => x.MtMad).HasColumnName("mt_mad");
            e.HasIndex(x => x.KeyCotation);
        });

        b.Entity<TauxChangeCotation>(e =>
        {
            // Composite key, no surrogate column and no tenant column.
            e.ToTable("t_taux_change_cotation");
            e.HasKey(x => new { x.KeyCotation, x.Devise });
            e.Property(x => x.KeyCotation).HasColumnName("key_cotation").HasMaxLength(30);
            e.Property(x => x.Devise).HasColumnName("devise").HasMaxLength(5);
            e.Property(x => x.TauxChange).HasColumnName("taux_change");
            e.Ignore(x => x.Id);
            e.HasIndex(x => x.KeyCotation);
        });

        b.Entity<DocumentCotation>(e =>
        {
            // No tenant column.
            e.ToTable("t_document_cotation");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_document").HasMaxLength(30);
            e.Property(x => x.KeyCotation).HasColumnName("key_cotation").HasMaxLength(30).IsRequired();
            e.Property(x => x.LibelleDocument).HasColumnName("libelle_document").HasMaxLength(255);
            e.Property(x => x.CheminDocument).HasColumnName("chemin_document").HasMaxLength(500);
            e.Property(x => x.TypePieceDocument).HasColumnName("type_piece_document").HasMaxLength(30);
            e.HasIndex(x => x.KeyCotation);
        });

        b.Entity<BaseCalculCotation>(e =>
        {
            // Externally seeded: no INSERT/UPDATE exists in the legacy source.
            e.ToTable("t_base_calcul_cotation");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("base_calcul").HasMaxLength(100);
            e.Property(x => x.Arrondi).HasColumnName("arrondi");
            e.Property(x => x.Utilisation).HasColumnName("utilisation").HasMaxLength(100);
            e.Property(x => x.Coefficient).HasColumnName("coefficient");
            e.Ignore(x => x.CoefficientEffectif);
        });

        b.Entity<FranchiseCotation>(e =>
        {
            e.ToTable("t_franchise_cotation");
            e.HasKey(x => new { x.SocieteKey, x.TypeDossier, x.NatureFranchise });
            e.Property(x => x.SocieteKey).HasColumnName("societe").HasMaxLength(10);
            e.Property(x => x.TypeDossier).HasColumnName("type_dossier").HasMaxLength(10);
            e.Property(x => x.NatureFranchise).HasColumnName("nature_franchise").HasMaxLength(30);
            e.Property(x => x.FranchiseComplet).HasColumnName("franchise_complet");
            // Stored as a string in the legacy column, not a number.
            e.Property(x => x.FranchiseGroupage).HasColumnName("franchise_groupage").HasMaxLength(50);
            e.Property(x => x.ConditionGeneraleFr).HasColumnName("CONDITION_GENERALE_fr");
            e.Property(x => x.ConditionPaiementFr).HasColumnName("CONDITION_PAIEMENT_fr");
            e.Property(x => x.PrestationNonIncluseFr).HasColumnName("PRESTATION_NON_INCLUSE_fr");
            e.Property(x => x.ConditionGeneraleAng).HasColumnName("CONDITION_GENERALE_ang");
            e.Property(x => x.ConditionPaiementAng).HasColumnName("CONDITION_PAIEMENT_ang");
            e.Property(x => x.PrestationNonIncluseAng).HasColumnName("PRESTATION_NON_INCLUSE_ang");
            e.Ignore(x => x.Id);
        });
    }

    private static void ConfigureBaremesAchat(ModelBuilder b)
    {
        b.Entity<BaremeAchatCotation>(e =>
        {
            e.ToTable("t_entete_bareme_achat_cotation");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_bareme").HasMaxLength(30);
            // Third distinct tenant column in the cotation module: soc_bareme.
            e.Property(x => x.SocieteKey).HasColumnName("soc_bareme").HasMaxLength(10).IsRequired();
            e.Property(x => x.LibelleBareme).HasColumnName("libelle_bareme").HasMaxLength(255);
            e.Property(x => x.TypeBareme).HasColumnName("type_bareme").HasMaxLength(20)
                .HasConversion(LegacyConverters.TypeBaremeConverter);
            e.Property(x => x.BaseCalcul).HasColumnName("base_calcul").HasMaxLength(100);
            e.Property(x => x.Fournisseur).HasColumnName("fournisseur").HasMaxLength(20);
            e.Property(x => x.DebutValidite).HasColumnName("debut_validite");
            e.Property(x => x.FinValidite).HasColumnName("fin_validite");
            e.Property(x => x.Statut).HasColumnName("Statut").HasMaxLength(30)
                .HasConversion(LegacyConverters.StatutBaremeConverter);
            e.Property(x => x.Arrondi1).HasColumnName("arrondi1");
            e.Property(x => x.Arrondi2).HasColumnName("arrondi2");
            e.Property(x => x.Devise).HasColumnName("devise").HasMaxLength(5);
            e.Property(x => x.Flux).HasColumnName("flux").HasMaxLength(20)
                .HasConversion(LegacyConverters.FluxBaremeConverter);
            e.Property(x => x.Incoterm).HasColumnName("incoterm").HasMaxLength(20);

            e.Ignore(x => x.IsModifiable);

            e.HasIndex(x => new { x.SocieteKey, x.Statut });
            e.HasIndex(x => x.LibelleBareme);

            e.HasMany(x => x.Colonnes).WithOne().HasForeignKey(c => c.KeyBareme).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Details).WithOne().HasForeignKey(d => d.KeyBareme).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Prestations).WithOne().HasForeignKey(p => p.KeyBareme).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ColonneBaremeAchat>(e =>
        {
            e.ToTable("t_colonne_bareme_achat_cotation");
            e.HasKey(x => new { x.KeyBareme, x.IdColonne });
            e.Property(x => x.KeyBareme).HasColumnName("key_bareme").HasMaxLength(30);
            e.Property(x => x.IdColonne).HasColumnName("id_colonne");
            e.Property(x => x.LibelleColonne).HasColumnName("libelle_colonne").HasMaxLength(200);
            e.Property(x => x.TrancheDebut).HasColumnName("tranche_debut");
            e.Property(x => x.TrancheFin).HasColumnName("tranche_fin");
            e.Property(x => x.FormuleCalcul).HasColumnName("formule_calcul").HasMaxLength(500);
            e.Property(x => x.MtMin).HasColumnName("mt_min");
            e.Property(x => x.Commentaire).HasColumnName("commentaire");
            e.Ignore(x => x.Id);
        });

        b.Entity<DetailBaremeAchat>(e =>
        {
            e.ToTable("t_detail_bareme_achat_cotation");
            e.HasKey(x => new { x.KeyBareme, x.IdColonne, x.Departement });
            e.Property(x => x.KeyBareme).HasColumnName("key_bareme").HasMaxLength(30);
            e.Property(x => x.IdColonne).HasColumnName("id_colonne");
            // Joins T_DEPARTEMENT_REGION.departement.
            e.Property(x => x.Departement).HasColumnName("departement").HasMaxLength(100);
            e.Property(x => x.Montant).HasColumnName("montant");
            e.Ignore(x => x.Id);
        });

        b.Entity<PrestationBaremeAchat>(e =>
        {
            e.ToTable("T_PRESTATION_BAREME_ACHAT_COTATION");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_ligne").HasMaxLength(30);
            e.Property(x => x.KeyBareme).HasColumnName("key_bareme").HasMaxLength(30).IsRequired();
            // Holds the RUBRIQUE's company, not the scale owner's.
            e.Property(x => x.SocieteKey).HasColumnName("societe").HasMaxLength(10);
            e.Property(x => x.CodeRubrique).HasColumnName("code_rubrique").HasMaxLength(20);
            e.Property(x => x.LibelleRubrique).HasColumnName("libelle_rubrique").HasMaxLength(200);
            e.Property(x => x.MtDevise).HasColumnName("MT_devise");
            e.Property(x => x.Devise).HasColumnName("devise").HasMaxLength(5);
            e.Property(x => x.BaseCalcul).HasColumnName("base_calcul").HasMaxLength(100);
            e.HasIndex(x => x.KeyBareme);
        });

        // Archives. The key is reused from the source rather than versioned, which is why the trace
        // table carries the ordering that distinguishes generations.
        b.Entity<HistBaremeAchat>(e =>
        {
            e.ToTable("t_hist_entete_bareme_achat_cotation");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_bareme").HasMaxLength(30);
            e.Property(x => x.SocieteKey).HasColumnName("soc_bareme").HasMaxLength(10);
            e.Property(x => x.LibelleBareme).HasColumnName("libelle_bareme").HasMaxLength(255);
            e.Property(x => x.TypeBareme).HasColumnName("type_bareme").HasMaxLength(20);
            e.Property(x => x.BaseCalcul).HasColumnName("Base_calcul").HasMaxLength(100);
            e.Property(x => x.Fournisseur).HasColumnName("fournisseur").HasMaxLength(20);
            e.Property(x => x.DebutValidite).HasColumnName("Debut_validite");
            e.Property(x => x.FinValidite).HasColumnName("fin_validite");
            e.Property(x => x.Statut).HasColumnName("Statut").HasMaxLength(30);
            e.Property(x => x.Arrondi1).HasColumnName("arrondi1");
            e.Property(x => x.Arrondi2).HasColumnName("arrondi2");
            e.Property(x => x.Devise).HasColumnName("devise").HasMaxLength(5);
            e.Property(x => x.Flux).HasColumnName("Flux").HasMaxLength(20);
            e.Property(x => x.Incoterm).HasColumnName("incoterm").HasMaxLength(20);
        });

        b.Entity<HistColonneBaremeAchat>(e =>
        {
            e.ToTable("T_HIST_COLONNE_BAREME_ACHAT_COTATION");
            e.HasKey(x => new { x.KeyBareme, x.IdColonne });
            e.Property(x => x.KeyBareme).HasColumnName("KEY_BAREME").HasMaxLength(30);
            e.Property(x => x.IdColonne).HasColumnName("ID_COLONNE");
            e.Property(x => x.LibelleColonne).HasColumnName("LIBELLE_COLONNE").HasMaxLength(200);
            e.Property(x => x.TrancheDebut).HasColumnName("TRANCHE_DEBUT");
            e.Property(x => x.TrancheFin).HasColumnName("TRANCHE_FIN");
            e.Property(x => x.FormuleCalcul).HasColumnName("FORMULE_CALCUL").HasMaxLength(500);
            e.Property(x => x.MtMin).HasColumnName("MT_MIN");
            // No `commentaire` column: the live table has one, this archive table does not.
            e.Ignore(x => x.Id);
        });

        b.Entity<HistDetailBaremeAchat>(e =>
        {
            e.ToTable("T_HIST_DETAIL_BAREME_ACHAT_COTATION");
            e.HasKey(x => new { x.KeyBareme, x.IdColonne, x.Departement });
            e.Property(x => x.KeyBareme).HasColumnName("KEY_BAREME").HasMaxLength(30);
            e.Property(x => x.IdColonne).HasColumnName("ID_COLONNE");
            e.Property(x => x.Departement).HasColumnName("DEPARTEMENT").HasMaxLength(100);
            e.Property(x => x.Montant).HasColumnName("MONTANT");
            e.Ignore(x => x.Id);
        });

        b.Entity<HistPrestationBaremeAchat>(e =>
        {
            e.ToTable("T_HIST_PRESTATION_BAREME_ACHAT_COTATION");
            e.HasKey(x => new { x.KeyBareme, x.KeyLigne });
            e.Property(x => x.KeyBareme).HasColumnName("key_bareme").HasMaxLength(30);
            e.Property(x => x.KeyLigne).HasColumnName("key_ligne").HasMaxLength(30);
            e.Property(x => x.SocieteKey).HasColumnName("societe").HasMaxLength(10);
            e.Property(x => x.CodeRubrique).HasColumnName("code_rubrique").HasMaxLength(20);
            e.Property(x => x.LibelleRubrique).HasColumnName("libelle_rubrique").HasMaxLength(200);
            e.Property(x => x.MtDevise).HasColumnName("MT_devise");
            e.Property(x => x.Devise).HasColumnName("devise").HasMaxLength(5);
            e.Property(x => x.BaseCalcul).HasColumnName("base_calcul").HasMaxLength(100);
            e.Ignore(x => x.Id);
        });
    }

    private static void ConfigureResources(ModelBuilder b)
    {
        b.Entity<Personnel>(e =>
        {
            e.ToTable("t_personnel");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_personnel").HasMaxLength(30);
            e.Property(x => x.SocieteKey).HasColumnName("soc_personnel").HasMaxLength(10).IsRequired();
            e.Property(x => x.Matricule).HasColumnName("matricule").HasMaxLength(30);
            e.Property(x => x.AgencePersonnel).HasColumnName("agence_personnel").HasMaxLength(10);
            e.Property(x => x.KeyFonctionPersonnel).HasColumnName("key_fonction_personnel").HasMaxLength(30);
            e.Property(x => x.KeyServicePersonnel).HasColumnName("key_service_personnel").HasMaxLength(30);
            e.Property(x => x.KeyDepartementPersonnel).HasColumnName("key_departement_personnel").HasMaxLength(30);
            e.Property(x => x.NomPersonnel).HasColumnName("nom_personnel").HasMaxLength(100);
            e.Property(x => x.PrenomPersonnel).HasColumnName("prenom_personnel").HasMaxLength(100);
            e.Property(x => x.Adresse1Personnel).HasColumnName("adresse1_personnel").HasMaxLength(200);
            e.Property(x => x.Adresse2Personnel).HasColumnName("adresse2_personnel").HasMaxLength(200);
            e.Property(x => x.CpPersonnel).HasColumnName("cp_personnel").HasMaxLength(20);
            e.Property(x => x.VillePersonnel).HasColumnName("ville_personnel").HasMaxLength(100);
            e.Property(x => x.PaysPersonnel).HasColumnName("pays_personnel").HasMaxLength(10);
            e.Property(x => x.GsmPersonnel).HasColumnName("gsm_personnel").HasMaxLength(50);
            e.Property(x => x.FixePersonnel).HasColumnName("fixe_personnel").HasMaxLength(50);
            e.Property(x => x.EmailPersonnel).HasColumnName("email_personnel").HasMaxLength(200);
            e.Property(x => x.DateEmbauche).HasColumnName("date_embauche");
            e.Property(x => x.DateNaissance).HasColumnName("date_naissance");
            e.Property(x => x.SituationFamiliale).HasColumnName("situation_familiale").HasMaxLength(50);
            // Departure IS this table's soft delete — there is no `supprimer` column.
            e.Property(x => x.DateSortie).HasColumnName("date_sortie");
            e.Property(x => x.MotifSortie).HasColumnName("motif_sortie").HasMaxLength(255);
            e.Property(x => x.Bloque).HasColumnName("bloque");
            e.Property(x => x.Admin).HasColumnName("admin");
            e.Property(x => x.AccesWeb).HasColumnName("acces_web");
            e.Property(x => x.PremiereConnexion).HasColumnName("premiere_connexion");
            e.Property(x => x.IdentifiantApp).HasColumnName("identifiant_app").HasMaxLength(100);
            e.Property(x => x.PassWord).HasColumnName("pass_word").HasMaxLength(255);
            e.Property(x => x.PassWordMessagerie).HasColumnName("pass_word_messagerie").HasMaxLength(255);
            e.Property(x => x.TypeEnvoieMail).HasColumnName("type_envoie_mail").HasMaxLength(50);
            e.Property(x => x.CnssPersonnel).HasColumnName("CNSS_PERSONNEL").HasMaxLength(50);
            e.Property(x => x.CinPersonnel).HasColumnName("CIN_PERSONNEL").HasMaxLength(50);
            e.Property(x => x.CimrPersonnel).HasColumnName("CIMR_PERSONNEL").HasMaxLength(50);
            e.Property(x => x.AssurancePersonnel).HasColumnName("ASSURANCE_PERSONNEL").HasMaxLength(50);
            e.Property(x => x.BanquePersonnel).HasColumnName("banque_personnel").HasMaxLength(100);
            e.Property(x => x.RibPersonnel).HasColumnName("rib_personnel").HasMaxLength(50);
            e.Property(x => x.MagasinMead).HasColumnName("magasin_mead").HasMaxLength(50);
            e.Property(x => x.Attachement).HasColumnName("attachement");
            // MONTHLY, despite the bare "Coût" label.
            e.Property(x => x.Cout).HasColumnName("cout");
            e.Property(x => x.DeviseCout).HasColumnName("devise_cout").HasMaxLength(5);
            e.Property(x => x.DateDernierTrajet).HasColumnName("date_dernier_trajet");
            e.Property(x => x.DerniereDestination).HasColumnName("derniere_destination").HasMaxLength(100);
            e.Property(x => x.StatutChauffeur).HasColumnName("statut_chauffeur").HasMaxLength(50);

            e.Ignore(x => x.IsActif);
            e.Ignore(x => x.IsChauffeur);
            e.Ignore(x => x.NomComplet);

            // No query filter: with no `supprimer` column, activity is `date_sortie is null`, which the
            // repository applies explicitly so that "show leavers" stays expressible.
            e.HasIndex(x => new { x.SocieteKey, x.DateSortie });
            e.HasIndex(x => x.KeyFonctionPersonnel);
            e.HasIndex(x => x.IdentifiantApp);
        });

        b.Entity<Materiel>(e =>
        {
            e.ToTable("t_materiel");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_materiel").HasMaxLength(30);
            e.Property(x => x.SocieteKey).HasColumnName("societe").HasMaxLength(10).IsRequired();
            e.Property(x => x.Immatriculation).HasColumnName("immatriculation").HasMaxLength(50);
            e.Property(x => x.Intitule).HasColumnName("intitule").HasMaxLength(150);
            // Discriminator: TRACTEUR / REMORQUE / … — there is no separate tractor/trailer table.
            e.Property(x => x.TypeMateriel).HasColumnName("type_materiel").HasMaxLength(50);
            e.Property(x => x.NatureMateriel).HasColumnName("nature_materiel").HasMaxLength(50);
            e.Property(x => x.Statut).HasColumnName("statut").HasMaxLength(1)
                .HasConversion(LegacyConverters.StatutMaterielConverter);
            e.Property(x => x.FournisseurMateriel).HasColumnName("fournisseur_materiel").HasMaxLength(20);
            e.Property(x => x.MoyenPropre).HasColumnName("moyen_propre").HasMaxLength(50);
            e.Property(x => x.DateAcquisition).HasColumnName("date_acquisition");
            e.Property(x => x.DateMiseCirculation).HasColumnName("date_mise_circulation");
            e.Property(x => x.DateFinGarantie).HasColumnName("date_fin_garantie");
            // Disposal path — coexists with `supprimer`; see the entity remarks.
            e.Property(x => x.DateSortie).HasColumnName("date_sortie");
            e.Property(x => x.NatureSortie).HasColumnName("nature_sortie").HasMaxLength(100);
            e.Property(x => x.MontantVente).HasColumnName("montant_vente");
            e.Property(x => x.KmSortie).HasColumnName("km_sortie");
            e.Property(x => x.Commentaire).HasColumnName("commentaire");
            e.Property(x => x.RefGps).HasColumnName("ref_gps").HasMaxLength(50);
            e.Property(x => x.NumMastercard).HasColumnName("num_mastercard").HasMaxLength(50);
            e.Property(x => x.NumCarteAds).HasColumnName("num_carte_ADS").HasMaxLength(50);
            e.Property(x => x.NumCarteJawaz).HasColumnName("num_carte_jawaz").HasMaxLength(50);
            e.Property(x => x.RfidMateriel).HasColumnName("rfid_materiel").HasMaxLength(50);
            e.Property(x => x.RfidAbrMateriel).HasColumnName("rfid_abr_materiel").HasMaxLength(50);
            e.Property(x => x.KeyCarteMateriel).HasColumnName("key_carte_materiel").HasMaxLength(30);
            e.Property(x => x.KeyCarteJawaz).HasColumnName("key_carte_jawaz").HasMaxLength(30);
            // A DISTANCE (km/day), not a cost — used to project preventive maintenance.
            e.Property(x => x.MoyenneKm).HasColumnName("moyenne_km");
            e.Property(x => x.CompteurKm).HasColumnName("compteur_km");
            e.Property(x => x.DateDernierCompteur).HasColumnName("date_dernier_compteur");
            e.Property(x => x.DateDernierTrajet).HasColumnName("date_dernier_trajet");
            e.Property(x => x.DerniereDestination).HasColumnName("derniere_destination").HasMaxLength(100);
            e.Property(x => x.Chauffeur).HasColumnName("chauffeur").HasMaxLength(100);
            e.Property(x => x.KeyChauffeur).HasColumnName("key_chauffeur").HasMaxLength(30);
            e.Property(x => x.PaysGps).HasColumnName("pays_gps").HasMaxLength(50);
            e.Property(x => x.VilleGps).HasColumnName("ville_gps").HasMaxLength(100);
            e.Property(x => x.FuelLevelGps).HasColumnName("fuel_level_gps");
            e.Property(x => x.Attachement).HasColumnName("attachement");
            e.Property(x => x.Supprimer).HasColumnName("supprimer");
            // MONTHLY.
            e.Property(x => x.Cout).HasColumnName("cout");
            e.Property(x => x.DeviseCout).HasColumnName("devise_cout").HasMaxLength(5);

            e.Ignore(x => x.IsRetire);
            e.Ignore(x => x.IsDisponible);
            e.Ignore(x => x.IsTracteur);
            e.Ignore(x => x.IsRemorque);

            // Disposal (`statut='S'` + date_sortie) is a business state, not a delete, so it is NOT
            // filtered here — disposed vehicles remain visible as history.
            e.HasQueryFilter(x => !x.Supprimer);

            e.HasIndex(x => new { x.SocieteKey, x.TypeMateriel, x.Statut });
            e.HasIndex(x => x.Immatriculation);
            e.HasIndex(x => x.KeyChauffeur);
        });

        b.Entity<TypeMateriel>(e =>
        {
            // No tenant column; the natural key IS the free-text label.
            e.ToTable("t_type_materiel");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("type_materiel").HasMaxLength(50);
            // Gates participation in the voyage costing engine.
            e.Property(x => x.GenerationCout).HasColumnName("generation_cout");
            e.Property(x => x.CodeDouane).HasColumnName("code_douane").HasMaxLength(3);
            e.Property(x => x.CodeNgp).HasColumnName("code_ngp").HasMaxLength(20);
            e.Property(x => x.Supprimer).HasColumnName("supprimer");
            e.HasQueryFilter(x => !x.Supprimer);
        });

        b.Entity<NatureMateriel>(e =>
        {
            // No tenant column; the natural key IS the free-text label.
            e.ToTable("t_nature_materiel");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("nature_materiel").HasMaxLength(50);
            e.Property(x => x.PoidsMax).HasColumnName("poids_max");
            e.Property(x => x.VolumeMax).HasColumnName("volume_max");
            // TEU coefficient.
            e.Property(x => x.Coefficient).HasColumnName("coefficient");
            e.Property(x => x.Supprimer).HasColumnName("supprimer");
            e.Ignore(x => x.CoefficientEffectif);
            e.HasQueryFilter(x => !x.Supprimer);
        });

        b.Entity<CoutKilometrageGasoil>(e =>
        {
            // No tenant column: scope is reached through key_tracteur.
            e.ToTable("t_cout_kilometrage_gasoil");            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_ligne").HasMaxLength(30);
            e.Property(x => x.KeyTracteur).HasColumnName("key_tracteur").HasMaxLength(30).IsRequired();
            e.Property(x => x.DateDebut).HasColumnName("date_debut");
            e.Property(x => x.DateFin).HasColumnName("date_fin");
            e.Property(x => x.TotalKm).HasColumnName("total_km");
            e.Property(x => x.TotalLitre).HasColumnName("total_litre");
            e.Property(x => x.TotalMontant).HasColumnName("total_montant");
            // The per-kilometre rate — the one cost the schema stores per unit of distance.
            e.Property(x => x.CoutKm).HasColumnName("cout_km");
            e.Property(x => x.RatioConsommation).HasColumnName("ratio_consommation");
            // Two rows are normally live in the same window, one per product.
            e.Property(x => x.ProduitGasoil).HasColumnName("produit_gasoil").HasMaxLength(20)
                .HasConversion(LegacyConverters.ProduitGasoilConverter);

            // Supports the legacy window lookup: key_tracteur + date_debut <= d <= date_fin.
            e.HasIndex(x => new { x.KeyTracteur, x.ProduitGasoil, x.DateDebut, x.DateFin });
        });

        b.Entity<PersonnelTypeDossier>(e =>
        {
            // No tenant column and no surrogate key: the pair is the identity.
            e.ToTable("t_personnel_type_dossier");
            e.HasKey(x => new { x.KeyPersonnel, x.TypeDossier });
            e.Property(x => x.KeyPersonnel).HasColumnName("key_personnel").HasMaxLength(30);
            e.Property(x => x.TypeDossier).HasColumnName("type_dossier").HasMaxLength(10);
            e.Ignore(x => x.Id);
            e.HasIndex(x => x.TypeDossier);
        });
    }

    private static void ConfigureEdi(ModelBuilder b)
    {
        b.Entity<EdiVoyageStaging>(e =>
        {
            // A permanent per-user staging table, not a SQL temp table.
            e.ToTable("tmp_edi_voyage");
            e.HasKey(x => x.Id);
            // The key comes from the seq_edi sequence, shared with the dossier staging table.
            e.Property(x => x.Id).HasColumnName("key_edi_voyage").HasMaxLength(30);
            // Tenant column is `soc`, and this table adds `agence` and `utilisateur` on top.
            e.Property(x => x.Societe).HasColumnName("SOC").HasMaxLength(10).IsRequired();
            e.Property(x => x.Agence).HasColumnName("AGENCE").HasMaxLength(10);
            e.Property(x => x.Utilisateur).HasColumnName("utilisateur").HasMaxLength(100);
            e.Property(x => x.NumVoyage).HasColumnName("NOVOYAGE").HasMaxLength(20);
            e.Property(x => x.PortDepart).HasColumnName("PORT_DEPART").HasMaxLength(50);
            // Stored as TEXT: the legacy validation is IsDate() on the raw field.
            e.Property(x => x.DateDepart).HasColumnName("DATE_DEPART").HasMaxLength(20);
            e.Property(x => x.PortArrive).HasColumnName("PORT_ARRIVE").HasMaxLength(50);
            e.Property(x => x.DateArrivee).HasColumnName("DATE_ARRIVEE").HasMaxLength(20);
            e.Property(x => x.Navire).HasColumnName("NAVIRE").HasMaxLength(50);
            e.Property(x => x.Vehicule).HasColumnName("VEHICULE").HasMaxLength(50);
            e.Property(x => x.Caisse).HasColumnName("CAISSE").HasMaxLength(50);
            e.Property(x => x.NbDossiers).HasColumnName("NB_DOSSIERS").HasMaxLength(10);
            e.Property(x => x.TypeVoyage).HasColumnName("TYPE_VOYAGE").HasMaxLength(20);
            e.Property(x => x.Plomb).HasColumnName("PLOMB").HasMaxLength(30);
            e.Property(x => x.TypeDossier).HasColumnName("type_dossier").HasMaxLength(5);
            // The anomaly register, not free text.
            e.Property(x => x.Commentaire).HasColumnName("commentaire");
            e.Ignore(x => x.ADesAnomalies);
            e.HasIndex(x => new { x.Societe, x.Agence, x.Utilisateur });
        });

        b.Entity<EdiDossierStaging>(e =>
        {
            e.ToTable("tmp_edi_dossier");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_edi_dossier").HasMaxLength(30);
            e.Property(x => x.Societe).HasColumnName("SOC").HasMaxLength(10).IsRequired();
            e.Property(x => x.Agence).HasColumnName("AGENCE").HasMaxLength(10);
            e.Property(x => x.Utilisateur).HasColumnName("utilisateur").HasMaxLength(100);
            e.Property(x => x.NumVoyage).HasColumnName("NOVOYAGE").HasMaxLength(20);
            e.Property(x => x.Position).HasColumnName("POSITION").HasMaxLength(10);
            e.Property(x => x.CodeExpediteur).HasColumnName("CODE_EXPEDITEUR").HasMaxLength(20);
            e.Property(x => x.NomExpediteur).HasColumnName("NOM_EXPEDITEUR").HasMaxLength(100);
            e.Property(x => x.Adresse1Expediteur).HasColumnName("ADRESSE1_EXPEDITEUR").HasMaxLength(100);
            e.Property(x => x.Adresse2Expediteur).HasColumnName("ADRESSE2_EXPEDITEUR").HasMaxLength(100);
            e.Property(x => x.Adresse3Expediteur).HasColumnName("ADRESSE3_EXPEDITEUR").HasMaxLength(100);
            e.Property(x => x.VilleExpediteur).HasColumnName("VILLE_EXPEDITEUR").HasMaxLength(80);
            e.Property(x => x.RefExpediteur).HasColumnName("REF_EXPEDITEUR").HasMaxLength(50);
            e.Property(x => x.CodeDestinataire).HasColumnName("CODE_DESTINATAIRE").HasMaxLength(20);
            e.Property(x => x.NomDestinataire).HasColumnName("NOM_DESTINATAIRE").HasMaxLength(100);
            e.Property(x => x.Adresse1Destinataire).HasColumnName("ADRESSE1_DESTINATAIRE").HasMaxLength(100);
            e.Property(x => x.Adresse2Destinataire).HasColumnName("ADRESSE2_DESTINATAIRE").HasMaxLength(100);
            e.Property(x => x.Adresse3Destinataire).HasColumnName("ADRESSE3_DESTINATAIRE").HasMaxLength(100);
            e.Property(x => x.VilleDestinataire).HasColumnName("VILLE_DESTINATAIRE").HasMaxLength(80);
            e.Property(x => x.RefDestinataire).HasColumnName("REF_DESTINATAIRE").HasMaxLength(50);
            e.Property(x => x.Marchandise).HasColumnName("MARCHANDISE").HasMaxLength(200);
            e.Property(x => x.Emballage).HasColumnName("EMBALLAGE").HasMaxLength(100);
            // Trailing underscore is part of the real column name.
            e.Property(x => x.Plan).HasColumnName("PLAN_").HasMaxLength(30);
            e.Property(x => x.Colis).HasColumnName("COLIS").HasMaxLength(20);
            e.Property(x => x.Poids).HasColumnName("POIDS").HasMaxLength(20);
            e.Property(x => x.Volume).HasColumnName("VOLUME").HasMaxLength(20);
            e.Property(x => x.ValeurMarchandise).HasColumnName("VALEUR_MARCHANDISE").HasMaxLength(20);
            e.Property(x => x.Incoterm).HasColumnName("INCOTERM").HasMaxLength(10);
            e.Property(x => x.Cod).HasColumnName("COD").HasMaxLength(10);
            e.Property(x => x.Valeur).HasColumnName("VALEUR").HasMaxLength(20);
            e.Property(x => x.Devise).HasColumnName("DEVISE").HasMaxLength(10);
            e.Property(x => x.Sh4).HasColumnName("SH4").HasMaxLength(30);
            // Five repeating invoice blocks; the legacy casing is inconsistent between the number and its
            // companions (`FACture1` vs `FACture1_Montant`).
            e.Property(x => x.Facture1).HasColumnName("FACture1").HasMaxLength(20);
            e.Property(x => x.Facture1Date).HasColumnName("FACture1_date").HasMaxLength(20);
            e.Property(x => x.Facture1Montant).HasColumnName("FACture1_Montant").HasMaxLength(20);
            e.Property(x => x.Facture2).HasColumnName("FACture2").HasMaxLength(20);
            e.Property(x => x.Facture2Date).HasColumnName("FACture2_date").HasMaxLength(20);
            e.Property(x => x.Facture2Montant).HasColumnName("FACture2_Montant").HasMaxLength(20);
            e.Property(x => x.Facture3).HasColumnName("FACture3").HasMaxLength(20);
            e.Property(x => x.Facture3Date).HasColumnName("FACture3_date").HasMaxLength(20);
            e.Property(x => x.Facture3Montant).HasColumnName("FACture3_Montant").HasMaxLength(20);
            e.Property(x => x.Facture4).HasColumnName("FACture4").HasMaxLength(20);
            e.Property(x => x.Facture4Date).HasColumnName("FACture4_date").HasMaxLength(20);
            e.Property(x => x.Facture4Montant).HasColumnName("FACture4_Montant").HasMaxLength(20);
            e.Property(x => x.Facture5).HasColumnName("FACture5").HasMaxLength(20);
            e.Property(x => x.Facture5Date).HasColumnName("FACture5_date").HasMaxLength(20);
            e.Property(x => x.Facture5Montant).HasColumnName("FACture5_Montant").HasMaxLength(20);
            // Reconciliation columns, filled during validation.
            e.Property(x => x.DeviseClt).HasColumnName("devise_clt").HasMaxLength(10);
            e.Property(x => x.IncotermClt).HasColumnName("INCOTERM_CLT").HasMaxLength(10);
            e.Property(x => x.CodClt).HasColumnName("COD_CLT").HasMaxLength(10);
            e.Property(x => x.KeyClient).HasColumnName("KEY_CLIENT").HasMaxLength(20);
            e.Property(x => x.Client).HasColumnName("CLIENT").HasMaxLength(120);
            e.Property(x => x.KeyCommercial).HasColumnName("key_commercial").HasMaxLength(20);
            e.Property(x => x.Commercial).HasColumnName("commercial").HasMaxLength(120);
            e.Property(x => x.LieuDedouanement).HasColumnName("lieu_dedouanement").HasMaxLength(80);
            e.Property(x => x.Commentaire).HasColumnName("commentaire");
            e.Ignore(x => x.ADesAnomalies);
            e.Ignore(x => x.NumDossier);
            e.HasIndex(x => new { x.Societe, x.Agence, x.Utilisateur });
            e.HasIndex(x => x.NumVoyage);
        });
    }

    private static void ConfigureCarburant(ModelBuilder b)
    {
        b.Entity<Citerne>(e =>
        {
            e.ToTable("t_citernes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("KEY_CITERNE").HasMaxLength(30);
            e.Property(x => x.SocieteKey).HasColumnName("SOCIETE").HasMaxLength(10).IsRequired();
            e.Property(x => x.Agence).HasColumnName("agence").HasMaxLength(10);
            e.Property(x => x.IntituleCiterne).HasColumnName("INTITULE_CITERNE").HasMaxLength(100);
            e.Property(x => x.IndexCompteur).HasColumnName("INDEX_COMPTEUR");
            e.Property(x => x.QteCiterne).HasColumnName("qte_citerne");
            // The per-litre weighted-average price; the ONLY price an issue is valued at.
            e.Property(x => x.Pmp).HasColumnName("pmp");
            e.Property(x => x.TypeCiterne).HasColumnName("type_citerne").HasMaxLength(20)
                .HasConversion(LegacyConverters.TypeCiterneConverter);
            e.Property(x => x.Supprime).HasColumnName("supprimer");

            e.Ignore(x => x.EstGasoil);
            e.Ignore(x => x.EstAdBlue);
            e.Ignore(x => x.EstConfreres);
            e.Ignore(x => x.Stock);
            e.Ignore(x => x.ValeurStock);

            e.HasQueryFilter(x => !x.Supprime);
            e.HasIndex(x => new { x.SocieteKey, x.TypeCiterne });
        });

        b.Entity<SortieGasoil>(e =>
        {
            e.ToTable("t_sortie_gasoil");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_sortie").HasMaxLength(30);
            e.Property(x => x.SocieteKey).HasColumnName("societe_sortie").HasMaxLength(10).IsRequired();
            e.Property(x => x.KeyCiterne).HasColumnName("key_citerne").HasMaxLength(30).IsRequired();
            e.Property(x => x.DateSortie).HasColumnName("date_sortie");
            e.Property(x => x.DateSaisie).HasColumnName("date_saisie");
            e.Property(x => x.TypeCiterne).HasColumnName("type_citerne").HasMaxLength(20)
                .HasConversion(LegacyConverters.TypeCiterneConverter);
            e.Property(x => x.KeyVoyage).HasColumnName("key_voyage").HasMaxLength(20);
            e.Property(x => x.KeyMateriel).HasColumnName("key_materiel").HasMaxLength(30);
            e.Property(x => x.KeyChauffeur).HasColumnName("key_chauffeur").HasMaxLength(30);
            e.Property(x => x.KeyTransporteur).HasColumnName("key_transporteur").HasMaxLength(20);
            e.Property(x => x.Matricule).HasColumnName("matricule").HasMaxLength(50);
            e.Property(x => x.Compteur).HasColumnName("compteur");
            e.Property(x => x.QteSortie).HasColumnName("qte_sortie");
            e.Property(x => x.MontantSortie).HasColumnName("montant_sortie");
            // Snapshot of the tank's PMP at issue time, not a live lookup.
            e.Property(x => x.PrixUnitaire).HasColumnName("prix_unitaire");
            e.Property(x => x.Plein).HasColumnName("plein");
            // Literals include the legacy typo "Sortie Gasoil Confrére".
            e.Property(x => x.TypeSortie).HasColumnName("type_sortie").HasMaxLength(50);
            e.Property(x => x.Observation).HasColumnName("observation");
            e.Property(x => x.Attachement).HasColumnName("attachement");
            e.Property(x => x.Supprime).HasColumnName("supprimer");

            e.HasQueryFilter(x => !x.Supprime);
            e.HasIndex(x => new { x.SocieteKey, x.DateSortie });
            e.HasIndex(x => x.KeyCiterne);
            e.HasIndex(x => x.KeyVoyage);
        });

        b.Entity<BonGasoil>(e =>
        {
            e.ToTable("t_bon_gasoil");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_bon").HasMaxLength(30);
            e.Property(x => x.SocieteKey).HasColumnName("societe_bon").HasMaxLength(10).IsRequired();
            e.Property(x => x.NumBon).HasColumnName("num_bon").HasMaxLength(50);
            e.Property(x => x.DateBon).HasColumnName("date_bon");
            e.Property(x => x.DateSaisie).HasColumnName("date_saisie");
            e.Property(x => x.KeyFournisseur).HasColumnName("key_fournisseur").HasMaxLength(20);
            e.Property(x => x.KeyVoyage).HasColumnName("key_voyage").HasMaxLength(20);
            e.Property(x => x.KeyMateriel).HasColumnName("key_materiel").HasMaxLength(30);
            e.Property(x => x.KeyChauffeur).HasColumnName("key_chauffeur").HasMaxLength(30);
            e.Property(x => x.KeyTransporteur).HasColumnName("key_transporteur").HasMaxLength(20);
            e.Property(x => x.Matricule).HasColumnName("matricule").HasMaxLength(50);
            e.Property(x => x.Compteur).HasColumnName("compteur");
            e.Property(x => x.QteBon).HasColumnName("qte_bon");
            e.Property(x => x.DeviseBon).HasColumnName("devise_bon").HasMaxLength(5);
            e.Property(x => x.MontantBon).HasColumnName("montant_bon");
            e.Property(x => x.TypeCiterne).HasColumnName("type_citerne").HasMaxLength(20)
                .HasConversion(LegacyConverters.TypeCiterneNullableConverter);
            e.Property(x => x.Plein).HasColumnName("plein");
            e.Property(x => x.Observation).HasColumnName("observation");
            e.Property(x => x.TypeBon).HasColumnName("type_bon").HasMaxLength(50);
            e.Property(x => x.Attachement).HasColumnName("attachement");
            e.Property(x => x.Supprime).HasColumnName("supprimer");

            e.HasQueryFilter(x => !x.Supprime);
            e.HasIndex(x => new { x.SocieteKey, x.DateBon });
        });

        b.Entity<JaugeGasoil>(e =>
        {
            e.ToTable("t_jauge_gasoil");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_jauge").HasMaxLength(30);
            e.Property(x => x.SocieteKey).HasColumnName("societe").HasMaxLength(10).IsRequired();
            e.Property(x => x.KeyCiterne).HasColumnName("key_citerne").HasMaxLength(30).IsRequired();
            e.Property(x => x.DateJauge).HasColumnName("date_jauge");
            e.Property(x => x.IndexCompteur).HasColumnName("index_compteur");
            e.Property(x => x.QteStock).HasColumnName("qte_stock");
            // Snapshot of the tank's stock at capture, so the gap stays meaningful.
            e.Property(x => x.QteStockTheorique).HasColumnName("qte_stock_theorique");
            e.Property(x => x.Supprime).HasColumnName("supprimer");
            e.Ignore(x => x.Ecart);
            e.HasQueryFilter(x => !x.Supprime);
            e.HasIndex(x => x.KeyCiterne);
        });

        b.Entity<PrixGasoil>(e =>
        {
            e.ToTable("t_prix_gasoil");
            // No key column: the practical key is (soc_prix, date_prix), so Id is synthetic and unmapped.
            e.HasKey(x => new { x.SocieteKey, x.DatePrix });
            e.Property(x => x.SocieteKey).HasColumnName("soc_prix").HasMaxLength(10);
            e.Property(x => x.DatePrix).HasColumnName("date_prix");
            e.Property(x => x.PrixDuGasoil).HasColumnName("prix_gasoil");
            e.Property(x => x.PrixAdBlue).HasColumnName("prix_adblue");
            e.Property(x => x.DateSaisieReel).HasColumnName("date_saisie_reel");
            e.Property(x => x.Utilisateur).HasColumnName("utilisateur").HasMaxLength(100);
            e.Property(x => x.Supprime).HasColumnName("supprimer");
            e.Ignore(x => x.Id);
            e.HasQueryFilter(x => !x.Supprime);
        });

        b.Entity<CodDossier>(e =>
        {
            e.ToTable("T_COD_DOSSIER");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_ligne_cod").HasMaxLength(30);
            e.Property(x => x.KeyDossier).HasColumnName("key_dossier").HasMaxLength(30);
            e.Property(x => x.Cod).HasColumnName("COD").HasMaxLength(50);
            e.HasIndex(x => x.KeyDossier);
        });

        b.Entity<FactureFournisseurDossier>(e =>
        {
            e.ToTable("T_FACTURE_FOURNISSEUR_DOSSIER");
            // No key column is inserted by the legacy: the column list is exactly (key_dossier, num_facture,
            // date_facture, montant_facture, devise_facture, intitule_fournisseur), so the pair is the key.
            e.HasKey(x => new { x.KeyDossier, x.NumFacture });
            e.Property(x => x.KeyDossier).HasColumnName("key_dossier").HasMaxLength(30);
            e.Property(x => x.NumFacture).HasColumnName("num_facture").HasMaxLength(50);
            e.Property(x => x.DateFacture).HasColumnName("date_facture");
            e.Property(x => x.MontantFacture).HasColumnName("montant_facture");
            e.Property(x => x.DeviseFacture).HasColumnName("devise_facture").HasMaxLength(10);
            // Written as a single space by the legacy when no supplier name is known.
            e.Property(x => x.IntituleFournisseur).HasColumnName("intitule_fournisseur").HasMaxLength(100);
            e.Ignore(x => x.Id);
        });

        b.Entity<StockGasoilConfrere>(e =>
        {
            // Tenant column is key_soc. The row is identified by (key_confrere, key_soc) — a NATURAL key, not
            // one drawn from the shared "GASOIL" sequence, which makes this the only fuel table that does not
            // use it.
            e.ToTable("T_STOCK_GASOIL_CONFRERE");
            e.HasKey(x => new { x.SocieteKey, x.KeyConfrere });
            e.Property(x => x.SocieteKey).HasColumnName("key_soc").HasMaxLength(10);
            e.Property(x => x.KeyConfrere).HasColumnName("key_confrere").HasMaxLength(30);
            e.Property(x => x.QteGasoilEntree).HasColumnName("qte_gasoil_entree");
            e.Property(x => x.QteGasoilSortie).HasColumnName("qte_gasoil_sortie");
            e.Ignore(x => x.Id);
            // Derived, so it must not be persisted as a column of its own.
            e.Ignore(x => x.Solde);
        });

        b.Entity<ConsommationGasoil>(e =>
        {
            // Tenant column is societe_consommation — a fourth distinct spelling in this module, alongside
            // `societe` on t_citernes, `societe_sortie` on t_sortie_gasoil and `key_soc` elsewhere.
            e.ToTable("t_consommation_gasoil");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_consommation").HasMaxLength(30);
            e.Property(x => x.SocieteKey).HasColumnName("societe_consommation").HasMaxLength(10);
            // The discriminator the consumption reports group by; legacy literals, accents and typos intact.
            e.Property(x => x.TypeSortie).HasColumnName("type_sortie").HasMaxLength(40);
            e.Property(x => x.DateConsommation).HasColumnName("date_consommation");
            e.Property(x => x.KeyCiterne).HasColumnName("key_citerne").HasMaxLength(30);
            e.Property(x => x.KeyVoyage).HasColumnName("key_voyage").HasMaxLength(30);
            e.Property(x => x.KeyMateriel).HasColumnName("key_materiel").HasMaxLength(30);
            e.Property(x => x.KeyChauffeur).HasColumnName("key_chauffeur").HasMaxLength(30);
            e.Property(x => x.KeyTransporteur).HasColumnName("key_transporteur").HasMaxLength(30);
            e.Property(x => x.KeyFournisseur).HasColumnName("key_fournisseur").HasMaxLength(30);
            e.Property(x => x.NumBon).HasColumnName("num_bon").HasMaxLength(50);
            e.Property(x => x.CompteurConsommation).HasColumnName("compteur_consommation");
            e.Property(x => x.QteConsommation).HasColumnName("qte_consommation");
            e.Property(x => x.MontantConsommation).HasColumnName("montant_consommation");
            e.Property(x => x.DeviseConsommation).HasColumnName("devise_consommation").HasMaxLength(10);
            e.Property(x => x.PrixUnitaire).HasColumnName("prix_unitaire");
            e.Property(x => x.Plein).HasColumnName("plein");
            e.Property(x => x.TypeCiterne).HasColumnName("type_citerne").HasMaxLength(20);
            e.Property(x => x.Matricule).HasColumnName("matricule").HasMaxLength(50);
            e.Property(x => x.Observation).HasColumnName("observation").HasMaxLength(500);
            e.Property(x => x.RatioCalcule).HasColumnName("ratio_calcule");
            e.Property(x => x.Supprime).HasColumnName("supprimer");
            e.HasIndex(x => new { x.SocieteKey, x.DateConsommation });
            e.HasIndex(x => x.KeyVoyage);
            e.HasQueryFilter(x => !x.Supprime);
        });

        b.Entity<ApprovisionnementCiterne>(e =>
        {
            e.ToTable("t_approvisionnement_citerne");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("key_approvisionnement").HasMaxLength(30);
            // Tenant column is `societe` on this table; the neighbouring t_sortie_gasoil uses
            // `societe_sortie` and t_consommation_gasoil uses `societe_consommation`.
            e.Property(x => x.SocieteKey).HasColumnName("societe").HasMaxLength(10);
            e.Property(x => x.KeyFournisseur).HasColumnName("key_fournisseur").HasMaxLength(30);
            e.Property(x => x.DateApprovisionnement).HasColumnName("date_approvisionnement");
            // num_bl, NOT num_bon — num_bon belongs to t_consommation_gasoil.
            e.Property(x => x.NumBl).HasColumnName("num_bl").HasMaxLength(50);
            e.Property(x => x.QteTotale).HasColumnName("qte_totale");
            // The only cost column in the module: lines are allocated from it, never stored.
            e.Property(x => x.MontantApprovisionnement).HasColumnName("montant_approvisionnement");
            e.Property(x => x.Attachement).HasColumnName("attachement");
            e.Property(x => x.Supprimer).HasColumnName("supprimer");

            // The legacy list query filters SUPPRIMER = 0, even though nothing ever sets it.
            e.HasQueryFilter(x => !x.Supprimer);

            e.HasMany(x => x.Lignes)
                .WithOne()
                .HasForeignKey(l => l.KeyApprovisionnement)
                .HasPrincipalKey(x => x.Id)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DetailApprovisionnementCiterne>(e =>
        {
            e.ToTable("t_detail_approvisionnement_citerne");
            // No surrogate key is ever inserted: the insert column list is exactly
            // (key_approvisionnement, key_citerne, index_compteur, qte_citerne). The pair is the key, and
            // there is no tenant column — scope is reached through the header.
            e.HasKey(x => new { x.KeyApprovisionnement, x.KeyCiterne });
            e.Property(x => x.KeyApprovisionnement).HasColumnName("key_approvisionnement").HasMaxLength(30);
            e.Property(x => x.KeyCiterne).HasColumnName("key_citerne").HasMaxLength(30);
            e.Property(x => x.IndexCompteur).HasColumnName("index_compteur");
            e.Property(x => x.QteCiterne).HasColumnName("qte_citerne");
            e.Ignore(x => x.Id);
        });
    }

    private static void ConfigureAudit(ModelBuilder b)
    {
        b.Entity<HistReferentiel>(e =>
        {
            e.ToTable("t_hist_referentiel");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.NomTable).HasColumnName("nom_table").HasMaxLength(100).IsRequired();
            e.Property(x => x.Evenement).HasColumnName("evenement").HasMaxLength(50).IsRequired();
            e.Property(x => x.Utilisateur).HasColumnName("utilisateur").HasMaxLength(100);
            e.Property(x => x.DateOperation).HasColumnName("date_operation");
            e.Property(x => x.Commentaire).HasColumnName("commentaire").HasMaxLength(4000);
            e.Property(x => x.SocReferentiel).HasColumnName("soc_referentiel").HasMaxLength(10);
            e.Property(x => x.PosteUtilisateur).HasColumnName("poste_utilisateur").HasMaxLength(50);
            e.HasIndex(x => x.DateOperation);
        });

        b.Entity<EvenementApp>(e =>
        {
            e.ToTable("t_evenement_app");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.DateEvenement).HasColumnName("date_evenement");
            e.Property(x => x.Utilisateur).HasColumnName("utilisateur").HasMaxLength(100);
            e.Property(x => x.Message).HasColumnName("message").HasMaxLength(4000).IsRequired();
            e.Property(x => x.CodeMessage).HasColumnName("code_message").HasMaxLength(50);
            e.Property(x => x.Programme).HasColumnName("programme").HasMaxLength(200);
            e.Property(x => x.PosteUtilisateur).HasColumnName("poste_utilisateur").HasMaxLength(50);
            e.HasIndex(x => x.DateEvenement);
        });

        // The 63 legacy trace tables share one shape; only the entity-key column name differs.
        // Each is mapped as its own table because that is what the existing viewers query.
        ConfigureTrace<TraceClient>(b, "t_trace_client", "key_client");
        ConfigureTrace<TraceReglement>(b, "t_trace_reglement", "key_reglement");
        ConfigureTrace<TraceFactureVente>(b, "t_trace_facture_vente", "key_facture");
        ConfigureTrace<TraceDossier>(b, "t_trace_dossier", "key_dossier");
        ConfigureTrace<TraceVoyage>(b, "t_trace_voyage", "key_voyage");
        ConfigureTrace<TraceBooking>(b, "t_trace_booking", "key_booking");
        ConfigureTrace<TraceCotation>(b, "t_trace_cotation", "key_cotation");
        ConfigureTrace<TracePersonnel>(b, "t_trace_personnel", "key_personnel");
        ConfigureTrace<TraceBaremeAchat>(b, "T_TRACE_BAREME_ACHAT_COTATION", "key_bareme");
    }

    private static void ConfigureTrace<T>(ModelBuilder b, string table, string keyColumn)
        where T : TraceEntry
    {
        b.Entity<T>(e =>
        {
            e.ToTable(table);
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.EntityKey).HasColumnName(keyColumn).HasMaxLength(50).IsRequired();
            e.Property(x => x.Societe).HasColumnName("soc_trace").HasMaxLength(10);
            e.Property(x => x.Agence).HasColumnName("agence_trace").HasMaxLength(10);
            e.Property(x => x.KeyStatut).HasColumnName("key_statut").HasMaxLength(20);
            e.Property(x => x.DateTraceUser).HasColumnName("date_trace_user");
            e.Property(x => x.DateTraceSystem).HasColumnName("date_trace_system");
            e.Property(x => x.Commentaire).HasColumnName("commentaire_trace").HasMaxLength(4000);
            e.Property(x => x.Utilisateur).HasColumnName("utilisateur").HasMaxLength(100);
            e.Property(x => x.KeyDocument).HasColumnName("key_document").HasMaxLength(50);
            e.Property(x => x.Valeur).HasColumnName("valeur");
            e.Property(x => x.PosteUtilisateur).HasColumnName("poste_utilisateur").HasMaxLength(50);
            e.HasIndex(x => new { x.EntityKey, x.DateTraceSystem });
        });
    }
}

/// <summary>
/// Value converters between the C# enums and the exact literals the legacy database stores.
/// </summary>
/// <remarks>
/// These matter more than they look. The legacy application compares some of these columns with
/// <c>UPPER()</c> and others without, and several values contain spaces and accents
/// ("Remis en banque", "Préparé", "Débours"). A wrong literal silently breaks a filter instead of
/// throwing, so every mapping is explicit and covered by unit tests.
/// </remarks>
internal static class LegacyConverters
{
    // The legacy/test databases contain both labels and one-letter codes.
    // Reject unknown values rather than silently changing a dossier's direction.
    public static readonly ValueConverter<Sens, string> SensDossierConverter =
        new(value => value.ToString(), stored => ParseSensDossier(stored));

    private static Sens ParseSensDossier(string stored) => stored.Trim().ToUpperInvariant() switch
    {
        "I" or "IMPORT" => Sens.Import,
        "E" or "EXPORT" => Sens.Export,
        "N" or "NATIONAL" => Sens.National,
        _ => throw new InvalidOperationException("Unknown dossier direction in the reference data.")
    };

    /// <summary>
    /// <c>t_prix_cotation.type_utilisation</c> — Montant / Pourcentage, or NULL.
    /// </summary>
    /// <remarks>
    /// Nullable because the legacy column is and unset lines exist; the case-insensitive read matters
    /// because the value was typed freely in places.
    /// </remarks>
    public static readonly ValueConverter<TypeUtilisation?, string?> TypeUtilisationNullable =
        new(v => TypeUtilisationToProvider(v), s => TypeUtilisationFromProvider(s));

    private static string? TypeUtilisationToProvider(TypeUtilisation? value) => value switch
    {
        TypeUtilisation.Montant => "Montant",
        TypeUtilisation.Pourcentage => "Pourcentage",
        _ => null
    };

    private static TypeUtilisation? TypeUtilisationFromProvider(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : string.Equals(value, "Pourcentage", StringComparison.OrdinalIgnoreCase)
                ? TypeUtilisation.Pourcentage
                : TypeUtilisation.Montant;

    /// <summary><c>Statut</c> of a purchase tariff scale: Non Validé / Validé / Supprimé.</summary>
    /// <remarks>
    /// Named with a <c>Converter</c> suffix on purpose: a field named <c>StatutBareme</c> would shadow
    /// the enum type of the same name inside this class, exactly as happened on
    /// <c>PrixCotation.TypeUtilisation</c>.
    /// </remarks>
    public static readonly ValueConverter<StatutBareme, string> StatutBaremeConverter =
        new(v => StatutBaremeToProvider(v), s => StatutBaremeFromProvider(s));

    private static string StatutBaremeToProvider(StatutBareme v) => v switch
    {
        StatutBareme.Valide => "Validé",
        StatutBareme.Supprime => "Supprimé",
        _ => "Non Validé"
    };

    private static StatutBareme StatutBaremeFromProvider(string s) => s?.Trim().ToUpperInvariant() switch
    {
        "VALIDÉ" or "VALIDE" => StatutBareme.Valide,
        "SUPPRIMÉ" or "SUPPRIME" => StatutBareme.Supprime,
        _ => StatutBareme.NonValide
    };

    public static readonly ValueConverter<TypeBareme?, string?> TypeBaremeConverter =
        new(v => TypeBaremeToProvider(v), s => TypeBaremeFromProvider(s));

    private static string? TypeBaremeToProvider(TypeBareme? v) => v switch
    {
        TypeBareme.Complet => "Complet",
        TypeBareme.Groupage => "Groupage",
        _ => null
    };

    private static TypeBareme? TypeBaremeFromProvider(string? s) => s?.Trim().ToUpperInvariant() switch
    {
        "COMPLET" => TypeBareme.Complet,
        "GROUPAGE" => TypeBareme.Groupage,
        _ => null
    };

    public static readonly ValueConverter<FluxBareme?, string?> FluxBaremeConverter =
        new(v => FluxBaremeToProvider(v), s => FluxBaremeFromProvider(s));

    private static string? FluxBaremeToProvider(FluxBareme? v) => v switch
    {
        FluxBareme.National => "National",
        FluxBareme.International => "International",
        _ => null
    };

    private static FluxBareme? FluxBaremeFromProvider(string? s) => s?.Trim().ToUpperInvariant() switch
    {
        "NATIONAL" => FluxBareme.National,
        "INTERNATIONAL" => FluxBareme.International,
        _ => null
    };

    /// <summary><c>t_materiel.statut</c>: D = Disponible, R = En réparation, S = Sorti.</summary>
    public static readonly ValueConverter<StatutMateriel, string> StatutMaterielConverter =
        new(v => StatutMaterielToProvider(v), s => StatutMaterielFromProvider(s));

    private static string StatutMaterielToProvider(StatutMateriel v) => v switch
    {
        StatutMateriel.EnReparation => "R",
        StatutMateriel.Sorti => "S",
        _ => "D"
    };

    // Anything unrecognised reads as Disponible, matching the legacy CASE that only special-cased 'D'.
    private static StatutMateriel StatutMaterielFromProvider(string s) => s?.Trim().ToUpperInvariant() switch
    {
        "R" => StatutMateriel.EnReparation,
        "S" => StatutMateriel.Sorti,
        _ => StatutMateriel.Disponible
    };

    /// <summary><c>t_cout_kilometrage_gasoil.produit_gasoil</c>: Gasoil / AdBlue.</summary>
    public static readonly ValueConverter<ProduitGasoil, string> ProduitGasoilConverter =
        new(v => ProduitGasoilToProvider(v), s => ProduitGasoilFromProvider(s));

    private static string ProduitGasoilToProvider(ProduitGasoil v) =>
        v == ProduitGasoil.AdBlue ? "AdBlue" : "Gasoil";

    private static ProduitGasoil ProduitGasoilFromProvider(string s) =>
        string.Equals(s?.Trim(), "AdBlue", StringComparison.OrdinalIgnoreCase)
            ? ProduitGasoil.AdBlue
            : ProduitGasoil.Gasoil;

    /// <summary><c>type_citerne</c>: Gasoil / AdBlue. Tanks are segregated by product.</summary>
    public static readonly ValueConverter<TypeCiterne, string> TypeCiterneConverter =
        new(v => TypeCiterneToProvider(v), s => TypeCiterneFromProvider(s));

    public static readonly ValueConverter<TypeCiterne?, string?> TypeCiterneNullableConverter =
        new(v => v.HasValue ? TypeCiterneToProvider(v.Value) : null,
            s => string.IsNullOrWhiteSpace(s) ? null : TypeCiterneFromProvider(s));

    private static string TypeCiterneToProvider(TypeCiterne v) =>
        v == TypeCiterne.AdBlue ? "AdBlue" : "Gasoil";

    private static TypeCiterne TypeCiterneFromProvider(string s) =>
        string.Equals(s?.Trim(), "AdBlue", StringComparison.OrdinalIgnoreCase)
            ? TypeCiterne.AdBlue
            : TypeCiterne.Gasoil;

    public static readonly ValueConverter<TypeReglement, string> TypeReglement = new(
        v => v.ToString(),
        s => Enum.Parse<TypeReglement>(s, true));

    public static readonly ValueConverter<NatureReglement, string> NatureReglement = new(
        v => v == Domain.Enums.NatureReglement.Avance ? "AVANCE" : "REGLEMENT",
        s => string.Equals(s, "AVANCE", StringComparison.OrdinalIgnoreCase)
            ? Domain.Enums.NatureReglement.Avance
            : Domain.Enums.NatureReglement.Reglement);

    // NOTE: ValueConverter's delegates are EXPRESSION TREES, and an expression tree may not contain a
    // `switch` expression (CS8514). Every non-trivial mapping is therefore a static method and the
    // converter holds a lambda that merely calls it — method calls are legal inside expression trees.
    public static readonly ValueConverter<StatutReglement, string> StatutReglement =
        new(v => StatutToProvider(v), s => StatutFromProvider(s));

    private static string StatutToProvider(Domain.Enums.StatutReglement v) => v switch
    {
        Domain.Enums.StatutReglement.Portefeuille => "Portefeuille",
        Domain.Enums.StatutReglement.RemisEnBanque => "Remis en banque",
        Domain.Enums.StatutReglement.RemisAuFournisseur => "Remis au Fournisseur",
        Domain.Enums.StatutReglement.Signe => "Signé",
        Domain.Enums.StatutReglement.Regle => "Réglé",
        Domain.Enums.StatutReglement.Impaye => "Impayé",
        _ => "Saisie"
    };

    private static Domain.Enums.StatutReglement StatutFromProvider(string s) => s switch
    {
        "Portefeuille" => Domain.Enums.StatutReglement.Portefeuille,
        "Remis en banque" => Domain.Enums.StatutReglement.RemisEnBanque,
        "Remis au Fournisseur" => Domain.Enums.StatutReglement.RemisAuFournisseur,
        "Signé" => Domain.Enums.StatutReglement.Signe,
        "Réglé" => Domain.Enums.StatutReglement.Regle,
        "Impayé" => Domain.Enums.StatutReglement.Impaye,
        _ => Domain.Enums.StatutReglement.Saisie
    };

    public static readonly ValueConverter<SensReglement, string> SensReglement = new(
        v => v == Domain.Enums.SensReglement.Vente ? "V" : "A",
        s => string.Equals(s, "A", StringComparison.OrdinalIgnoreCase)
            ? Domain.Enums.SensReglement.Achat
            : Domain.Enums.SensReglement.Vente);

    public static readonly ValueConverter<TypeLettrage, string> TypeLettrage = new(
        v => v.ToString(),
        s => Enum.Parse<TypeLettrage>(s, true));

    public static readonly ValueConverter<TypeTresorie, string> TypeTresorie = new(
        v => v == Domain.Enums.TypeTresorie.Banque ? "B" : "C",
        s => string.Equals(s, "C", StringComparison.OrdinalIgnoreCase)
            ? Domain.Enums.TypeTresorie.Caisse
            : Domain.Enums.TypeTresorie.Banque);

    public static readonly ValueConverter<TypeTiers, string> TypeTiers =
        new(v => TiersToProvider(v), s => TiersFromProvider(s));

    private static string TiersToProvider(Domain.Enums.TypeTiers v) => v switch
    {
        Domain.Enums.TypeTiers.Fournisseur => "F",
        Domain.Enums.TypeTiers.Agent => "A",
        Domain.Enums.TypeTiers.Tiers => "T",
        _ => "C"
    };

    private static Domain.Enums.TypeTiers TiersFromProvider(string s) => s switch
    {
        "F" => Domain.Enums.TypeTiers.Fournisseur,
        "A" => Domain.Enums.TypeTiers.Agent,
        "T" => Domain.Enums.TypeTiers.Tiers,
        _ => Domain.Enums.TypeTiers.Client
    };

    public static readonly ValueConverter<PeriodiciteReleve, string> PeriodiciteReleve =
        new(v => ReleveToProvider(v), s => ReleveFromProvider(s));

    private static string ReleveToProvider(Domain.Enums.PeriodiciteReleve v) => v switch
    {
        Domain.Enums.PeriodiciteReleve.Hebdomadaire => "H",
        Domain.Enums.PeriodiciteReleve.Quinzaine => "Q",
        Domain.Enums.PeriodiciteReleve.Mensuel => "M",
        _ => string.Empty
    };

    private static Domain.Enums.PeriodiciteReleve ReleveFromProvider(string s) => s switch
    {
        "H" => Domain.Enums.PeriodiciteReleve.Hebdomadaire,
        "Q" => Domain.Enums.PeriodiciteReleve.Quinzaine,
        "M" => Domain.Enums.PeriodiciteReleve.Mensuel,
        _ => Domain.Enums.PeriodiciteReleve.Aucune
    };

    public static readonly ValueConverter<StatutDeclarationTva, string> StatutDeclarationTva =
        new(v => DeclarationToProvider(v), s => DeclarationFromProvider(s));

    private static string DeclarationToProvider(Domain.Enums.StatutDeclarationTva v) => v switch
    {
        Domain.Enums.StatutDeclarationTva.Prepare => "Préparé",
        Domain.Enums.StatutDeclarationTva.Declare => "Déclaré",
        _ => "Attente Déclaration"
    };

    private static Domain.Enums.StatutDeclarationTva DeclarationFromProvider(string s) => s switch
    {
        "Préparé" => Domain.Enums.StatutDeclarationTva.Prepare,
        "Déclaré" => Domain.Enums.StatutDeclarationTva.Declare,
        _ => Domain.Enums.StatutDeclarationTva.AttenteDeclaration
    };

    // Fully qualified on purpose: inside the field initialiser the name `TypeFacture` would otherwise
    // bind to this field rather than to the enum.
    public static readonly ValueConverter<TypeFacture, string> TypeFacture = new(
        v => ((char)v).ToString(),
        s => string.IsNullOrEmpty(s) ? Domain.Entities.Invoicing.TypeFacture.Facture : (TypeFacture)s[0]);

    public static readonly ValueConverter<NatureLigne, string> NatureLigne =
        new(v => NatureLigneToProvider(v), s => NatureLigneFromProvider(s));

    private static string NatureLigneToProvider(NatureLigne v) => v switch
    {
        Domain.Entities.Invoicing.NatureLigne.Debours => "Débours",
        Domain.Entities.Invoicing.NatureLigne.Assigne => "Assigné",
        Domain.Entities.Invoicing.NatureLigne.TaxeRegionale => "TAXE REGIONALE",
        Domain.Entities.Invoicing.NatureLigne.Charge => "Charge",
        _ => "Produit"
    };

    private static NatureLigne NatureLigneFromProvider(string s) => s switch
    {
        "Débours" => Domain.Entities.Invoicing.NatureLigne.Debours,
        "Assigné" => Domain.Entities.Invoicing.NatureLigne.Assigne,
        "TAXE REGIONALE" => Domain.Entities.Invoicing.NatureLigne.TaxeRegionale,
        "Charge" => Domain.Entities.Invoicing.NatureLigne.Charge,
        _ => Domain.Entities.Invoicing.NatureLigne.Produit
    };

    public static readonly ValueConverter<NatureRepartition, string> NatureRepartition =
        new(v => NatureRepartitionToProvider(v), s => NatureRepartitionFromProvider(s));

    private static string NatureRepartitionToProvider(NatureRepartition v) => v switch
    {
        Domain.Entities.Invoicing.NatureRepartition.Achat => "Achat",
        Domain.Entities.Invoicing.NatureRepartition.Fp => "FP",
        _ => "Vente"
    };

    private static NatureRepartition NatureRepartitionFromProvider(string s) => s switch
    {
        "Achat" => Domain.Entities.Invoicing.NatureRepartition.Achat,
        "FP" => Domain.Entities.Invoicing.NatureRepartition.Fp,
        _ => Domain.Entities.Invoicing.NatureRepartition.Vente
    };

    // ---------------------------------------------------------------------------------------------
    // Shipments (Exploitation). Converter fields are deliberately suffixed so they cannot collide
    // with the enum type names inside the field initialisers.
    // ---------------------------------------------------------------------------------------------

    public static readonly ValueConverter<NatureVoyage, string> NatureVoyageConverter =
        new(v => NatureVoyageToProvider(v), s => NatureVoyageFromProvider(s));

    private static string NatureVoyageToProvider(NatureVoyage v) => v switch
    {
        // The legacy literal really is spelled "Confrére"; preserved verbatim.
        Domain.Entities.Shipments.NatureVoyage.Groupage => "Groupage",
        Domain.Entities.Shipments.NatureVoyage.GroupageConfrere => "Groupage Confrére",
        _ => "Complet"
    };

    private static NatureVoyage NatureVoyageFromProvider(string s) => s switch
    {
        "Groupage" => Domain.Entities.Shipments.NatureVoyage.Groupage,
        "Groupage Confrére" => Domain.Entities.Shipments.NatureVoyage.GroupageConfrere,
        _ => Domain.Entities.Shipments.NatureVoyage.Complet
    };

    public static readonly ValueConverter<CategorieVoyage, string> CategorieVoyageConverter =
        new(v => CategorieVoyageToProvider(v), s => CategorieVoyageFromProvider(s));

    private static string CategorieVoyageToProvider(CategorieVoyage v) => v switch
    {
        Domain.Entities.Shipments.CategorieVoyage.Previsionnel => "PREVISIONNEL",
        Domain.Entities.Shipments.CategorieVoyage.Enlevement => "ENLEVEMENT",
        Domain.Entities.Shipments.CategorieVoyage.Autre => "AUTRE",
        Domain.Entities.Shipments.CategorieVoyage.Mead => "MEAD",
        _ => "INTERNATIONAL"
    };

    private static CategorieVoyage CategorieVoyageFromProvider(string s) => s switch
    {
        "PREVISIONNEL" => Domain.Entities.Shipments.CategorieVoyage.Previsionnel,
        "ENLEVEMENT" => Domain.Entities.Shipments.CategorieVoyage.Enlevement,
        "AUTRE" => Domain.Entities.Shipments.CategorieVoyage.Autre,
        "MEAD" => Domain.Entities.Shipments.CategorieVoyage.Mead,
        _ => Domain.Entities.Shipments.CategorieVoyage.International
    };

    public static readonly ValueConverter<StatutReception, string> StatutReceptionConverter =
        new(v => StatutReceptionToProvider(v), s => StatutReceptionFromProvider(s));

    private static string StatutReceptionToProvider(StatutReception v) => v switch
    {
        Domain.Entities.Shipments.StatutReception.Receptionne => "Réceptionné",
        _ => "Attente Réception"
    };

    private static StatutReception StatutReceptionFromProvider(string s) => s switch
    {
        "Réceptionné" => Domain.Entities.Shipments.StatutReception.Receptionne,
        _ => Domain.Entities.Shipments.StatutReception.AttenteReception
    };

    public static readonly ValueConverter<StatutFacturation, string> StatutFacturationConverter =
        new(v => StatutFacturationToProvider(v), s => StatutFacturationFromProvider(s));

    private static string StatutFacturationToProvider(StatutFacturation v) => v switch
    {
        Domain.Entities.Shipments.StatutFacturation.NonFacture => "Non Facturé",
        Domain.Entities.Shipments.StatutFacturation.Facture => "Facturé",
        _ => "A ne pas facturer"
    };

    private static StatutFacturation StatutFacturationFromProvider(string s) => s switch
    {
        "Non Facturé" => Domain.Entities.Shipments.StatutFacturation.NonFacture,
        "Facturé" => Domain.Entities.Shipments.StatutFacturation.Facture,
        _ => Domain.Entities.Shipments.StatutFacturation.ANePasFacturer
    };

    public static readonly ValueConverter<StatutClotureDossier, string> StatutClotureConverter =
        new(v => StatutClotureToProvider(v), s => StatutClotureFromProvider(s));

    private static string StatutClotureToProvider(StatutClotureDossier v) => v switch
    {
        Domain.Entities.Shipments.StatutClotureDossier.Cloture => "Clôturé",
        _ => "Encours"
    };

    private static StatutClotureDossier StatutClotureFromProvider(string s) => s switch
    {
        "Clôturé" => Domain.Entities.Shipments.StatutClotureDossier.Cloture,
        _ => Domain.Entities.Shipments.StatutClotureDossier.Encours
    };

    public static readonly ValueConverter<LieuDepotage, string> LieuDepotageConverter =
        new(v => LieuDepotageToProvider(v), s => LieuDepotageFromProvider(s));

    private static string LieuDepotageToProvider(LieuDepotage v) => v switch
    {
        Domain.Entities.Shipments.LieuDepotage.Tc => "TC",
        _ => "Remorque"
    };

    private static LieuDepotage LieuDepotageFromProvider(string s) => s switch
    {
        "TC" => Domain.Entities.Shipments.LieuDepotage.Tc,
        _ => Domain.Entities.Shipments.LieuDepotage.Remorque
    };
}
