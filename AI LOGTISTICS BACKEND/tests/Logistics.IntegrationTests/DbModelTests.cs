using Logistics.Domain.Entities.Invoicing;
using Logistics.Domain.Enums;
using Logistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Logistics.IntegrationTests;

/// <summary>
/// Verifies the EF model without needing a database.
/// </summary>
/// <remarks>
/// This is the highest-value test in the migration: the mapping is the part most likely to be wrong,
/// because every table and column name was reverse-engineered from raw SQL literals rather than from a
/// schema dump. Building the model catches missing keys, bad converters, unmapped properties and
/// navigation mistakes at test time instead of at first query.
/// <para>
/// It also pins the legacy names so a future refactor cannot quietly rename <c>t_clients</c> to
/// <c>Clients</c> and break a straight data migration.
/// </para>
/// </remarks>
public sealed class DbModelTests
{
    private static LogisticsDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<LogisticsDbContext>()
            // Never connected to; the model is built from metadata only.
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=LogisticsModelOnly;Trusted_Connection=True;")
            .Options;

        return new LogisticsDbContext(options);
    }

    [Fact]
    public void Model_builds_without_error()
    {
        using var db = NewContext();

        var entityTypes = db.Model.GetEntityTypes().ToList();

        Assert.NotEmpty(entityTypes);
        // 20 reference + 4 client + 5 payment + 4 invoicing + 7 audit = 40 mapped types.
        Assert.True(entityTypes.Count >= 38, $"Expected at least 38 mapped entity types but found {entityTypes.Count}.");
    }

    [Theory]
    // Reference data
    [InlineData("Societe", "t_societe")]
    [InlineData("Agence", "t_agence")]
    [InlineData("Pays", "T_PAYS")]
    [InlineData("ZonePays", "T_ZONE_PAYS")]
    [InlineData("Ville", "T_VILLE")]
    [InlineData("Devise", "t_devise")]
    [InlineData("Port", "t_port")]
    [InlineData("Incoterm", "t_incoterm")]
    [InlineData("TypeDossier", "t_type_dossier")]
    [InlineData("ModePaiement", "t_mode_paiement")]
    [InlineData("CompteTva", "t_compte_tva")]
    [InlineData("NatureFrais", "t_nature_frais")]
    [InlineData("ClasseClient", "t_classe_client")]
    [InlineData("StatutClient", "t_statut_client")]
    [InlineData("Risque", "t_risque")]
    [InlineData("SecteurActivite", "t_secteur_activite")]
    [InlineData("GroupeSociete", "t_groupe_societe")]
    [InlineData("Tresorie", "t_tresorie")]
    [InlineData("NatureFraisBancaire", "T_NATURE_FRAIS_BANCAIRE")]
    [InlineData("TypeIncidentPaiement", "T_TYPE_INCIDENT_PAIEMENT")]
    // Clients
    [InlineData("Client", "t_clients")]
    [InlineData("AdresseClient", "t_adresse_client")]
    [InlineData("TiersContact", "t_contact_tiers")]
    [InlineData("BanqueTiers", "t_banque_tiers")]
    // Payments
    [InlineData("EnteteReglement", "t_entete_reglement")]
    [InlineData("DetailReglement", "t_detail_reglement")]
    [InlineData("FraisBancaire", "T_FRAIS_BANCAIRES")]
    [InlineData("EnteteRemise", "t_entete_remise")]
    [InlineData("TauxChange", "t_taux_change")]
    // Invoicing
    [InlineData("EnteteFactureVente", "T_ENTETE_FACTURE_VENTE")]
    [InlineData("DetailFactureVente", "T_DETAIL_FACTURE_VENTE")]
    [InlineData("TvaFactureVente", "T_TVA_FACTURE_VENTE")]
    [InlineData("RepartitionDossier", "T_REPARTITION_DOSSIER")]
    // Audit
    [InlineData("HistReferentiel", "t_hist_referentiel")]
    [InlineData("EvenementApp", "t_evenement_app")]
    [InlineData("TraceClient", "t_trace_client")]
    [InlineData("TraceReglement", "t_trace_reglement")]
    [InlineData("TraceFactureVente", "t_trace_facture_vente")]
    [InlineData("TraceDossier", "t_trace_dossier")]
    [InlineData("TraceVoyage", "t_trace_voyage")]
    public void Entity_maps_to_the_legacy_table_name(string entityName, string expectedTable)
    {
        using var db = NewContext();

        var entityType = db.Model.GetEntityTypes().SingleOrDefault(t => t.ClrType.Name == entityName);

        Assert.NotNull(entityType);
        Assert.Equal(expectedTable, entityType!.GetTableName());
    }

    [Theory]
    // KeyClient / KeyReglement are computed from Id, and Id is the mapped property.
    [InlineData("Client", "Id", "KEY_CLIENT")]
    [InlineData("Client", "RaisonSociale", "RAISON_SOCIALE")]
    [InlineData("Client", "SocieteKey", "SOC_CLIENT")]
    [InlineData("Client", "Supprime", "SUPPRIMER")]
    [InlineData("Client", "CompteComptable", "COMPTE_COMPTABLE")]
    [InlineData("Agence", "Supprime", "supp")]
    [InlineData("Pays", "Alpha2", "ALPHA2")]
    [InlineData("EnteteReglement", "Id", "key_reglement")]
    [InlineData("EnteteReglement", "SocieteKey", "soc_reglement")]
    // The legacy schema really does misspell this one.
    [InlineData("EnteteReglement", "ComissionBanque", "comission_banque")]
    [InlineData("EnteteReglement", "MontantDevise", "montant_devise")]
    [InlineData("DetailReglement", "Sens", "sens")]
    [InlineData("FraisBancaire", "SocieteKey", "soc_frais_bancaire")]
    [InlineData("EnteteFactureVente", "Id", "KEY_FACTURE")]
    [InlineData("EnteteFactureVente", "SocieteKey", "SOC_FACT")]
    [InlineData("EnteteFactureVente", "NetAPayer", "NET_a_payer")]
    [InlineData("EnteteFactureVente", "HtMad", "HT_MAD")]
    [InlineData("DetailFactureVente", "NatureLigne", "nature_Rubrique")]
    [InlineData("RepartitionDossier", "KeyVoyageInter", "key_voy_inter")]
    [InlineData("RepartitionDossier", "KeyDossierInter", "key_dossier_inter")]
    [InlineData("RepartitionDossier", "KeyLigneTaxation", "Key_ligne_taxation")]
    public void Property_maps_to_the_legacy_column_name(string entityName, string propertyName, string expectedColumn)
    {
        using var db = NewContext();

        var entityType = db.Model.GetEntityTypes().Single(t => t.ClrType.Name == entityName);
        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(expectedColumn, property!.GetColumnName());
    }

    [Fact]
    public void Composite_keys_follow_the_legacy_logical_keys()
    {
        using var db = NewContext();

        // t_detail_reglement is keyed by (key_reglement, key_facture).
        var detail = db.Model.GetEntityTypes().Single(t => t.ClrType.Name == "DetailReglement");
        Assert.Equal(
            ["KeyReglement", "KeyFacture"],
            detail.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());

        // t_compte_tva is keyed by (code_tva, tva_soc).
        var tva = db.Model.GetEntityTypes().Single(t => t.ClrType.Name == "CompteTva");
        Assert.Equal(
            ["Id", "SocieteKey"],
            tva.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());

        // t_banque_tiers is keyed by (key_banque, key_tier).
        var banque = db.Model.GetEntityTypes().Single(t => t.ClrType.Name == "BanqueTiers");
        Assert.Equal(
            ["Id", "KeyTier"],
            banque.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());

        // t_tva_facture_vente is keyed by (key_facture, code_tva).
        var tvaFacture = db.Model.GetEntityTypes().Single(t => t.ClrType.Name == "TvaFactureVente");
        Assert.Equal(
            ["KeyFacture", "CodeTva"],
            tvaFacture.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
    }

    [Theory]
    [InlineData("EnteteReglement", "Statut", "Remis en banque", StatutReglement.RemisEnBanque)]
    [InlineData("EnteteReglement", "Statut", "Signé", StatutReglement.Signe)]
    [InlineData("EnteteReglement", "Statut", "Réglé", StatutReglement.Regle)]
    [InlineData("EnteteReglement", "Statut", "Impayé", StatutReglement.Impaye)]
    [InlineData("EnteteReglement", "Statut", "Saisie", StatutReglement.Saisie)]
    public void Status_converter_round_trips_the_exact_legacy_literals(
        string entityName, string propertyName, string legacyLiteral, object expected)
    {
        // Accents and spaces are load-bearing: the desktop application compares these literals
        // directly, so a wrong mapping silently breaks a filter instead of throwing.
        using var db = NewContext();

        var entityType = db.Model.GetEntityTypes().Single(t => t.ClrType.Name == entityName);
        var property = entityType.FindProperty(propertyName)!;
        var converter = property.GetValueConverter();

        Assert.NotNull(converter);

        var fromModel = converter!.ConvertToProvider(expected);
        Assert.Equal(legacyLiteral, fromModel);

        var backToModel = converter.ConvertFromProvider(legacyLiteral);
        Assert.Equal(expected, backToModel);
    }

    [Theory]
    [InlineData("DetailFactureVente", "NatureLigne", "Débours", NatureLigne.Debours)]
    [InlineData("DetailFactureVente", "NatureLigne", "Assigné", NatureLigne.Assigne)]
    [InlineData("DetailFactureVente", "NatureLigne", "TAXE REGIONALE", NatureLigne.TaxeRegionale)]
    [InlineData("DetailFactureVente", "NatureLigne", "Produit", NatureLigne.Produit)]
    [InlineData("DetailReglement", "Sens", "V", SensReglement.Vente)]
    [InlineData("DetailReglement", "Sens", "A", SensReglement.Achat)]
    [InlineData("Tresorie", "TypeTresorie", "B", TypeTresorie.Banque)]
    [InlineData("Tresorie", "TypeTresorie", "C", TypeTresorie.Caisse)]
    [InlineData("EnteteFactureVente", "TypeFacture", "F", TypeFacture.Facture)]
    [InlineData("EnteteFactureVente", "TypeFacture", "A", TypeFacture.Avoir)]
    [InlineData("EnteteFactureVente", "TypeFacture", "O", TypeFacture.Od)]
    [InlineData("RepartitionDossier", "NatureRepartition", "Vente", NatureRepartition.Vente)]
    [InlineData("RepartitionDossier", "NatureRepartition", "Achat", NatureRepartition.Achat)]
    public void Other_enum_converters_round_trip_their_legacy_literals(
        string entityName, string propertyName, string legacyLiteral, object expected)
    {
        using var db = NewContext();

        var entityType = db.Model.GetEntityTypes().Single(t => t.ClrType.Name == entityName);
        var converter = entityType.FindProperty(propertyName)!.GetValueConverter();

        Assert.NotNull(converter);
        Assert.Equal(legacyLiteral, converter!.ConvertToProvider(expected));
        Assert.Equal(expected, converter.ConvertFromProvider(legacyLiteral));
    }

    [Fact]
    public void Client_children_are_reached_through_navigations()
    {
        using var db = NewContext();

        var client = db.Model.GetEntityTypes().Single(t => t.ClrType.Name == "Client");

        Assert.Contains(client.GetNavigations(), n => n.Name == nameof(Domain.Entities.Clients.Client.Adresses));
        Assert.Contains(client.GetNavigations(), n => n.Name == nameof(Domain.Entities.Clients.Client.Banques));
    }

    [Fact]
    public void Soft_delete_tables_declare_a_query_filter()
    {
        using var db = NewContext();

        // A missing filter here means soft-deleted rows would leak into every read — exactly the bug
        // the legacy code avoided by remembering to append `supprimer=0` by hand.
        foreach (var name in new[] { "Client", "Agence", "EnteteReglement", "FraisBancaire" })
        {
            var entityType = db.Model.GetEntityTypes().Single(t => t.ClrType.Name == name);
            Assert.NotNull(entityType.GetQueryFilter());
        }
    }
}
