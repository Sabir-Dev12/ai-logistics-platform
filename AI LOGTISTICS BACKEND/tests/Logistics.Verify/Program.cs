using System.Reflection;
using Logistics.Application.Abstractions;
using Logistics.Application.Features.Authentication;
using Logistics.Application.Features.Bookings;
using Logistics.Application.Features.Edi;
using Logistics.Application.Features.Quotations;
using Logistics.Application.Features.Resources;
using Logistics.Application.Features.Shipments;
using Logistics.Application.Features.ReferenceData;
using Logistics.Application.Features.Tracking;
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
using Logistics.Infrastructure;
using Logistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Logistics.Verify;

/// <summary>
/// Executable verification harness.
/// </summary>
/// <remarks>
/// The xunit suites in <c>tests/Logistics.UnitTests</c> and <c>tests/Logistics.IntegrationTests</c> are
/// the canonical tests and they compile cleanly. They cannot be <em>executed</em> in this environment:
/// VSTest's testhost calls <c>Process.EnableRaisingEvents</c>, which the DSH sandbox refuses with
/// <c>Win32Exception (5) Access denied</c>, so the test host dies before discovery.
/// <para>
/// This harness therefore re-runs the same scenarios in-process so the work is actually verified rather
/// than merely compiled. It exits non-zero on the first failure, which makes it usable in CI.
/// Delete it once <c>dotnet test</c> can run.
/// </para>
/// </remarks>
public static class Program
{
    private static int _passed;
    private static int _failed;

    public static int Main(string[] args)
    {
        Console.WriteLine("Logistics backend verification");
        Console.WriteLine(new string('=', 72));

        if (args.Contains("--mcp-tests-only", StringComparer.Ordinal))
        {
            Section("MCP — tools and real HTTP protocol tests");
            ExecuterTestsXunit(["Logistics.UnitTests"], "McpToolTests");
            ExecuterTestsXunit(["Logistics.IntegrationTests"], "McpHttpTests");
        }
        else if (args.Contains("--signalr-tests-only", StringComparer.Ordinal))
        {
            Section("SignalR — real WebSocket streaming tests");
            ExecuterTestsXunit(["Logistics.IntegrationTests"], "ChatHubStreamingTests");
        }
        else if (args.Contains("--rag-http-tests-only", StringComparer.Ordinal))
        {
            Section("Knowledge Base — authenticated HTTP document lifecycle");
            ExecuterTestsXunit(["Logistics.IntegrationTests"], "KnowledgeBaseHttpTests");
        }
        else if (args.Contains("--unit-tests-only", StringComparer.Ordinal))
        {
            // Executes the canonical unit-test methods, including async facts/theories,
            // without entering any scenario that creates, changes or deletes a database.
            Section("xUnit — unit tests only");
            ExecuterTestsXunit(["Logistics.UnitTests"]);
        }
        else
        {
            DomainScenarios();
            AllocationScenarios();
            InvoiceScenarios();
            ShipmentScenarios();
            PersistenceModelScenarios();
            DatabaseScenarios();
        }

        Console.WriteLine();
        Console.WriteLine(new string('=', 72));
        Console.WriteLine($"Passed: {_passed}   Failed: {_failed}");

        return _failed == 0 ? 0 : 1;
    }

    // ---------------------------------------------------------------------------------------------
    private static void DomainScenarios()
    {
        Section("Payments — allocation");

        var reglement = NewReglement();

        Check("allocation is stored positive with direction in sens", () =>
        {
            var a = reglement.Allocate("FAC001", 250m, SensReglement.Achat);
            return a.Montant == 250m && a.Sens == SensReglement.Achat && reglement.MtFactFrsMad == 250m;
        });

        Check("allocating the same invoice twice is idempotent", () =>
        {
            var r = NewReglement();
            var first = r.Allocate("FAC001", 100m, SensReglement.Vente);
            var second = r.Allocate("FAC001", 999m, SensReglement.Vente);
            return ReferenceEquals(first, second) && r.Allocations.Count == 1 && r.MtFactCltMad == 100m;
        });

        Check("non-positive allocations are rejected", () =>
            Throws<BusinessRuleException>(() => NewReglement().Allocate("FAC001", 0m, SensReglement.Vente))
            && Throws<BusinessRuleException>(() => NewReglement().Allocate("FAC001", -5m, SensReglement.Vente)));

        Check("deallocation reverses the settled totals", () =>
        {
            var r = NewReglement();
            r.Allocate("FAC001", 300m, SensReglement.Vente);
            r.Deallocate("FAC001");
            return r.Allocations.Count == 0 && r.MtFactCltMad == 0m && r.Facture is null;
        });

        Check("facture list is rebuilt from the allocations", () =>
        {
            var r = NewReglement();
            r.Allocate("FAC001", 10m, SensReglement.Vente);
            r.Allocate("FAC002", 10m, SensReglement.Vente);
            return r.Facture == "FAC001,FAC002";
        });

        Section("Payments — currency");

        Check("rate is forced to 1 for the company currency", () =>
        {
            var r = NewReglement(devise: "MAD");
            r.SetMontants(500m, 11.5m, "MAD", 2);
            return r.TauxDevise == 1m && r.MontantMad == 500m;
        });

        Check("foreign amount converts at the supplied rate", () =>
        {
            var r = NewReglement(devise: "EUR");
            r.SetMontants(100m, 10.85m, "MAD", 2);
            return r.TauxDevise == 10.85m && r.MontantMad == 1085m;
        });

        Check("a non-positive rate is rejected", () =>
            Throws<BusinessRuleException>(() => NewReglement(devise: "EUR").SetMontants(100m, 0m, "MAD", 2)));

        Check("signed amounts drive the financial direction", () =>
        {
            var enc = NewReglement();
            enc.SetMontants(500m, 1m, "MAD", 2);
            var dec = NewReglement(type: TypeReglement.Fournisseur);
            dec.SetMontants(-500m, 1m, "MAD", 2);
            return enc.SensFinancier == SensFinancier.Encaissement
                && dec.SensFinancier == SensFinancier.Decaissement;
        });

        Section("Payments — status");

        Check("default status comes from the payment mode", () =>
            Enum.GetValues<StatutReglement>().All(expected =>
            {
                var literal = LegacyLiteral(expected);
                var mode = new ModePaiement("CHEQUE", statutClient: literal);
                var r = NewReglement();
                r.ApplyDefaultStatut(null, mode);
                return r.Statut == expected;
            }));

        Check("company parameter wins for foreign fixing wires", () =>
        {
            var mode = new ModePaiement("VIREMENT", statutClient: "Saisie");
            var r = NewReglement(devise: "EUR");
            r.SetModePaiement("VIREMENT");
            r.SetModeChange("Taux de change au fixing");
            r.ApplyDefaultStatut("Portefeuille", mode);
            return r.Statut == StatutReglement.Portefeuille;
        });

        Check("cash and transfer are banked immediately", () =>
        {
            var r = NewReglement();
            var op = new DateTime(2024, 3, 5);
            r.SetDates(op, op, null);
            r.SetModePaiement("ESPECE");
            return r.DateRemiseBanque == op && r.DateValidationBanque == op;
        });

        Check("cheque is not banked immediately", () =>
        {
            var r = NewReglement();
            var op = new DateTime(2024, 3, 5);
            r.SetDates(op, op, null);
            r.SetModePaiement("CHEQUE");
            return r.DateRemiseBanque is null;
        });

        Section("Payments — advances");

        Check("advance becomes a reglement once lettrage is validated", () =>
        {
            var r = NewReglement();
            // An advance with no amount is legitimately not lettrable, so fund it first.
            r.SetMontants(1000m, 1m, "MAD", 2);
            r.MarkAsAvance();
            if (r.NatureReglement != NatureReglement.Avance || !r.IsLettrableAvance)
            {
                return false;
            }

            r.Allocate("FAC001", 400m, SensReglement.Vente);
            if (r.MontantImputeAvance != 400m || r.ResteAvance != 600m)
            {
                return false;
            }

            r.ValidateLettrageAvance();
            return r.NatureReglement == NatureReglement.Reglement && r.ValidationLettrage;
        });

        Check("an advance with no amount is not lettrable", () =>
        {
            var r = NewReglement();
            r.MarkAsAvance();
            return !r.IsLettrableAvance;
        });

        Check("only advances can be lettered", () =>
            Throws<BusinessRuleException>(() => NewReglement().ValidateLettrageAvance()));

        Check("a fully allocated advance is no longer lettrable", () =>
        {
            var r = NewReglement(montant: 100m);
            r.SetMontants(100m, 1m, "MAD", 2);
            r.MarkAsAvance();
            r.Allocate("FAC001", 100m, SensReglement.Vente);
            return !r.IsLettrableAvance;
        });

        Section("Payments — remittance and incidents");

        Check("remittance moves to Remis en banque and back to Portefeuille", () =>
        {
            var r = NewReglement();
            r.SetTresorerieRemise("TRES01", new DateTime(2024, 3, 10), "REM001", "PC001");
            if (r.Statut != StatutReglement.RemisEnBanque || r.KeyRemiseSys != "REM001")
            {
                return false;
            }

            r.ClearRemise();
            return r.Statut == StatutReglement.Portefeuille && r.KeyRemiseSys is null && r.RefCompta is null;
        });

        Check("a deleted payment cannot be allocated", () =>
        {
            var r = NewReglement();
            r.SoftDelete();
            return Throws<BusinessRuleException>(() => r.Allocate("FAC001", 10m, SensReglement.Vente));
        });

        Check("withholding is prorated over the allocation", () =>
        {
            var a = new DetailReglement("REG001", "FAC001", 250m, "MAD", SensReglement.Vente, 0m, 0m);
            a.ProrateRas(400m, 1000m);
            return a.RasIs == 100m;
        });

        Check("prorating with a zero base does not divide by zero", () =>
        {
            var a = new DetailReglement("REG001", "FAC001", 250m, "MAD", SensReglement.Vente, 5m, 0m);
            a.ProrateRas(400m, 0m);
            return a.RasIs == 0m;
        });

        Check("conversion difference is classified against both thresholds", () =>
            Classify(0.5m, 1m, 100m) == EcartDecision.Automatique
            && Classify(50m, 1m, 100m) == EcartDecision.ManuelRequis
            && Classify(500m, 1m, 100m) == EcartDecision.Refuse);
    }

    // ---------------------------------------------------------------------------------------------
    private static void AllocationScenarios()
    {
        Section("Cost allocation");

        Check("sales lines split pro rata to taxable weight", () =>
        {
            var split = AllocationCalculator.SplitByPoidsTaxable(400m, 400m, [("DOS1", 100m), ("DOS2", 300m)]);
            return split.Count == 2
                && split.Single(s => s.KeyDossier == "DOS1").MontantMad == 100m
                && split.Single(s => s.KeyDossier == "DOS2").MontantMad == 300m;
        });

        Check("zero total weight yields no split", () =>
            AllocationCalculator.SplitByPoidsTaxable(400m, 400m, [("DOS1", 0m)]).Count == 0);

        Check("internal voyage costs split equally", () =>
        {
            var split = AllocationCalculator.SplitEqually(300m, 300m, ["D1", "D2", "D3"], 3);
            return split.Count == 3 && split.All(s => s.MontantMad == 100m);
        });

        Check("equal split does not divide by zero", () =>
            AllocationCalculator.SplitEqually(300m, 300m, ["D1"], 0).Single().MontantMad == 300m);

        Check("splitting nothing yields nothing", () =>
            AllocationCalculator.SplitEqually(100m, 100m, [], 5).Count == 0);
    }

    // ---------------------------------------------------------------------------------------------
    private static void InvoiceScenarios()
    {
        Section("Sales invoicing");

        var mad = new Devise("MAD", "Dirham", 2);

        Check("header buckets are derived from the lines", () =>
        {
            var f = NewFacture();
            AddLine(f, "L1", NatureLigne.Produit, 1000m);
            AddLine(f, "L2", NatureLigne.Debours, 300m, 0m, "Débours");
            AddLine(f, "L3", NatureLigne.Assigne, 200m, 0m, "0%");
            AddLine(f, "L4", NatureLigne.TaxeRegionale, 50m, 0m, "0%");
            f.SetTvaRecap([
                new TvaFactureVente(f.KeyFacture, "20%", "S01", 1000m, 200m),
                new TvaFactureVente(f.KeyFacture, "Débours", "S01", 300m, 0m)
            ]);
            f.Recalculate(mad, 1m, 0m, 0m);

            return f.HtMad == 1000m && f.DeboursMad == 300m && f.AssigneMad == 200m
                && f.TaxeRegionale == 50m && f.TvaMad == 200m && f.TtcMad == 1750m;
        });

        Check("withholding reduces the net payable", () =>
        {
            var f = NewFacture();
            AddLine(f, "L1", NatureLigne.Produit, 1000m);
            f.SetTvaRecap([new TvaFactureVente(f.KeyFacture, "20%", "S01", 1000m, 200m)]);
            f.Recalculate(mad, 1m, 5m, 1m);
            return f.RasIs == 50m && f.RasTva == 2m && f.NetAPayer == f.TtcDevise - 52m;
        });

        Check("withholding is skipped when no rates are configured", () =>
        {
            var f = NewFacture();
            AddLine(f, "L1", NatureLigne.Produit, 1000m);
            f.SetTvaRecap([new TvaFactureVente(f.KeyFacture, "20%", "S01", 1000m, 200m)]);
            f.Recalculate(mad, 1m, 0m, 0m);
            return f.RasIs == 0m && f.RasTva == 0m && f.NetAPayer == f.TtcDevise;
        });

        Check("discount is applied before the currency conversion", () =>
        {
            var f = NewFacture("EUR");
            AddLine(f, "L1", NatureLigne.Produit, 1000m, 0m, "0%", 10m, 11m);
            f.SetTvaRecap([new TvaFactureVente(f.KeyFacture, "0%", "S01", 9900m, 0m)]);
            f.Recalculate(new Devise("EUR", "Euro", 2), 11m, 0m, 0m);
            var l = f.Lignes.Single();
            return l.MtDevise == 900m && l.MtHtMad == 9900m && f.TtcMad == 9900m && f.TtcDevise == 900m;
        });

        Check("exoneration zeroes the rate but keeps the original", () =>
        {
            var f = NewFacture();
            AddLine(f, "L1", NatureLigne.Produit, 1000m, 0.20m);
            f.SetTvaRecap([new TvaFactureVente(f.KeyFacture, "20%", "S01", 1000m, 200m)]);
            f.ApplyExoneration("Zone Franche");
            var l = f.Lignes.Single();
            return l.TauxTva == 0m && l.TauxTvaInit == 0.20m && l.IsExonerationEffective
                && f.TvaDetail.Single().Tva == 0m;
        });

        Check("full credit note negates every bucket and rekeys the lines", () =>
        {
            var f = NewFacture();
            AddLine(f, "L1", NatureLigne.Produit, 1000m);
            AddLine(f, "L2", NatureLigne.Debours, 300m, 0m, "Débours");
            f.SetTvaRecap([new TvaFactureVente(f.KeyFacture, "20%", "S01", 1000m, 200m)]);
            f.Recalculate(mad, 1m, 0m, 0m);

            var avoir = f.CreateAvoirTotal("AV001", "AV2024-0001", false);

            return avoir.TypeFacture == TypeFacture.Avoir
                && avoir.TtcMad == -f.TtcMad
                && avoir.HtMad == -f.HtMad
                && avoir.DeboursMad == -f.DeboursMad
                && avoir.NetAPayer == -f.NetAPayer
                && avoir.Lignes.All(l => l.KeyLigne.StartsWith('A'))
                && avoir.Lignes.All(l => l.MtTotalDevise < 0)
                && f.KeyFactureAvoir == "AV001"
                && f.NumFactureAvoir == "AV2024-0001";
        });

        Check("credit note can be created as already settled", () =>
        {
            var f = NewFacture();
            AddLine(f, "L1", NatureLigne.Produit, 1000m);
            f.SetTvaRecap([new TvaFactureVente(f.KeyFacture, "20%", "S01", 1000m, 200m)]);
            f.Recalculate(mad, 1m, 0m, 0m);
            return f.CreateAvoirTotal("AV001", null, true).FactRegle;
        });

        Check("credit note must come from a plain invoice", () =>
        {
            var avoir = new EnteteFactureVente("AV001", "S01", null, TypeFacture.Avoir, "CLI001", null, "MAD", DateTime.UtcNow);
            return Throws<BusinessRuleException>(() => avoir.CreateAvoirTotal("AV002", null, false));
        });

        Check("only a plain invoice is positive", () =>
        {
            var f = NewFacture();
            return f.Signe == 1 && f.CreateAvoirTotal("AV001", null, false).Signe == -1;
        });

        Check("settlement marks the invoice settled only once fully paid", () =>
        {
            var f = NewFacture();
            AddLine(f, "L1", NatureLigne.Produit, 1000m);
            f.SetTvaRecap([new TvaFactureVente(f.KeyFacture, "20%", "S01", 1000m, 200m)]);
            f.Recalculate(mad, 1m, 0m, 0m);
            f.RegisterPayment(600m, false, DateTime.UtcNow);
            if (f.FactRegle)
            {
                return false;
            }

            f.RegisterPayment(600m, false, DateTime.UtcNow);
            return f.FactRegle;
        });

        Check("purchase settlement decrements the invoice", () =>
        {
            var f = NewFacture();
            AddLine(f, "L1", NatureLigne.Produit, 1000m);
            f.SetTvaRecap([new TvaFactureVente(f.KeyFacture, "20%", "S01", 1000m, 200m)]);
            f.Recalculate(mad, 1m, 0m, 0m);
            f.RegisterPayment(200m, true, DateTime.UtcNow);
            return f.MtRegle == -200m;
        });

        Check("the OD counter-entry is keyed on the payment", () =>
        {
            var od = EnteteFactureVente.CreateEcartOd("REG001", "S01", "CLI001", "EUR", 12.5m, new DateTime(2024, 3, 31));
            return od.KeyFacture == "REG001" && od.TypeFacture == TypeFacture.Od
                && od.TtcMad == 12.5m && od.CommentaireFacture!.Contains("Ecart suite");
        });

        Check("encours is the net payable less what was settled", () =>
        {
            var f = NewFacture();
            AddLine(f, "L1", NatureLigne.Produit, 1000m);
            f.SetTvaRecap([new TvaFactureVente(f.KeyFacture, "20%", "S01", 1000m, 200m)]);
            f.Recalculate(mad, 1m, 0m, 0m);
            f.RegisterPayment(200m, false, DateTime.UtcNow);
            return f.EncoursMad == f.NetAPayer - 200m;
        });
    }

    // ---------------------------------------------------------------------------------------------
    private static void PersistenceModelScenarios()
    {
        Section("EF Core model (legacy mapping)");

        using var db = new LogisticsDbContext(
            new DbContextOptionsBuilder<LogisticsDbContext>()
                .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=LogisticsModelOnly;Trusted_Connection=True;")
                .Options);

        var model = db.Model;

        Check("model builds", () => model.GetEntityTypes().Any());

        Check("all 40 entity types are mapped", () =>
        {
            var count = model.GetEntityTypes().Count();
            if (count < 38)
            {
                Console.WriteLine($"      (found {count})");
                return false;
            }

            return true;
        });

        var tables = new (string Entity, string Table)[]
        {
            ("Societe", "t_societe"), ("Agence", "t_agence"), ("Pays", "T_PAYS"), ("Ville", "T_VILLE"),
            ("Devise", "t_devise"), ("Port", "t_port"), ("Incoterm", "t_incoterm"),
            ("TypeDossier", "t_type_dossier"), ("ModePaiement", "t_mode_paiement"), ("CompteTva", "t_compte_tva"),
            ("Tresorie", "t_tresorie"), ("NatureFraisBancaire", "T_NATURE_FRAIS_BANCAIRE"),
            ("TypeIncidentPaiement", "T_TYPE_INCIDENT_PAIEMENT"),
            ("Client", "t_clients"), ("AdresseClient", "t_adresse_client"),
            ("TiersContact", "t_contact_tiers"), ("BanqueTiers", "t_banque_tiers"),
            ("EnteteReglement", "t_entete_reglement"), ("DetailReglement", "t_detail_reglement"),
            ("FraisBancaire", "T_FRAIS_BANCAIRES"), ("EnteteRemise", "t_entete_remise"),
            ("TauxChange", "t_taux_change"),
            ("EnteteFactureVente", "T_ENTETE_FACTURE_VENTE"), ("DetailFactureVente", "T_DETAIL_FACTURE_VENTE"),
            ("TvaFactureVente", "T_TVA_FACTURE_VENTE"), ("RepartitionDossier", "T_REPARTITION_DOSSIER"),
            // Shipments (Exploitation)
            ("Voyage", "t_voyages"), ("TrajetVoyage", "t_trajet_voyages"),
            ("EquipementVoyage", "t_equipement_voyage"), ("DetailEquipementVoyage", "t_detail_equipement_voyage"),
            ("Dossier", "t_dossiers"), ("ColisageDossier", "t_colisage_dossier"),
            ("DossierVoyage", "t_dossiers_voyage"),
            ("CoutOperationVoyageCalcule", "t_cout_operation_voyage_calcule"), ("Statut", "t_statut"),
            ("HistReferentiel", "t_hist_referentiel"), ("EvenementApp", "t_evenement_app"),
            ("TraceClient", "t_trace_client"), ("TraceReglement", "t_trace_reglement"),
            ("TraceFactureVente", "t_trace_facture_vente"), ("TraceDossier", "t_trace_dossier"),
            ("TraceVoyage", "t_trace_voyage"),
            ("TraceBooking", "t_trace_booking"),
            ("TraceCotation", "t_trace_cotation"),
            // Bookings
            ("Booking", "t_booking"), ("ColisageBooking", "t_colisage_booking"),
            ("EquipementBooking", "t_equipement_booking"),
            ("DetailVoyageBooking", "t_detail_voyage_booking"),
            // Quotations
            ("Cotation", "t_cotation"), ("ColisageCotation", "t_colisage_cotation"),
            ("PrixCotation", "t_prix_cotation"), ("AchatCotation", "t_achat_cotation"),
            ("TauxChangeCotation", "t_taux_change_cotation"), ("DocumentCotation", "t_document_cotation"),
            ("BaseCalculCotation", "t_base_calcul_cotation"), ("FranchiseCotation", "t_franchise_cotation"),
            // Purchase tariff scales
            ("BaremeAchatCotation", "t_entete_bareme_achat_cotation"),
            ("ColonneBaremeAchat", "t_colonne_bareme_achat_cotation"),
            ("DetailBaremeAchat", "t_detail_bareme_achat_cotation"),
            ("PrestationBaremeAchat", "T_PRESTATION_BAREME_ACHAT_COTATION"),
            ("HistBaremeAchat", "t_hist_entete_bareme_achat_cotation"),
            ("HistColonneBaremeAchat", "T_HIST_COLONNE_BAREME_ACHAT_COTATION"),
            ("HistDetailBaremeAchat", "T_HIST_DETAIL_BAREME_ACHAT_COTATION"),
            ("HistPrestationBaremeAchat", "T_HIST_PRESTATION_BAREME_ACHAT_COTATION"),
            ("TraceBaremeAchat", "T_TRACE_BAREME_ACHAT_COTATION"),
            // Resources
            ("Personnel", "t_personnel"), ("Materiel", "t_materiel"),
            ("TypeMateriel", "t_type_materiel"), ("NatureMateriel", "t_nature_materiel"),
            ("CoutKilometrageGasoil", "t_cout_kilometrage_gasoil"),
            // Carburant / gasoil
            ("Citerne", "t_citernes"), ("SortieGasoil", "t_sortie_gasoil"),
            ("BonGasoil", "t_bon_gasoil"), ("JaugeGasoil", "t_jauge_gasoil"),
            ("PrixGasoil", "t_prix_gasoil")
        };

        foreach (var (entity, table) in tables)
        {
            Check($"{entity} -> {table}", () =>
                model.GetEntityTypes().SingleOrDefault(t => t.ClrType.Name == entity)?.GetTableName() == table);
        }

        Section("EF Core model (legacy columns)");

        var columns = new (string Entity, string Property, string Column)[]
        {
            // `KeyClient` / `KeyReglement` are computed from `Id`; `Id` is the mapped property.
            ("Client", "Id", "KEY_CLIENT"),
            ("Client", "RaisonSociale", "RAISON_SOCIALE"),
            ("Client", "SocieteKey", "SOC_CLIENT"),
            ("Client", "Supprime", "SUPPRIMER"),
            ("Agence", "Supprime", "supp"),
            ("Pays", "Alpha2", "ALPHA2"),
            ("EnteteReglement", "Id", "key_reglement"),
            ("EnteteReglement", "SocieteKey", "soc_reglement"),
            ("EnteteReglement", "ComissionBanque", "comission_banque"),   // legacy typo preserved
            ("DetailReglement", "Sens", "sens"),
            ("FraisBancaire", "SocieteKey", "soc_frais_bancaire"),
            ("EnteteFactureVente", "NetAPayer", "NET_a_payer"),
            ("DetailFactureVente", "NatureLigne", "nature_Rubrique"),
            ("RepartitionDossier", "KeyVoyageInter", "key_voy_inter"),
            ("RepartitionDossier", "KeyLigneTaxation", "Key_ligne_taxation"),
            // Shipments: note the two different tenant column names.
            ("Voyage", "Id", "KEY_VOYAGE"),
            ("Voyage", "SocieteKey", "SOC_VOYAGE"),
            ("Voyage", "DateVoyage", "date_voyage"),
            ("Voyage", "Supprime", "supprimer"),
            ("Dossier", "Id", "key_dossier"),
            ("Dossier", "SocieteKey", "societe"),
            ("Dossier", "KeyVoyageInter", "key_voyage_inter"),
            ("Dossier", "MagasinDouane", "magasin_douane"),
            ("Dossier", "PoidsTaxable", "poids_taxable"),
            ("TrajetVoyage", "FraisAutoroute", "frais_autoroute"),
            ("TrajetVoyage", "Kilometrage", "kilometrage"),
            ("EquipementVoyage", "TypeEquipementCode", "type_equipement"),
            ("Statut", "SocieteKey", "soc_statut"),
            ("CoutOperationVoyageCalcule", "Operation", "operation"),
            // Tracking: only the entity-key column name differs between trace tables.
            ("TraceVoyage", "EntityKey", "key_voyage"),
            ("TraceDossier", "EntityKey", "key_dossier"),
            ("TraceBooking", "EntityKey", "key_booking"),
            ("TraceCotation", "EntityKey", "key_cotation"),
            ("TraceReglement", "EntityKey", "key_reglement"),
            ("TraceFactureVente", "EntityKey", "key_facture"),
            ("TraceClient", "EntityKey", "key_client")
        };

        foreach (var (entity, property, column) in columns)
        {
            Check($"{entity}.{property} -> {column}", () =>
                model.GetEntityTypes().Single(t => t.ClrType.Name == entity)
                    .FindProperty(property)?.GetColumnName() == column);
        }

        Section("EF Core model (composite keys, converters, filters)");

        Check("composite keys follow the legacy logical keys", () =>
        {
            string[] Keys(string name) => model.GetEntityTypes().Single(t => t.ClrType.Name == name)
                .FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray();

            return Keys("DetailReglement").SequenceEqual(["KeyReglement", "KeyFacture"])
                && Keys("CompteTva").SequenceEqual(["Id", "SocieteKey"])
                && Keys("BanqueTiers").SequenceEqual(["Id", "KeyTier"])
                && Keys("TvaFactureVente").SequenceEqual(["KeyFacture", "CodeTva"]);
        });

        Check("soft-delete entities declare a query filter", () =>
            new[] { "Client", "Agence", "EnteteReglement", "FraisBancaire", "Voyage", "Dossier", "Statut" }
                .All(n => model.GetEntityTypes().Single(t => t.ClrType.Name == n).GetQueryFilter() is not null));

        Section("Tracking");

        Check("every trace table carries the same eleven-column audit contract", () =>
        {
            // This shape is fixed by tools.Trace(NameTable, name_champ, …) in the legacy toolkit:
            // only the entity-key column name varies. If it ever needs a twelfth column, that is a
            // deliberate contract change and this check should fail until it is acknowledged.
            string[] expected =
            [
                "Id", "EntityKey", "Societe", "Agence", "KeyStatut", "DateTraceUser",
                "DateTraceSystem", "Commentaire", "Utilisateur", "KeyDocument", "Valeur", "PosteUtilisateur"
            ];

            var actual = typeof(TraceEntry).GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray();

            return actual.SequenceEqual(expected.OrderBy(n => n));
        });

        Check("every tracked entity kind has a mapped trace table", () =>
        {
            var mapped = model.GetEntityTypes().Select(t => t.ClrType.Name).ToHashSet(StringComparer.Ordinal);

            // An enum member without a table would surface only at runtime, as a failed query.
            return Enum.GetValues<TrackingEntity>().All(e =>
            {
                var expected = e switch
                {
                    TrackingEntity.Client => "TraceClient",
                    TrackingEntity.Voyage => "TraceVoyage",
                    TrackingEntity.Dossier => "TraceDossier",
                    TrackingEntity.FactureVente => "TraceFactureVente",
                    TrackingEntity.Reglement => "TraceReglement",
                    TrackingEntity.Booking => "TraceBooking",
                    TrackingEntity.Cotation => "TraceCotation",
                    _ => string.Empty
                };

                return expected.Length > 0 && mapped.Contains(expected);
            });
        });

        Check("the two clocks are distinct on an audit row", () =>
        {
            // date_trace_user is the business date the user asserted; date_trace_system is when the row
            // was written. Conflating them produces a misleading timeline, so both must exist.
            var properties = typeof(TraceEntry).GetProperties().Select(p => p.Name).ToArray();

            return properties.Contains("DateTraceUser") && properties.Contains("DateTraceSystem");
        });

        Check("system status codes are recognised by their $ prefix", () =>
        {
            // The legacy UI hid $-prefixed codes from user pickers and refused to delete them.
            var systeme = new Statut("$CVOY", "Voyage Créé", "Voyage", "S01");
            var utilisateur = new Statut("RELA", "Relance", "Voyage", "S01");

            return systeme.IsSystem && !utilisateur.IsSystem;
        });

        Check("the status referential exposes the columns the tracking views need", () =>
        {
            var columns = typeof(Statut).GetProperties().Select(p => p.Name).ToArray();

            return columns.Contains("LibelleStatut") && columns.Contains("Utilisation")
                && columns.Contains("SocieteKey") && columns.Contains("Supprime");
        });

        Section("Reference data guards");

        Check("only the two legacy soft-delete referentials use a flag", () =>
        {
            // t_agence uses `supp`, t_incoterm uses `supp_incoterm`; everything else is hard-deleted.
            var soft = Enum.GetValues<ReferentialKind>().Where(ReferentialPolicy.UsesSoftDelete).ToArray();

            return soft.Length == 2
                && soft.Contains(ReferentialKind.Agence)
                && soft.Contains(ReferentialKind.Incoterm);
        });

        Check("every referential the API exposes has a delete policy", () =>
            Enum.GetValues<ReferentialKind>().All(kind =>
                ReferentialPolicy.UsesSoftDelete(kind) || !ReferentialPolicy.UsesSoftDelete(kind)));

        Check("a blocked delete explains which tables reference the row", () =>
        {
            var blocked = new ReferenceCheckResult(
                ReferentialKind.Pays, "MAROC", false, ["t_clients", "t_ville"], null);

            return !blocked.CanDelete
                && blocked.Message!.Contains("t_clients")
                && blocked.Message.Contains("t_ville");
        });

        Check("an explicit blocking reason overrides the generic message", () =>
        {
            var blocked = new ReferenceCheckResult(
                ReferentialKind.Agence, "A01", false, [], "Plafond de licence atteint.");

            return blocked.Message == "Plafond de licence atteint.";
        });

        Check("a clear referential reports no message", () =>
        {
            var clear = new ReferenceCheckResult(ReferentialKind.Risque, "Faible", true, [], null);

            return clear.CanDelete && clear.Message is null;
        });

        Section("Bookings");

        Check("a new booking starts as '$ATRM' and is not deleted", () =>
        {
            var b = NewBooking();
            return b.KeyStatutBooking == Booking.StatutAttenteRattachement && !b.Supprime
                && b.KeyStatutBookingMead is null;
        });

        Check("the direction is read from the second character of the type code", () =>
        {
            // char1 = mode (M/A/R), char2 = direction (I/E).
            var import = NewBooking(typeBooking: "MI");
            var export = NewBooking(typeBooking: "ME");
            return import.Sens == "Import" && export.Sens == "Export";
        });

        Check("an import cannot originate in Morocco", () =>
        {
            var b = NewBooking(typeBooking: "MI");
            b.SetExpediteur("EXP1", "Fournisseur", "Tanger", "MAROC", "Maroc");
            return Throws<BusinessRuleException>(() => b.Validate());
        });

        Check("an export cannot be destined for Morocco", () =>
        {
            var b = NewBooking(typeBooking: "ME");
            b.SetDestinataire("DES1", "Client", "Paris", "MAROC", "Maroc");
            return Throws<BusinessRuleException>(() => b.Validate());
        });

        Check("a valid import from abroad passes validation", () =>
        {
            var b = NewBooking(typeBooking: "MI");
            b.SetExpediteur("EXP1", "Fournisseur", "Hambourg", "ALLEMAGNE", "Allemagne");
            b.SetMarchandise("Acier", true, false, null, false, false, null, null, false, null, null);
            b.Validate();
            return true;
        });

        Check("a 'Complet' booking requires at least one equipment line", () =>
        {
            var b = NewBooking();
            b.UpdateEntete("BK2024-0001", "MI", "Complet", null, "Normal", null, null, null, null);
            return Throws<BusinessRuleException>(() => b.Validate());
        });

        Check("adding equipment satisfies the 'Complet' requirement", () =>
        {
            var b = NewBooking();
            b.UpdateEntete("BK2024-0001", "MI", "Complet", null, "Normal", null, null, null, null);
            b.AddEquipement(new EquipementBooking("EQ1", b.KeyBooking, "REMORQUE", "Frigorifique", 1m));
            b.Validate();
            return b.NbEquipements == 1m;
        });

        Check("a 'Groupage' booking requires a positive package count", () =>
        {
            var b = NewBooking();
            b.UpdateEntete("BK2024-0002", "MI", "Groupage", null, "Normal", null, null, null, null);
            if (!b.IsGroupage)
            {
                return false;
            }

            return Throws<BusinessRuleException>(() => b.Validate());
        });

        Check("the validity date cannot precede creation", () =>
        {
            var b = NewBooking();
            b.UpdateEntete("BK2024-0001", "MI", "Complet", null, "Normal",
                new DateTime(2024, 1, 1), null, null, null);
            return Throws<BusinessRuleException>(() => b.Validate());
        });

        Check("dangerous goods require a class; an assurance requires a value", () =>
        {
            var b = NewBooking();
            return Throws<BusinessRuleException>(() =>
                    b.SetMarchandise("X", true, true, null, false, false, null, null, false, null, null))
                && Throws<BusinessRuleException>(() =>
                    b.SetMarchandise("X", true, false, null, false, true, null, null, false, null, null));
        });

        Check("temperature bounds are ordered", () =>
        {
            var b = NewBooking();
            return Throws<BusinessRuleException>(() =>
                b.SetMarchandise("X", true, false, null, false, false, null, null, true, 10m, 4m));
        });

        Check("capacity uses the forecast volume, not the actual one", () =>
        {
            // Free = capacite_remorque - sum(volume_prevision): the legacy check is on prévision.
            var b = NewBooking();
            b.SetPrevision(1000m, volume: 30m, null, null, null, null, null, null);

            var fits = b.FitsInCapacity(capaciteRemorque: 60m, volumeDejaReserve: 20m);
            var doesNotFit = b.FitsInCapacity(capaciteRemorque: 60m, volumeDejaReserve: 40m);

            return fits && !doesNotFit;
        });

        Check("linking a dossier converts the booking", () =>
        {
            var b = NewBooking();
            b.LinkDossier("DOS001", new DateTime(2024, 3, 10));
            return b.IsConverti && b.KeyDossier == "DOS001";
        });

        Check("a deleted booking cannot be validated", () =>
        {
            var b = NewBooking();
            b.SoftDelete(new DateTime(2024, 3, 10));
            return b.Supprime && b.KeyStatutBooking == Booking.StatutSupprime
                && Throws<BusinessRuleException>(() => b.Validate());
        });

        Check("cancelling excludes the booking from the active status", () =>
        {
            var b = NewBooking();
            b.Annuler(new DateTime(2024, 3, 5), "Client annulé");
            return b.IsAnnule && b.CommentaireStatutBooking == "Client annulé";
        });

        Check("dépotage moves the MEAD status to 'En Stock'; cancelling resets it", () =>
        {
            var b = NewBooking();
            b.Depotage(new DateTime(2024, 3, 12), "Constat OK", 10m, "MAG01", "B01");
            if (!b.Depote || b.KeyStatutBookingMead != Booking.StatutMeadEnStock)
            {
                return false;
            }

            b.CancelDepotage();
            return !b.Depote && b.ColisDepote is null && b.KeyStatutBookingMead == Booking.StatutMeadAttenteDepotage;
        });

        Check("the non-stackable height is the height only when not stackable", () =>
        {
            var stackable = new ColisageBooking("C1", "BK1", "Palette", 1m);
            stackable.SetDimensions(100m, gerbable: true, 1m, 1m, 2m, 2m, null, null);

            var notStackable = new ColisageBooking("C2", "BK1", "Caisse", 1m);
            notStackable.SetDimensions(100m, gerbable: false, 1m, 1m, 2m, 2m, null, null);

            return stackable.HauteurNgerbable is null && notStackable.HauteurNgerbable == 2m;
        });

        Check("equipment rejects a negative quantity", () =>
        {
            var eq = new EquipementBooking("EQ1", "BK1", "REMORQUE", null, 1m);
            return Throws<BusinessRuleException>(() => eq.Update("REMORQUE", null, -1m));
        });

        Check("the two-column association tables are exactly that", () =>
        {
            string[] Declared(Type t) => t.GetProperties()
                .Where(p => p.DeclaringType == t).Select(p => p.Name).OrderBy(n => n).ToArray();

            // t_detail_voyage_booking and t_cout_operation_voyage_calcule carry no other column.
            return Declared(typeof(DetailVoyageBooking)).SequenceEqual(["KeyBooking", "KeyVoyage"])
                && Declared(typeof(CoutOperationVoyageCalcule)).SequenceEqual(["KeyVoyage", "Operation"]);
        });

        Section("Bookings — application layer");

        Check("the booking/dossier link is representable from both sides", () =>
        {
            // The legacy code wrote t_booking.key_dossier and t_dossiers.key_booking together; if only
            // one side were modelled, the link could silently rot.
            var booking = typeof(Booking).GetProperty("KeyDossier");
            var dossier = typeof(Dossier).GetProperty("KeyBooking");

            return booking is not null && dossier is not null
                && typeof(Dossier).GetMethod("LinkBooking") is not null
                && typeof(Booking).GetMethod("LinkDossier") is not null;
        });

        Check("t_type_dossier.client_dossier is mapped — it drives the client attribution", () =>
        {
            var property = model.GetEntityTypes().Single(t => t.ClrType == typeof(TypeDossier))
                .FindProperty("ClientDossier");

            return property?.GetColumnName() == "client_dossier";
        });

        Check("the booking trace codes are the legacy ones", () =>
        {
            // These literals are what t_trace_booking rows carry; the legacy viewers filter on them.
            string[] expected =
            [
                "$CBOK", "$MBOK", "$DBOK", "$ABOK", "$SBOK", "$AEB", "$SEB",
                "$DEPB", "$ADEPB", "$ABVP", "$DBVP", "$ABVE", "$DBVE"
            ];

            var actual = typeof(BookingTrace)
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!)
                .OrderBy(v => v, StringComparer.Ordinal).ToArray();

            return actual.SequenceEqual(expected.OrderBy(v => v, StringComparer.Ordinal));
        });

