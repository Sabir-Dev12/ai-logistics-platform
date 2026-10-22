using Logistics.Domain.Common;
using Logistics.Domain.Entities.Invoicing;
using Logistics.Domain.Entities.ReferenceData;
using Logistics.Domain.Enums;

namespace Logistics.UnitTests;

/// <summary>
/// Tests for the sales-invoice header derivation and the full credit note ("avoir total").
/// </summary>
public sealed class FactureVenteTests
{
    private static readonly Devise Mad = new("MAD", "Dirham", 2);

    private static EnteteFactureVente NewFacture(string devise = "MAD")
    {
        var facture = new EnteteFactureVente("FAC001", "S01", "FV2024-0001", TypeFacture.Facture,
            "CLI001", "ACME SARL", devise, new DateTime(2024, 3, 1));
        facture.SetEntete("AG01", "FR", "TI", "TRANSPORT INTERNATIONAL", "DOS001", null,
            new DateTime(2024, 4, 1), null, null);
        return facture;
    }

    private static void AddLine(
        EnteteFactureVente facture, string key, NatureLigne nature, decimal total, decimal tauxTva = 0.20m,
        decimal tauxRemise = 0m, decimal tauxChange = 1m, string codeTva = "20%")
    {
        var line = new DetailFactureVente(key, facture.KeyFacture, "Ligne", nature, total, tauxRemise, tauxChange, tauxTva);
        typeof(DetailFactureVente).GetProperty(nameof(DetailFactureVente.CodeTva))!.SetValue(line, codeTva);
        facture.AddLigne(line);
    }

    [Fact]
    public void Header_buckets_are_derived_from_the_lines()
    {
        var facture = NewFacture();
        AddLine(facture, "L1", NatureLigne.Produit, 1000m);   // 20% VAT
        AddLine(facture, "L2", NatureLigne.Debours, 300m, tauxTva: 0m, codeTva: "Débours");
        AddLine(facture, "L3", NatureLigne.Assigne, 200m, tauxTva: 0m, codeTva: "0%");
        AddLine(facture, "L4", NatureLigne.TaxeRegionale, 50m, tauxTva: 0m, codeTva: "0%");

        facture.SetTvaRecap([
            new TvaFactureVente(facture.KeyFacture, "20%", "S01", 1000m, 200m),
            new TvaFactureVente(facture.KeyFacture, "Débours", "S01", 300m, 0m)
        ]);

        facture.Recalculate(Mad, 1m, 0m, 0m);

        Assert.Equal(1000m, facture.HtMad);        // only the "Produit" bucket
        Assert.Equal(300m, facture.DeboursMad);
        Assert.Equal(200m, facture.AssigneMad);
        Assert.Equal(50m, facture.TaxeRegionale);
        Assert.Equal(200m, facture.TvaMad);
        Assert.Equal(1750m, facture.TtcMad);
        Assert.Equal(1750m, facture.TotalHtMad + facture.TvaMad);
    }

    [Fact]
    public void Withholding_reduces_the_net_payable()
    {
        var facture = NewFacture();
        AddLine(facture, "L1", NatureLigne.Produit, 1000m);
        facture.SetTvaRecap([new TvaFactureVente(facture.KeyFacture, "20%", "S01", 1000m, 200m)]);

        // 5% withholding on the ex-tax base, 1% on the VAT.
        facture.Recalculate(Mad, 1m, tauxRasIs: 5m, tauxRasTva: 1m);

        Assert.Equal(50m, facture.RasIs);   // 1000 * 5%
        Assert.Equal(2m, facture.RasTva);   // 200 * 1%
        Assert.Equal(facture.TtcDevise - 52m, facture.NetAPayer);
    }

    [Fact]
    public void Withholding_is_skipped_when_the_company_configured_no_rates()
    {
        var facture = NewFacture();
        AddLine(facture, "L1", NatureLigne.Produit, 1000m);
        facture.SetTvaRecap([new TvaFactureVente(facture.KeyFacture, "20%", "S01", 1000m, 200m)]);

        facture.Recalculate(Mad, 1m, tauxRasIs: 0m, tauxRasTva: 0m);

        Assert.Equal(0m, facture.RasIs);
        Assert.Equal(0m, facture.RasTva);
        Assert.Equal(facture.TtcDevise, facture.NetAPayer);
    }