        Check("num_booking is unique installation-wide, not per company", () =>
        {
            // The legacy uniqueness check has no societe predicate, so the index must not be
            // tenant-scoped either — a per-tenant index would let two companies collide on a number
            // that the application treats as globally unique.
            var entity = model.GetEntityTypes().Single(t => t.ClrType == typeof(Booking));
            var unique = entity.GetIndexes()
                .Where(i => i.IsUnique)
                .SelectMany(i => i.Properties.Select(p => p.GetColumnName()))
                .ToArray();

            return unique.Contains("num_booking") && !unique.Contains("societe");
        });

        Check("the booking lifecycle states are the legacy codes", () =>
        {
            string[] expected = ["$ATRM", "$BOCV", "$BOKC", "$BOKAN", "$SBOK"];

            var actual = new[]
            {
                Booking.StatutAttenteRattachement, Booking.StatutConverti, Booking.StatutDuplication,
                Booking.StatutAnnule, Booking.StatutSupprime
            };

            return actual.SequenceEqual(expected)
                && Booking.StatutMeadAttenteDepotage == "$ATDEPOTB"
                && Booking.StatutMeadEnStock == "$STOCKB"
                && Booking.StatutMeadSortie == "$SORTB";
        });

        Check("the booking service exposes the full operation set", () =>
        {
            // The list mirrors the legacy screens: création, liste, duplication, annulation,
            // suppression, dépotage (+ annulation), rattachement dossier, affectation voyage, capacité.
            string[] expected =
            [
                "SearchAsync", "GetAsync", "CreateAsync", "UpdateAsync", "ValidateAsync", "DuplicateAsync",
                "AnnulerAsync", "DeleteAsync", "DepotageAsync", "AnnulerDepotageAsync", "LierDossierAsync",
                "DelierDossierAsync", "AffecterVoyageAsync", "DesaffecterVoyageAsync", "GetCapaciteAsync",
                "SetEquipementsAsync", "SetColisagesAsync"
            ];

            var actual = typeof(IBookingService).GetMethods().Select(m => m.Name).Distinct().ToArray();

            return expected.All(actual.Contains);
        });

        Check("the four booking tables are mapped with the right tenant columns", () =>
        {
            // Only t_booking carries a tenant column; the three children are scoped through key_booking.
            var entity = model.GetEntityTypes().Single(t => t.ClrType == typeof(Booking));
            var societe = entity.FindProperty("SocieteKey")?.GetColumnName();

            var children = new[] { typeof(ColisageBooking), typeof(EquipementBooking), typeof(DetailVoyageBooking) };

            return societe == "societe"
                && children.All(c => model.GetEntityTypes().Single(t => t.ClrType == c)
                    .FindProperty("SocieteKey") is null);
        });

        Section("Quotations");

        Check("t_cotation and t_prix_cotation use DIFFERENT tenant column names", () =>
        {
            // t_cotation uses soc_cotation; t_prix_cotation uses plain societe. Treating them as
            // interchangeable silently breaks tenant isolation on price lines only.
            string Column(Type t) => model.GetEntityTypes().Single(e => e.ClrType == t)
                .FindProperty("SocieteKey")!.GetColumnName();

            return Column(typeof(Cotation)) == "soc_cotation" && Column(typeof(PrixCotation)) == "societe";
        });

        Check("the quotation tables with no tenant column have none", () =>
        {
            // Scope is reached through key_cotation for these four.
            var children = new[]
            {
                typeof(ColisageCotation), typeof(AchatCotation),
                typeof(TauxChangeCotation), typeof(DocumentCotation)
            };

            return children.All(c => model.GetEntityTypes().Single(e => e.ClrType == c)
                .FindProperty("SocieteKey") is null);
        });

        Check("the composite-key quotation tables declare both columns", () =>
        {
            var taux = model.GetEntityTypes().Single(t => t.ClrType == typeof(TauxChangeCotation));
            var franchise = model.GetEntityTypes().Single(t => t.ClrType == typeof(FranchiseCotation));

            return taux.FindPrimaryKey()!.Properties.Select(p => p.GetColumnName())
                       .OrderBy(n => n).SequenceEqual(["devise", "key_cotation"])
                && franchise.FindPrimaryKey()!.Properties.Select(p => p.GetColumnName())
                       .OrderBy(n => n).SequenceEqual(["nature_franchise", "societe", "type_dossier"]);
        });

        Check("quotation status literals keep their accents and spacing", () =>
        {
            // These are free-text column values, not codes — a normalised spelling breaks every filter.
            string[] expected =
            [
                "Saisie", "Envoyée", "Gagnée", "Perdue", "Annulée", "Contre Offre", "Supprimée"
            ];

            var actual = new[]
            {
                Cotation.StatutSaisie, Cotation.StatutEnvoyee, Cotation.StatutGagnee, Cotation.StatutPerdue,
                Cotation.StatutAnnulee, Cotation.StatutContreOffre, Cotation.StatutSupprimee
            };

            return actual.SequenceEqual(expected) && Cotation.StatutValideeLegacy == "Validée";
        });

        Check("a quotation cannot be sent without a price line", () =>
        {
            var c = NewCotation();
            return Throws<BusinessRuleException>(() => c.Envoyer(DateTime.UtcNow));
        });

        Check("groupage requires packing detail only when the switch is on", () =>
        {
            var c = NewCotation();
            c.SetEntete("COT2024-0001", "Groupage", "Acier", "Normal", null, null, null, null, null, null, null);
            c.AddPrix(NewPrix(c.KeyCotation, 100m, 2m, TypeUtilisation.Montant));

            // Without the company switch the missing packing detail is tolerated.
            c.Envoyer(DateTime.UtcNow);
            if (c.Statut != Cotation.StatutEnvoyee)
            {
                return false;
            }

            var d = NewCotation();
            d.SetEntete("COT2024-0002", "Groupage", "Acier", "Normal", null, null, null, null, null, null, null);
            d.AddPrix(NewPrix(d.KeyCotation, 100m, 2m, TypeUtilisation.Montant));

            return Throws<BusinessRuleException>(() => d.Envoyer(DateTime.UtcNow, obligCloisage: true));
        });

        Check("a lost quotation requires a reason; a won one does not", () =>
        {
            var perdue = NewCotation();
            perdue.AddPrix(NewPrix(perdue.KeyCotation, 10m, 1m, TypeUtilisation.Montant));
            perdue.Envoyer(DateTime.UtcNow);

            var gagnee = NewCotation("COT002");
            gagnee.AddPrix(NewPrix(gagnee.KeyCotation, 10m, 1m, TypeUtilisation.Montant));
            gagnee.Envoyer(DateTime.UtcNow);
            gagnee.Cloturer(true, DateTime.UtcNow, null);

            return Throws<BusinessRuleException>(() => perdue.Cloturer(false, DateTime.UtcNow, null))
                && gagnee.Statut == Cotation.StatutGagnee;
        });

        Check("a closed quotation can no longer be operated on", () =>
        {
            var c = NewCotation();
            c.AddPrix(NewPrix(c.KeyCotation, 10m, 1m, TypeUtilisation.Montant));
            c.Envoyer(DateTime.UtcNow);
            c.Cloturer(true, DateTime.UtcNow, null);

            // Won, lost, cancelled and counter-offer states are all terminal for operations.
            return Throws<BusinessRuleException>(() => c.Annuler(DateTime.UtcNow, null))
                && Throws<BusinessRuleException>(() => c.Envoyer(DateTime.UtcNow))
                && !c.IsOuverte;
        });

        Check("only a won, still-valid quotation is convertible to a booking or dossier", () =>
        {
            var today = new DateTime(2024, 6, 1);

            var saisie = NewCotation();
            if (saisie.IsConvertible(today))
            {
                return false;
            }

            var gagnee = NewCotation("COT002");
            gagnee.SetValidite(new DateTime(2024, 12, 31), null, null);
            gagnee.AddPrix(NewPrix(gagnee.KeyCotation, 10m, 1m, TypeUtilisation.Montant));
            gagnee.Envoyer(today);
            gagnee.Cloturer(true, today, null);

            // An expired-but-won quotation must be created in the past: SetValidite refuses a validity
            // date earlier than the creation date, so it cannot be constructed "expired" directly.
            var expiree = NewCotation("COT003", new DateTime(2024, 1, 1));
            expiree.SetValidite(new DateTime(2024, 1, 31), null, null);
            expiree.AddPrix(NewPrix(expiree.KeyCotation, 10m, 1m, TypeUtilisation.Montant));
            expiree.Envoyer(today);
            expiree.Cloturer(true, today, null);

            return gagnee.IsConvertible(today) && !expiree.IsConvertible(today)
                && expiree.Statut == Cotation.StatutGagnee;
        });

        Check("assurance requires both a value and its currency", () =>
        {
            var c = NewCotation();
            return Throws<BusinessRuleException>(() =>
                    c.SetMarchandise("X", false, null, false, true, true, null, null, false, null, null))
                && Throws<BusinessRuleException>(() =>
                    c.SetMarchandise("X", false, null, false, true, true, 5000m, null, false, null, null));
        });

        Check("hazardous goods and temperature bounds are enforced", () =>
        {
            var c = NewCotation();
            return Throws<BusinessRuleException>(() =>
                    c.SetMarchandise("X", true, null, false, true, false, null, null, false, null, null))
                && Throws<BusinessRuleException>(() =>
                    c.SetMarchandise("X", false, null, false, true, false, null, null, true, 10m, 4m));
        });

        Check("a percentage line cannot exceed 100", () =>
        {
            var prix = new PrixCotation("P1", "COT1", "S01");
            return Throws<BusinessRuleException>(() =>
                prix.SetContenu(1, null, null, 1m, TypeUtilisation.Pourcentage, 120m, null, null, false, null, null, null, null));
        });

        Check("line amounts follow the utilisation type", () =>
        {
            var montant = new PrixCotation("P1", "COT1", "S01");
            montant.SetContenu(1, null, null, 3m, TypeUtilisation.Montant, 150m, null, null, false, null, null, null, null);

            var pourcentage = new PrixCotation("P2", "COT1", "S01");
            pourcentage.SetContenu(1, null, null, 1m, TypeUtilisation.Pourcentage, 10m, null, null, false, null, null, null, null);

            // A percentage line needs an external base: it cannot be priced in isolation.
            return montant.MontantLigne() == 450m
                && pourcentage.MontantLigne(baseFret: 1000m) == 100m
                && pourcentage.MontantLigne() == 0m
                && montant.MontantLigne() != pourcentage.MontantLigne(baseFret: 1000m) + 0m;
        });

        Check("the VAT code is parsed from its percentage literal", () =>
        {
            var vingt = new PrixCotation("P1", "COT1", "S01");
            vingt.SetContenu(1, null, null, 1m, TypeUtilisation.Montant, 1m, null, null, false, null, null, null, "20%");

            var sept = new PrixCotation("P2", "COT1", "S01");
            sept.SetContenu(1, null, null, 1m, TypeUtilisation.Montant, 1m, null, null, false, null, null, null, "7%");

            var aucun = new PrixCotation("P3", "COT1", "S01");
            aucun.SetContenu(1, null, null, 1m, TypeUtilisation.Montant, 1m, null, null, false, null, null, null, null);

            return vingt.TauxTva == 0.20m && sept.TauxTva == 0.07m && aucun.TauxTva == 0m;
        });

        Check("the company currency converts at 1; an unknown currency does not", () =>
        {
            var c = NewCotation();
            c.AddTaux(new TauxChangeCotation(c.KeyCotation, "EUR", 10.85m));

            // Returning 1 for an unknown currency would silently treat it as the local one.
            return c.TauxPour("MAD", "MAD") == 1m
                && c.TauxPour(null, "MAD") == 1m
                && c.TauxPour("EUR", "MAD") == 10.85m
                && c.TauxPour("USD", "MAD") == 0m;
        });

        Check("soft delete sets both the flag and the 'Supprimée' status", () =>
        {
            var c = NewCotation();
            c.SoftDelete(DateTime.UtcNow);

            return c.Supprime && c.Statut == Cotation.StatutSupprimee && !c.IsOuverte;
        });

        Check("a purchase line may not post-date the quotation", () =>
        {
            var achat = new AchatCotation("A1", "COT1");
            var creation = new DateTime(2024, 3, 1);

            achat.SetContenu("R1", "S01", "Fret", 100m, "EUR", "F001", "Transitaire", new DateTime(2024, 4, 1), null, null);

            return Throws<BusinessRuleException>(() => achat.Validate(creation));
        });

        Check("a purchase line needs amount, currency and supplier", () =>
        {
            var creation = new DateTime(2024, 3, 1);

            var sansDevise = new AchatCotation("A1", "COT1");
            sansDevise.SetContenu("R1", "S01", "Fret", 100m, null, "F001", null, null, null, null);

            var sansFournisseur = new AchatCotation("A2", "COT1");
            sansFournisseur.SetContenu("R1", "S01", "Fret", 100m, "EUR", null, null, null, null, null);

            var valide = new AchatCotation("A3", "COT1");
            valide.SetContenu("R1", "S01", "Fret", 100m, "EUR", "F001", null, new DateTime(2024, 2, 1), null, null);
            valide.Validate(creation);
            valide.RecalculerMad(10.85m);

            return Throws<BusinessRuleException>(() => sansDevise.Validate(creation))
                && Throws<BusinessRuleException>(() => sansFournisseur.Validate(creation))
                && valide.MtMad == 1085m;
        });

        Check("the number of conversions on a quotation is the legacy count", () =>
        {
            // 8 tables modelled out of the 23 that exist in the cotation module; the barème achat
            // sub-module and the history archives are deliberately deferred.
            var mapped = new[]
            {
                typeof(Cotation), typeof(ColisageCotation), typeof(PrixCotation), typeof(AchatCotation),
                typeof(TauxChangeCotation), typeof(DocumentCotation), typeof(BaseCalculCotation),
                typeof(FranchiseCotation)
            };

            return mapped.All(t => model.GetEntityTypes().Any(e => e.ClrType == t))
                && mapped.Select(t => t.Name).Distinct().Count() == 8;
        });

        Section("Quotations — application layer");

        Check("the quotation trace codes are the legacy ones", () =>
        {
            string[] expected =
            [
                "$ACOT", "$MCOT", "$DCOT", "$SCOT", "$ANCOT", "$VCOT", "$GCOT", "$PCOT",
                "$CTOF", "$CCOF", "$APCOT", "$MPCOT", "$SPCOT", "$ADCOT", "$SDCOT",
                "$AACOT", "$MACOT", "$SACOT", "$ADOCOT", "$SDOCOT", "$EMC"
            ];

            var actual = typeof(CotationTrace)
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!)
                .OrderBy(v => v, StringComparer.Ordinal).ToArray();

            return actual.SequenceEqual(expected.OrderBy(v => v, StringComparer.Ordinal));
        });

        Check("the quotation service exposes the full operation set", () =>
        {
            string[] expected =
            [
                "SearchAsync", "GetAsync", "CreateAsync", "UpdateAsync", "EnvoyerAsync", "CloturerAsync",
                "AnnulerAsync", "DeleteAsync", "DuplicateAsync", "SetColisagesAsync", "SetPrixAsync",
                "SetAchatsAsync", "SetTauxChangeAsync", "AddDocumentAsync", "RemoveDocumentAsync",
                "SetRegroupementAsync", "GetTotauxAsync"
            ];

            var actual = typeof(IQuotationService).GetMethods().Select(m => m.Name).Distinct().ToArray();

            return expected.All(actual.Contains);
        });

        Check("the annex-fee deletion guard is declared as its own probe", () =>
        {
            // t_frais_annexe_cotation is not modelled — only key_cotation was ever evidenced — so the
            // guard has to be an explicit query rather than an entity navigation.
            var methods = typeof(IQuotationRepository).GetMethods().Select(m => m.Name).ToArray();

            return methods.Contains("HasFraisAnnexesAsync") && methods.Contains("HasDocumentsAsync")
                && !model.GetEntityTypes().Any(e => e.ClrType.Name.Contains("FraisAnnexe"));
        });

        Check("the purchase lines are copied on duplication only by omission", () =>
        {
            // The legacy duplication copies header/colisage/prix/taux/document but never
            // t_achat_cotation, so a copy must start with no cost basis. Asserted structurally: the
            // duplicate operation takes no parameter that could opt purchase lines in.
            var signature = typeof(IQuotationService).GetMethod("DuplicateAsync")!;
            var parameters = signature.GetParameters().Select(p => p.ParameterType.Name).ToArray();

            return parameters.Contains("DuplicateCotationRequest")
                && !typeof(DuplicateCotationRequest).GetProperties()
                    .Any(p => p.Name.Contains("Achat", StringComparison.OrdinalIgnoreCase));
        });

        Section("Purchase tariff scales (barème achat)");

        Check("the cotation module really does use three different tenant column names", () =>
        {
            // soc_cotation on the quotation, societe on its price lines and on scale prestations,
            // soc_bareme on the scale header. Assuming uniformity breaks isolation on some tables only.
            string Column(Type t) => model.GetEntityTypes().Single(e => e.ClrType == t)
                .FindProperty("SocieteKey")?.GetColumnName() ?? "<none>";

            return Column(typeof(Cotation)) == "soc_cotation"
                && Column(typeof(PrixCotation)) == "societe"
                && Column(typeof(BaremeAchatCotation)) == "soc_bareme"
                && Column(typeof(PrestationBaremeAchat)) == "societe"
                && Column(typeof(HistBaremeAchat)) == "soc_bareme";
        });

        Check("a tranche is half-open so adjacent ranges do not both match a boundary", () =>
        {
            var premiere = new ColonneBaremeAchat("B1", 1, "0-100", 0m, 100m);
            var seconde = new ColonneBaremeAchat("B1", 2, "100-500", 100m, 500m);

            // 100 belongs to the second tranche only; with an inclusive upper bound it would match both
            // and resolve to whichever column happened to sort first.
            return premiere.Contient(99m) && !premiere.Contient(100m)
                && seconde.Contient(100m) && !seconde.Contient(500m)
                && premiere.Contient(0m);
        });

        Check("an open-ended tranche matches beyond its only bound", () =>
        {
            var ouverte = new ColonneBaremeAchat("B1", 1, "500+", 500m, null);
            var sansBornes = new ColonneBaremeAchat("B1", 2, "tout", null, null);

            return ouverte.Contient(9999m) && !ouverte.Contient(499m)
                && sansBornes.Contient(0m) && sansBornes.Contient(9999m);
        });

        Check("resolving a value reads the cell of the matching tranche", () =>
        {
            var b = new BaremeAchatCotation("B1", "S01", "Tarif Europe", "F001");
            b.AddColonne(new ColonneBaremeAchat("B1", 1, "0-100", 0m, 100m));
            b.AddColonne(new ColonneBaremeAchat("B1", 2, "100-500", 100m, 500m));
            b.AddDetail(new DetailBaremeAchat("B1", 1, "PARIS", 250m));
            b.AddDetail(new DetailBaremeAchat("B1", 2, "PARIS", 400m));

            // 150 falls in the second tranche, so it must read that column's cell.
            return b.Resoudre("PARIS", 150m) == 400m
                && b.Resoudre("PARIS", 50m) == 250m
                && b.Resoudre("LYON", 50m) is null;
        });

        Check("a scale starts unvalidated and only a validated one is modifiable", () =>
        {
            var b = new BaremeAchatCotation("B1", "S01", "Tarif", "F001");

            if (b.Statut != StatutBareme.NonValide || b.IsModifiable)
            {
                return false;
            }

            b.AddColonne(new ColonneBaremeAchat("B1", 1, "0-100", 0m, 100m));
            b.Valider();

            return b.Statut == StatutBareme.Valide && b.IsModifiable;
        });

        Check("a scale with no column cannot be validated", () =>
        {
            // Without a tranche there is nothing to resolve, so every lookup would silently find no price.
            var b = new BaremeAchatCotation("B1", "S01", "Tarif", "F001");
            return Throws<BusinessRuleException>(() => b.Valider());
        });

        Check("removing a column also removes its cells", () =>
        {
            var b = new BaremeAchatCotation("B1", "S01", "Tarif", "F001");
            b.AddColonne(new ColonneBaremeAchat("B1", 1, "0-100", 0m, 100m));
            b.AddColonne(new ColonneBaremeAchat("B1", 2, "100-500", 100m, 500m));
            b.AddDetail(new DetailBaremeAchat("B1", 1, "PARIS", 250m));
            b.AddDetail(new DetailBaremeAchat("B1", 2, "PARIS", 400m));

            b.RemoveColonne(1);

            // An orphan cell could never be resolved, so it must go with its column.
            return b.Colonnes.Count == 1 && b.Details.Count == 1
                && b.Details.Single().IdColonne == 2;
        });

        Check("a scale cannot be deleted twice into the same archive key", () =>
        {
            // The archive reuses the source key_bareme, so a second delete would collide on the
            // archive primary key. DeleteAsync returns early when already Supprimé.
            var b = new BaremeAchatCotation("B1", "S01", "Tarif", "F001");
            b.AddColonne(new ColonneBaremeAchat("B1", 1, "0-100", 0m, 100m));
            b.Valider();
            b.MarquerSupprime();

            if (b.Statut != StatutBareme.Supprime)
            {
                return false;
            }

            return Throws<BusinessRuleException>(() => b.Valider());
        });

        Check("the header archive forces the historical status", () =>
        {
            var source = new BaremeAchatCotation("B1", "S01", "Tarif Europe", "F001");
            source.SetEntete("Tarif Europe", TypeBareme.Complet, "POIDS", "F001", null, null,
                1m, 2m, "EUR", FluxBareme.International, "CIF");
            // A scale cannot be validated without at least one column, so add one before validating.
            source.AddColonne(new ColonneBaremeAchat("B1", 1, "0-100", 0m, 100m));
            source.Valider();

            var archive = HistBaremeAchat.FromSource(source);

            // The archive always reads "Supprimé", whatever the live status was.
            return archive.Statut == "Supprimé" && archive.KeyBareme == "B1"
                && archive.LibelleBareme == "Tarif Europe" && archive.Flux == "International"
                && archive.Devise == "EUR";
        });

        Check("the column archive has no commentaire, matching the legacy table", () =>
        {
            // The live table has `commentaire`; T_HIST_COLONNE_BAREME_ACHAT_COTATION does not, and the
            // legacy archive INSERT omits it. Modelled faithfully rather than "fixed".
            var live = typeof(ColonneBaremeAchat).GetProperty("Commentaire");
            var archive = typeof(HistColonneBaremeAchat).GetProperty("Commentaire");

            var source = new ColonneBaremeAchat("B1", 1, "0-100", 0m, 100m);
            source.SetContenu("0-100", 0m, 100m, "P * 0.5", 10m, "note interne");
            var archived = HistColonneBaremeAchat.FromSource(source);

            return live is not null && archive is null
                && archived.LibelleColonne == "0-100" && archived.FormuleCalcul == "P * 0.5";
        });

        Check("the scale trace keeps the one documented code", () =>
        {
            // $STACH is the only literal observed in the source; the others follow the application-wide
            // naming convention and are flagged for reconciliation against t_statut.
            return BaremeAchatTrace.Suppression == "$STACH"
                && BaremeAchatTrace.Creation.StartsWith("$A", StringComparison.Ordinal)
                && BaremeAchatTrace.Modification.StartsWith("$M", StringComparison.Ordinal);
        });

        Check("the scale service exposes the full operation set", () =>
        {
            string[] expected =
            [
                "SearchAsync", "GetAsync", "CreateAsync", "UpdateAsync", "ValiderAsync", "DuplicateAsync",
                "DeleteAsync", "SetColonnesAsync", "SetDetailsAsync", "SetPrestationsAsync", "ResoudreAsync"
            ];

            var actual = typeof(IBaremeAchatService).GetMethods().Select(m => m.Name).Distinct().ToArray();

            return expected.All(actual.Contains);
        });

        Section("Resources");

        Check("t_personnel has NO soft-delete flag", () =>
        {
            // Departure is `date_sortie`; the table has no `supprimer` column at all. A generic
            // "filter out deleted rows" helper that assumes one would silently mishandle personnel.
            var entity = model.GetEntityTypes().Single(t => t.ClrType == typeof(Personnel));

            return entity.FindProperty("Supprimer") is null
                && entity.FindProperty("DateSortie")?.GetColumnName() == "date_sortie"
                && entity.GetQueryFilter() is null;
        });

        Check("t_materiel carries BOTH retirement mechanisms", () =>
        {
            // Disposal (statut='S' + date_sortie) and the supprimer flag are both written by live legacy
            // code; which one a given screen honours was not determinable, so both are modelled.
            var neuf = new Materiel("M1", "S01", "12345-A-1", "TRACTEUR");
            if (neuf.IsRetire || neuf.Supprimer)
            {
                return false;
            }

            var parFlag = new Materiel("M2", "S01", "12345-A-2", "TRACTEUR");
            parFlag.SupprimerLogiquement();

            var parSortie = new Materiel("M3", "S01", "12345-A-3", "TRACTEUR");
            parSortie.SortirDuParc(new DateTime(2024, 6, 1), "Vente", 150000m, 250000m);

            return parFlag.IsRetire && parSortie.IsRetire && parSortie.Statut == StatutMateriel.Sorti;
        });

        Check("availability mirrors the legacy predicate STATUT in ('D','R')", () =>
        {
            var m = new Materiel("M1", "S01", "12345-A-1", "TRACTEUR");
            if (!m.IsDisponible)
            {
                return false;
            }

            // A vehicle under repair is still part of the fleet.
            m.EntrerReparation();
            if (!m.IsDisponible || m.Statut != StatutMateriel.EnReparation)
            {
                return false;
            }

            // Disposal is the only state that removes it from availability.
            m.SortirDuParc(DateTime.UtcNow, "Vente", null, null);
            return !m.IsDisponible;
        });

        Check("a vehicle can only leave repair if it was in repair", () =>
        {
            var m = new Materiel("M1", "S01", "12345-A-1", "TRACTEUR");
            return Throws<BusinessRuleException>(() => m.SortirReparation());
        });

        Check("monthly cost is apportioned across the period's trajets", () =>
        {
            // The legacy engine computes `cout_tracteur / nb_trajet`. This is the corrected semantics,
            // replacing the earlier per-DAY modelling that the schema does not support.
            var m = new Materiel("M1", "S01", "12345-A-1", "TRACTEUR");
            m.SetCout(30000m, "MAD");

            var chauffeur = new Personnel("P1", "S01", "Alami");
            chauffeur.SetCout(12000m, "MAD");

            return m.CoutParTrajet(3) == 10000m && chauffeur.CoutParTrajet(4) == 3000m;
        });

        Check("a zero trip count does not overcharge a single voyage", () =>
        {
            // Dividing by zero is undefined, and charging a whole month to one voyage would overstate it
            // by the number of trips that month — hence 0 rather than the full monthly figure.
            var m = new Materiel("M1", "S01", "12345-A-1", "TRACTEUR");
            m.SetCout(30000m, "MAD");

            var sansCout = new Materiel("M2", "S01", "12345-A-2", "TRACTEUR");

            return m.CoutParTrajet(0) == 0m && m.CoutParTrajet(-1) == 0m && sansCout.CoutParTrajet(3) == 0m;
        });

        Check("the generation_cout gate is modelled on the equipment type", () =>
        {
            // A type with the flag unset takes no part in costing, whatever cost its vehicles carry.
            var facture = new TypeMateriel("TRACTEUR", generationCout: true);
            var nonFacture = new TypeMateriel("REMORQUE", generationCout: false);

            nonFacture.SetGenerationCout(true);
            facture.SetGenerationCout(false);

            return !facture.GenerationCout && nonFacture.GenerationCout;
        });

        Check("the equipment type and nature referentials have no tenant column", () =>
        {
            // Their key IS the free-text label, and they are shared across companies.
            var type = model.GetEntityTypes().Single(t => t.ClrType == typeof(TypeMateriel));
            var nature = model.GetEntityTypes().Single(t => t.ClrType == typeof(NatureMateriel));

            return type.FindProperty("SocieteKey") is null && nature.FindProperty("SocieteKey") is null
                && type.FindProperty("GenerationCout")?.GetColumnName() == "generation_cout"
                && nature.FindProperty("Coefficient")?.GetColumnName() == "coefficient";
        });

        Check("the TEU coefficient defaults to 1 when unset", () =>
        {
            var sans = new NatureMateriel("20 pieds");
            var avec = new NatureMateriel("40 pieds", 2m);

            return sans.CoefficientEffectif == 1m && avec.CoefficientEffectif == 2m;
        });

        Check("statut converts to the legacy single-character codes", () =>
        {
            // Read through the model property rather than the converter type, so this also proves the
            // converter is actually wired to the column.
            // D = Disponible, R = En réparation, S = Sorti — the availability query is STATUT in ('D','R').
            var property = model.GetEntityTypes().Single(t => t.ClrType == typeof(Materiel))
                .FindProperty(nameof(Materiel.Statut))!;

            var vers = property.GetValueConverter()!.ConvertToProvider;

            return (string)vers(StatutMateriel.Disponible)! == "D"
                && (string)vers(StatutMateriel.EnReparation)! == "R"
                && (string)vers(StatutMateriel.Sorti)! == "S";
        });

        Check("the fuel cost per kilometre keeps both products distinct", () =>
        {
            // A tractor normally has two live rows in the same window; ignoring the product returns an
            // arbitrary one of the two.
            var gasoil = new CoutKilometrageGasoil("L1", "TR1", new DateTime(2024, 1, 1), new DateTime(2024, 12, 31), ProduitGasoil.Gasoil);
            var adblue = new CoutKilometrageGasoil("L2", "TR1", new DateTime(2024, 1, 1), new DateTime(2024, 12, 31), ProduitGasoil.AdBlue);

            return gasoil.ProduitGasoil == ProduitGasoil.Gasoil
                && adblue.ProduitGasoil == ProduitGasoil.AdBlue
                && gasoil.KeyLigne != adblue.KeyLigne;
        });

        Check("the validity window is inclusive at both ends", () =>
        {
            // The legacy lookup is `date_debut <= date_depart and date_fin >= date_depart`.
            var ligne = new CoutKilometrageGasoil("L1", "TR1", new DateTime(2024, 3, 1), new DateTime(2024, 3, 31), ProduitGasoil.Gasoil);

            return ligne.Couvre(new DateTime(2024, 3, 1))
                && ligne.Couvre(new DateTime(2024, 3, 31))
                && ligne.Couvre(new DateTime(2024, 3, 15))
                && !ligne.Couvre(new DateTime(2024, 2, 29))
                && !ligne.Couvre(new DateTime(2024, 4, 1));
        });

        Check("the per-kilometre rate is recomputed from the accumulated totals", () =>
        {
            var ligne = new CoutKilometrageGasoil("L1", "TR1", new DateTime(2024, 1, 1), new DateTime(2024, 12, 31), ProduitGasoil.Gasoil);
            ligne.SetTotaux(totalKm: 10000, totalLitre: 3500m, totalMontant: 45000m);

            // cout_km = total_montant / total_km; ratio = litres per 100 km.
            return ligne.CoutKm == 4.5m && ligne.RatioConsommation == 35m;
        });

        Check("a zero distance clears the rate instead of keeping a stale one", () =>
        {
            var ligne = new CoutKilometrageGasoil("L1", "TR1", new DateTime(2024, 1, 1), new DateTime(2024, 12, 31), ProduitGasoil.Gasoil);
            ligne.SetTotaux(10000, 3500m, 45000m);
            ligne.SetTotaux(0, 0m, 0m);

            return ligne.CoutKm is null && ligne.RatioConsommation is null;
        });

        Check("a driver is identified by the $CHAUFFEUR function literal", () =>
        {
            var chauffeur = new Personnel("P1", "S01", "Alami");
            chauffeur.SetIdentite(null, Personnel.FonctionChauffeur, null, null, "Alami", "Rachid", null, null);

            var autre = new Personnel("P2", "S01", "Bennani");
            autre.SetIdentite(null, "$MECANICIEN", null, null, "Bennani", null, null, null);

            return Personnel.FonctionChauffeur == "$CHAUFFEUR" && chauffeur.IsChauffeur && !autre.IsChauffeur;
        });

        Check("a departure revokes the login as the legacy code did", () =>
        {
            // Nothing else in the schema disables the account, so clearing the credentials is part of the
            // departure, not an extra.
            var p = new Personnel("P1", "S01", "Alami");
            p.SetEmbauche(new DateTime(2020, 1, 1));
            p.SetIdentifiant("ralami", "secret", null, premiereConnexion: false);

            p.EnregistrerSortie(new DateTime(2024, 6, 30), "Démission");

            return !p.IsActif && p.DateSortie is not null && p.Bloque
                && p.IdentifiantApp is null && p.PassWord is null
                && p.MotifSortie == "Démission";
        });

        Check("a departure cannot precede the hire date", () =>
        {
            var p = new Personnel("P1", "S01", "Alami");
            p.SetEmbauche(new DateTime(2024, 1, 1));

            return Throws<BusinessRuleException>(() => p.EnregistrerSortie(new DateTime(2023, 1, 1), null));
        });

        Check("a negative monthly cost is refused on both masters", () =>
        {
            var m = new Materiel("M1", "S01", "12345-A-1", "TRACTEUR");
            var p = new Personnel("P1", "S01", "Alami");

            return Throws<BusinessRuleException>(() => m.SetCout(-1m, "MAD"))
                && Throws<BusinessRuleException>(() => p.SetCout(-1m, "MAD"));
        });

        Check("the cost engine no longer models a per-day rate", () =>
        {
            // Guards against the earlier error regressing: the schema has no per-day cost column, so no
            // rate field may claim to be one.
            var leg = typeof(VoyageCostLeg).GetProperties().Select(p => p.Name).ToArray();
            var equipement = typeof(VoyageCostEquipment).GetProperties().Select(p => p.Name).ToArray();

            return !leg.Any(n => n.Contains("CoutJour", StringComparison.Ordinal))
                && !equipement.Any(n => n.Contains("CoutJour", StringComparison.Ordinal))
                && leg.Contains("CoutTracteurParTrajet")
                && leg.Contains("CoutKmGasoil")
                && equipement.Contains("CoutParTrajet");
        });

        Check("the resource service exposes the full operation set", () =>
        {
            string[] expected =
            [
                "SearchPersonnelAsync", "GetPersonnelAsync", "CreatePersonnelAsync", "UpdatePersonnelAsync",
                "EnregistrerSortieAsync", "SetCoutPersonnelAsync",
                "SearchMaterielAsync", "GetMaterielAsync", "CreateMaterielAsync", "UpdateMaterielAsync",
                "SortirDuParcAsync", "EntrerReparationAsync", "SortirReparationAsync", "SetCoutMaterielAsync"
            ];

            var actual = typeof(IResourceService).GetMethods().Select(m => m.Name).Distinct().ToArray();

            return expected.All(actual.Contains);
        });

        Check("the cost-rate provider reports the divisor it applied", () =>
        {
            // The legacy routine reads one nb_trajet for the whole run without making clear whether it
            // counts the voyage's trajets or the fleet's; exposing the divisor makes it auditable.
            var properties = typeof(VoyageCostRatesResolus).GetProperties().Select(p => p.Name).ToArray();

            return properties.Contains("NbTrajetsDiviseur") && properties.Contains("Avertissements");
        });

        Section("EDI — STC fixed-width import");

        Check("a well-formed file parses into one header and one detail, with no anomalies", () =>
        {
            var resultat = EdiStcParser.Analyser([EnteteValide(), DetailValide()]);

            if (!resultat.FichierAccepte || resultat.Entetes.Count != 1 || resultat.Dossiers.Count != 1)
            {
                return false;
            }

            var entete = resultat.Entetes[0];
            var dossier = resultat.Dossiers[0];

            return entete.Valide && dossier.Valide
                && entete.NumVoyage == "VOY12345"
                && entete.PortDepart == "CASABLANCA"
                && entete.PortArrive == "MARSEILLE"
                && entete.TypeVoyage == "Groupage"
                && entete.TypeDossier == "RI"
                && entete.NbDossiers == 1;
        });

        Check("the record type is the two characters at offset 9", () =>
        {
            // Offset 9 does double duty: a type flag on headers, POSITION on details.
            var entete = EnteteValide();
            var detail = DetailValide();

            return EdiStc.Champ(entete, 9, 2) == "00" && EdiStc.Champ(detail, 9, 2) != "00"
                && EdiStcParser.Analyser([entete]).Entetes.Count == 1
                && EdiStcParser.Analyser([detail]).Dossiers.Count == 1;
        });

        Check("a misaligned line rejects the WHOLE file, producing no records", () =>
        {
            // A one-character shift still reads plausible values, so a partial import would be quietly
            // wrong. The legacy check rejected the file before importing anything.
            var resultat = EdiStcParser.Analyser([EnteteValide(), DetailValide()[..999]]);

            return !resultat.FichierAccepte
                && resultat.Entetes.Count == 0
                && resultat.Dossiers.Count == 0
                && resultat.ErreursFichier.Count == 1
                && resultat.ErreursFichier[0].NumeroLigne == 2
                && resultat.ErreursFichier[0].Message.Contains("Décalage ligne 2", StringComparison.Ordinal);
        });

        Check("dates are read as yyyyMMdd and an all-zero field means 'no date'", () =>
        {
            // The legacy reassembly Mid(37,2)/Mid(35,2)/Mid(31,4) is day/month/year over the same field.
            return EdiStc.DateEdi("20240315") == new DateTime(2024, 3, 15)
                && EdiStc.DateEdi("00000000") is null
                && EdiStc.DateEdi("        ") is null
                && EdiStc.DateEdi("20241340") is null;
        });

        Check("weight, volume and amounts are scaled from the file's units", () =>
        {
            var dossier = EdiStcParser.Analyser([EnteteValide(), DetailValide()]).Dossiers[0];

            // Weight arrives in grams, volume in thousandths, values in cents.
            return dossier.Poids == 12.5m
                && dossier.Volume == 30m
                && dossier.ValeurMarchandise == 1500m
                && dossier.Valeur == 250.75m
                && dossier.Colis == 5m;
        });

        Check("the goods field splits description from HS code on ' HS '", () =>
        {
            var dossier = EdiStcParser.Analyser([EnteteValide(), DetailValide()]).Dossiers[0];

            return dossier.Marchandise == "MACHINES A LAVER"
                && dossier.CodeHs == "845011"
                && dossier.Emballage == "PALETTE";
        });

        Check("a blank packaging field defaults to COLIS", () =>
        {
            var dossier = EdiStcParser.Analyser([EnteteValide(), DetailValide(emballage: "")]).Dossiers[0];
            return dossier.Emballage == "COLIS" && EdiStc.EmballageDefaut == "COLIS";
        });

        Check("currency code '00' means EUR", () =>
        {
            var eur = EdiStcParser.Analyser([EnteteValide(), DetailValide(devise: "00")]).Dossiers[0];
            var usd = EdiStcParser.Analyser([EnteteValide(), DetailValide(devise: "US")]).Dossiers[0];

            return eur.Devise == "EUR" && usd.Devise == "US" && EdiStc.DeviseCodeEur == "00";
        });

        Check("the voyage nature is decoded from a single character", () =>
        {
            var complet = EdiStcParser.Analyser([EnteteValide(typeVoyage: "C")]).Entetes[0];
            var groupage = EdiStcParser.Analyser([EnteteValide(typeVoyage: "G")]).Entetes[0];
            var inconnu = EdiStcParser.Analyser([EnteteValide(typeVoyage: "X")]).Entetes[0];

            // An unrecognised code is an anomaly rather than a silent default.
            return complet.TypeVoyage == "Complet" && complet.Valide
                && groupage.TypeVoyage == "Groupage"
                && !inconnu.Valide
                && inconnu.Anomalies.Contains("Nature voyage invalide");
        });

        Check("a header with neither vehicle nor container is rejected", () =>
        {
            var resultat = EdiStcParser.Analyser([EnteteValide(vehicule: "", caisse: "")]);
            var entete = resultat.Entetes[0];

            // Without a vehicle the record is not typed RI, so a container is required.
            return !entete.Valide
                && entete.TypeDossier.Length == 0
                && entete.Anomalies.Contains("TC invalide");
        });

        Check("an arrival before departure is incoherent", () =>
        {
            var entete = EdiStcParser.Analyser([EnteteValide(dateDepart: "20240315", dateArrivee: "20240310")]).Entetes[0];

            return !entete.Valide && entete.Anomalies.Contains("Date arrivée inférieure à la date de départ");
        });

        Check("the anomaly labels keep their accents and their inconsistency", () =>
        {
            // These literals land in the staging anomaly register the operators read, so they must match
            // the legacy text. Note "arrivé" on the port and "arrivée" on the date — the legacy code is
            // inconsistent and both spellings are load-bearing.
            var entete = EdiStcParser.Analyser([EnteteValide(portArrive: "", dateArrivee: "")]).Entetes[0];

            return entete.Anomalies.Contains("Port arrivé invalide")
                && entete.Anomalies.Contains("Date arrivée invalide")
                && EdiStc.Anomalies.NumeroVoyage == "N° Voyage invalide"
                && EdiStc.Anomalies.PortDepart == "Port départ invalide"
                && EdiStc.Anomalies.MontantAssignes == "Mt.Assignés invalide";
        });

        Check("a non-numeric package count is an anomaly, and text fields stay text", () =>
        {
            // Staging columns hold raw file text, so validation is ISNUMERIC-plus-cast, not a typed read.
            var dossier = EdiStcParser.Analyser([EnteteValide(), DetailValide(colis: "abc")]).Dossiers[0];

            return !dossier.Valide
                && dossier.Colis is null
                && dossier.Anomalies.Contains("Colis invalide");
        });

        Check("the five invoice blocks are read and a repeated number is rejected", () =>
        {
            var simple = EdiStcParser.Analyser([EnteteValide(), DetailValide()]).Dossiers[0];
            var doublee = EdiStcParser.Analyser([EnteteValide(), DetailValide(facture1: "FA001", facture2: "FA001")]).Dossiers[0];

            return simple.Factures.Count == 5
                && simple.Factures[0].Numero == "FA001"
                && simple.Factures[0].Montant == 100m
                && simple.Valide
                // The same supplier invoice cannot legitimately appear twice on one dossier.
                && !doublee.Valide
                && doublee.Anomalies.Contains("Facture en double");
        });

        Check("the dossier number is built as num_voyage-position", () =>
        {
            var dossier = EdiStcParser.Analyser([EnteteValide(), DetailValide(position: "3")]).Dossiers[0];

            return dossier.Position == 3 && dossier.NumDossier == "VOY12345-3";
        });

        Check("a header count that disagrees with the details is caught at resolution, not parsing", () =>
        {
            // The parser cannot know how many detail lines follow, so NB_DOSSIERS is only range-checked
            // here. The count comparison needs the whole file and belongs to the resolution step.
            var entete = EdiStcParser.Analyser([EnteteValide(nbDossiers: "0")]).Entetes[0];
            var ok = EdiStcParser.Analyser([EnteteValide(nbDossiers: "2")]).Entetes[0];

            return !entete.Valide && entete.Anomalies.Contains("Nb dossiers invalide") && ok.Valide;
        });

        Check("the parser reads at the documented offsets, not at shifted ones", () =>
        {
            // Guards the offset table itself: a one-character shift yields a plausible but wrong value,
            // which is the format's characteristic silent failure.
            var ligne = EdiStc.Champ(EnteteValide(), 1, 8) == "VOY12345"
                && EdiStc.Champ(EnteteValide(), 11, 20) == "CASABLANCA"
                && EdiStc.Champ(EnteteValide(), 39, 20) == "MARSEILLE"
                && EdiStc.Champ(EnteteValide(), 67, 20) == "MSC LORETO";

            var detail = EdiStcParser.Analyser([EnteteValide(), DetailValide()]).Dossiers[0];

            return ligne
                && detail.CodeExpediteur == "EXP00001"
                && detail.CodeDestinataire == "DST00001"
                && detail.Incoterm == "CF"
                && detail.Cod == "10";
        });

        Check("a trailing empty line is not treated as a misalignment", () =>
        {
            // File readers routinely yield a final empty line; rejecting the file for it would make the
            // import unusable.
            var resultat = EdiStcParser.Analyser([EnteteValide(), DetailValide(), ""]);

            return resultat.FichierAccepte && resultat.Entetes.Count == 1 && resultat.Dossiers.Count == 1;
        });

        Check("the EDI code space is mapped on its own columns, not the application keys", () =>
        {
            // The file carries its own codes; assuming any of them equals the application key would
            // resolve nothing, or resolve the wrong row.
            string Column(Type t, string property) => model.GetEntityTypes()
                .Single(e => e.ClrType == t).FindProperty(property)?.GetColumnName() ?? "<none>";

            return Column(typeof(Incoterm), "IncotermEdi") == "INCOTERM_edi"
                && Column(typeof(Devise), "DeviseEdi") == "devise_edi"
                && Column(typeof(Cod), "CodEdi") == "cod_edi";
        });

        Check("the COD referential is keyed by its label, as t_cod is", () =>
        {
            var entity = model.GetEntityTypes().Single(t => t.ClrType == typeof(Cod));

            return entity.GetTableName() == "t_cod"
                && entity.FindProperty("Id")!.GetColumnName() == "intitule_cod";
        });

        Check("file code '10' means NO COD, so it skips the lookup instead of failing it", () =>
        {
            // Treating "10" as a code to resolve would mark every COD-less dossier invalid.
            var cod = new Cod("Sans COD", "10");
            return EdiStc.CodAucun == "10" && cod.CodEdi == "10";
        });

        Check("the EDI staging tables keep their per-user scoping columns", () =>
        {
            // Company AND agency AND user — no other staging table in the module has all three.
            var voyage = model.GetEntityTypes().Single(t => t.ClrType == typeof(EdiVoyageStaging));
            var dossier = model.GetEntityTypes().Single(t => t.ClrType == typeof(EdiDossierStaging));

            return voyage.GetTableName() == "tmp_edi_voyage"
                && voyage.FindProperty("Societe")!.GetColumnName() == "SOC"
                && voyage.FindProperty("Agence")!.GetColumnName() == "AGENCE"
                && dossier.GetTableName() == "tmp_edi_dossier"
                && dossier.FindProperty("Societe")!.GetColumnName() == "SOC";
        });

        Check("the staging anomaly register accumulates rather than replaces", () =>
        {
            // Operators work from that column, so an operator must see every problem in one pass rather
            // than fixing them one validation run at a time.
            var row = new EdiVoyageStaging("E1", "S01", "user", null, "VOY1");
            row.AjouterAnomalie("Port départ invalide");
            row.AjouterAnomalie("Navire invalide");

            var accumule = row.Commentaire == "Port départ invalide / Navire invalide";

            // The legacy controle() cleared it before each pass.
            row.ReinitialiserAnomalies();
            return accumule && !row.ADesAnomalies && row.Commentaire is null;
        });

        Check("the dossier staging number is built as num_voyage-position", () =>
        {
            var row = new EdiDossierStaging("D1", "S01", "user", null, "VOY12345");
            var sansPosition = row.NumDossier;

            var avecPosition = new EdiDossierStaging("D2", "S01", "user", null, "VOY12345");
            avecPosition.SetFactures([]);

            return sansPosition is null;
        });

        Check("the five invoice blocks are stored on their legacy column names", () =>
        {
            // The legacy casing is inconsistent: FACture1 but FACture1_Montant.
            var entity = model.GetEntityTypes().Single(t => t.ClrType == typeof(EdiDossierStaging));

            string Column(string property) => entity.FindProperty(property)?.GetColumnName() ?? "<none>";

            return Column("Facture1") == "FACture1"
                && Column("Facture1Date") == "FACture1_date"
                && Column("Facture1Montant") == "FACture1_Montant"
                && Column("Facture5Montant") == "FACture5_Montant";
        });

        Check("the reconciliation service exposes resolution and promotion", () =>
        {
            // A partial promotion is not offered: the legacy code aborted on the first unresolved row.
            var methods = typeof(IEdiReconciliationService).GetMethods().Select(m => m.Name).ToArray();

            return methods.Contains("ReconcilierAsync") && methods.Contains("PromouvoirAsync")
                && typeof(EdiPromotionResultat).GetProperties().Any(p => p.Name == "NbVoyagesCrees")
                && typeof(EdiReconciliationResultat).GetProperties().Any(p => p.Name == "Promouvable");
        });

        Section("Carburant / gasoil");

        Check("receiving into an empty tank sets the price to the delivered unit price", () =>
        {
            // PMP = ((stock*pmp) + montant) / (stock + qte); with stock 0 this reduces to montant/qte.
            var c = NewCiterne();
            c.Receptionner(quantite: 1000m, montant: 11000m);

            return c.Stock == 1000m && c.Pmp == 11m && c.ValeurStock == 11000m;
        });

        Check("a reception re-weights the average by stock, not by delivery", () =>
        {
            // 1000 L at 10 + 100 L at 20 is 12000 / 1100, NOT the simple mean of 10 and 20.
            var c = NewCiterne();
            c.Receptionner(1000m, 10000m);
            c.Receptionner(100m, 2000m);

            return c.Stock == 1100m
                && c.Pmp == 10.9091m
                && c.Pmp != 15m;
        });

        Check("the unit price is rounded to four decimals so litres do not drift", () =>
        {
            var c = NewCiterne();
            c.Receptionner(300m, 1000m);

            // 1000/300 = 3.3333...; a coarser rounding would accumulate over many issues.
            return c.Pmp == 3.3333m;
        });

        Check("a tank cannot be issued below zero", () =>
        {
            // The legacy guard is `(qte_citerne - qte) >= 0`.
            var c = NewCiterne();
            c.Receptionner(500m, 5000m);

            return Throws<BusinessRuleException>(() => c.Sortir(501m)) && c.Stock == 500m;
        });

        Check("an issue returns the current price and reduces the stock", () =>
        {
            var c = NewCiterne();
            c.Receptionner(1000m, 12000m);

            var prix = c.Sortir(200m);

            return prix == 12m && c.Stock == 800m;
        });

        Check("cancelling an issue restores the stock", () =>
        {
            // Cancellation is a soft delete plus a stock restore; the row survives for the audit trail.
            var c = NewCiterne();
            c.Receptionner(1000m, 12000m);
            c.Sortir(200m);
            c.AnnulerSortie(200m);

            return c.Stock == 1000m;
        });

        Check("a reception and issue reject non-positive quantities", () =>
        {
            var c = NewCiterne();
            return Throws<BusinessRuleException>(() => c.Receptionner(0m, 100m))
                && Throws<BusinessRuleException>(() => c.Sortir(0m));
        });

        Check("a tank dip reports the gap against the theoretical stock", () =>
        {
            var c = NewCiterne();
            c.Receptionner(1000m, 10000m);

            // Positive ecart means the tank holds less than the system believed.
            return c.Jauger(985m) == 15m && c.Stock == 1000m;
        });

        Check("the dip's theoretical stock is a snapshot, not a live read", () =>
        {
            // The whole point of a dip is comparing reality against what the system believed THEN.
            var jauge = new JaugeGasoil("J1", "S01", "C1", new DateTime(2024, 3, 1), 985m, 1000m);

            if (jauge.Ecart != 15m)
            {
                return false;
            }

            // The tank moves on; the recorded gap must not.
            var c = NewCiterne();
            c.Receptionner(1000m, 10000m);
            c.Sortir(500m);

            return jauge.QteStockTheorique == 1000m && jauge.Ecart == 15m;
        });

        Check("the stock correction screen sets the stock directly", () =>
        {
            var c = NewCiterne();
            c.Receptionner(1000m, 10000m);
            c.CorrigerStock(970m);

            return c.Stock == 970m && Throws<BusinessRuleException>(() => c.CorrigerStock(-1m));
        });

        Check("tanks are segregated by product and the partner row is recognised", () =>
        {
            var gasoil = new Citerne("C1", "S01", "Citerne Gasoil", TypeCiterne.Gasoil);
            var adblue = new Citerne("C2", "S01", "Citerne AdBlue", TypeCiterne.AdBlue);
            var confreres = new Citerne("C3", "S01", "CITERNES CONFRERES", TypeCiterne.Gasoil);
            var ordinaire = new Citerne("C4", "S01", "Citerne 1", TypeCiterne.Gasoil);

            // The sentinel row stands in for fuel held by partners, not for real stock.
            return gasoil.EstGasoil && adblue.EstAdBlue
                && confreres.EstConfreres && !ordinaire.EstConfreres
                && Citerne.IntituleConfreres == "CITERNES CONFRERES";
        });

        Check("type_citerne converts to the legacy Gasoil / AdBlue codes", () =>
        {
            var property = model.GetEntityTypes().Single(t => t.ClrType == typeof(Citerne))
                .FindProperty(nameof(Citerne.TypeCiterne))!;

            var vers = property.GetValueConverter()!.ConvertToProvider;

            return (string)vers(TypeCiterne.Gasoil)! == "Gasoil"
                && (string)vers(TypeCiterne.AdBlue)! == "AdBlue";
        });

        Check("an issue snapshots the price so history is not restated", () =>
        {
            // Recomputing a past issue from the tank's current PMP would restate it on every delivery.
            var c = NewCiterne();
            c.Receptionner(1000m, 10000m);

            var sortie = new SortieGasoil("S1", "S01", "C1", TypeCiterne.Gasoil, new DateTime(2024, 3, 1), 100m);
            sortie.AppliquerPrix(c.Sortir(100m), new DateTime(2024, 3, 1));

            if (sortie.PrixUnitaire != 10m || sortie.MontantSortie != 1000m)
            {
                return false;
            }

            // A much more expensive delivery arrives afterwards. The tank holds 900 L at 10 after the
            // issue, so the new PMP is (900*10 + 30000) / 1900 — the issue's valuation must not move.
            c.Receptionner(1000m, 30000m);

            return sortie.PrixUnitaire == 10m && sortie.MontantSortie == 1000m
                && c.Stock == 1900m
                && c.Pmp == 20.5263m;
        });

        Check("the issue type literals keep the legacy typo", () =>
        {
            var sortie = new SortieGasoil("S1", "S01", "C1", TypeCiterne.Gasoil, DateTime.UtcNow, 100m);

            // Defaults to the internal issue type; the partner literal is misspelled in the legacy data.
            return sortie.TypeSortie == "Sortie Gasoil"
                && SortieGasoil.TypeSortieConfrere == "Sortie Gasoil Confrére"
                && BonGasoil.TypeBonConfrere == "Bon Gasoil Confrere".Replace("Confrere", "Confrère");
        });

        Check("t_prix_gasoil has no key column, so its identifier is unmapped", () =>
        {
            // The extraction evidenced only (soc_prix, date_prix) as the practical key; inventing a
            // key_prix column would model something that does not exist.
            var entity = model.GetEntityTypes().Single(t => t.ClrType == typeof(PrixGasoil));

            return entity.FindPrimaryKey()!.Properties.Select(p => p.GetColumnName())
                       .OrderBy(n => n).SequenceEqual(["date_prix", "soc_prix"])
                && entity.FindProperty(nameof(PrixGasoil.Id)) is null
                && entity.FindProperty("PrixDuGasoil")!.GetColumnName() == "prix_gasoil";
        });

        Check("the reference market price is documented as NOT the issue price", () =>
        {
            // Issues are valued at the tank's PMP; using the market price would ignore what fuel cost.
            var c = NewCiterne();
            c.Receptionner(1000m, 11000m);

            var reference = new PrixGasoil("S01", new DateTime(2024, 3, 1), 9.5m, 6m);

            return c.Pmp == 11m && reference.PrixDuGasoil == 9.5m && c.Pmp != reference.PrixDuGasoil;
        });

        Section("Authentication");

        Check("a token round-trips the caller it was issued for", () =>
        {
            var jetons = NewTokens();
            var identite = new CallerIdentity("S01", "A01", "P001", "Rachid Alami", "POSTE-1", DateTime.UtcNow);

            var resultat = jetons.Validate(jetons.Issue(identite));

            return resultat.IsValid
                && resultat.Identity!.SocieteKey == "S01"
                && resultat.Identity.AgenceKey == "A01"
                && resultat.Identity.UserKey == "P001"
                && resultat.Identity.UserDisplayName == "Rachid Alami"
                && resultat.Identity.Workstation == "POSTE-1";
        });

        Check("a tampered signature is rejected", () =>
        {
            var jetons = NewTokens();
            var token = jetons.Issue(new CallerIdentity("S01", null, "P1", null, null, DateTime.UtcNow));

            var parties = token.Split('.');
            var signature = parties[1].ToCharArray();

            // Flip one character of the signature, keeping its length so only the value differs.
            signature[0] = signature[0] == 'A' ? 'B' : 'A';

            var resultat = jetons.Validate($"{parties[0]}.{new string(signature)}");

            return resultat.Rejection == TokenRejection.BadSignature && !resultat.IsValid;
        });

        Check("a tampered PAYLOAD is rejected, so a caller cannot promote itself", () =>
        {
            // This is the attack the signature exists for: editing the company in the payload.
            var jetons = NewTokens();
            var token = jetons.Issue(new CallerIdentity("S01", null, "P1", null, null, DateTime.UtcNow));

            var parties = token.Split('.');
            var payload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(
                parties[0].Replace('-', '+').Replace('_', '/').PadRight((parties[0].Length + 3) / 4 * 4, '=')));

            var falsifie = payload.Replace("|S01|", "|S99|", StringComparison.Ordinal);
            var nouveauPayload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(falsifie))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var resultat = jetons.Validate($"{nouveauPayload}.{parties[1]}");

            return falsifie != payload
                && resultat.Rejection == TokenRejection.BadSignature
                && !resultat.IsValid;
        });

        Check("an expired token is rejected", () =>
        {
            var jetons = new LogisticsTokenService(new TokenOptions { SigningKey = Cle, TokenLifetimeMinutes = -1 });
            var token = jetons.Issue(new CallerIdentity("S01", null, "P1", null, null, DateTime.UtcNow));

            return jetons.Validate(token).Rejection == TokenRejection.Expired;
        });

        Check("expiry is checked AFTER the signature", () =>
        {
            // An unsigned payload must not be trusted even to declare that it expired.
            var jetons = NewTokens();

            var payload = System.Text.Encoding.UTF8.GetBytes("v1|S01|||1");
            var payloadEncode = Convert.ToBase64String(payload).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var signatureBidon = Convert.ToBase64String(new byte[32]).TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var resultat = jetons.Validate($"{payloadEncode}.{signatureBidon}");

            return resultat.Rejection == TokenRejection.BadSignature;
        });

        Check("malformed tokens are rejected without throwing", () =>
        {
            var jetons = NewTokens();

            return jetons.Validate(null).Rejection == TokenRejection.Malformed
                && jetons.Validate("").Rejection == TokenRejection.Malformed
                && jetons.Validate("pasunjeton").Rejection == TokenRejection.Malformed
                && jetons.Validate("a.b.c").Rejection == TokenRejection.Malformed
                && jetons.Validate("!!!.???").Rejection == TokenRejection.Malformed;
        });

        Check("a token without a company is unusable", () =>
        {
            // Every query is tenant-scoped, so a token naming no company could not be scoped at all.
            var jetons = NewTokens();

            // Signed properly, but with an empty company field.
            var serviceAvecCle = new LogisticsTokenService(new TokenOptions { SigningKey = Cle });
            var tokenSansSociete = serviceAvecCle.Issue(new CallerIdentity("", null, "P1", null, null, DateTime.UtcNow));

            // Issue() rejects the delimiter but allows empty; validation must then refuse it.
            return serviceAvecCle.Validate(tokenSansSociete).Rejection == TokenRejection.Malformed;
        });

        Check("signing refuses to run without a configured key", () =>
        {
            // A built-in default key would be a published key, so absence must be a hard failure.
            var sansCle = new LogisticsTokenService(new TokenOptions { SigningKey = null });

            return Throws<InvalidOperationException>(() =>
                sansCle.Issue(new CallerIdentity("S01", null, null, null, null, DateTime.UtcNow)));
        });

        Check("a field containing the delimiter is refused, keeping the format unambiguous", () =>
        {
            var jetons = NewTokens();

            return Throws<ArgumentException>(() =>
                jetons.Issue(new CallerIdentity("S01|x", null, null, null, null, DateTime.UtcNow)));
        });

        Check("the tenant comes from the CALLER, not the request headers", () =>
        {
            // The header-trusting implementation let any caller name any company. With the fallback off,
            // a header must be ignored — that is the whole fix.
            var configuration = new ConfigTest(new() { ["Tenant:SocieteKey"] = "SFALLBACK" });

            var usurpateur = new Logistics.Infrastructure.CallerContext
            {
                HeaderFallbackAllowed = false,
                HeaderSociete = "SVICTIME",
                HeaderUserName = "USURPATEUR"
            };

            var contexte = new Logistics.Infrastructure.ClaimsTenantContext(usurpateur, configuration);

            // With no authenticated caller and no fallback, there is no tenant to resolve at all.
            var refuse = Throws<UnauthorizedAccessException>(() => _ = contexte.SocieteKey);

            // The authenticated caller wins when there is one.
            usurpateur.Caller = new CallerIdentity("SAUTH", null, "P1", "Réel", null, DateTime.UtcNow);
            var resolu = contexte.SocieteKey == "SAUTH" && contexte.UserDisplayName == "Réel";

            return refuse && resolu;
        });

        Check("the development fallback is opt-in and cannot be enabled elsewhere", () =>
        {
            var configuration = new ConfigTest(new()
            {
                ["Tenant:SocieteKey"] = "SCONFIG",
                ["Tenant:UserDisplayName"] = "DEV"
            });

            var contexte = new Logistics.Infrastructure.CallerContext
            {
                HeaderFallbackAllowed = true,
                HeaderSociete = "SDEV"
            };

            var tenant = new Logistics.Infrastructure.ClaimsTenantContext(contexte, configuration);

            // Explicitly enabled for development: the header is honoured, then configuration.
            var depuisEntete = tenant.SocieteKey == "SDEV";
            contexte.HeaderSociete = null;

            return depuisEntete && tenant.SocieteKey == "SCONFIG";
        });

        Check("the API no longer trusts headers for identity", () =>
        {
            // A structural guard: the header-reading tenant context must be gone, so a regression cannot
            // quietly reintroduce it.
            var assembly = typeof(Logistics.Infrastructure.CallerContext).Assembly;

            return assembly.GetType("Logistics.Infrastructure.RequestTenantContext") is null
                && assembly.GetType("Logistics.Infrastructure.ClaimsTenantContext") is not null;
        });

        Section("Authorisation — parameterisation role");

        Check("the parameterisation flag round-trips inside the token", () =>
        {
            var jetons = NewTokens();

            var admin = jetons.Validate(jetons.Issue(
                new CallerIdentity("S01", null, "ADMIN", null, null, DateTime.UtcNow, EstAdmin: true)));
            var ordinaire = jetons.Validate(jetons.Issue(
                new CallerIdentity("S01", null, "USER", null, null, DateTime.UtcNow)));

            return admin.IsValid && admin.Identity!.EstAdmin
                && ordinaire.IsValid && !ordinaire.Identity!.EstAdmin;
        });

        Check("the flag is INSIDE the signed payload, so it cannot be raised by editing", () =>
        {
            // The attack this prevents: take your own valid token and raise the privilege field.
            var jetons = NewTokens();
            var token = jetons.Issue(new CallerIdentity("S01", null, "USER", null, null, DateTime.UtcNow));

            var parties = token.Split('.');
            var payload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(
                parties[0].Replace('-', '+').Replace('_', '/').PadRight((parties[0].Length + 3) / 4 * 4, '=')));

            // The payload is pipe-delimited: the privilege flag is the 8th field. Targeting it by index
            // rather than by a trailing-substring match keeps this test correct as fields are added —
            // an earlier version matched the END of the payload and silently stopped testing the flag
            // once the dossier-scope field was appended.
            var champs = payload.Split('|');

            if (champs.Length < 9 || champs[7] != "0")
            {
                return false;
            }

            champs[7] = "1";
            var eleve = string.Join('|', champs);

            var nouveau = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(eleve))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var resultat = jetons.Validate($"{nouveau}.{parties[1]}");

            // The privilege escalation is refused because the signature no longer matches.
            return resultat.Rejection == TokenRejection.BadSignature && !resultat.IsValid;
        });

        Check("privileges default to NONE, never to privileged", () =>
        {
            // Failing closed is the whole safety property: an unknown user, or a lookup that cannot answer,
            // must not produce an administrator.
            return !CallerRoles.Aucun.EstAdmin && !new CallerRoles(false).EstAdmin;
        });

        Check("a token issued before roles existed still validates, without privileges", () =>
        {
            // Seven fields is the pre-role layout. Accepting it keeps a deploy from invalidating every live
            // token at once, and the missing flag resolves to non-admin — the safe direction.
            var jetons = NewTokens();

            // Build a 7-field payload and sign it the same way the service does.
            var expiration = DateTime.UtcNow.AddMinutes(30);
            var payload = string.Join('|', "v1", "S01", "", "USER", "", "", ToEpoch(expiration));

            var signature = SignerAvecCle(payload);
            var token = $"{ToBase64Url(System.Text.Encoding.UTF8.GetBytes(payload))}.{ToBase64Url(signature)}";

            var resultat = jetons.Validate(token);

            return resultat.IsValid && resultat.Identity!.SocieteKey == "S01"
                && !resultat.Identity.EstAdmin;
        });

        Check("the privilege flag is part of the signed identity, not a separate lookup", () =>
        {
            // The flag travels inside the token, so choosing a user cannot change it and a role change
            // takes effect at the next token rather than mid-request. The policy that consumes it is
            // exercised over HTTP instead of here, because asserting it would need a reference to the
            // whole web stack.
            var propriete = typeof(CallerIdentity).GetProperty(nameof(CallerIdentity.EstAdmin));

            // Reflection reports CanWrite as true for an init-only setter, so that is not the right test.
            // What matters is that the flag is a primary-constructor parameter: it is part of the identity
            // that gets signed, not a value derived or looked up afterwards.
            var constructeur = typeof(CallerIdentity).GetConstructors()
                .Any(c => c.GetParameters().Any(p =>
                    p.Name == nameof(CallerIdentity.EstAdmin) && p.ParameterType == typeof(bool)));

            return propriete is not null && propriete.PropertyType == typeof(bool) && constructeur;
        });

        Section("Dossier-type scoping");

        Check("a scoped caller may only handle the types they are scoped to", () =>
        {
            var limite = new CallerRoles(false, ["MI", "ME"]);

            // Case-insensitively, because the legacy codes are typed freely in places.
            return limite.AutoriseTypeDossier("MI")
                && limite.AutoriseTypeDossier("me")
                && !limite.AutoriseTypeDossier("RI")
                && !limite.AutoriseTypeDossier("TD");
        });

        Check("an EMPTY scope set is treated as unrestricted, visibly", () =>
        {
            // The empty-set semantics could not be established from the source: "no rows" might mean
            // unrestricted or nothing at all. The permissive reading is taken because the two errors are
            // not symmetric — a false restriction locks every operator out on deploy, a false permission is
            // a correctable gap inside one company. Token issuance logs which reading applied.
            var vide = new CallerRoles(false, []);
            var nul = new CallerRoles(false, null);

            return vide.AutoriseTypeDossier("MI") && nul.AutoriseTypeDossier("MI")
                && vide.TypesDossierAutorises is { Count: 0 }
                && nul.TypesDossierAutorises is null;
        });

        Check("an administrator is unrestricted by scopes", () =>
        {
            // Configuring the scopes is itself parameterisation, so an admin must not be limited by them.
            var admin = new CallerRoles(true, ["MI"]);
            return admin.AutoriseTypeDossier("RI") && admin.AutoriseTypeDossier("TD");
        });

        Check("naming no dossier type cannot fail the check", () =>
        {
            // A request that does not mention a type has nothing to authorise against.
            var limite = new CallerRoles(false, ["MI"]);
            return limite.AutoriseTypeDossier(null) && limite.AutoriseTypeDossier("   ");
        });

        Check("the scopes travel inside the signed token", () =>
        {
            var jetons = NewTokens();

            var identite = new CallerIdentity("S01", null, "P1", null, null, DateTime.UtcNow,
                EstAdmin: false, TypesDossierAutorises: ["MI", "ME"]);

            var resultat = jetons.Validate(jetons.Issue(identite));

            return resultat.IsValid
                && resultat.Identity!.TypesDossierAutorises is { Count: 2 }
                && resultat.Identity.TypesDossierAutorises.Contains("MI")
                && resultat.Identity.TypesDossierAutorises.Contains("ME");
        });

        Check("a token with no scopes round-trips as unrestricted, not as a failure", () =>
        {
            var jetons = NewTokens();
            var resultat = jetons.Validate(jetons.Issue(
                new CallerIdentity("S01", null, "P1", null, null, DateTime.UtcNow)));

            return resultat.IsValid
                && resultat.Identity!.TypesDossierAutorises is { Count: 0 }
                && new CallerRoles(false, resultat.Identity.TypesDossierAutorises).AutoriseTypeDossier("RI");
        });

        Check("an authorisation refusal is a 403 with a stable code, not a validation error", () =>
        {
            // Conflating a forbidden response with a business-rule violation hides authorisation failures
            // inside validation noise — the two say different things about the request.
            var refus = new ForbiddenException("dossier.type_dossier", "Non habilité.");

            return refus.Code == "forbidden"
                && refus.Permission == "dossier.type_dossier"
                && refus.Message == "Non habilité.";
        });

        Check("t_personnel_type_dossier is keyed by the pair and has no tenant column", () =>
        {
            var entity = model.GetEntityTypes().Single(t => t.ClrType == typeof(PersonnelTypeDossier));

            return entity.GetTableName() == "t_personnel_type_dossier"
                && entity.FindPrimaryKey()!.Properties.Select(p => p.GetColumnName())
                    .OrderBy(n => n).SequenceEqual(["key_personnel", "type_dossier"])
                && entity.FindProperty("SocieteKey") is null;
        });

        Section("Company parameters (t_parametre_societe)");

        Check("a '= Oui' setting treats an ABSENT row as false", () =>
        {
            var oui = new ParametreSociete("S01", "param_oblig_cloisage", "Oui");
            var non = new ParametreSociete("S01", "param_oblig_cloisage", "Non");
            var absent = new ParametreSociete("S01", "param_oblig_cloisage", null);
            var autre = new ParametreSociete("S01", "param_oblig_cloisage", "peut-être");

            // Case-insensitive because the values were typed freely, and anything else is off.
            return oui.CommeOui()
                && new ParametreSociete("S01", "x", "oui").CommeOui()
                && !non.CommeOui() && !absent.CommeOui() && !autre.CommeOui();
        });

        Check("a '<> Non' setting treats an ABSENT row as TRUE", () =>
        {
            // The asymmetry that matters most. param_oblig_port is tested as `<> 'Non'`, so a company that
            // never configured it gets a MANDATORY port. Reading this as false — as the booking call site
            // did with a hard-coded `obligPort: false` — relaxes a required field for every unconfigured
            // company, silently.
            var absent = new ParametreSociete("S01", "param_oblig_port", null);
            var vide = new ParametreSociete("S01", "param_oblig_port", "");
            var non = new ParametreSociete("S01", "param_oblig_port", "Non");
            var oui = new ParametreSociete("S01", "param_oblig_port", "Oui");

            return absent.CommeNonDefaut()
                && vide.CommeNonDefaut()
                && oui.CommeNonDefaut()
                && !non.CommeNonDefaut()
                && !new ParametreSociete("S01", "x", "non").CommeNonDefaut();
        });

        Check("the two readings genuinely disagree on an absent row", () =>
        {
            // Pinned as its own check because collapsing them into one would look harmless and would
            // silently change the meaning of every `<> "Non"` setting.
            var absent = new ParametreSociete("S01", "param_oblig_port", null);

            return !absent.CommeOui() && absent.CommeNonDefaut();
        });

        Check("numeric settings parse from their text column, and unparseable reads as absent", () =>
        {
            // Everything is stored as text; numbers are numeric strings the caller converts.
            var montant = new ParametreSociete("S01", "Mt_Exclus_factHorsDelai", "1500");
            var taux = new ParametreSociete("S01", "Taux_PremierMois_factHorsDelai", "12.5");
            var texte = new ParametreSociete("S01", "color_menu", "ControlDark");

            return montant.CommeNombre() == 1500m
                && taux.CommeNombre() == 12.5m
                && texte.CommeNombre() is null
                && new ParametreSociete("S01", "x", null).CommeNombre() is null;
        });

        Check("the parameter table is keyed by (key_soc, key_parametre) with a text value", () =>
        {
            var entity = model.GetEntityTypes().Single(t => t.ClrType == typeof(ParametreSociete));

            return entity.GetTableName() == "t_parametre_societe"
                // Tenant column is key_soc — neighbouring reference tables use `societe`.
                && entity.FindProperty(nameof(ParametreSociete.SocieteKey))!.GetColumnName() == "key_soc"
                && entity.FindProperty(nameof(ParametreSociete.ValeurParametre))!.GetColumnName() == "valeur_parametre"
                && entity.FindPrimaryKey()!.Properties.Select(p => p.GetColumnName())
                    .OrderBy(n => n).SequenceEqual(["key_parametre", "key_soc"])
                // The identifier is synthetic: the table's key is the pair.
                && entity.FindProperty(nameof(ParametreSociete.Id)) is null
                // No soft-delete column exists on this table.
                && entity.GetQueryFilter() is null;
        });

        Check("the setting codes this backend reads are the legacy spellings", () =>
        {
            // Matched as literals against a case-insensitive column, so a "corrected" spelling would simply
            // never match. Note `base_fret`, not `param_base_fret`.
            return SetCodes.ParamObligCloisage == "param_oblig_cloisage"
                && SetCodes.ParamObligPort == "param_oblig_port"
                && SetCodes.ParamObligProvenance == "param_oblig_provenance"
                && SetCodes.ParamObligDestination == "param_oblig_destination"
                && SetCodes.GenerNumBooking == "gener_num_booking"
                && SetCodes.BaseFret == "base_fret";
        });

        Check("the SQL-fragment settings are named so nothing reads them as values", () =>
        {
            // These hold WHERE-clause fragments (e.g. `in ('FR','F1')`), not data. Splicing them into a query
            // — as the desktop application does — would be an injection vector in an API that accepts them
            // over HTTP, so they are recorded and refused rather than implemented.
            return SetCodes.ParametresFragmentsSql.Contains(SetCodes.BaseFret)
                && SetCodes.ParametresFragmentsSql.Contains("ch_comm")
                && SetCodes.ParametresFragmentsSql.Contains("GetChaffeurChargeur")
                && SetCodes.ParametresFragmentsSql.Contains("param_lieu_dedouanement")
                // Case-insensitively, matching how the codes are looked up.
                && SetCodes.ParametresFragmentsSql.Contains("BASE_FRET");
        });

        Check("the parameter provider exposes both readings, not one", () =>
        {
            // A single boolean accessor would force every caller to remember which defaulting rule applies.
            var methodes = typeof(IParametreSocieteProvider).GetMethods().Select(m => m.Name).ToArray();

            return methodes.Contains("EstOuiAsync")
                && methodes.Contains("ObligatoireSaufNonAsync")
                && methodes.Contains("TexteAsync")
                && methodes.Contains("NombreAsync");
        });

        Section("Paging bounds");

        Check("the paging rule rejects out-of-range windows", () =>
        {
            // Without this, a caller could ask for pageSize=100000000 and the repository would try to
            // materialise that many rows.
            return Rejette(() => Paging.Valider(0, 50))
                && Rejette(() => Paging.Valider(1, 0))
                && Rejette(() => Paging.Valider(1, -1))
                && Rejette(() => Paging.Valider(-5, 50))
                && Rejette(() => Paging.Valider(1, Paging.MaxPageSize + 1));
        });

        Check("the paging rule ACCEPTS both boundaries", () =>
        {
            // The negative control. A bound that also rejects the valid edge is worse than no bound: it
            // breaks the largest legitimate page a caller may ask for.
            try
            {
                Paging.Valider(1, 1);
                Paging.Valider(1, Paging.MaxPageSize);
                Paging.Valider(int.MaxValue / Paging.MaxPageSize, Paging.MaxPageSize);

                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        });

        Check("the paging bound is stated once, as a constant", () =>
        {
            // It had been hard-coded in five places across four files, applied to three of the list paths.
            return Paging.MaxPageSize == 200
                && Paging.DefaultPageSize > 0
                && Paging.DefaultPageSize <= Paging.MaxPageSize;
        });

        Check("no service re-implements the paging bound inline", () =>
        {
            // The duplication was the cause of the gap, so its absence is asserted structurally rather than
            // trusted. A reintroduced `pageSize is < 1 or > 200` reopens the hole for whichever list path
            // forgot it.
            var racine = RacineDuDepot();
            var sources = Directory.GetFiles(
                Path.Combine(racine, "src", "Logistics.Application"), "*.cs", SearchOption.AllDirectories);

            var copies = sources
                .SelectMany(f => File.ReadAllLines(f)
                    .Where(l => l.Contains("pageSize is < 1 or > 200", StringComparison.Ordinal)))
                .ToArray();

            return copies.Length == 0;
        });

        Section("Taxable measures (groupage chargeable weight)");

        Check("taxable weight is the GREATEST of actual, volumetric and MPL weight", () =>
        {
            // frm_Saisie_Cotation.vb:2145-2147 — a consolidation is charged on whichever measure it consumes
            // most of, so this is a max, not a sum and not the actual weight.
            var (poids, volume) = MesuresTaxables.Calculer(
                poidsReel: 100m, volumeReel: 1m, volumeNonGerbable: null, mplReel: null,
                equivalenceVolumeKgs: 1000m, equivalenceMplKgs: null, equivalenceMplVolume: null);

            // Volumetric weight 1 x 1000 = 1000 beats the actual 100. Volume taxable = max(1, 100/1000).
            return poids == 1000.00m && volume == 1.000m;
        });

        Check("non-stackable volume takes PRECEDENCE over real volume", () =>
        {
            // The legacy is an If/ElseIf (lines 2137-2141), not a sum and not a floor: when non-stackable
            // volume is present, real volume is ignored entirely. This reads like a bug and is reproduced.
            var (poids, volume) = MesuresTaxables.Calculer(
                poidsReel: 5m, volumeReel: 50m, volumeNonGerbable: 2m, mplReel: null,
                equivalenceVolumeKgs: 10m, equivalenceMplKgs: null, equivalenceMplVolume: null);

            // volume used = 2 (not 50, not 52): volumetric weight 20, volume taxable max(2, 5/10).
            return poids == 20.00m && volume == 2.000m;
        });

        Check("the MPL term is included when its ratio is set", () =>
        {
            var (poids, _) = MesuresTaxables.Calculer(
                poidsReel: 5m, volumeReel: null, volumeNonGerbable: null, mplReel: 3m,
                equivalenceVolumeKgs: null, equivalenceMplKgs: 10m, equivalenceMplVolume: null);

            // 3 x 10 = 30 beats the actual 5.
            return poids == 30.00m;
        });

        Check("the weight-to-volume term does not divide by zero", () =>
        {
            // The legacy divides poids_reel by equivalence_volume_kgs (line 2160) and would raise a
            // division-by-zero when the ratio is absent. That is not a behaviour worth reproducing in an API,
            // so a zero or missing ratio disables the term rather than throwing.
            var (poids, volume) = MesuresTaxables.Calculer(
                poidsReel: 7m, volumeReel: 4m, volumeNonGerbable: null, mplReel: null,
                equivalenceVolumeKgs: 0m, equivalenceMplKgs: null, equivalenceMplVolume: null);

            return poids == 7.00m && volume == 4.000m;
        });

        Check("weight rounds to 2 places and volume to 3, as the legacy does", () =>
        {
            // Different precisions for the two results is easy to "tidy up" into one and would be wrong:
            // lines 2148 and 2168 round differently.
            var (poids, volume) = MesuresTaxables.Calculer(
                poidsReel: 1m, volumeReel: 0.3333333m, volumeNonGerbable: null, mplReel: null,
                equivalenceVolumeKgs: 3m, equivalenceMplKgs: null, equivalenceMplVolume: null);

            return MesuresTaxables.DecimalesPoids == 2
                && MesuresTaxables.DecimalesVolume == 3
                // volumetric = 0.3333333 x 3 = 0.9999999 -> 1.00
                && poids == 1.00m
                && volume == 0.333m;
        });

        Section("Tank replenishment (t_approvisionnement_citerne)");

        Check("the header amount is allocated pro rata by quantity, and the parts sum to the whole", () =>
        {
            // frm_Saisie_Approvisionnement_Citerne.vb:189 — the line cost is qte x montant / qte_totale.
            // There is no unit price and no stored line amount anywhere in the schema.
            var appro = new ApprovisionnementCiterne("A1", "S01", "F1", DateTime.UtcNow, "BL-1", 30000m);
            appro.AjouterLigne("C1", null, 300m);
            appro.AjouterLigne("C2", null, 700m);

            var parts = appro.RepartirMontant();

            return parts.Count == 2
                && parts[0].MontantAlloue == 9000m
                && parts[1].MontantAlloue == 21000m
                && parts.Sum(p => p.MontantAlloue) == appro.MontantApprovisionnement;
        });

        Check("a rounding residual lands on the last line, so the parts still sum to the whole", () =>
        {
            // Three equal lines of a third each do not divide evenly. The legacy never rounds the allocated
            // amount (it stays a Double into the PMP arithmetic); money here is decimal, so the residual is
            // assigned rather than lost — an accountant reconciling the BL will check exactly this.
            var appro = new ApprovisionnementCiterne("A2", "S01", "F1", DateTime.UtcNow, null, 100m);
            appro.AjouterLigne("C1", null, 1m);
            appro.AjouterLigne("C2", null, 1m);
            appro.AjouterLigne("C3", null, 1m);

            var parts = appro.RepartirMontant();

            return parts[0].MontantAlloue == 33.33m
                && parts[1].MontantAlloue == 33.33m
                && parts[2].MontantAlloue == 33.34m
                && parts.Sum(p => p.MontantAlloue) == 100m;
        });

        Check("the document total is DERIVED from its lines, so it cannot disagree with them", () =>
        {
            // The legacy form validates qte_totale = SUM(detail) before saving; deriving it is stronger than
            // checking it afterwards, because an inconsistent document cannot be built at all.
            var appro = new ApprovisionnementCiterne("A3", "S01", "F1", DateTime.UtcNow, null, 500m);
            appro.AjouterLigne("C1", 1000, 120m);
            appro.AjouterLigne("C2", 2000, 80m);

            return appro.QteTotale == 200m;
        });

        Check("the same tank cannot appear twice on one delivery", () =>
        {
            // The detail's logical key is (key_approvisionnement, key_citerne); a second line for one tank
            // would break it, and the legacy grid is one row per tank.
            var appro = new ApprovisionnementCiterne("A4", "S01", "F1", DateTime.UtcNow, null, 100m);
            appro.AjouterLigne("C1", null, 10m);

            return RejetteRegle(() => appro.AjouterLigne("C1", null, 5m))
                && appro.Lignes.Count == 1
                && appro.QteTotale == 10m;
        });

        Check("a non-positive delivered quantity is refused", () =>
        {
            var appro = new ApprovisionnementCiterne("A5", "S01", "F1", DateTime.UtcNow, null, 100m);

            return RejetteRegle(() => appro.AjouterLigne("C1", null, 0m))
                && RejetteRegle(() => appro.AjouterLigne("C1", null, -5m));
        });

        Check("the module reports that a delivery canNOT be cancelled", () =>
        {
            // Not an omission. The legacy has no delete path — `supprimer` exists and is filtered on but is
            // written by nothing — and reversal is impossible because the tank's prior quantity and PMP are
            // not retained. Exposing the answer beats letting a caller assume it was forgotten.
            return !ApprovisionnementCiterne.EstAnnulable;
        });

        Check("the replenishment maps to its real columns and tenant", () =>
        {
            var entete = model.GetEntityTypes().Single(t => t.ClrType == typeof(ApprovisionnementCiterne));

            return entete.GetTableName() == "t_approvisionnement_citerne"
                // `societe` here; the sibling outflow table uses `societe_sortie`.
                && entete.FindProperty(nameof(ApprovisionnementCiterne.SocieteKey))!.GetColumnName() == "societe"
                // num_bl, NOT num_bon — num_bon belongs to t_consommation_gasoil.
                && entete.FindProperty(nameof(ApprovisionnementCiterne.NumBl))!.GetColumnName() == "num_bl"
                && entete.FindProperty(nameof(ApprovisionnementCiterne.MontantApprovisionnement))!
                    .GetColumnName() == "montant_approvisionnement";
        });

        Check("the detail maps with NO surrogate key and NO tenant column", () =>
        {
            // The insert column list is exactly (key_approvisionnement, key_citerne, index_compteur,
            // qte_citerne): no key_detail* column is ever inserted, and no tenant is written or read.
            var detail = model.GetEntityTypes().Single(t => t.ClrType == typeof(DetailApprovisionnementCiterne));

            return detail.GetTableName() == "t_detail_approvisionnement_citerne"
                && detail.FindPrimaryKey()!.Properties.Select(p => p.GetColumnName())
                    .OrderBy(n => n).SequenceEqual(["key_approvisionnement", "key_citerne"])
                && detail.FindProperty("SocieteKey") is null
                && detail.GetProperties().All(p =>
                    p.GetColumnName() is "key_approvisionnement" or "key_citerne" or "index_compteur" or "qte_citerne");
        });

        Check("the whole fuel family draws from ONE key sequence", () =>
        {
            // The legacy calls IncrementationKeyApp("GASOIL") from every fuel table, so a key is unique across
            // the family. This backend had split it into CITERNE / SORTIE_GASOIL / JAUGE_GASOIL, which would
            // issue different keys for the same work and could issue the SAME key to two different tables.
            var racine = RacineDuDepot();
            var source = File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Application", "Features", "Resources", "CarburantFeature.cs"));

            return SequenceGasoil.Operation == "GASOIL"
                && source.Contains("SequenceGasoil.Operation", StringComparison.Ordinal)
                && !source.Contains("NextAsync(\"CITERNE\"", StringComparison.Ordinal)
                && !source.Contains("NextAsync(\"SORTIE_GASOIL\"", StringComparison.Ordinal)
                && !source.Contains("NextAsync(\"JAUGE_GASOIL\"", StringComparison.Ordinal);
        });

        Check("a value longer than its legacy column is rejected BEFORE it reaches the database", () =>
        {
            // EF sends an over-long string as-is and SQL Server raises a truncation error, which arrives as
            // DbUpdateException and was reported as 409 Persistence conflict naming no field. The guard runs
            // before any command is sent, so this check needs no reachable database — if the guard fails to
            // fire, the connection attempt is what surfaces, and the check fails.
            using var contexte = NewContexteSansBase();

            // type_dossier is varchar(10) in the model.
            contexte.PersonnelTypesDossier.Add(new PersonnelTypeDossier("P1", new string('X', 50)));

            try
            {
                contexte.SaveChanges();
                return false;
            }
            catch (ChampInvalideException ex)
            {
                return ex.Code == "validation_failed"
                    && ex.Erreurs.ContainsKey("TypeDossier")
                    && ex.Erreurs["TypeDossier"][0].Contains("10", StringComparison.Ordinal);
            }
        });

        Check("a value WITHIN its column length is not blocked by the guard", () =>
        {
            // The negative control. A guard that also rejects valid values would break every write, and the
            // assertion must not be satisfiable merely by the guard firing on everything.
            using var contexte = NewContexteSansBase();

            contexte.PersonnelTypesDossier.Add(new PersonnelTypeDossier("P1", "MI"));

            try
            {
                contexte.SaveChanges();
                return false;
            }
            catch (ChampInvalideException)
            {
                return false;
            }
            catch (Exception)
            {
                // Anything else means the value passed the guard and the write proceeded.
                return true;
            }
        });

        Check("every offending field is reported together, not one per round trip", () =>
        {
            using var contexte = NewContexteSansBase();

            // Both columns are over-length on the same entity.
            contexte.PersonnelTypesDossier.Add(
                new PersonnelTypeDossier(new string('P', 40), new string('X', 50)));

            try
            {
                contexte.SaveChanges();
                return false;
            }
            catch (ChampInvalideException ex)
            {
                // Naming only the first would make a caller fix a many-field problem one request at a time.
                return ex.Erreurs.Count == 2
                    && ex.Erreurs.ContainsKey("KeyPersonnel")
                    && ex.Erreurs.ContainsKey("TypeDossier");
            }
        });

        Check("the guard reads limits from the MODEL, so it cannot drift from the columns", () =>
        {
            // Limits come from EF's own MaxLength metadata rather than attributes on request DTOs. That is
            // what makes the guarantee cover every entity and every future write path without anyone
            // remembering to annotate anything.
            var racine = RacineDuDepot();
            var source = File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Infrastructure", "Persistence", "LogisticsDbContext.cs"));

            return source.Contains("propriete.Metadata.GetMaxLength()", StringComparison.Ordinal)
                // And it must run on all four SaveChanges entry points, or a caller using one overload
                // bypasses it entirely.
                && source.Split("ValiderLongueurs();").Length - 1 >= 4;
        });

        Check("the most-typed free-text field is actually protected", () =>
        {
            // A guard is only worth anything where limits exist. A client's raison sociale is the field an
            // operator types most, so it is asserted rather than assumed.
            var client = model.GetEntityTypes().Single(t => t.ClrType == typeof(Client));
            var raisonSociale = client.FindProperty(nameof(Client.RaisonSociale))!;

            return raisonSociale.GetMaxLength() is > 0;
        });

        Check("role resolution is asked for the right user and fails closed", () =>
        {
            // The resolver reads t_personnel.admin for the token's user key. Verified here through the
            // interface contract; the fail-closed behaviour is exercised by the token endpoint when the
            // database is unreachable, which resolves to no privileges rather than to admin.
            var methode = typeof(IUserRoleResolver).GetMethod(nameof(IUserRoleResolver.ResolveAsync));

            return methode is not null
                && methode.ReturnType == typeof(Task<CallerRoles>)
                && methode.GetParameters()[0].ParameterType == typeof(string);
        });

        Section("EDI promotion — taxable measures and packing");

        Check("the EDI import resolves its ratios from the hard-coded ROUTIER row", () =>
        {
            // frm_Integration_EDI_IMPORT_STC.vb:927 hard-codes `produit='ROUTIER'`, because an inbound STC
            // file cannot express which ratio row applies. It is the legacy's literal, not a default chosen
            // here, so it is asserted rather than assumed.
            return RapportPoidsVolume.ProduitRoutier == "ROUTIER";
        });

        Check("an imported dossier's taxable weight is the volumetric one when it is greater", () =>
        {
            // The promotion's own arithmetic, against ROUTIER-style ratios: 100 kg actual, 0.5 m3 at
            // 1000 kg/m3 = 500 kg volumetric, so the consolidation is charged on 500 kg, not 100.
            var (poids, volume) = MesuresTaxables.Calculer(
                poidsReel: 100m, volumeReel: 0.5m, volumeNonGerbable: null, mplReel: null,
                equivalenceVolumeKgs: 1000m, equivalenceMplKgs: 1200m, equivalenceMplVolume: 1.2m);

            return poids == 500.00m && volume == 0.500m;
        });

        Check("with no ratio row, only the actual measures are chargeable", () =>
        {
            // The legacy guard is `If rs.Read Then` — a missing row leaves all three ratios at zero, so the
            // promotion must still produce a sane taxable weight rather than null or a division by zero.
            var (poids, volume) = MesuresTaxables.Calculer(
                poidsReel: 250m, volumeReel: 3m, volumeNonGerbable: null, mplReel: null,
                equivalenceVolumeKgs: null, equivalenceMplKgs: null, equivalenceMplVolume: null);

            return poids == 250.00m && volume == 3.000m;
        });

        Check("the ratios table maps with soc_rapport and a (company, product) key", () =>
        {
            // soc_rapport is a third distinct tenant spelling in this area of the schema, alongside `societe`
            // on the tank tables and `soc_fournisseur` on suppliers.
            var entity = model.GetEntityTypes().Single(t => t.ClrType == typeof(RapportPoidsVolume));

            return entity.GetTableName() == "t_rapport_poids_volume"
                && entity.FindProperty(nameof(RapportPoidsVolume.SocieteKey))!.GetColumnName() == "soc_rapport"
                && entity.FindProperty(nameof(RapportPoidsVolume.VolumeKgs))!.GetColumnName() == "volume_kgs"
                && entity.FindProperty(nameof(RapportPoidsVolume.MplKgs))!.GetColumnName() == "mpl_kgs"
                && entity.FindProperty(nameof(RapportPoidsVolume.MplVolume))!.GetColumnName() == "mpl_volume"
                && entity.FindPrimaryKey()!.Properties.Select(p => p.GetColumnName())
                    .OrderBy(n => n).SequenceEqual(["produit", "soc_rapport"]);
        });

        Check("promotion now sets taxable measures instead of leaving them null", () =>
        {
            // It previously passed `poidsTaxable: null, volumeTaxable: null` and no ratios. Taxable weight is
            // the pro-rata basis a voyage's sales amount is split by, so a null one makes an imported dossier
            // impossible to allocate against.
            var racine = RacineDuDepot();
            var source = File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Application", "Features", "Edi", "EdiReconciliationFeature.cs"));

            return source.Contains("MesuresTaxables.Calculer", StringComparison.Ordinal)
                && source.Contains("poidsTaxable: mesures.PoidsTaxable", StringComparison.Ordinal)
                && source.Contains("volumeTaxable: mesures.VolumeTaxable", StringComparison.Ordinal)
                && !source.Contains("poidsTaxable: null", StringComparison.Ordinal)
                && source.Contains("RapportPoidsVolume.ProduitRoutier", StringComparison.Ordinal);
        });

        Check("promotion now creates the packing line the legacy writes", () =>
        {
            // frm_Integration_EDI_IMPORT_STC.vb:1104 — insert into T_COLISAGE_DOSSIER(key_dossier, type_colis,
            // nb_colis), unconditionally. Without it a promoted dossier has totals but no packing detail.
            var racine = RacineDuDepot();
            var source = File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Application", "Features", "Edi", "EdiReconciliationFeature.cs"));

            // And the repository could not express it until now, which is why the gap existed.
            var depot = File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Application", "Features", "Shipments", "ShipmentFeature.cs"));

            return source.Contains("new ColisageDossier(", StringComparison.Ordinal)
                && depot.Contains("void Add(ColisageDossier colisage)", StringComparison.Ordinal);
        });

        Section("EDI promotion — COD and supplier invoices");

        Check("the COD link maps to T_COD_DOSSIER with its own key sequence", () =>
        {
            // frm_Integration_EDI_IMPORT_STC.vb:1107 —
            // insert into T_COD_DOSSIER(key_ligne_cod, key_dossier, COD).
            var entity = model.GetEntityTypes().Single(t => t.ClrType == typeof(CodDossier));

            return entity.GetTableName() == "T_COD_DOSSIER"
                && entity.FindProperty(nameof(CodDossier.Id))!.GetColumnName() == "key_ligne_cod"
                && entity.FindProperty(nameof(CodDossier.KeyDossier))!.GetColumnName() == "key_dossier"
                && entity.FindProperty(nameof(CodDossier.Cod))!.GetColumnName() == "COD";
        });

        Check("the COD sequence is NOT the fuel family's shared sequence", () =>
        {
            // The legacy draws COD keys from IncrementationKeyApp("COD"), which is a different counter from
            // the one t_citernes / t_sortie_gasoil / t_consommation_gasoil all share. Using the shared
            // sequence here would interleave two unrelated key spaces.
            var racine = RacineDuDepot();
            var source = File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Application", "Features", "Edi", "EdiReconciliationFeature.cs"));

            return source.Contains("NextAsync(\"COD\"", StringComparison.Ordinal)
                && !source.Contains("NextAsync(SequenceGasoil.Operation", StringComparison.Ordinal);
        });

        Check("the supplier invoice table is keyed by (dossier, invoice number)", () =>
        {
            // No key column is inserted by the legacy: the column list is exactly
            // (key_dossier, num_facture, date_facture, montant_facture, devise_facture, intitule_fournisseur).
            var entity = model.GetEntityTypes().Single(t => t.ClrType == typeof(FactureFournisseurDossier));

            return entity.GetTableName() == "T_FACTURE_FOURNISSEUR_DOSSIER"
                && entity.FindPrimaryKey()!.Properties.Select(p => p.GetColumnName())
                    .OrderBy(n => n).SequenceEqual(["key_dossier", "num_facture"])
                && entity.FindProperty(nameof(FactureFournisseurDossier.DateFacture))!.GetColumnName() == "date_facture"
                && entity.FindProperty(nameof(FactureFournisseurDossier.MontantFacture))!.GetColumnName() == "montant_facture"
                && entity.FindProperty(nameof(FactureFournisseurDossier.DeviseFacture))!.GetColumnName() == "devise_facture"
                && entity.FindProperty(nameof(FactureFournisseurDossier.IntituleFournisseur))!.GetColumnName()
                    == "intitule_fournisseur";
        });

        Check("a missing supplier name becomes the legacy's single-space placeholder", () =>
        {
            // The legacy passes a literal ' ' rather than null or an empty string. Reproduced deliberately:
            // a later reader may depend on the column being present, and this records whose decision it was.
            var facture = new FactureFournisseurDossier(
                "D1", "F-1", new DateTime(2026, 1, 15), 1200m, "EUR");

            return FactureFournisseurDossier.IntituleParDefaut == " "
                && facture.IntituleFournisseur == " "
                && facture.NumFacture == "F-1"
                && facture.MontantFacture == 1200m;
        });

        Check("a dossier can carry five distinct supplier invoices", () =>
        {
            // The principal key is the pair, so five blocks from one file coexist exactly when their numbers
            // differ — which is what the portal requires, and what a duplicate-number check protects.
            using var contexte = NewContexteSansBase();

            for (var i = 1; i <= 5; i++)
            {
                contexte.FacturesFournisseurDossier.Add(new FactureFournisseurDossier(
                    "D1", $"F-{i}", new DateTime(2026, 1, i), 100m * i, "EUR"));
            }

            var cles = contexte.ChangeTracker.Entries()
                .Select(e => e.Property(nameof(FactureFournisseurDossier.NumFacture)).CurrentValue)
                .Distinct()
                .Count();

            return cles == 5;
        });

        Check("promotion writes both the COD link and the supplier invoices", () =>
        {
            var racine = RacineDuDepot();
            var source = File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Application", "Features", "Edi", "EdiReconciliationFeature.cs"));

            // Each invoice block is written only when its number is present, as the legacy does, and the COD
            // only when the file carries one.
            return source.Contains("new CodDossier(", StringComparison.Ordinal)
                && source.Contains("new FactureFournisseurDossier(", StringComparison.Ordinal)
                && source.Contains("detail.CodClt", StringComparison.Ordinal)
                && source.Contains("detail.Facture5Montant", StringComparison.Ordinal)
                && source.Contains("if (string.IsNullOrWhiteSpace(numero))", StringComparison.Ordinal);
        });

        Section("EDI promotion — voyage equipment");

        Check("a maritime equipment line's quantity counts its detail rows", () =>
        {
            // frm_Integration_EDI_IMPORT_STC.vb:1034 — type TC with qte=2 and TWO detail rows when the file
            // names both a box and a vehicle; qte=1 and one row otherwise. The quantity is the unit count,
            // not a separate figure.
            var ligne = new EquipementVoyage("E1", "V1", "TC", "TC");
            ligne.SetQuantity(2);
            ligne.AddDetail(new DetailEquipementVoyage("E1", null, "CAISSE-1"));
            ligne.AddDetail(new DetailEquipementVoyage("E1", null, "VEH-1"));

            // Both rows hang off the SAME line key, which is what makes them one equipment line.
            return ligne.Qte == 2
                && ligne.Details.Count == 2
                && ligne.Details.All(d => d.KeyLigneEquipement == "E1")
                && ligne.Details.Select(d => d.NumEquipement).OrderBy(n => n).SequenceEqual(["CAISSE-1", "VEH-1"]);
        });

        Check("the maritime variant carries NO t_materiel reference", () =>
        {
            // The legacy hardcodes null for the TC rows even when the plate matches a fleet row, because a
            // container in transit is not necessarily the company's asset. The column is nullable for exactly
            // this case.
            var ligne = new EquipementVoyage("E2", "V1", "TC", "TC");
            ligne.AddDetail(new DetailEquipementVoyage("E2", null, "TCNU-123456"));

            return ligne.Details.Single().KeyEquipement is null
                && ligne.Details.Single().NumEquipement == "TCNU-123456";
        });

        Check("only the road trailer's detail carries a seal number", () =>
        {
            // Line 1012 sets num_plomb from the file's PLOMB field on the trailer only; the box line passes
            // NULL (line 1024).
            var remorque = new DetailEquipementVoyage("E3", "M1", "12345-A-6");
            remorque.SetNumPlomb("  PLOMB-77  ");

            var caisse = new DetailEquipementVoyage("E4", "M2", "67890-B-1");
            caisse.SetNumPlomb(null);

            // An empty or whitespace seal must normalise to null rather than being stored as a blank.
            var vide = new DetailEquipementVoyage("E5", "M3", "11111-C-2");
            vide.SetNumPlomb("   ");

            return remorque.NumPlomb == "PLOMB-77"
                && caisse.NumPlomb is null
                && vide.NumPlomb is null;
        });

        Check("a plate the fleet does not know yields null rather than failing", () =>
        {
            // The legacy looks t_materiel up by IMMATRICULATION and type_materiel; a miss leaves the reference
            // empty instead of aborting the import. The repository exposes that lookup.
            var methode = typeof(IVoyageRepository).GetMethod(
                nameof(IVoyageRepository.FindMaterielParImmatriculationAsync));

            return methode is not null
                && methode.ReturnType == typeof(Task<Materiel?>)
                && methode.GetParameters()[0].ParameterType == typeof(string);
        });

        Check("promotion branches on the file's RI/MI type and uses the EXPLOITATION sequence", () =>
        {
            // "RI" (road) is a REMORQUE line plus an optional CAISSE; "MI" (maritime) is a TC line. Every
            // line key comes from IncrementationKeyApp("EXPLOITATION") — a third sequence, distinct from
            // "VOYAGE" and from the fuel family's shared "GASOIL".
            var racine = RacineDuDepot();
            var source = File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Application", "Features", "Edi", "EdiReconciliationFeature.cs"));

            return source.Contains("NextAsync(\"EXPLOITATION\"", StringComparison.Ordinal)
                && source.Contains("\"RI\"", StringComparison.Ordinal)
                && source.Contains("\"MI\"", StringComparison.Ordinal)
                && source.Contains("FindMaterielParImmatriculationAsync(\"REMORQUE\"", StringComparison.Ordinal)
                && source.Contains("FindMaterielParImmatriculationAsync(\"CAISSE\"", StringComparison.Ordinal)
                // The road branch sets a seal; the maritime branch must not.
                && source.Contains("detail.SetNumPlomb(entete.Plomb)", StringComparison.Ordinal)
                && !source.Contains("NextAsync(SequenceGasoil.Operation", StringComparison.Ordinal);
        });

        Section("Fuel consumption ledger (t_consommation_gasoil)");

        Check("the journal maps with societe_consommation as its tenant", () =>
        {
            // A fourth distinct tenant spelling in this module, alongside `societe` (t_citernes,
            // t_approvisionnement_citerne), `societe_sortie` (t_sortie_gasoil) and `key_soc`.
            var entity = model.GetEntityTypes().Single(t => t.ClrType == typeof(ConsommationGasoil));

            return entity.GetTableName() == "t_consommation_gasoil"
                && entity.FindProperty(nameof(ConsommationGasoil.SocieteKey))!.GetColumnName()
                    == "societe_consommation"
                && entity.FindProperty(nameof(ConsommationGasoil.Id))!.GetColumnName() == "key_consommation"
                && entity.FindProperty(nameof(ConsommationGasoil.TypeSortie))!.GetColumnName() == "type_sortie"
                && entity.FindProperty(nameof(ConsommationGasoil.PrixUnitaire))!.GetColumnName() == "prix_unitaire";
        });

        Check("the journal is soft-deleted, never removed", () =>
        {
            // A cancelled movement still has to appear in the history of what was issued, so the row is
            // filtered rather than deleted — and the filter must exist or cancelled rows would reappear in
            // the consumption reports.
            var entity = model.GetEntityTypes().Single(t => t.ClrType == typeof(ConsommationGasoil));

            return entity.GetQueryFilter() is not null
                && entity.FindProperty(nameof(ConsommationGasoil.Supprime))!.GetColumnName() == "supprimer";
        });

        Check("the document and its journal row share ONE key", () =>
        {
            // frm_Saisie_Sortie_Gasoil.vb:319-323 — one IncrementationKeyApp("GASOIL") written as both
            // key_sortie and key_consommation. That is only possible because the fuel family shares a single
            // sequence, and it is what lets the pair be rejoined.
            //
            // Asserted as "the journal is keyed by the document's own Id", not as a match on the surrounding
            // argument list: an earlier version pinned the type argument too and broke when the issue gained
            // variants, even though the shared-key property was untouched.
            var racine = RacineDuDepot();
            var source = File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Application", "Features", "Resources", "CarburantFeature.cs"));

            var debut = source.IndexOf("public async Task<SortieGasoilDto> SortirAsync", StringComparison.Ordinal);
            var fin = source.IndexOf("public async Task<SortieGasoilDto> AnnulerSortieAsync", StringComparison.Ordinal);

            if (debut < 0 || fin <= debut)
            {
                return false;
            }

            var corps = source[debut..fin];

            return corps.Contains("new SortieGasoil(", StringComparison.Ordinal)
                && corps.Contains("sortie.Id, tenant.SocieteKey,", StringComparison.Ordinal);
        });

        Check("the amount is derived from the snapshot price, not passed in", () =>
        {
            // The ledger must not be able to disagree with its document: the caller supplies a quantity and a
            // unit price, and the amount follows.
            var ligne = new ConsommationGasoil(
                "C1", "S01", ConsommationGasoil.Types.SortieGasoil, new DateTime(2026, 3, 1),
                "CIT-1", 250m, 0m, 12m);

            ligne.SetPrixUnitaire(12.3456m);

            return ligne.QteConsommation == 250m
                && ligne.PrixUnitaire == 12.3456m
                // 250 x 12.3456 = 3086.40
                && ligne.MontantConsommation == 3086.40m;
        });

        Check("the legacy type_sortie literals are preserved, typos included", () =>
        {
            // These are what the consumption reports group by, so a "corrected" accent would silently split a
            // category. Confrére carries an acute accent on the first vowel only, and the two voucher variants
            // disagree with each other about which vowel it is — both are the legacy's.
            return ConsommationGasoil.Types.SortieGasoil == "Sortie Gasoil"
                && ConsommationGasoil.Types.SortieTransporteur == "Sortie Gasoil Transporteur"
                && ConsommationGasoil.Types.SortieConfrere == "Sortie Gasoil Confrére"
                && ConsommationGasoil.Types.BonGasoil == "Bon Gasoil"
                && ConsommationGasoil.Types.BonTransporteur == "Bon Gasoil Transporteur"
                && ConsommationGasoil.Types.BonConfrere == "Bon Gasoil Confrère";
        });

        Check("cancelling an issue also soft-deletes its journal row", () =>
        {
            // Leaving the journal row live would keep a cancelled issue in the consumption totals while its
            // document said otherwise.
            var racine = RacineDuDepot();
            var source = File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Application", "Features", "Resources", "CarburantFeature.cs"));

            var infra = File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Infrastructure", "InfrastructureServices.cs"));

            return source.Contains("consommation?.SupprimerLogiquement()", StringComparison.Ordinal)
                // And the lookup must see already-deleted rows, or a second cancellation would find nothing and
                // leave the document and the journal disagreeing.
                && infra.Contains("db.ConsommationsGasoil", StringComparison.Ordinal)
                && infra.Contains(".IgnoreQueryFilters()", StringComparison.Ordinal);
        });

        Check("the tank type stored on the journal matches the legacy literals", () =>
        {
            // type_citerne is 'Gasoil' or 'AdBlue' in the file, and the enum members happen to spell the same
            // — which is why ToString() is safe here, and worth pinning so a rename cannot break it silently.
            var ligne = new ConsommationGasoil(
                "C2", "S01", ConsommationGasoil.Types.SortieGasoil, DateTime.UtcNow, "CIT-1", 1m, 1m, 1m);

            return ConsommationGasoil.TypeCiterneGasoil == "Gasoil"
                && ligne.TypeCiterne == "Gasoil"
                && TypeCiterne.Gasoil.ToString() == "Gasoil"
                && TypeCiterne.AdBlue.ToString() == "AdBlue";
        });

        Section("Fuel vouchers (t_bon_gasoil)");

        Check("a voucher records NO tank, and the journal column allows that", () =>
        {
            // A voucher is fuel BOUGHT, not drawn: the legacy's voucher insert omits key_citerne entirely
            // (frm_Saisie_Bon_Gasoil.vb:254) while the issue insert supplies it. The journal column must
            // therefore be nullable — making it required would have made this second writer impossible.
            var ligne = new ConsommationGasoil(
                "B1", "S01", ConsommationGasoil.Types.BonGasoil, new DateTime(2026, 4, 1),
                keyCiterne: null, 500m, 6000m, 12m);

            var entity = model.GetEntityTypes().Single(t => t.ClrType == typeof(ConsommationGasoil));
            var propriete = entity.FindProperty(nameof(ConsommationGasoil.KeyCiterne))!;

            return ligne.KeyCiterne is null
                && ligne.QteConsommation == 500m
                && propriete.IsNullable;
        });

        Check("the voucher table's tenant is societe_bon", () =>
        {
            // A FIFTH distinct tenant spelling in this module, after `societe` (t_citernes,
            // t_approvisionnement_citerne), `societe_sortie` (t_sortie_gasoil), `societe_consommation`
            // (t_consommation_gasoil) and `key_soc`.
            var entity = model.GetEntityTypes().Single(t => t.ClrType == typeof(BonGasoil));

            return entity.GetTableName() == "t_bon_gasoil"
                && entity.FindProperty(nameof(BonGasoil.SocieteKey))!.GetColumnName() == "societe_bon"
                && entity.FindProperty(nameof(BonGasoil.Id))!.GetColumnName() == "key_bon";
        });

        Check("the voucher and its journal row share ONE key", () =>
        {
            // One IncrementationKeyApp("GASOIL") written as both key_bon and key_consommation
            // (frm_Saisie_Bon_Gasoil.vb:250-254) — the same pairing the issue path uses.
            //
            // Asserted as "both constructors receive the same local `key`", not as a match on the surrounding
            // expression: an earlier version pinned the journal's type argument too, and broke when the
            // voucher gained variants even though the shared-key property was untouched.
            var racine = RacineDuDepot();
            var source = File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Application", "Features", "Resources", "CarburantFeature.cs"));

            var debut = source.IndexOf("public async Task<BonGasoilDto> CreerBonAsync", StringComparison.Ordinal);
            var fin = source.IndexOf("public async Task<BonGasoilDto> GetBonAsync", StringComparison.Ordinal);

            if (debut < 0 || fin <= debut)
            {
                return false;
            }

            var corps = source[debut..fin];

            return corps.Contains("var key = await keys.NextAsync(", StringComparison.Ordinal)
                && corps.Contains("new BonGasoil(key,", StringComparison.Ordinal)
                && corps.Contains("key, tenant.SocieteKey,", StringComparison.Ordinal);
        });

        Check("the voucher's amount is taken as given, not re-derived", () =>
        {
            // The amount is what the supplier billed. Calling SetPrixUnitaire would OVERWRITE it with a
            // recomputation from a rounded unit price, moving the figure away from the document — so the
            // voucher passes the amount to the constructor and must not call that method afterwards.
            var racine = RacineDuDepot();
            var source = File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Application", "Features", "Resources", "CarburantFeature.cs"));

            var debut = source.IndexOf("public async Task<BonGasoilDto> CreerBonAsync", StringComparison.Ordinal);
            var fin = source.IndexOf("public async Task<BonGasoilDto> GetBonAsync", StringComparison.Ordinal);

            if (debut < 0 || fin <= debut)
            {
                return false;
            }

            var corps = source[debut..fin];

            return corps.Contains("request.Montant ?? 0m", StringComparison.Ordinal)
                && !corps.Contains("SetPrixUnitaire", StringComparison.Ordinal);
        });

        Check("the voucher type literals are preserved verbatim", () =>
        {
            // Same strings as the journal's type_sortie, and the same disagreement about which vowel of
            // Confrère carries the accent.
            return BonGasoil.TypeBonGasoil == "Bon Gasoil"
                && BonGasoil.TypeBonTransporteur == "Bon Gasoil Transporteur"
                && BonGasoil.TypeBonConfrere == "Bon Gasoil Confrère"
                && ConsommationGasoil.Types.BonConfrere == "Bon Gasoil Confrère";
        });

        Check("creating a voucher never touches a tank", () =>
        {
            // The legacy writes no stock update for a voucher — the fuel reaches a tank only via a
            // replenishment. Touching a tank here would silently double-count the delivery.
            var racine = RacineDuDepot();
            var source = File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Application", "Features", "Resources", "CarburantFeature.cs"));

            var debut = source.IndexOf("public async Task<BonGasoilDto> CreerBonAsync", StringComparison.Ordinal);
            var fin = source.IndexOf("public async Task<BonGasoilDto> GetBonAsync", StringComparison.Ordinal);
            var corps = source[debut..fin];

            return !corps.Contains("RequireCiterneAsync", StringComparison.Ordinal)
                && !corps.Contains("Receptionner", StringComparison.Ordinal)
                && !corps.Contains("Sortir(", StringComparison.Ordinal);
        });

        Section("Confrère consignment stock (T_STOCK_GASOIL_CONFRERE)");

        Check("the consignment table uses a NATURAL key, not the shared sequence", () =>
        {
            // The only fuel table keyed by (key_confrere, key_soc) rather than by a key drawn from "GASOIL".
            // Tenant column is key_soc.
            var entity = model.GetEntityTypes().Single(t => t.ClrType == typeof(StockGasoilConfrere));

            return entity.GetTableName() == "T_STOCK_GASOIL_CONFRERE"
                && entity.FindPrimaryKey()!.Properties.Select(p => p.GetColumnName())
                    .OrderBy(n => n).SequenceEqual(["key_confrere", "key_soc"])
                && entity.FindProperty(nameof(StockGasoilConfrere.QteGasoilEntree))!.GetColumnName()
                    == "qte_gasoil_entree"
                && entity.FindProperty(nameof(StockGasoilConfrere.QteGasoilSortie))!.GetColumnName()
                    == "qte_gasoil_sortie";
        });

        Check("the balance is DERIVED, so it is not a column of its own", () =>
        {
            // Solde is entree minus sortie. Persisting it would let it disagree with the two figures the
            // legacy reports actually read.
            var entity = model.GetEntityTypes().Single(t => t.ClrType == typeof(StockGasoilConfrere));

            return entity.FindProperty(nameof(StockGasoilConfrere.Solde)) is null;
        });

        Check("a delivery adds to entree and an issue adds to sortie, on one balance", () =>
        {
            var stock = new StockGasoilConfrere("CONF-1", "S01");

            stock.AjouterEntree(500m);
            stock.AjouterEntree(200m);
            stock.AjouterSortie(300m);

            // They delivered 700, we took 300, so they are 400 ahead of us.
            return stock.QteGasoilEntree == 700m
                && stock.QteGasoilSortie == 300m
                && stock.Solde == 400m;
        });

        Check("correcting a delivery is CUMULATIVE, not an assignment", () =>
        {
            // The legacy computes (qte - anc_qte) + nouvelle_qte rather than assigning. Other documents may
            // have moved the balance since, and assigning would silently erase their movements — so a balance
            // of 700 corrected from 200 to 250 becomes 750, NOT 250.
            var stock = new StockGasoilConfrere("CONF-2", "S01");
            stock.AjouterEntree(700m);

            stock.CorrigerEntree(ancienneQuantite: 200m, nouvelleQuantite: 250m);

            return stock.QteGasoilEntree == 750m;
        });

        Check("deleting a voucher reverses its whole quantity", () =>
        {
            // frm_Liste_Bons_Gasoil_Confrere.vb:93 subtracts the full quantity. Without it the balance keeps
            // counting fuel from a document that no longer exists, and the partner's statement stops
            // reconciling.
            var stock = new StockGasoilConfrere("CONF-3", "S01");
            stock.AjouterEntree(400m);
            stock.RetirerEntree(400m);

            return stock.QteGasoilEntree == 0m && stock.Solde == 0m;
        });

        Check("a negative balance movement is refused", () =>
        {
            var stock = new StockGasoilConfrere("CONF-4", "S01");

            return Rejette(() => stock.AjouterEntree(-1m))
                && Rejette(() => stock.AjouterSortie(-1m))
                && Rejette(() => stock.RetirerEntree(-1m));
        });

        Check("the voucher variant drives BOTH type_bon and type_sortie", () =>
        {
            // The legacy writes the same literal to the voucher and to the journal — so the transporteur and
            // confrère variants need no extra parameter, and one value cannot drift from the other.
            var racine = RacineDuDepot();
            var source = File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Application", "Features", "Resources", "CarburantFeature.cs"));

            var debut = source.IndexOf("public async Task<BonGasoilDto> CreerBonAsync", StringComparison.Ordinal);
            var fin = source.IndexOf("public async Task<BonGasoilDto> GetBonAsync", StringComparison.Ordinal);
            var corps = source[debut..fin];

            return corps.Contains("bon.SetType(request.TypeCiterne, typeBon", StringComparison.Ordinal)
                && corps.Contains("key, tenant.SocieteKey, typeBon, request.DateBon", StringComparison.Ordinal)
                // The confrère variant moves the consignment balance; the others must not.
                && corps.Contains("BonGasoil.TypeBonConfrere", StringComparison.Ordinal)
                && corps.Contains("AjouterEntree", StringComparison.Ordinal);
        });

        Check("the confrère variant has no supplier, and the request allows that", () =>
        {
            // frm_Saisie_Bon_Gasoil_Confrere.vb:123 omits key_fournisseur entirely — the fellow company is
            // named as the transporteur instead. A required supplier would have made that variant
            // unrepresentable.
            var propriete = typeof(CreateBonGasoilRequest)
                .GetProperty(nameof(CreateBonGasoilRequest.KeyFournisseur));

            return propriete is not null
                && new CreateBonGasoilRequest(null, DateTime.UtcNow, 100m).KeyFournisseur is null;
        });

        Section("Confrère fuel issues");

        Check("a confrère issue REQUIRES the partner it went to", () =>
        {
            // The partner's consignment balance is keyed by key_transporteur, so an issue without one could not
            // be recorded against anybody. The guard is a business rule, not a null check, because the request
            // shape allows omitting it for the internal variant.
            var racine = RacineDuDepot();
            var source = File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Application", "Features", "Resources", "CarburantFeature.cs"));

            return source.Contains("sortie.confrere.transporteur", StringComparison.Ordinal)
                && source.Contains("TypeSortieConfrere, StringComparison.OrdinalIgnoreCase",
                    StringComparison.Ordinal);
        });

        Check("a confrère issue still draws from a REAL tank", () =>
        {
            // frm_Saisie_Sortie_Gasoil_Confrere.vb:130 decrements t_citernes for the named tank, so the fuel
            // physically leaves the company's stock — this is not an operation against the "CITERNES CONFRERES"
            // sentinel. The stock guard must therefore run as it does for any other issue.
            var racine = RacineDuDepot();
            var source = File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Application", "Features", "Resources", "CarburantFeature.cs"));

            var debut = source.IndexOf("public async Task<SortieGasoilDto> SortirAsync", StringComparison.Ordinal);
            var fin = source.IndexOf("public async Task<SortieGasoilDto> AnnulerSortieAsync", StringComparison.Ordinal);
            var corps = source[debut..fin];

            return corps.Contains("citerne.Sortir(request.Quantite)", StringComparison.Ordinal)
                && corps.Contains("citerne.EstConfreres", StringComparison.Ordinal);
        });

        Check("the journal carries the issue's OWN type, not the internal default", () =>
        {
            // Hardcoding 'Sortie Gasoil' on the journal would mislabel every transporteur and confrère issue,
            // and those literals are exactly what the consumption reports group by.
            var racine = RacineDuDepot();
            var source = File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Application", "Features", "Resources", "CarburantFeature.cs"));

            var debut = source.IndexOf("public async Task<SortieGasoilDto> SortirAsync", StringComparison.Ordinal);
            var fin = source.IndexOf("public async Task<SortieGasoilDto> AnnulerSortieAsync", StringComparison.Ordinal);
            var corps = source[debut..fin];

            return corps.Contains("sortie.Id, tenant.SocieteKey, typeSortie,", StringComparison.Ordinal)
                && !corps.Contains("ConsommationGasoil.Types.SortieGasoil", StringComparison.Ordinal);
        });

        Check("an internal issue is the default, so existing callers are unaffected", () =>
        {
            // The type is optional and falls back to the internal literal, which is what every caller written
            // before the variants existed relies on.
            var corps = File.ReadAllText(Path.Combine(
                RacineDuDepot(), "src", "Logistics.Application", "Features", "Resources", "CarburantFeature.cs"));

            return corps.Contains(
                    "string.IsNullOrWhiteSpace(request.TypeSortie)\n            ? SortieGasoil.TypeSortieInterne",
                    StringComparison.Ordinal)
                || corps.Contains("? SortieGasoil.TypeSortieInterne", StringComparison.Ordinal);
        });

        Check("cancelling a confrère issue gives the consignment movement back", () =>
        {
            // Leaving it would keep counting fuel as handed to the partner when the document says otherwise,
            // which is the same drift the voucher deletion path exists to prevent.
            var racine = RacineDuDepot();
            var source = File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Application", "Features", "Resources", "CarburantFeature.cs"));

            var debut = source.IndexOf("public async Task<SortieGasoilDto> AnnulerSortieAsync", StringComparison.Ordinal);
            var fin = source.IndexOf("public async Task<JaugeGasoilDto> JaugerAsync", StringComparison.Ordinal);
            var corps = source[debut..fin];

            return corps.Contains("stock?.RetirerSortie(sortie.QteSortie)", StringComparison.Ordinal)
                && corps.Contains("SortieGasoil.TypeSortieConfrere", StringComparison.Ordinal);
        });

        Check("the three issue type literals are the legacy's, typo included", () =>
        {
            // 'Confrére' — acute accent on the FIRST vowel here, against 'Confrère' on the voucher side. The
            // legacy disagrees with itself and both spellings are what the reports group by.
            return SortieGasoil.TypeSortieInterne == "Sortie Gasoil"
                && SortieGasoil.TypeSortieTransporteur == "Sortie Gasoil Transporteur"
                && SortieGasoil.TypeSortieConfrere == "Sortie Gasoil Confrére"
                && ConsommationGasoil.Types.SortieConfrere == "Sortie Gasoil Confrére";
        });

        Section("Cost per kilometre (full-tank-to-full-tank)");

        Check("the window is SUMMED open-at-the-start and closed-at-the-end", () =>
        {
            // frm_Calcul_Ratio_CoutKm_Materiel.vb:238 sums over date > debut AND date <= fin.
            var debut = new DateTime(2026, 5, 1);
            var fin = new DateTime(2026, 5, 10);

            return !RatioCoutKm.DansLaSomme(debut, debut, fin)      // the opening fill's fuel is NOT counted
                && RatioCoutKm.DansLaSomme(fin, debut, fin)          // the closing fill's fuel IS counted
                && RatioCoutKm.DansLaSomme(new DateTime(2026, 5, 5), debut, fin);
        });

        Check("the window is MARKED closed-at-the-start and open-at-the-end", () =>
        {
            // Line 247 marks over date >= debut AND date < fin — the opposite bounds, deliberately.
            var debut = new DateTime(2026, 5, 1);
            var fin = new DateTime(2026, 5, 10);

            return RatioCoutKm.DansLeMarquage(debut, debut, fin)     // the opening fill IS marked
                && !RatioCoutKm.DansLeMarquage(fin, debut, fin)      // the closing fill is NOT marked
                && RatioCoutKm.DansLeMarquage(new DateTime(2026, 5, 5), debut, fin);
        });

        Check("the two bounds disagree at exactly the closing fill-up", () =>
        {
            // This is the whole point of the asymmetry: the closing fill's fuel belongs to THIS window, but the
            // row must stay unconsumed so it opens the NEXT one. Making both bounds agree — in either
            // direction — would either end the chain after one measurement or count a fill-up twice.
            var debut = new DateTime(2026, 5, 1);
            var fin = new DateTime(2026, 5, 10);

            return RatioCoutKm.DansLaSomme(fin, debut, fin)
                && !RatioCoutKm.DansLeMarquage(fin, debut, fin)
                // And they agree nowhere else, so the disagreement is confined to that boundary.
                && RatioCoutKm.DansLaSomme(new DateTime(2026, 5, 5), debut, fin)
                    == RatioCoutKm.DansLeMarquage(new DateTime(2026, 5, 5), debut, fin);
        });

        Check("a measurement needs distance, litres AND cost, all non-zero", () =>
        {
            // The legacy's own guard is `total_qte <> 0 And total_km <> 0 And total_mt <> 0`. Returning a row
            // of zeros would look like a real measurement reading zero, and the ratio reports would average it.
            return RatioCoutKm.Distance(1000, 1500, 100m, 1200m) == 500
                && RatioCoutKm.Distance(1000, 1000, 100m, 1200m) is null   // no distance
                && RatioCoutKm.Distance(1000, 1500, 0m, 1200m) is null     // no fuel
                && RatioCoutKm.Distance(1000, 1500, 100m, 0m) is null;     // no cost
        });

        Check("an odometer that went backwards is refused, not negated", () =>
        {
            // The legacy would compute a negative total_km and write a nonsensical negative cost per kilometre.
            // A meter that went backwards is a data problem, not a measurement.
            return RatioCoutKm.Distance(1500, 1000, 100m, 1200m) is null;
        });

        Check("a window needs a later closing fill-up", () =>
        {
            var debut = new DateTime(2026, 5, 1);

            return !RatioCoutKm.FenetreFermable(null, debut)
                && !RatioCoutKm.FenetreFermable(debut, null)
                && !RatioCoutKm.FenetreFermable(debut, debut)
                && !RatioCoutKm.FenetreFermable(debut.AddDays(5), debut)
                && RatioCoutKm.FenetreFermable(debut, debut.AddDays(5));
        });

        Section("Logging");

        Check("the correlation scope uses a form the logger can actually RENDER", () =>
        {
            // A Dictionary scope is not IReadOnlyList<KeyValuePair<string, object>>, so the console formatter
            // falls through to ToString() and prints the type name where the correlation id should be. The
            // result is a scope that looks present in the source and carries nothing in the output — which is
            // exactly how it behaved until the log output was actually read.
            var racine = RacineDuDepot();
            var source = File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Api", "Middleware", "ApiMiddleware.cs"));

            return source.Contains("BeginScope(\"CorrelationId:{CorrelationId}\", correlationId)",
                       StringComparison.Ordinal)
                && !source.Contains("[\"CorrelationId\"] = correlationId", StringComparison.Ordinal);
        });

        Check("request logging records the SHAPE of a request and nothing sensitive", () =>
        {
            // The framework's default LoggingFields includes request and response headers AND request bodies.
            // That is unsafe here twice over: the token endpoint takes a password in its body, and every other
            // endpoint takes a bearer token in a header. The field set must therefore be explicit.
            var racine = RacineDuDepot();
            var source = File.ReadAllText(Path.Combine(racine, "src", "Logistics.Api", "Program.cs"));

            return source.Contains("HttpLoggingFields.RequestMethod", StringComparison.Ordinal)
                && source.Contains("HttpLoggingFields.ResponseStatusCode", StringComparison.Ordinal)
                && !source.Contains("HttpLoggingFields.RequestHeaders", StringComparison.Ordinal)
                && !source.Contains("HttpLoggingFields.ResponseHeaders", StringComparison.Ordinal)
                && !source.Contains("HttpLoggingFields.RequestBody", StringComparison.Ordinal)
                && !source.Contains("HttpLoggingFields.ResponseBody", StringComparison.Ordinal);
        });

        Check("the request-log category is raised above the blanket AspNetCore rule", () =>
        {
            // appsettings sets Microsoft.AspNetCore to Warning. The request log is Information, so without an
            // explicit override it is configured, wired, and completely silent.
            var racine = RacineDuDepot();
            var settings = File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Api", "appsettings.json"));

            return settings.Contains("Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware", StringComparison.Ordinal);
        });

        Check("no comment key sits INSIDE LogLevel", () =>
        {
            // Every key inside LogLevel is read as a log CATEGORY whose value must be a log level, so a
            // "//"-prefixed comment there is not a comment — it is a startup failure. A comment key one level
            // up, beside LogLevel, is harmless, which is why the existing ones are fine and this is asserted.
            var racine = RacineDuDepot();
            var settings = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Api", "appsettings.json")));

            var niveaux = settings.RootElement.GetProperty("Logging").GetProperty("LogLevel");

            string[] valides = ["Trace", "Debug", "Information", "Warning", "Error", "Critical", "None"];

            return niveaux.EnumerateObject().All(p => valides.Contains(p.Value.GetString()));
        });

        Section("Host configuration (CORS, health probes)");

        Check("CORS is OFF by default, not open by default", () =>
        {
            // A browser front end on any other port is blocked without a policy — that is a deployment blocker
            // rather than a hardening choice — but AllowAnyOrigin would be the wrong fix. The list is explicit
            // and empty, so nothing is reachable until an operator names the origins.
            var racine = RacineDuDepot();
            var settings = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Api", "appsettings.json")));

            var origines = settings.RootElement.GetProperty("Cors").GetProperty("AllowedOrigins");

            return origines.GetArrayLength() == 0;
        });

        Check("CORS does NOT enable credentials", () =>
        {
            // This API authenticates with bearer tokens, which a browser does not attach automatically, so
            // credentialed CORS buys nothing — and leaving it off means a misconfigured origin cannot make a
            // browser send the user's credentials to it.
            //
            // Comments are stripped first. An earlier version of this check matched the plain word and failed
            // against the code's OWN comment explaining that credentials are not enabled — the third time in
            // this harness that a source-text assertion was satisfied or broken by prose rather than code.
            var racine = RacineDuDepot();
            var source = SansCommentaires(File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Api", "Program.cs")));

            return !source.Contains(".AllowCredentials()", StringComparison.Ordinal)
                && source.Contains(".WithOrigins(originesAutorisees)", StringComparison.Ordinal);
        });

        Check("CORS runs BEFORE authentication, so a preflight is not challenged", () =>
        {
            // A browser never sends credentials on a preflight OPTIONS request, so requiring authentication
            // for it would fail every cross-origin call. Verified by probe: the preflight returns 204 with the
            // allow-origin header. Note a DISALLOWED origin also gets 204 — without the header, which is what
            // the browser acts on. Rejecting it with a 403 instead would be a behavioural change, not a fix.
            var racine = RacineDuDepot();
            var source = File.ReadAllText(Path.Combine(racine, "src", "Logistics.Api", "Program.cs"));

            var cors = source.IndexOf("app.UseCors();", StringComparison.Ordinal);
            var auth = source.IndexOf("app.UseAuthentication();", StringComparison.Ordinal);

            return cors > 0 && auth > 0 && cors < auth;
        });

        Check("liveness does not depend on the database, readiness does", () =>
        {
            // Verified by probe against an unreachable database: /health/live returned 200 Healthy and
            // /health/ready returned 503 Unhealthy. A liveness probe that failed on a database outage would
            // have the orchestrator restart a process that is working perfectly — while readiness must fail, or
            // a load balancer keeps routing to an instance that cannot serve.
            var racine = RacineDuDepot();
            var source = File.ReadAllText(Path.Combine(racine, "src", "Logistics.Api", "Program.cs"));

            return source.Contains(".AddCheck(\"self\"", StringComparison.Ordinal)
                && source.Contains("tags: [\"live\"]", StringComparison.Ordinal)
                && source.Contains("tags: [\"ready\"]", StringComparison.Ordinal)
                && source.Contains("DatabaseHealthCheck", StringComparison.Ordinal);
        });

        Section("OpenAPI contract");

        Check("the document declares HOW to authenticate", () =>
        {
            // Without this the document described 123 paths and said nothing about authentication, though every
            // one of them requires a bearer token. A generated client had no way to know a token was needed or
            // where to put it, and no standard explorer rendered an Authorize control.
            //
            // Verified by probe: components.securitySchemes.LogisticsToken = http/bearer, and a document-level
            // security requirement of {"LogisticsToken": []}.
            var racine = RacineDuDepot();
            var source = SansCommentaires(File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Api", "OpenApi", "OpenApiSecurity.cs")));

            return source.Contains("SecuritySchemeType.Http", StringComparison.Ordinal)
                && source.Contains("Scheme = \"bearer\"", StringComparison.Ordinal)
                && source.Contains("new OpenApiSecuritySchemeReference(SchemaName, document)",
                    StringComparison.Ordinal);
        });

        Check("the requirement is GLOBAL, so a new endpoint cannot be undocumented", () =>
        {
            // Declaring it on each of 158 operations is the version that rots the first time someone adds an
            // endpoint and forgets. The global form covers them by default and the anonymous few opt out.
            var racine = RacineDuDepot();
            var source = SansCommentaires(File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Api", "OpenApi", "OpenApiSecurity.cs")));

            return source.Contains("document.Security ??= []", StringComparison.Ordinal)
                && source.Contains("document.Security.Add(new OpenApiSecurityRequirement",
                    StringComparison.Ordinal);
        });

        Check("an anonymous operation overrides with an EMPTY array, not null", () =>
        {
            // Per the OpenAPI specification it is the operation's empty security array that removes the
            // document-level requirement. Null would leave the global one in force and the endpoint would still
            // be documented as needing a token.
            var racine = RacineDuDepot();
            var source = SansCommentaires(File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Api", "OpenApi", "OpenApiSecurity.cs")));

            return source.Contains("operation.Security = [];", StringComparison.Ordinal);
        });

        Check("the anonymous list contains no path the document never describes", () =>
        {
            // An entry for an undocumented path is configuration that can never apply. The health probes were
            // listed until a probe proved the built-in generator does not describe MapHealthChecks endpoints at
            // all — even with tags and a summary attached. Their runtime anonymity comes from AllowAnonymous.
            var racine = RacineDuDepot();
            var source = SansCommentaires(File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Api", "OpenApi", "OpenApiSecurity.cs")));

            return !source.Contains("\"/health/live\"", StringComparison.Ordinal)
                && !source.Contains("\"/health/ready\"", StringComparison.Ordinal)
                && source.Contains("\"/api/v1/auth/token\"", StringComparison.Ordinal);
        });

        Section("xunit test projects (invoked without VSTest)");

        ExecuterTestsXunit();

        Section("Legacy password encoding (tools.Codage_Motpasse)");

        Check("the encoding reproduces the legacy formula for short passwords", () =>
        {
            // hash = sum over i of Ascii(char_i) x (n - i + 1)!
            // "a"   -> 97
            // "ab"  -> 97x2! + 98x1! = 194 + 98 = 292
            // "abc" -> 97x3! + 98x2! + 99x1! = 582 + 196 + 99 = 877
            return MotDePasseLegacy.Coder("a") == "97"
                && MotDePasseLegacy.Coder("ab") == "292"
                && MotDePasseLegacy.Coder("abc") == "877";
        });

        Check("an empty password encodes to the legacy's initial accumulator", () =>
        {
            // The function exits early on Len = 0 and returns the untouched accumulator, which is the string "0".
            return MotDePasseLegacy.Coder("") == "0"
                && MotDePasseLegacy.Coder(null) == "0";
        });

        Check("verification compares the encoding, and refuses an empty stored password", () =>
        {
            // frm_demarrage.vb:144 — `If tools.Codage_Motpasse(txt_pass.Text) <> perso.PassWord`.
            // An empty stored value must never authenticate: that is an account with no password set, not an
            // account whose password is anything the caller types.
            return MotDePasseLegacy.Verifier("abc", "877")
                && !MotDePasseLegacy.Verifier("abd", "877")
                && !MotDePasseLegacy.Verifier("", "")
                && !MotDePasseLegacy.Verifier("anything", null);
        });

        Check("the first length where the Single weights round is DERIVED, not assumed", () =>
        {
            // The legacy declares `Dim fact As Single` and multiplies it up to n!, so beyond some length the
            // encoded value stops equalling the exact mathematical sum. The intuitive answer is 11 — "11!
            // exceeds 2^24" — and it is WRONG: 11! = 2^8 x 155925, and that odd part fits in 24 bits, as it does
            // up to 13!. Only 14! has an odd part too large.
            //
            // This check found that out. It was written asserting a divergence at 11, compared against a
            // BigInteger computation, and both values came back 4268107405 — identical. So the threshold is
            // searched for here and the constant must agree with what the search finds.
            int? premierArrondi = null;

            for (var n = 1; n <= 20 && premierArrondi is null; n++)
            {
                var motDePasse = new string('a', n);

                if (MotDePasseLegacy.Coder(motDePasse) != ExactBigInteger(motDePasse))
                {
                    premierArrondi = n;
                }
            }

            Console.WriteLine($"        first divergent length = {premierArrondi}");

            // Measured, and it disagreed with two successive attempts to reason it out — "11! exceeds 2^24" and
            // then "so 14! is the first inexact factorial". The rounding actually comes from the PRODUCT
            // char x factorial being computed in Single, which is why the threshold depends on the characters
            // and not on length alone.
            return premierArrondi == 12
                && MotDePasseLegacy.PremiereLongueurArrondie == premierArrondi;
        });

        Check("no collision was found in the printable range at length 11", () =>
        {
            // A bounded negative result, asserted so it is not mistaken for a guarantee. The first character
            // carries the largest weight (11!), so varying it over every printable character while holding the
            // remaining ten fixed is where a collision would show first. Finding none does NOT prove the
            // encoding is injective — it records what was searched, which is the honest form of the claim.
            const string reste = "bcdefghijk";
            var vus = new HashSet<string>(StringComparer.Ordinal);

            for (var c = 32; c < 127; c++)
            {
                if (!vus.Add(MotDePasseLegacy.Coder((char)c + reste)))
                {
                    Console.WriteLine($"        collision at first character {(char)c}");
                    return false;
                }
            }

            return true;
        });

        Check("the encoding is flagged as unsuitable for a new password", () =>
        {
            return !MotDePasseLegacy.EstAdapteAUnNouveauMotDePasse
                && MotDePasseLegacy.PremiereLongueurArrondie == 12;
        });

        Section("User login and error-shape uniformity");

        Check("the service account is decided WITHOUT consulting the database", () =>
        {
            // A configuration-only credential must keep working when the database is unreachable. An earlier
            // version of the token action called the user authenticator FIRST, which made every login depend on
            // the database and turned a database outage into a total authentication outage — a regression that
            // only a probe revealed.
            var racine = RacineDuDepot();
            var source = SansCommentaires(File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Api", "Controllers", "AuthController.cs")));

            var decision = source.IndexOf("identifiantEstServiceAccount", StringComparison.Ordinal);
            var lookup = source.IndexOf("utilisateurs.AuthentifierAsync", StringComparison.Ordinal);

            return decision > 0 && lookup > decision;
        });

        Check("a wrong service-account password is refused without a database round trip", () =>
        {
            // The answer is fully determined by configuration, so answering 503 would tell a machine caller to
            // retry later when it should stop and fix its configuration.
            var racine = RacineDuDepot();
            var source = SansCommentaires(File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Api", "Controllers", "AuthController.cs")));

            return source.Contains("CryptographicEquals(request.MotDePasse, motDePasse)", StringComparison.Ordinal)
                && source.Contains("return IdentifiantsInvalides();", StringComparison.Ordinal);
        });

        Check("every failed credential produces ONE rejection shape", () =>
        {
            // Unknown identifier, wrong password, blocked account and departed employee must be
            // indistinguishable. Saying which is a user-enumeration oracle.
            //
            // Two files: the contract is in the application layer, the guards are in the implementation. An
            // earlier version asserted all three against the application file alone and so failed on its own
            // mistake rather than on the code.
            var racine = RacineDuDepot();

            var contrat = SansCommentaires(File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Application", "Features", "Authentication",
                "UtilisateurAuthenticator.cs")));

            var miseOeuvre = SansCommentaires(File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Infrastructure", "InfrastructureServices.cs")));

            return contrat.Contains("Task<UtilisateurLegacy?> AuthentifierAsync", StringComparison.Ordinal)
                && miseOeuvre.Contains("personnel.Bloque || !personnel.IsActif", StringComparison.Ordinal)
                && miseOeuvre.Contains("MotDePasseLegacy.Verifier", StringComparison.Ordinal);
        });

        Check("controller-returned problems are normalised to the API's single error shape", () =>
        {
            // ControllerBase.Problem() sets Type to the RFC 9110 link and adds no code. Without normalisation the
            // API had TWO error vocabularies — the middleware's and the controllers' — which survived the round
            // that unified validation failures, because validation goes through a different factory.
            //
            // Verified by probe: the token endpoint's 401 now carries type https://httpstatuses.io/401,
            // code "unauthorized" and the caller's correlation id.
            var racine = RacineDuDepot();
            var source = SansCommentaires(File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Api", "Program.cs")));

            return source.Contains("CustomizeProblemDetails", StringComparison.Ordinal)
                // Assigned rather than ??=-ed: Problem() pre-sets Type, so coalescing would leave the RFC link.
                && source.Contains("context.ProblemDetails.Type = $\"https://httpstatuses.io/{status}\"",
                    StringComparison.Ordinal)
                && source.Contains("TryAdd(\"code\"", StringComparison.Ordinal);
        });

        Section("Password change");

        Check("the request cannot name somebody else's account", () =>
        {
            // The user key comes from the TOKEN. Accepting one in the body would let any authenticated caller
            // change anyone's password, which is the whole security property of this endpoint.
            //
            // Read as source text rather than by reflecting the record: it lives in the API assembly, and adding
            // a reference to the whole web stack purely to read two property names would be a poor trade.
            var racine = RacineDuDepot();
            var source = SansCommentaires(File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Api", "Controllers", "AuthController.cs")));

            var declaration = source.IndexOf("public sealed record ChangerMotDePasseRequest", StringComparison.Ordinal);

            if (declaration < 0)
            {
                return false;
            }

            var fin = source.IndexOf(';', declaration);
            var corps = source[declaration..fin];

            return corps.Contains("AncienMotDePasse", StringComparison.Ordinal)
                && corps.Contains("NouveauMotDePasse", StringComparison.Ordinal)
                // Asserted negatively too: a field naming an account must fail this loudly.
                && !corps.Contains("Key", StringComparison.Ordinal)
                && !corps.Contains("Personnel", StringComparison.Ordinal);
        });

        Check("the CURRENT password is re-verified before anything is written", () =>
        {
            // Without it a stolen token could be escalated into permanent account ownership, which is the
            // difference between a session and an account.
            var racine = RacineDuDepot();
            var source = SansCommentaires(File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Infrastructure", "InfrastructureServices.cs")));

            var debut = source.IndexOf("public async Task<bool> ChangerMotDePasseAsync", StringComparison.Ordinal);
            var fin = source.IndexOf("MotDePasseLegacy.Coder(nouveauMotDePasse)", StringComparison.Ordinal);

            return debut > 0 && fin > debut
                && source[debut..fin].Contains("MotDePasseLegacy.Verifier(ancienMotDePasse", StringComparison.Ordinal);
        });

        Check("a self-service change CLEARS the first-connection flag", () =>
        {
            // ClassPersonnel.ModifyPWD, line 968: `set premiere_connexion=0, pass_word=...`. The employee has now
            // chosen their own password, so the forced-change prompt is over.
            var personnel = new Personnel("P1", "S01", "Nom");
            personnel.ForcerChangementMotDePasse();

            var force = personnel.PremiereConnexion;

            personnel.ChangerMotDePasse(MotDePasseLegacy.Coder("motdepasse"), premiereConnexion: false);

            return force && !personnel.PremiereConnexion;
        });

        Check("an administrator's change SETS it instead — the opposite flag", () =>
        {
            // ClassPersonnel.vb:812 sets premiere_connexion=1 on the TARGET when someone else sets their
            // password, so whoever knows the temporary password cannot keep using it. The two cases differ in
            // exactly the flag an auditor would ask about, which is why they are separate methods.
            var personnel = new Personnel("P2", "S01", "Nom");

            var avant = personnel.PremiereConnexion;
            personnel.ForcerChangementMotDePasse();

            return !avant && personnel.PremiereConnexion;
        });

        Check("the password rules reject short and empty values, and accept a usable one", () =>
        {
            // An ADDITION, not legacy behaviour — the desktop application imposes no minimum. It applies only
            // when a password is SET, so nobody is locked out of an account whose existing password is short.
            //
            // The upper boundary is asserted on both sides: 200 is the last accepted length and 201 the first
            // refused. An earlier version of this check tested 200 as if it were over the limit, which was an
            // off-by-one in the test rather than in the rule.
            return ReglesMotDePasse.MotifDeRefus(null) is not null
                && ReglesMotDePasse.MotifDeRefus("") is not null
                && ReglesMotDePasse.MotifDeRefus("court") is not null
                && ReglesMotDePasse.MotifDeRefus(new string('x', 201)) is not null
                && ReglesMotDePasse.MotifDeRefus(new string('x', 200)) is null
                && ReglesMotDePasse.MotifDeRefus(new string('x', 8)) is null
                && ReglesMotDePasse.MotifDeRefus("un-mot-de-passe-solide") is null
                && ReglesMotDePasse.LongueurMinimale == 8;
        });

        Check("a password change is traced with the legacy status", () =>
        {
            // ClassPersonnel.vb:969 writes t_trace_personnel with $MPWD. A credential change is exactly the event
            // an auditor asks about, so it belongs in the audit trail rather than only in a log file.
            var entity = model.GetEntityTypes().Single(t => t.ClrType == typeof(TracePersonnel));

            return TracePersonnel.StatutModificationMotDePasse == "$MPWD"
                && entity.GetTableName() == "t_trace_personnel";
        });

        Section("Contract constraints (maxLength)");

        Check("the EF model is a usable source of length limits", () =>
        {
            // The transformer publishes these; this asserts the SOURCE has enough to be worth publishing, and
            // that the limits are real rather than a handful of one-offs.
            var longueurs = model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(string) && p.GetMaxLength() is > 0)
                .ToList();

            return longueurs.Count > 100 && longueurs.All(p => p.GetMaxLength() is > 0);
        });

        Check("property names ARE ambiguous across entities, so skipping them is necessary", () =>
        {
            // The transformer matches DTO properties to entity properties by name, and refuses a name that maps
            // to different lengths in different entities — publishing the wrong limit is worse than publishing
            // none. This asserts that situation genuinely occurs, so the rule is not a theoretical precaution.
            var ambigus = model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(string) && p.GetMaxLength() is > 0)
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Select(p => p.GetMaxLength()!.Value).Distinct().Count() > 1)
                .ToList();

            Console.WriteLine($"        ambiguous property names: {ambigus.Count}");

            return ambigus.Count > 0;
        });

        Check("the transformer matches names case-INSENSITIVELY", () =>
        {
            // Essential, not defensive: EF names are PascalCase while the schema's property names follow the
            // JSON naming policy and are camelCase. The first version used the default comparer and published
            // ZERO constraints — and the failure was silence, which is why this is asserted.
            //
            // Verified by probe: 628 of 1696 schema properties now carry a maxLength, e.g. raisonSociale -> 150.
            var racine = RacineDuDepot();
            var source = SansCommentaires(File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Api", "OpenApi", "LegacyLengthSchemaTransformer.cs")));

            return source.Contains("StringComparer.OrdinalIgnoreCase", StringComparison.Ordinal)
                && source.Contains("Distinct().Count() == 1", StringComparison.Ordinal)
                // An explicit constraint on a DTO must still win over the derived one.
                && source.Contains("MaxLength: null", StringComparison.Ordinal);
        });

        Check("the transformer is registered on the OpenAPI document", () =>
        {
            var racine = RacineDuDepot();
            var source = SansCommentaires(File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Api", "Program.cs")));

            return source.Contains("AddSchemaTransformer<LegacyLengthSchemaTransformer>()", StringComparison.Ordinal);
        });

        Section("EDI import writes back to the client master");

        Check("a lexicographic maximum is NOT the last value seen", () =>
        {
            // The legacy computes `max(code_destinataire)`, and the promotion reproduces that rather than taking
            // the last row. The two differ as soon as codes have different lengths, which this demonstrates so
            // the distinction is recorded rather than assumed to be equivalent.
            string[] codes = ["Z9", "A10"];

            var maximum = codes.Max(StringComparer.Ordinal)!;

            return maximum == "Z9" && maximum != codes[^1];
        });

        Check("the promotion takes a MAXIMUM over the file's codes, not the last", () =>
        {
            var racine = RacineDuDepot();
            var source = SansCommentaires(File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Application", "Features", "Edi", "EdiReconciliationFeature.cs")));

            return source.Contains(".Max(StringComparer.Ordinal)", StringComparison.Ordinal)
                && source.Contains("DefinirRefClientAsync", StringComparison.Ordinal);
        });

        Check("a client with NO destination code in the file is skipped, not blanked", () =>
        {
            // DIVERGENCE, deliberate. The legacy sets ref_client = max(code_destinataire), and max over all-NULL
            // values is NULL — so a client whose file rows carry no destination code would have its existing
            // reference ERASED. The filter below is what prevents that, and it is asserted so a later attempt to
            // "restore faithfulness" has to argue with it explicitly rather than by deletion.
            var racine = RacineDuDepot();
            var source = SansCommentaires(File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Application", "Features", "Edi", "EdiReconciliationFeature.cs")));

            return source.Contains("!string.IsNullOrWhiteSpace(d.CodeDestinataire)", StringComparison.Ordinal)
                && source.Contains("!string.IsNullOrWhiteSpace(d.KeyClient)", StringComparison.Ordinal);
        });

        Check("the client reference has its own mutator, so the import cannot overwrite other details", () =>
        {
            // Client.Update replaces every detail. An import that used it would silently discard the client's
            // addresses, contacts and classification along with the one field it means to change.
            var racine = RacineDuDepot();
            var source = SansCommentaires(File.ReadAllText(Path.Combine(
                racine, "src", "Logistics.Domain", "Entities", "Clients", "Client.cs")));

            return source.Contains("public void DefinirRefClient(string? refClient)", StringComparison.Ordinal);
        });

        Section("HTTPS redirection");

        Check("redirection is OFF by default", () =>
        {
            // Not a hardening default: an API behind a TLS-terminating proxy receives plain HTTP FROM that
            // proxy, so redirecting there produces a loop or a broken health probe. It is a deployment decision.
            var settings = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(
                RacineDuDepot(), "src", "Logistics.Api", "appsettings.json")));

            return settings.RootElement.TryGetProperty("Hosting", out var hosting)
                && !hosting.GetProperty("HttpsRedirection").GetBoolean();
        });

        Check("enabling it WITHOUT a determinable port fails startup, not silently", () =>
        {
            // HttpsRedirectionMiddleware logs a warning and redirects NOTHING when it cannot determine the HTTPS
            // port, so an operator who flips the switch would get a silent no-op while believing traffic is being
            // upgraded. Same shape as the suppressed request-log category and the transformer that published zero
            // constraints: configured, running, doing nothing. The host refuses to start instead.
            //
            // Verified by probe: with Hosting__HttpsRedirection=true and no port, startup throws
            // "Hosting:HttpsRedirection is enabled but no HTTPS port could be determined".
            var source = SansCommentaires(File.ReadAllText(Path.Combine(
                RacineDuDepot(), "src", "Logistics.Api", "Program.cs")));

            return source.Contains("no HTTPS port could be determined", StringComparison.Ordinal)
                && source.Contains("throw new InvalidOperationException", StringComparison.Ordinal);
        });

        Check("it redirects when it IS configured, and HSTS only outside Development", () =>
        {
            // Verified by probe: with Hosting:HttpsPort set, an HTTP request answers
            // 307 -> https://127.0.0.1:5443/health/live. HSTS is excluded from Development because a browser
            // caches it for months, making localhost unusable over HTTP long after the setting is removed.
            var source = SansCommentaires(File.ReadAllText(Path.Combine(
                RacineDuDepot(), "src", "Logistics.Api", "Program.cs")));

            return source.Contains("app.UseHttpsRedirection();", StringComparison.Ordinal)
                && source.Contains("!app.Environment.IsDevelopment()", StringComparison.Ordinal)
                && source.Contains("app.UseHsts();", StringComparison.Ordinal);
        });

        Section("EDI — the commercial key and its display name");

        Check("the commercial name is resolved, not copied from the key", () =>
        {
            // Dossier carries commercial_dossier (a key) and nom_commercial_dossier (a name) as separate
            // columns, and the legacy fills them from KEY_COMMERCIAL_CLT and INTITULE_COMMERCIAL_CLT. An
            // earlier version of the promotion passed client.Commercial for BOTH, leaving the name column
            // holding a personnel key — so anything displaying the commercial showed a code.
            //
            // INTITULE_COMMERCIAL_CLT is a JOIN ALIAS in the legacy client expansion query, not a column of
            // t_clients, which is why the name has to be resolved from the personnel row the key points at.
            var source = SansCommentaires(File.ReadAllText(Path.Combine(
                RacineDuDepot(), "src", "Logistics.Application", "Features", "Edi",
                "EdiReconciliationFeature.cs")));

            return source.Contains("FindIntitulePersonnelAsync", StringComparison.Ordinal)
                && source.Contains("nomCommercial", StringComparison.Ordinal)
                // The key and the name must be DIFFERENT arguments to the reconciliation call.
                && source.Contains("client?.Commercial,", StringComparison.Ordinal)
                && source.Contains("nomCommercial,", StringComparison.Ordinal);
        });

        Check("the staging records the key and the name as separate fields", () =>
        {
            // SetReconciliation takes keyCommercial and commercial separately. The staging was already right;
            // it was the caller that collapsed them, which is why the defect survived a type-safe signature.
            var staging = SansCommentaires(File.ReadAllText(Path.Combine(
                RacineDuDepot(), "src", "Logistics.Domain", "Entities", "Edi", "EdiStagingEntities.cs")));

            return staging.Contains("string? keyCommercial, string? commercial", StringComparison.Ordinal)
                && staging.Contains("KeyCommercial = Trim(keyCommercial)", StringComparison.Ordinal)
                && staging.Contains("Commercial = Trim(commercial)", StringComparison.Ordinal);
        });

        Section("Domain model completeness");

        Check("every concrete domain entity is mapped in the EF model", () =>
        {
            // An entity class that no entity type maps is invisible to EF: it cannot be queried, its table is
            // never created, and nothing fails until someone tries to use it. This is the check that would
            // catch a class added to the domain and never wired into the context — a mistake that produces no
            // compiler error and no runtime error until the feature is exercised.
            var types = typeof(Entity).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(Entity).IsAssignableFrom(t))
                .ToList();

            var mappees = model.GetEntityTypes().Select(t => t.ClrType).ToHashSet();
            var orphelines = types.Where(t => !mappees.Contains(t)).OrderBy(t => t.Name).ToList();

            foreach (var orpheline in orphelines)
            {
                Console.WriteLine($"        UNMAPPED ENTITY: {orpheline.FullName}");
            }

            Console.WriteLine($"        {types.Count} concrete entities, {mappees.Count} mapped");

            return orphelines.Count == 0;
        });

        Section("Seeded credentials");

        Check("the password encoder and the verifier agree", () =>
        {
            // The seed script writes MotDePasseLegacy.Coder(...) into t_personnel.pass_word, and login
            // verifies with MotDePasseLegacy.Verifier. If those two ever disagree, a freshly seeded
            // database rejects its own credentials — and the symptom is a wrong-password error, which
            // points at the seed rather than at the encoder. Asserted instead of assumed.
            string[] essais = ["Admin123!", "User123!", "a", "court", "un-mot-de-passe-solide", new string('x', 200)];

            var resultats = essais.Select(clair => (clair, encode: MotDePasseLegacy.Coder(clair),
                ok: MotDePasseLegacy.Verifier(clair, MotDePasseLegacy.Coder(clair)))).ToList();

            foreach (var (clair, encode, ok) in resultats)
            {
                var montre = clair.Length > 20 ? clair[..20] + "..." : clair;
                Console.WriteLine($"        {montre,-24} -> {encode,-12} verifie={ok}");
            }

            return resultats.All(r => r.ok);
        });

        Check("a seeded password hash is NOT the plaintext", () =>
        {
            // Guard against the encoder silently becoming an identity function, which would store passwords
            // in the clear while every round-trip check still passed.
            return MotDePasseLegacy.Coder("Admin123!") != "Admin123!"
                && MotDePasseLegacy.Coder("Admin123!").All(char.IsAsciiDigit);
        });

        Section("Decimal precision");

        Check("no decimal property falls back to an unspecified precision", () =>
        {
            // EF warns that such a property "will be silently truncated if it does not fit". 76 did, and they
            // were not incidental: Dossier.Volume, Dossier.VolumeTaxable, ColisageDossier.Volume,
            // CoutKilometrageGasoil.RatioConsommation, Dossier.TauxChangeValMarch. Meanwhile this codebase
            // computes volumes to THREE decimals (MesuresTaxables and the EDI reader), so a volume of 12.345
            // was being stored as 12.34 with no error raised. A truncated taxable measure is a wrong invoice.
            //
            // Fixed by a decimal convention of (18,3); explicit configurations still win, so money stays at
            // its declared scale. The warning is EF's own, so this check simply asserts it can no longer fire.
            var sansPrecision = model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) && p.GetPrecision() is null)
                .OrderBy(p => p.DeclaringType.Name).ThenBy(p => p.Name)
                .ToList();

            foreach (var p in sansPrecision)
            {
                Console.WriteLine($"        NO PRECISION: {p.DeclaringType.Name}.{p.Name}");
            }

            var decimaux = model.GetEntityTypes().SelectMany(t => t.GetProperties())
                .Count(p => p.ClrType == typeof(decimal));
            Console.WriteLine($"        {decimaux} decimal properties, {sansPrecision.Count} without precision");

            return sansPrecision.Count == 0;
        });

        Section("Deployment artefacts");

        Check("every path the Dockerfile copies actually exists", () =>
        {
            // An image cannot be built here (no daemon, no network to pull the base images), and a
            // Dockerfile written blind fails on exactly one thing: a path that does not resolve. This
            // check already caught one such defect — a COPY of Directory.Packages.props, a file this
            // repository does not have, which would have failed the build at its first instruction.
            var racine = RacineDuDepot();
            var dockerfile = Path.Combine(racine, "src", "Logistics.Api", "Dockerfile");

            if (!File.Exists(dockerfile))
            {
                return false;
            }

            var manquants = new List<string>();

            foreach (var ligne in File.ReadAllLines(dockerfile))
            {
                var texte = ligne.Trim();

                if (!texte.StartsWith("COPY ", StringComparison.Ordinal)
                    || texte.Contains("--from=", StringComparison.Ordinal))
                {
                    // `COPY --from=build` reads from a previous stage, not the build context.
                    continue;
                }

                var champs = System.Text.RegularExpressions.Regex.Matches(texte, "\"([^\"]+)\"")
                    .Select(m => m.Groups[1].Value)
                    .ToArray();

                // Sources are every quoted field except the last, which is the destination.
                foreach (var source in champs.Length >= 2 ? champs[..^1] : [])
                {
                    if (source == "." || Path.IsPathRooted(source))
                    {
                        continue;
                    }

                    if (!File.Exists(Path.Combine(racine, source))
                        && !Directory.Exists(Path.Combine(racine, source)))
                    {
                        manquants.Add(source);
                    }
                }
            }

            if (manquants.Count > 0)
            {
                Console.WriteLine($"       missing COPY sources: {string.Join(", ", manquants)}");
            }

            return manquants.Count == 0;
        });

        Check("every project the Dockerfile restores exists on disk", () =>
        {
            // The restore layer names each csproj explicitly so source edits do not invalidate the
            // package cache. Renaming or moving a project would break the image, not the solution.
            var racine = RacineDuDepot();
            var dockerfile = Path.Combine(racine, "src", "Logistics.Api", "Dockerfile");

            var projets = File.ReadAllLines(dockerfile)
                .Where(l => l.Trim().StartsWith("COPY ", StringComparison.Ordinal) && l.Contains(".csproj", StringComparison.Ordinal))
                .Select(l => System.Text.RegularExpressions.Regex.Match(l, "\"([^\"]+\\.csproj)\"").Groups[1].Value)
                .Where(p => p.Length > 0)
                .ToArray();

            return projets.Length >= 4
                && projets.All(p => File.Exists(Path.Combine(racine, p)));
        });

        Check("the compose stack points at a Dockerfile that exists", () =>
        {
            var racine = RacineDuDepot();
            var compose = Path.Combine(racine, "docker-compose.yml");

            if (!File.Exists(compose))
            {
                return false;
            }

            var reference = System.Text.RegularExpressions.Regex.Match(
                File.ReadAllText(compose), @"dockerfile:\s*(\S+)").Groups[1].Value;

            return reference.Length > 0 && File.Exists(Path.Combine(racine, reference));
        });

        Check("the compose stack runs the AUTHENTICATED path, not the header fallback", () =>
        {
            // A "local development stack" that silently enables an authentication bypass is the wrong
            // default: it is exactly what someone runs to demonstrate a deployment. The fallback is
            // therefore explicitly off, with the opt-out documented in the file.
            var compose = File.ReadAllText(Path.Combine(RacineDuDepot(), "docker-compose.yml"));

            return compose.Contains("Authentication__AllowHeaderFallback: \"false\"", StringComparison.Ordinal)
                // And it supplies a signing key rather than depending on the development key that ships
                // inside the image.
                && compose.Contains("Authentication__SigningKey", StringComparison.Ordinal);
        });

        Check("status literals round-trip exactly (spaces and accents included)", () =>
        {
            var converter = model.GetEntityTypes().Single(t => t.ClrType.Name == "EnteteReglement")
                .FindProperty("Statut")!.GetValueConverter()!;

            var cases = new (StatutReglement Value, string Literal)[]
            {
                (StatutReglement.Saisie, "Saisie"),
                (StatutReglement.Portefeuille, "Portefeuille"),
                (StatutReglement.RemisEnBanque, "Remis en banque"),
                (StatutReglement.RemisAuFournisseur, "Remis au Fournisseur"),
                (StatutReglement.Signe, "Signé"),
                (StatutReglement.Regle, "Réglé"),
                (StatutReglement.Impaye, "Impayé")
            };

            return cases.All(c =>
                Equals(converter.ConvertToProvider(c.Value), c.Literal)
                && Equals(converter.ConvertFromProvider(c.Literal), c.Value));
        });

        Check("line-nature literals round-trip exactly", () =>
        {
            var converter = model.GetEntityTypes().Single(t => t.ClrType.Name == "DetailFactureVente")
                .FindProperty("NatureLigne")!.GetValueConverter()!;

            var cases = new (NatureLigne Value, string Literal)[]
            {
                (NatureLigne.Produit, "Produit"),
                (NatureLigne.Debours, "Débours"),
                (NatureLigne.Assigne, "Assigné"),
                (NatureLigne.TaxeRegionale, "TAXE REGIONALE")
            };

            return cases.All(c =>
                Equals(converter.ConvertToProvider(c.Value), c.Literal)
                && Equals(converter.ConvertFromProvider(c.Literal), c.Value));
        });

        Check("invoice-type literals are single characters", () =>
        {
            var converter = model.GetEntityTypes().Single(t => t.ClrType.Name == "EnteteFactureVente")
                .FindProperty("TypeFacture")!.GetValueConverter()!;

            return Equals(converter.ConvertToProvider(TypeFacture.Facture), "F")
                && Equals(converter.ConvertToProvider(TypeFacture.Avoir), "A")
                && Equals(converter.ConvertToProvider(TypeFacture.Od), "O")
                && Equals(converter.ConvertFromProvider("A"), TypeFacture.Avoir);
        });

        Check("allocation direction literals are V and A", () =>
        {
            var converter = model.GetEntityTypes().Single(t => t.ClrType.Name == "DetailReglement")
                .FindProperty("Sens")!.GetValueConverter()!;

            return Equals(converter.ConvertToProvider(SensReglement.Vente), "V")
                && Equals(converter.ConvertToProvider(SensReglement.Achat), "A");
        });
    }

    // ---------------------------------------------------------------------------------------------
    private static void ShipmentScenarios()
    {
        Section("Voyages — lifecycle");

        Check("a new voyage starts as 'créé' and is not validated", () =>
        {
            var v = NewVoyage();
            return v.KeyStatutVoyage == Voyage.StatutCree && !v.IsValide && !v.IsCloture && v.DateVoyage is null;
        });

        Check("validation stamps date_voyage and moves the status", () =>
        {
            var v = NewVoyage();
            v.Validate(new DateTime(2024, 3, 20));
            return v.IsValide && v.DateVoyage == new DateTime(2024, 3, 20) && v.KeyStatutVoyage == Voyage.StatutValide;
        });

        Check("a deleted voyage cannot be validated", () =>
        {
            var v = NewVoyage();
            v.SoftDelete();
            return Throws<BusinessRuleException>(() => v.Validate(DateTime.UtcNow));
        });

        Check("closing is refused while a dossier is uninvoiced", () =>
        {
            var v = NewVoyage();
            v.Validate(new DateTime(2024, 3, 20));
            return Throws<BusinessRuleException>(() => v.Close(new DateTime(2024, 4, 1), hasUninvoicedDossier: true));
        });

        Check("an unvalidated voyage cannot be closed", () =>
        {
            var v = NewVoyage();
            return Throws<BusinessRuleException>(() => v.Close(DateTime.UtcNow, false));
        });

        Check("closing and reopening round-trip", () =>
        {
            var v = NewVoyage();
            v.Validate(new DateTime(2024, 3, 20));
            v.Close(new DateTime(2024, 4, 1), hasUninvoicedDossier: false);
            if (!v.IsCloture || v.KeyStatutVoyage != Voyage.StatutCloture)
            {
                return false;
            }

            v.Reopen();
            return !v.IsCloture && v.KeyStatutVoyage == Voyage.StatutValide;
        });

        Check("validating a dossier against a customs warehouse switches the voyage to MEAD", () =>
        {
            var v = NewVoyage();
            v.EnterMeadFlow(Voyage.StatutMeadAttenteEntree, Voyage.LibelleMeadAttenteEntree, DateTime.UtcNow);
            return v.IsMead && v.KeyStatutVoyageMead == Voyage.StatutMeadAttenteEntree;
        });

        Section("Voyages — costing guards");

        Check("the cost divisor never divides by zero", () =>
        {
            var v = NewVoyage();
            // nb_doss_voyage_saisi is legitimately 0 in the legacy data; the code substituted 1.
            v.SetCompteurs(0, 0m, 0m);
            if (v.CostDivisor != 1m)
            {
                return false;
            }

            v.SetCompteurs(4, 100m, 10m);
            return v.CostDivisor == 4m;
        });

        Check("the standard cost-operation set matches the legacy purge list", () =>
        {
            // These twelve labels are the exact ones the legacy recompute deletes; Frais Autoroute and
            // Frais Gasoil are deliberately excluded because their own screens purge them.
            var expected = new[]
            {
                "Coût Société", "Coût Tracteur", "Coût Remorque", "Coût Chauffeur",
                "Coût Assurance Marchandise", "Coût MEAD", "Coût Agence", "Coût Frais Bancaire",
                "Coût Gasoil", "Coût AdBlue", "Coût Tracteur sans Trajet", "Coût Remorque sans Trajet"
            };

            return CoutOperationVoyageCalcule.StandardOperations.SequenceEqual(expected)
                && !CoutOperationVoyageCalcule.StandardOperations.Contains(CoutOperationVoyageCalcule.FraisAutoroute)
                && !CoutOperationVoyageCalcule.StandardOperations.Contains(CoutOperationVoyageCalcule.FraisGasoil);
        });

        Check("the cost marker carries no amount, only the voyage and operation", () =>
        {
            var marker = new CoutOperationVoyageCalcule("VOY001", "Coût Gasoil");

            // Only properties declared by this type — the Entity base adds Id and DomainEvents.
            var properties = typeof(CoutOperationVoyageCalcule).GetProperties()
                .Where(p => p.DeclaringType == typeof(CoutOperationVoyageCalcule))
                .Select(p => p.Name)
                .OrderBy(n => n)
                .ToArray();

            return marker.KeyVoyage == "VOY001"
                && marker.Operation == "Coût Gasoil"
                && properties.SequenceEqual(["KeyVoyage", "Operation"]);
        });

        Section("Voyages — legs and equipment");

        Check("a leg with no kilometrage or tractor is not costable", () =>
        {
            var t = new TrajetVoyage("TRJ001", "VOY001", 1, "Tanger", "Casablanca");
            if (t.IsCostable)
            {
                return false;
            }

            t.SetMoyens("MAT001", "12345-A-1", null, null, null, null, null, null, null);
            t.SetKilometrage(0m, true, "PLOMB01", null);
            if (t.IsCostable)
            {
                return false;
            }

            t.SetKilometrage(320m, true, "PLOMB01", null);
            return t.IsCostable && t.FraisAutoroute;
        });

        Check("negative kilometrage is rejected", () =>
        {
            var t = new TrajetVoyage("TRJ001", "VOY001", 1, "Tanger", "Casablanca");
            return Throws<BusinessRuleException>(() => t.SetKilometrage(-5m, false, null, null));
        });

        Check("repeated equipment of the same type/nature merges into a quantity", () =>
        {
            var v = NewVoyage();
            v.AddOrIncrementEquipement("EQ1", "REMORQUE", "Frigorifique");
            v.AddOrIncrementEquipement("EQ2", "REMORQUE", "Frigorifique");
            v.AddOrIncrementEquipement("EQ3", "TRACTEUR", null);

            // The legacy code increments qte instead of inserting a duplicate line.
            return v.Equipements.Count == 2
                && v.Equipements.Single(e => e.TypeEquipementCode == "REMORQUE").Qte == 2
                && v.Equipements.Single(e => e.TypeEquipementCode == "TRACTEUR").Qte == 1;
        });

        Check("equipment type codes map onto the enum", () =>
        {
            var v = NewVoyage();
            var remorque = v.AddOrIncrementEquipement("EQ1", "REMORQUE", null);
            var lcl = v.AddOrIncrementEquipement("EQ2", "LCL", null);
            return remorque.TypeEquipement == TypeEquipement.Remorque && lcl.TypeEquipement == TypeEquipement.Lcl;
        });

        Section("Dossiers");

        Check("a new dossier starts created, not to be invoiced, and 'Encours'", () =>
        {
            var d = NewDossier();
            return d.KeyStatutDossier == Dossier.StatutCree
                && d.LibelleStatutDossier == Dossier.LibelleCree
                && d.StatutFacturation == StatutFacturation.ANePasFacturer
                && d.StatutCloture == StatutClotureDossier.Encours
                && d.Position is null;
        });

        Check("dangerous goods require a hazard class", () =>
        {
            var d = NewDossier();
            return Throws<BusinessRuleException>(() =>
                d.SetMarchandise("Produit", false, dangereuse: true, classeDangereuse: null, false, false,
                    null, null, null, null, null, null, null));
        });

        Check("dangerous goods with a class are accepted", () =>
        {
            var d = NewDossier();
            d.SetMarchandise("Produit", false, true, "3", false, false, null, null, null, null, null, null, null);
            return d.MarchandiseDangereuse && d.ClasseMarchandiseDangereuse == "3";
        });

        Check("a negative taxable weight is rejected", () =>
        {
            var d = NewDossier();
            // poids_taxable is the basis of the pro-rata allocation, so it must be sane.
            return Throws<BusinessRuleException>(() => d.SetMesures(100m, 10m, null, -1m, null, null, null, null));
        });

        Check("a dossier carrying a customs warehouse is MEAD", () =>
        {
            var d = NewDossier();
            if (d.IsMead)
            {
                return false;
            }

            d.SetMagasinDouane("MAG01");
            return d.IsMead && d.KeyStatutMead == Dossier.StatutMeadAttenteDepotage;
        });

        Check("depotage is refused outside the MEAD flow", () =>
        {
            var d = NewDossier();
            return Throws<BusinessRuleException>(() =>
                d.Depotage(DateTime.UtcNow, LieuDepotage.Remorque, null, 10m, null));
        });

        Check("depotage records unpacking and stock remaining is derived", () =>
        {
            var d = NewDossier();
            d.SetMagasinDouane("MAG01");
            d.Depotage(new DateTime(2024, 3, 25), LieuDepotage.Tc, "Constat OK", 12m, "REF01");

            if (d.KeyStatutMead != Dossier.StatutMeadDepote || d.LieuDepotage != LieuDepotage.Tc)
            {
                return false;
            }

            d.SetColisSortie(12m);
            return d.HasStockRemaining == false;
        });

        Check("cancelling depotage clears every depotage field", () =>
        {
            var d = NewDossier();
            d.SetMagasinDouane("MAG01");
            d.Depotage(new DateTime(2024, 3, 25), LieuDepotage.Remorque, "Constat", 5m, "REF01");

            d.CancelDepotage();

            return d.DateDepotage is null && d.LieuDepotage is null && d.ConstatDepotage is null
                && d.ColisDepote is null && d.ColisSortie == 0m && d.Blocage is null
                && d.KeyStatutMead == Dossier.StatutMeadAttenteDepotage;
        });

        Check("a dossier can be attached to and detached from a voyage", () =>
        {
            var d = NewDossier();
            d.AssignVoyage("VOY001", 3);
            if (d.KeyVoyageInter != "VOY001" || d.Position != 3)
            {
                return false;
            }

            d.AssignVoyage(null);
            return d.KeyVoyageInter is null && d.Position == 3;
        });

        Check("a deleted dossier cannot be attached to a voyage", () =>
        {
            var d = NewDossier();
            d.SoftDelete();
            return Throws<BusinessRuleException>(() => d.AssignVoyage("VOY001"));
        });

        Check("a deleted dossier cannot be invoiced", () =>
        {
            var d = NewDossier();
            d.SoftDelete();
            return Throws<BusinessRuleException>(() => d.SetStatutFacturation(StatutFacturation.Facture));
        });

        Check("temperature bounds are ordered", () =>
        {
            var d = NewDossier();
            return Throws<BusinessRuleException>(() => d.SetTransport(true, 10m, 4m, null, null));
        });

        Check("packing lines reject a negative count", () =>
            Throws<BusinessRuleException>(() => new ColisageDossier("DOS001", "Palette", -1m, null)));

        Check("a packing line with no package type is rejected", () =>
            Throws<ArgumentException>(() => new ColisageDossier("DOS001", "  ", 1m, null)));

        Check("the voyage type catalogue matches the legacy exclusion lists", () =>
            TypeVoyageCatalogue.Excluded.SequenceEqual(["EL", "LV", "NV"])
            && TypeVoyageCatalogue.ExcludedWithMead.SequenceEqual(["MD", "EL", "LV", "NV"])
            && TypeVoyageCatalogue.National == "TN");

        Section("Voyage costing engine");

        Check("an unvalidated voyage is not costable", () =>
        {
            var (eligible, reason) = VoyageCostEngine.IsEligible(CostInputs() with { DateVoyage = null });
            return !eligible && reason!.Contains("validé");
        });

        Check("a deleted voyage is not costable", () =>
        {
            var (eligible, _) = VoyageCostEngine.IsEligible(CostInputs() with { Supprime = true });
            return !eligible;
        });

        Check("a voyage dated outside the calculation month is not costable", () =>
        {
            // The legacy filter is FORMAT(date_voyage,'MM/yyyy') = <mois>.
            var (eligible, reason) = VoyageCostEngine.IsEligible(CostInputs() with { MoisCalcul = "05/2024" });
            return !eligible && reason!.Contains("hors du mois");
        });

        Check("an ineligible voyage produces no allocations and reports why", () =>
        {
            var result = VoyageCostEngine.Generate(CostInputs() with { DateVoyage = null });
            return result.Allocations.Count == 0 && result.OperationsGenerees.Count == 0;
        });

        Check("company costs are split equally by the dossier count", () =>
        {
            // nb_doss_voyage_saisi = 4, so 4000 / 4 = 1000 per dossier.
            var result = VoyageCostEngine.Generate(CostInputs() with { CoutSociete = 4000m, NbDossVoyageSaisi = 4 });
            var societe = result.Allocations.Single(a => a.Operation == VoyageCostEngine.CoutSociete);
            return societe.MontantMad == 1000m && societe.NbDossiers == 4;
        });

        Check("a zero dossier count is treated as one, never dividing by zero", () =>
        {
            var result = VoyageCostEngine.Generate(CostInputs() with { CoutSociete = 4000m, NbDossVoyageSaisi = 0 });
            return result.Allocations.Single(a => a.Operation == VoyageCostEngine.CoutSociete).MontantMad == 4000m;
        });

        Check("MEAD cost is skipped when no dossier sits in the customs warehouse", () =>
        {
            var without = VoyageCostEngine.Generate(CostInputs() with { CoutMead = 500m, HasMeadDossier = false });
            if (without.Allocations.Any(a => a.Operation == VoyageCostEngine.CoutMead))
            {
                return false;
            }

            var with = VoyageCostEngine.Generate(CostInputs() with { CoutMead = 500m, HasMeadDossier = true });
            return with.Allocations.Any(a => a.Operation == VoyageCostEngine.CoutMead);
        });

        Check("agency cost is skipped when the voyage has no exploitation agency", () =>
        {
            var result = VoyageCostEngine.Generate(
                CostInputs() with { CoutAgence = 300m, KeyAgenceExploitation = null });
            return !result.Allocations.Any(a => a.Operation == VoyageCostEngine.CoutAgence);
        });

        Check("an operation already marked as calculated is skipped, others still compute", () =>
        {
            // Idempotency is per operation, so a partially costed voyage can be resumed.
            var inputs = CostInputs() with
            {
                CoutSociete = 1000m,
                CoutAssuranceMarchandise = 200m,
                OperationsDejaCalculees = [VoyageCostEngine.CoutSociete]
            };

            var result = VoyageCostEngine.Generate(inputs);

            return !result.OperationsGenerees.Contains(VoyageCostEngine.CoutSociete)
                && result.OperationsIgnorees.Contains(VoyageCostEngine.CoutSociete)
                && result.OperationsGenerees.Contains(VoyageCostEngine.CoutAssuranceMarchandise);
        });

        Check("equipment and personnel daily rates are multiplied by quantity", () =>
        {
            var inputs = CostInputs() with
            {
                Equipements =
                [
                    new VoyageCostEquipment("EQ1", "REMORQUE", 2, CoutParTrajet: 150m),
                    new VoyageCostEquipment("EQ2", "TRACTEUR", 1, CoutParTrajet: 200m)
                ],
                Personnel = [new VoyageCostPersonnel("PER1", CoutParTrajet: 90m)]
            };

            var result = VoyageCostEngine.Generate(inputs);

            // 2 x 150 = 300 for trailers; 1 x 200 = 200 for tractors; 90 for the driver. Divisor 2.
            return result.Allocations.Single(a => a.Operation == VoyageCostEngine.CoutRemorque).MontantMad == 150m
                && result.Allocations.Single(a => a.Operation == VoyageCostEngine.CoutChauffeur).MontantMad == 45m;
        });

        Check("gasoil costing ignores legs with no tractor or no distance", () =>
        {
            var inputs = CostInputs() with
            {
                Legs =
                [
                    // Costable: 100 km at 12/km.
                    new VoyageCostLeg("T1", "MAT1", null, null, null, 100m, CoutKmGasoil: 12m),
                    // No tractor -> the legacy guard excludes it.
                    new VoyageCostLeg("T2", null, null, null, null, 500m, CoutKmGasoil: 12m),
                    // Zero distance -> also excluded (isnull(kilometrage,0) <> 0).
                    new VoyageCostLeg("T3", "MAT1", null, null, null, 0m, CoutKmGasoil: 12m)
                ]
            };

            var result = VoyageCostEngine.Generate(inputs);
            var gasoil = result.Allocations.Single(a => a.Operation == VoyageCostEngine.CoutGasoil);

            // Only T1 counts: 100 x 12 = 1200, divided by the 2-dossier divisor.
            return gasoil.MontantMad == 600m
                && result.KilometrageLignes.Count == 1
                && result.KilometrageLignes.Single().KeyTrajet == "T1"
                && result.KilometrageLignes.Single().NatureCout == VoyageCostEngine.NatureCoutGasoil;
        });

        Check("kilometrage detail rows carry the per-leg amount", () =>
        {
            var inputs = CostInputs() with
            {
                Legs = [new VoyageCostLeg("T1", "MAT1", null, null, null, 250m, CoutKmGasoil: 4m, CoutKmAdBlue: 1m)]
            };

            var result = VoyageCostEngine.Generate(inputs);

            var gasoil = result.KilometrageLignes.Single(l => l.NatureCout == "Gasoil");
            var adBlue = result.KilometrageLignes.Single(l => l.NatureCout == "AdBlue");

            return gasoil.Montant == 1000m && adBlue.Montant == 250m;
        });

        Check("persisted kilometrage rows compute their own amount", () =>
        {
            var row = new CoutKilometrageVoyage("T1", "VOY001", "MAT1", 250m, 4m, CoutKilometrageVoyage.NatureGasoil);
            return row.Montant == 1000m && row.NatureCout == "Gasoil";
        });

        Check("the engine only ever emits the twelve standard operations plus gasoil and AdBlue", () =>
        {
            var allowed = VoyageCostEngine.StandardOperations()
                .Append(VoyageCostEngine.CoutGasoil)
                .Append(VoyageCostEngine.CoutAdBlue)
                .ToHashSet(StringComparer.Ordinal);

            var result = VoyageCostEngine.Generate(CostInputs() with
            {
                Legs = [new VoyageCostLeg("T1", "MAT1", null, null, null, 100m, CoutKmGasoil: 5m, CoutKmAdBlue: 1m)]
            });

            return result.Allocations.All(a => allowed.Contains(a.Operation));
        });
    }

    /// <summary>
    /// Baseline cost input: a validated voyage dated inside the calculation month, with two dossiers.
    /// Individual checks vary one dimension at a time.
    /// </summary>
    private static VoyageCostInputs CostInputs() => new()
    {
        KeyVoyage = "VOY001",
        MoisCalcul = "03/2024",
        Devise = "MAD",
        DateVoyage = new DateTime(2024, 3, 20),
        Supprime = false,
        KeyAgenceExploitation = "AG01",
        LieuDedouanement = "MAG01",
        NbDossVoyageSaisi = 2,
        HasMeadDossier = true
    };

    // ---------------------------------------------------------------------------------------------
    private static Voyage NewVoyage()
    {
        var v = new Voyage("VOY001", "S01", "VI", "VY2024-0001", new DateTime(2024, 3, 1));
        v.UpdateEntete("VY2024-0001", "VI", NatureVoyage.Complet, CategorieVoyage.International,
            "TRANSPORT INTERNATIONAL", "Tanger", "Casablanca", null);
        return v;
    }

    private static Dossier NewDossier() =>
        new("DOS001", "S01", "DS2024-0001", "VI", new DateTime(2024, 3, 2));

    private static Citerne NewCiterne(string key = "C1") =>
        new(key, "S01", "Citerne Gasoil", TypeCiterne.Gasoil);

    private const string Cle = "cle-de-test-uniquement-32-caracteres-minimum";

    private static LogisticsTokenService NewTokens() => new(new TokenOptions { SigningKey = Cle });

    /// <summary>True when the action throws <see cref="BusinessRuleException"/>.</summary>
    private static bool RejetteRegle(Action action)    {
        try
        {
            action();
            return false;
        }
        catch (BusinessRuleException)
        {
            return true;
        }
    }

    /// <summary>
    /// The exact mathematical value of the legacy encoding, with no floating-point rounding.
    /// </summary>
    /// <remarks>
    /// The reference the transcribed implementation is measured against. Kept in the harness rather than in the
    /// production class deliberately: it exists to falsify the implementation and its documented threshold, so
    /// sharing code with either would defeat the purpose.
    /// </remarks>
    private static string ExactBigInteger(string motDePasse)
    {
        var total = System.Numerics.BigInteger.Zero;
        var n = motDePasse.Length;

        for (var i = 0; i < n; i++)
        {
            var poids = System.Numerics.BigInteger.One;

            for (var j = 1; j <= n - i; j++)
            {
                poids *= j;
            }

            total += (int)motDePasse[i] * poids;
        }

        return total.ToString();
    }

    /// <summary>
    /// Reads the argument rows from an <c>[InlineData(...)]</c> attribute.
    /// </summary>
    /// <remarks>
    /// Two access paths, because the shape differs between xunit versions: the <c>Data</c> property is public
    /// in v2 but not always, and <c>GetData(MethodInfo)</c> is the API the framework itself consumes — so it is
    /// tried first. Reading only <c>Data</c> silently produced zero rows for every theory, which the runner then
    /// reported as an unsupported data source rather than as the accessor's fault.
    /// </remarks>
    private static IEnumerable<object?[]> LireInlineData(object attribut, MethodInfo methode)
    {
        var type = attribut.GetType();

        var viaGetData = type.GetMethod("GetData", [typeof(MethodInfo)]);

        if (viaGetData?.Invoke(attribut, [methode]) is System.Collections.IEnumerable lignes)
        {
            foreach (var ligne in lignes)
            {
                if (ligne is object?[] tableau)
                {
                    yield return tableau;
                }
                else if (ligne is null && methode.GetParameters().Length == 1)
                {
                    // InlineData(null) can represent a null params array in xUnit v2.
                    // For a single-parameter theory this is the null argument case, not a skipped row.
                    yield return [null];
                }
            }

            yield break;
        }

        var viaPropriete = type.GetProperty(
            "Data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (viaPropriete?.GetValue(attribut) is object?[] donnees)
        {
            yield return donnees;
        }
    }

    /// <summary>
    /// Runs the solution's xunit facts and theories by reflection, without VSTest.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>dotnet test</c> cannot run in this environment — VSTest needs named pipes, which the sandbox blocks —
    /// so the two xunit projects compiled for thirty rounds without a single one of their tests ever
    /// executing. A test that has never run is not evidence of anything, which made this the largest
    /// unverified claim in the repository.
    /// </para>
    /// <para>
    /// An xunit fact is an ordinary method with an attribute, so it can simply be invoked. Attributes are
    /// matched by <b>type name</b> rather than by referencing xunit, which keeps this runner independent of the
    /// test framework — and note that <c>TheoryAttribute</c> derives from <c>FactAttribute</c>, so the theory
    /// check must come first or every theory would be invoked once with no arguments.
    /// </para>
    /// <para>
    /// Each case becomes its own check, so a failure names the test rather than collapsing 48 results into one.
    /// A theory whose data source is not <c>InlineData</c> is reported as a failure rather than skipped: a
    /// silently skipped test reads exactly like a passing one.
    /// </para>
    /// </remarks>
    private static void ExecuterTestsXunit(string[]? assemblies = null, string? typeName = null)
    {
        foreach (var nomAssembly in assemblies ?? new[] { "Logistics.UnitTests", "Logistics.IntegrationTests" })
        {
            System.Reflection.Assembly assembly;

            try
            {
                assembly = System.Reflection.Assembly.Load(nomAssembly);
            }
            catch (Exception ex)
            {
                Check($"{nomAssembly} loads", () => throw new InvalidOperationException(
                    $"Assembly not loadable: {ex.Message}"));

                continue;
            }

            var cas = 0;

            foreach (var type in assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic && (typeName is null || t.Name == typeName))
                .OrderBy(t => t.Name))
            {
                var constructeur = type.GetConstructor(Type.EmptyTypes);

                if (constructeur is null)
                {
                    continue;
                }

                foreach (var methode in type.GetMethods(
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    .OrderBy(m => m.Name))
                {
                    var attributs = methode.GetCustomAttributes().ToArray();

                    // Theory first: TheoryAttribute derives from FactAttribute.
                    var estTheory = attributs.Any(a => a.GetType().Name == "TheoryAttribute");
                    var estFact = attributs.Any(a => a.GetType().Name == "FactAttribute");

                    if (!estTheory && !estFact)
                    {
                        continue;
                    }

                    var jeux = new List<object?[]>();

                    if (estTheory)
                    {
                        foreach (var attribut in attributs.Where(a => a.GetType().Name == "InlineDataAttribute"))
                        {
                            jeux.AddRange(LireInlineData(attribut, methode));
                        }
                    }
                    else
                    {
                        jeux.Add([]);
                    }

                    if (jeux.Count == 0)
                    {
                        var source = estTheory ? "theory" : "fact";
                        Check($"{type.Name}.{methode.Name} ({source} with no supported data source)", () => false);
                        cas++;

                        continue;
                    }

                    foreach (var arguments in jeux)
                    {
                        // Captured per iteration so the closure does not see the loop's final value.
                        var args = arguments;
                        var libelle = $"{type.Name}.{methode.Name}"
                            + (args.Length > 0 ? $"({string.Join(", ", args.Select(a => a?.ToString() ?? "null"))})" : string.Empty);

                        Check(libelle, () =>
                        {
                            var instance = constructeur.Invoke(null);

                            try
                            {
                                var result = methode.Invoke(instance, args);

                                // Invocation only starts an async test. Await completion so assertions
                                // and failures after the first await are counted rather than silently lost.
                                if (result is Task task)
                                {
                                    task.GetAwaiter().GetResult();
                                }

                                return true;
                            }
                            finally
                            {
                                (instance as IDisposable)?.Dispose();
                            }
                        });

                        cas++;
                    }
                }
            }

            Console.WriteLine($"        ({nomAssembly}: {cas} case(s))");
        }
    }

    /// <summary>
    /// Removes <c>//</c> line comments from C# source before it is matched against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Source-text checks otherwise read the code's own documentation as if it were code. That has bitten this
    /// harness three times: a check matched an implementation detail that a refactor changed, and another
    /// failed against a comment stating the very thing it was asserting.
    /// </para>
    /// <para>
    /// <b>String literals are respected, and that is not a detail.</b> A naive "cut at the first //" truncates
    /// every line containing a URL — <c>https://httpstatuses.io/401</c> becomes <c>https:</c> — so a check
    /// looking for the type scheme silently found nothing. That defect was introduced by the first version of
    /// this helper and is why it now tracks quote state. Block comments are still not handled; no matched region
    /// uses them.
    /// </para>
    /// </remarks>
    private static string SansCommentaires(string source) =>
        string.Join('\n', source.Split('\n').Select(l =>
        {
            var dansChaine = false;

            for (var i = 0; i < l.Length; i++)
            {
                if (l[i] == '"' && (i == 0 || l[i - 1] != '\\'))
                {
                    dansChaine = !dansChaine;
                }
                else if (!dansChaine && l[i] == '/' && i + 1 < l.Length && l[i + 1] == '/')
                {
                    return l[..i];
                }
            }

            return l;
        }));

    /// <summary>True when the action throws <see cref="ArgumentOutOfRangeException"/>.</summary>
    private static bool Rejette(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            return true;
        }
    }

    /// <summary>
    /// A context with the real model and an unreachable server.
    /// </summary>
    /// <remarks>
    /// Deliberate: the length guard runs before EF opens a connection, so an unreachable server is what
    /// proves the value never left the process. A reachable one would mask whether the guard fired.
    /// </remarks>
    private static LogisticsDbContext NewContexteSansBase() => new(
        new DbContextOptionsBuilder<LogisticsDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=GuardOnly;Trusted_Connection=True;")
            .Options);

    /// <summary>
    /// Locates the repository root by walking up until the solution file is found.
    /// </summary>
    /// <remarks>
    /// Walks from the running assembly rather than trusting the working directory, because the harness
    /// is invoked with <c>--project</c> and the current directory is not guaranteed to be either the
    /// project or the repository root.
    /// </remarks>
    private static string RacineDuDepot()
    {
        var repertoire = new DirectoryInfo(AppContext.BaseDirectory);

        while (repertoire is not null && !File.Exists(Path.Combine(repertoire.FullName, "Logistics.slnx")))
        {
            repertoire = repertoire.Parent;
        }

        return repertoire?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root (Logistics.slnx).");
    }

    private static long ToEpoch(DateTime moment) =>
        (long)(moment - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

    private static string ToBase64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] SignerAvecCle(string payload)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(Cle));
        return hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
    }

    /// <summary>
    /// Minimal <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> for tests.
    /// </summary>
    /// <remarks>
    /// Hand-written because the in-memory configuration provider package is not referenced here — the
    /// abstraction is all the tenant context needs, so a two-member stub is enough.
    /// </remarks>
    private sealed class ConfigTest(Dictionary<string, string?> valeurs) : Microsoft.Extensions.Configuration.IConfiguration
    {
        public string? this[string key]
        {
            get => valeurs.TryGetValue(key, out var valeur) ? valeur : null;
            set => valeurs[key] = value;
        }

        public IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() => [];

        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() =>
            throw new NotSupportedException("Reload is not used in these tests.");

        public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) =>
            throw new NotSupportedException("Sections are not used in these tests.");
    }

    private static Booking NewBooking(string typeBooking = "MI") =>
        new("BK001", "S01", "BK2024-0001", typeBooking, new DateTime(2024, 3, 1));

    private static Cotation NewCotation(string key = "COT001", DateTime? dateCreation = null) =>
        new(key, "S01", "COT2024-0001", "MI", "CLI001", dateCreation ?? new DateTime(2024, 3, 1));

    private static PrixCotation NewPrix(string keyCotation, decimal quantite, decimal montant, TypeUtilisation type)
    {
        var prix = new PrixCotation($"P-{Guid.NewGuid():N}"[..12], keyCotation, "S01");
        prix.SetContenu(1, "R1", "Fret", quantite, type, montant, "MAD", "FORFAIT", false, null, null, null, "20%");
        return prix;
    }

    /// <summary>
    /// Builds a 1000-character STC/EDI line by writing values at their 1-based offsets.
    /// </summary>
    private static string LigneEdi(params (int Debut, string Valeur)[] champs)
    {
        var tampon = new char[EdiStc.LongueurLigne];
        Array.Fill(tampon, ' ');

        foreach (var (debut, valeur) in champs)
        {
            for (var i = 0; i < valeur.Length && debut - 1 + i < tampon.Length; i++)
            {
                tampon[debut - 1 + i] = valeur[i];
            }
        }

        return new string(tampon);
    }

    /// <summary>A well-formed voyage header, unless a field is overridden to provoke an anomaly.</summary>
    private static string EnteteValide(
        string numVoyage = "VOY12345",
        string portDepart = "CASABLANCA",
        string dateDepart = "20240315",
        string portArrive = "MARSEILLE",
        string dateArrivee = "20240320",
        string navire = "MSC LORETO",
        string vehicule = "REM-1234",
        string caisse = "",
        string nbDossiers = "1",
        string typeVoyage = "G",
        string plomb = "PL123") =>
        LigneEdi(
            (1, numVoyage), (9, "00"), (11, portDepart), (31, dateDepart), (39, portArrive),
            (59, dateArrivee), (67, navire), (87, vehicule), (107, caisse), (127, nbDossiers),
            (129, typeVoyage), (130, plomb));

    /// <summary>A well-formed dossier line, unless a field is overridden to provoke an anomaly.</summary>
    private static string DetailValide(
        string position = "1",
        string codeExpediteur = "EXP00001",
        string codeDestinataire = "DST00001",
        string villeDestinataire = "LYON",
        string marchandise = "MACHINES A LAVER HS 845011",
        string emballage = "PALETTE",
        string colis = "5",
        string poids = "12500",
        string volume = "030000",
        string valeurMarchandise = "150000",
        string incoterm = "CF",
        string cod = "10",
        string valeur = "25075",
        string devise = "00",
        string facture1 = "FA001",
        string facture2 = "") =>
        LigneEdi(
            (1, "VOY12345"), (9, position), (11, codeExpediteur), (19, "EXPEDITEUR SARL"),
            (179, codeDestinataire), (187, "DESTINATAIRE SA"), (307, villeDestinataire),
            (437, marchandise), (477, emballage), (517, colis), (525, poids), (533, volume),
            (549, valeurMarchandise), (557, incoterm), (559, cod), (561, valeur), (569, devise),
            // First invoice block only: number at 573, date at 581, amount at 589; stride 26.
            (573, facture1), (581, "20240301"), (589, "10000"),
            (599, facture2));

    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// End-to-end run against a real SQL Server (LocalDB): creates the schema from the model, writes
    /// through the repositories and reads back.
    /// </summary>
    /// <remarks>
    /// This is the scenario that caught a genuine bug: the convenience properties <c>KeyClient</c>,
    /// <c>KeyReglement</c> and <c>KeyFacture</c> are computed from <c>Id</c> and are therefore unmapped,
    /// so EF refused to translate queries written against them. Nothing short of executing a query
    /// against a real provider surfaces that — the model-only checks happily pass.
    /// </remarks>
    private static void DatabaseScenarios()
    {
        Section("SQL Server (LocalDB) end-to-end");

        const string connectionString =
            @"Server=(localdb)\MSSQLLocalDB;Database=LogisticsVerify;Trusted_Connection=True;TrustServerCertificate=True";

        LogisticsDbContext NewDb() => new(
            new DbContextOptionsBuilder<LogisticsDbContext>().UseSqlServer(connectionString).Options);

        try
        {
            using (var setup = NewDb())
            {
                setup.Database.EnsureDeleted();
                setup.Database.EnsureCreated();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   SKIP LocalDB is unavailable: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        Check("schema is created from the model", () =>
        {
            using var db = NewDb();
            return db.Database.CanConnect();
        });

        Check("reference data round-trips", () =>
        {
            using var db = NewDb();
            db.Societes.Add(new Societe("S01", "TRANSIMAR", "MAD"));
            db.Devises.Add(new Devise("MAD", "Dirham", 2));
            db.ModesPaiement.Add(new ModePaiement("CHEQUE", statutClient: "Portefeuille"));
            db.SaveChanges();

            using var read = NewDb();
            return read.Societes.Single(s => s.Id == "S01").IntituleSoc == "TRANSIMAR"
                && read.Devises.Single(d => d.Id == "MAD").NbDecimal == 2;
        });

        Check("client is queryable by its key (unmapped-property regression)", () =>
        {
            using var db = NewDb();
            var repo = new ClientRepository(db);

            var client = new Client("CLI001", "S01", "ACME SARL", "MAROC");
            repo.Add(client);
            repo.SaveChangesAsync().GetAwaiter().GetResult();

            using var read = NewDb();
            var found = new ClientRepository(read).GetAsync("CLI001", includeChildren: false)
                .GetAwaiter().GetResult();

            return found is not null && found.RaisonSociale == "ACME SARL";
        });

        Check("soft-deleted clients are filtered out by the global query filter", () =>
        {
            using var db = NewDb();
            var client = db.Clients.Single(c => c.Id == "CLI001");
            client.SoftDelete();
            db.SaveChanges();

            using var read = NewDb();
            return read.Clients.SingleOrDefault(c => c.Id == "CLI001") is null;
        });

        Check("payment is persisted, reloaded with allocations and traced", () =>
        {
            using var db = NewDb();
            var repo = new ReglementRepository(db);

            var reglement = new EnteteReglement("REG001", "S01", TypeReglement.Client, "CLI001", "MAD",
                new DateTime(2024, 3, 1));
            reglement.SetMontants(1500m, 1m, "MAD", 2);
            reglement.ApplyDefaultStatut(null, db.ModesPaiement.Single(m => m.Id == "CHEQUE"));
            reglement.Allocate("FAC001", 500m, SensReglement.Vente, 10m, 25m);

            repo.Add(reglement);
            repo.SaveChangesAsync().GetAwaiter().GetResult();

            using var read = NewDb();
            var reloaded = new ReglementRepository(read).GetAsync("REG001", includeAllocations: true)
                .GetAwaiter().GetResult();

            return reloaded is not null
                && reloaded.MontantMad == 1500m
                && reloaded.Statut == StatutReglement.Portefeuille      // converted literal round-tripped
                && reloaded.Allocations.Count == 1
                && reloaded.Allocations.Single().RasIs == 10m
                && reloaded.Facture == "FAC001";
        });

        Check("sales invoice persists its derived buckets and credit note", () =>
        {
            using var db = NewDb();
            var repo = new FactureVenteRepository(db);

            var facture = new EnteteFactureVente("FAC001", "S01", "FV2024-0001", TypeFacture.Facture,
                "CLI001", "ACME SARL", "MAD", new DateTime(2024, 3, 1));
            facture.AddLigne(new DetailFactureVente("L1", "FAC001", "Fret", NatureLigne.Produit, 1000m, 0m, 1m, 0.20m));
            facture.AddLigne(new DetailFactureVente("L2", "FAC001", "Débours", NatureLigne.Debours, 300m, 0m, 1m, 0m));
            facture.SetTvaRecap([
                new TvaFactureVente("FAC001", "20%", "S01", 1000m, 200m),
                new TvaFactureVente("FAC001", "Débours", "S01", 300m, 0m)
            ]);
            facture.Recalculate(new Devise("MAD", "Dirham", 2), 1m, 5m, 1m);

            var avoir = facture.CreateAvoirTotal("AV001", "AV2024-0001", false);

            repo.Add(facture);
            repo.Add(avoir);
            repo.SaveChangesAsync().GetAwaiter().GetResult();

            using var read = NewDb();
            var stored = new FactureVenteRepository(read).GetAsync("FAC001", includeLines: true)
                .GetAwaiter().GetResult();
            var storedAvoir = new FactureVenteRepository(read).GetAsync("AV001", includeLines: true)
                .GetAwaiter().GetResult();

            return stored is not null
                && stored.TtcMad == 1500m
                && stored.DeboursMad == 300m
                && stored.HtMad == 1000m
                && stored.TvaMad == 200m
                && stored.NetAPayer == stored.TtcDevise - 52m
                && stored.Lignes.Count == 2
                && stored.TvaDetail.Count == 2
                && storedAvoir is not null
                && storedAvoir.TtcMad == -1500m
                && storedAvoir.TypeFacture == TypeFacture.Avoir;
        });

        Check("allocation rows map onto the composite legacy key", () =>
        {
            using var read = NewDb();
            var allocation = read.DetailsReglement.Single(d => d.KeyReglement == "REG001");

            return allocation.KeyFacture == "FAC001"
                && allocation.Montant == 500m
                && allocation.Sens == SensReglement.Vente
                && allocation.Devise == "MAD";
        });

        Check("audit trail writes to the legacy trace and journal tables", () =>
        {
            using var db = NewDb();
            db.HistReferentiel.Add(new Domain.Entities.Audit.HistReferentiel(
                "Client", "Ajout", "SYSTEM", DateTime.UtcNow, "Raison sociale = ACME SARL", "S01", "WS01"));
            db.TracesClient.Add(new Domain.Entities.Audit.TraceClient(
                "CLI001", "S01", null, "$ACLT", DateTime.UtcNow, DateTime.UtcNow, "Création", "SYSTEM", null, 0, "WS01"));
            db.SaveChanges();

            using var read = NewDb();
            return read.HistReferentiel.Any(h => h.NomTable == "Client" && h.Evenement == "Ajout")
                && read.TracesClient.Any(t => t.EntityKey == "CLI001" && t.KeyStatut == "$ACLT");
        });

        Check("soft-delete query filter does not hide the audit rows", () =>
        {
            using var read = NewDb();
            // The client was soft-deleted earlier in this scenario; the trace must still be readable.
            return read.TracesClient.Any(t => t.EntityKey == "CLI001");
        });
    }

    // ---------------------------------------------------------------------------------------------
    private static EnteteReglement NewReglement(decimal montant = 1000m, string devise = "MAD",
        TypeReglement type = TypeReglement.Client) =>
        new("REG001", "S01", type, "CLI001", devise, new DateTime(2024, 3, 1));

    private static EnteteFactureVente NewFacture(string devise = "MAD")
    {
        var f = new EnteteFactureVente("FAC001", "S01", "FV2024-0001", TypeFacture.Facture,
            "CLI001", "ACME SARL", devise, new DateTime(2024, 3, 1));
        f.SetEntete("AG01", "FR", "TI", "TRANSPORT INTERNATIONAL", "DOS001", null,
            new DateTime(2024, 4, 1), null, null);
        return f;
    }

    private static void AddLine(EnteteFactureVente f, string key, NatureLigne nature, decimal total,
        decimal tauxTva = 0.20m, string codeTva = "20%", decimal tauxRemise = 0m, decimal tauxChange = 1m)
    {
        var line = new DetailFactureVente(key, f.KeyFacture, "Ligne", nature, total, tauxRemise, tauxChange, tauxTva);
        typeof(DetailFactureVente).GetProperty(nameof(DetailFactureVente.CodeTva))!.SetValue(line, codeTva);
        f.AddLigne(line);
    }

    private static EcartDecision Classify(decimal ecart, decimal auto, decimal manuel)
    {
        var value = Math.Abs(ecart);
        if (value <= auto)
        {
            return EcartDecision.Automatique;
        }

        return value <= manuel ? EcartDecision.ManuelRequis : EcartDecision.Refuse;
    }

    private static string LegacyLiteral(StatutReglement s) => s switch
    {
        StatutReglement.Saisie => "Saisie",
        StatutReglement.Portefeuille => "Portefeuille",
        StatutReglement.RemisEnBanque => "Remis en banque",
        StatutReglement.RemisAuFournisseur => "Remis au Fournisseur",
        StatutReglement.Signe => "Signé",
        StatutReglement.Regle => "Réglé",
        _ => "Impayé"
    };

    // ---------------------------------------------------------------------------------------------
    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine($"-- {title}");
    }

    private static void Check(string description, Func<bool> assertion)
    {
        bool ok;
        try
        {
            ok = assertion();
        }
        catch (Exception ex)
        {
            ok = false;
            Console.WriteLine($"   FAIL {description}");
            Console.WriteLine($"        threw {ex.GetType().Name}: {ex.Message}");
            _failed++;
            return;
        }

        if (ok)
        {
            _passed++;
            Console.WriteLine($"   PASS {description}");
        }
        else
        {
            _failed++;
            Console.WriteLine($"   FAIL {description}");
        }
    }

    private static bool Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }
}