    [Fact]
    public void Discount_is_applied_before_the_currency_conversion()
    {
        var facture = NewFacture("EUR");

        // 1000 gross less 10% = 900 net; at 11 EUR->MAD that is 9900 MAD.
        AddLine(facture, "L1", NatureLigne.Produit, 1000m, tauxTva: 0m, tauxRemise: 10m, tauxChange: 11m, codeTva: "0%");
        facture.SetTvaRecap([new TvaFactureVente(facture.KeyFacture, "0%", "S01", 9900m, 0m)]);

        facture.Recalculate(new Devise("EUR", "Euro", 2), 11m, 0m, 0m);

        Assert.Equal(900m, facture.Lignes.Single().MtDevise);
        Assert.Equal(9900m, facture.Lignes.Single().MtHtMad);
        Assert.Equal(9900m, facture.TtcMad);
        Assert.Equal(900m, facture.TtcDevise);
    }

    [Fact]
    public void Exoneration_zeroes_every_rate_and_keeps_the_original_for_detection()
    {
        var facture = NewFacture();
        AddLine(facture, "L1", NatureLigne.Produit, 1000m, tauxTva: 0.20m);
        facture.SetTvaRecap([new TvaFactureVente(facture.KeyFacture, "20%", "S01", 1000m, 200m)]);

        facture.ApplyExoneration("Zone Franche");

        var line = facture.Lignes.Single();
        Assert.Equal(0m, line.TauxTva);
        Assert.Equal(0.20m, line.TauxTvaInit);
        Assert.True(line.IsExonerationEffective);
        Assert.Equal("Zone Franche", facture.CommentaireExoneration);
        Assert.Equal(0m, facture.TvaDetail.Single().Tva);
    }

    [Fact]
    public void Full_credit_note_negates_every_bucket_and_rekeys_the_lines()
    {
        var facture = NewFacture();
        AddLine(facture, "L1", NatureLigne.Produit, 1000m);
        AddLine(facture, "L2", NatureLigne.Debours, 300m, tauxTva: 0m, codeTva: "Débours");
        facture.SetTvaRecap([new TvaFactureVente(facture.KeyFacture, "20%", "S01", 1000m, 200m)]);
        facture.Recalculate(Mad, 1m, 0m, 0m);

        var avoir = facture.CreateAvoirTotal("AV001", "AV2024-0001", avoirTotalFactRegle: false);

        Assert.Equal(TypeFacture.Avoir, avoir.TypeFacture);
        Assert.Equal(-facture.TtcMad, avoir.TtcMad);
        Assert.Equal(-facture.HtMad, avoir.HtMad);
        Assert.Equal(-facture.DeboursMad, avoir.DeboursMad);
        Assert.Equal(-facture.NetAPayer, avoir.NetAPayer);

        // Every line is duplicated with its key re-prefixed by 'A'.
        Assert.All(avoir.Lignes, l => Assert.StartsWith("A", l.KeyLigne));
        Assert.All(avoir.Lignes, l => Assert.True(l.MtTotalDevise < 0));

        // The source invoice records the link back to its credit note.
        Assert.Equal("AV001", facture.KeyFactureAvoir);
        Assert.Equal("AV2024-0001", facture.NumFactureAvoir);
    }

    [Fact]
    public void Credit_note_can_be_created_as_already_settled()
    {
        var facture = NewFacture();
        AddLine(facture, "L1", NatureLigne.Produit, 1000m);
        facture.SetTvaRecap([new TvaFactureVente(facture.KeyFacture, "20%", "S01", 1000m, 200m)]);
        facture.Recalculate(Mad, 1m, 0m, 0m);

        var avoir = facture.CreateAvoirTotal("AV001", "AV2024-0001", avoirTotalFactRegle: true);

        Assert.True(avoir.FactRegle);
    }

    [Fact]
    public void Credit_note_must_come_from_a_plain_invoice()
    {
        var avoir = new EnteteFactureVente("AV001", "S01", null, TypeFacture.Avoir, "CLI001", null, "MAD", DateTime.UtcNow);

        Assert.Throws<BusinessRuleException>(() => avoir.CreateAvoirTotal("AV002", null, false));
    }

    [Fact]
    public void Only_a_plain_invoice_is_positive()
    {
        var facture = NewFacture();
        Assert.Equal(1, facture.Signe);

        var avoir = facture.CreateAvoirTotal("AV001", null, false);
        Assert.Equal(-1, avoir.Signe);
    }

    [Fact]
    public void Settlement_marks_the_invoice_as_settled_once_fully_paid()
    {
        var facture = NewFacture();
        AddLine(facture, "L1", NatureLigne.Produit, 1000m);
        facture.SetTvaRecap([new TvaFactureVente(facture.KeyFacture, "20%", "S01", 1000m, 200m)]);
        facture.Recalculate(Mad, 1m, 0m, 0m);

        facture.RegisterPayment(600m, negateForPurchase: false, DateTime.UtcNow);
        Assert.False(facture.FactRegle);

        facture.RegisterPayment(600m, negateForPurchase: false, DateTime.UtcNow);
        Assert.True(facture.FactRegle);
    }

    [Fact]
    public void Purchase_settlement_decrements_the_invoice()
    {
        var facture = NewFacture();
        AddLine(facture, "L1", NatureLigne.Produit, 1000m);
        facture.SetTvaRecap([new TvaFactureVente(facture.KeyFacture, "20%", "S01", 1000m, 200m)]);
        facture.Recalculate(Mad, 1m, 0m, 0m);

        // The supplier side is negated, matching the legacy `mt_regle = mt_regle + (-1 * montant)`.
        facture.RegisterPayment(200m, negateForPurchase: true, DateTime.UtcNow);

        Assert.Equal(-200m, facture.MtRegle);
    }

    [Fact]
    public void The_od_counter_entry_is_keyed_on_the_payment()
    {
        var od = EnteteFactureVente.CreateEcartOd("REG001", "S01", "CLI001", "EUR", 12.5m, new DateTime(2024, 3, 31));

        Assert.Equal("REG001", od.KeyFacture);
        Assert.Equal(TypeFacture.Od, od.TypeFacture);
        Assert.Equal(12.5m, od.TtcMad);
        Assert.Contains("Ecart suite", od.CommentaireFacture);
    }

    [Fact]
    public void Encours_is_the_net_payable_less_what_was_settled()
    {
        var facture = NewFacture();
        AddLine(facture, "L1", NatureLigne.Produit, 1000m);
        facture.SetTvaRecap([new TvaFactureVente(facture.KeyFacture, "20%", "S01", 1000m, 200m)]);
        facture.Recalculate(Mad, 1m, 0m, 0m);

        facture.RegisterPayment(200m, negateForPurchase: false, DateTime.UtcNow);

        Assert.Equal(facture.NetAPayer - 200m, facture.EncoursMad);
    }
}

/// <summary>
/// Tests for the two competing pro-rata rules used when a voyage cost or a sales line must be spread
/// across dossiers.
/// </summary>
public sealed class AllocationCalculatorTests
{
    [Fact]
    public void Sales_lines_split_pro_rata_to_taxable_weight()
    {
        var dossiers = new (string, decimal)[] { ("DOS1", 100m), ("DOS2", 300m) };

        var split = AllocationCalculator.SplitByPoidsTaxable(400m, 400m, dossiers);

        Assert.Equal(2, split.Count);
        Assert.Equal(100m, split.Single(s => s.KeyDossier == "DOS1").MontantMad);
        Assert.Equal(300m, split.Single(s => s.KeyDossier == "DOS2").MontantMad);
    }

    [Fact]
    public void Split_by_weight_returns_nothing_when_total_weight_is_zero()
    {
        // The caller must fall back to an equal split; the legacy code guarded this the same way.
        var split = AllocationCalculator.SplitByPoidsTaxable(400m, 400m, [("DOS1", 0m), ("DOS2", 0m)]);

        Assert.Empty(split);
    }

    [Fact]
    public void Internal_voyage_costs_split_equally()
    {
        var split = AllocationCalculator.SplitEqually(300m, 300m, ["DOS1", "DOS2", "DOS3"], nbDossVoyageSaisi: 3);

        Assert.Equal(3, split.Count);
        Assert.All(split, s => Assert.Equal(100m, s.MontantMad));
    }

    [Fact]
    public void Equal_split_does_not_divide_by_zero()
    {
        // `nb_doss_voyage_saisi` can legitimately be 0 in the legacy data; the code substituted 1.
        var split = AllocationCalculator.SplitEqually(300m, 300m, ["DOS1"], nbDossVoyageSaisi: 0);

        Assert.Equal(300m, split.Single().MontantMad);
    }

    [Fact]
    public void Equal_split_of_nothing_yields_nothing()
    {
        Assert.Empty(AllocationCalculator.SplitEqually(100m, 100m, [], nbDossVoyageSaisi: 5));
    }
}
